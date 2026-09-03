using System.Collections.Generic;

namespace CaseManagement.AssistanceReceiptIntegration
{
    // ─────────────────────────────────────────────────────────────────────────
    // بستهٔ مساعدتِ غیرنقدی — حداکثر ۵ بسته (طبقِ خواستهٔ کاربر)، هرکدام با
    // چند قلمِ جنس (مثلاً «آرد ۱ بوجی، روغن ۱ بشکه، شکر ۴ کیلو»).
    // ─────────────────────────────────────────────────────────────────────────
    public class AssistancePackage
    {
        public int PackageID { get; set; }
        public string Name { get; set; }
        public int SortOrder { get; set; }
        public List<AssistancePackageItem> Items { get; set; }

        public AssistancePackage()
        {
            Items = new List<AssistancePackageItem>();
        }
    }

    public class AssistancePackageItem
    {
        public int ItemID { get; set; }
        public int PackageID { get; set; }
        public string ItemName { get; set; }
        public decimal Quantity { get; set; }
        public string Unit { get; set; }
        public int SortOrder { get; set; }
    }
}
