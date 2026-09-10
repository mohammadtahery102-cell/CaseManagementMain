using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Globalization;
using System.Text;
using CaseManagement.Accounting.Ledger.Application;
using CaseManagement.Accounting.Ledger.Domain;
using CaseManagement.AiPlatform.Domain;
using CaseManagement.AiPlatform.Infrastructure;
using CaseManagement.Crm.Domain;
using CaseManagement.Trade;

namespace CaseManagement.AiPlatform.Application
{
    internal static class AiRead
    {
        public static SQLiteParameter P(string n, object v) { return new SQLiteParameter(n, v ?? DBNull.Value); }

        public static long Scalar(AiPlatformStore store, string sql, params SQLiteParameter[] p)
        {
            return store.Count(sql, p);
        }

        public static string N(long v)
        {
            return v.ToString("N0", CultureInfo.InvariantCulture);
        }

        public static AiMetricRow Row(string label, string value)
        {
            return new AiMetricRow { Label = label, Value = value };
        }

        public static string MonthKey(DateTime utc)
        {
            return utc.ToUniversalTime().ToString("yyyy-MM", CultureInfo.InvariantCulture);
        }

        public static string IsoDate(DateTime utc)
        {
            return utc.ToUniversalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        public static void AppendTop(IList<AiMetricRow> rows, string title, DataTable table, string nameCol, string valueCol)
        {
            if (table == null || table.Rows.Count == 0)
            {
                rows.Add(Row(title, "—"));
                return;
            }
            for (int i = 0; i < table.Rows.Count; i++)
            {
                string name = Convert.ToString(table.Rows[i][nameCol]);
                if (string.IsNullOrWhiteSpace(name)) name = "(بدون نام)";
                rows.Add(Row(title + " #" + (i + 1) + " " + name, N(Convert.ToInt64(table.Rows[i][valueCol]))));
            }
        }

        public static string FormatReport(AiAnalysisReport report)
        {
            if (report == null) return "";
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(report.Title ?? "");
            if (report.Rows == null) return sb.ToString();
            for (int i = 0; i < report.Rows.Count; i++)
                sb.AppendLine((report.Rows[i].Label ?? "") + ": " + (report.Rows[i].Value ?? ""));
            return sb.ToString();
        }
    }

    public static class AiFacts
    {
        public static long PostedSales(AiPlatformStore store, int companyId)
        {
            return AiRead.Scalar(store,
                "SELECT IFNULL(SUM(AmountMinor),0) FROM SalInvoice WHERE CompanyID=@c AND IsDeleted=0 AND Status=@s;",
                AiRead.P("@c", companyId), AiRead.P("@s", TradeCodes.Posted));
        }

        public static long PostedPurchases(AiPlatformStore store, int companyId)
        {
            return AiRead.Scalar(store,
                "SELECT IFNULL(SUM(AmountMinor),0) FROM PurInvoice WHERE CompanyID=@c AND IsDeleted=0 AND Status=@s;",
                AiRead.P("@c", companyId), AiRead.P("@s", TradeCodes.Posted));
        }

        public static long InventoryValue(AiPlatformStore store, int companyId)
        {
            return AiRead.Scalar(store,
                "SELECT IFNULL(SUM(InventoryValueMinor),0) FROM InvItemBalance WHERE CompanyID=@c;",
                AiRead.P("@c", companyId));
        }

        public static long Cash(AiPlatformStore store, int companyId)
        {
            return AiRead.Scalar(store, @"
SELECT IFNULL(SUM(l.DebitBaseMinor - l.CreditBaseMinor),0)
FROM GlJournalLine l
JOIN GlJournal j ON j.JournalID = l.JournalID
JOIN GlAccount a ON a.AccountID = l.AccountID
WHERE l.CompanyID=@c AND j.Status=@s AND j.IsDeleted=0 AND a.IsDeleted=0 AND a.AccountCode LIKE '11%';",
                AiRead.P("@c", companyId), AiRead.P("@s", LedgerCodes.JournalPosted));
        }

        public static long GlTurnover(AiPlatformStore store, int companyId, string likeCode, bool revenue)
        {
            string expr = revenue
                ? "IFNULL(SUM(l.CreditBaseMinor - l.DebitBaseMinor),0)"
                : "IFNULL(SUM(l.DebitBaseMinor - l.CreditBaseMinor),0)";
            return AiRead.Scalar(store, @"
SELECT " + expr + @"
FROM GlJournalLine l
JOIN GlJournal j ON j.JournalID = l.JournalID
JOIN GlAccount a ON a.AccountID = l.AccountID
WHERE l.CompanyID=@c AND j.Status=@s AND j.IsDeleted=0 AND a.IsDeleted=0 AND a.AccountCode LIKE @like;",
                AiRead.P("@c", companyId), AiRead.P("@s", LedgerCodes.JournalPosted), AiRead.P("@like", likeCode));
        }

        public static long MonthSales(AiPlatformStore store, int companyId, string yyyyMm)
        {
            return AiRead.Scalar(store,
                "SELECT IFNULL(SUM(AmountMinor),0) FROM SalInvoice WHERE CompanyID=@c AND IsDeleted=0 AND Status=@s AND substr(InvoiceDate,1,7)=@m;",
                AiRead.P("@c", companyId), AiRead.P("@s", TradeCodes.Posted), AiRead.P("@m", yyyyMm));
        }

        public static long MonthPurchases(AiPlatformStore store, int companyId, string yyyyMm)
        {
            return AiRead.Scalar(store,
                "SELECT IFNULL(SUM(AmountMinor),0) FROM PurInvoice WHERE CompanyID=@c AND IsDeleted=0 AND Status=@s AND substr(InvoiceDate,1,7)=@m;",
                AiRead.P("@c", companyId), AiRead.P("@s", TradeCodes.Posted), AiRead.P("@m", yyyyMm));
        }

        public static long AgedReceivables(AiPlatformStore store, int companyId, string beforeDate)
        {
            return AiRead.Scalar(store,
                "SELECT IFNULL(SUM(AmountMinor),0) FROM SalInvoice WHERE CompanyID=@c AND IsDeleted=0 AND Status=@s AND InvoiceDate < @d;",
                AiRead.P("@c", companyId), AiRead.P("@s", TradeCodes.Posted), AiRead.P("@d", beforeDate));
        }

        public static DataTable TopCustomers(AiPlatformStore store, int companyId, string monthOrNull, int take)
        {
            string sql = @"
SELECT IFNULL(c.Name,'(بدون مشتری)') AS Name, SUM(i.AmountMinor) AS Amt
FROM SalInvoice i
LEFT JOIN SalCustomer c ON c.CustomerID = i.CustomerID
WHERE i.CompanyID=@c AND i.IsDeleted=0 AND i.Status=@s";
            if (!string.IsNullOrEmpty(monthOrNull)) sql += " AND substr(i.InvoiceDate,1,7)=@m";
            sql += " GROUP BY i.CustomerID ORDER BY Amt DESC LIMIT " + take + ";";
            if (string.IsNullOrEmpty(monthOrNull))
                return store.Query(sql, AiRead.P("@c", companyId), AiRead.P("@s", TradeCodes.Posted));
            return store.Query(sql, AiRead.P("@c", companyId), AiRead.P("@s", TradeCodes.Posted), AiRead.P("@m", monthOrNull));
        }

        public static DataTable TopProducts(AiPlatformStore store, int companyId, int take)
        {
            return store.Query(@"
SELECT IFNULL(it.Name,'(کالا)') AS Name, IFNULL(SUM(l.AmountMinor),0) AS Amt
FROM SalInvoiceLine l
JOIN SalInvoice i ON i.InvoiceID = l.InvoiceID
LEFT JOIN InvItem it ON it.ItemID = l.ItemID
WHERE i.CompanyID=@c AND i.IsDeleted=0 AND i.Status=@s
GROUP BY l.ItemID ORDER BY Amt DESC LIMIT " + take + ";",
                AiRead.P("@c", companyId), AiRead.P("@s", TradeCodes.Posted));
        }

        public static DataTable TopVendors(AiPlatformStore store, int companyId, int take)
        {
            return store.Query(@"
SELECT IFNULL(v.Name,'(فروشنده)') AS Name, SUM(i.AmountMinor) AS Amt
FROM PurInvoice i
LEFT JOIN PurVendor v ON v.VendorID = i.VendorID
WHERE i.CompanyID=@c AND i.IsDeleted=0 AND i.Status=@s
GROUP BY i.VendorID ORDER BY Amt DESC LIMIT " + take + ";",
                AiRead.P("@c", companyId), AiRead.P("@s", TradeCodes.Posted));
        }

        public static DataTable MonthlySales(AiPlatformStore store, int companyId, int take)
        {
            return store.Query(@"
SELECT substr(InvoiceDate,1,7) AS Ym, IFNULL(SUM(AmountMinor),0) AS Amt
FROM SalInvoice WHERE CompanyID=@c AND IsDeleted=0 AND Status=@s
GROUP BY Ym ORDER BY Ym DESC LIMIT " + take + ";",
                AiRead.P("@c", companyId), AiRead.P("@s", TradeCodes.Posted));
        }

        public static DataTable MonthlyPurchases(AiPlatformStore store, int companyId, int take)
        {
            return store.Query(@"
SELECT substr(InvoiceDate,1,7) AS Ym, IFNULL(SUM(AmountMinor),0) AS Amt
FROM PurInvoice WHERE CompanyID=@c AND IsDeleted=0 AND Status=@s
GROUP BY Ym ORDER BY Ym DESC LIMIT " + take + ";",
                AiRead.P("@c", companyId), AiRead.P("@s", TradeCodes.Posted));
        }

        public static long LowStockCount(AiPlatformStore store, int companyId)
        {
            return AiRead.Scalar(store, @"
SELECT COUNT(1) FROM (
  SELECT i.ItemID, IFNULL(SUM(b.QuantityOnHand),0) AS Qty, i.MinQtyBase AS MinQty
  FROM InvItem i
  LEFT JOIN InvItemBalance b ON b.ItemID=i.ItemID AND b.CompanyID=i.CompanyID
  WHERE i.CompanyID=@c AND i.IsDeleted=0
  GROUP BY i.ItemID
  HAVING Qty <= MinQty AND MinQty > 0
);", AiRead.P("@c", companyId));
        }

        public static long ZeroStockCount(AiPlatformStore store, int companyId)
        {
            return AiRead.Scalar(store, @"
SELECT COUNT(1) FROM InvItem i
WHERE i.CompanyID=@c AND i.IsDeleted=0
  AND NOT EXISTS (SELECT 1 FROM InvItemBalance b WHERE b.CompanyID=i.CompanyID AND b.ItemID=i.ItemID AND b.QuantityOnHand<>0);",
                AiRead.P("@c", companyId));
        }

        public static long OverstockCount(AiPlatformStore store, int companyId)
        {
            return AiRead.Scalar(store, @"
SELECT COUNT(1) FROM (
  SELECT i.ItemID, IFNULL(SUM(b.QuantityOnHand),0) AS Qty, i.MaxQtyBase AS MaxQty
  FROM InvItem i
  LEFT JOIN InvItemBalance b ON b.ItemID=i.ItemID AND b.CompanyID=i.CompanyID
  WHERE i.CompanyID=@c AND i.IsDeleted=0
  GROUP BY i.ItemID
  HAVING MaxQty > 0 AND Qty > MaxQty
);", AiRead.P("@c", companyId));
        }

        public static long SlowMovingCount(AiPlatformStore store, int companyId, string sinceDate)
        {
            return AiRead.Scalar(store, @"
SELECT COUNT(1) FROM InvItem i
WHERE i.CompanyID=@c AND i.IsDeleted=0
  AND IFNULL((SELECT SUM(QuantityOnHand) FROM InvItemBalance b WHERE b.CompanyID=i.CompanyID AND b.ItemID=i.ItemID),0) > 0
  AND NOT EXISTS (
    SELECT 1 FROM SalInvoiceLine l
    JOIN SalInvoice s ON s.InvoiceID=l.InvoiceID
    WHERE l.ItemID=i.ItemID AND s.CompanyID=@c AND s.IsDeleted=0 AND s.Status=@s AND s.InvoiceDate >= @since
  );",
                AiRead.P("@c", companyId), AiRead.P("@s", TradeCodes.Posted), AiRead.P("@since", sinceDate));
        }

        public static DataTable SlowItems(AiPlatformStore store, int companyId, string sinceDate, int take)
        {
            return store.Query(@"
SELECT i.Name AS Name, IFNULL((SELECT SUM(QuantityOnHand) FROM InvItemBalance b WHERE b.CompanyID=i.CompanyID AND b.ItemID=i.ItemID),0) AS Amt
FROM InvItem i
WHERE i.CompanyID=@c AND i.IsDeleted=0
  AND IFNULL((SELECT SUM(QuantityOnHand) FROM InvItemBalance b WHERE b.CompanyID=i.CompanyID AND b.ItemID=i.ItemID),0) > 0
  AND NOT EXISTS (
    SELECT 1 FROM SalInvoiceLine l
    JOIN SalInvoice s ON s.InvoiceID=l.InvoiceID
    WHERE l.ItemID=i.ItemID AND s.CompanyID=@c AND s.IsDeleted=0 AND s.Status=@s AND s.InvoiceDate >= @since
  )
ORDER BY Amt DESC LIMIT " + take + ";",
                AiRead.P("@c", companyId), AiRead.P("@s", TradeCodes.Posted), AiRead.P("@since", sinceDate));
        }

        public static DataTable FastItems(AiPlatformStore store, int companyId, string sinceDate, int take)
        {
            return store.Query(@"
SELECT IFNULL(it.Name,'(کالا)') AS Name, IFNULL(SUM(l.Qty),0) AS Amt
FROM SalInvoiceLine l
JOIN SalInvoice i ON i.InvoiceID=l.InvoiceID
LEFT JOIN InvItem it ON it.ItemID=l.ItemID
WHERE i.CompanyID=@c AND i.IsDeleted=0 AND i.Status=@s AND i.InvoiceDate >= @since
GROUP BY l.ItemID ORDER BY Amt DESC LIMIT " + take + ";",
                AiRead.P("@c", companyId), AiRead.P("@s", TradeCodes.Posted), AiRead.P("@since", sinceDate));
        }

        public static DataTable ReorderItems(AiPlatformStore store, int companyId, int take)
        {
            return store.Query(@"
SELECT i.Name AS Name, (i.MinQtyBase - IFNULL(SUM(b.QuantityOnHand),0)) AS Amt
FROM InvItem i
LEFT JOIN InvItemBalance b ON b.ItemID=i.ItemID AND b.CompanyID=i.CompanyID
WHERE i.CompanyID=@c AND i.IsDeleted=0
GROUP BY i.ItemID
HAVING IFNULL(SUM(b.QuantityOnHand),0) <= i.MinQtyBase AND i.MinQtyBase > 0
ORDER BY Amt DESC LIMIT " + take + ";",
                AiRead.P("@c", companyId));
        }

        public static long ExpenseGl(AiPlatformStore store, int companyId)
        {
            return GlTurnover(store, companyId, "5%", false);
        }

        public static long RevenueGl(AiPlatformStore store, int companyId)
        {
            return GlTurnover(store, companyId, "4%", true);
        }

        public static string PctChange(long current, long previous)
        {
            if (previous == 0) return current == 0 ? "0%" : "n/a";
            long pct = (current - previous) * 100 / previous;
            return pct.ToString(CultureInfo.InvariantCulture) + "%";
        }
    }

    public static class AiSmartAnalytics
    {
        public static AiAnalysisReport Sales(AiPlatformStore store, ILedgerIdentity identity)
        {
            int c = identity.CompanyId;
            DateTime asOf = identity.UtcNow;
            string month = AiRead.MonthKey(asOf);
            string prev = AiRead.MonthKey(asOf.AddMonths(-1));
            long total = AiFacts.PostedSales(store, c);
            long invoices = AiRead.Scalar(store, "SELECT COUNT(1) FROM SalInvoice WHERE CompanyID=@c AND IsDeleted=0;", AiRead.P("@c", c));
            long posted = AiRead.Scalar(store, "SELECT COUNT(1) FROM SalInvoice WHERE CompanyID=@c AND IsDeleted=0 AND Status=@s;", AiRead.P("@c", c), AiRead.P("@s", TradeCodes.Posted));
            long orders = AiRead.Scalar(store, "SELECT COUNT(1) FROM SalOrder WHERE CompanyID=@c AND IsDeleted=0;", AiRead.P("@c", c));
            long quotes = AiRead.Scalar(store, "SELECT COUNT(1) FROM SalQuotation WHERE CompanyID=@c AND IsDeleted=0;", AiRead.P("@c", c));
            long thisM = AiFacts.MonthSales(store, c, month);
            long prevM = AiFacts.MonthSales(store, c, prev);
            long avg = posted == 0 ? 0 : total / posted;
            long anomalies = AiRead.Scalar(store,
                "SELECT COUNT(1) FROM SalInvoice WHERE CompanyID=@c AND IsDeleted=0 AND Status=@s AND AmountMinor > @lim;",
                AiRead.P("@c", c), AiRead.P("@s", TradeCodes.Posted), AiRead.P("@lim", avg <= 0 ? long.MaxValue : avg * 2));

            List<AiMetricRow> rows = new List<AiMetricRow>
            {
                AiRead.Row("پیش‌فاکتور", AiRead.N(quotes)),
                AiRead.Row("سفارش فروش", AiRead.N(orders)),
                AiRead.Row("فاکتور فروش", AiRead.N(invoices)),
                AiRead.Row("فاکتور ثبت‌شده", AiRead.N(posted)),
                AiRead.Row("مبلغ فروش ثبت‌شده (جزئی)", AiRead.N(total)),
                AiRead.Row("فروش این ماه", AiRead.N(thisM)),
                AiRead.Row("فروش ماه قبل", AiRead.N(prevM)),
                AiRead.Row("رشد ماهانه", AiFacts.PctChange(thisM, prevM)),
                AiRead.Row("مقایسه درآمد ماه جاری/قبل", AiRead.N(thisM) + " / " + AiRead.N(prevM)),
                AiRead.Row("ناهنجاری فروش (۲ برابر میانگین)", AiRead.N(anomalies))
            };
            DataTable trend = AiFacts.MonthlySales(store, c, 6);
            for (int i = 0; i < trend.Rows.Count; i++)
                rows.Add(AiRead.Row("روند فروش " + Convert.ToString(trend.Rows[i]["Ym"]), AiRead.N(Convert.ToInt64(trend.Rows[i]["Amt"]))));
            AiRead.AppendTop(rows, "مشتری برتر", AiFacts.TopCustomers(store, c, null, 5), "Name", "Amt");
            AiRead.AppendTop(rows, "کالای پرفروش", AiFacts.TopProducts(store, c, 5), "Name", "Amt");
            return new AiAnalysisReport { Title = "تحلیل فروش", Rows = rows };
        }

        public static AiAnalysisReport Purchase(AiPlatformStore store, ILedgerIdentity identity)
        {
            int c = identity.CompanyId;
            DateTime asOf = identity.UtcNow;
            string month = AiRead.MonthKey(asOf);
            string prev = AiRead.MonthKey(asOf.AddMonths(-1));
            long total = AiFacts.PostedPurchases(store, c);
            long invoices = AiRead.Scalar(store, "SELECT COUNT(1) FROM PurInvoice WHERE CompanyID=@c AND IsDeleted=0;", AiRead.P("@c", c));
            long posted = AiRead.Scalar(store, "SELECT COUNT(1) FROM PurInvoice WHERE CompanyID=@c AND IsDeleted=0 AND Status=@s;", AiRead.P("@c", c), AiRead.P("@s", TradeCodes.Posted));
            long orders = AiRead.Scalar(store, "SELECT COUNT(1) FROM PurOrder WHERE CompanyID=@c AND IsDeleted=0;", AiRead.P("@c", c));
            long receipts = AiRead.Scalar(store, "SELECT COUNT(1) FROM PurGoodsReceipt WHERE CompanyID=@c AND IsDeleted=0;", AiRead.P("@c", c));
            long thisM = AiFacts.MonthPurchases(store, c, month);
            long prevM = AiFacts.MonthPurchases(store, c, prev);
            DataTable vendors = AiFacts.TopVendors(store, c, 1);
            long topAmt = vendors.Rows.Count == 0 ? 0 : Convert.ToInt64(vendors.Rows[0]["Amt"]);
            string dep = total == 0 ? "0%" : ((topAmt * 100) / total).ToString(CultureInfo.InvariantCulture) + "%";
            string cost = thisM > prevM && prevM > 0 ? "افزایش نسبت به ماه قبل" : "پایدار یا کاهش";

            List<AiMetricRow> rows = new List<AiMetricRow>
            {
                AiRead.Row("سفارش خرید", AiRead.N(orders)),
                AiRead.Row("رسید کالا", AiRead.N(receipts)),
                AiRead.Row("فاکتور خرید", AiRead.N(invoices)),
                AiRead.Row("فاکتور ثبت‌شده", AiRead.N(posted)),
                AiRead.Row("مبلغ خرید ثبت‌شده (جزئی)", AiRead.N(total)),
                AiRead.Row("خرید این ماه", AiRead.N(thisM)),
                AiRead.Row("خرید ماه قبل", AiRead.N(prevM)),
                AiRead.Row("روند خرید", AiFacts.PctChange(thisM, prevM)),
                AiRead.Row("تشخیص افزایش هزینه", cost),
                AiRead.Row("وابستگی به فروشنده اول", dep)
            };
            DataTable trend = AiFacts.MonthlyPurchases(store, c, 6);
            for (int i = 0; i < trend.Rows.Count; i++)
                rows.Add(AiRead.Row("روند خرید " + Convert.ToString(trend.Rows[i]["Ym"]), AiRead.N(Convert.ToInt64(trend.Rows[i]["Amt"]))));
            AiRead.AppendTop(rows, "فروشنده برتر", AiFacts.TopVendors(store, c, 5), "Name", "Amt");
            return new AiAnalysisReport { Title = "تحلیل خرید", Rows = rows };
        }

        public static AiAnalysisReport Inventory(AiPlatformStore store, ILedgerIdentity identity)
        {
            int c = identity.CompanyId;
            string since = AiRead.IsoDate(identity.UtcNow.AddDays(-90));
            long items = AiRead.Scalar(store, "SELECT COUNT(1) FROM InvItem WHERE CompanyID=@c AND IsDeleted=0;", AiRead.P("@c", c));
            long qty = AiRead.Scalar(store, "SELECT IFNULL(SUM(QuantityOnHand),0) FROM InvItemBalance WHERE CompanyID=@c;", AiRead.P("@c", c));
            long value = AiFacts.InventoryValue(store, c);
            long zero = AiFacts.ZeroStockCount(store, c);
            long slow = AiFacts.SlowMovingCount(store, c, since);
            long low = AiFacts.LowStockCount(store, c);
            long over = AiFacts.OverstockCount(store, c);
            List<AiMetricRow> rows = new List<AiMetricRow>
            {
                AiRead.Row("کالا", AiRead.N(items)),
                AiRead.Row("موجودی روی دست", AiRead.N(qty)),
                AiRead.Row("ارزش موجودی (جزئی)", AiRead.N(value)),
                AiRead.Row("کالای بدون موجودی", AiRead.N(zero)),
                AiRead.Row("کالای کندگردش (۹۰ روز)", AiRead.N(slow)),
                AiRead.Row("پیش‌بینی سفارش مجدد", AiRead.N(low)),
                AiRead.Row("تشخیص موجودی مازاد", AiRead.N(over)),
                AiRead.Row("هشدار ریسک موجودی", (zero + low) > 0 ? "فعال" : "آرام")
            };
            AiRead.AppendTop(rows, "کندگردش", AiFacts.SlowItems(store, c, since, 5), "Name", "Amt");
            AiRead.AppendTop(rows, "تندگردش", AiFacts.FastItems(store, c, since, 5), "Name", "Amt");
            AiRead.AppendTop(rows, "پیشنهاد سفارش", AiFacts.ReorderItems(store, c, 5), "Name", "Amt");
            return new AiAnalysisReport { Title = "تحلیل موجودی", Rows = rows };
        }

        public static AiAnalysisReport Financial(AiPlatformStore store, ILedgerIdentity identity)
        {
            int c = identity.CompanyId;
            string month = AiRead.MonthKey(identity.UtcNow);
            string prev = AiRead.MonthKey(identity.UtcNow.AddMonths(-1));
            long journals = AiRead.Scalar(store, "SELECT COUNT(1) FROM GlJournal WHERE CompanyID=@c AND IsDeleted=0 AND Status=@s;",
                AiRead.P("@c", c), AiRead.P("@s", LedgerCodes.JournalPosted));
            long cash = AiFacts.Cash(store, c);
            long ar = AiFacts.PostedSales(store, c);
            long ap = AiFacts.PostedPurchases(store, c);
            long aged = AiFacts.AgedReceivables(store, c, AiRead.IsoDate(identity.UtcNow.AddDays(-30)));
            long rev = AiFacts.RevenueGl(store, c);
            long exp = AiFacts.ExpenseGl(store, c);
            if (rev == 0) rev = ar;
            if (exp == 0) exp = ap;
            long profit = rev - exp;
            long thisS = AiFacts.MonthSales(store, c, month);
            long prevS = AiFacts.MonthSales(store, c, prev);
            long thisP = AiFacts.MonthPurchases(store, c, month);
            long prevP = AiFacts.MonthPurchases(store, c, prev);
            return new AiAnalysisReport
            {
                Title = "تحلیل مالی",
                Rows = new List<AiMetricRow>
                {
                    AiRead.Row("اسناد دفتر کل ثبت‌شده", AiRead.N(journals)),
                    AiRead.Row("موقعیت نقد (حساب‌های ۱۱xx)", AiRead.N(cash)),
                    AiRead.Row("مطالبات باز (فاکتور فروش ثبت‌شده)", AiRead.N(ar)),
                    AiRead.Row("بدهی باز (فاکتور خرید ثبت‌شده)", AiRead.N(ap)),
                    AiRead.Row("ریسک مطالبات بالای ۳۰ روز", AiRead.N(aged)),
                    AiRead.Row("تعهدات پرداختنی", AiRead.N(ap)),
                    AiRead.Row("روند نقد (فروش−خرید این ماه)", AiRead.N(thisS - thisP)),
                    AiRead.Row("روند سود", AiFacts.PctChange(thisS - thisP, prevS - prevP)),
                    AiRead.Row("روند هزینه", AiFacts.PctChange(thisP, prevP)),
                    AiRead.Row("سود (درآمد−هزینه)", AiRead.N(profit))
                }
            };
        }

        public static AiAnalysisReport Customers(AiPlatformStore store, ILedgerIdentity identity)
        {
            int c = identity.CompanyId;
            string since = AiRead.IsoDate(identity.UtcNow.AddDays(-90));
            long customers = AiRead.Scalar(store, "SELECT COUNT(1) FROM SalCustomer WHERE CompanyID=@c AND IsDeleted=0;", AiRead.P("@c", c));
            long vendors = AiRead.Scalar(store, "SELECT COUNT(1) FROM PurVendor WHERE CompanyID=@c AND IsDeleted=0;", AiRead.P("@c", c));
            long activities = AiRead.Scalar(store, "SELECT COUNT(1) FROM CrmActivity WHERE CompanyID=@c;", AiRead.P("@c", c));
            long leads = AiRead.Scalar(store, "SELECT COUNT(1) FROM CrmLead WHERE CompanyID=@c AND IsDeleted=0;", AiRead.P("@c", c));
            long opps = AiRead.Scalar(store, "SELECT COUNT(1) FROM CrmOpportunity WHERE CompanyID=@c AND IsDeleted=0;", AiRead.P("@c", c));
            long converted = AiRead.Scalar(store,
                "SELECT COUNT(1) FROM CrmLead WHERE CompanyID=@c AND IsDeleted=0 AND Status=@st;",
                AiRead.P("@c", c), AiRead.P("@st", CrmCodes.StatusConverted));
            long active = AiRead.Scalar(store, @"
SELECT COUNT(DISTINCT CustomerID) FROM SalInvoice
WHERE CompanyID=@c AND IsDeleted=0 AND Status=@s AND InvoiceDate >= @since AND CustomerID > 0;",
                AiRead.P("@c", c), AiRead.P("@s", TradeCodes.Posted), AiRead.P("@since", since));
            long lost = AiRead.Scalar(store, @"
SELECT COUNT(DISTINCT CustomerID) FROM SalInvoice i
WHERE i.CompanyID=@c AND i.IsDeleted=0 AND i.Status=@s AND i.CustomerID > 0
  AND NOT EXISTS (
    SELECT 1 FROM SalInvoice x WHERE x.CompanyID=i.CompanyID AND x.CustomerID=i.CustomerID
      AND x.IsDeleted=0 AND x.Status=@s AND x.InvoiceDate >= @since
  );",
                AiRead.P("@c", c), AiRead.P("@s", TradeCodes.Posted), AiRead.P("@since", since));
            string conv = leads == 0 ? "0%" : ((converted * 100) / leads).ToString(CultureInfo.InvariantCulture) + "%";
            List<AiMetricRow> rows = new List<AiMetricRow>
            {
                AiRead.Row("مشتریان فروش", AiRead.N(customers)),
                AiRead.Row("فروشندگان", AiRead.N(vendors)),
                AiRead.Row("فعالیت CRM", AiRead.N(activities)),
                AiRead.Row("سرنخ", AiRead.N(leads)),
                AiRead.Row("فرصت", AiRead.N(opps)),
                AiRead.Row("مشتریان فعال (۹۰ روز)", AiRead.N(active)),
                AiRead.Row("مشتریان ازدست‌رفته", AiRead.N(lost)),
                AiRead.Row("نرخ تبدیل سرنخ", conv)
            };
            AiRead.AppendTop(rows, "ارزش مشتری", AiFacts.TopCustomers(store, c, null, 5), "Name", "Amt");
            return new AiAnalysisReport { Title = "تحلیل مشتریان", Rows = rows };
        }
    }

    public sealed class AiForecastService
    {
        private readonly AiPlatformStore _store;
        private readonly AiLicenseService _license;
        public AiForecastService() : this(new AiPlatformStore(), new AiLicenseService()) { }
        public AiForecastService(AiPlatformStore store, AiLicenseService license)
        {
            _store = store;
            _license = license;
        }

        public AiAnalysisReport Forecast(ILedgerIdentity identity)
        {
            if (!_license.CanForecast(identity)) return new AiAnalysisReport
            {
                Title = "دسترسی ندارد",
                Rows = new List<AiMetricRow> { AiRead.Row("وضعیت", "پیش‌بینی نیازمند لایسنس Executive است.") }
            };
            int c = identity.CompanyId;
            DataTable months = AiFacts.MonthlySales(_store, c, 3);
            long sum = 0;
            int n = months.Rows.Count;
            for (int i = 0; i < n; i++) sum += Convert.ToInt64(months.Rows[i]["Amt"]);
            long monthAvg = n == 0 ? 0 : sum / n;
            DataTable invoices = _store.Query(@"
SELECT AmountMinor FROM SalInvoice WHERE CompanyID=@c AND IsDeleted=0 AND Status=@s
ORDER BY InvoiceDate DESC LIMIT 3;",
                AiRead.P("@c", c), AiRead.P("@s", TradeCodes.Posted));
            long invSum = 0;
            int ni = invoices.Rows.Count;
            for (int i = 0; i < ni; i++) invSum += Convert.ToInt64(invoices.Rows[i]["AmountMinor"]);
            long invAvg = ni == 0 ? 0 : invSum / ni;
            long cash = AiFacts.Cash(_store, c);
            long ap = AiFacts.PostedPurchases(_store, c);
            long cashNext = cash + monthAvg - AiFacts.MonthPurchases(_store, c, AiRead.MonthKey(identity.UtcNow));
            return new AiAnalysisReport
            {
                Title = "بینش مدیریتی",
                Rows = new List<AiMetricRow>
                {
                    AiRead.Row("پیش‌بینی فروش دوره بعد (میانگین ۳ ماه)", AiRead.N(monthAvg)),
                    AiRead.Row("پیش‌بینی فروش دوره بعد (میانگین ۳ فاکتور اخیر)", AiRead.N(invAvg)),
                    AiRead.Row("نمونه فاکتور برای میانگین", AiRead.N(ni)),
                    AiRead.Row("پیش‌بینی نقد", AiRead.N(cashNext)),
                    AiRead.Row("تعهد پرداختنی در پیش‌بینی", AiRead.N(ap)),
                    AiRead.Row("راهبرد", n == 0
                        ? "داده‌ای برای روند فروش ثبت نشده است."
                        : "تمرکز روی وصول مطالبات و سلامت موجودی؛ پیش‌بینی خطی است نه مدل یادگیری.")
                }
            };
        }
    }

    public sealed class AiAlertEngine
    {
        private readonly AiPlatformStore _store;
        private readonly AiLicenseService _license;
        public AiAlertEngine() : this(new AiPlatformStore(), new AiLicenseService()) { }
        public AiAlertEngine(AiPlatformStore store, AiLicenseService license)
        {
            _store = store;
            _license = license;
        }

        public IList<AiAlert> Evaluate(ILedgerIdentity identity)
        {
            List<AiAlert> list = new List<AiAlert>();
            if (!_license.CanAlerts(identity)) return list;
            int c = identity.CompanyId;
            DateTime asOf = identity.UtcNow;
            string month = AiRead.MonthKey(asOf);
            string prev = AiRead.MonthKey(asOf.AddMonths(-1));
            long low = AiFacts.LowStockCount(_store, c) + AiFacts.ZeroStockCount(_store, c);
            if (low > 0)
                list.Add(Alert("LOW_STOCK", "High", "کمبود موجودی", AiRead.N(low) + " کالا در ریسک سفارش/موجودی صفر."));
            long aged = AiFacts.AgedReceivables(_store, c, AiRead.IsoDate(asOf.AddDays(-30)));
            long ar = AiFacts.PostedSales(_store, c);
            if (aged > 0 && (ar == 0 || aged * 100 / ar >= 25))
                list.Add(Alert("OVERDUE_AR", "High", "مطالبات معوق بزرگ", "مطالبات قدیمی‌تر از ۳۰ روز: " + AiRead.N(aged)));
            long totalP = AiFacts.PostedPurchases(_store, c);
            DataTable v = AiFacts.TopVendors(_store, c, 1);
            if (totalP > 0 && v.Rows.Count > 0 && Convert.ToInt64(v.Rows[0]["Amt"]) * 100 / totalP >= 40)
                list.Add(Alert("VENDOR_CONCENTRATION", "Medium", "تمرکز فروشنده", "بیش از ۴۰٪ خرید از یک فروشنده است."));
            long thisS = AiFacts.MonthSales(_store, c, month);
            long prevS = AiFacts.MonthSales(_store, c, prev);
            if (prevS > 0 && thisS < prevS * 80 / 100)
                list.Add(Alert("REVENUE_DECLINE", "High", "کاهش درآمد", "فروش ماه جاری نسبت به ماه قبل بیش از ۲۰٪ کاهش یافته است."));
            long thisP = AiFacts.MonthPurchases(_store, c, month);
            long prevP = AiFacts.MonthPurchases(_store, c, prev);
            if (prevP > 0 && thisP > prevP * 150 / 100)
                list.Add(Alert("UNUSUAL_EXPENSE", "Medium", "هزینه غیرعادی", "خرید این ماه بیش از ۱٫۵ برابر ماه قبل است."));
            long cash = AiFacts.Cash(_store, c);
            if (cash < totalP / 4 && totalP > 0)
                list.Add(Alert("CASH_SHORTAGE", "High", "ریسک کمبود نقد", "موجودی نقد نسبت به تعهدات خرید پایین است."));
            if (list.Count == 0)
                list.Add(Alert("OK", "Info", "وضعیت آرام", "هشدار هوشمندی در آستانه تعریف‌شده نیست."));
            return list;
        }

        private static AiAlert Alert(string code, string sev, string title, string detail)
        {
            return new AiAlert { Code = code, Severity = sev, Title = title, Detail = detail };
        }
    }

    public sealed class AiExecutiveDashboardService
    {
        private readonly AiPlatformStore _store;
        private readonly AiLicenseService _license;
        private readonly AiForecastService _forecast;
        private readonly AiAlertEngine _alerts;

        public AiExecutiveDashboardService() : this(new AiPlatformStore(), new AiLicenseService()) { }
        public AiExecutiveDashboardService(AiPlatformStore store, AiLicenseService license)
        {
            _store = store;
            _license = license;
            _forecast = new AiForecastService(store, license);
            _alerts = new AiAlertEngine(store, license);
        }

        public AiExecutiveSnapshot Get(ILedgerIdentity identity)
        {
            AiExecutiveSnapshot snap = new AiExecutiveSnapshot { Widgets = new List<AiMetricRow>(), Summary = "" };
            if (!_license.CanExecutive(identity))
            {
                snap.Summary = "داشبورد مدیریتی نیازمند لایسنس Executive است.";
                snap.Widgets.Add(AiRead.Row("وضعیت", snap.Summary));
                return snap;
            }
            int c = identity.CompanyId;
            long ar = AiFacts.PostedSales(_store, c);
            long ap = AiFacts.PostedPurchases(_store, c);
            long rev = AiFacts.RevenueGl(_store, c);
            long exp = AiFacts.ExpenseGl(_store, c);
            if (rev == 0) rev = ar;
            if (exp == 0) exp = ap;
            long profit = rev - exp;
            long inv = AiFacts.InventoryValue(_store, c);
            long cash = AiFacts.Cash(_store, c);
            int health = Score(cash, profit, inv, ar, ap, identity);
            snap.RevenueMinor = rev;
            snap.ExpenseMinor = exp;
            snap.ProfitMinor = profit;
            snap.InventoryValueMinor = inv;
            snap.ReceivablesMinor = ar;
            snap.PayablesMinor = ap;
            snap.CashMinor = cash;
            snap.HealthScore = health;
            snap.Widgets.Add(AiRead.Row("درآمد", AiRead.N(rev)));
            snap.Widgets.Add(AiRead.Row("هزینه", AiRead.N(exp)));
            snap.Widgets.Add(AiRead.Row("سود", AiRead.N(profit)));
            snap.Widgets.Add(AiRead.Row("ارزش موجودی", AiRead.N(inv)));
            snap.Widgets.Add(AiRead.Row("مطالبات", AiRead.N(ar)));
            snap.Widgets.Add(AiRead.Row("بدهی‌ها", AiRead.N(ap)));
            snap.Widgets.Add(AiRead.Row("موقعیت نقد", AiRead.N(cash)));
            snap.Widgets.Add(AiRead.Row("امتیاز سلامت کسب‌وکار", health.ToString(CultureInfo.InvariantCulture)));
            StringBuilder sb = new StringBuilder();
            sb.Append("خلاصه مدیریتی: سلامت ").Append(health).Append("/100. ");
            sb.Append(profit >= 0 ? "سود عملیاتی مثبت است. " : "زیان عملیاتی مشاهده می‌شود. ");
            sb.Append(cash >= 0 ? "نقد در محدوده قابل قبول است. " : "نقد منفی است. ");
            IList<AiAlert> alerts = _alerts.Evaluate(identity);
            int hot = 0;
            for (int i = 0; i < alerts.Count; i++)
                if (alerts[i].Severity == "High") hot++;
            sb.Append("هشدارهای شدید: ").Append(hot).Append(". ");
            AiAnalysisReport f = _forecast.Forecast(identity);
            sb.Append(Find(f, "راهبرد"));
            snap.Summary = sb.ToString();
            snap.Widgets.Add(AiRead.Row("خلاصه اجرایی", snap.Summary));
            return snap;
        }

        private int Score(long cash, long profit, long inv, long ar, long ap, ILedgerIdentity identity)
        {
            int s = 40;
            if (cash > 0) s += 15;
            if (profit >= 0) s += 15;
            if (inv > 0) s += 10;
            string month = AiRead.MonthKey(identity.UtcNow);
            string prev = AiRead.MonthKey(identity.UtcNow.AddMonths(-1));
            long thisS = AiFacts.MonthSales(_store, identity.CompanyId, month);
            long prevS = AiFacts.MonthSales(_store, identity.CompanyId, prev);
            if (prevS == 0 || thisS >= prevS) s += 10; else s -= 10;
            if (ap == 0 || ar <= ap * 3) s += 10; else s -= 5;
            if (s < 0) s = 0;
            if (s > 100) s = 100;
            return s;
        }

        private static string Find(AiAnalysisReport report, string label)
        {
            if (report == null || report.Rows == null) return "";
            for (int i = 0; i < report.Rows.Count; i++)
                if (report.Rows[i].Label == label) return report.Rows[i].Value ?? "";
            return "";
        }
    }

    public sealed class AiReportGenerator
    {
        private readonly AiPlatformStore _store;
        private readonly AiLicenseService _license;
        private readonly AiExecutiveDashboardService _exec;

        public AiReportGenerator() : this(new AiPlatformStore(), new AiLicenseService()) { }
        public AiReportGenerator(AiPlatformStore store, AiLicenseService license)
        {
            _store = store;
            _license = license;
            _exec = new AiExecutiveDashboardService(store, license);
        }

        public static string ToText(AiAnalysisReport report)
        {
            return AiRead.FormatReport(report);
        }

        public string Generate(string kind, ILedgerIdentity identity)
        {
            if (identity == null) return "";
            if (string.Equals(kind, "Executive", StringComparison.OrdinalIgnoreCase))
            {
                if (!_license.CanExecutive(identity)) return "دسترسی ندارد";
                AiExecutiveSnapshot snap = _exec.Get(identity);
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("خلاصه مدیریتی گنجینه");
                sb.AppendLine(snap.Summary);
                if (snap.Widgets != null)
                    for (int i = 0; i < snap.Widgets.Count; i++)
                        sb.AppendLine(snap.Widgets[i].Label + ": " + snap.Widgets[i].Value);
                return sb.ToString();
            }
            if (!_license.CanReports(identity)) return "دسترسی ندارد";
            AiAnalysisReport report;
            if (string.Equals(kind, "Sales", StringComparison.OrdinalIgnoreCase)) report = AiSmartAnalytics.Sales(_store, identity);
            else if (string.Equals(kind, "Purchase", StringComparison.OrdinalIgnoreCase)) report = AiSmartAnalytics.Purchase(_store, identity);
            else if (string.Equals(kind, "Inventory", StringComparison.OrdinalIgnoreCase)) report = AiSmartAnalytics.Inventory(_store, identity);
            else report = AiSmartAnalytics.Financial(_store, identity);
            return AiRead.FormatReport(report);
        }
    }

    public sealed class AiQueryService
    {
        private readonly AiPlatformStore _store;
        private readonly AiLicenseService _license;

        public AiQueryService() : this(new AiPlatformStore(), new AiLicenseService()) { }
        public AiQueryService(AiPlatformStore store, AiLicenseService license)
        {
            _store = store;
            _license = license;
        }

        public string ResolveIntent(string text)
        {
            string q = Normalize(text);
            if (q.Length == 0) return AiQueryIntents.Unknown;
            if (Contains(q, "delete") || Contains(q, "update ") || Contains(q, "insert ") || Contains(q, "drop ")
                || q.Contains(";") || Contains(q, "post "))
                return AiQueryIntents.Unknown;
            if ((Contains(q, "top") && Contains(q, "customer")) || Contains(q, "مشتریان برتر") || Contains(q, "مشتری برتر"))
                return AiQueryIntents.TopCustomers;
            if ((Contains(q, "product") && (Contains(q, "not selling") || Contains(q, "slow")))
                || Contains(q, "نمی\u200cفروش") || Contains(q, "نمیفروش") || Contains(q, "کندگردش") || Contains(q, "فروش نمی"))
                return AiQueryIntents.SlowProducts;
            if (Contains(q, "cash") || Contains(q, "نقد"))
                return AiQueryIntents.Cash;
            if (Contains(q, "debtor") || Contains(q, "receivable") || Contains(q, "بدهکار") || Contains(q, "مطالبات"))
                return AiQueryIntents.Debtors;
            if ((Contains(q, "profit") && Contains(q, "trend")) || Contains(q, "روند سود"))
                return AiQueryIntents.ProfitTrend;
            if ((Contains(q, "inventory") && Contains(q, "risk")) || Contains(q, "ریسک موجودی"))
                return AiQueryIntents.InventoryRisk;
            if ((Contains(q, "top") && Contains(q, "product")) || Contains(q, "کالای پرفروش"))
                return AiQueryIntents.TopProducts;
            return AiQueryIntents.Unknown;
        }

        public AiQueryAnswer Ask(string text, ILedgerIdentity identity)
        {
            AiQueryAnswer ans = new AiQueryAnswer { Intent = AiQueryIntents.Unknown, Matched = false, Text = "", Report = null };
            if (identity == null || !_license.CanChat(identity))
            {
                ans.Text = "دسترسی ندارد";
                return ans;
            }
            string intent = ResolveIntent(text);
            ans.Intent = intent;
            if (intent == AiQueryIntents.Unknown) return ans;
            ans.Matched = true;
            ans.Report = Execute(intent, identity, text);
            ans.Text = AiRead.FormatReport(ans.Report);
            return ans;
        }

        private AiAnalysisReport Execute(string intent, ILedgerIdentity identity, string text)
        {
            int c = identity.CompanyId;
            string month = Contains(Normalize(text), "this month") || Contains(Normalize(text), "این ماه")
                ? AiRead.MonthKey(identity.UtcNow) : null;
            if (intent == AiQueryIntents.TopCustomers)
            {
                List<AiMetricRow> rows = new List<AiMetricRow>();
                AiRead.AppendTop(rows, "مشتری برتر", AiFacts.TopCustomers(_store, c, month, 5), "Name", "Amt");
                return new AiAnalysisReport { Title = "مشتریان برتر", Rows = rows };
            }
            if (intent == AiQueryIntents.TopProducts)
            {
                List<AiMetricRow> rows = new List<AiMetricRow>();
                AiRead.AppendTop(rows, "کالای پرفروش", AiFacts.TopProducts(_store, c, 5), "Name", "Amt");
                return new AiAnalysisReport { Title = "کالاهای پرفروش", Rows = rows };
            }
            if (intent == AiQueryIntents.SlowProducts)
                return new AiAnalysisReport
                {
                    Title = "کالاهای کم‌فروش",
                    Rows = new List<AiMetricRow>
                    {
                        AiRead.Row("کالای کندگردش (۹۰ روز)", AiRead.N(AiFacts.SlowMovingCount(_store, c, AiRead.IsoDate(identity.UtcNow.AddDays(-90)))))
                    }
                };
            if (intent == AiQueryIntents.Cash)
                return new AiAnalysisReport
                {
                    Title = "وضعیت نقد",
                    Rows = new List<AiMetricRow> { AiRead.Row("موقعیت نقد (حساب‌های ۱۱xx)", AiRead.N(AiFacts.Cash(_store, c))) }
                };
            if (intent == AiQueryIntents.Debtors)
                return new AiAnalysisReport
                {
                    Title = "بدهکاران",
                    Rows = new List<AiMetricRow>
                    {
                        AiRead.Row("مطالبات باز", AiRead.N(AiFacts.PostedSales(_store, c))),
                        AiRead.Row("مطالبات بالای ۳۰ روز", AiRead.N(AiFacts.AgedReceivables(_store, c, AiRead.IsoDate(identity.UtcNow.AddDays(-30)))))
                    }
                };
            if (intent == AiQueryIntents.ProfitTrend)
                return AiSmartAnalytics.Financial(_store, identity);
            if (intent == AiQueryIntents.InventoryRisk)
                return AiSmartAnalytics.Inventory(_store, identity);
            return new AiAnalysisReport { Title = "نامشخص", Rows = new List<AiMetricRow>() };
        }

        private static string Normalize(string text)
        {
            return (text ?? "").Trim().ToLowerInvariant();
        }

        private static bool Contains(string hay, string needle)
        {
            return hay.IndexOf(needle, StringComparison.Ordinal) >= 0;
        }
    }
}
