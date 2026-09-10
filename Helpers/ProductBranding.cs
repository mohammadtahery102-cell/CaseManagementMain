using System;
using System.Data;

namespace CaseManagement.Helpers
{
    // نمایش تجاری محصول — فقط متن و عنوان. منطق ماژول‌ها و اسکیما دست‌نخورده است.
    public static class ProductBranding
    {
        public const string Brand = "گنجینه";
        public const string CommercialName = "سیستم کسب و کار گنجینه";

        public static string ProductName
        {
            get { return ProductMode.IsErp ? CommercialName : "گنجینه"; }
        }

        // عنوان ورود: نام ثبت‌شده در تنظیمات؛ اگر خالی باشد نام محصول.
        public static string ApplicationTitle
        {
            get
            {
                try
                {
                    string org = SettingsHelper.Get(SettingsHelper.OrgName);
                    if (!string.IsNullOrWhiteSpace(org))
                        return org.Trim();
                }
                catch { }
                return ProductMode.IsErp ? CommercialName : "سیستم مدیریت پرونده گنجینه";
            }
        }

        public static string ProductSubtitle
        {
            get
            {
                return ProductMode.IsErp
                    ? "حسابداری، موجودی، خرید و فروش"
                    : "سیستم مدیریت پرونده";
            }
        }

        public static string SidebarTitle
        {
            get { return ProductMode.IsErp ? Brand : Brand; }
        }

        public static string SidebarSubtitle
        {
            get
            {
                return ProductMode.IsErp ? "سیستم مدیریت یکپارچه" : "سیستم مدیریت پرونده";
            }
        }

        public static string WindowTitle
        {
            get
            {
                return ProductMode.IsErp ? CommercialName : "مدیریت پرونده گنجینه";
            }
        }

        public static string LoginTitle
        {
            get
            {
                return ProductMode.IsErp ? ApplicationTitle : "سیستم مدیریت پرونده گنجینه";
            }
        }

        public static string LoginTagline
        {
            get
            {
                return ProductMode.IsErp
                    ? "مدیریت مالی، موجودی، خرید، فروش و منابع سازمان"
                    : "راهکار یکپارچه‌ی ثبت، پیگیری و گزارش‌گیریِ پرونده‌های ایتام";
            }
        }

        public static string SupportFallback
        {
            get
            {
                return ProductMode.IsErp
                    ? "پشتیبانی فنی " + CommercialName
                    : "پشتیبانی فنی سامانه گنجینه";
            }
        }

        public static string AboutProductName
        {
            get { return ProductMode.IsErp ? CommercialName : "سیستم مدیریت پرونده‌ها"; }
        }

        public static string AboutSubtitle
        {
            get
            {
                return ProductMode.IsErp
                    ? "نرم‌افزار یکپارچه مالی و عملیاتی"
                    : "سیستم مدیریت پرونده‌های اجتماعی";
            }
        }

        public static string WelcomeLine(string centerDisplay)
        {
            if (ProductMode.IsErp)
                return "به " + CommercialName + " خوش آمدید  ·  " + (centerDisplay ?? "");
            return "به سیستم مدیریت پرونده گنجینه خوش آمدید  ·  " + (centerDisplay ?? "");
        }

        public static string StatusLine(string centerDisplay, string userName, string roleDisplay)
        {
            if (ProductMode.IsErp)
            {
                return CommercialName
                    + "  ·  " + (centerDisplay ?? "")
                    + "  ·  " + (userName ?? "")
                    + " / " + (roleDisplay ?? "");
            }
            return (centerDisplay ?? "") + "  ·  " + (userName ?? "") + " / " + (roleDisplay ?? "");
        }

        public static string CashBookWindowTitle(string centerDisplay)
        {
            if (ProductMode.IsErp)
                return "صندوق و دریافت/پرداخت  —  " + (centerDisplay ?? "");
            return "حسابداری داخلی ایتام  —  " + (centerDisplay ?? "");
        }

        public static string CashBookBanner
        {
            get
            {
                return ProductMode.IsErp
                    ? "صندوق و دریافت/پرداخت"
                    : "💰  حسابداری داخلی ایتام";
            }
        }

        public static string PermissionCaption(string key, string current)
        {
            if (!ProductMode.IsErp || string.IsNullOrWhiteSpace(key)) return current;
            if (string.Equals(key, "Accounting.View", StringComparison.OrdinalIgnoreCase))
                return "مشاهده صندوق و دریافت/پرداخت";
            if (string.Equals(key, "Workflow.Review", StringComparison.OrdinalIgnoreCase))
                return "بررسی درخواست";
            return current;
        }

        public static string ModuleCaption(string key, string current)
        {
            if (!ProductMode.IsErp || string.IsNullOrWhiteSpace(key)) return current;
            if (string.Equals(key, "Accounting", StringComparison.OrdinalIgnoreCase))
                return "حسابداری";
            return current;
        }

        public const string EmptyList = "موردی برای نمایش وجود ندارد.";
        public const string Loading = "در حال بارگذاری…";
        public const string SavedOk = "ذخیره با موفقیت انجام شد.";
        public const string SelectRequired = "لطفاً فیلدهای الزامی را تکمیل کنید.";
        public const string MetricUnavailable = "—";

        public static string CategoryDisplayName(string stored)
        {
            if (!ProductMode.IsErp || string.IsNullOrWhiteSpace(stored)) return stored;
            switch (stored.Trim())
            {
                case "کمک خیرین": return "کمک دریافتی";
                case "شهریه ایتام عام": return "شهریه";
                case "شهریه ایتام سادات": return "شهریه";
                case "شهریه ایتام اهل سنت": return "شهریه";
                default: return stored;
            }
        }

        public static void OverlayCategoryNames(DataTable table, string column)
        {
            if (table == null || !ProductMode.IsErp || string.IsNullOrWhiteSpace(column)) return;
            if (!table.Columns.Contains(column)) return;
            foreach (DataRow row in table.Rows)
            {
                if (row.RowState == DataRowState.Deleted) continue;
                row[column] = CategoryDisplayName(Convert.ToString(row[column]));
            }
        }

        public static DataTable ApplyErpAdminPresentation(DataTable table)
        {
            if (table == null || !ProductMode.IsErp) return table;
            if (!table.Columns.Contains("کلید")) return table;

            for (int i = table.Rows.Count - 1; i >= 0; i--)
            {
                string key = Convert.ToString(table.Rows[i]["کلید"]);
                if (ProductMode.IsCharityModule(key) || ProductMode.IsCharityPermission(key))
                {
                    table.Rows[i].Delete();
                    continue;
                }

                if (table.Columns.Contains("عنوان"))
                    table.Rows[i]["عنوان"] = PermissionCaption(key, Convert.ToString(table.Rows[i]["عنوان"]));
                if (table.Columns.Contains("نام ماژول"))
                    table.Rows[i]["نام ماژول"] = ModuleCaption(key, Convert.ToString(table.Rows[i]["نام ماژول"]));
            }

            table.AcceptChanges();
            return table;
        }
    }
}
