using CaseManagement.DAL;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Windows.Forms;

namespace CaseManagement.Helpers
{
    public class ReferenceOption
    {
        public int    ID;
        public string Code;
        public string Name;

        public override string ToString() { return Name; }
    }

    // Phase 3 (بازبینی) — پرچم‌های نمایشِ بخشِ اختصاصی برای یک نوع درخواست.
    // منبعِ واحدِ حقیقت: FrmCase و هر منطقِ آیندهٔ دیگر (قواعدِ مساعدت،
    // امتیازِ آسیب‌پذیری) فقط همین را می‌خوانند، هرگز RequestTypeID را
    // هاردکد نمی‌کنند.
    public class RequestTypeSections
    {
        public bool ShowOrphanSection;
        public bool ShowDisabilitySection;
        public bool ShowMigrantSection;
        // Phase 4 — بخشِ «اطلاعات سرپرست» (ایتام/بی‌سرپرست/بدسرپرست).
        public bool ShowGuardianSection;
        // Phase 7 — بخشِ «نمایندهٔ قانونی» (فعلاً فقط «معلول»).
        public bool ShowRepresentativeSection;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Phase 3 — خواندنِ جداولِ مرجعِ تازه (TblRequestType/TblServiceStatus).
    // همانِ الگوی کشِ LookupHelper، برای دو جدولی که علاوه‌بر متن، شناسهٔ
    // پایدار (Code/ID) هم دارند — چیزی که TblLookup ندارد.
    // ═══════════════════════════════════════════════════════════════════════
    public static class ReferenceDataService
    {
        private static List<ReferenceOption> _requestTypes;
        private static List<ReferenceOption> _serviceStatuses;
        private static List<ReferenceOption> _documentCategories;

        public static void ClearCache()
        {
            _requestTypes = null;
            _serviceStatuses = null;
            _documentCategories = null;
            _requestTypeSectionsCache.Clear();
        }

        public static List<ReferenceOption> GetRequestTypes()
        {
            if (_requestTypes != null) return _requestTypes;
            _requestTypes = LoadOptions("TblRequestType", "RequestTypeID");
            return _requestTypes;
        }

        public static List<ReferenceOption> GetServiceStatuses()
        {
            if (_serviceStatuses != null) return _serviceStatuses;
            _serviceStatuses = LoadOptions("TblServiceStatus", "ServiceStatusID");
            return _serviceStatuses;
        }

        public static List<ReferenceOption> GetDocumentCategories()
        {
            if (_documentCategories != null) return _documentCategories;
            _documentCategories = LoadOptions("TblDocumentCategory", "DocumentCategoryID");
            return _documentCategories;
        }

        public static void FillDocumentCategoryCombo(ComboBox cmb)
        {
            FillCombo(cmb, GetDocumentCategories());
        }

        public static ReferenceOption FindDocumentCategoryByName(string name)
        {
            return GetDocumentCategories().Find(o => string.Equals(o.Name, name, StringComparison.Ordinal));
        }

        public static ReferenceOption FindDocumentCategoryById(int id)
        {
            return GetDocumentCategories().Find(o => o.ID == id);
        }

        // جست‌وجو با Code — قاعدهٔ پروژه: منطق با Code مقایسه می‌کند نه با نامِ
        // فارسی که کاربر می‌تواند در تنظیماتِ داده‌های مرجع عوضش کند.
        public static ReferenceOption FindDocumentCategoryByCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return null;
            return GetDocumentCategories().Find(
                o => string.Equals(o.Code, code, StringComparison.OrdinalIgnoreCase));
        }

        // FolderName در ReferenceOption نیست (فقط مخصوصِ این جدول است)؛
        // برای مسیرِ ذخیرهٔ فایل جداگانه خوانده می‌شود.
        public static string GetDocumentCategoryFolderName(int documentCategoryId)
        {
            try
            {
                using (var con = new DatabaseHelper().GetConnection())
                using (var cmd = new SQLiteCommand(
                    "SELECT FolderName FROM TblDocumentCategory WHERE DocumentCategoryID = @Id;", con))
                {
                    cmd.Parameters.AddWithValue("@Id", documentCategoryId);
                    con.Open();
                    object result = cmd.ExecuteScalar();
                    return result == null || result == DBNull.Value ? "General" : result.ToString();
                }
            }
            catch { return "General"; }
        }

        private static List<ReferenceOption> LoadOptions(string table, string idColumn)
        {
            var list = new List<ReferenceOption>();
            try
            {
                using (var con = new DatabaseHelper().GetConnection())
                using (var cmd = new SQLiteCommand(string.Format(
                    "SELECT {0} AS ID, Code, Name FROM {1} WHERE IsActive = 1 ORDER BY SortOrder;",
                    idColumn, table), con))
                {
                    con.Open();
                    using (var dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            list.Add(new ReferenceOption
                            {
                                ID = Convert.ToInt32(dr["ID"]),
                                Code = dr["Code"].ToString(),
                                Name = dr["Name"].ToString()
                            });
                        }
                    }
                }
            }
            catch { /* دیتابیس هنوز آماده نیست؛ لیست خالی برمی‌گردد */ }

            return list;
        }

        public static void FillRequestTypeCombo(ComboBox cmb)
        {
            FillCombo(cmb, GetRequestTypes());
        }

        public static void FillServiceStatusCombo(ComboBox cmb)
        {
            FillCombo(cmb, GetServiceStatuses());
        }

        private static void FillCombo(ComboBox cmb, List<ReferenceOption> options)
        {
            object current = cmb.SelectedValue;
            cmb.DisplayMember = "Name";
            cmb.ValueMember = "ID";
            cmb.DataSource = new List<ReferenceOption>(options);

            if (current is int id)
            {
                int idx = options.FindIndex(o => o.ID == id);
                if (idx >= 0) cmb.SelectedIndex = idx;
            }
        }

        public static ReferenceOption FindRequestTypeByCode(string code)
        {
            return GetRequestTypes().Find(o => string.Equals(o.Code, code, StringComparison.OrdinalIgnoreCase));
        }

        public static ReferenceOption FindServiceStatusByCode(string code)
        {
            return GetServiceStatuses().Find(o => string.Equals(o.Code, code, StringComparison.OrdinalIgnoreCase));
        }

        public static ReferenceOption FindRequestTypeById(int id)
        {
            return GetRequestTypes().Find(o => o.ID == id);
        }

        public static ReferenceOption FindServiceStatusById(int id)
        {
            return GetServiceStatuses().Find(o => o.ID == id);
        }

        // برای فرم‌هایی که هنوز کمبوی متنیِ Persian دارند (dual-write):
        // مقدارِ نمایشیِ انتخاب‌شده را به شناسهٔ مرجع نگاشت می‌کند.
        public static ReferenceOption FindRequestTypeByName(string name)
        {
            return GetRequestTypes().Find(o => string.Equals(o.Name, name, StringComparison.Ordinal));
        }

        public static ReferenceOption FindServiceStatusByName(string name)
        {
            return GetServiceStatuses().Find(o => string.Equals(o.Name, name, StringComparison.Ordinal));
        }

        private static readonly Dictionary<int, RequestTypeSections> _requestTypeSectionsCache =
            new Dictionary<int, RequestTypeSections>();

        // Phase 3 (بازبینی) — پرچم‌های بخش برای یک RequestTypeID مشخص.
        public static RequestTypeSections GetRequestTypeSections(int requestTypeId)
        {
            RequestTypeSections cached;
            if (_requestTypeSectionsCache.TryGetValue(requestTypeId, out cached))
                return cached;

            var result = new RequestTypeSections();
            try
            {
                using (var con = new DatabaseHelper().GetConnection())
                using (var cmd = new SQLiteCommand(@"
SELECT ShowOrphanSection, ShowDisabilitySection, ShowMigrantSection, ShowGuardianSection,
       COALESCE(ShowRepresentativeSection, 0) AS ShowRepresentativeSection
FROM TblRequestType WHERE RequestTypeID = @Id;", con))
                {
                    cmd.Parameters.AddWithValue("@Id", requestTypeId);
                    con.Open();
                    using (var dr = cmd.ExecuteReader())
                    {
                        if (dr.Read())
                        {
                            result.ShowOrphanSection = Convert.ToInt32(dr["ShowOrphanSection"]) != 0;
                            result.ShowDisabilitySection = Convert.ToInt32(dr["ShowDisabilitySection"]) != 0;
                            result.ShowMigrantSection = Convert.ToInt32(dr["ShowMigrantSection"]) != 0;
                            result.ShowGuardianSection = Convert.ToInt32(dr["ShowGuardianSection"]) != 0;
                            result.ShowRepresentativeSection = Convert.ToInt32(dr["ShowRepresentativeSection"]) != 0;
                        }
                    }
                }
            }
            catch { /* دیتابیس هنوز آماده نیست؛ همه بخش‌ها پنهان می‌مانند */ }

            _requestTypeSectionsCache[requestTypeId] = result;
            return result;
        }

        // برای فرم‌هایی که فقط نامِ نمایشیِ انتخاب‌شده را دارند (dual-write).
        public static RequestTypeSections GetRequestTypeSectionsByName(string requestTypeName)
        {
            var option = FindRequestTypeByName(requestTypeName);
            return option != null ? GetRequestTypeSections(option.ID) : new RequestTypeSections();
        }
    }
}
