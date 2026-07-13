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
    // دو فایل ورودی: (۱) سرپرستان (۲) اعضای خانواده. هر دو یک ستون «کد عمومی»
    // دارند که شناسه‌ی یکتای خانواده است و تنها مبنای ارتباط بین دو فایل و با
    // دیتابیس (TblCase.Code) است.
    //
    // ★★★ نقطه‌ی کالیبراسیون ★★★
    // تنها چیزی که با «فایل نمونه‌ی واقعی» باید تنظیم شود، جدول‌های نگاشت زیر
    // (GuardianFieldMap / MemberFieldMap و نام ستون کد عمومی) است: هر ستون
    // دیتابیس به فهرستی از «عناوین ممکن ستون در HTML» نگاشت می‌شود. Parser خودش
    // ساختار جدول HTML را تشخیص می‌دهد؛ فقط باید بداند کدام عنوانِ ستون به کدام
    // فیلد می‌رود. با دیدن فایل واقعی، این چند خط در چند دقیقه دقیق می‌شود.
    // ─────────────────────────────────────────────────────────────────────────
    public sealed class HtmlSyncProvider : IDataSyncProvider
    {
        public string Name { get { return "خروجی HTML سامانه مرکزی"; } }

        // عناوین ممکن ستون «کد عمومی» (کلید خانواده) در هر دو فایل.
        private static readonly string[] PublicCodeHeaders =
            { "کد عمومی", "کد خانواده", "کد اختصاصی", "کد فامیل", "کد" };

        // نگاشت ستون دیتابیس TblCase → عناوین ممکن ستون در فایل سرپرستان.
        private static readonly Dictionary<string, string[]> GuardianFieldMap =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "HeadFullName",     new[] { "نام سرپرست", "نام و تخلص", "نام کامل", "اسم سرپرست" } },
            { "HeadFatherName",   new[] { "نام پدر", "ولد", "اسم پدر" } },
            { "HeadTazkiraNo",    new[] { "شماره تذکره", "تذکره", "شماره تذکره سرپرست" } },
            { "Phone",            new[] { "شماره تماس", "تلفن", "موبایل", "شماره" } },
            { "Province",         new[] { "ولایت", "استان" } },
            { "District",         new[] { "ولسوالی", "شهرستان" } },
            { "RequestType",      new[] { "نوع درخواست", "نوع کمک" } },
            { "MaritalStatus",    new[] { "وضعیت تأهل", "تاهل" } },
            { "Job",              new[] { "شغل", "وظیفه" } },
        };

        // نگاشت ستون دیتابیس TblFamily → عناوین ممکن ستون در فایل اعضا.
        private static readonly Dictionary<string, string[]> MemberFieldMap =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "MemberName",       new[] { "نام عضو", "نام", "اسم" } },
            { "MemberFatherName", new[] { "نام پدر", "ولد" } },
            { "MemberTazkiraNo",  new[] { "شماره تذکره", "تذکره" } },
            { "BirthDate",        new[] { "تاریخ تولد", "تولد", "سن" } },
            { "Gender",           new[] { "جنسیت", "جنس" } },
            { "MemberSadat",      new[] { "سیادت", "سادات" } },
            { "MemberEducation",  new[] { "تحصیلات", "سطح تحصیلات" } },
            { "Skill",            new[] { "مهارت", "حرفه" } },
        };

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

        private SyncRecord BuildGuardian(Dictionary<string, string> row)
        {
            var rec = new SyncRecord { Entity = SyncEntity.Guardian };
            rec.PublicCode = ResolvePublicCode(row);
            MapFields(row, GuardianFieldMap, rec.SourceValues);
            // کد عمومی همیشه در ستون کلیدی دیتابیس (Code) هم قرار می‌گیرد.
            if (!string.IsNullOrWhiteSpace(rec.PublicCode))
                rec.SourceValues["Code"] = rec.PublicCode;
            rec.Title = rec.SourceValues.ContainsKey("HeadFullName") ? rec.SourceValues["HeadFullName"] : rec.PublicCode;
            return rec;
        }

        private SyncRecord BuildMember(Dictionary<string, string> row)
        {
            var rec = new SyncRecord { Entity = SyncEntity.FamilyMember };
            rec.PublicCode = ResolvePublicCode(row);
            MapFields(row, MemberFieldMap, rec.SourceValues);
            rec.Title = rec.SourceValues.ContainsKey("MemberName") ? rec.SourceValues["MemberName"] : "";
            rec.MemberKey = ComputeMemberKey(rec.SourceValues);
            return rec;
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

        private string ResolvePublicCode(Dictionary<string, string> row)
        {
            foreach (string header in PublicCodeHeaders)
            {
                string val = FindByHeader(row, header);
                if (!string.IsNullOrWhiteSpace(val))
                    return val.Trim();
            }
            return "";
        }

        private void MapFields(Dictionary<string, string> row,
            Dictionary<string, string[]> map, Dictionary<string, string> target)
        {
            foreach (var kv in map)
            {
                foreach (string candidate in kv.Value)
                {
                    string val = FindByHeader(row, candidate);
                    if (val != null)
                    {
                        target[kv.Key] = val.Trim();
                        break;
                    }
                }
            }
        }

        // جستجوی مقدار بر اساس عنوان ستون (تطبیق انعطاف‌پذیر: بی‌توجه به فاصله‌ی
        // اضافی و «ي/ك» عربی در برابر «ی/ک» فارسی).
        private static string FindByHeader(Dictionary<string, string> row, string header)
        {
            string norm = NormalizeHeader(header);
            foreach (var kv in row)
            {
                if (NormalizeHeader(kv.Key) == norm)
                    return kv.Value;
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

        // ─── تجزیه‌ی جدول HTML (تحمل‌پذیر، بدون وابستگی خارجی) ────────────────
        // بزرگ‌ترین جدول فایل (بیشترین ردیف) به‌عنوان جدول داده انتخاب می‌شود؛
        // ردیف اول با <th> (یا اولین ردیف) به‌عنوان سرستون، بقیه داده.
        public static List<Dictionary<string, string>> ParseHtmlTable(string filePath)
        {
            var rows = new List<Dictionary<string, string>>();
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
                    headers = cells; // اولین ردیف = سرستون
                    continue;
                }

                var dict = new Dictionary<string, string>(StringComparer.Ordinal);
                for (int i = 0; i < cells.Count && i < headers.Count; i++)
                {
                    string key = headers[i];
                    if (string.IsNullOrWhiteSpace(key)) key = "col" + i;
                    if (!dict.ContainsKey(key)) dict[key] = cells[i];
                }
                rows.Add(dict);
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
