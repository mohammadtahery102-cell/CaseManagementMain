using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Threading;
using CaseManagement.DAL;
using CaseManagement.Helpers;

namespace CaseManagement.Sync
{
    // ═════════════════════════════════════════════════════════════════════════
    // اعمالِ برنامه‌ی رسانه: کپیِ فایل‌ها + ثبت در دیتابیس.
    //
    // آموزش — چرا «اول فایل، بعد تراکنشِ کوتاه» و نه همه‌چیز داخل یک تراکنش:
    // موتورِ همگام‌سازیِ متن (SyncEngine) همه‌ی کارش را داخل یک تراکنش انجام
    // می‌دهد و این برای داده‌ی متنی درست است. ولی کپیِ فایل «تراکنش‌پذیر» نیست:
    //   • اگر هزاران فایل داخل یک تراکنشِ باز کپی شوند، دیتابیس چند دقیقه قفل
    //     می‌ماند و بقیه‌ی برنامه از کار می‌افتد.
    //   • اگر وسطِ کار خطا بدهد، تراکنش برمی‌گردد ولی فایل‌های کپی‌شده روی دیسک
    //     می‌مانند و پرونده‌ها را کثیف می‌کنند.
    // پس: اول فایل‌ها نوشته می‌شوند و مسیرشان جمع می‌شود، بعد یک تراکنشِ کوتاه
    // فقط ردیف‌های دیتابیس را می‌نویسد. اگر آن تراکنش شکست بخورد، همه‌ی
    // فایل‌هایی که همین اجرا ساخته پاک می‌شوند (جبرانِ کامل).
    //
    // تضمین‌ها:
    //   • هیچ فایلی از قبل موجود، بدون تأیید صریح کاربر جایگزین نمی‌شود.
    //   • هیچ سند یا عکسی هرگز حذف نمی‌شود.
    //   • فایلِ منبعِ کاربر هرگز تغییر نمی‌کند.
    //   • همه‌چیز در AuditLog ثبت می‌شود.
    // ═════════════════════════════════════════════════════════════════════════
    public sealed class MediaSyncEngine
    {
        private readonly DatabaseHelper _db;

        public MediaSyncEngine(DatabaseHelper db)
        {
            _db = db ?? new DatabaseHelper();
        }

        public MediaReport Apply(MediaPlan plan, IProgress<SyncProgress> progress, CancellationToken cancel)
        {
            var report = new MediaReport();

            if (plan == null)
            {
                report.ErrorMessage = "برنامه‌ی رسانه خالی است.";
                report.FinishedAt = DateTime.Now;
                return report;
            }

            if (!SecurityContext.CanEdit())
            {
                report.ErrorMessage = "کاربر با دسترسیِ فقط-مشاهده اجازه‌ی ورود عکس و سند ندارد.";
                report.FinishedAt = DateTime.Now;
                return report;
            }

            List<MediaItem> photos = plan.Photos.Where(p => p.Selected && p.IsApplicable).ToList();
            List<MediaItem> docs = plan.Documents.Where(d => d.Selected && d.IsApplicable).ToList();

            report.MissingPhotos = plan.CasesMissingPhoto.Count;
            report.DuplicateFiles = plan.PhotosDuplicate;
            report.UnsupportedFiles = plan.PhotosUnsupported + plan.DocsUnsupported;
            report.InvalidFileNames = plan.Photos.Count(p => p.Action == MediaAction.InvalidName) +
                                      plan.Documents.Count(d => d.Action == MediaAction.InvalidName);
            report.LargeImages = plan.Photos.Count(p => p.SizeBytes > MediaScanner.RecommendedMaxPhotoBytes);

            int total = Math.Max(1, photos.Count + docs.Count);
            int done = 0;

            // مسیرِ فایل‌هایی که همین اجرا ساخته — برای پاک‌سازی در صورت شکست.
            var writtenFiles = new List<string>();

            // آنچه باید در تراکنشِ کوتاه نوشته شود.
            var photoWrites = new List<KeyValuePair<MediaItem, string>>();
            var docWrites = new List<KeyValuePair<MediaItem, string>>();

            try
            {
                // ── فاز ۱: نوشتنِ فایل‌ها (بدونِ هیچ تراکنشِ باز) ──────────────
                foreach (MediaItem item in photos)
                {
                    cancel.ThrowIfCancellationRequested();

                    string saved = CopyPhoto(item, report);
                    if (!string.IsNullOrWhiteSpace(saved))
                    {
                        writtenFiles.Add(saved);
                        photoWrites.Add(new KeyValuePair<MediaItem, string>(item, saved));
                    }
                    else
                    {
                        report.Errors++;
                        report.Add("عکسِ «" + item.FileName + "» ذخیره نشد: " + FileHelper.LastError);
                    }

                    done++;
                    Report(progress, "کپیِ عکس‌ها — " + item.CaseCode, done, total);
                }

                foreach (MediaItem item in docs)
                {
                    cancel.ThrowIfCancellationRequested();

                    string saved = CopyDocument(item);
                    if (!string.IsNullOrWhiteSpace(saved))
                    {
                        writtenFiles.Add(saved);
                        docWrites.Add(new KeyValuePair<MediaItem, string>(item, saved));
                    }
                    else
                    {
                        report.Errors++;
                        report.Add("سندِ «" + item.FileName + "» ذخیره نشد: " + FileHelper.LastError);
                    }

                    done++;
                    Report(progress, "کپیِ اسناد — " + item.CaseCode, done, total);
                }

                // ── فاز ۲: تراکنشِ کوتاهِ دیتابیس ────────────────────────────
                Report(progress, "ثبت در دیتابیس", total, total);

                using (SQLiteConnection con = _db.GetConnection())
                {
                    con.Open();
                    using (SQLiteTransaction tr = con.BeginTransaction())
                    {
                        try
                        {
                            foreach (var pair in photoWrites)
                            {
                                // هر نوع عکس، ستونِ خودش را دارد.
                                switch (pair.Key.Kind)
                                {
                                    case MediaKind.FamilyPhoto:
                                        UpdateCaseColumn(con, tr, "FamilyPhotoPath", pair.Key.CasId, pair.Value);
                                        break;

                                    case MediaKind.MemberPhoto:
                                        UpdateMemberPhoto(con, tr, pair.Key.FamId, pair.Value);
                                        break;

                                    default:
                                        UpdateCaseColumn(con, tr, "PhotoPath", pair.Key.CasId, pair.Value);
                                        break;
                                }

                                if (pair.Key.Action == MediaAction.Replace) report.PhotosReplaced++;
                                else report.PhotosImported++;
                            }

                            foreach (var pair in docWrites)
                            {
                                InsertDocument(con, tr, pair.Key, pair.Value);
                                if (pair.Key.Action == MediaAction.Replace) report.DocumentsReplaced++;
                                else report.DocumentsImported++;
                            }

                            tr.Commit();
                        }
                        catch
                        {
                            tr.Rollback();
                            throw;
                        }
                    }
                }

                report.Success = true;
                report.Add("عکس‌های واردشده: " + report.PhotosImported +
                           "  |  جایگزین‌شده: " + report.PhotosReplaced);
                report.Add("اسناد واردشده: " + report.DocumentsImported +
                           "  |  جایگزین‌شده: " + report.DocumentsReplaced);
            }
            catch (OperationCanceledException)
            {
                CleanupFiles(writtenFiles, report);
                report.RolledBack = true;
                report.Success = false;
                report.ErrorMessage = "عملیات توسط کاربر لغو شد؛ هیچ تغییری ثبت نشد.";
            }
            catch (Exception ex)
            {
                // جبرانِ کامل: هر فایلی که همین اجرا ساخته پاک می‌شود تا چیزی
                // نیمه‌کاره روی دیسک نماند.
                CleanupFiles(writtenFiles, report);
                report.RolledBack = true;
                report.Success = false;
                report.ErrorMessage = "خطا در ورود رسانه‌ها؛ همه‌چیز برگردانده شد: " + ex.Message;
            }

            report.FinishedAt = DateTime.Now;

            try
            {
                AuditLogger.Log("همگام‌سازی رسانه", "TblCase", 0, "",
                    "عکس: " + report.PhotosImported + "+" + report.PhotosReplaced +
                    " | سند: " + report.DocumentsImported + "+" + report.DocumentsReplaced +
                    " | خطا: " + report.Errors);
            }
            catch { }

            return report;
        }

        // ─── کپیِ عکس ────────────────────────────────────────────────────────
        private string CopyPhoto(MediaItem item, MediaReport report)
        {
            // مسیرِ مقصد دقیقاً همان الگویی است که فرم‌های برنامه استفاده
            // می‌کنند، تا عکس در همان فرم و در خروجی Word خودکار دیده شود.
            string section;
            string baseName;

            switch (item.Kind)
            {
                case MediaKind.FamilyPhoto:
                    section = FileHelper.SectionFamilyPhoto;
                    baseName = item.CaseCode + "-Family";
                    break;

                case MediaKind.MemberPhoto:
                    section = FileHelper.SectionMemberPhotos;
                    // نامِ عضو در نامِ فایل می‌ماند تا بعداً هم قابلِ تشخیص باشد
                    // (همان مشکلی که باعثِ ابهامِ نگاشت در خروجی‌ها می‌شد).
                    baseName = FileHelper.CleanName(item.CaseCode + "-" + (item.MemberName ?? "عضو"));
                    break;

                default:
                    section = FileHelper.SectionHeadPhoto;
                    baseName = item.CaseCode + "-Head";
                    break;
            }

            // اگر چرخش یا فشرده‌سازی لازم است، اول روی یک فایلِ موقت انجام
            // می‌شود و بعد همان به پوشه‌ی پرونده می‌رود — منبع دست‌نخورده می‌ماند.
            string prepared = item.SourcePath;
            string tempFile = null;
            bool rotated = false, compressed = false;

            try
            {
                // آموزش — یکسان‌سازیِ فرمت روی JPG (خواسته‌ی کاربر):
                // هر عکسی که JPG نیست (png/bmp) به JPG تبدیل می‌شود تا همه‌ی
                // عکس‌های سیستم یک فرمت داشته باشند و خروجی Word/کارت/چاپ
                // رفتار یکنواخت بدهد. فایلِ منبعِ کاربر هرگز تغییر نمی‌کند —
                // تبدیل روی یک فایلِ موقت انجام و همان کپی می‌شود.
                // استثنا: webp که GDI+ در .NET 4.7.2 رمزگشایی‌اش نمی‌کند؛ آن
                // بدون تبدیل کپی می‌شود و همین در راهنما و گزارش گفته شده.
                string sourceExt = Path.GetExtension(item.SourcePath ?? "").ToLowerInvariant();
                bool needsJpegConversion =
                    sourceExt != ".jpg" && sourceExt != ".jpeg" && sourceExt != ".webp";

                if (item.NeedsRotation || item.WillCompress || needsJpegConversion)
                {
                    tempFile = Path.Combine(Path.GetTempPath(),
                        "cm_media_" + Guid.NewGuid().ToString("N") + ".jpg");

                    // با needsJpegConversion، حتی اگر فشرده‌سازی لازم نباشد هم
                    // خروجی JPG نوشته می‌شود.
                    if (ImageOrientationHelper.TryProcessCopy(item.SourcePath, tempFile,
                            item.WillCompress, MediaScanner.RecommendedMaxPhotoBytes,
                            out rotated, out compressed, forceJpeg: needsJpegConversion))
                    {
                        prepared = tempFile;
                        if (rotated) report.RotatedImages++;
                        if (compressed) report.CompressedImages++;
                    }
                }

                return FileHelper.SaveFileToCaseFolder(
                    prepared,
                    item.CaseCode,
                    section,
                    baseName,
                    item.ExistingPath ?? "");
            }
            finally
            {
                if (tempFile != null)
                {
                    try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
                }
            }
        }

        private string CopyDocument(MediaItem item)
        {
            string baseName = Path.GetFileNameWithoutExtension(item.FileName);
            if (string.IsNullOrWhiteSpace(baseName)) baseName = item.CaseCode + "-Doc";

            return FileHelper.SaveFileToCaseFolder(
                item.SourcePath,
                item.CaseCode,
                FileHelper.SectionDocs,
                baseName);
        }

        // ─── نوشتن در دیتابیس ────────────────────────────────────────────────
        // نامِ ستون از فهرستِ بسته‌ی محدود می‌آید (نه ورودیِ کاربر)، پس
        // درج‌شدنش در SQL امن است؛ مقدار همچنان پارامتری می‌ماند.
        private void UpdateCaseColumn(SQLiteConnection con, SQLiteTransaction tr,
                                      string column, int casId, string path)
        {
            if (column != "PhotoPath" && column != "FamilyPhotoPath")
                throw new ArgumentException("ستونِ عکسِ ناشناخته: " + column);

            using (var cmd = new SQLiteCommand(
                "UPDATE TblCase SET " + column + " = @P, UpdatedAt = CURRENT_TIMESTAMP WHERE CasID = @Id", con, tr))
            {
                cmd.Parameters.AddWithValue("@P", path);
                cmd.Parameters.AddWithValue("@Id", casId);
                cmd.ExecuteNonQuery();
            }
        }

        private void UpdateMemberPhoto(SQLiteConnection con, SQLiteTransaction tr, int famId, string path)
        {
            if (famId <= 0) return;

            using (var cmd = new SQLiteCommand(
                "UPDATE TblFamily SET MemberPhotoPath = @P WHERE FamID = @Id", con, tr))
            {
                cmd.Parameters.AddWithValue("@P", path);
                cmd.Parameters.AddWithValue("@Id", famId);
                cmd.ExecuteNonQuery();
            }
        }

        // ثبتِ سند دقیقاً با همان الگوی فرم اسناد، تا در همان فرم و در
        // گزارش‌های موجود بدون هیچ تغییری دیده شود.
        private void InsertDocument(SQLiteConnection con, SQLiteTransaction tr, MediaItem item, string savedPath)
        {
            using (var cmd = new SQLiteCommand(@"
INSERT INTO TblDocs (CasID, DocType, OriginalFileName, DocFilePath, RelatedCaseRef, DocDescription, GlobalID)
VALUES (@CasID, @DocType, @OriginalFileName, @DocFilePath, @RelatedCaseRef, @DocDescription,
        lower(hex(randomblob(4))) || '-' || lower(hex(randomblob(2))) || '-' ||
        lower(hex(randomblob(2))) || '-' || lower(hex(randomblob(2))) || '-' || lower(hex(randomblob(6))))", con, tr))
            {
                cmd.Parameters.AddWithValue("@CasID", item.CasId);
                cmd.Parameters.AddWithValue("@DocType", ClassifyDocType(item.FileName));
                cmd.Parameters.AddWithValue("@OriginalFileName", item.FileName ?? "");
                cmd.Parameters.AddWithValue("@DocFilePath", savedPath ?? "");
                cmd.Parameters.AddWithValue("@RelatedCaseRef", item.CaseCode ?? "");
                cmd.Parameters.AddWithValue("@DocDescription", "واردشده از بسته‌ی همگام‌سازی");
                cmd.ExecuteNonQuery();
            }
        }

        // ─── دسته‌بندیِ خودکارِ نوع سند از روی نام فایل ──────────────────────
        // آموزش — عمداً ساده و قابل‌پیش‌بینی: اگر نام فایل نشانه‌ی روشنی داشت
        // همان، وگرنه «سایر». حدسِ هوشمندانه‌ی مبهم بدتر از یک نوعِ عمومیِ
        // درست است، چون کاربر می‌تواند بعداً در فرم اسناد اصلاحش کند.
        private static string ClassifyDocType(string fileName)
        {
            string n = (fileName ?? "").ToLowerInvariant();

            if (n.Contains("nationalid") || n.Contains("tazkira") || n.Contains("تذکره") || n.Contains("id"))
                return "تذکره";
            if (n.Contains("house") || n.Contains("contract") || n.Contains("خانه") || n.Contains("اجاره"))
                return "سند مسکن";
            if (n.Contains("medical") || n.Contains("health") || n.Contains("صحی") || n.Contains("طبی"))
                return "سند صحی";
            if (n.Contains("birth") || n.Contains("تولد"))
                return "تصدیق تولد";
            if (n.Contains("school") || n.Contains("edu") || n.Contains("مکتب") || n.Contains("تحصیل"))
                return "سند تحصیلی";
            if (n.Contains("death") || n.Contains("وفات") || n.Contains("فوت"))
                return "تصدیق وفات";

            return "سایر";
        }

        private void CleanupFiles(List<string> files, MediaReport report)
        {
            int removed = 0;
            foreach (string f in files)
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(f) && File.Exists(f)) { File.Delete(f); removed++; }
                }
                catch { }
            }
            if (removed > 0)
                report.Add("پاک‌سازی: " + removed + " فایلِ نیمه‌کاره حذف شد تا چیزی روی دیسک نماند.");
        }

        private static void Report(IProgress<SyncProgress> progress, string phase, int current, int total)
        {
            if (progress == null) return;
            progress.Report(new SyncProgress { Phase = phase, Current = current, Total = total });
        }
    }
}
