using System;
using System.Collections.Generic;
using System.Data;
using CaseManagement.Accounting;
using CaseManagement.Accounting.Ledger.Adapters;
using CaseManagement.Accounting.Ledger.Application;
using CaseManagement.Accounting.Ledger.Domain;
using CaseManagement.Inventory.Application;
using CaseManagement.Inventory.Domain;
using CaseManagement.Purchase.Application;
using CaseManagement.Purchase.Domain;
using CaseManagement.Sales.Application;
using CaseManagement.Sales.Domain;
using CaseManagement.Trade;

namespace CaseManagement.Helpers
{
    // اعداد داشبورد از سرویس‌های موجود. اگر جدول/مجوز نباشد «—». محاسبهٔ جعلی ندارد.
    public sealed class ErpDashboardSnapshot
    {
        public string SalesToday = ProductBranding.MetricUnavailable;
        public string PurchasesToday = ProductBranding.MetricUnavailable;
        public string Stock = ProductBranding.MetricUnavailable;
        public string Receivables = ProductBranding.MetricUnavailable;
        public string Payables = ProductBranding.MetricUnavailable;
        public string Cash = ProductBranding.MetricUnavailable;
        public string Bank = ProductBranding.MetricUnavailable;
        public string ProfitLoss = ProductBranding.MetricUnavailable;
        public string OpenOrders = ProductBranding.MetricUnavailable;
        public string OpenInvoices = ProductBranding.MetricUnavailable;
    }

    public static class ErpDashboardMetrics
    {
        public static ErpDashboardSnapshot Load()
        {
            var snap = new ErpDashboardSnapshot();
            ILedgerIdentity identity = null;
            try { identity = DesktopLedgerIdentity.FromSession(); }
            catch { }

            Try(delegate
            {
                var svc = new SalesService();
                IList<SalInvoice> hist = svc.SalesHistory(identity);
                long today = 0;
                int openInv = 0;
                for (int i = 0; i < hist.Count; i++)
                {
                    if (IsToday(hist[i].InvoiceDate))
                        today += hist[i].AmountMinor;
                    string st = hist[i].Status ?? "";
                    if (st == TradeCodes.Draft || st == TradeCodes.Submitted || st == TradeCodes.Approved)
                        openInv++;
                }
                snap.SalesToday = FormatMinor(today);
                snap.OpenInvoices = openInv.ToString("N0");
            });

            Try(delegate
            {
                var svc = new PurchaseService();
                IList<PurInvoice> hist = svc.PurchaseHistory(identity);
                long today = 0;
                for (int i = 0; i < hist.Count; i++)
                {
                    if (IsToday(hist[i].InvoiceDate))
                        today += hist[i].AmountMinor;
                }
                snap.PurchasesToday = FormatMinor(today);
                IList<OpenPurchaseOrderRow> po = svc.OpenOrders(identity);
                IList<OpenSalesOrderRow> so = new SalesService().OpenOrders(identity);
                snap.OpenOrders = (Count(po) + Count(so)).ToString("N0");
            });

            Try(delegate
            {
                var q = new InventoryQueryService();
                int n = Count(q.StockOnHand(identity));
                snap.Stock = n.ToString("N0") + " قلم";
            });

            Try(delegate
            {
                var reports = new LedgerReportingService();
                var query = new LedgerReportQuery();
                IList<TrialBalanceRow> tb = reports.GetTrialBalance(query, identity);
                snap.Receivables = FormatMinor(Closing(tb, "1200"));
                snap.Payables = FormatMinor(Closing(tb, "2100"));
                long cashGl = Closing(tb, "1101");
                long bankGl = Closing(tb, "1102");
                ProfitAndLossResult pl = reports.GetProfitAndLoss(query, identity);
                snap.ProfitLoss = FormatMinor(pl == null ? 0 : pl.NetIncome);
                if (tb != null && tb.Count > 0)
                {
                    if (cashGl != 0) snap.Cash = FormatMinor(cashGl);
                    if (bankGl != 0) snap.Bank = FormatMinor(bankGl);
                }
            });

            Try(delegate
            {
                var repo = new AccountingRepo();
                var funds = repo.GetFunds();
                double cash = 0, bank = 0;
                bool anyCash = false, anyBank = false;
                foreach (System.Data.DataRow r in funds.Rows)
                {
                    int id = Convert.ToInt32(r["FundID"]);
                    string type = Convert.ToString(r["نوع"]) ?? "";
                    double bal = repo.GetFundBalance(id);
                    if (type.IndexOf("بانک", StringComparison.Ordinal) >= 0)
                    {
                        bank += bal;
                        anyBank = true;
                    }
                    else
                    {
                        cash += bal;
                        anyCash = true;
                    }
                }
                if (anyCash) snap.Cash = cash.ToString("N0");
                if (anyBank) snap.Bank = bank.ToString("N0");
            });

            return snap;
        }

        public static ErpDashboardPayload LoadPayload()
        {
            ErpDashboardSnapshot snap = Load();
            var payload = new ErpDashboardPayload();
            payload.userName = SecurityContext.Username ?? "";
            payload.role = UiTheme.RoleDisplay(SecurityContext.Role);
            payload.center = SecurityContext.CenterDisplay ?? "";
            payload.product = ProductBranding.CommercialName;
            payload.updatedAt = PersianDateHelper.ToPersianDateString(DateTime.Now);
            payload.today = payload.updatedAt;
            payload.lastLogin = LastLoginDisplay();
            payload.systemStatus = "آماده";
            payload.insight = "خلاصه وضعیت مالی " + payload.center;
            payload.footer = "نسخه ۱.۰.۰";
            payload.kpis = new List<ErpKpiDto>();
            payload.alerts = new List<ErpAlertDto>();
            payload.documents = new List<ErpDocDto>();
            payload.activities = new List<ErpActivityDto>();
            payload.quickActions = ErpQuickActions.Load();
            payload.expenses = new List<ErpSliceDto>();
            payload.cashFlow = new List<ErpCashPointDto>();
            payload.revenueExpense = new ErpLineSeriesDto();
            payload.revenueExpense.income = new List<ErpPointDto>();
            payload.revenueExpense.expense = new List<ErpPointDto>();

            ILedgerIdentity identity = null;
            try { identity = DesktopLedgerIdentity.FromSession(); } catch { }

            long monthRevenue = 0, lastRevenue = 0, monthExpense = 0, lastExpense = 0;
            Try(delegate
            {
                ProfitAndLossResult now = MonthPl(identity, 0);
                ProfitAndLossResult prev = MonthPl(identity, -1);
                if (now != null)
                {
                    monthRevenue = now.RevenueTotal;
                    monthExpense = now.ExpenseTotal;
                }
                if (prev != null)
                {
                    lastRevenue = prev.RevenueTotal;
                    lastExpense = prev.ExpenseTotal;
                }
            });

            payload.kpis.Add(Kpi("درآمد امروز", snap.SalesToday, "افغانی", "sale", null, null));
            payload.kpis.Add(Kpi("درآمد ماه", FormatMinor(monthRevenue), "افغانی", "income", monthRevenue, lastRevenue));
            payload.kpis.Add(Kpi("هزینه ماه", FormatMinor(monthExpense), "افغانی", "expense", monthExpense, lastExpense));
            payload.kpis.Add(Kpi("موجودی بانک", snap.Bank, "افغانی", "bank", null, null));
            payload.kpis.Add(Kpi("موجودی صندوق", snap.Cash, "افغانی", "cash", null, null));
            payload.kpis.Add(Kpi("سود خالص", snap.ProfitLoss, "افغانی", "profit", monthRevenue - monthExpense, lastRevenue - lastExpense));

            Try(delegate { FillSeries(payload, identity); });
            Try(delegate { FillExpenses(payload, identity); });
            Try(delegate { FillCashFlow(payload); });
            Try(delegate { FillDocuments(payload); });
            Try(delegate { FillActivities(payload); });
            Try(delegate { FillAlerts(payload, snap, identity); });

            long expTotal = 0;
            for (int i = 0; i < payload.expenses.Count; i++)
                expTotal += payload.expenses[i].value;
            payload.expenseTotal = FormatMinor(expTotal);
            payload.theme = ErpUiPrefs.Theme;
            payload.resolvedTheme = ErpUiPrefs.ResolvedTheme();
            payload.density = ErpUiPrefs.Density;
            payload.fontScale = ErpUiPrefs.FontScale;
            payload.summary = BuildSummary(monthRevenue, lastRevenue, snap, payload);
            return payload;
        }

        private static System.Collections.Generic.List<string> BuildSummary(
            long monthRevenue, long lastRevenue, ErpDashboardSnapshot snap, ErpDashboardPayload payload)
        {
            var lines = new System.Collections.Generic.List<string>();
            if (lastRevenue != 0)
            {
                double pct = (monthRevenue - lastRevenue) * 100.0 / Math.Abs(lastRevenue);
                string verb = pct >= 0 ? "افزایش" : "کاهش";
                lines.Add("درآمد این ماه نسبت به ماه گذشته " + Math.Abs(pct).ToString("N0") + "٪ " + verb + " یافته است.");
            }
            else
            {
                lines.Add("مقایسه درآمد ماه جاری با ماه قبل در حال حاضر در دسترس نیست.");
            }

            bool lowStock = false;
            bool critical = false;
            if (payload.alerts != null)
            {
                for (int i = 0; i < payload.alerts.Count; i++)
                {
                    if (payload.alerts[i] == null) continue;
                    if (payload.alerts[i].tone == "danger") critical = true;
                    if ((payload.alerts[i].title ?? "").IndexOf("موجودی", StringComparison.Ordinal) >= 0)
                        lowStock = true;
                }
            }
            if (lowStock)
                lines.Add("موجودی برخی کالاها کمتر از حد مجاز است.");
            else if (snap.Stock == ProductBranding.MetricUnavailable)
                lines.Add("وضعیت موجودی کالا در حال حاضر قابل محاسبه نیست.");
            else
                lines.Add("موجودی کالا در وضعیت مطلوب قرار دارد.");

            if (critical)
                lines.Add("هشدار بحرانی فعال است و نیاز به پیگیری دارد.");
            else
                lines.Add("هیچ هشدار بحرانی ثبت نشده است.");
            return lines;
        }

        private static ProfitAndLossResult MonthPl(ILedgerIdentity identity, int monthOffset)
        {
            DateTime start = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(monthOffset);
            DateTime end = start.AddMonths(1).AddDays(-1);
            var reports = new LedgerReportingService();
            var query = new LedgerReportQuery
            {
                CompanyId = LedgerCodes.DefaultCompanyId,
                FromDate = start.ToString("yyyy-MM-dd"),
                ToDate = end.ToString("yyyy-MM-dd")
            };
            return reports.GetProfitAndLoss(query, identity);
        }

        private static ErpKpiDto Kpi(string label, string value, string unit, string icon, long? now, long? prev)
        {
            var dto = new ErpKpiDto();
            dto.label = label;
            dto.value = string.IsNullOrWhiteSpace(value) ? ProductBranding.MetricUnavailable : value;
            dto.unit = unit;
            dto.icon = icon;
            dto.hint = "نسبت به ماه قبل";
            if (!now.HasValue || !prev.HasValue || prev.Value == 0)
            {
                dto.change = ProductBranding.MetricUnavailable;
                return dto;
            }
            double pct = (now.Value - prev.Value) * 100.0 / Math.Abs(prev.Value);
            dto.trend = pct >= 0 ? "up" : "down";
            dto.change = (pct >= 0 ? "↑ " : "↓ ") + Math.Abs(pct).ToString("N0") + "٪";
            return dto;
        }

        private static void FillSeries(ErpDashboardPayload payload, ILedgerIdentity identity)
        {
            var sales = new SalesService().SalesHistory(identity);
            var purchases = new PurchaseService().PurchaseHistory(identity);
            for (int m = 11; m >= 0; m--)
            {
                DateTime start = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-m);
                DateTime end = start.AddMonths(1);
                string label = PersianDateHelper.ToPersianDateString(start);
                if (label.Length >= 7) label = label.Substring(0, 7);
                payload.revenueExpense.income.Add(Point(label, SumInvoices(sales, start, end)));
                payload.revenueExpense.expense.Add(Point(label, SumPurchases(purchases, start, end)));
            }
        }

        private static long SumInvoices(IList<SalInvoice> hist, DateTime start, DateTime end)
        {
            long sum = 0;
            if (hist == null) return 0;
            for (int i = 0; i < hist.Count; i++)
            {
                if (hist[i].Status != TradeCodes.Posted) continue;
                DateTime d;
                if (!TryDate(hist[i].InvoiceDate, out d)) continue;
                if (d >= start && d < end) sum += hist[i].AmountMinor;
            }
            return sum;
        }

        private static long SumPurchases(IList<PurInvoice> hist, DateTime start, DateTime end)
        {
            long sum = 0;
            if (hist == null) return 0;
            for (int i = 0; i < hist.Count; i++)
            {
                if (hist[i].Status != TradeCodes.Posted) continue;
                DateTime d;
                if (!TryDate(hist[i].InvoiceDate, out d)) continue;
                if (d >= start && d < end) sum += hist[i].AmountMinor;
            }
            return sum;
        }

        private static void FillExpenses(ErpDashboardPayload payload, ILedgerIdentity identity)
        {
            var reports = new LedgerReportingService();
            var query = new LedgerReportQuery { CompanyId = LedgerCodes.DefaultCompanyId };
            IList<TrialBalanceRow> tb = reports.GetTrialBalance(query, identity);
            if (tb == null) return;
            var top = new List<TrialBalanceRow>();
            for (int i = 0; i < tb.Count; i++)
            {
                if (!string.Equals(tb[i].AccountTypeCode, LedgerCodes.TypeExpense, StringComparison.OrdinalIgnoreCase))
                    continue;
                long amt = Math.Abs(tb[i].ClosingDebit - tb[i].ClosingCredit);
                if (amt <= 0) continue;
                top.Add(tb[i]);
            }
            top.Sort(delegate (TrialBalanceRow a, TrialBalanceRow b)
            {
                long aa = Math.Abs(a.ClosingDebit - a.ClosingCredit);
                long bb = Math.Abs(b.ClosingDebit - b.ClosingCredit);
                return bb.CompareTo(aa);
            });
            int take = Math.Min(5, top.Count);
            for (int i = 0; i < take; i++)
            {
                long amt = Math.Abs(top[i].ClosingDebit - top[i].ClosingCredit);
                var slice = new ErpSliceDto();
                slice.label = string.IsNullOrWhiteSpace(top[i].AccountName) ? top[i].AccountCode : top[i].AccountName;
                slice.value = amt;
                slice.display = FormatMinor(amt);
                payload.expenses.Add(slice);
            }
        }

        private static void FillCashFlow(ErpDashboardPayload payload)
        {
            DataTable tx = new AccountingRepo().GetTransactions(null, null);
            var map = new Dictionary<string, ErpCashPointDto>();
            for (int m = 11; m >= 0; m--)
            {
                DateTime start = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-m);
                string label = PersianDateHelper.ToPersianDateString(start);
                if (label.Length >= 7) label = label.Substring(0, 7);
                var pt = new ErpCashPointDto();
                pt.label = label;
                pt.key = start.ToString("yyyy-MM");
                map[pt.key] = pt;
                payload.cashFlow.Add(pt);
            }
            if (tx == null) return;
            foreach (DataRow row in tx.Rows)
            {
                DateTime d;
                if (!TryDate(Convert.ToString(row["تاریخ"]), out d)) continue;
                string key = d.ToString("yyyy-MM");
                ErpCashPointDto pt;
                if (!map.TryGetValue(key, out pt)) continue;
                double amt = row["مبلغ"] == DBNull.Value ? 0 : Convert.ToDouble(row["مبلغ"]);
                string dir = Convert.ToString(row["نوع"]) ?? "";
                if (dir.IndexOf("دریافت", StringComparison.Ordinal) >= 0) pt.incoming += amt;
                else pt.outgoing += amt;
            }
        }

        private static void FillDocuments(ErpDashboardPayload payload)
        {
            DataTable tx = new AccountingRepo().GetTransactions(null, null);
            if (tx == null) return;
            int n = 0;
            foreach (DataRow row in tx.Rows)
            {
                if (n >= 8) break;
                var doc = new ErpDocDto();
                doc.number = Convert.ToString(row["شماره سند"]);
                doc.date = Convert.ToString(row["تاریخ"]);
                string party = Convert.ToString(row["طرف حساب"]);
                string cat = ProductBranding.CategoryDisplayName(Convert.ToString(row["دسته‌بندی"]));
                doc.title = string.IsNullOrWhiteSpace(party) ? cat : party + " — " + cat;
                object amt = row["مبلغ"];
                doc.amount = amt == DBNull.Value ? ProductBranding.MetricUnavailable : Convert.ToDouble(amt).ToString("N0");
                doc.status = "تأیید شده";
                payload.documents.Add(doc);
                n++;
            }
        }

        private static void FillActivities(ErpDashboardPayload payload)
        {
            if (payload.documents == null) return;
            int n = Math.Min(6, payload.documents.Count);
            for (int i = 0; i < n; i++)
            {
                ErpDocDto d = payload.documents[i];
                payload.activities.Add(new ErpActivityDto
                {
                    title = string.IsNullOrWhiteSpace(d.title) ? "سند مالی" : d.title,
                    detail = string.IsNullOrWhiteSpace(d.number) ? d.status : "سند " + d.number,
                    time = d.date
                });
            }
        }

        private static string LastLoginDisplay()
        {
            string raw = SettingsHelper.Get("ErpLastLoginPrev", "");
            if (string.IsNullOrWhiteSpace(raw)) return "اولین ورود";
            DateTime d;
            if (DateTime.TryParse(raw, out d))
                return PersianDateHelper.ToPersianDateString(d) + "  " + d.ToString("HH:mm");
            return raw;
        }

        private static void FillAlerts(ErpDashboardPayload payload, ErpDashboardSnapshot snap, ILedgerIdentity identity)
        {
            if (snap.OpenInvoices != ProductBranding.MetricUnavailable && snap.OpenInvoices != "0")
            {
                payload.alerts.Add(Alert("warn", "فاکتورهای باز", snap.OpenInvoices + " فاکتور در انتظار تکمیل گردش کار است."));
            }
            if (snap.OpenOrders != ProductBranding.MetricUnavailable && snap.OpenOrders != "0")
            {
                payload.alerts.Add(Alert("info", "سفارشات باز", snap.OpenOrders + " سفارش هنوز ثبت قطعی نشده است."));
            }
            Try(delegate
            {
                IList<ReorderRow> low = new InventoryQueryService().Reorder(identity);
                int c = Count(low);
                if (c > 0)
                    payload.alerts.Add(Alert("danger", "موجودی کم کالا", c.ToString("N0") + " کالا کمتر از حد مجاز است."));
            });
        }

        private static ErpAlertDto Alert(string tone, string title, string detail)
        {
            return new ErpAlertDto { tone = tone, title = title, detail = detail };
        }

        private static ErpPointDto Point(string label, long minor)
        {
            return new ErpPointDto { label = label, value = minor / 100.0 };
        }

        private static bool TryDate(string raw, out DateTime date)
        {
            date = DateTime.MinValue;
            if (string.IsNullOrWhiteSpace(raw)) return false;
            if (DateTime.TryParse(raw, out date)) return true;
            if (raw.Length >= 10 && DateTime.TryParse(raw.Substring(0, 10), out date)) return true;
            return false;
        }

        private static void Try(Action body)
        {
            try { body(); }
            catch { }
        }

        private static int Count<T>(IList<T> list)
        {
            return list == null ? 0 : list.Count;
        }

        private static long Closing(IList<TrialBalanceRow> rows, string code)
        {
            if (rows == null) return 0;
            for (int i = 0; i < rows.Count; i++)
            {
                if (string.Equals(rows[i].AccountCode, code, StringComparison.OrdinalIgnoreCase))
                    return rows[i].ClosingDebit - rows[i].ClosingCredit;
            }
            return 0;
        }

        private static string FormatMinor(long minor)
        {
            return (minor / 100.0).ToString("N0");
        }

        private static bool IsToday(string date)
        {
            if (string.IsNullOrWhiteSpace(date)) return false;
            string iso = DateTime.Today.ToString("yyyy-MM-dd");
            if (date.StartsWith(iso, StringComparison.Ordinal)) return true;
            try
            {
                return string.Equals(
                    PersianDateHelper.ToPersianDateString(DateTime.Today),
                    date.Trim(),
                    StringComparison.Ordinal);
            }
            catch { return false; }
        }
    }

    public sealed class ErpDashboardPayload
    {
        public string userName { get; set; }
        public string role { get; set; }
        public string center { get; set; }
        public string product { get; set; }
        public string updatedAt { get; set; }
        public string today { get; set; }
        public string lastLogin { get; set; }
        public string systemStatus { get; set; }
        public string insight { get; set; }
        public string footer { get; set; }
        public string expenseTotal { get; set; }
        public string theme { get; set; }
        public string resolvedTheme { get; set; }
        public string density { get; set; }
        public int fontScale { get; set; }
        public List<string> summary { get; set; }
        public List<ErpKpiDto> kpis { get; set; }
        public ErpLineSeriesDto revenueExpense { get; set; }
        public List<ErpCashPointDto> cashFlow { get; set; }
        public List<ErpSliceDto> expenses { get; set; }
        public List<ErpDocDto> documents { get; set; }
        public List<ErpAlertDto> alerts { get; set; }
        public List<ErpActivityDto> activities { get; set; }
        public List<ErpQuickActionDto> quickActions { get; set; }
    }

    public sealed class ErpKpiDto
    {
        public string label { get; set; }
        public string value { get; set; }
        public string unit { get; set; }
        public string icon { get; set; }
        public string change { get; set; }
        public string hint { get; set; }
        public string trend { get; set; }
    }

    public sealed class ErpLineSeriesDto
    {
        public List<ErpPointDto> income { get; set; }
        public List<ErpPointDto> expense { get; set; }
    }

    public sealed class ErpPointDto
    {
        public string label { get; set; }
        public double value { get; set; }
    }

    public sealed class ErpCashPointDto
    {
        public string label { get; set; }
        public string key { get; set; }
        public double incoming { get; set; }
        public double outgoing { get; set; }
    }

    public sealed class ErpSliceDto
    {
        public string label { get; set; }
        public long value { get; set; }
        public string display { get; set; }
    }

    public sealed class ErpDocDto
    {
        public string number { get; set; }
        public string date { get; set; }
        public string title { get; set; }
        public string amount { get; set; }
        public string status { get; set; }
    }

    public sealed class ErpActivityDto
    {
        public string title { get; set; }
        public string detail { get; set; }
        public string time { get; set; }
    }

    public sealed class ErpAlertDto
    {
        public string tone { get; set; }
        public string title { get; set; }
        public string detail { get; set; }
    }
}
