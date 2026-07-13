using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;
using ClosedXML.Excel;
using CaseManagement.Helpers;

namespace CaseManagement.Accounting
{
    // ─────────────────────────────────────────────────────────────────────────
    // موتور ۸ گزارش حسابداری ایتام — چاپ (Print/PrintPreview/PDF از طریق
    // پرینتر PDF ویندوز) و خروجی اکسل. تمام اعداد از AccountingRepo خوانده
    // می‌شوند؛ هیچ عددی دستی در این کلاس نوشته نمی‌شود.
    // آموزش: از همان الگوی PrintHelper (سربرگ/پاورقی/لوگو/مهر/امضا) استفاده
    // می‌شود اما با چیدمان اختصاصی هر گزارش (شبیه شیت‌های اکسل مرجع مؤسسه).
    // ─────────────────────────────────────────────────────────────────────────
    public class AccReports
    {
        private readonly AccountingRepo _repo;

        public AccReports(AccountingRepo repo) { _repo = repo; }

        private static readonly Font TitleFont = new Font("B Nazanin", 15F, FontStyle.Bold);
        private static readonly Font OrgFont = new Font("B Nazanin", 12F, FontStyle.Bold);
        private static readonly Font HeaderFont = new Font("B Nazanin", 10.5F, FontStyle.Bold);
        private static readonly Font LabelFont = new Font("B Nazanin", 10F, FontStyle.Bold);
        private static readonly Font ValueFont = new Font("B Nazanin", 10F, FontStyle.Regular);
        private static readonly Font TotalFont = new Font("B Nazanin", 10.5F, FontStyle.Bold);
        private static readonly Font FooterFont = new Font("B Nazanin", 8.5F);

        // ═══════════════════════════════════════════════════════════════════
        // زیرساخت مشترک چاپ: سربرگ/پاورقی/جدول صفحه‌بندی‌شده
        // ═══════════════════════════════════════════════════════════════════
        private class ReportModel
        {
            public string Title;
            public List<KeyValuePair<string, string>> SummaryRows = new List<KeyValuePair<string, string>>();
            public string[] ColumnHeaders;
            public float[] ColumnWeights;
            public List<string[]> Rows = new List<string[]>();
            public HashSet<int> BoldRows = new HashSet<int>();
        }

        private void Print(IWin32Window owner, ReportModel model)
        {
            int summaryDone = 0;
            bool summaryFinished = false;
            int rowIndex = 0;

            using (PrintDocument doc = new PrintDocument())
            {
                doc.DocumentName = model.Title;
                doc.BeginPrint += delegate { summaryDone = 0; summaryFinished = false; rowIndex = 0; };
                doc.PrintPage += delegate (object s, PrintPageEventArgs e)
                {
                    int y = e.MarginBounds.Top;
                    DrawHeader(e.Graphics, e, model.Title, ref y);

                    if (!summaryFinished)
                    {
                        summaryDone = DrawSummary(e.Graphics, e, model.SummaryRows, summaryDone, ref y);
                        summaryFinished = summaryDone >= model.SummaryRows.Count;
                        if (summaryFinished && model.SummaryRows.Count > 0) y += 10;
                    }

                    if (summaryFinished && model.ColumnHeaders != null)
                        rowIndex = DrawTable(e.Graphics, e, model.ColumnHeaders, model.ColumnWeights, model.Rows, model.BoldRows, rowIndex, ref y);

                    bool hasMore = !summaryFinished || (model.ColumnHeaders != null && rowIndex < model.Rows.Count);
                    e.HasMorePages = hasMore;

                    if (!hasMore)
                        DrawSignatures(e.Graphics, e);

                    DrawFooter(e.Graphics, e);
                };

                ShowPreview(owner, doc, model.Title);
            }
        }

        private void DrawHeader(Graphics g, PrintPageEventArgs e, string title, ref int y)
        {
            string orgName = _repo.GetSetting("OrgName");
            string headerText = _repo.GetSetting("HeaderText");
            StringFormat sfCenter = new StringFormat { Alignment = StringAlignment.Center };

            string logoPath = _repo.GetSetting("LogoPath");
            Image logo = TryLoadImage(logoPath);
            if (logo != null)
            {
                try { g.DrawImage(logo, e.MarginBounds.Right - 60, y, 55, 55); }
                finally { logo.Dispose(); }
            }

            if (!string.IsNullOrWhiteSpace(orgName))
            {
                g.DrawString(orgName, OrgFont, Brushes.Black, new RectangleF(e.MarginBounds.Left, y, e.MarginBounds.Width, 24), sfCenter);
                y += 24;
            }
            if (!string.IsNullOrWhiteSpace(headerText))
            {
                g.DrawString(headerText, ValueFont, Brushes.DimGray, new RectangleF(e.MarginBounds.Left, y, e.MarginBounds.Width, 18), sfCenter);
                y += 18;
            }

            g.DrawString(title, TitleFont, Brushes.Black, new RectangleF(e.MarginBounds.Left, y, e.MarginBounds.Width, 32), sfCenter);
            y += 32;
            g.DrawLine(Pens.Black, e.MarginBounds.Left, y, e.MarginBounds.Right, y);
            y += 10;
        }

        private int DrawSummary(Graphics g, PrintPageEventArgs e, List<KeyValuePair<string, string>> rows, int startIndex, ref int y)
        {
            int rowHeight = 24;
            int colWidth = e.MarginBounds.Width / 2;
            StringFormat sfLabel = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
            StringFormat sfValue = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };

            int i = startIndex;
            while (i < rows.Count && y + rowHeight < e.MarginBounds.Bottom)
            {
                // دو ستون همزمان در هر ردیف (چیدمان شبیه شیت‌های اکسل)
                for (int col = 0; col < 2 && i < rows.Count; col++, i++)
                {
                    int colRight = e.MarginBounds.Right - col * colWidth;
                    RectangleF labelRect = new RectangleF(colRight - 150, y, 150, rowHeight);
                    RectangleF valueRect = new RectangleF(colRight - colWidth + 8, y, colWidth - 158, rowHeight);
                    g.DrawString(rows[i].Key + " :", LabelFont, Brushes.Black, labelRect, sfLabel);
                    g.DrawString(rows[i].Value ?? "", ValueFont, Brushes.Black, valueRect, sfValue);
                }
                y += rowHeight;
            }
            return i;
        }

        private int DrawTable(Graphics g, PrintPageEventArgs e, string[] headers, float[] weights, List<string[]> rows, HashSet<int> boldRows, int startRow, ref int y)
        {
            float totalWeight = 0; foreach (float w in weights) totalWeight += w;
            float[] colWidths = new float[weights.Length];
            for (int i = 0; i < weights.Length; i++) colWidths[i] = e.MarginBounds.Width * weights[i] / totalWeight;

            int rowHeight = 24;
            StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

            // سربرگ جدول فقط در بالای هر صفحه رسم می‌شود
            float x = e.MarginBounds.Right;
            for (int c = 0; c < headers.Length; c++)
            {
                x -= colWidths[c];
                RectangleF rect = new RectangleF(x, y, colWidths[c], rowHeight);
                g.FillRectangle(new SolidBrush(UiTheme.Primary), rect);
                g.DrawRectangle(Pens.Gray, x, y, colWidths[c], rowHeight);
                g.DrawString(headers[c], HeaderFont, Brushes.White, rect, sf);
            }
            y += rowHeight;

            int r = startRow;
            while (r < rows.Count && y + rowHeight < e.MarginBounds.Bottom)
            {
                bool bold = boldRows.Contains(r);
                Font font = bold ? TotalFont : ValueFont;
                Brush back = bold ? new SolidBrush(UiTheme.HoverTint) : Brushes.White;

                x = e.MarginBounds.Right;
                for (int c = 0; c < headers.Length; c++)
                {
                    x -= colWidths[c];
                    RectangleF rect = new RectangleF(x, y, colWidths[c], rowHeight);
                    g.FillRectangle(back, rect);
                    g.DrawRectangle(Pens.LightGray, x, y, colWidths[c], rowHeight);
                    string val = c < rows[r].Length ? rows[r][c] : "";
                    g.DrawString(val, font, Brushes.Black, rect, sf);
                }
                y += rowHeight;
                r++;
            }
            return r;
        }

        private void DrawSignatures(Graphics g, PrintPageEventArgs e)
        {
            int y = e.MarginBounds.Bottom - 90;
            if (y < e.MarginBounds.Top) return;

            string accSign = _repo.GetSetting("AccountantSignature");
            string mgrSign = _repo.GetSetting("ManagerSignature");
            string prepSign = _repo.GetSetting("PreparerSignature");

            int colW = e.MarginBounds.Width / 3;
            StringFormat sfCenter = new StringFormat { Alignment = StringAlignment.Center };

            DrawSignatureBlock(g, e.MarginBounds.Right - colW, y, colW, "امضاء ترتیب‌کننده", prepSign);
            DrawSignatureBlock(g, e.MarginBounds.Right - colW * 2, y, colW, "امضاء حسابدار", accSign);
            DrawSignatureBlock(g, e.MarginBounds.Left, y, colW, "امضاء مسئول ایتام", mgrSign);
        }

        private void DrawSignatureBlock(Graphics g, int x, int y, int width, string label, string signaturePath)
        {
            StringFormat sfCenter = new StringFormat { Alignment = StringAlignment.Center };
            Image sig = TryLoadImage(signaturePath);
            if (sig != null)
            {
                try { g.DrawImage(sig, x + width / 2 - 45, y, 90, 40); }
                finally { sig.Dispose(); }
            }
            g.DrawLine(Pens.Black, x + 15, y + 44, x + width - 15, y + 44);
            g.DrawString(label, LabelFont, Brushes.Black, new RectangleF(x, y + 46, width, 20), sfCenter);
        }

        private void DrawFooter(Graphics g, PrintPageEventArgs e)
        {
            string footerText = _repo.GetSetting("FooterText");
            string text = string.IsNullOrWhiteSpace(footerText)
                ? "چاپ‌شده در " + PersianDateHelper.ToPersianDateTimeString(DateTime.Now)
                : footerText + "   —   " + PersianDateHelper.ToPersianDateTimeString(DateTime.Now);
            StringFormat sf = new StringFormat { Alignment = StringAlignment.Center };
            g.DrawString(text, FooterFont, Brushes.Gray, new RectangleF(e.MarginBounds.Left, e.MarginBounds.Bottom + 6, e.MarginBounds.Width, 16), sf);

            string stampPath = _repo.GetSetting("StampPath");
            Image stamp = TryLoadImage(stampPath);
            if (stamp != null)
            {
                try { g.DrawImage(stamp, e.MarginBounds.Left, e.MarginBounds.Bottom - 160, 75, 75); }
                finally { stamp.Dispose(); }
            }
        }

        private static Image TryLoadImage(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path)) return null;
                using (var fs = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite))
                using (var img = Image.FromStream(fs))
                    return new Bitmap(img);
            }
            catch { return null; }
        }

        private void ShowPreview(IWin32Window owner, PrintDocument doc, string title)
        {
            using (PrintPreviewDialog ppd = new PrintPreviewDialog())
            {
                ppd.Document = doc;
                ppd.Text = "پیش‌نمایش چاپ — " + title;
                ppd.StartPosition = FormStartPosition.CenterScreen;
                ppd.WindowState = FormWindowState.Maximized;
                ppd.MinimumSize = new Size(820, 600);
                ppd.RightToLeft = RightToLeft.Yes;
                ppd.ShowDialog(owner);
            }
        }

        private static string N(double v) { return v.ToString("N0"); }

        // ═══════════════════════════════════════════════════════════════════
        // فاکتور/سند تک‌رویداد (سند دریافت یا سند پرداخت) — برای هر تراکنش
        // ═══════════════════════════════════════════════════════════════════
        public void PrintVoucher(IWin32Window owner, int txnId)
        {
            DataRow row = _repo.GetTransactionById(txnId);
            if (row == null) { UiTheme.ShowWarning(owner, "تراکنش پیدا نشد."); return; }

            bool income = row["Direction"].ToString() == "دریافت";
            string docTitle = income ? "سند دریافت" : "سند پرداخت";
            double amount = Convert.ToDouble(row["Amount"]);

            using (PrintDocument doc = new PrintDocument())
            {
                doc.DocumentName = docTitle + " " + row["DocNo"];
                doc.PrintPage += delegate (object s, PrintPageEventArgs e)
                {
                    Graphics g = e.Graphics;
                    int y = e.MarginBounds.Top;
                    DrawHeader(g, e, docTitle + "   شماره: " + row["DocNo"], ref y);

                    int left = e.MarginBounds.Left;
                    int right = e.MarginBounds.Right;
                    int width = e.MarginBounds.Width;
                    y += 6;

                    // ردیف‌های اطلاعات سند (label:value، دو ستون)
                    var pairs = new List<KeyValuePair<string, string>>();
                    pairs.Add(new KeyValuePair<string, string>("تاریخ", GetStr(row, "TxnDate")));
                    pairs.Add(new KeyValuePair<string, string>("دوره مالی", GetStr(row, "PeriodTitle")));
                    pairs.Add(new KeyValuePair<string, string>("نوع سند", income ? "دریافت" : "پرداخت"));
                    pairs.Add(new KeyValuePair<string, string>("صندوق", GetStr(row, "FundName")));
                    pairs.Add(new KeyValuePair<string, string>(income ? "دریافت از" : "پرداخت به", GetStr(row, "PartyName")));
                    pairs.Add(new KeyValuePair<string, string>("دسته‌بندی", GetStr(row, "CategoryName")));
                    if (row["Qty"] != DBNull.Value && !string.IsNullOrWhiteSpace(row["Qty"].ToString()))
                        pairs.Add(new KeyValuePair<string, string>("تعداد/مقدار", GetStr(row, "Qty")));
                    if (row["DollarAmount"] != DBNull.Value)
                    {
                        pairs.Add(new KeyValuePair<string, string>("مبلغ دلاری", Convert.ToDouble(row["DollarAmount"]).ToString("N2")));
                        if (row["DollarRate"] != DBNull.Value)
                            pairs.Add(new KeyValuePair<string, string>("نرخ دلار", Convert.ToDouble(row["DollarRate"]).ToString("N2")));
                    }

                    int rowH = 30;
                    int colW = width / 2;
                    StringFormat sfLabel = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
                    StringFormat sfValue = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };

                    for (int i = 0; i < pairs.Count; i += 2)
                    {
                        for (int c = 0; c < 2 && (i + c) < pairs.Count; c++)
                        {
                            int cellRight = right - c * colW;
                            Rectangle cell = new Rectangle(cellRight - colW, y, colW, rowH);
                            g.DrawRectangle(Pens.LightGray, cell);
                            RectangleF lblR = new RectangleF(cellRight - 130, y, 122, rowH);
                            RectangleF valR = new RectangleF(cellRight - colW + 6, y, colW - 140, rowH);
                            g.DrawString(pairs[i + c].Key + " :", LabelFont, Brushes.Black, lblR, sfLabel);
                            g.DrawString(pairs[i + c].Value, ValueFont, Brushes.Black, valR, sfValue);
                        }
                        y += rowH;
                    }

                    // جعبه مبلغ (برجسته)
                    y += 10;
                    Rectangle amountBox = new Rectangle(left, y, width, 44);
                    g.FillRectangle(new SolidBrush(UiTheme.HoverTint), amountBox);
                    g.DrawRectangle(Pens.Gray, amountBox);
                    g.DrawString("مبلغ :  " + N(amount) + "  افغانی", new Font("B Nazanin", 14F, FontStyle.Bold), Brushes.Black,
                        new RectangleF(left + 8, y, width - 16, 44), new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center });
                    y += 50;

                    // مبلغ به حروف
                    Rectangle wordsBox = new Rectangle(left, y, width, 34);
                    g.DrawRectangle(Pens.LightGray, wordsBox);
                    g.DrawString("به حروف :  " + NumberToPersianWords((long)Math.Round(amount)) + " افغانی", LabelFont, Brushes.Black,
                        new RectangleF(left + 8, y, width - 16, 34), new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center });
                    y += 40;

                    // شرح
                    string desc = GetStr(row, "Description");
                    if (!string.IsNullOrWhiteSpace(desc))
                    {
                        Rectangle descBox = new Rectangle(left, y, width, 60);
                        g.DrawRectangle(Pens.LightGray, descBox);
                        g.DrawString("شرح :  " + desc, ValueFont, Brushes.Black,
                            new RectangleF(left + 8, y + 4, width - 16, 52), new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Near });
                        y += 66;
                    }

                    DrawSignatures(g, e);
                    DrawFooter(g, e);
                    e.HasMorePages = false;
                };

                ShowPreview(owner, doc, docTitle);
            }
        }

        private static string GetStr(DataRow row, string col)
        {
            if (!row.Table.Columns.Contains(col) || row[col] == DBNull.Value) return "";
            return row[col].ToString();
        }

        // ─── تبدیل عدد به حروف فارسی (برای «مبلغ به حروف» روی سند) ─────────────
        private static readonly string[] _ones = { "", "یک", "دو", "سه", "چهار", "پنج", "شش", "هفت", "هشت", "نه",
            "ده", "یازده", "دوازده", "سیزده", "چهارده", "پانزده", "شانزده", "هفده", "هجده", "نوزده" };
        private static readonly string[] _tens = { "", "", "بیست", "سی", "چهل", "پنجاه", "شصت", "هفتاد", "هشتاد", "نود" };
        private static readonly string[] _hundreds = { "", "یکصد", "دویست", "سیصد", "چهارصد", "پانصد", "ششصد", "هفتصد", "هشتصد", "نهصد" };
        private static readonly string[] _scales = { "", " هزار", " میلیون", " میلیارد", " بیلیون" };

        private static string NumberToPersianWords(long number)
        {
            if (number == 0) return "صفر";
            if (number < 0) return "منفی " + NumberToPersianWords(-number);

            var groups = new System.Collections.Generic.List<int>();
            while (number > 0) { groups.Add((int)(number % 1000)); number /= 1000; }

            var parts = new System.Collections.Generic.List<string>();
            for (int i = groups.Count - 1; i >= 0; i--)
            {
                if (groups[i] == 0) continue;
                parts.Add(ThreeDigitToWords(groups[i]) + _scales[i]);
            }
            return string.Join(" و ", parts);
        }

        private static string ThreeDigitToWords(int n)
        {
            var parts = new System.Collections.Generic.List<string>();
            int h = n / 100, rem = n % 100;
            if (h > 0) parts.Add(_hundreds[h]);
            if (rem >= 20) { parts.Add(_tens[rem / 10]); if (rem % 10 > 0) parts.Add(_ones[rem % 10]); }
            else if (rem > 0) parts.Add(_ones[rem]);
            return string.Join(" و ", parts);
        }

        // ═══════════════════════════════════════════════════════════════════
        // زیرساخت مشترک اکسل
        // ═══════════════════════════════════════════════════════════════════
        private IXLWorksheet NewSheet(XLWorkbook wb, string title, List<KeyValuePair<string, string>> summary)
        {
            IXLWorksheet ws = wb.Worksheets.Add(title.Length > 28 ? title.Substring(0, 28) : title);
            ws.RightToLeft = true;
            ws.Cell(1, 1).Value = _repo.GetSetting("OrgName");
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 13;
            ws.Range(1, 1, 1, 6).Merge();

            ws.Cell(2, 1).Value = title;
            ws.Cell(2, 1).Style.Font.Bold = true;
            ws.Cell(2, 1).Style.Font.FontSize = 14;
            ws.Range(2, 1, 2, 6).Merge();

            int r = 4;
            foreach (var kv in summary)
            {
                ws.Cell(r, 1).Value = kv.Key;
                ws.Cell(r, 1).Style.Font.Bold = true;
                ws.Cell(r, 2).Value = kv.Value;
                r++;
            }
            return ws;
        }

        private int WriteTable(IXLWorksheet ws, int startRow, string[] headers, List<string[]> rows, HashSet<int> boldRows)
        {
            for (int c = 0; c < headers.Length; c++)
            {
                ws.Cell(startRow, c + 1).Value = headers[c];
                ws.Cell(startRow, c + 1).Style.Font.Bold = true;
                ws.Cell(startRow, c + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#2C5A85");
                ws.Cell(startRow, c + 1).Style.Font.FontColor = XLColor.White;
            }
            int r = startRow + 1;
            for (int i = 0; i < rows.Count; i++)
            {
                for (int c = 0; c < rows[i].Length; c++)
                {
                    ws.Cell(r, c + 1).Value = rows[i][c];
                    if (boldRows.Contains(i)) ws.Cell(r, c + 1).Style.Font.Bold = true;
                }
                r++;
            }
            ws.Range(startRow, 1, r - 1, headers.Length).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(startRow, 1, r - 1, headers.Length).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            ws.Columns().AdjustToContents();
            return r;
        }

        // ═══════════════════════════════════════════════════════════════════
        // گزارش ۱: صورت حساب کلی
        // ═══════════════════════════════════════════════════════════════════
        private ReportModel BuildGeneralStatement(int? periodId)
        {
            string periodTitle = periodId.HasValue ? _repo.GetPeriodTitle(periodId.Value) : "همه دوره‌ها";
            double opening = periodId.HasValue ? _repo.GetPeriodOpening(periodId.Value) : 0;
            double income = _repo.SumTransactions(periodId, "دریافت", null);
            double stipend = SumStipendTotal(periodId);
            double salary = _repo.SumSalary(periodId);
            double otherExpense = _repo.SumExpenseItems(periodId);
            double paymentTxn = _repo.SumTransactions(periodId, "پرداخت", null);
            double totalPayment = stipend + salary + otherExpense + paymentTxn;
            double closing = opening + income - totalPayment;

            DataTable stipendRows = _repo.GetStipends(periodId);
            double dollarSum = 0, afghaniFromDollar = 0;
            DataTable rawTxns = _repo.GetTransactionsRaw(periodId, null, null);
            foreach (DataRow row in rawTxns.Rows)
            {
                if (row["DollarAmount"] != DBNull.Value && row["Direction"].ToString() == "دریافت")
                {
                    dollarSum += Convert.ToDouble(row["DollarAmount"]);
                    afghaniFromDollar += Convert.ToDouble(row["Amount"]);
                }
            }
            double rate = dollarSum > 0 ? afghaniFromDollar / dollarSum : 0;

            var model = new ReportModel { Title = "صورت حساب کلی ایتام  —  " + SecurityContext.CenterDisplay + "  بابت " + periodTitle };
            model.SummaryRows.Add(new KeyValuePair<string, string>("نام مرکز", SecurityContext.CenterDisplay));
            model.SummaryRows.Add(new KeyValuePair<string, string>("دوره", periodTitle));
            model.SummaryRows.Add(new KeyValuePair<string, string>("تاریخ گزارش", PersianDateHelper.ToPersianDateTimeString(DateTime.Now)));
            model.SummaryRows.Add(new KeyValuePair<string, string>("مسئول ثبت", SecurityContext.Username));
            if (dollarSum > 0)
            {
                model.SummaryRows.Add(new KeyValuePair<string, string>("مبلغ دریافتی (دلاری)", dollarSum.ToString("N2")));
                model.SummaryRows.Add(new KeyValuePair<string, string>("نرخ دلار به افغانی", rate.ToString("N2")));
            }
            model.SummaryRows.Add(new KeyValuePair<string, string>("باقی مانده از قبل", N(opening)));
            model.SummaryRows.Add(new KeyValuePair<string, string>("جمع کل موجودی", N(opening + income)));
            model.SummaryRows.Add(new KeyValuePair<string, string>("مجموع پرداختی و مصارف", N(totalPayment)));
            model.SummaryRows.Add(new KeyValuePair<string, string>("باقی مانده برج فعلی", N(closing)));

            model.ColumnHeaders = new[] { "عنوان", "مرکز", "مبلغ (افغانی)" };
            model.ColumnWeights = new float[] { 2, 1.5f, 1.5f };
            AddIfNonZero(model, "شهریه ایتام", stipend);
            AddIfNonZero(model, "حقوق پرسنل", salary);
            DataTable catSummary = _repo.GetExpenseCategorySummary(periodId);
            foreach (DataRow r in catSummary.Rows)
                AddIfNonZero(model, r["عنوان"].ToString(), Convert.ToDouble(r["مبلغ"]));
            model.BoldRows.Add(model.Rows.Count);
            model.Rows.Add(new[] { "جمع کل", SecurityContext.CenterDisplay, N(totalPayment) });
            return model;
        }

        private void AddIfNonZero(ReportModel model, string title, double amount)
        {
            if (amount == 0) return;
            model.Rows.Add(new[] { title, SecurityContext.CenterDisplay, N(amount) });
        }

        private double SumStipendTotal(int? periodId)
        {
            double total = 0;
            DataTable dt = _repo.GetStipends(periodId);
            foreach (DataRow r in dt.Rows) total += Convert.ToDouble(r["جمع پرداختی"]);
            return total;
        }

        public void PrintGeneralStatement(IWin32Window owner, int? periodId) { Print(owner, BuildGeneralStatement(periodId)); }

        public void ExportGeneralStatementExcel(string path, int? periodId)
        {
            var model = BuildGeneralStatement(periodId);
            using (var wb = new XLWorkbook())
            {
                var ws = NewSheet(wb, model.Title, model.SummaryRows);
                WriteTable(ws, model.SummaryRows.Count + 6, model.ColumnHeaders, model.Rows, model.BoldRows);
                wb.SaveAs(path);
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // گزارش ۲: صورت حساب جزیی شهریه
        // ═══════════════════════════════════════════════════════════════════
        private ReportModel BuildDetailedStatement(int? periodId)
        {
            string periodTitle = periodId.HasValue ? _repo.GetPeriodTitle(periodId.Value) : "همه دوره‌ها";
            var model = new ReportModel { Title = "صورت حساب جزیی ایتام  —  " + SecurityContext.CenterDisplay + "  بابت " + periodTitle };
            model.SummaryRows.Add(new KeyValuePair<string, string>("مرکز", SecurityContext.CenterDisplay));
            model.SummaryRows.Add(new KeyValuePair<string, string>("دوره", periodTitle));

            model.ColumnHeaders = new[] { "ولایت", "نوع", "چند نفره", "تعداد خانوار", "تعداد یتیم", "مبلغ شهریه", "جمع پرداختی" };
            model.ColumnWeights = new float[] { 1.3f, 1f, 0.9f, 1f, 0.9f, 1.1f, 1.2f };

            DataTable dt = _repo.GetStipends(periodId);
            double grandFamily = 0, grandOrphan = 0, grandTotal = 0;
            string lastType = null;
            double typeFamily = 0, typeOrphan = 0, typeTotal = 0;

            foreach (DataRow r in dt.Rows)
            {
                string sadatType = r["نوع"].ToString();
                if (lastType != null && sadatType != lastType)
                {
                    model.BoldRows.Add(model.Rows.Count);
                    model.Rows.Add(new[] { "", "جمع " + lastType, "", typeFamily.ToString("N0"), typeOrphan.ToString("N0"), "", N(typeTotal) });
                    typeFamily = 0; typeOrphan = 0; typeTotal = 0;
                }
                lastType = sadatType;

                double fc = Convert.ToDouble(r["تعداد خانوار"]);
                double oc = Convert.ToDouble(r["تعداد یتیم"]);
                double tp = Convert.ToDouble(r["جمع پرداختی"]);
                typeFamily += fc; typeOrphan += oc; typeTotal += tp;
                grandFamily += fc; grandOrphan += oc; grandTotal += tp;

                model.Rows.Add(new[]
                {
                    r["ولایت"].ToString(), sadatType, r["چند نفره"].ToString(),
                    fc.ToString("N0"), oc.ToString("N0"), Convert.ToDouble(r["مبلغ شهریه"]).ToString("N0"), N(tp)
                });
            }
            if (lastType != null)
            {
                model.BoldRows.Add(model.Rows.Count);
                model.Rows.Add(new[] { "", "جمع " + lastType, "", typeFamily.ToString("N0"), typeOrphan.ToString("N0"), "", N(typeTotal) });
            }
            model.BoldRows.Add(model.Rows.Count);
            model.Rows.Add(new[] { "", "جمع کل", "", grandFamily.ToString("N0"), grandOrphan.ToString("N0"), "", N(grandTotal) });
            return model;
        }

        public void PrintDetailedStatement(IWin32Window owner, int? periodId) { Print(owner, BuildDetailedStatement(periodId)); }

        public void ExportDetailedStatementExcel(string path, int? periodId)
        {
            var model = BuildDetailedStatement(periodId);
            using (var wb = new XLWorkbook())
            {
                var ws = NewSheet(wb, model.Title, model.SummaryRows);
                WriteTable(ws, model.SummaryRows.Count + 6, model.ColumnHeaders, model.Rows, model.BoldRows);
                wb.SaveAs(path);
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // گزارش ۳: صورت حساب هزینه‌ها
        // ═══════════════════════════════════════════════════════════════════
        private ReportModel BuildExpenseStatement(int? periodId)
        {
            string periodTitle = periodId.HasValue ? _repo.GetPeriodTitle(periodId.Value) : "همه دوره‌ها";
            var model = new ReportModel { Title = "صورت حساب هزینه‌ها  —  " + SecurityContext.CenterDisplay + "  بابت " + periodTitle };
            model.SummaryRows.Add(new KeyValuePair<string, string>("مرکز", SecurityContext.CenterDisplay));
            model.SummaryRows.Add(new KeyValuePair<string, string>("دوره", periodTitle));

            model.ColumnHeaders = new[] { "شماره", "دسته‌بندی", "شرح", "تعداد/مقدار", "قیمت", "شماره سند", "تاریخ" };
            model.ColumnWeights = new float[] { 0.6f, 1.1f, 2.6f, 1f, 1f, 0.9f, 1f };

            DataTable dt = _repo.GetExpenseItems(periodId);
            double total = 0;
            int i = 1;
            foreach (DataRow r in dt.Rows)
            {
                double price = Convert.ToDouble(r["قیمت"]);
                total += price;
                model.Rows.Add(new[]
                {
                    (i++).ToString(), r["دسته‌بندی"].ToString(), r["شرح"].ToString(), r["تعداد/مقدار"].ToString(),
                    price.ToString("N0"), r["شماره سند"].ToString(), r["تاریخ"].ToString()
                });
            }
            model.BoldRows.Add(model.Rows.Count);
            model.Rows.Add(new[] { "", "", "", "", "", "جمع", N(total) });
            return model;
        }

        public void PrintExpenseStatement(IWin32Window owner, int? periodId) { Print(owner, BuildExpenseStatement(periodId)); }

        public void ExportExpenseStatementExcel(string path, int? periodId)
        {
            var model = BuildExpenseStatement(periodId);
            using (var wb = new XLWorkbook())
            {
                var ws = NewSheet(wb, model.Title, model.SummaryRows);
                WriteTable(ws, model.SummaryRows.Count + 6, model.ColumnHeaders, model.Rows, model.BoldRows);
                wb.SaveAs(path);
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // گزارش ۴: صورت حساب حقوق
        // ═══════════════════════════════════════════════════════════════════
        private ReportModel BuildSalaryStatement(int? periodId)
        {
            string periodTitle = periodId.HasValue ? _repo.GetPeriodTitle(periodId.Value) : "همه دوره‌ها";
            var model = new ReportModel { Title = "صورت حساب حقوق  —  " + SecurityContext.CenterDisplay + "  بابت " + periodTitle };
            model.SummaryRows.Add(new KeyValuePair<string, string>("مرکز", SecurityContext.CenterDisplay));
            model.SummaryRows.Add(new KeyValuePair<string, string>("دوره", periodTitle));

            model.ColumnHeaders = new[] { "نام", "سمت", "مبلغ" };
            model.ColumnWeights = new float[] { 1.6f, 1.3f, 1.1f };

            DataTable dt = _repo.GetSalaries(periodId);
            double total = 0;
            foreach (DataRow r in dt.Rows)
            {
                double amt = Convert.ToDouble(r["مبلغ"]);
                total += amt;
                model.Rows.Add(new[] { r["نام"].ToString(), r["سمت"].ToString(), amt.ToString("N0") });
            }
            model.BoldRows.Add(model.Rows.Count);
            model.Rows.Add(new[] { "", "جمع کل", N(total) });
            return model;
        }

        public void PrintSalaryStatement(IWin32Window owner, int? periodId) { Print(owner, BuildSalaryStatement(periodId)); }

        public void ExportSalaryStatementExcel(string path, int? periodId)
        {
            var model = BuildSalaryStatement(periodId);
            using (var wb = new XLWorkbook())
            {
                var ws = NewSheet(wb, model.Title, model.SummaryRows);
                WriteTable(ws, model.SummaryRows.Count + 6, model.ColumnHeaders, model.Rows, model.BoldRows);
                wb.SaveAs(path);
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // گزارش ۵: صورت حساب دریافت بودجه
        // ═══════════════════════════════════════════════════════════════════
        private ReportModel BuildBudgetReceiptStatement(int? periodId)
        {
            var model = new ReportModel { Title = "صورت حساب دریافت بودجه  —  " + SecurityContext.CenterDisplay };
            model.ColumnHeaders = new[] { "دوره", "مانده قبل", "دریافت بودجه", "پرداخت", "مانده پایان" };
            model.ColumnWeights = new float[] { 1.4f, 1.2f, 1.2f, 1.2f, 1.2f };

            DataTable periods = _repo.GetPeriodsForCombo();
            double sumOpen = 0, sumIn = 0, sumOut = 0, sumClose = 0;
            foreach (DataRow r in periods.Rows)
            {
                int pid = Convert.ToInt32(r["PeriodID"]);
                if (periodId.HasValue && pid != periodId.Value) continue;

                double opening = _repo.GetPeriodOpening(pid);
                double budgetIn = _repo.SumTransactions(pid, "دریافت", "Income");
                double stipend = SumStipendTotal(pid);
                double salary = _repo.SumSalary(pid);
                double expense = _repo.SumExpenseItems(pid);
                double payTxn = _repo.SumTransactions(pid, "پرداخت", null);
                double totalOut = stipend + salary + expense + payTxn;
                double closing = opening + budgetIn - totalOut;

                sumOpen += opening; sumIn += budgetIn; sumOut += totalOut; sumClose = closing;
                model.Rows.Add(new[] { r["Display"].ToString(), N(opening), N(budgetIn), N(totalOut), N(closing) });
            }
            model.BoldRows.Add(model.Rows.Count);
            model.Rows.Add(new[] { "جمع", N(sumOpen), N(sumIn), N(sumOut), N(sumClose) });
            return model;
        }

        public void PrintBudgetReceiptStatement(IWin32Window owner, int? periodId) { Print(owner, BuildBudgetReceiptStatement(periodId)); }

        public void ExportBudgetReceiptExcel(string path, int? periodId)
        {
            var model = BuildBudgetReceiptStatement(periodId);
            using (var wb = new XLWorkbook())
            {
                var ws = NewSheet(wb, model.Title, model.SummaryRows);
                WriteTable(ws, model.SummaryRows.Count + 6, model.ColumnHeaders, model.Rows, model.BoldRows);
                wb.SaveAs(path);
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // گزارش ۶: دفتر صندوق (مانده تجمعی صحیح از ابتدای صندوق)
        // ═══════════════════════════════════════════════════════════════════
        private ReportModel BuildFundLedger(int fundId, int? periodId)
        {
            string fundName = _repo.GetFundName(fundId);
            var model = new ReportModel { Title = "دفتر صندوق  —  " + fundName };
            model.SummaryRows.Add(new KeyValuePair<string, string>("صندوق", fundName));
            if (periodId.HasValue)
                model.SummaryRows.Add(new KeyValuePair<string, string>("دوره", _repo.GetPeriodTitle(periodId.Value)));

            model.ColumnHeaders = new[] { "تاریخ", "شماره سند", "شرح/طرف حساب", "دریافت", "پرداخت", "مانده" };
            model.ColumnWeights = new float[] { 1f, 0.9f, 2f, 1f, 1f, 1.1f };

            double balance = _repo.GetFundOpening(fundId);
            model.Rows.Add(new[] { "", "", "مانده اولیه صندوق", "", "", N(balance) });
            model.BoldRows.Add(0);

            DataTable dt = _repo.GetFundTransactionsChronological(fundId);
            foreach (DataRow r in dt.Rows)
            {
                double amount = Convert.ToDouble(r["Amount"]);
                bool isIncome = r["Direction"].ToString() == "دریافت";
                balance += isIncome ? amount : -amount;

                bool inPeriod = !periodId.HasValue || (r["PeriodID"] != DBNull.Value && Convert.ToInt32(r["PeriodID"]) == periodId.Value);
                if (!inPeriod) continue;

                string desc = r["PartyName"] != DBNull.Value ? r["PartyName"].ToString() : (r["Description"] != DBNull.Value ? r["Description"].ToString() : "");
                model.Rows.Add(new[]
                {
                    r["TxnDate"].ToString(), r["DocNo"].ToString(), desc,
                    isIncome ? amount.ToString("N0") : "", isIncome ? "" : amount.ToString("N0"), N(balance)
                });
            }
            model.BoldRows.Add(model.Rows.Count);
            model.Rows.Add(new[] { "", "", "مانده فعلی صندوق", "", "", N(balance) });
            return model;
        }

        public void PrintFundLedger(IWin32Window owner, int fundId, int? periodId) { Print(owner, BuildFundLedger(fundId, periodId)); }

        public void ExportFundLedgerExcel(string path, int fundId, int? periodId)
        {
            var model = BuildFundLedger(fundId, periodId);
            using (var wb = new XLWorkbook())
            {
                var ws = NewSheet(wb, model.Title, model.SummaryRows);
                WriteTable(ws, model.SummaryRows.Count + 6, model.ColumnHeaders, model.Rows, model.BoldRows);
                wb.SaveAs(path);
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // گزارش ۷: دفتر طرف حساب
        // ═══════════════════════════════════════════════════════════════════
        private ReportModel BuildPartyLedger(int partyId)
        {
            string partyName = _repo.GetPartyName(partyId);
            var model = new ReportModel { Title = "دفتر طرف حساب  —  " + partyName };
            model.SummaryRows.Add(new KeyValuePair<string, string>("طرف حساب", partyName));

            model.ColumnHeaders = new[] { "تاریخ", "شماره سند", "صندوق", "شرح", "دریافت", "پرداخت" };
            model.ColumnWeights = new float[] { 1f, 0.9f, 1.1f, 1.8f, 1f, 1f };

            double sumIn = 0, sumOut = 0;
            DataTable dt = _repo.GetPartyTransactionsChronological(partyId);
            foreach (DataRow r in dt.Rows)
            {
                double amount = Convert.ToDouble(r["Amount"]);
                bool isIncome = r["Direction"].ToString() == "دریافت";
                if (isIncome) sumIn += amount; else sumOut += amount;

                model.Rows.Add(new[]
                {
                    r["TxnDate"].ToString(), r["DocNo"].ToString(),
                    r["FundName"] != DBNull.Value ? r["FundName"].ToString() : "",
                    r["Description"] != DBNull.Value ? r["Description"].ToString() : "",
                    isIncome ? amount.ToString("N0") : "", isIncome ? "" : amount.ToString("N0")
                });
            }
            model.BoldRows.Add(model.Rows.Count);
            model.Rows.Add(new[] { "", "", "", "جمع کل", N(sumIn), N(sumOut) });
            return model;
        }

        public void PrintPartyLedger(IWin32Window owner, int partyId) { Print(owner, BuildPartyLedger(partyId)); }

        public void ExportPartyLedgerExcel(string path, int partyId)
        {
            var model = BuildPartyLedger(partyId);
            using (var wb = new XLWorkbook())
            {
                var ws = NewSheet(wb, model.Title, model.SummaryRows);
                WriteTable(ws, model.SummaryRows.Count + 6, model.ColumnHeaders, model.Rows, model.BoldRows);
                wb.SaveAs(path);
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // گزارش ۸: ریز تراکنش‌های یک دوره مالی (دریافت/پرداخت + شهریه + حقوق + هزینه)
        // ═══════════════════════════════════════════════════════════════════
        private ReportModel BuildPeriodDetail(int periodId)
        {
            string periodTitle = _repo.GetPeriodTitle(periodId);
            var model = new ReportModel { Title = "ریز تراکنش‌های دوره  —  " + periodTitle };
            model.SummaryRows.Add(new KeyValuePair<string, string>("دوره", periodTitle));
            model.SummaryRows.Add(new KeyValuePair<string, string>("مانده ابتدای دوره", N(_repo.GetPeriodOpening(periodId))));
            model.SummaryRows.Add(new KeyValuePair<string, string>("مانده پایان دوره", N(_repo.GetPeriodClosing(periodId))));

            model.ColumnHeaders = new[] { "نوع", "تاریخ", "شماره سند", "شرح", "دریافت", "پرداخت" };
            model.ColumnWeights = new float[] { 0.9f, 1f, 0.9f, 2.2f, 1f, 1f };

            DataTable txns = _repo.GetTransactionsRaw(periodId, null, null);
            foreach (DataRow r in txns.Rows)
            {
                double amount = Convert.ToDouble(r["Amount"]);
                bool isIncome = r["Direction"].ToString() == "دریافت";
                string desc = (r["PartyName"] != DBNull.Value ? r["PartyName"].ToString() + " — " : "") +
                              (r["Description"] != DBNull.Value ? r["Description"].ToString() : "");
                model.Rows.Add(new[] { "تراکنش", r["TxnDate"].ToString(), r["DocNo"].ToString(), desc,
                    isIncome ? amount.ToString("N0") : "", isIncome ? "" : amount.ToString("N0") });
            }

            DataTable stipends = _repo.GetStipends(periodId);
            foreach (DataRow r in stipends.Rows)
            {
                double amt = Convert.ToDouble(r["جمع پرداختی"]);
                model.Rows.Add(new[] { "شهریه", "", "", r["نوع"] + " / " + r["چند نفره"] + " نفره", "", amt.ToString("N0") });
            }

            DataTable salaries = _repo.GetSalaries(periodId);
            foreach (DataRow r in salaries.Rows)
            {
                double amt = Convert.ToDouble(r["مبلغ"]);
                model.Rows.Add(new[] { "حقوق", "", "", r["نام"] + " / " + r["سمت"], "", amt.ToString("N0") });
            }

            DataTable expenses = _repo.GetExpenseItems(periodId);
            foreach (DataRow r in expenses.Rows)
            {
                double amt = Convert.ToDouble(r["قیمت"]);
                model.Rows.Add(new[] { "هزینه", r["تاریخ"].ToString(), r["شماره سند"].ToString(), r["شرح"].ToString(), "", amt.ToString("N0") });
            }

            return model;
        }

        public void PrintPeriodDetail(IWin32Window owner, int periodId) { Print(owner, BuildPeriodDetail(periodId)); }

        public void ExportPeriodDetailExcel(string path, int periodId)
        {
            var model = BuildPeriodDetail(periodId);
            using (var wb = new XLWorkbook())
            {
                var ws = NewSheet(wb, model.Title, model.SummaryRows);
                WriteTable(ws, model.SummaryRows.Count + 6, model.ColumnHeaders, model.Rows, model.BoldRows);
                wb.SaveAs(path);
            }
        }
    }
}
