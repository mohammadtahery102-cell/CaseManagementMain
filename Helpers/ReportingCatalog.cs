using System;
using System.Collections.Generic;

namespace CaseManagement.Helpers
{
    // فهرست گزارشات مرکز گزارش‌گیری — فقط سازمان‌دهی ناوبری.
    // منطق حسابداری / انبار / فروش تغییر نمی‌کند.
    public sealed class ReportNavItem
    {
        public string CategoryKey;
        public string CategoryTitle;
        public string Section;
        public string Title;
        public bool Ready;
        public string Dest;
        public string ExistingName;
    }

    public static class ReportingCatalog
    {
        public const string SoonHint = "در نسخه‌های بعدی تکمیل می‌شود";

        public const string Dashboards = "dashboards";
        public const string Finance = "finance";
        public const string Operations = "operations";
        public const string Parties = "parties";
        public const string Analytics = "analytics";
        public const string Smart = "smart";
        public const string Security = "security";
        public const string System = "system";
        public const string Output = "output";

        public static readonly string[] CategoryOrder = new[]
        {
            Dashboards, Finance, Operations, Parties, Analytics, Smart, Security, System, Output
        };

        private static readonly List<ReportNavItem> Items = Build();

        public static IList<ReportNavItem> All
        {
            get { return Items; }
        }

        public static string CategoryTitle(string key)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                if (Items[i].CategoryKey == key)
                    return Items[i].CategoryTitle;
            }
            return "گزارشات";
        }

        public static IList<ReportNavItem> ForCategory(string key)
        {
            List<ReportNavItem> list = new List<ReportNavItem>();
            for (int i = 0; i < Items.Count; i++)
            {
                if (Items[i].CategoryKey == key)
                    list.Add(Items[i]);
            }
            return list;
        }

        public static IList<ReportNavItem> Search(string query)
        {
            string q = (query ?? "").Trim();
            if (q.Length == 0) return new List<ReportNavItem>();
            List<ReportNavItem> list = new List<ReportNavItem>();
            for (int i = 0; i < Items.Count; i++)
            {
                ReportNavItem it = Items[i];
                if ((it.Title != null && ContainsIgnoreCase(it.Title, q))
                    || (it.Section != null && ContainsIgnoreCase(it.Section, q))
                    || (it.CategoryTitle != null && ContainsIgnoreCase(it.CategoryTitle, q)))
                    list.Add(it);
            }
            return list;
        }

        private static bool ContainsIgnoreCase(string text, string query)
        {
            return text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static int ReadyCount()
        {
            int n = 0;
            for (int i = 0; i < Items.Count; i++)
                if (Items[i].Ready) n++;
            return n;
        }

        private static List<ReportNavItem> Build()
        {
            List<ReportNavItem> list = new List<ReportNavItem>();

            // 1. داشبوردهای مدیریتی
            Add(list, Dashboards, "داشبوردهای مدیریتی", null, "خلاصه کسب و کار", true, "home", "داشبورد ERP");
            Add(list, Dashboards, "داشبوردهای مدیریتی", null, "عملکرد امروز", true, "home", "داشبورد ERP");
            Add(list, Dashboards, "داشبوردهای مدیریتی", null, "عملکرد ماه", true, "home", "داشبورد ERP");
            Add(list, Dashboards, "داشبوردهای مدیریتی", null, "شاخص‌های کلیدی KPI", true, "home", "داشبورد ERP");
            Add(list, Dashboards, "داشبوردهای مدیریتی", null, "وضعیت نقدینگی", true, "home", "داشبورد ERP");
            Add(list, Dashboards, "داشبوردهای مدیریتی", null, "سلامت کسب و کار", true, "ai-health", "داشبورد هوشمند");
            Add(list, Dashboards, "داشبوردهای مدیریتی", null, "مقایسه دوره‌ها", false, null, null);
            Add(list, Dashboards, "داشبوردهای مدیریتی", null, "داشبورد مدیرعامل", true, "ai-exec", "داشبورد مدیریتی هوش مصنوعی");

            // 2. گزارشات مالی
            Add(list, Finance, "گزارشات مالی", "صورت‌های مالی", "ترازنامه", true, "ledger-balance", "ترازنامه");
            Add(list, Finance, "گزارشات مالی", "صورت‌های مالی", "سود و زیان", true, "ledger-pnl", "سود و زیان");
            Add(list, Finance, "گزارشات مالی", "صورت‌های مالی", "جریان نقدی", true, "ledger-cashflow", "جریان نقدی");
            Add(list, Finance, "گزارشات مالی", "صورت‌های مالی", "تراز آزمایشی", true, "trial", "تراز آزمایشی");
            Add(list, Finance, "گزارشات مالی", "دفترها", "دفتر روزنامه", true, "daybook", "دفتر روزنامه");
            Add(list, Finance, "گزارشات مالی", "دفترها", "دفتر کل", true, "ledger-gl", "دفتر کل");
            Add(list, Finance, "گزارشات مالی", "دفترها", "دفتر معین", true, "subsidiary", "دفتر معین");
            Add(list, Finance, "گزارشات مالی", "دفترها", "دفتر تفصیلی", true, "ledger-detail", "دفتر تفصیلی حسابداری");
            Add(list, Finance, "گزارشات مالی", "سایر", "گردش حساب‌ها", true, "ledger-gl", "گردش حساب / دفتر کل");
            Add(list, Finance, "گزارشات مالی", "سایر", "درآمدها", true, "cash-reports", "خلاصه دریافت و پرداخت");
            Add(list, Finance, "گزارشات مالی", "سایر", "هزینه‌ها", true, "cash-reports", "گزارش هزینه‌ها");
            Add(list, Finance, "گزارشات مالی", "سایر", "صندوق و بانک", true, "cash-reports", "دفتر صندوق");
            Add(list, Finance, "گزارشات مالی", "سایر", "بدهکاران", true, "cash-debtors", "بدهکاران");
            Add(list, Finance, "گزارشات مالی", "سایر", "بستانکاران", true, "cash-creditors", "بستانکاران");
            Add(list, Finance, "گزارشات مالی", "سایر", "بودجه", true, "budget", "بودجه");

            // 3. گزارشات عملیات
            Add(list, Operations, "گزارشات عملیات", null, "فروش", true, "sale", "فروش");
            Add(list, Operations, "گزارشات عملیات", null, "خرید", true, "purchase", "خرید");
            Add(list, Operations, "گزارشات عملیات", null, "انبار", true, "inventory", "موجودی کالا");
            Add(list, Operations, "گزارشات عملیات", null, "ورود و خروج کالا", true, "kardex", "دفتر موجودی");
            Add(list, Operations, "گزارشات عملیات", null, "موجودی کالا", true, "stock", "موجودی انبار");
            Add(list, Operations, "گزارشات عملیات", null, "انبارگردانی", false, null, null);
            Add(list, Operations, "گزارشات عملیات", null, "دارایی ثابت", true, "assets", "دارایی ثابت");
            Add(list, Operations, "گزارشات عملیات", null, "حقوق و دستمزد", true, "payroll", "حقوق و دستمزد");
            Add(list, Operations, "گزارشات عملیات", null, "صندوق فروش", true, "pos", "صندوق فروش");

            // 4. گزارشات طرف‌های تجاری
            Add(list, Parties, "گزارشات طرف‌های تجاری", null, "مشتریان", true, "customers", "ارتباط با مشتریان");
            Add(list, Parties, "گزارشات طرف‌های تجاری", null, "تامین‌کنندگان", true, "vendors", "ارتباط با مشتریان");
            Add(list, Parties, "گزارشات طرف‌های تجاری", null, "مشتریان برتر", true, "sales-top", "فروش مشتری");
            Add(list, Parties, "گزارشات طرف‌های تجاری", null, "تامین‌کنندگان برتر", true, "purchase-top", "خرید تامین‌کننده");
            Add(list, Parties, "گزارشات طرف‌های تجاری", null, "مطالبات مشتریان", true, "trial", "تراز آزمایشی / مطالبات");
            Add(list, Parties, "گزارشات طرف‌های تجاری", null, "بدهی تامین‌کنندگان", true, "trial", "تراز آزمایشی / بدهی‌ها");
            Add(list, Parties, "گزارشات طرف‌های تجاری", null, "رتبه‌بندی طرف‌های تجاری", false, null, null);

            // 5. گزارشات تحلیلی
            Add(list, Analytics, "گزارشات تحلیلی", null, "تحلیل فروش", true, "ai-sales", "تحلیل فروش");
            Add(list, Analytics, "گزارشات تحلیلی", null, "تحلیل خرید", true, "ai-purchase", "تحلیل خرید");
            Add(list, Analytics, "گزارشات تحلیلی", null, "تحلیل سودآوری", true, "ai-finance", "تحلیل مالی");
            Add(list, Analytics, "گزارشات تحلیلی", null, "تحلیل موجودی", true, "ai-stock", "تحلیل موجودی");
            Add(list, Analytics, "گزارشات تحلیلی", null, "تحلیل مشتریان", true, "ai-customers", "تحلیل مشتریان");
            Add(list, Analytics, "گزارشات تحلیلی", null, "تحلیل تامین‌کنندگان", true, "purchase-top", "خرید تامین‌کننده");
            Add(list, Analytics, "گزارشات تحلیلی", null, "تحلیل نقدینگی", true, "ai-finance", "تحلیل مالی");
            Add(list, Analytics, "گزارشات تحلیلی", null, "تحلیل روندها", false, null, null);
            Add(list, Analytics, "گزارشات تحلیلی", null, "تحلیل مقایسه‌ای", false, null, null);

            // 6. گزارشات هوشمند
            Add(list, Smart, "گزارشات هوشمند", null, "پیش‌بینی فروش", true, "ai-forecast", "پیش‌بینی");
            Add(list, Smart, "گزارشات هوشمند", null, "پیش‌بینی نقدینگی", true, "ai-forecast", "پیش‌بینی");
            Add(list, Smart, "گزارشات هوشمند", null, "پیش‌بینی موجودی", false, null, null);
            Add(list, Smart, "گزارشات هوشمند", null, "هشدارهای هوشمند", true, "ai-alerts", "هشدارهای هوشمند");
            Add(list, Smart, "گزارشات هوشمند", null, "کشف ناهنجاری", false, null, null);
            Add(list, Smart, "گزارشات هوشمند", null, "فرصت‌های رشد", true, "crm-opps", "پایپلاین فرصت");
            Add(list, Smart, "گزارشات هوشمند", null, "ریسک‌های کسب و کار", true, "ai-alerts", "هشدارهای هوشمند");
            Add(list, Smart, "گزارشات هوشمند", null, "پیشنهادات هوشمند", true, "ai-chat", "گفتگو با هوش مصنوعی");

            // 7. گزارشات امنیت و کاربران
            Add(list, Security, "گزارشات امنیت و کاربران", null, "کاربران", true, "users", "مدیریت کاربران");
            Add(list, Security, "گزارشات امنیت و کاربران", null, "نقش‌ها", true, "roles", "ماتریس مجوزها");
            Add(list, Security, "گزارشات امنیت و کاربران", null, "دسترسی‌ها", true, "roles", "ماتریس مجوزها");
            Add(list, Security, "گزارشات امنیت و کاربران", null, "ورود کاربران", true, "audit", "ممیزی امنیتی");
            Add(list, Security, "گزارشات امنیت و کاربران", null, "فعالیت کاربران", true, "audit-tab", "گزارش رویدادها");
            Add(list, Security, "گزارشات امنیت و کاربران", null, "تغییرات سیستم", true, "audit", "ممیزی امنیتی");
            Add(list, Security, "گزارشات امنیت و کاربران", null, "عملیات مدیران", true, "audit", "ممیزی امنیتی");
            Add(list, Security, "گزارشات امنیت و کاربران", null, "گزارش حسابرسی", true, "audit", "ممیزی امنیتی");

            // 8. گزارشات سیستم
            Add(list, System, "گزارشات سیستم", null, "سلامت سیستم", false, null, null);
            Add(list, System, "گزارشات سیستم", null, "وضعیت پایگاه داده", false, null, null);
            Add(list, System, "گزارشات سیستم", null, "عملکرد سیستم", false, null, null);
            Add(list, System, "گزارشات سیستم", null, "خطاها", true, "errors", "گزارش خطاها");
            Add(list, System, "گزارشات سیستم", null, "سرویس‌ها", true, "modules", "مدیریت ماژول‌ها");
            Add(list, System, "گزارشات سیستم", null, "پشتیبان‌گیری", true, "backup", "تنظیمات / پشتیبان‌گیری");
            Add(list, System, "گزارشات سیستم", null, "رویدادها", true, "audit-tab", "گزارش رویدادها");

            // 9. خروجی و چاپ
            Add(list, Output, "خروجی و چاپ", null, "PDF", true, "report-builder", "گزارش‌ساز / چاپ");
            Add(list, Output, "خروجی و چاپ", null, "Excel", true, "report-builder", "گزارش‌ساز / اکسل");
            Add(list, Output, "خروجی و چاپ", null, "Word", false, null, null);
            Add(list, Output, "خروجی و چاپ", null, "چاپ مستقیم", true, "report-builder", "گزارش‌ساز / چاپ");
            Add(list, Output, "خروجی و چاپ", null, "ارسال ایمیل", false, null, null);
            Add(list, Output, "خروجی و چاپ", null, "آرشیو گزارشات", false, null, null);
            Add(list, Output, "خروجی و چاپ", null, "گزارشات زمان‌بندی‌شده", false, null, null);

            return list;
        }

        private static void Add(List<ReportNavItem> list, string key, string cat, string section,
            string title, bool ready, string dest, string existing)
        {
            list.Add(new ReportNavItem
            {
                CategoryKey = key,
                CategoryTitle = cat,
                Section = section,
                Title = title,
                Ready = ready,
                Dest = dest,
                ExistingName = existing
            });
        }
    }
}
