using CaseManagement.DAL;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;

namespace CaseManagement.Helpers
{
    // یک نمایندهٔ قانونی (وکیل/قیّم) — شکلِ خواندنی/نوشتنیِ یک ردیفِ
    // TblCaseRepresentative. عمداً کلاسِ ساده و بدونِ منطق است؛ همهٔ قواعد
    // در CaseRepresentativeService.
    public class RepresentativeRow
    {
        public int    RepresentativeID;
        public int    CasID;
        public int    RepresentativeOrder;
        public string FullName;
        public string RelationshipToBeneficiary;
        public string IdCardType;
        public string NationalID;
        public string Phone;
        public string SecondaryPhone;
        public string Address;
        public string PhotoPath;
        public string Notes;

        // «خالی» یعنی کاربر هیچ‌چیزِ معناداری وارد نکرده — نمایندهٔ دومِ
        // دست‌نخورده باید ذخیره *نشود*، نه اینکه ردیفِ تهی بسازد.
        public bool IsEmpty
        {
            get
            {
                return Blank(FullName) && Blank(RelationshipToBeneficiary) && Blank(NationalID)
                    && Blank(Phone) && Blank(SecondaryPhone) && Blank(Address)
                    && Blank(PhotoPath) && Blank(Notes);
            }
        }

        private static bool Blank(string value) { return string.IsNullOrWhiteSpace(value); }
    }

    // آیا ثبتِ «نمایندهٔ اول» در این فراخوانی الزامی است؟
    //
    // آموزش — چرا پارامترِ صریح و نه پیش‌فرضِ پنهان: نسخهٔ اولِ این
    // قاعده را بدون قید اعمال می‌کرد، و نتیجه‌اش این بود که پرونده‌هایِ
    // قدیمیِ معلولیت — که نماینده‌ای ثبت‌شده ندارند و شاید نماینده‌ای
    // هم نداشته باشند — دیگر قابلِ ذخیره نبودند؛ حتی برای اصلاحِ
    // یک شماره تلفنِ بی‌ربط. با پارامترِ صریح، هر فراخوان مجبور است
    // بگوید در این موقعیت الزامی هست یا نه — پس فراموش‌کردنِ
    // این تفکیک دیگر ممکن نیست.
    public enum RepresentativeRequirement
    {
        // پروندهٔ موجود: نمایندهٔ خالی مجاز است، ولی اگر چیزی
        // وارد شده باشد باید کامل و معتبر باشد.
        Optional,
        // پروندهٔ جدید یا گذار به «فعال»: نمایندهٔ اول الزامی است.
        Required
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Phase 7 — تنها نویسنده/خوانندهٔ TblCaseRepresentative.
    //
    // چرا همه‌چیز اینجاست و نه در FrmCase: همان دلیلِ CaseModuleService و
    // FieldVisitService — حسابرسی، تایم‌لاین و صفِ همگام‌سازی باید سه پیامدِ
    // *غیرقابلِ فراموشی* هر نوشتن باشند. اگر در کدِ فرم پخش می‌شدند، اولین
    // مسیرِ تازه (ورودِ اکسل، سینک، ابزارِ اصلاح) یکی‌شان را جا می‌انداخت.
    //
    // تفاوت با ماژول‌های فاز ۴: آن‌ها UNIQUE(CasID) دارند (یک ردیف به‌ازای
    // هر پرونده)؛ اینجا UNIQUE(CasID, RepresentativeOrder) است — چند ردیف،
    // ولی هر «جایگاه» یکتا. پس UPSERT بر پایهٔ (CasID, Order) انجام می‌شود.
    // ═══════════════════════════════════════════════════════════════════════
    public static class CaseRepresentativeService
    {
        public const string TableName = "TblCaseRepresentative";
        public const string ModuleTitle = "نمایندهٔ قانونی";

        // جایگاه‌های تعریف‌شدهٔ فعلی. سقف داده است نه شِما: نمایندهٔ سوم فقط
        // این عدد را عوض می‌خواهد، نه ستون یا جدولِ تازه.
        public const int OrderPrimary = 1;
        public const int OrderSecondary = 2;
        public const int MaxRepresentatives = 2;

        public const string LookupRelationship = "RepresentativeRelationship";

        // ─── خواندن ──────────────────────────────────────────────────────────
        public static List<RepresentativeRow> GetAll(int casId)
        {
            var result = new List<RepresentativeRow>();
            if (casId <= 0) return result;

            try
            {
                using (var con = new DatabaseHelper().GetConnection())
                using (var cmd = new SQLiteCommand(@"
SELECT RepresentativeID, CasID, RepresentativeOrder, FullName, RelationshipToBeneficiary,
       IdCardType, NationalID, Phone, SecondaryPhone, Address, PhotoPath, Notes
FROM TblCaseRepresentative
WHERE CasID = @CasID AND IsActive = 1
ORDER BY RepresentativeOrder;", con))
                {
                    cmd.Parameters.AddWithValue("@CasID", casId);
                    con.Open();
                    using (var dr = cmd.ExecuteReader())
                    {
                        while (dr.Read()) result.Add(Read(dr));
                    }
                }
            }
            catch (Exception ex)
            {
                // خواندن هرگز نباید بازکردنِ پرونده را بشکند — همان قاعدهٔ
                // CaseModuleService.Load.
                System.Diagnostics.Debug.WriteLine("CaseRepresentativeService.GetAll failed: " + ex.Message);
            }

            return result;
        }

        public static RepresentativeRow Get(int casId, int order)
        {
            foreach (RepresentativeRow row in GetAll(casId))
                if (row.RepresentativeOrder == order) return row;
            return null;
        }

        public static int GetCount(int casId)
        {
            return GetAll(casId).Count;
        }

        // آیا پرونده نمایندهٔ اولِ ثبت‌شده دارد؟ دروازهٔ فعال‌سازی و
        // هشدارِ پروندهٔ قدیمی هر دو همین را می‌پرسند — یک تعریف،
        // تا دو جای مختلف نتوانند دو جوابِ متفاوت بدهند.
        //
        // ملاک، وجودِ ردیف است نه کامل‌بودنِ فیلدهایش: ردیفی که از
        // مسیرِ فرم آمده از اعتبارسنجی گذشته، و ردیفی که از واردات
        // یا سینک آمده دادهٔ موجود است و دروازه نباید دوباره
        // قضاوتش کند.
        public static bool HasPrimary(int casId)
        {
            return Get(casId, OrderPrimary) != null;
        }

        // برای گزارش/خروجی: همان داده به شکلِ DataTable، با نامِ ستونِ
        // ثابت — مصرف‌کننده‌ها (RDLC/اکسل) به ترتیبِ ستون وابسته نشوند.
        public static DataTable GetTable(int casId)
        {
            using (var con = new DatabaseHelper().GetConnection())
            using (var cmd = new SQLiteCommand(@"
SELECT RepresentativeID, CasID, RepresentativeOrder, FullName, RelationshipToBeneficiary,
       IdCardType, NationalID, Phone, SecondaryPhone, Address, PhotoPath, Notes
FROM TblCaseRepresentative
WHERE CasID = @CasID AND IsActive = 1
ORDER BY RepresentativeOrder;", con))
            {
                cmd.Parameters.AddWithValue("@CasID", casId);
                con.Open();
                using (var adapter = new SQLiteDataAdapter(cmd))
                {
                    var table = new DataTable("RepresentativeData");
                    adapter.Fill(table);
                    return table;
                }
            }
        }

        private static RepresentativeRow Read(IDataRecord dr)
        {
            return new RepresentativeRow
            {
                RepresentativeID = Convert.ToInt32(dr["RepresentativeID"]),
                CasID = Convert.ToInt32(dr["CasID"]),
                RepresentativeOrder = Convert.ToInt32(dr["RepresentativeOrder"]),
                FullName = Str(dr["FullName"]),
                RelationshipToBeneficiary = Str(dr["RelationshipToBeneficiary"]),
                IdCardType = Str(dr["IdCardType"]),
                NationalID = Str(dr["NationalID"]),
                Phone = Str(dr["Phone"]),
                SecondaryPhone = Str(dr["SecondaryPhone"]),
                Address = Str(dr["Address"]),
                PhotoPath = Str(dr["PhotoPath"]),
                Notes = Str(dr["Notes"])
            };
        }

        // ─── نوشتن (UPSERT بر پایهٔ CasID + Order) ───────────────────────────
        // پیش‌شرط: فراخوان باید *قبلاً* Validate را با موفقیت گذرانده باشد.
        // این متد دوباره اعتبارسنجیِ حداقلی می‌کند (نامِ خالی) تا هیچ مسیری
        // نتواند ردیفِ بی‌نام بسازد، ولی پیامِ کاربرپسند کارِ Validate است.
        public static int Save(int casId, RepresentativeRow row)
        {
            if (casId <= 0 || row == null) return 0;
            if (string.IsNullOrWhiteSpace(row.FullName))
                throw new ArgumentException("نام نماینده نمی‌تواند خالی باشد.");

            int order = row.RepresentativeOrder <= 0 ? OrderPrimary : row.RepresentativeOrder;
            int representativeId;
            bool isNew;
            Dictionary<string, string> before = null;

            try
            {
                using (var con = new DatabaseHelper().GetConnection())
                {
                    con.Open();
                    representativeId = GetExistingId(con, casId, order);
                    isNew = representativeId == 0;

                    if (isNew)
                    {
                        representativeId = Insert(con, casId, order, row);
                    }
                    else
                    {
                        before = ReadCurrentValues(con, representativeId);
                        Update(con, representativeId, row);
                    }
                }
            }
            catch (Exception ex)
            {
                // برخلافِ تایم‌لاین، شکستِ اینجا دادهٔ کسب‌وکاری را از دست
                // می‌دهد؛ پس بی‌صدا رد نمی‌شود.
                System.Diagnostics.Debug.WriteLine("CaseRepresentativeService.Save failed: " + ex.Message);
                throw;
            }

            string label = LabelFor(order);

            if (isNew)
            {
                TimelineService.LogRepresentativeCreated(casId, representativeId, label, row.FullName);
                AuditLogger.Log("ثبت نماینده قانونی", TableName, representativeId, "", Describe(row));
            }
            else
            {
                TimelineService.LogRepresentativeUpdated(casId, representativeId, label, row.FullName);
                LogFieldChanges(casId, representativeId, label, before, row);
            }

            SyncCapture(representativeId, isNew);
            return representativeId;
        }

        // ─── حذف ────────────────────────────────────────────────────────────
        // حذفِ واقعی (نه IsActive=0): «نمایندهٔ دوم را پاک کن» باید جایگاهش
        // را واقعاً آزاد کند، وگرنه UNIQUE(CasID, Order) ثبتِ نمایندهٔ دومِ
        // بعدی را رد می‌کرد. فایلِ عکس روی دیسک عمداً دست‌نخورده می‌ماند —
        // همان محافظه‌کاریِ FrmDocs/FieldVisitService.
        public static bool Delete(int casId, int order)
        {
            RepresentativeRow existing = Get(casId, order);
            if (existing == null) return false;

            // صفِ همگام‌سازیِ حذف باید *پیش از* DELETE برداشته شود: هویتِ
            // رکورد (GlobalID/ParentGlobalID) فقط تا وقتی ردیف هست خواندنی
            // است. همان الگوی دومرحله‌ایِ CaseModuleService.Delete.
            CaseManagement.Sync.SyncOutboxService.PendingDelete pending = null;
            try
            {
                pending = CaseManagement.Sync.SyncOutboxService.PrepareDelete(
                    TableName, existing.RepresentativeID);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("CaseRepresentativeService.PrepareDelete failed: " + ex.Message);
            }

            using (var con = new DatabaseHelper().GetConnection())
            using (var cmd = new SQLiteCommand(
                "DELETE FROM TblCaseRepresentative WHERE RepresentativeID = @Id;", con))
            {
                cmd.Parameters.AddWithValue("@Id", existing.RepresentativeID);
                con.Open();
                cmd.ExecuteNonQuery();
            }

            try { CaseManagement.Sync.SyncOutboxService.CommitDelete(pending); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("CaseRepresentativeService.CommitDelete failed: " + ex.Message);
            }

            string label = LabelFor(order);
            TimelineService.LogRepresentativeDeleted(casId, existing.RepresentativeID, label, existing.FullName);
            AuditLogger.Log("حذف نماینده قانونی", TableName, existing.RepresentativeID, Describe(existing), "");
            return true;
        }

        public static string LabelFor(int order)
        {
            if (order == OrderPrimary) return "نمایندهٔ اول";
            if (order == OrderSecondary) return "نمایندهٔ دوم";
            return "نمایندهٔ " + order;
        }

        // ═══════════════════════════════════════════════════════════════════
        // اعتبارسنجی — تنها مرجعِ قواعد.
        //
        // خواستهٔ صریح: «از ذخیرهٔ دادهٔ نامعتبر جلوگیری کن». پس این متد
        // پیامِ خطا برمی‌گرداند (نه bool) تا فرم بتواند دقیقاً همان جمله را
        // نشان دهد، و همان قواعد از هر مسیرِ دیگری (آزمون، ورودِ دسته‌ای)
        // هم اعمال شوند.
        //
        // خروجی null یعنی معتبر.
        // ═══════════════════════════════════════════════════════════════════
        // requirement مشخص می‌کند آیا *نبودنِ* نمایندهٔ اول خطاست:
        //   Required — پروندهٔ جدید، یا گذار به «فعال»
        //   Optional — ویرایشِ پروندهٔ موجود (سازگاری با دادهٔ قدیمی)
        //
        // در هر دو حالت، نماینده‌ای که *وارد شده* باید کامل و معتبر
        // باشد: سازگاری با گذشته یعنی مجبورنکردنِ کاربر به پرکردنِ چیزی
        // که هنوز نمی‌داند — نه پذیرفتنِ دادهٔ ناقصِ تازه.
        public static string Validate(int casId, RepresentativeRow primary, RepresentativeRow secondary,
            RepresentativeRequirement requirement)
        {
            bool hasPrimary = primary != null && !primary.IsEmpty;

            // ─── نمایندهٔ اول اجباری است ─────────────────────────────────────
            if (!hasPrimary)
            {
                if (requirement == RepresentativeRequirement.Required)
                    return "ثبت «نمایندهٔ اول» برای پروندهٔ معلولیت الزامی است.";

                // نمایندهٔ دومِ تنها بی‌معناست: جایگاهِ دوم بدونِ اول، هم
                // در گزارش و هم در چاپ ردیفِ خالی می‌ساخت.
                if (secondary != null && !secondary.IsEmpty)
                    return "برای ثبتِ «نمایندهٔ دوم» ابتدا باید «نمایندهٔ اول» ثبت شود.";

                return null;
            }

            string error = ValidateOne(primary, OrderPrimary);
            if (error != null) return error;

            // نمایندهٔ دومِ دست‌نخورده کاملاً مجاز است (اختیاری). ولی اگر
            // کاربر بخشی از آن را پر کرده باشد، باید کامل و معتبر باشد —
            // وگرنه رکوردِ نیمه‌کاره ذخیره می‌شد.
            bool hasSecondary = secondary != null && !secondary.IsEmpty;
            if (hasSecondary)
            {
                error = ValidateOne(secondary, OrderSecondary);
                if (error != null) return error;

                error = ValidateNotDuplicatePair(primary, secondary);
                if (error != null) return error;
            }

            // ─── نماینده نمی‌تواند خودِ ذینفع باشد ──────────────────────────
            error = ValidateNotBeneficiary(casId, primary, OrderPrimary);
            if (error != null) return error;
            if (hasSecondary)
            {
                error = ValidateNotBeneficiary(casId, secondary, OrderSecondary);
                if (error != null) return error;
            }

            return null;
        }

        // اعتبارسنجیِ یک نماینده به‌تنهایی.
        public static string ValidateOne(RepresentativeRow row, int order)
        {
            string label = LabelFor(order);

            if (string.IsNullOrWhiteSpace(row.FullName))
                return "نام کاملِ «" + label + "» را وارد کنید.";

            if (row.FullName.Trim().Length < 3)
                return "نام کاملِ «" + label + "» باید حداقل ۳ نویسه باشد.";

            // ─── نسبت: واژگانِ بسته ─────────────────────────────────────────
            // خواستهٔ صریح «نسبت‌های نامعتبر». مرجع، همان فهرستِ TblLookup
            // است که مدیر می‌تواند ویرایشش کند — نه فهرستی هاردکد در اینجا.
            if (string.IsNullOrWhiteSpace(row.RelationshipToBeneficiary))
                return "نسبتِ «" + label + "» با ذینفع را انتخاب کنید.";

            if (!IsKnownRelationship(row.RelationshipToBeneficiary))
                return "نسبتِ «" + label + "» با ذینفع معتبر نیست؛ از فهرست انتخاب کنید.";

            // ─── تذکره ──────────────────────────────────────────────────────
            // قاعده کاملاً در IdCardHelper است تا با فرمِ پرونده/اعضا یکی
            // بماند؛ ولی برخلافِ آن‌ها اینجا شماره *اجباری* است — یک نمایندهٔ
            // قانونیِ بدونِ هویتِ قابلِ استعلام بی‌معناست.
            if (string.IsNullOrWhiteSpace(row.NationalID))
                return "شمارهٔ تذکرهٔ «" + label + "» را وارد کنید.";

            string idError;
            if (!IdCardHelper.IsValid(row.IdCardType, row.NationalID, out idError))
                return label + ": " + idError;

            // ─── تلفن ───────────────────────────────────────────────────────
            if (string.IsNullOrWhiteSpace(row.Phone))
                return "شمارهٔ تماسِ «" + label + "» را وارد کنید.";

            string phoneError;
            if (!IsValidPhone(row.Phone, out phoneError))
                return label + " (شماره تماس): " + phoneError;

            if (!string.IsNullOrWhiteSpace(row.SecondaryPhone))
            {
                if (!IsValidPhone(row.SecondaryPhone, out phoneError))
                    return label + " (شماره تماس دوم): " + phoneError;

                // دو شمارهٔ یکسان یعنی یکی از آن‌ها اطلاعاتی اضافه نمی‌کند.
                if (SamePhone(row.Phone, row.SecondaryPhone))
                    return label + ": شمارهٔ تماس دوم نباید با شمارهٔ تماس اول یکسان باشد.";
            }

            return null;
        }

        // ─── نمایندهٔ تکراری ─────────────────────────────────────────────────
        // خواستهٔ صریح: «نمایندگان تکراری» و «تذکره‌های تکراری». هر دو بررسی
        // *درونِ یک پرونده* قطعی‌اند و ذخیره را می‌بندند.
        private static string ValidateNotDuplicatePair(RepresentativeRow primary, RepresentativeRow secondary)
        {
            if (SameId(primary.NationalID, secondary.NationalID))
                return "شمارهٔ تذکرهٔ نمایندهٔ اول و دوم یکسان است؛ یک شخص نمی‌تواند دو بار ثبت شود.";

            if (SameName(primary.FullName, secondary.FullName) &&
                SamePhone(primary.Phone, secondary.Phone))
                return "نمایندهٔ اول و دوم یک شخص‌اند (نام و شمارهٔ تماسِ یکسان)؛ نمایندهٔ دوم باید شخصِ دیگری باشد.";

            return null;
        }

        // ─── نماینده ≠ ذینفع ────────────────────────────────────────────────
        // یک شخص نمی‌تواند نمایندهٔ قانونیِ خودش باشد. با تذکرهٔ سرپرستِ
        // پرونده مقایسه می‌شود، چون آن، هویتِ ثبت‌شدهٔ ذینفع در TblCase است.
        private static string ValidateNotBeneficiary(int casId, RepresentativeRow row, int order)
        {
            if (casId <= 0 || string.IsNullOrWhiteSpace(row.NationalID)) return null;

            try
            {
                using (var con = new DatabaseHelper().GetConnection())
                using (var cmd = new SQLiteCommand(
                    "SELECT COALESCE(HeadTazkiraNo, '') FROM TblCase WHERE CasID = @CasID;", con))
                {
                    cmd.Parameters.AddWithValue("@CasID", casId);
                    con.Open();
                    object result = cmd.ExecuteScalar();
                    string headId = result == null || result == DBNull.Value ? "" : result.ToString();

                    if (SameId(headId, row.NationalID))
                        return LabelFor(order) + ": شمارهٔ تذکره با خودِ ذینفع یکسان است؛ یک شخص نمی‌تواند نمایندهٔ قانونیِ خود باشد.";
                }
            }
            catch (Exception ex)
            {
                // نبودِ دیتابیس نباید ذخیره را ببندد — این بررسی تکمیلی است.
                System.Diagnostics.Debug.WriteLine("ValidateNotBeneficiary failed: " + ex.Message);
            }

            return null;
        }

        // ─── همان تذکره در پرونده‌های دیگر ───────────────────────────────────
        // عمداً *هشدار* است نه خطا: یک وکیلِ رسمی یا قیّمِ خانوادگی قانوناً
        // می‌تواند نمایندهٔ چند ذینفع باشد. بستنِ این حالت، دادهٔ درست را
        // غیرقابلِ ثبت می‌کرد. خروجی: فهرستِ کدِ پرونده‌ها (خالی = بی‌سابقه).
        public static List<string> FindOtherCasesWithSameId(int casId, string nationalId)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(nationalId)) return result;

            string digits = IdCardHelper.DigitsOnly(nationalId);
            if (digits.Length == 0) return result;

            try
            {
                using (var con = new DatabaseHelper().GetConnection())
                using (var cmd = new SQLiteCommand(@"
SELECT DISTINCT c.Code
FROM TblCaseRepresentative r
JOIN TblCase c ON c.CasID = r.CasID
WHERE r.CasID <> @CasID AND r.IsActive = 1
  AND REPLACE(REPLACE(COALESCE(r.NationalID, ''), '-', ''), ' ', '') = @Digits
ORDER BY c.Code;", con))
                {
                    cmd.Parameters.AddWithValue("@CasID", casId);
                    cmd.Parameters.AddWithValue("@Digits", digits);
                    con.Open();
                    using (var dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            string code = Str(dr[0]);
                            if (code.Length > 0) result.Add(code);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("FindOtherCasesWithSameId failed: " + ex.Message);
            }

            return result;
        }

        // ─── قالبِ شمارهٔ تماس ───────────────────────────────────────────────
        // قاعده عمداً سخت‌گیرتر از «هر رشته‌ای» و آسان‌گیرتر از یک الگوی
        // ثابتِ افغانستان است: شماره‌های ثبت‌شدهٔ موجود هم داخلی‌اند و هم
        // بین‌المللی. فقط رقم (پس از یکسان‌سازیِ ارقامِ فارسی/عربی) و طولِ
        // منطقی بررسی می‌شود.
        public const int PhoneMinDigits = 7;
        public const int PhoneMaxDigits = 15;

        public static bool IsValidPhone(string phone, out string error)
        {
            error = "";
            string raw = (phone ?? "").Trim();
            if (raw.Length == 0) return true;   // خالی‌بودن را فراخوان تصمیم می‌گیرد

            // کاراکترهای رایجِ قالب‌بندی مجازند؛ حرف نه.
            foreach (char c in raw)
            {
                bool isDigit = (c >= '0' && c <= '9')
                    || (c >= 0x06F0 && c <= 0x06F9)   // ارقام فارسی
                    || (c >= 0x0660 && c <= 0x0669);  // ارقام عربی
                if (isDigit) continue;
                if (c == '+' || c == '-' || c == ' ' || c == '(' || c == ')') continue;

                error = "شمارهٔ تماس فقط می‌تواند شامل رقم و نشانه‌های + - ( ) و فاصله باشد.";
                return false;
            }

            string digits = IdCardHelper.DigitsOnly(raw);
            if (digits.Length < PhoneMinDigits || digits.Length > PhoneMaxDigits)
            {
                error = "شمارهٔ تماس باید بین " + PhoneMinDigits + " تا " + PhoneMaxDigits + " رقم باشد.";
                return false;
            }

            return true;
        }

        // نسبت باید در فهرستِ مرجع باشد. اگر فهرست به هر دلیل خالی برگردد
        // (دیتابیسِ نیمه‌آماده) قاعده باز می‌شود تا ورودِ داده قفل نشود —
        // fail-open فقط در همین حالتِ زیرساختی، نه برای مقدارِ اشتباه.
        public static bool IsKnownRelationship(string relationship)
        {
            string value = (relationship ?? "").Trim();
            if (value.Length == 0) return false;

            try
            {
                List<string> known = LookupHelper.GetValues(LookupRelationship);
                if (known == null || known.Count == 0) return true;

                foreach (string item in known)
                    if (string.Equals((item ?? "").Trim(), value, StringComparison.Ordinal)) return true;

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("IsKnownRelationship failed: " + ex.Message);
                return true;
            }
        }

        // ─── مقایسه‌های هنجارشده ─────────────────────────────────────────────
        // تذکره با/بدون خط تیره و با ارقامِ فارسی همان تذکره است؛ مقایسهٔ خامِ
        // رشته‌ای «۱۲۳۴-...» و «1234...» را دو شخصِ متفاوت می‌دید و بررسیِ
        // تکراری را بی‌اثر می‌کرد.
        public static bool SameId(string a, string b)
        {
            string da = IdCardHelper.DigitsOnly(a ?? "");
            string db = IdCardHelper.DigitsOnly(b ?? "");
            return da.Length > 0 && da == db;
        }

        public static bool SamePhone(string a, string b)
        {
            string da = IdCardHelper.DigitsOnly(a ?? "");
            string db = IdCardHelper.DigitsOnly(b ?? "");
            return da.Length > 0 && da == db;
        }

        public static bool SameName(string a, string b)
        {
            string na = NormalizeName(a);
            string nb = NormalizeName(b);
            return na.Length > 0 && na == nb;
        }

        // فاصله‌های چندگانه و گونه‌های عربیِ «ی»/«ک» در نامِ فارسی رایج‌اند و
        // نباید دو نوشتنِ یک نام را دو شخص نشان دهند.
        private static string NormalizeName(string value)
        {
            string text = (value ?? "").Trim();
            if (text.Length == 0) return "";

            text = text.Replace('ي', 'ی').Replace('ك', 'ک');
            var sb = new System.Text.StringBuilder(text.Length);
            bool lastWasSpace = false;
            foreach (char c in text)
            {
                bool isSpace = char.IsWhiteSpace(c) || c == '‌';
                if (isSpace)
                {
                    if (!lastWasSpace && sb.Length > 0) sb.Append(' ');
                    lastWasSpace = true;
                }
                else
                {
                    sb.Append(c);
                    lastWasSpace = false;
                }
            }
            return sb.ToString().Trim();
        }

        // ═══════════════════════════════════════════════════════════════════
        // حسابرسیِ سطحِ فیلد — خواستهٔ صریحِ «مقدار قبلی/جدید/کاربر/تاریخ/ساعت».
        //
        // تاریخ/ساعت و کاربر را خودِ TblCaseTimeline و TblAuditLog از
        // SecurityContext می‌گیرند (ستون‌های EventDate/UserID/Username)، پس
        // اینجا فقط «چه فیلدی، از چه، به چه» می‌ماند.
        //
        // فقط فیلدهای واقعاً تغییرکرده ثبت می‌شوند؛ ذخیرهٔ بدونِ تغییر نباید
        // تایم‌لاین را با ردیف‌های بی‌محتوا پر کند (همان قاعدهٔ #۳۶).
        // ═══════════════════════════════════════════════════════════════════
        private static void LogFieldChanges(int casId, int representativeId, string label,
            Dictionary<string, string> before, RepresentativeRow row)
        {
            if (before == null) return;

            var after = ToDictionary(row);
            foreach (var pair in after)
            {
                string oldValue;
                if (!before.TryGetValue(pair.Key, out oldValue)) oldValue = "";
                string newValue = pair.Value ?? "";
                if (string.Equals(oldValue ?? "", newValue, StringComparison.Ordinal)) continue;

                string fieldLabel = FieldLabel(pair.Key);

                TimelineService.LogRepresentativeFieldChanged(
                    casId, representativeId, label, fieldLabel, oldValue ?? "", newValue);

                // خواستهٔ صریح شش عملِ نام‌برده را جدا می‌شمارد (تغییرِ عکس/
                // آدرس/تلفن)؛ نامِ عملیات در حسابرسی همان تفکیک را نگه می‌دارد
                // تا بتوان مستقیماً روی TblAuditLog فیلتر کرد.
                AuditLogger.Log(OperationFor(pair.Key), TableName, representativeId,
                    oldValue ?? "", newValue);
            }
        }

        private static string OperationFor(string column)
        {
            switch (column)
            {
                case "PhotoPath": return "تغییر عکس نماینده";
                case "Address": return "تغییر آدرس نماینده";
                case "Phone":
                case "SecondaryPhone": return "تغییر تماس نماینده";
                default: return "ویرایش نماینده قانونی";
            }
        }

        private static string FieldLabel(string column)
        {
            switch (column)
            {
                case "FullName": return "نام کامل";
                case "RelationshipToBeneficiary": return "نسبت با ذینفع";
                case "IdCardType": return "نوع تذکره";
                case "NationalID": return "شماره تذکره";
                case "Phone": return "شماره تماس";
                case "SecondaryPhone": return "شماره تماس دوم";
                case "Address": return "آدرس";
                case "PhotoPath": return "عکس";
                case "Notes": return "یادداشت";
                default: return column;
            }
        }

        private static string Describe(RepresentativeRow row)
        {
            return LabelFor(row.RepresentativeOrder) + ": " + (row.FullName ?? "") +
                   " | نسبت: " + (row.RelationshipToBeneficiary ?? "") +
                   " | تذکره: " + (row.NationalID ?? "") +
                   " | تماس: " + (row.Phone ?? "");
        }

        // ─── درونی ──────────────────────────────────────────────────────────
        private static readonly string[] DataColumns =
        {
            "FullName", "RelationshipToBeneficiary", "IdCardType", "NationalID",
            "Phone", "SecondaryPhone", "Address", "PhotoPath", "Notes"
        };

        private static Dictionary<string, string> ToDictionary(RepresentativeRow row)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "FullName",                  Trim(row.FullName) },
                { "RelationshipToBeneficiary", Trim(row.RelationshipToBeneficiary) },
                { "IdCardType",                Trim(row.IdCardType) },
                { "NationalID",                Trim(row.NationalID) },
                { "Phone",                     Trim(row.Phone) },
                { "SecondaryPhone",            Trim(row.SecondaryPhone) },
                { "Address",                   Trim(row.Address) },
                { "PhotoPath",                 Trim(row.PhotoPath) },
                { "Notes",                     Trim(row.Notes) }
            };
        }

        private static int GetExistingId(SQLiteConnection con, int casId, int order)
        {
            using (var cmd = new SQLiteCommand(
                "SELECT RepresentativeID FROM TblCaseRepresentative WHERE CasID = @CasID AND RepresentativeOrder = @Order LIMIT 1;", con))
            {
                cmd.Parameters.AddWithValue("@CasID", casId);
                cmd.Parameters.AddWithValue("@Order", order);
                object result = cmd.ExecuteScalar();
                return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
            }
        }

        private static Dictionary<string, string> ReadCurrentValues(SQLiteConnection con, int representativeId)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var quoted = new List<string>();
                foreach (string column in DataColumns) quoted.Add("[" + column + "]");

                using (var cmd = new SQLiteCommand("SELECT " + string.Join(", ", quoted.ToArray()) +
                    " FROM TblCaseRepresentative WHERE RepresentativeID = @Id;", con))
                {
                    cmd.Parameters.AddWithValue("@Id", representativeId);
                    using (var dr = cmd.ExecuteReader())
                    {
                        if (!dr.Read()) return result;
                        foreach (string column in DataColumns)
                            result[column] = Str(dr[column]);
                    }
                }
            }
            catch (Exception ex)
            {
                // خواندنِ مقدارِ قبلی فقط برای حسابرسی است — شکستش نباید
                // ذخیرهٔ خودِ داده را بشکند.
                System.Diagnostics.Debug.WriteLine("CaseRepresentativeService.ReadCurrentValues failed: " + ex.Message);
                return null;
            }
            return result;
        }

        private static int Insert(SQLiteConnection con, int casId, int order, RepresentativeRow row)
        {
            using (var cmd = new SQLiteCommand(@"
INSERT INTO TblCaseRepresentative
    (CasID, RepresentativeOrder, FullName, RelationshipToBeneficiary, IdCardType, NationalID,
     Phone, SecondaryPhone, Address, PhotoPath, Notes, IsActive, CenterID, CreatedBy, GlobalID)
VALUES
    (@CasID, @Order, @FullName, @Relationship, @IdCardType, @NationalID,
     @Phone, @SecondaryPhone, @Address, @PhotoPath, @Notes, 1, @CenterID, @CreatedBy,
     lower(hex(randomblob(4))) || '-' || lower(hex(randomblob(2))) || '-' ||
     lower(hex(randomblob(2))) || '-' || lower(hex(randomblob(2))) || '-' || lower(hex(randomblob(6))));", con))
            {
                Bind(cmd, row);
                cmd.Parameters.AddWithValue("@CasID", casId);
                cmd.Parameters.AddWithValue("@Order", order);
                cmd.Parameters.AddWithValue("@CenterID",
                    SecurityContext.CurrentCenterId > 0 ? (object)SecurityContext.CurrentCenterId : DBNull.Value);
                cmd.Parameters.AddWithValue("@CreatedBy", (object)SecurityContext.Username ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }

            using (var idCmd = new SQLiteCommand("SELECT last_insert_rowid();", con))
                return Convert.ToInt32((long)idCmd.ExecuteScalar());
        }

        private static void Update(SQLiteConnection con, int representativeId, RepresentativeRow row)
        {
            using (var cmd = new SQLiteCommand(@"
UPDATE TblCaseRepresentative SET
    FullName = @FullName, RelationshipToBeneficiary = @Relationship,
    IdCardType = @IdCardType, NationalID = @NationalID,
    Phone = @Phone, SecondaryPhone = @SecondaryPhone,
    Address = @Address, PhotoPath = @PhotoPath, Notes = @Notes,
    IsActive = 1, UpdatedAt = datetime('now')
WHERE RepresentativeID = @Id;", con))
            {
                Bind(cmd, row);
                cmd.Parameters.AddWithValue("@Id", representativeId);
                cmd.ExecuteNonQuery();
            }
        }

        private static void Bind(SQLiteCommand cmd, RepresentativeRow row)
        {
            cmd.Parameters.AddWithValue("@FullName", Trim(row.FullName));
            cmd.Parameters.AddWithValue("@Relationship", NullIfEmpty(row.RelationshipToBeneficiary));
            cmd.Parameters.AddWithValue("@IdCardType", NullIfEmpty(row.IdCardType));
            cmd.Parameters.AddWithValue("@NationalID", NullIfEmpty(row.NationalID));
            cmd.Parameters.AddWithValue("@Phone", NullIfEmpty(row.Phone));
            cmd.Parameters.AddWithValue("@SecondaryPhone", NullIfEmpty(row.SecondaryPhone));
            cmd.Parameters.AddWithValue("@Address", NullIfEmpty(row.Address));
            cmd.Parameters.AddWithValue("@PhotoPath", NullIfEmpty(row.PhotoPath));
            cmd.Parameters.AddWithValue("@Notes", NullIfEmpty(row.Notes));
        }

        private static void SyncCapture(int representativeId, bool isNew)
        {
            try
            {
                CaseManagement.Sync.SyncOutboxService.Capture(TableName, representativeId,
                    isNew
                        ? CaseManagement.Sync.OfflineSyncInitializer.OperationCreate
                        : CaseManagement.Sync.OfflineSyncInitializer.OperationUpdate);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("CaseRepresentativeService.SyncCapture failed: " + ex.Message);
            }
        }

        private static string Str(object value)
        {
            return value == DBNull.Value || value == null ? "" : value.ToString();
        }

        private static string Trim(string value)
        {
            return (value ?? "").Trim();
        }

        private static object NullIfEmpty(string value)
        {
            string trimmed = (value ?? "").Trim();
            return trimmed.Length == 0 ? (object)DBNull.Value : trimmed;
        }
    }
}
