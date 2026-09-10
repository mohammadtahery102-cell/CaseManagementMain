using System;
using System.Windows.Forms;
using CaseManagement.Accounting;
using CaseManagement.Accounting.Ledger.Adapters;
using CaseManagement.AiPlatform.Adapters;
using CaseManagement.Assets.Adapters;
using CaseManagement.Crm.Adapters;
using CaseManagement.Enterprise;
using CaseManagement.Inventory.Adapters;
using CaseManagement.Payroll.Adapters;
using CaseManagement.Pos.Adapters;
using CaseManagement.Purchase.Adapters;
using CaseManagement.Sales.Adapters;

namespace CaseManagement.Helpers
{
    public static class ErpWorkspace
    {
        public static void Open(IWin32Window owner, string dest)
        {
            string key = (dest ?? "").Trim().ToLowerInvariant();
            switch (key)
            {
                case "home":
                    {
                        FrmDashboard dash = FindDashboard(owner);
                        if (dash != null) dash.ShowErpHome();
                        return;
                    }
                case "audit-tab":
                    {
                        FrmDashboard dash = FindDashboard(owner);
                        if (dash != null) dash.ShowAuditTab();
                        return;
                    }
                case "ledger":
                case "ledger-gl":
                    Show(owner, new FrmLedger("دفتر کل", FrmLedger.TabReports, FrmLedger.ReportGl));
                    return;
                case "journal":
                    Show(owner, new FrmLedger("ثبت سند", FrmLedger.TabJournals, null));
                    return;
                case "draft":
                    Show(owner, new FrmLedger("پیش‌نویس اسناد", FrmLedger.TabJournals, null));
                    return;
                case "daybook":
                case "ledger-journal":
                    Show(owner, new FrmLedger("دفتر روزنامه", FrmLedger.TabJournals, null));
                    return;
                case "subsidiary":
                    Show(owner, new FrmLedger("دفتر معین", FrmLedger.TabReports, FrmLedger.ReportGl));
                    return;
                case "detail":
                    Show(owner, new FrmLedger("حساب‌های تفصیلی", FrmLedger.TabCoa, null));
                    return;
                case "coa":
                    Show(owner, new FrmLedger("سایر حساب‌ها", FrmLedger.TabCoa, null));
                    return;
                case "trial":
                    Show(owner, new FrmLedger("تراز آزمایشی", FrmLedger.TabReports, FrmLedger.ReportTrial));
                    return;
                case "pnl":
                case "ledger-pnl":
                    Show(owner, new FrmLedger("سود و زیان", FrmLedger.TabReports, FrmLedger.ReportPnl));
                    return;
                case "balance":
                case "ledger-balance":
                    Show(owner, new FrmLedger("ترازنامه", FrmLedger.TabReports, FrmLedger.ReportBalance));
                    return;
                case "gl-move":
                    Show(owner, new FrmLedger("گردش حساب", FrmLedger.TabReports, FrmLedger.ReportGl));
                    return;
                case "period":
                case "close-year":
                case "year-end":
                    Show(owner, new FrmLedger("عملیات پایان دوره", FrmLedger.TabCalendar, null));
                    return;
                case "cash":
                case "transfer":
                case "revise":
                case "void":
                    Show(owner, new FrmAccounting("تراکنش‌های مالی", FrmAccounting.TabTxn, null));
                    return;
                case "receive":
                    Show(owner, new FrmAccounting("دریافت وجه", FrmAccounting.TabTxn, FrmAccounting.DirectionReceive));
                    return;
                case "pay":
                    Show(owner, new FrmAccounting("پرداخت وجه", FrmAccounting.TabTxn, FrmAccounting.DirectionPay));
                    return;
                case "banks":
                    Show(owner, new FrmAccounting("حساب‌های بانکی", FrmAccounting.TabFunds, null));
                    return;
                case "funds":
                    Show(owner, new FrmAccounting("صندوق‌ها", FrmAccounting.TabFunds, null));
                    return;
                case "parties":
                case "cash-parties":
                    Show(owner, new FrmAccounting("طرف حساب", FrmAccounting.TabParties, null));
                    return;
                case "income":
                    Show(owner, new FrmAccounting("درآمدها", FrmAccounting.TabIncome, null));
                    return;
                case "expense":
                    Show(owner, new FrmAccounting("هزینه‌ها", FrmAccounting.TabExpense, null));
                    return;
                case "cash-report":
                case "bank-report":
                case "cashflow":
                case "cash-reports":
                    Show(owner, new FrmAccounting("گزارشات مالی", FrmAccounting.TabReports, null));
                    return;
                case "open-period":
                case "close-month":
                case "carry":
                    Show(owner, new FrmAccounting("دوره مالی", FrmAccounting.TabPeriod, null));
                    return;
                case "invoice":
                case "sale":
                case "sale-return":
                    Show(owner, new FrmSales());
                    return;
                case "sales-top":
                    Show(owner, new FrmSales("فروش مشتری"));
                    return;
                case "purchase":
                case "purchase-return":
                    Show(owner, new FrmPurchase());
                    return;
                case "purchase-top":
                    Show(owner, new FrmPurchase("خرید تامین‌کننده"));
                    return;
                case "inventory":
                    Show(owner, new FrmInventory());
                    return;
                case "goods":
                    Show(owner, new FrmInventory("کالاها", FrmInventory.TabItems, null));
                    return;
                case "stock":
                    Show(owner, new FrmInventory("موجودی کالا", FrmInventory.TabReports, FrmInventory.ReportStockOnHand));
                    return;
                case "kardex":
                    Show(owner, new FrmInventory("کاردکس کالا", FrmInventory.TabReports, FrmInventory.ReportKardex));
                    return;
                case "crm":
                case "customers":
                case "vendors":
                    Show(owner, new FrmCrm());
                    return;
                case "crm-opps":
                    Show(owner, new FrmCrm("پایپلاین فرصت"));
                    return;
                case "assets":
                    Show(owner, new FrmAssets());
                    return;
                case "payroll":
                    Show(owner, new FrmPayroll());
                    return;
                case "pos":
                    Show(owner, new FrmPos());
                    return;
                case "ai":
                    Show(owner, new FrmAiPlatform());
                    return;
                case "ai-health":
                    Show(owner, new FrmAiPlatform("داشبورد هوشمند"));
                    return;
                case "ai-exec":
                    Show(owner, new FrmAiPlatform("داشبورد مدیریتی"));
                    return;
                case "ai-chat":
                    Show(owner, new FrmAiPlatform("گفتگو با هوش مصنوعی"));
                    return;
                case "ai-sales":
                    Show(owner, new FrmAiPlatform("تحلیل فروش"));
                    return;
                case "ai-purchase":
                    Show(owner, new FrmAiPlatform("تحلیل خرید"));
                    return;
                case "ai-finance":
                    Show(owner, new FrmAiPlatform("تحلیل مالی"));
                    return;
                case "ai-stock":
                    Show(owner, new FrmAiPlatform("تحلیل موجودی"));
                    return;
                case "ai-customers":
                    Show(owner, new FrmAiPlatform("تحلیل مشتریان"));
                    return;
                case "ai-forecast":
                    Show(owner, new FrmAiPlatform("پیش‌بینی"));
                    return;
                case "ai-alerts":
                    Show(owner, new FrmAiPlatform("هشدارهای هوشمند"));
                    return;
                case "ai-settings":
                    Show(owner, new FrmAiPlatform("تنظیمات هوش مصنوعی"));
                    return;
                case "users":
                    Show(owner, new FrmUsers());
                    return;
                case "roles":
                    Show(owner, new FrmPermissionMatrix());
                    return;
                case "audit":
                    Show(owner, new FrmSecurityAudit());
                    return;
                case "errors":
                    Show(owner, new FrmErrorLog());
                    return;
                case "modules":
                    Show(owner, new FrmModules());
                    return;
                case "report-builder":
                    Show(owner, new FrmReportBuilder());
                    return;
                case "reports":
                case "reporting":
                    Show(owner, new FrmReportingCenter());
                    return;
                case "backup":
                case "company":
                case "system":
                case "theme":
                case "settings":
                case "profile":
                    {
                        FrmDashboard dash = FindDashboard(owner);
                        if (dash != null)
                            dash.OpenSettingsFromNav();
                        else
                            Show(owner, new FrmSettings());
                        return;
                    }
                case "shortcuts":
                    Show(owner, new FrmErpQuickActions());
                    return;
                case "soon":
                case "tools":
                case "cheques":
                case "budget":
                    Show(owner, new FrmErpPlaceholder("این بخش"));
                    return;
                default:
                    Show(owner, new FrmErpPlaceholder("این بخش"));
                    return;
            }
        }

        private static FrmDashboard FindDashboard(IWin32Window owner)
        {
            Form f = owner as Form;
            while (f != null)
            {
                FrmDashboard d = f as FrmDashboard;
                if (d != null) return d;
                f = f.Owner;
            }
            return null;
        }

        private static void Show(IWin32Window owner, Form frm)
        {
            using (frm)
                frm.ShowDialog(owner);
        }
    }
}
