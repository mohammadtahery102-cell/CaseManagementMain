using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Web.Script.Serialization;
using CaseManagement.DAL;

namespace CaseManagement.GuardianCardIntegration
{
    // ─────────────────────────────────────────────────────────────────────────
    // طراحِ سبکِ کارت («Card Designer») — رنگ/فونت/پس‌زمینهٔ هر رو/واترمارک/
    // هولوگرام/QR/بارکد. آموزش — همهٔ این‌ها فقط داخلِ پوشهٔ کاریِ یک‌بارمصرف
    // (نه بستهٔ فریزشدهٔ GuardianCard) اعمال می‌شوند؛ نگاه کنید
    // GuardianCardRenderer.ApplyDesignOverrides. مقادیرِ خالی/null یعنی
    // «همان طرحِ اصلیِ GuardianCard، بدون تغییر» — نصب‌های موجود بدونِ هیچ
    // قالبی دقیقاً همان کارتِ قبلی را می‌گیرند.
    //
    // آموزش — محدودیتِ صادقانه: چون طرحِ کارت یک HTML/CSS ثابت است (نه یک
    // canvas پویا)، پس‌زمینه/واترمارک روی این عناصر ست می‌شوند نه «زیرِ همهٔ
    // لایه‌ها»؛ در بخش‌هایی که یک پنل (.panel) پس‌زمینهٔ مات دارد، تصویر پیدا
    // نیست — این دقیقاً همان محدودیتی است که در گزارشِ معماری پیش از این
    // کار توضیح داده شد (نسخهٔ سبک، نه طراحِ کاملِ drag-and-drop).
    // ─────────────────────────────────────────────────────────────────────────
    public class CardTemplateDesign
    {
        public string PrimaryColor { get; set; } = "";
        public string SecondaryColor { get; set; } = "";
        public string FontFamily { get; set; } = "";
        public string BackgroundFrontPath { get; set; } = "";
        public string BackgroundBackPath { get; set; } = "";
        public string WatermarkPath { get; set; } = "";
        public int WatermarkOpacityPercent { get; set; } = 15;
        public bool HologramEnabled { get; set; } = true;
        // آموزش — این دو با ToggleableFields (متنی) قاطی نشدند چون منطقِ
        // اعمالشان تصویری/staging است، نه صرفاً خالی‌کردنِ یک رشته.
        public bool ShowQRCode { get; set; } = false;
        public bool ShowBarcode { get; set; } = true;
    }

    // یک قالبِ کارت — نام + کدام فیلدهای اختیاری روشن‌اند + طراحِ بصری.
    // نگاه کنید آموزشِ کاملِ محدودیت معماری در DatabaseInitializer (بخش
    // TblCardTemplate).
    public class CardTemplate
    {
        public int TemplateID { get; set; }
        public string Name { get; set; }
        public Dictionary<string, bool> Fields { get; set; } = new Dictionary<string, bool>();
        public CardTemplateDesign Design { get; set; } = new CardTemplateDesign();
        public bool IsDefault { get; set; }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // لایه داده برای مدیریت قالب‌های کارت (بخش «CARD TEMPLATE MANAGEMENT»).
    // ─────────────────────────────────────────────────────────────────────────
    public class CardTemplateRepository
    {
        private readonly DatabaseHelper _db = new DatabaseHelper();
        private static readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();

        // فهرستِ کاملِ فیلدهای اختیاریِ متنیِ قابل‌کنترل — دقیقاً همان‌هایی که
        // در docs/TEMPLATE_SCHEMA.json پروژهٔ GuardianCard با
        // validation.required برابر false مشخص شده‌اند (یا مطابق تصمیمِ
        // کسب‌وکاریِ این پروژه اختیاری‌اند). فیلدهای الزامی (Photo،
        // GuardianName، FatherName، NationalID، تاریخ‌ها) اصلاً اینجا
        // نیستند — هرگز نباید خاموش شوند.
        public static readonly string[] ToggleableFields =
        {
            "PublicCode", "Website", "Email", "IssuedBy", "Position", "Logo", "Signature", "Stamp"
        };

        public List<CardTemplate> GetAll()
        {
            var list = new List<CardTemplate>();
            using (SQLiteConnection con = _db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(
                "SELECT TemplateID, Name, FieldsJson, DesignJson, IsDefault FROM TblCardTemplate ORDER BY IsDefault DESC, Name", con))
            {
                con.Open();
                using (SQLiteDataReader dr = cmd.ExecuteReader())
                    while (dr.Read())
                        list.Add(MapRow(dr));
            }
            return list;
        }

        public CardTemplate GetById(int templateId)
        {
            using (SQLiteConnection con = _db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(
                "SELECT TemplateID, Name, FieldsJson, DesignJson, IsDefault FROM TblCardTemplate WHERE TemplateID = @Id", con))
            {
                cmd.Parameters.AddWithValue("@Id", templateId);
                con.Open();
                using (SQLiteDataReader dr = cmd.ExecuteReader())
                    return dr.Read() ? MapRow(dr) : null;
            }
        }

        // templateId=0 یعنی رکورد جدید. نامِ تکراری با خطای UNIQUE مسدود می‌شود
        // (پیام آن در فراخوان مدیریت می‌شود).
        public int Save(int templateId, string name, Dictionary<string, bool> fields, CardTemplateDesign design)
        {
            string fieldsJson = _serializer.Serialize(fields);
            string designJson = _serializer.Serialize(design ?? new CardTemplateDesign());

            using (SQLiteConnection con = _db.GetConnection())
            {
                con.Open();
                if (templateId <= 0)
                {
                    using (SQLiteCommand cmd = new SQLiteCommand(
                        "INSERT INTO TblCardTemplate (Name, FieldsJson, DesignJson) VALUES (@Name, @Fields, @Design); SELECT last_insert_rowid();", con))
                    {
                        cmd.Parameters.AddWithValue("@Name", name.Trim());
                        cmd.Parameters.AddWithValue("@Fields", fieldsJson);
                        cmd.Parameters.AddWithValue("@Design", designJson);
                        return Convert.ToInt32(cmd.ExecuteScalar());
                    }
                }

                using (SQLiteCommand cmd = new SQLiteCommand(
                    "UPDATE TblCardTemplate SET Name = @Name, FieldsJson = @Fields, DesignJson = @Design WHERE TemplateID = @Id", con))
                {
                    cmd.Parameters.AddWithValue("@Name", name.Trim());
                    cmd.Parameters.AddWithValue("@Fields", fieldsJson);
                    cmd.Parameters.AddWithValue("@Design", designJson);
                    cmd.Parameters.AddWithValue("@Id", templateId);
                    cmd.ExecuteNonQuery();
                }
                return templateId;
            }
        }

        // قالبِ پیش‌فرض (IsDefault=1) هرگز حذف نمی‌شود — نصب‌های بدون انتخابِ
        // صریحِ قالب باید همیشه یک قالبِ معتبر (کامل) داشته باشند.
        public void Delete(int templateId)
        {
            using (SQLiteConnection con = _db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(
                "DELETE FROM TblCardTemplate WHERE TemplateID = @Id AND IsDefault = 0", con))
            {
                cmd.Parameters.AddWithValue("@Id", templateId);
                con.Open();
                cmd.ExecuteNonQuery();
            }
        }

        // اگر یک فیلدِ toggleable در FieldsJson نیامده باشد (مثلاً قالبی که
        // قبل از افزودنِ یک فیلدِ جدید به ToggleableFields ساخته شده)،
        // پیش‌فرض «روشن» در نظر گرفته می‌شود — امن‌تر از خاموش‌کردنِ ناخواسته‌ی
        // چیزی که کاربر قصدِ خاموش‌کردنش را نداشته.
        public static bool IsFieldEnabled(CardTemplate template, string field)
        {
            if (template == null) return true;
            return !template.Fields.TryGetValue(field, out bool v) || v;
        }

        // آموزش — اِعمالِ قالب روی فیلدهای *متنیِ* یک GuardianCardData از قبل
        // ساخته‌شده. فیلدهای الزامی (Photo/GuardianName/...) اصلاً در این
        // سوییچ نیستند، پس هرگز لمس نمی‌شوند.
        //
        // چرا Logo/Signature/Stamp اینجا نیستند؟ چون GuardianCardRenderer
        // (StageAndPopulate/StageAndPopulateBatch) این سه را از رویِ مسیرِ
        // مطلقِ *مبدأ* (نه data.Logo فعلی) دوباره Stage می‌کند و data.Logo را
        // بی‌قید‌وشرط بازنویسی می‌کند — پس خالی‌کردنِ data.Logo اینجا هیچ اثری
        // نمی‌داشت. فراخوان باید IsFieldEnabled را مستقیماً روی *پارامترِ
        // مسیرِ مبدأ* که به Renderer می‌دهد اعمال کند (نگاه کنید
        // FrmGuardianCardPreview.LoadCardAsync / FrmGuardianCardBatchPrint).
        public static void ApplyTextFields(GuardianCardData data, CardTemplate template)
        {
            if (data == null || template == null) return;

            if (!IsFieldEnabled(template, "PublicCode")) data.PublicCode = "";
            if (!IsFieldEnabled(template, "Website")) data.Website = "";
            if (!IsFieldEnabled(template, "Email")) data.Email = "";
            if (!IsFieldEnabled(template, "IssuedBy")) data.IssuedBy = "";
            if (!IsFieldEnabled(template, "Position")) data.Position = "";
        }

        private static CardTemplate MapRow(SQLiteDataReader dr)
        {
            var fields = DeserializeDict(dr["FieldsJson"]);
            var fieldsBool = new Dictionary<string, bool>();
            foreach (var kv in fields)
                fieldsBool[kv.Key] = Convert.ToBoolean(kv.Value);

            CardTemplateDesign design;
            object designCell = dr["DesignJson"];
            if (designCell == DBNull.Value || designCell == null)
            {
                design = new CardTemplateDesign();
            }
            else
            {
                try
                {
                    design = _serializer.Deserialize<CardTemplateDesign>(designCell.ToString()) ?? new CardTemplateDesign();
                }
                catch
                {
                    design = new CardTemplateDesign();
                }
            }

            return new CardTemplate
            {
                TemplateID = Convert.ToInt32(dr["TemplateID"]),
                Name = dr["Name"].ToString(),
                Fields = fieldsBool,
                Design = design,
                IsDefault = Convert.ToInt32(dr["IsDefault"]) != 0
            };
        }

        private static Dictionary<string, object> DeserializeDict(object cell)
        {
            string json = cell == DBNull.Value || cell == null ? "{}" : cell.ToString();
            try
            {
                return _serializer.Deserialize<Dictionary<string, object>>(json) ?? new Dictionary<string, object>();
            }
            catch
            {
                return new Dictionary<string, object>();
            }
        }
    }
}
