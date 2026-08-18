using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Threading;
using CaseManagement.Helpers;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace CaseManagement.DevCenter
{
    // ═════════════════════════════════════════════════════════════════════════
    // «گزارش سلامت سازمانی» — خروجی PDF.
    //
    // آموزش — چرا اینجا هیچ منطقِ PDF نوشته نشده است: پروژه از قبل یک زنجیرهٔ
    // کاملِ سند دارد — ساخت DOCX با OpenXML (مثل GridReportExporter) و سپس
    // تبدیل به PDF با PdfConversionHelper (اول Microsoft Word، بعد
    // LibreOffice). ساختِ یک موتور PDF دوم یعنی دو رفتار، دو باگ و دو
    // نگهداری. پس این کلاس فقط «محتوا» می‌سازد و برای قالب‌بندی و تبدیل، از
    // همان زیرساخت موجود استفاده می‌کند.
    //
    // اگر هیچ مبدلی نصب نباشد، به‌جای خطا، همان فایل Word تحویل داده می‌شود و
    // این موضوع صریحاً به کاربر گفته می‌شود — گزارشِ در دسترس بهتر از هیچ است.
    //
    // ⚠ حریم خصوصی: این گزارش عمداً «فقط سنجه» است. هیچ نام، شماره، مسیر
    // فایلِ کاربر، مقدارِ تنظیمات یا دادهٔ مددجو در آن نمی‌آید — برخلاف بستهٔ
    // پشتیبانی که لاگ خام دارد. برای همین می‌توان آن را به‌راحتی برای مدیر یا
    // ناظر فرستاد.
    // ═════════════════════════════════════════════════════════════════════════
    internal static class DevCenterHealthReport
    {
        // هشت دستهٔ خواسته‌شده + مرحلهٔ جمع‌آوری و مرحلهٔ ساخت سند.
        public const int TotalSteps = 10;

        internal sealed class CategoryScore
        {
            public string Name           = "";
            public string Status         = "";
            public int    Score;               // ۰..۱۰۰ ؛ منفی ⇒ قابل اندازه‌گیری نبود
            public int    Problems;
            public string Risk           = "";
            public string Recommendation = "";

            public string ScoreText
            {
                get { return Score < 0 ? DevCenterService.NotAvailable : Score + " / 100"; }
            }
        }

        internal sealed class HealthReportResult
        {
            public string Path        = "";
            public bool   IsPdf;
            public string Note        = "";
        }

        // ─── سطح ریسک از روی امتیاز ──────────────────────────────────────────
        private static string RiskOf(int score)
        {
            if (score < 0)  return DevCenterService.NotAvailable;
            if (score >= 85) return "کم";
            if (score >= 70) return "متوسط";
            if (score >= 50) return "بالا";
            return "بحرانی";
        }

        private static string StatusOf(int score)
        {
            if (score < 0)  return DevCenterService.NotAvailable;
            if (score >= 85) return "سالم";
            if (score >= 70) return "قابل قبول";
            if (score >= 50) return "نیازمند رسیدگی";
            return "بحرانی";
        }

        private static CategoryScore Make(string name, int score, int problems, string recommendation)
        {
            return new CategoryScore
            {
                Name = name,
                Score = score < 0 ? -1 : Math.Max(0, Math.Min(100, score)),
                Problems = problems,
                Status = StatusOf(score),
                Risk = RiskOf(score),
                Recommendation = recommendation
            };
        }

        // ═════════════════════════════════════════════════════════════════════
        // جمع‌آوری هشت دسته
        //
        // ⚠ هر دسته فقط از سنجه‌هایی ساخته می‌شود که *واقعاً اندازه‌گیری شده‌اند*.
        // اگر منبعِ یک دسته در این پایگاه‌داده نباشد، امتیاز آن «در دسترس نیست»
        // می‌شود و در میانگین هم شرکت نمی‌کند — همان قاعده‌ای که «دکتر
        // دیتابیس» از ابتدا داشت.
        // ═════════════════════════════════════════════════════════════════════
        internal static List<CategoryScore> BuildCategories(
            DevCenterService.SystemOverview overview,
            DataTable doctorRows,
            IProgress<DevCenterService.DevProgress> progress,
            CancellationToken cancel,
            ref int step)
        {
            var list = new List<CategoryScore>();

            list.Add(Step(progress, cancel, ref step, "دیتابیس",     delegate { return Database(overview, doctorRows); }));
            list.Add(Step(progress, cancel, ref step, "امنیت",        Security));
            list.Add(Step(progress, cancel, ref step, "کارایی",       delegate { return Performance(overview); }));
            list.Add(Step(progress, cancel, ref step, "ذخیره‌سازی",   Storage));
            list.Add(Step(progress, cancel, ref step, "بکاپ",         Backup));
            list.Add(Step(progress, cancel, ref step, "کیفیت داده",   delegate { return DataQuality(doctorRows); }));
            list.Add(Step(progress, cancel, ref step, "پیکربندی",     Configuration));
            list.Add(Step(progress, cancel, ref step, "محیط اجرا",    Environment_));

            return list;
        }

        private static CategoryScore Step(IProgress<DevCenterService.DevProgress> progress,
            CancellationToken cancel, ref int step, string name, Func<CategoryScore> build)
        {
            cancel.ThrowIfCancellationRequested();

            step++;
            if (progress != null)
                progress.Report(new DevCenterService.DevProgress(step, TotalSteps, "ارزیابی: " + name));

            try { return build(); }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                return new CategoryScore
                {
                    Name = name,
                    Score = -1,
                    Problems = 0,
                    Status = DevCenterService.NotAvailable,
                    Risk = DevCenterService.NotAvailable,
                    Recommendation = "این دسته ارزیابی نشد: " + ex.Message
                };
            }
        }

        // شمارشِ ردیف‌های «نیازمند بررسی» در نتیجهٔ دکتر، برای نام‌های داده‌شده.
        private static int DoctorProblems(DataTable rows, params string[] checkNames)
        {
            if (rows == null) return 0;

            int total = 0;
            foreach (DataRow row in rows.Rows)
            {
                string name = Convert.ToString(row[0]);
                if (Array.IndexOf(checkNames, name) < 0) continue;
                if (Convert.ToString(row[3]) != DevCenterService.StateAttention) continue;

                int count;
                string text = Convert.ToString(row[2]).Replace(",", "").Replace("،", "");
                total += int.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out count) ? count : 1;
            }
            return total;
        }

        // آیا این بررسی اصلاً اجرا شده است؟ (برای تفکیک «سالم» از «ناموجود»)
        private static bool DoctorMeasured(DataTable rows, params string[] checkNames)
        {
            if (rows == null) return false;

            foreach (DataRow row in rows.Rows)
            {
                if (Array.IndexOf(checkNames, Convert.ToString(row[0])) < 0) continue;

                string state = Convert.ToString(row[3]);
                if (state == DevCenterService.StateHealthy || state == DevCenterService.StateAttention)
                    return true;
            }
            return false;
        }

        // ─── ۱) دیتابیس ──────────────────────────────────────────────────────
        private static CategoryScore Database(DevCenterService.SystemOverview overview, DataTable doctorRows)
        {
            const string integrity = "یکپارچگی دیتابیس";
            const string broken    = "ارجاع‌های شکسته";

            if (!DoctorMeasured(doctorRows, integrity, broken))
                return Make("دیتابیس", -1, 0, "بررسی یکپارچگی روی این پایگاه‌داده اجرا نشد.");

            int problems = DoctorProblems(doctorRows, integrity, broken, "رکوردهای یتیم");
            bool healthy = overview != null && overview.DbStatus == "سالم";

            int score = 100;
            if (!healthy) score -= 40;
            score -= Math.Min(40, problems);

            return Make("دیتابیس", score, problems,
                problems == 0 && healthy
                    ? "وضعیت مطلوب است؛ بررسی دوره‌ای ادامه یابد."
                    : "ابتدا «دکتر دیتابیس» را اجرا و ارجاع‌های شکسته و رکوردهای یتیم را اصلاح کنید. پیش از هر اصلاح، بکاپ بگیرید.");
        }

        // ─── ۲) امنیت ────────────────────────────────────────────────────────
        private static CategoryScore Security()
        {
            // سنجه‌های واقعیِ موجود: ورودهای ناموفق اخیر و پیکربندیِ سیاست‌های
            // امنیتی. هیچ نام کاربری‌ای در گزارش نمی‌آید — فقط تعداد.
            if (!DevCenterService.TableExists("EntSecurityEvent"))
                return Make("امنیت", -1, 0, "جدول رویدادهای امنیتی در این پایگاه‌داده وجود ندارد.");

            int failedLogins = DevCenterService.CountFailedLogins(30);
            int timeout      = SettingsHelper.GetInt(SettingsHelper.SessionTimeoutMinutes, 0);
            int maxAttempts  = SettingsHelper.GetInt(SettingsHelper.MaxFailedAttempts, 0);

            int problems = 0;
            var advice = new List<string>();

            int score = 100;
            if (failedLogins > 0)
            {
                problems += failedLogins;
                score -= Math.Min(30, failedLogins);
                advice.Add("ورودهای ناموفق اخیر بررسی شوند");
            }
            if (timeout <= 0)
            {
                problems++; score -= 15;
                advice.Add("پایان خودکار نشست فعال نیست");
            }
            if (maxAttempts <= 0)
            {
                problems++; score -= 15;
                advice.Add("سقف تلاش ناموفق ورود تنظیم نشده است");
            }

            return Make("امنیت", score, problems,
                advice.Count == 0
                    ? "سیاست‌های امنیتی فعال و بدون رویداد مشکوک است."
                    : string.Join("؛ ", advice.ToArray()) + ".");
        }

        // ─── ۳) کارایی ───────────────────────────────────────────────────────
        private static CategoryScore Performance(DevCenterService.SystemOverview overview)
        {
            if (overview == null)
                return Make("کارایی", -1, 0, "نمای کلی سیستم محاسبه نشد.");

            int problems = 0;
            var advice = new List<string>();
            int score = 100;

            // آمار بهینه‌ساز: نبودِ sqlite_stat1 یعنی ANALYZE هرگز اجرا نشده.
            if (!DevCenterService.TableExists("sqlite_stat1"))
            {
                problems++; score -= 20;
                advice.Add("«به‌روزرسانی آمار (ANALYZE)» اجرا شود");
            }

            int unresolved = DevCenterService.UnresolvedErrorCount();
            if (unresolved > 0)
            {
                problems += unresolved;
                score -= Math.Min(30, unresolved);
                advice.Add(unresolved + " خطای بررسی‌نشده در لاگ خطا");
            }

            return Make("کارایی", score, problems,
                advice.Count == 0
                    ? "وضعیت کارایی مطلوب است."
                    : string.Join("؛ ", advice.ToArray()) + ".");
        }

        // ─── ۴) ذخیره‌سازی ───────────────────────────────────────────────────
        private static CategoryScore Storage()
        {
            int missingFolders = DevCenterService.CountMissingStorageFolders();
            int freePercent    = DevCenterService.GetFreeDiskPercent();

            int problems = missingFolders < 0 ? 0 : missingFolders;
            int score = 100;
            var advice = new List<string>();

            if (missingFolders > 0)
            {
                score -= Math.Min(40, missingFolders * 20);
                advice.Add(missingFolders + " مسیر پیکربندی‌شده روی دیسک وجود ندارد");
            }

            if (freePercent >= 0)
            {
                if (freePercent < 5)       { score -= 40; problems++; advice.Add("فضای آزاد دیسک بحرانی است"); }
                else if (freePercent < 15) { score -= 20; problems++; advice.Add("فضای آزاد دیسک رو به اتمام است"); }
            }

            return Make("ذخیره‌سازی", score, problems,
                advice.Count == 0
                    ? "مسیرها در دسترس و فضای دیسک کافی است."
                    : string.Join("؛ ", advice.ToArray()) + ".");
        }

        // ─── ۵) بکاپ ─────────────────────────────────────────────────────────
        private static CategoryScore Backup()
        {
            string last = SettingsHelper.Get(SettingsHelper.LastBackupDate);

            if (string.IsNullOrWhiteSpace(last))
                return Make("بکاپ", 0, 1,
                    "هیچ بکاپی ثبت نشده است. بلافاصله یک بکاپ کامل تهیه و زمان‌بندی خودکار را فعال کنید.");

            DateTime taken;
            if (!DateTime.TryParseExact(last, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                                        DateTimeStyles.None, out taken))
                return Make("بکاپ", -1, 0, "تاریخ آخرین بکاپ قابل خواندن نبود.");

            int days = (int)(DateTime.Today - taken.Date).TotalDays;

            int score = days <= 0 ? 100 : days == 1 ? 90 : days <= 7 ? 70 : days <= 30 ? 40 : 10;

            return Make("بکاپ", score, days > 1 ? 1 : 0,
                days <= 1
                    ? "بکاپ به‌روز است."
                    : "آخرین بکاپ " + days + " روز پیش گرفته شده؛ بکاپ تازه تهیه و زمان‌بندی بررسی شود.");
        }

        // ─── ۶) کیفیت داده ───────────────────────────────────────────────────
        private static CategoryScore DataQuality(DataTable doctorRows)
        {
            const string quality    = "کیفیت داده";
            const string duplicates = "رکوردهای تکراری";
            const string numbering  = "شماره‌گذاری نامعتبر";

            if (!DoctorMeasured(doctorRows, quality, duplicates, numbering))
                return Make("کیفیت داده", -1, 0, "بررسی‌های کیفیت داده روی این پایگاه‌داده اجرا نشد.");

            int problems = DoctorProblems(doctorRows, quality, duplicates, numbering, "سازگاری بارکد");
            int score = 100 - Math.Min(60, problems);

            return Make("کیفیت داده", score, problems,
                problems == 0
                    ? "دادهٔ پرونده‌ها بدون ایراد شناسایی‌شده است."
                    : "از صفحه‌های «کیفیت داده» و «موارد تکراری» برای اصلاح استفاده کنید؛ کدهای خالی یا تکراری مانع تولید بارکد می‌شوند.");
        }

        // ─── ۷) پیکربندی ─────────────────────────────────────────────────────
        private static CategoryScore Configuration()
        {
            // ⚠ فقط *وجود* کلیدها بررسی می‌شود؛ هیچ مقداری در گزارش نمی‌آید.
            string[] required =
            {
                SettingsHelper.OrgName,
                SettingsHelper.BackupPath,
                SettingsHelper.PhotoStoragePath,
                SettingsHelper.ReportsPath
            };

            int missing = 0;
            foreach (string key in required)
                if (string.IsNullOrWhiteSpace(SettingsHelper.Get(key))) missing++;

            int score = 100 - missing * 20;

            return Make("پیکربندی", score, missing,
                missing == 0
                    ? "تنظیمات پایه کامل است."
                    : missing + " تنظیم پایه (نام مؤسسه یا مسیرهای ذخیره‌سازی) خالی است؛ از صفحهٔ تنظیمات تکمیل شود.");
        }

        // ─── ۸) محیط اجرا ────────────────────────────────────────────────────
        private static CategoryScore Environment_()
        {
            int problems = 0;
            var advice = new List<string>();
            int score = 100;

            if (!System.Environment.Is64BitProcess)
            {
                problems++; score -= 10;
                advice.Add("برنامه در حالت ۳۲ بیتی اجرا می‌شود");
            }

            long workingSetMb = 0;
            try { workingSetMb = System.Diagnostics.Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024); }
            catch { }

            if (workingSetMb > 1500)
            {
                problems++; score -= 20;
                advice.Add("مصرف حافظه بالاست (" + workingSetMb + " مگابایت)");
            }

            return Make("محیط اجرا", score, problems,
                advice.Count == 0
                    ? "محیط اجرا مناسب است."
                    : string.Join("؛ ", advice.ToArray()) + ".");
        }

        // ═════════════════════════════════════════════════════════════════════
        // ساخت خروجی
        // ═════════════════════════════════════════════════════════════════════
        internal static HealthReportResult Export(string targetPath,
            IProgress<DevCenterService.DevProgress> progress, CancellationToken cancel)
        {
            int step = 0;

            cancel.ThrowIfCancellationRequested();
            if (progress != null)
                progress.Report(new DevCenterService.DevProgress(++step, TotalSteps, "جمع‌آوری سنجه‌های سیستم"));

            DevCenterService.SystemOverview overview = null;
            try { overview = DevCenterService.GetOverview(); } catch { }

            DataTable doctorRows = null;
            try { doctorRows = DevCenterService.RunDatabaseDoctor(null, cancel).Rows; }
            catch (OperationCanceledException) { throw; }
            catch { }

            List<CategoryScore> categories =
                BuildCategories(overview, doctorRows, progress, cancel, ref step);

            cancel.ThrowIfCancellationRequested();
            if (progress != null)
                progress.Report(new DevCenterService.DevProgress(TotalSteps, TotalSteps, "ساخت سند گزارش"));

            // سند ابتدا به‌صورت DOCX ساخته می‌شود (همان مسیرِ استانداردِ پروژه)
            // و سپس با زیرساخت موجود به PDF تبدیل می‌گردد.
            string docxPath = Path.ChangeExtension(targetPath, ".docx");
            WriteDocx(docxPath, overview, categories);

            var result = new HealthReportResult { Path = docxPath, IsPdf = false };

            if (!PdfConversionHelper.IsAvailable())
            {
                result.Note = "Microsoft Word یا LibreOffice روی این سیستم نصب نیست؛ گزارش به‌صورت Word ذخیره شد.";
                return result;
            }

            try
            {
                string pdfPath = PdfConversionHelper.ConvertDocxToPdf(docxPath);

                // فایل PDF در کنار DOCX ساخته می‌شود؛ اگر کاربر نام دیگری خواسته
                // بود، به همان نام منتقل می‌شود.
                if (!string.Equals(pdfPath, targetPath, StringComparison.OrdinalIgnoreCase))
                {
                    if (File.Exists(targetPath)) File.Delete(targetPath);
                    File.Move(pdfPath, targetPath);
                }

                try { File.Delete(docxPath); } catch { }

                result.Path = targetPath;
                result.IsPdf = true;
                return result;
            }
            catch (Exception ex)
            {
                // تبدیل شکست خورد ⇒ همان Word تحویل داده می‌شود، با توضیح روشن.
                result.Note = "تبدیل به PDF انجام نشد (" + ex.Message + "). گزارش به‌صورت Word ذخیره شد.";
                return result;
            }
        }

        private static void WriteDocx(string path,
            DevCenterService.SystemOverview overview, List<CategoryScore> categories)
        {
            using (WordprocessingDocument doc = WordprocessingDocument.Create(
                path, WordprocessingDocumentType.Document))
            {
                MainDocumentPart main = doc.AddMainDocumentPart();
                main.Document = new Document();
                Body body = main.Document.AppendChild(new Body());

                body.AppendChild(GridReportExporter.MakeParagraph("گزارش سلامت سامانه",
                    true, "34", JustificationValues.Center, "1B3A5C"));
                body.AppendChild(GridReportExporter.MakeParagraph(
                    "مرکز کنترل توسعه‌دهنده — گزارش فنی",
                    false, "20", JustificationValues.Center, "68758A"));
                body.AppendChild(Spacer());

                // ─── امتیاز کلی ───
                int overall = overview == null ? -1 : overview.HealthScore;
                body.AppendChild(GridReportExporter.MakeParagraph(
                    "امتیاز سلامت سیستم: " + (overall < 0 ? DevCenterService.NotAvailable : overall + " / 100")
                    + (overview == null ? "" : "   (" + overview.Performance + ")"),
                    true, "28", JustificationValues.Right, ColorFor(overall)));

                if (overview != null && overview.HealthNotes.Count > 0)
                    body.AppendChild(GridReportExporter.MakeParagraph(
                        "دلایل کسر امتیاز: " + string.Join(" — ", overview.HealthNotes.ToArray()),
                        false, "18", JustificationValues.Right, "68758A"));

                body.AppendChild(Spacer());

                // ─── جدول دسته‌ها ───
                body.AppendChild(GridReportExporter.MakeParagraph("ارزیابی دسته‌بندی‌شده",
                    true, "24", JustificationValues.Right, "1B3A5C"));

                Table table = NewTable();

                TableRow header = new TableRow();
                header.Append(GridReportExporter.MakeCell("دسته", true));
                header.Append(GridReportExporter.MakeCell("وضعیت", true));
                header.Append(GridReportExporter.MakeCell("امتیاز", true));
                header.Append(GridReportExporter.MakeCell("موارد یافت‌شده", true));
                header.Append(GridReportExporter.MakeCell("سطح ریسک", true));
                header.Append(GridReportExporter.MakeCell("توصیه", true));
                table.Append(header);

                foreach (CategoryScore c in categories)
                {
                    TableRow row = new TableRow();
                    row.Append(GridReportExporter.MakeCell(c.Name, false));
                    row.Append(GridReportExporter.MakeCell(c.Status, false));
                    row.Append(GridReportExporter.MakeCell(c.ScoreText, false));
                    row.Append(GridReportExporter.MakeCell(
                        c.Score < 0 ? DevCenterService.NotAvailable : c.Problems.ToString("N0"), false));
                    row.Append(GridReportExporter.MakeCell(c.Risk, false));
                    row.Append(GridReportExporter.MakeCell(c.Recommendation, false));
                    table.Append(row);
                }

                body.AppendChild(table);
                body.AppendChild(Spacer());

                // ─── شناسنامهٔ گزارش ───
                body.AppendChild(GridReportExporter.MakeParagraph("مشخصات گزارش",
                    true, "24", JustificationValues.Right, "1B3A5C"));

                Table info = NewTable();
                AddInfo(info, "تاریخ و ساعت تولید",
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                AddInfo(info, "نسخهٔ نرم‌افزار", overview == null ? DevCenterService.NotAvailable : overview.AppVersion);
                AddInfo(info, "نسخهٔ دیتابیس",  overview == null ? DevCenterService.NotAvailable : overview.DbVersion);
                AddInfo(info, "وضعیت دیتابیس",  overview == null ? DevCenterService.NotAvailable : overview.DbStatus);
                AddInfo(info, "حجم دیتابیس",    overview == null ? DevCenterService.NotAvailable : overview.DbSize);
                AddInfo(info, "نام رایانه",     System.Environment.MachineName);
                AddInfo(info, "سیستم‌عامل",     System.Environment.OSVersion.VersionString);
                AddInfo(info, "معماری فرایند",  System.Environment.Is64BitProcess ? "x64" : "x86");
                AddInfo(info, "نسخهٔ .NET",     System.Environment.Version.ToString());
                AddInfo(info, "تولیدکننده",     SecurityContext.Username);
                body.AppendChild(info);

                body.AppendChild(Spacer());
                body.AppendChild(GridReportExporter.MakeParagraph(
                    "این گزارش تنها شامل سنجه‌های فنی است و هیچ اطلاعات فردی، مالی یا محرمانه‌ای در آن درج نشده است.",
                    false, "16", JustificationValues.Right, "68758A"));

                body.AppendChild(new SectionProperties(
                    new PageSize { Width = 11906U, Height = 16838U },
                    new PageMargin { Top = 720, Bottom = 720, Left = 720, Right = 720, Header = 360, Footer = 360, Gutter = 0 },
                    new BiDi()));

                main.Document.Save();
            }
        }

        private static string ColorFor(int score)
        {
            if (score < 0)   return "68758A";
            if (score >= 85) return "1E7B34";
            if (score >= 70) return "9A6B00";
            return "B32020";
        }

        private static Paragraph Spacer()
        {
            return GridReportExporter.MakeParagraph("", false, "10", JustificationValues.Right, null);
        }

        private static Table NewTable()
        {
            Table table = new Table();
            table.AppendChild(new TableProperties(
                new TableBorders(
                    new TopBorder    { Val = BorderValues.Single, Size = 4, Color = "B0B8C4" },
                    new BottomBorder { Val = BorderValues.Single, Size = 4, Color = "B0B8C4" },
                    new LeftBorder   { Val = BorderValues.Single, Size = 4, Color = "B0B8C4" },
                    new RightBorder  { Val = BorderValues.Single, Size = 4, Color = "B0B8C4" },
                    new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = "D6DCE5" },
                    new InsideVerticalBorder   { Val = BorderValues.Single, Size = 4, Color = "D6DCE5" }),
                new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
                new BiDiVisual()));
            return table;
        }

        private static void AddInfo(Table table, string label, string value)
        {
            TableRow row = new TableRow();
            row.Append(GridReportExporter.MakeCell(label, true));
            row.Append(GridReportExporter.MakeCell(value ?? "", false));
            table.Append(row);
        }
    }
}
