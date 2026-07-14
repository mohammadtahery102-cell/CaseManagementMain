using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace CaseManagement.Sync
{
    // ─────────────────────────────────────────────────────────────────────────
    // HtmlSyncProvider — منبع همگام‌سازی از خروجی HTML سامانه مرکزی.
    //
    // دو فایل ورودی: (۱) سرپرستان (۲) اعضای خانواده. «کد عمومی» = کد اختصاصی
    // سرپرست (TblCase.Code) و تنها مبنای ارتباط است. فایل اعضا کد مستقل ندارد؛
    // همان کد عمومی سرپرست در هر ردیفِ عضو تکرار می‌شود — اگر یک خانواده ۱۰ یتیم
    // داشته باشد، همان کد عمومی ۱۰ بار در فایل اعضا می‌آید و هر ۱۰ ردیف زیر همان
    // پرونده (CasID) اضافه می‌شوند.
    //
    // کالیبره‌شده روی فایل واقعی کاربر (۱۴۰۵/۰۴/۱۴):
    //   • فایل سرپرستان: ردیف, کد عمومی, نام, نام پدر, وضعیت تأهل, تاریخ تولد,
    //     ش تذکره, سیادت, تلفن همراه, آدرس, قطع, شماره پرونده, ولایت, تعداد یتیم,
    //     ولسوالی, مذهب, وضعیت خدمات, وضعیت تذکره
    //     - «آدرس» = محل سکونت فعلی سرپرست.
    //     - «شماره پرونده» → ستون موجود TblCase.CaseNo.
    //     - قطع/تعداد یتیم/وضعیت تذکره/تاریخ تولد سرپرست: بدون ستون متناظر در
    //       دیتابیس فعلی → عمداً نادیده گرفته می‌شوند (طبق تصمیم کاربر).
    //   • فایل اعضا: ردیف, کد عمومی, نام, نام پدر, ش تذکره, تاریخ تولد, تلفن همراه,
    //     نام(۲), تاریخ تولد(۲), ش تذکره(۲), سیادت, جنسیت, وضعیت خدمات
    //     - نام/تاریخ تولد/ش‌تذکره اول = خود عضو.
    //     - نام/تاریخ تولد/ش‌تذکره دوم (تکراری) = کفیل/سرپرست دیگر — چون ستون
    //       متناظر در TblFamily نداریم، به‌صورت یادداشت در ستون عمومی «Details»
    //       نوشته می‌شود (قابل مشاهده و انتخاب/رد در Wizard، نه بازنویسی خاموش).
    // ─────────────────────────────────────────────────────────────────────────
    public sealed class HtmlSyncProvider : IDataSyncProvider
    {
        public string Name { get { return "خروجی HTML سامانه مرکزی"; } }

        // عناوین ممکن ستون «کد عمومی» (= کد اختصاصی سرپرست، کلید خانواده).
        private static readonly string[] PublicCodeHeaders =
            { "کد عمومی", "کد اختصاصی", "کد خانواده", "کد فامیل", "کد" };

        // نگاشت ستون دیتابیس TblCase → عناوین ستون در فایل سرپرستان (occurrence=1).
        private static readonly Dictionary<string, string[]> GuardianFieldMap =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "HeadFullName",         new[] { "نام", "نام سرپرست", "نام و تخلص", "نام کامل" } },
            { "HeadFatherName",       new[] { "نام پدر", "ولد" } },
            { "HeadTazkiraNo",        new[] { "ش تذکره", "شماره تذکره", "تذکره" } },
            { "HeadSadat",            new[] { "سیادت" } },
            { "Phone",                new[] { "تلفن همراه", "شماره تماس", "تلفن", "موبایل" } },
            { "HeadCurrentResidence", new[] { "آدرس" } },
            { "CaseNo",               new[] { "شماره پرونده" } },
            { "Province",             new[] { "ولایت", "استان" } },
            { "District",             new[] { "ولسوالی", "شهرستان" } },
            { "Religion",             new[] { "مذهب" } },
            { "MaritalStatus",        new[] { "وضعیت تأهل", "تاهل" } },
            { "ServiceStatus",        new[] { "وضعیت خدمات" } },
        };

        // نگاشت ستون دیتابیس TblFamily → عناوین ستون در فایل اعضا (occurrence=1؛
        // یعنی وقتی عنوان دوبار تکرار شده، همیشه اولین/خودِ عضو گرفته می‌شود).
        private static readonly Dictionary<string, string[]> MemberFieldMap =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "MemberName",       new[] { "نام", "نام عضو" } },
            { "MemberFatherName", new[] { "نام پدر", "ولد" } },
            { "MemberTazkiraNo",  new[] { "ش تذکره", "شماره تذکره", "تذکره" } },
            { "BirthDate",        new[] { "تاریخ تولد" } },
            { "Gender",           new[] { "جنسیت" } },
            { "MemberSadat",      new[] { "سیادت" } },
        };

        // CHECK constraint واقعی TblCase.ServiceStatus — مقدار خارج از این فهرست
        // هرگز نوشته نمی‌شود (تا کل تراکنش به‌خاطر یک مقدار نامعتبر Rollback نشود).
        private static readonly HashSet<string> AllowedServiceStatuses =
            new HashSet<string>(StringComparer.Ordinal) { "فعال", "در انتظار تأیید", "قطع موقت", "قطع" };

        // ─── مرحله ۲: تجزیه ──────────────────────────────────────────────────
        public ParsedSyncData Parse(SyncSource source, IProgress<SyncProgress> progress)
        {
            if (source == null) throw new ArgumentNullException("source");
            var result = new ParsedSyncData();

            if (!string.IsNullOrWhiteSpace(source.GuardiansFilePath))
            {
                Report(progress, "تجزیه فایل سرپرستان", 0, 1);
                var rows = ParseHtmlTable(source.GuardiansFilePath);
                for (int i = 0; i < rows.Count; i++)
                {
                    result.Guardians.Add(BuildGuardian(rows[i]));
                    if ((i & 0x3FF) == 0) Report(progress, "تجزیه فایل سرپرستان", i + 1, rows.Count);
                }
                Report(progress, "تجزیه فایل سرپرستان", rows.Count, rows.Count);
            }

            if (!string.IsNullOrWhiteSpace(source.MembersFilePath))
            {
                Report(progress, "تجزیه فایل اعضا", 0, 1);
                var rows = ParseHtmlTable(source.MembersFilePath);
                for (int i = 0; i < rows.Count; i++)
                {
                    result.Members.Add(BuildMember(rows[i]));
                    if ((i & 0x3FF) == 0) Report(progress, "تجزیه فایل اعضا", i + 1, rows.Count);
                }
                Report(progress, "تجزیه فایل اعضا", rows.Count, rows.Count);
            }

            return result;
        }

        private SyncRecord BuildGuardian(HtmlRow row)
        {
            var rec = new SyncRecord { Entity = SyncEntity.Guardian };
            rec.PublicCode = ResolvePublicCode(row);
            MapFields(row, GuardianFieldMap, rec.SourceValues, occurrence: 1);

            // کد عمومی همیشه در ستون کلیدی دیتابیس (Code) هم قرار می‌گیرد.
            if (!string.IsNullOrWhiteSpace(rec.PublicCode))
                rec.SourceValues["Code"] = rec.PublicCode;

            // نرمال‌سازی وضعیت خدمات؛ اگر با CHECK constraint سازگار نبود، حذف
            // می‌شود (نه این‌که کل تراکنش را با خطای دیتابیس Rollback کند).
            string status;
            if (rec.SourceValues.TryGetValue("ServiceStatus", out status))
            {
                string normalized = NormalizeServiceStatus(status);
                if (AllowedServiceStatuses.Contains(normalized))
                    rec.SourceValues["ServiceStatus"] = normalized;
                else
                    rec.SourceValues.Remove("ServiceStatus");
            }

            rec.Title = rec.SourceValues.ContainsKey("HeadFullName") ? rec.SourceValues["HeadFullName"] : rec.PublicCode;
            return rec;
        }

        private SyncRecord BuildMember(HtmlRow row)
        {
            var rec = new SyncRecord { Entity = SyncEntity.FamilyMember };
            rec.PublicCode = ResolvePublicCode(row);
            MapFields(row, MemberFieldMap, rec.SourceValues, occurrence: 1);

            // ─── ستون دوم (کفیل/سرپرست دیگر) + تلفن + وضعیت خدمات ─────────────
            // این‌ها معادل مستقیمی در TblFamily ندارند؛ برای این‌که داده گم نشود
            // (و در عین حال فیلد «خودِ عضو» را خراب نکند)، به‌صورت یادداشت خوانا
            // در ستون عمومی TblFamily.Details نوشته می‌شوند. کاربر در Wizard
            // (مرحله «جزئیات») دقیقاً همین متن را قبل از تأیید می‌بیند و می‌تواند
            // اعمال/رد کند.
            var noteLines = new List<string>();

            string ownPhone = FindByHeader(row, "تلفن همراه", 1) ?? FindByHeader(row, "شماره تماس", 1);
            if (!string.IsNullOrWhiteSpace(ownPhone))
                noteLines.Add("تلفن: " + ownPhone.Trim());

            string sponsorName = FindByHeader(row, "نام", 2);
            string sponsorTazkira = FindByHeader(row, "ش تذکره", 2) ?? FindByHeader(row, "شماره تذکره", 2);
            string sponsorDob = FindByHeader(row, "تاریخ تولد", 2);
            if (!string.IsNullOrWhiteSpace(sponsorName) || !string.IsNullOrWhiteSpace(sponsorTazkira) || !string.IsNullOrWhiteSpace(sponsorDob))
            {
                string line = "کفیل/سرپرست دیگر:";
                if (!string.IsNullOrWhiteSpace(sponsorName)) line += " " + sponsorName.Trim();
                if (!string.IsNullOrWhiteSpace(sponsorTazkira)) line += " — تذکره " + sponsorTazkira.Trim();
                if (!string.IsNullOrWhiteSpace(sponsorDob)) line += " — تولد " + sponsorDob.Trim();
                noteLines.Add(line);
            }

            string serviceStatus = FindByHeader(row, "وضعیت خدمات", 1);
            if (!string.IsNullOrWhiteSpace(serviceStatus))
                noteLines.Add("وضعیت خدمات: " + serviceStatus.Trim());

            if (noteLines.Count > 0)
                rec.SourceValues["Details"] = string.Join("\n", noteLines);

            rec.Title = rec.SourceValues.ContainsKey("MemberName") ? rec.SourceValues["MemberName"] : "";
            rec.MemberKey = ComputeMemberKey(rec.SourceValues);
            return rec;
        }

        // نرمال‌سازی مقادیر متغیر «وضعیت خدمات» به ۴ مقدار مجاز CHECK constraint
        // (هم‌راستا با FrmCase.NormalizeServiceStatus).
        private static string NormalizeServiceStatus(string value)
        {
            value = (value ?? "").Trim();
            if (value == "در حالت قطع") return "قطع";
            if (value == "درانتظار" || value == "در انتظار" ||
                value == "انتظار تاييد" || value == "انتظار تایید" ||
                value == "در انتظار تاييد")
                return "در انتظار تأیید";
            if (value == "") return "فعال";
            return value;
        }

        // کلید هویت عضو داخل خانواده: تذکره (اگر باشد) وگرنه نام+نام‌پدر.
        // آموزش: بدون یک کلید پایدار، یک عضو در هر بار همگام‌سازی «جدید» به‌نظر
        // می‌رسد و تکراری ثبت می‌شود. تذکره پایدارترین گزینه است.
        public static string ComputeMemberKey(Dictionary<string, string> values)
        {
            string tazkira;
            if (values.TryGetValue("MemberTazkiraNo", out tazkira) && !string.IsNullOrWhiteSpace(tazkira))
                return "T:" + Normalize(tazkira);

            string name = values.ContainsKey("MemberName") ? values["MemberName"] : "";
            string father = values.ContainsKey("MemberFatherName") ? values["MemberFatherName"] : "";
            return "N:" + Normalize(name) + "|" + Normalize(father);
        }

        private static string Normalize(string s)
        {
            return (s ?? "").Trim();
        }

        private string ResolvePublicCode(HtmlRow row)
        {
            foreach (string header in PublicCodeHeaders)
            {
                string val = FindByHeader(row, header, 1);
                if (!string.IsNullOrWhiteSpace(val))
                    return val.Trim();
            }
            return "";
        }

        private void MapFields(HtmlRow row, Dictionary<string, string[]> map,
            Dictionary<string, string> target, int occurrence)
        {
            foreach (var kv in map)
            {
                foreach (string candidate in kv.Value)
                {
                    string val = FindByHeader(row, candidate, occurrence);
                    if (val != null)
                    {
                        target[kv.Key] = val.Trim();
                        break;
                    }
                }
            }
        }

        // جستجوی مقدار بر اساس عنوان ستون + شماره‌ی وقوع (برای ستون‌های هم‌نامِ
        // تکراری مثل «نام» که یک‌بار برای خودِ عضو و یک‌بار برای کفیل می‌آید).
        // occurrence=1 یعنی اولین ستونی که این عنوان را دارد (از راست به چپ فایل
        // اصلی که هنگام تجزیه، ترتیب واقعی ستون‌ها حفظ شده است).
        private static string FindByHeader(HtmlRow row, string header, int occurrence)
        {
            string norm = NormalizeHeader(header);
            int seen = 0;
            for (int i = 0; i < row.Keys.Count; i++)
            {
                if (NormalizeHeader(row.Keys[i]) == norm)
                {
                    seen++;
                    if (seen == occurrence)
                        return row.Values[i];
                }
            }
            return null;
        }

        private static string NormalizeHeader(string s)
        {
            if (s == null) return "";
            return s.Replace('ي', 'ی').Replace('ك', 'ک')
                    .Replace("‌", " ")   // نیم‌فاصله → فاصله
                    .Trim()
                    .ToLowerInvariant();
        }

        // ─── مرحله ۳: اعتبارسنجی ─────────────────────────────────────────────
        public List<string> Validate(ParsedSyncData data)
        {
            var errors = new List<string>();
            if (data == null) { errors.Add("داده‌ای برای اعتبارسنجی وجود ندارد."); return errors; }

            int guardianNoCode = data.Guardians.Count(g => string.IsNullOrWhiteSpace(g.PublicCode));
            if (guardianNoCode > 0)
                errors.Add(guardianNoCode + " سرپرست بدون «کد عمومی» است و نادیده گرفته می‌شود.");

            int memberNoCode = data.Members.Count(m => string.IsNullOrWhiteSpace(m.PublicCode));
            if (memberNoCode > 0)
                errors.Add(memberNoCode + " عضو خانواده بدون «کد عمومی» است و قابل ارتباط با خانواده نیست.");

            int memberNoName = data.Members.Count(m => string.IsNullOrWhiteSpace(m.Title));
            if (memberNoName > 0)
                errors.Add(memberNoName + " عضو خانواده بدون «نام» است.");

            if (data.Guardians.Count == 0 && data.Members.Count == 0)
                errors.Add("هیچ رکورد قابل‌خواندنی در فایل‌ها یافت نشد (ساختار جدول HTML شناسایی نشد).");

            return errors;
        }

        // ─── ردیف تجزیه‌شده با حفظ ترتیب دقیق ستون‌ها ────────────────────────
        // آموزش — رفع باگ بحرانی: نسخه‌ی قبلی از Dictionary<string,string> با
        // کلید = نام ستون استفاده می‌کرد؛ وقتی دو ستون هم‌نام بودند (مثل «نام»ِ
        // خودِ عضو و «نام»ِ کفیل)، مقدار دومی به‌خاطر «اگر کلید موجود بود
        // نادیده بگیر» به‌طور کاملاً بی‌صدا گم می‌شد — نه خطا، نه هشدار، فقط
        // حذف داده. حالا هر ردیف یک لیست هم‌ترتیب (Keys/Values) نگه می‌دارد که
        // امکان جستجوی «امین وقوعِ فلان عنوان» را می‌دهد (FindByHeader بالا).
        private sealed class HtmlRow
        {
            public readonly List<string> Keys = new List<string>();
            public readonly List<string> Values = new List<string>();
        }

        // ─── تجزیه‌ی جدول HTML (تحمل‌پذیر، بدون وابستگی خارجی) ────────────────
        // بزرگ‌ترین جدول فایل (بیشترین ردیف) به‌عنوان جدول داده انتخاب می‌شود؛
        // ردیف اول با <th> (یا اولین ردیف) به‌عنوان سرستون، بقیه داده.
        private static List<HtmlRow> ParseHtmlTable(string filePath)
        {
            var rows = new List<HtmlRow>();
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return rows;

            string html = File.ReadAllText(filePath, DetectEncoding(filePath));

            // همه‌ی جدول‌ها را پیدا کن؛ آن‌که بیشترین <tr> دارد جدول داده است.
            var tables = Regex.Matches(html, "<table[^>]*>(.*?)</table>",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            string bestTable = null;
            int bestRowCount = -1;
            foreach (Match t in tables)
            {
                int rc = Regex.Matches(t.Groups[1].Value, "<tr", RegexOptions.IgnoreCase).Count;
                if (rc > bestRowCount) { bestRowCount = rc; bestTable = t.Groups[1].Value; }
            }
            if (bestTable == null) return rows;

            var trMatches = Regex.Matches(bestTable, "<tr[^>]*>(.*?)</tr>",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (trMatches.Count == 0) return rows;

            List<string> headers = null;
            foreach (Match tr in trMatches)
            {
                List<string> cells = ExtractCells(tr.Groups[1].Value);
                if (cells.Count == 0) continue;

                if (headers == null)
                {
                    headers = cells; // اولین ردیف = سرستون (ترتیب اصلی حفظ می‌شود)
                    continue;
                }

                var row = new HtmlRow();
                for (int i = 0; i < cells.Count && i < headers.Count; i++)
                {
                    string key = string.IsNullOrWhiteSpace(headers[i]) ? ("col" + i) : headers[i];
                    row.Keys.Add(key);
                    row.Values.Add(cells[i]);
                }
                rows.Add(row);
            }

            return rows;
        }

        private static List<string> ExtractCells(string trInner)
        {
            var cells = new List<string>();
            var cellMatches = Regex.Matches(trInner, "<t[hd][^>]*>(.*?)</t[hd]>",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            foreach (Match c in cellMatches)
                cells.Add(CleanCell(c.Groups[1].Value));
            return cells;
        }

        private static string CleanCell(string raw)
        {
            if (raw == null) return "";
            string noTags = Regex.Replace(raw, "<[^>]+>", " ");
            string decoded = WebUtility.HtmlDecode(noTags);
            string collapsed = Regex.Replace(decoded, "\\s+", " ");
            return collapsed.Trim();
        }

        // تشخیص انکدینگ ساده: BOM یا meta charset؛ پیش‌فرض UTF-8.
        private static Encoding DetectEncoding(string filePath)
        {
            try
            {
                byte[] head = new byte[4];
                using (var fs = File.OpenRead(filePath))
                    fs.Read(head, 0, 4);
                if (head[0] == 0xEF && head[1] == 0xBB && head[2] == 0xBF) return Encoding.UTF8;
                if (head[0] == 0xFF && head[1] == 0xFE) return Encoding.Unicode;
                if (head[0] == 0xFE && head[1] == 0xFF) return Encoding.BigEndianUnicode;
            }
            catch { }
            return Encoding.UTF8;
        }

        private static void Report(IProgress<SyncProgress> progress, string phase, int current, int total)
        {
            if (progress != null)
                progress.Report(new SyncProgress { Phase = phase, Current = current, Total = total });
        }
    }
}
