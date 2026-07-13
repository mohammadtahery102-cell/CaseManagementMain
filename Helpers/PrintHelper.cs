using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;

namespace CaseManagement.Helpers
{
    // چاپ حرفه‌ای عمومی: سربرگ با نام/لوگوی مؤسسه، عنوان سند، و بدنه
    // (فهرست لیبل:مقدار یا جدول)، با پاگین‌بندی خودکار برای اسناد بلند.
    public static class PrintHelper
    {
        private static readonly Font TitleFont  = new Font("Segoe UI", 15F, FontStyle.Bold);
        private static readonly Font HeaderFont = new Font("Segoe UI", 10F, FontStyle.Bold);
        private static readonly Font LabelFont  = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        private static readonly Font ValueFont  = new Font("Segoe UI", 9.5F, FontStyle.Regular);
        private static readonly Font FooterFont = new Font("Segoe UI", 8F);

        // ─── چاپ سند تک‌رکوردی (پرونده/عضو خانواده/سند) به‌صورت لیبل:مقدار ────
        public static void PrintKeyValueDocument(IWin32Window owner, string title, List<KeyValuePair<string, string>> fields)
        {
            int index = 0;

            using (PrintDocument doc = new PrintDocument())
            {
                doc.DocumentName = title;
                doc.BeginPrint += delegate { index = 0; };
                doc.PrintPage += delegate (object s, PrintPageEventArgs e)
                {
                    index = DrawKeyValuePage(e, title, fields, index);
                    e.HasMorePages = index < fields.Count;
                };

                ShowPreview(owner, doc, title);
            }
        }

        private static int DrawKeyValuePage(PrintPageEventArgs e, string title, List<KeyValuePair<string, string>> fields, int startIndex)
        {
            Graphics g = e.Graphics;
            int y = e.MarginBounds.Top;

            DrawHeader(g, e, title, ref y);

            int labelWidth = 170;
            int rowHeight = 26;

            StringFormat sfLabel = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
            StringFormat sfValue = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };

            int i = startIndex;
            while (i < fields.Count && y + rowHeight < e.MarginBounds.Bottom)
            {
                KeyValuePair<string, string> kv = fields[i];
                RectangleF labelRect = new RectangleF(e.MarginBounds.Right - labelWidth, y, labelWidth, rowHeight);
                RectangleF valueRect = new RectangleF(e.MarginBounds.Left, y, e.MarginBounds.Right - labelWidth - e.MarginBounds.Left - 10, rowHeight);

                g.DrawString(kv.Key + " :", LabelFont, Brushes.Black, labelRect, sfLabel);
                g.DrawString(kv.Value ?? "", ValueFont, Brushes.Black, valueRect, sfValue);
                g.DrawLine(Pens.LightGray, e.MarginBounds.Left, y + rowHeight - 2, e.MarginBounds.Right, y + rowHeight - 2);

                y += rowHeight;
                i++;
            }

            DrawFooter(g, e);
            return i;
        }

        // ─── چاپ جدول (فهرست اعضا/اسناد/کمک‌ها/گزارش‌ها) ───────────────────────
        public static void PrintDataTable(IWin32Window owner, string title, DataTable table)
        {
            int rowIndex = 0;

            using (PrintDocument doc = new PrintDocument())
            {
                doc.DocumentName = title;
                doc.BeginPrint += delegate { rowIndex = 0; };
                doc.PrintPage += delegate (object s, PrintPageEventArgs e)
                {
                    rowIndex = DrawTablePage(e, title, table, rowIndex);
                    e.HasMorePages = rowIndex < table.Rows.Count;
                };

                ShowPreview(owner, doc, title);
            }
        }

        private static int DrawTablePage(PrintPageEventArgs e, string title, DataTable table, int startRow)
        {
            Graphics g = e.Graphics;
            int y = e.MarginBounds.Top;
            DrawHeader(g, e, title, ref y);

            int colCount = table.Columns.Count;
            if (colCount == 0)
            {
                DrawFooter(g, e);
                return table.Rows.Count;
            }

            float colWidth = e.MarginBounds.Width / (float)colCount;
            int rowHeight = 24;
            StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

            float x = e.MarginBounds.Right;
            for (int c = 0; c < colCount; c++)
            {
                x -= colWidth;
                RectangleF rect = new RectangleF(x, y, colWidth, rowHeight);
                g.FillRectangle(Brushes.WhiteSmoke, rect);
                g.DrawRectangle(Pens.Gray, x, y, colWidth, rowHeight);
                g.DrawString(UiTheme.TranslateHeader(table.Columns[c].ColumnName), HeaderFont, Brushes.Black, rect, sf);
            }
            y += rowHeight;

            int r = startRow;
            while (r < table.Rows.Count && y + rowHeight < e.MarginBounds.Bottom)
            {
                x = e.MarginBounds.Right;
                for (int c = 0; c < colCount; c++)
                {
                    x -= colWidth;
                    RectangleF rect = new RectangleF(x, y, colWidth, rowHeight);
                    g.DrawRectangle(Pens.LightGray, x, y, colWidth, rowHeight);
                    object val = table.Rows[r][c];
                    g.DrawString(val == null || val == DBNull.Value ? "" : val.ToString(), ValueFont, Brushes.Black, rect, sf);
                }
                y += rowHeight;
                r++;
            }

            DrawFooter(g, e);
            return r;
        }

        // ─── لوگو/مهر/امضا روی سربرگ و پاورقی — قابل روشن/خاموش از تب چاپ ─────
        private static void DrawHeader(Graphics g, PrintPageEventArgs e, string title, ref int y)
        {
            string orgName = SettingsHelper.Get(SettingsHelper.OrgName);
            StringFormat sfCenter = new StringFormat { Alignment = StringAlignment.Center };

            if (SettingsHelper.GetInt(SettingsHelper.ShowLogoOnPrint, 0) == 1)
            {
                Image logo = TryLoadImage(SettingsHelper.Get(SettingsHelper.LogoPath));
                if (logo != null)
                {
                    try { g.DrawImage(logo, e.MarginBounds.Right - 60, y, 60, 60); }
                    finally { logo.Dispose(); }
                }
            }

            if (!string.IsNullOrWhiteSpace(orgName))
            {
                g.DrawString(orgName, HeaderFont, Brushes.Black,
                    new RectangleF(e.MarginBounds.Left, y, e.MarginBounds.Width, 24), sfCenter);
                y += 24;
            }

            g.DrawString(title, TitleFont, Brushes.Black,
                new RectangleF(e.MarginBounds.Left, y, e.MarginBounds.Width, 34), sfCenter);
            y += 34;

            g.DrawLine(Pens.Black, e.MarginBounds.Left, y, e.MarginBounds.Right, y);
            y += 12;
        }

        private static void DrawFooter(Graphics g, PrintPageEventArgs e)
        {
            string footer = "چاپ‌شده در " + PersianDateHelper.ToPersianDateTimeString(DateTime.Now);
            StringFormat sf = new StringFormat { Alignment = StringAlignment.Center };
            g.DrawString(footer, FooterFont, Brushes.Gray,
                new RectangleF(e.MarginBounds.Left, e.MarginBounds.Bottom + 8, e.MarginBounds.Width, 18), sf);

            int stampX = e.MarginBounds.Left;
            if (SettingsHelper.GetInt(SettingsHelper.ShowStamp, 0) == 1)
            {
                Image stamp = TryLoadImage(SettingsHelper.Get(SettingsHelper.StampPath));
                if (stamp != null)
                {
                    try { g.DrawImage(stamp, stampX, e.MarginBounds.Bottom - 70, 70, 70); }
                    finally { stamp.Dispose(); }
                }
            }

            if (SettingsHelper.GetInt(SettingsHelper.ShowSignature, 0) == 1)
            {
                Image signature = TryLoadImage(SettingsHelper.Get(SettingsHelper.SignaturePath));
                if (signature != null)
                {
                    try { g.DrawImage(signature, e.MarginBounds.Right - 120, e.MarginBounds.Bottom - 50, 120, 50); }
                    finally { signature.Dispose(); }
                }
            }
        }

        private static Image TryLoadImage(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
                    return null;
                using (var fs = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite))
                using (var img = Image.FromStream(fs))
                    return new Bitmap(img);
            }
            catch { return null; }
        }

        // ─── پرینتر پیش‌فرض/اندازه کاغذ/حاشیه‌ها از تب چاپ (اگر تنظیم شده باشد) ─
        private static void ApplyPrintSettings(PrintDocument doc)
        {
            string printer = SettingsHelper.Get(SettingsHelper.DefaultPrinter);
            if (!string.IsNullOrWhiteSpace(printer))
            {
                try
                {
                    foreach (string name in PrinterSettings.InstalledPrinters)
                    {
                        if (string.Equals(name, printer, StringComparison.OrdinalIgnoreCase))
                        {
                            doc.PrinterSettings.PrinterName = printer;
                            break;
                        }
                    }
                }
                catch { /* انتخاب پرینتر غیرحیاتی است */ }
            }

            int marginTop = SettingsHelper.GetInt(SettingsHelper.MarginTop, -1);
            int marginBottom = SettingsHelper.GetInt(SettingsHelper.MarginBottom, -1);
            int marginLeft = SettingsHelper.GetInt(SettingsHelper.MarginLeft, -1);
            int marginRight = SettingsHelper.GetInt(SettingsHelper.MarginRight, -1);
            if (marginTop >= 0 && marginBottom >= 0 && marginLeft >= 0 && marginRight >= 0)
            {
                try
                {
                    doc.DefaultPageSettings.Margins = new Margins(marginLeft, marginRight, marginTop, marginBottom);
                }
                catch { /* حاشیه نامعتبر؛ پیش‌فرض سیستم حفظ می‌شود */ }
            }
        }

        private static void ShowPreview(IWin32Window owner, PrintDocument doc, string title)
        {
            ApplyPrintSettings(doc);

            using (PrintPreviewDialog ppd = new PrintPreviewDialog())
            {
                ppd.Document = doc;
                ppd.Text = "پیش‌نمایش چاپ — " + title;
                // آموزش — رفع باگ «بالای صفحه چاپ دیده نمی‌شود»: پیش‌فرض
                // PrintPreviewDialog گاهی با اندازه/موقعیتی باز می‌شد که نوار
                // ابزار بالای آن (دکمه چاپ/بزرگ‌نمایی) بالاتر از لبه صفحه
                // می‌افتاد و در دسترس نبود. با Maximized کردن، کل پنجره از جمله
                // نوار ابزار بالا همیشه کامل و در صفحه دیده می‌شود.
                ppd.StartPosition = FormStartPosition.CenterScreen;
                ppd.WindowState = FormWindowState.Maximized;
                ppd.MinimumSize = new Size(820, 600);
                ppd.RightToLeft = RightToLeft.Yes;
                ppd.ShowDialog(owner);
            }
        }
    }
}
