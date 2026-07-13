using CaseManagement.DAL;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Windows.Forms;

namespace CaseManagement.Helpers
{
    // مقادیر پایه (Lookup) را از TblLookup بارگذاری می‌کند.
    // هر کنترل کومبوباکسی که قبلاً مقادیر سخت‌کد شده داشت،
    // حالا از این کلاس می‌خواند تا مدیر بتواند آن‌ها را از صفحه تنظیمات تغییر دهد.
    public static class LookupHelper
    {
        // کش به ازای هر Session برای جلوگیری از round-trip های مکرر
        private static readonly Dictionary<string, List<string>> _cache =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        public static List<string> GetValues(string category)
        {
            if (_cache.TryGetValue(category, out List<string> cached))
                return cached;

            List<string> values = new List<string>();
            try
            {
                using (var con = new DatabaseHelper().GetConnection())
                using (var cmd = new SQLiteCommand(@"
SELECT Value FROM TblLookup
WHERE  Category = @Cat AND IsActive = 1
ORDER  BY SortOrder, Value", con))
                {
                    cmd.Parameters.AddWithValue("@Cat", category);
                    con.Open();
                    using (var dr = cmd.ExecuteReader())
                        while (dr.Read())
                            values.Add(dr["Value"].ToString());
                }
            }
            catch { /* دیتابیس هنوز آماده نیست؛ لیست خالی برمی‌گردد */ }

            _cache[category] = values;
            return values;
        }

        // پس از تغییر مقادیر در FrmSettings، کش را پاک می‌کند
        public static void ClearCache() { _cache.Clear(); }

        // پر کردن یک ComboBox از TblLookup
        public static void FillCombo(ComboBox cmb, string category, string allLabel = null)
        {
            string current = cmb.Text;
            cmb.Items.Clear();

            if (!string.IsNullOrEmpty(allLabel))
                cmb.Items.Add(allLabel);

            foreach (string v in GetValues(category))
                cmb.Items.Add(v);

            // اگر مقدار فعلی هنوز در لیست هست، دوباره انتخاب می‌شود
            int idx = cmb.FindStringExact(current);
            if (idx >= 0)
                cmb.SelectedIndex = idx;
            else if (cmb.Items.Count > 0)
                cmb.SelectedIndex = 0;
        }

        // اضافه کردن AutoComplete روی TextBox از مقادیر Lookup
        public static void AttachAutoComplete(System.Windows.Forms.TextBox txt, string category)
        {
            List<string> vals = GetValues(category);
            if (vals.Count == 0) return;

            var src = new AutoCompleteStringCollection();
            src.AddRange(vals.ToArray());
            txt.AutoCompleteMode   = AutoCompleteMode.SuggestAppend;
            txt.AutoCompleteSource = AutoCompleteSource.CustomSource;
            txt.AutoCompleteCustomSource = src;
        }
    }
}
