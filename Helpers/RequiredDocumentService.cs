using CaseManagement.DAL;
using System.Collections.Generic;
using System.Data.SQLite;

namespace CaseManagement.Helpers
{
    // یک ردیفِ «دستهٔ سندِ الزامی که کم است» — برای نمایش در FrmDocs.
    public class MissingDocumentCategory
    {
        public int    DocumentCategoryID;
        public string Code;
        public string Name;
        public int    RequiredCount;
        public int    ExistingCount;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Phase 3 — منبعِ واحدِ حقیقت برای «سندِ الزامیِ کم است». هم سرویس (این
    // کلاس) و هم هر UI (فعلاً فقط FrmDocs) دقیقاً از همین کوئری استفاده
    // می‌کنند تا هیچ‌جا دو منطقِ متفاوت برای «کامل بودن اسناد» نباشد.
    // ═══════════════════════════════════════════════════════════════════════
    public static class RequiredDocumentService
    {
        // ═══════════════════════════════════════════════════════════════════
        // سندِ الزامیِ *مشروط* — قاعدهٔ مصوبِ کاربر
        //
        //   DisabilityCardStatus = «دارد»    ⇒ «عکس کارت معلولیت» الزامی
        //   «ندارد» / «در حال اقدام» / خالی  ⇒ اختیاری
        //
        // چرا لازم شد: این دسته با MinCount=1 برای نوعِ DISABLED در
        // DatabaseInitializer ثبت شده و CaseActivationValidator گذار به
        // «فعال» را روی هر سندِ الزامیِ کم *مسدود* می‌کند. نتیجه‌اش یک
        // تناقضِ واقعی بود: معلولی که اصلاً کارتِ دولتی ندارد — حالتی که
        // خودِ لیستِ DisabilityCardStatus صریحاً مجاز می‌داند — هرگز فعال
        // نمی‌شد و هیچ خدمتی دریافت نمی‌کرد.
        //
        // چرا مقایسهٔ دقیق و نه LIKE: رشتهٔ «ندارد» شاملِ «دارد» است، پس
        // LIKE دقیقاً همان حالتی را الزامی می‌کرد که باید معاف شود.
        //
        // چرا اینجا و نه در هر کوئری: سه مصرف‌کننده این ماتریس را می‌خوانند
        // (دو متدِ همین کلاس + CaseCompletionService.CalculateDocumentCompletion).
        // اگر قاعده در یکی اعمال می‌شد و در دیگری نه، «دروازهٔ فعال‌سازی» و
        // «درصدِ تکمیل» دربارهٔ یک پرونده دو حرفِ متفاوت می‌زدند.
        // ═══════════════════════════════════════════════════════════════════
        public const string DisabilityCardPhotoCode = "DISABILITY_CARD_PHOTO";
        public const string DisabilityCardStatusHas = "دارد";

        // برای کوئری‌هایی که TblCase را با نامِ مستعارِ c جوین کرده‌اند.
        public const string ConditionalFilterJoined =
            " AND NOT (dc.Code = @DisabCardPhotoCode" +
            " AND IFNULL(TRIM(c.DisabilityCardStatus), '') <> @DisabCardHas)";

        // برای کوئری‌هایی که TblCase را جوین نکرده‌اند و فقط @CasID دارند.
        public const string ConditionalFilterByCaseId =
            " AND NOT (dc.Code = @DisabCardPhotoCode" +
            " AND IFNULL(TRIM((SELECT c2.DisabilityCardStatus FROM TblCase c2" +
            " WHERE c2.CasID = @CasID)), '') <> @DisabCardHas)";

        // پارامترهای هر دو نسخهٔ بالا یکی‌اند؛ یک‌جا بسته می‌شوند تا هیچ
        // فراخوانی نتواند یکی را جا بیندازد.
        public static void AddConditionalParameters(SQLiteCommand cmd)
        {
            cmd.Parameters.AddWithValue("@DisabCardPhotoCode", DisabilityCardPhotoCode);
            cmd.Parameters.AddWithValue("@DisabCardHas", DisabilityCardStatusHas);
        }

        // فهرستِ دسته‌هایی که برای نوعِ درخواستِ این پرونده الزامی‌اند ولی
        // تعدادِ اسنادِ ثبت‌شده (غیربایگانی) از حداقلِ لازم کمتر است.
        public static List<MissingDocumentCategory> GetMissingRequiredCategories(int casId)
        {
            var result = new List<MissingDocumentCategory>();

            using (var con = new DatabaseHelper().GetConnection())
            using (var cmd = new SQLiteCommand(@"
SELECT dc.DocumentCategoryID, dc.Code, dc.Name, rd.MinCount,
       (SELECT COUNT(*) FROM TblDocs d
         WHERE d.CasID = c.CasID
           AND d.DocumentCategoryID = dc.DocumentCategoryID
           AND IFNULL(d.IsArchived, 0) = 0) AS ExistingCount
FROM TblCase c
JOIN TblRequiredDocument rd ON rd.RequestTypeID = c.RequestTypeID AND rd.IsActive = 1
JOIN TblDocumentCategory dc ON dc.DocumentCategoryID = rd.DocumentCategoryID AND dc.IsActive = 1
WHERE c.CasID = @CasID AND rd.IsMandatory = 1" + ConditionalFilterJoined + @"
ORDER BY dc.SortOrder;", con))
            {
                cmd.Parameters.AddWithValue("@CasID", casId);
                AddConditionalParameters(cmd);
                con.Open();

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int required = reader.GetInt32(reader.GetOrdinal("MinCount"));
                        int existing = reader.GetInt32(reader.GetOrdinal("ExistingCount"));
                        if (existing >= required) continue;

                        result.Add(new MissingDocumentCategory
                        {
                            DocumentCategoryID = reader.GetInt32(reader.GetOrdinal("DocumentCategoryID")),
                            Code = reader["Code"].ToString(),
                            Name = reader["Name"].ToString(),
                            RequiredCount = required,
                            ExistingCount = existing
                        });
                    }
                }
            }

            return result;
        }

        public static bool IsComplete(int casId)
        {
            return GetMissingRequiredCategories(casId).Count == 0;
        }

        // ═══════════════════════════════════════════════════════════════════
        // Phase 5.5-A — «سندِ ناقص».
        //
        // تعریفِ دقیق (لازم است صریح باشد، وگرنه «ناقص» هرچیزی می‌شود):
        // ردیفِ سند در یک دستهٔ الزامی وجود دارد — پس از نظرِ *شمارش* آن دسته
        // کامل به‌نظر می‌رسد — ولی فایلی به آن پیوست نشده (DocFilePath خالی).
        // چنین سندی دستهٔ الزامی را ظاهراً پر می‌کند بدونِ اینکه مدرکی واقعاً
        // موجود باشد؛ برای همین جدا از «کم بودن» شمرده و گزارش می‌شود.
        //
        // وضعیتِ تأیید (IsVerified) عمداً اینجا بررسی *نمی‌شود* — تصمیمِ صریحِ
        // کاربر برای این فاز.
        // ═══════════════════════════════════════════════════════════════════
        public static List<MissingDocumentCategory> GetIncompleteRequiredCategories(int casId)
        {
            var result = new List<MissingDocumentCategory>();

            using (var con = new DatabaseHelper().GetConnection())
            using (var cmd = new SQLiteCommand(@"
SELECT dc.DocumentCategoryID, dc.Code, dc.Name, rd.MinCount,
       (SELECT COUNT(*) FROM TblDocs d
         WHERE d.CasID = c.CasID
           AND d.DocumentCategoryID = dc.DocumentCategoryID
           AND IFNULL(d.IsArchived, 0) = 0
           AND IFNULL(TRIM(d.DocFilePath), '') = '') AS IncompleteCount
FROM TblCase c
JOIN TblRequiredDocument rd ON rd.RequestTypeID = c.RequestTypeID AND rd.IsActive = 1
JOIN TblDocumentCategory dc ON dc.DocumentCategoryID = rd.DocumentCategoryID AND dc.IsActive = 1
WHERE c.CasID = @CasID AND rd.IsMandatory = 1" + ConditionalFilterJoined + @"
ORDER BY dc.SortOrder;", con))
            {
                cmd.Parameters.AddWithValue("@CasID", casId);
                AddConditionalParameters(cmd);
                con.Open();

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int incomplete = reader.GetInt32(reader.GetOrdinal("IncompleteCount"));
                        if (incomplete == 0) continue;

                        result.Add(new MissingDocumentCategory
                        {
                            DocumentCategoryID = reader.GetInt32(reader.GetOrdinal("DocumentCategoryID")),
                            Code = reader["Code"].ToString(),
                            Name = reader["Name"].ToString(),
                            RequiredCount = reader.GetInt32(reader.GetOrdinal("MinCount")),
                            ExistingCount = incomplete
                        });
                    }
                }
            }

            return result;
        }
    }
}
