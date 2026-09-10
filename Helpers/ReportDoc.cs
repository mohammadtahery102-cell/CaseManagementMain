using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace CaseManagement.Helpers
{
    // ═════════════════════════════════════════════════════════════════════════
    // موتورِ سندِ چاپیِ حرفه‌ای — سربرگِ مؤسسه، عنوان، بلوک‌های محتوا، پاورقی،
    // شمارهٔ «صفحهٔ X از Y»، و صفحه‌بندیِ واقعی.
    //
    // آموزش — چرا کلاسِ تازه و نه تغییرِ PrintHelper: آن کلاس فقط دو شکلِ ثابت
    // می‌شناسد (لیبل:مقدار، یا یک جدول با ستون‌های هم‌عرض) و هر سندِ چندبخشی
    // را مجبور می‌کند چند بار پیش‌نمایش باز کند. اینجا سند از «بلوک» ساخته
    // می‌شود و همهٔ بلوک‌ها در یک سندِ پیوسته صفحه‌بندی می‌گردند. PrintHelper
    // دست‌نخورده می‌ماند تا فرم‌هایی که هنوز از آن استفاده می‌کنند نشکنند.
    //
    // معماریِ رندر (سه گام):
    //   ۱. Flatten — هر بلوک با اندازه‌گیریِ واقعیِ متن به چند «اتم» شکسته
    //      می‌شود؛ اتم کوچک‌ترین چیزی است که *نباید* بین دو صفحه بشکند
    //      (یک سطرِ جدول، یک ردیفِ لیبل:مقدار، یک خطِ پاراگراف).
    //   ۲. Paginate — اتم‌ها حریصانه در صفحه‌ها چیده می‌شوند. اتمی که
    //      RepeatHeader دارد (سطرهای جدول)، اگر ابتدای صفحه بیفتد سرستونِ
    //      جدول دوباره بالای آن کشیده می‌شود — همان کاری که Word می‌کند.
    //   ۳. Draw — هر صفحه فقط اتم‌های خودش را می‌کشد. چون شمارشِ صفحه‌ها قبل
    //      از کشیدن انجام شده، «صفحهٔ ۲ از ۷» از صفحهٔ اول درست است.
    //
    // راست‌به‌چپ: همهٔ متن‌ها با StringFormatFlags.DirectionRightToLeft کشیده
    // می‌شوند و ستون‌های جدول از راست به چپ چیده می‌گردند.
    // ═════════════════════════════════════════════════════════════════════════

    public enum ReportAlign { Right, Center, Left }

    // ─── ستونِ جدول ──────────────────────────────────────────────────────────
    public sealed class ReportDocColumn
    {
        public string Header;
        public string Field;                 // نامِ ستون در DataTable
        public float Weight = 1f;            // سهمِ نسبی از عرضِ جدول
        public ReportAlign Align = ReportAlign.Right;
        public bool Bold;

        public static ReportDocColumn Of(string header, string field, float weight,
                                      ReportAlign align = ReportAlign.Right)
        {
            return new ReportDocColumn { Header = header, Field = field, Weight = weight, Align = align };
        }
    }

    // ─── بلوک‌ها ─────────────────────────────────────────────────────────────
    public abstract class ReportBlock { }

    public sealed class ReportHeading : ReportBlock
    {
        public string Text;
        public string Note;                  // متنِ کم‌رنگِ کنارِ عنوان (مثلاً «۱۲ ردیف»)
        public bool PageBreakBefore;
    }

    public sealed class ReportParagraph : ReportBlock
    {
        public string Text;
        public bool Muted;
    }

    public sealed class ReportCallout : ReportBlock
    {
        public string Text;
        public Color Accent = UiTheme.Warning;
    }

    public sealed class ReportKeyValues : ReportBlock
    {
        public List<KeyValuePair<string, string>> Items = new List<KeyValuePair<string, string>>();
        public int Columns = 2;              // چند جفتِ لیبل:مقدار در هر سطر
    }

    public sealed class ReportTable : ReportBlock
    {
        public DataTable Data;
        public List<ReportDocColumn> Columns = new List<ReportDocColumn>();
        public bool ShowRowNumbers = true;
        public string EmptyText = "ردیفی برای نمایش وجود ندارد.";

        // اگر Columns خالی بماند، همهٔ ستون‌های DataTable با وزنِ برابر
        // استفاده می‌شوند — راهِ سریعِ چاپِ یک جدولِ آماده.
        public static ReportTable FromDataTable(DataTable data, params string[] hiddenColumns)
        {
            var t = new ReportTable { Data = data };
            if (data == null) return t;

            foreach (DataColumn c in data.Columns)
            {
                if (hiddenColumns != null && hiddenColumns.Contains(c.ColumnName)) continue;
                t.Columns.Add(ReportDocColumn.Of(c.ColumnName, c.ColumnName, 1f,
                    IsNumericLike(c) ? ReportAlign.Center : ReportAlign.Right));
            }
            return t;
        }

        private static bool IsNumericLike(DataColumn c)
        {
            Type t = c.DataType;
            return t == typeof(int) || t == typeof(long) || t == typeof(decimal) ||
                   t == typeof(double) || t == typeof(float) || t == typeof(short);
        }
    }

    // ─── شبکهٔ عکس ───────────────────────────────────────────────────────────
    // آموزش — تا امروز ReportDoc فقط متن و جدول می‌کشید، پس عکس‌های بازدید
    // میدانی هیچ راهی به خروجی نداشتند. این بلوک عکس‌ها را چند‌تا‌در‌ردیف با
    // زیرنویس می‌چیند؛ هر ردیف یک «اتم» است، پس صفحه‌بندی وسطِ یک ردیف
    // نمی‌شکند.
    public sealed class ReportImageItem
    {
        public string Path;
        public string Caption;
    }

    public sealed class ReportImageGrid : ReportBlock
    {
        public List<ReportImageItem> Items = new List<ReportImageItem>();
        public int Columns = 3;
        public float CellHeight = 170f;      // ارتفاعِ کادرِ عکس (یک‌صدمِ اینچ)
        public string EmptyText = "عکسی ثبت نشده است.";
    }

    public sealed class ReportSpacer : ReportBlock { public float Height = 10f; }

    public sealed class ReportPageBreak : ReportBlock { }

    public sealed class ReportSignatures : ReportBlock
    {
        public string[] Roles = { "تهیه‌کننده", "تأییدکننده", "مدیر مرکز" };
    }

    // ═════════════════════════════════════════════════════════════════════════
    public sealed class ReportDoc
    {
        // ── شناسنامهٔ سند ────────────────────────────────────────────────────
        public string Title = "";
        public string Subtitle = "";
        public string DocumentCode = "";      // شمارهٔ سند در پاورقی
        public List<KeyValuePair<string, string>> HeaderFields =
            new List<KeyValuePair<string, string>>();

        public List<ReportBlock> Blocks = new List<ReportBlock>();

        // صفحهٔ جلد (برای سندهای بلند مثل «پروندهٔ کامل»)
        public bool ShowCoverPage;
        public string CoverHeadline = "";
        public List<KeyValuePair<string, string>> CoverFields =
            new List<KeyValuePair<string, string>>();

        public bool Landscape;

        // ── قلم‌ها ───────────────────────────────────────────────────────────
        // فونتِ سیستمیِ فارسی‌خوان. Tahoma روی هر ویندوزی هست و اعداد/حروفِ
        // فارسی را درست می‌چیند؛ اگر فونتِ اختصاصیِ مؤسسه نصب بود، همان.
        private static readonly string FaFamily = ResolveFamily();
        private static string ResolveFamily()
        {
            string[] wanted = { "IRANSans", "Vazirmatn", "B Nazanin", "Sahel", "Tahoma" };
            var installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (FontFamily f in FontFamily.Families) installed.Add(f.Name);
            }
            catch { }
            foreach (string w in wanted)
                if (installed.Contains(w)) return w;
            return "Tahoma";
        }

        private static Font F(float size, FontStyle style = FontStyle.Regular)
        {
            return new Font(FaFamily, size, style, GraphicsUnit.Point);
        }

        private static readonly Font FnCoverTitle = F(24f, FontStyle.Bold);
        private static readonly Font FnTitle = F(15f, FontStyle.Bold);
        private static readonly Font FnSubtitle = F(10f);
        private static readonly Font FnOrg = F(12.5f, FontStyle.Bold);
        private static readonly Font FnOrgSub = F(7.5f);
        private static readonly Font FnHeading = F(11.5f, FontStyle.Bold);
        private static readonly Font FnLabel = F(8.5f, FontStyle.Bold);
        private static readonly Font FnValue = F(9f);
        private static readonly Font FnTableHead = F(8.5f, FontStyle.Bold);
        private static readonly Font FnTableCell = F(8.5f);
        private static readonly Font FnFooter = F(7.5f);

        private static readonly Color Ink = ColorTranslator.FromHtml("#1F2A36");
        private static readonly Color InkMuted = ColorTranslator.FromHtml("#69778A");
        private static readonly Color Rule = ColorTranslator.FromHtml("#C9D3DF");
        private static readonly Color RuleSoft = ColorTranslator.FromHtml("#E4EAF1");
        private static readonly Color HeadBand = ColorTranslator.FromHtml("#1B3A5C");
        private static readonly Color Zebra = ColorTranslator.FromHtml("#F5F8FB");
        private static readonly Color Accent = ColorTranslator.FromHtml("#2C5A85");

        private static readonly StringFormat SfRight = MakeFormat(StringAlignment.Near);
        private static readonly StringFormat SfCenter = MakeFormat(StringAlignment.Center);
        private static readonly StringFormat SfLeft = MakeFormat(StringAlignment.Far);

        private static StringFormat MakeFormat(StringAlignment align)
        {
            // آموزش — با DirectionRightToLeft، معنیِ Near/Far برعکس می‌شود:
            // Near یعنی «سمتِ راست». پس SfRight = Near و SfLeft = Far.
            return new StringFormat(StringFormatFlags.DirectionRightToLeft)
            {
                Alignment = align,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter
            };
        }

        private static readonly StringFormat WrapNear   = MakeWrapped(StringAlignment.Near);
        private static readonly StringFormat WrapCenter = MakeWrapped(StringAlignment.Center);
        private static readonly StringFormat WrapFar    = MakeWrapped(StringAlignment.Far);

        private static StringFormat MakeWrapped(StringAlignment align)
        {
            return new StringFormat(StringFormatFlags.DirectionRightToLeft)
            {
                Alignment = align,
                LineAlignment = StringAlignment.Near,
                Trimming = StringTrimming.Word
            };
        }

        // سه قالبِ ثابت و نه ساختِ تازه در هر سلول: DrawString در هر صفحه
        // صدها بار صدا زده می‌شود و هر StringFormat یک هندلِ GDI+ می‌گیرد.
        private static StringFormat Wrapped(StringAlignment align)
        {
            if (align == StringAlignment.Center) return WrapCenter;
            if (align == StringAlignment.Far) return WrapFar;
            return WrapNear;
        }

        private static StringFormat FormatFor(ReportAlign a)
        {
            if (a == ReportAlign.Center) return SfCenter;
            if (a == ReportAlign.Left) return SfLeft;
            return SfRight;
        }

        // ═════════════════════════════════════════════════════════════════════
        // اتم — کوچک‌ترین واحدی که بین دو صفحه شکسته نمی‌شود.
        // ═════════════════════════════════════════════════════════════════════
        private sealed class Atom
        {
            public float Height;
            public Action<Graphics, RectangleF> Draw;
            public Atom RepeatHeader;     // سرستونی که باید بالای صفحهٔ بعد تکرار شود
            public bool IsRepeatHeader;
            public bool KeepWithNext;     // عنوانِ بخش نباید تنها ته صفحه بماند
            public bool CoverOnly;
        }

        private List<List<Atom>> _pages;
        private float _contentWidth;

        // ═════════════════════════════════════════════════════════════════════
        // خروجی‌ها
        // ═════════════════════════════════════════════════════════════════════
        public void Preview(IWin32Window owner)
        {
            using (PrintDocument doc = CreateDocument())
            using (PrintPreviewDialog dlg = new PrintPreviewDialog())
            {
                dlg.Document = doc;
                dlg.Text = "پیش‌نمایش چاپ — " + Title;
                dlg.StartPosition = FormStartPosition.CenterScreen;
                dlg.WindowState = FormWindowState.Maximized;
                dlg.MinimumSize = new Size(900, 640);
                dlg.RightToLeft = RightToLeft.Yes;
                dlg.UseAntiAlias = true;
                try { dlg.ShowDialog(owner); }
                catch (Exception ex)
                {
                    UiTheme.ShowError(owner, "خطا در پیش‌نمایش چاپ: " + ex.Message);
                }
            }
        }

        // چاپِ مستقیم روی پرینترِ پیش‌فرض (بدونِ پیش‌نمایش).
        public void PrintDirect()
        {
            using (PrintDocument doc = CreateDocument())
                doc.Print();
        }

        // ── ذخیرهٔ PDF بدونِ وابستگیِ بیرونی ─────────────────────────────────
        // آموزش — «Microsoft Print to PDF» از ویندوز ۱۰ به بعد جزوِ خودِ
        // سیستم‌عامل است، پس ساختِ PDF نه به Word نیاز دارد نه LibreOffice.
        // اگر روی این ماشین نصب/فعال نبود، false برمی‌گردد تا فراخواننده
        // بتواند به مسیرِ دیگری (Word→PDF) برگردد و پیامِ روشن بدهد.
        public const string PdfPrinterName = "Microsoft Print to PDF";

        public static bool IsPdfPrinterAvailable()
        {
            try
            {
                foreach (string p in PrinterSettings.InstalledPrinters)
                    if (string.Equals(p, PdfPrinterName, StringComparison.OrdinalIgnoreCase))
                        return true;
            }
            catch { }
            return false;
        }

        public bool SaveAsPdf(string outputPath)
        {
            if (!IsPdfPrinterAvailable()) return false;

            string folder = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(folder)) Directory.CreateDirectory(folder);
            if (File.Exists(outputPath)) File.Delete(outputPath);

            using (PrintDocument doc = CreateDocument())
            {
                doc.PrinterSettings.PrinterName = PdfPrinterName;
                doc.PrinterSettings.PrintToFile = true;
                doc.PrinterSettings.PrintFileName = outputPath;

                // تعویضِ پرینتر، DefaultPageSettings را از درایورِ تازه
                // بازمی‌سازد؛ جهت و حاشیه باید دوباره اعمال شوند وگرنه سندِ
                // افقی عمودی چاپ می‌شود.
                doc.DefaultPageSettings.Landscape = Landscape;
                doc.DefaultPageSettings.Margins = new Margins(45, 45, 50, 50);
                doc.PrintController = new StandardPrintController();   // بدونِ پنجرهٔ «در حال چاپ»
                doc.Print();
            }

            // درایورِ PDF فایل را همزمان می‌نویسد؛ چند لحظه صبر تا کاملِ آن
            // روی دیسک بنشیند، وگرنه بازکردنِ فوریِ فایل خطا می‌دهد.
            for (int i = 0; i < 40 && !FileReady(outputPath); i++)
                System.Threading.Thread.Sleep(150);

            return File.Exists(outputPath);
        }

        private static bool FileReady(string path)
        {
            try
            {
                if (!File.Exists(path)) return false;
                using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None)) { }
                return new FileInfo(path).Length > 0;
            }
            catch { return false; }
        }

        // ═════════════════════════════════════════════════════════════════════
        // عمومی است تا بشود بیرونِ پیش‌نمایش هم سند را رندر کرد (وارسیِ
        // چیدمان، چاپِ مستقیم، یا هر مقصدِ چاپیِ دیگر).
        public PrintDocument CreateDocument()
        {
            var doc = new PrintDocument();
            doc.DocumentName = string.IsNullOrWhiteSpace(Title) ? "گزارش" : Title;
            doc.DefaultPageSettings.Landscape = Landscape;
            doc.DefaultPageSettings.Margins = new Margins(45, 45, 50, 50);
            ApplyUserPrintSettings(doc);

            int pageIndex = 0;

            doc.BeginPrint += delegate
            {
                pageIndex = 0;
                using (Graphics g = doc.PrinterSettings.CreateMeasurementGraphics(doc.DefaultPageSettings))
                {
                    Rectangle bounds = doc.DefaultPageSettings.Bounds;
                    Margins m = doc.DefaultPageSettings.Margins;
                    var area = new RectangleF(m.Left, m.Top,
                                              bounds.Width - m.Left - m.Right,
                                              bounds.Height - m.Top - m.Bottom);
                    BuildPages(g, area);
                }
            };

            doc.PrintPage += delegate (object s, PrintPageEventArgs e)
            {
                if (_pages == null || _pages.Count == 0)
                {
                    e.HasMorePages = false;
                    return;
                }

                DrawPage(e, pageIndex);
                pageIndex++;
                e.HasMorePages = pageIndex < _pages.Count;
            };

            return doc;
        }

        private static void ApplyUserPrintSettings(PrintDocument doc)
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
                catch { }
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // گام ۱ و ۲ — شکستنِ بلوک‌ها به اتم و چیدنِ اتم‌ها در صفحه‌ها
        // ═════════════════════════════════════════════════════════════════════
        private void BuildPages(Graphics g, RectangleF area)
        {
            _contentWidth = area.Width;

            float headerHeight = MeasureHeaderHeight();
            float bodyHeight = area.Height - headerHeight - FooterHeight;

            var atoms = new List<Atom>();
            foreach (ReportBlock b in Blocks) Flatten(g, b, atoms);

            _pages = new List<List<Atom>>();

            if (ShowCoverPage)
                _pages.Add(new List<Atom> { new Atom { CoverOnly = true, Height = 0, Draw = null } });

            var page = new List<Atom>();
            float used = 0f;

            for (int i = 0; i < atoms.Count; i++)
            {
                Atom a = atoms[i];

                if (a.Height < 0)   // شکستِ صفحهٔ دستی
                {
                    if (page.Count > 0) { _pages.Add(page); page = new List<Atom>(); used = 0f; }
                    continue;
                }

                // عنوانِ بخش تنها ته صفحه نماند: با اتمِ بعدی سنجیده می‌شود.
                float need = a.Height;
                if (a.KeepWithNext && i + 1 < atoms.Count && atoms[i + 1].Height > 0)
                    need += atoms[i + 1].Height;

                bool startsPage = page.Count == 0;
                float extra = (startsPage && a.RepeatHeader != null) ? a.RepeatHeader.Height : 0f;

                if (!startsPage && used + need + extra > bodyHeight)
                {
                    _pages.Add(page);
                    page = new List<Atom>();
                    used = 0f;
                    startsPage = true;
                    extra = a.RepeatHeader != null ? a.RepeatHeader.Height : 0f;
                }

                if (startsPage && a.RepeatHeader != null && !a.IsRepeatHeader)
                {
                    page.Add(a.RepeatHeader);
                    used += a.RepeatHeader.Height;
                }

                page.Add(a);
                used += a.Height;
            }

            if (page.Count > 0) _pages.Add(page);
            if (_pages.Count == 0) _pages.Add(new List<Atom>());
        }

        // ═════════════════════════════════════════════════════════════════════
        // گام ۳ — کشیدنِ یک صفحه
        // ═════════════════════════════════════════════════════════════════════
        private void DrawPage(PrintPageEventArgs e, int pageIndex)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Margins m = e.PageSettings.Margins;
            Rectangle bounds = e.PageSettings.Bounds;
            var area = new RectangleF(m.Left, m.Top,
                                      bounds.Width - m.Left - m.Right,
                                      bounds.Height - m.Top - m.Bottom);

            List<Atom> page = _pages[pageIndex];

            if (page.Count == 1 && page[0].CoverOnly)
            {
                DrawCover(g, area);
                DrawFooter(g, area, pageIndex + 1, _pages.Count);
                return;
            }

            // با صفحهٔ جلد، «صفحهٔ اولِ محتوا» اندیسِ ۱ است — شناسنامهٔ کاملِ
            // سند باید همان‌جا بیاید، نه روی جلد.
            bool firstContentPage = ShowCoverPage ? pageIndex == 1 : pageIndex == 0;

            float y = area.Top;
            DrawLetterhead(g, area, ref y, firstContentPage);

            foreach (Atom a in page)
            {
                if (a.Draw != null)
                    a.Draw(g, new RectangleF(area.Left, y, area.Width, a.Height));
                y += a.Height;
            }

            DrawFooter(g, area, pageIndex + 1, _pages.Count);
        }

        // ─── سربرگ ───────────────────────────────────────────────────────────
        private float MeasureHeaderHeight()
        {
            float h = 46f;                                   // نوارِ نامِ مؤسسه
            h += 34f;                                        // عنوانِ سند
            if (!string.IsNullOrWhiteSpace(Subtitle)) h += 16f;
            if (HeaderFields.Count > 0)
                h += 12f + (float)Math.Ceiling(HeaderFields.Count / 3.0) * 18f + 10f;
            return h + 10f;
        }

        private const float FooterHeight = 34f;

        private void DrawLetterhead(Graphics g, RectangleF area, ref float y, bool firstPage)
        {
            string orgName = SettingsHelper.Get(SettingsHelper.OrgName);
            if (string.IsNullOrWhiteSpace(orgName))
                orgName = SettingsHelper.Get(SettingsHelper.OrgNameEn);

            var sub = new List<string>();
            AddIf(sub, SettingsHelper.Get(SettingsHelper.Address));
            AddIf(sub, SettingsHelper.Get(SettingsHelper.Phone));
            AddIf(sub, SettingsHelper.Get(SettingsHelper.Website));
            string subLine = string.Join("   ·   ", sub);

            float logoBox = 0f;
            if (SettingsHelper.GetInt(SettingsHelper.ShowLogoOnPrint, 0) == 1)
            {
                using (Image logo = TryLoadImage(SettingsHelper.Get(SettingsHelper.LogoPath)))
                {
                    if (logo != null)
                    {
                        logoBox = 44f;
                        float ratio = Math.Min(logoBox / logo.Width, logoBox / logo.Height);
                        float w = logo.Width * ratio, h = logo.Height * ratio;
                        g.DrawImage(logo, area.Right - w, y + (logoBox - h) / 2f, w, h);
                    }
                }
            }

            var nameRect = new RectangleF(area.Left, y, area.Width - logoBox - 8f, 26f);
            using (var br = new SolidBrush(Ink))
                g.DrawString(orgName ?? "", FnOrg, br, nameRect, SfRight);

            if (subLine.Length > 0)
            {
                using (var br = new SolidBrush(InkMuted))
                    g.DrawString(subLine, FnOrgSub, br,
                        new RectangleF(area.Left, y + 24f, area.Width - logoBox - 8f, 14f), SfRight);
            }

            // مرکزِ فعلی، سمتِ چپِ سربرگ — سندِ چاپ‌شده باید بگوید مالِ کدام شعبه است.
            using (var br = new SolidBrush(InkMuted))
                g.DrawString(SecurityContext.CenterDisplay ?? "", FnOrgSub, br,
                    new RectangleF(area.Left, y + 24f, area.Width * 0.4f, 14f), SfLeft);

            y += 44f;
            using (var pen = new Pen(Accent, 1.6f))
                g.DrawLine(pen, area.Left, y, area.Right, y);
            y += 10f;

            // عنوانِ سند
            using (var br = new SolidBrush(Ink))
                g.DrawString(Title ?? "", FnTitle, br,
                    new RectangleF(area.Left, y, area.Width, 26f), SfCenter);
            y += 26f;

            if (!string.IsNullOrWhiteSpace(Subtitle))
            {
                using (var br = new SolidBrush(InkMuted))
                    g.DrawString(Subtitle, FnSubtitle, br,
                        new RectangleF(area.Left, y, area.Width, 16f), SfCenter);
                y += 16f;
            }
            y += 8f;

            // شناسنامهٔ سند فقط در صفحهٔ اول کامل چاپ می‌شود؛ در صفحاتِ بعد
            // جایش خالی می‌ماند تا ارتفاعِ سربرگ همه‌جا یکسان بماند و
            // صفحه‌بندیِ محاسبه‌شده به‌هم نریزد.
            if (HeaderFields.Count > 0)
            {
                float rows = (float)Math.Ceiling(HeaderFields.Count / 3.0);
                var box = new RectangleF(area.Left, y, area.Width, rows * 18f + 12f);

                if (firstPage)
                {
                    using (var fill = new SolidBrush(Zebra))
                    using (var pen = new Pen(RuleSoft, 1f))
                    {
                        g.FillRectangle(fill, box);
                        g.DrawRectangle(pen, box.X, box.Y, box.Width, box.Height);
                    }

                    float cellW = area.Width / 3f;
                    for (int i = 0; i < HeaderFields.Count; i++)
                    {
                        int r = i / 3, c = i % 3;
                        var cell = new RectangleF(area.Right - (c + 1) * cellW,
                                                  box.Y + 6f + r * 18f, cellW - 6f, 18f);
                        DrawLabelValue(g, cell, HeaderFields[i].Key, HeaderFields[i].Value);
                    }
                }
                else
                {
                    // در صفحاتِ بعد جای همان جعبه رزرو می‌ماند (وگرنه ارتفاعِ
                    // سربرگ فرق می‌کرد و صفحه‌بندیِ محاسبه‌شده به‌هم می‌ریخت)،
                    // ولی به‌جای فضای خالی یک سطرِ «ادامهٔ سند» می‌نشیند.
                    var line = new List<string>();
                    for (int i = 0; i < HeaderFields.Count && line.Count < 3; i++)
                        if (!string.IsNullOrWhiteSpace(HeaderFields[i].Value))
                            line.Add(HeaderFields[i].Key + ": " + HeaderFields[i].Value);

                    using (var br = new SolidBrush(InkMuted))
                        g.DrawString(string.Join("   ·   ", line) + "   ·   ادامه", FnFooter, br,
                            new RectangleF(area.Left, box.Y, area.Width, 18f), SfRight);

                    using (var pen = new Pen(RuleSoft, 1f))
                        g.DrawLine(pen, area.Left, box.Y + 20f, area.Right, box.Y + 20f);
                }

                y += box.Height + 10f;
            }
        }

        private static void AddIf(List<string> list, string value)
        {
            if (!string.IsNullOrWhiteSpace(value)) list.Add(value.Trim());
        }

        private static void DrawLabelValue(Graphics g, RectangleF cell, string label, string value)
        {
            string text = (label ?? "") + ": ";
            SizeF labelSize;
            using (var br = new SolidBrush(InkMuted))
            {
                labelSize = g.MeasureString(text, FnLabel);
                g.DrawString(text, FnLabel, br, cell, SfRight);
            }
            var valueRect = new RectangleF(cell.X, cell.Y,
                                           Math.Max(10f, cell.Width - labelSize.Width), cell.Height);
            using (var br = new SolidBrush(Ink))
                g.DrawString(value ?? "", FnValue, br, valueRect, SfRight);
        }

        // ─── پاورقی ──────────────────────────────────────────────────────────
        private void DrawFooter(Graphics g, RectangleF area, int page, int total)
        {
            float y = area.Bottom - FooterHeight + 6f;
            using (var pen = new Pen(RuleSoft, 1f))
                g.DrawLine(pen, area.Left, y, area.Right, y);
            y += 4f;

            string right = "چاپ: " + PersianDateHelper.ToPersianDateTimeString(DateTime.Now) +
                           (string.IsNullOrWhiteSpace(SecurityContext.Username)
                                ? "" : "  ·  کاربر: " + SecurityContext.Username);
            string center = string.IsNullOrWhiteSpace(DocumentCode) ? "" : "شمارهٔ سند: " + DocumentCode;
            string left = "صفحهٔ " + Fa(page) + " از " + Fa(total);

            using (var br = new SolidBrush(InkMuted))
            {
                var rect = new RectangleF(area.Left, y, area.Width, 16f);
                g.DrawString(right, FnFooter, br, rect, SfRight);
                g.DrawString(center, FnFooter, br, rect, SfCenter);
                g.DrawString(left, FnFooter, br, rect, SfLeft);
            }

            // مهر و امضاء فقط پای صفحهٔ آخر — روی صفحات میانی وسطِ جدول
            // می‌افتاد و متن را می‌پوشاند.
            if (page == total) DrawStampAndSignature(g, area);
        }

        private static void DrawStampAndSignature(Graphics g, RectangleF area)
        {
            if (SettingsHelper.GetInt(SettingsHelper.ShowStamp, 0) == 1)
            {
                using (Image stamp = TryLoadImage(SettingsHelper.Get(SettingsHelper.StampPath)))
                    if (stamp != null)
                        g.DrawImage(stamp, area.Left, area.Bottom - FooterHeight - 62f, 62f, 62f);
            }

            if (SettingsHelper.GetInt(SettingsHelper.ShowSignature, 0) == 1)
            {
                using (Image sign = TryLoadImage(SettingsHelper.Get(SettingsHelper.SignaturePath)))
                    if (sign != null)
                        g.DrawImage(sign, area.Right - 110f, area.Bottom - FooterHeight - 48f, 110f, 48f);
            }
        }

        // ─── صفحهٔ جلد ───────────────────────────────────────────────────────
        private void DrawCover(Graphics g, RectangleF area)
        {
            float y = area.Top + 30f;

            using (Image logo = TryLoadImage(SettingsHelper.Get(SettingsHelper.LogoPath)))
            {
                if (logo != null)
                {
                    float box = 90f;
                    float ratio = Math.Min(box / logo.Width, box / logo.Height);
                    float w = logo.Width * ratio, h = logo.Height * ratio;
                    g.DrawImage(logo, area.Left + (area.Width - w) / 2f, y, w, h);
                    y += h + 18f;
                }
            }

            string orgName = SettingsHelper.Get(SettingsHelper.OrgName);
            if (string.IsNullOrWhiteSpace(orgName))
                orgName = SettingsHelper.Get(SettingsHelper.OrgNameEn);

            using (var br = new SolidBrush(Ink))
                g.DrawString(orgName ?? "", FnOrg, br,
                    new RectangleF(area.Left, y, area.Width, 26f), SfCenter);
            y += 40f;

            using (var pen = new Pen(Accent, 2f))
                g.DrawLine(pen, area.Left + area.Width * 0.25f, y,
                                area.Right - area.Width * 0.25f, y);
            y += 44f;

            using (var br = new SolidBrush(Accent))
                g.DrawString(string.IsNullOrWhiteSpace(CoverHeadline) ? Title : CoverHeadline,
                    FnCoverTitle, br, new RectangleF(area.Left, y, area.Width, 44f), SfCenter);
            y += 52f;

            if (!string.IsNullOrWhiteSpace(Subtitle))
            {
                using (var br = new SolidBrush(InkMuted))
                    g.DrawString(Subtitle, FnSubtitle, br,
                        new RectangleF(area.Left, y, area.Width, 20f), SfCenter);
                y += 30f;
            }

            y += 30f;
            float boxW = area.Width * 0.72f;
            float boxX = area.Left + (area.Width - boxW) / 2f;
            float boxH = CoverFields.Count * 26f + 20f;

            using (var fill = new SolidBrush(Zebra))
            using (var pen = new Pen(Rule, 1f))
            {
                g.FillRectangle(fill, boxX, y, boxW, boxH);
                g.DrawRectangle(pen, boxX, y, boxW, boxH);
            }

            float ry = y + 10f;
            foreach (var kv in CoverFields)
            {
                using (var br = new SolidBrush(InkMuted))
                    g.DrawString(kv.Key + ":", FnLabel, br,
                        new RectangleF(boxX + boxW - 150f, ry, 140f, 24f), SfRight);
                using (var br = new SolidBrush(Ink))
                    g.DrawString(kv.Value ?? "", FnValue, br,
                        new RectangleF(boxX + 10f, ry, boxW - 170f, 24f), SfRight);
                using (var pen = new Pen(RuleSoft, 1f))
                    g.DrawLine(pen, boxX + 10f, ry + 25f, boxX + boxW - 10f, ry + 25f);
                ry += 26f;
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // شکستنِ بلوک‌ها به اتم
        // ═════════════════════════════════════════════════════════════════════
        private void Flatten(Graphics g, ReportBlock block, List<Atom> outAtoms)
        {
            var pageBreak = block as ReportPageBreak;
            if (pageBreak != null) { outAtoms.Add(new Atom { Height = -1 }); return; }

            var spacer = block as ReportSpacer;
            if (spacer != null)
            {
                outAtoms.Add(new Atom { Height = spacer.Height, Draw = delegate { } });
                return;
            }

            var heading = block as ReportHeading;
            if (heading != null) { FlattenHeading(heading, outAtoms); return; }

            var para = block as ReportParagraph;
            if (para != null) { FlattenParagraph(g, para, outAtoms); return; }

            var callout = block as ReportCallout;
            if (callout != null) { FlattenCallout(g, callout, outAtoms); return; }

            var kv = block as ReportKeyValues;
            if (kv != null) { FlattenKeyValues(kv, outAtoms); return; }

            var table = block as ReportTable;
            if (table != null) { FlattenTable(g, table, outAtoms); return; }

            var images = block as ReportImageGrid;
            if (images != null) { FlattenImageGrid(g, images, outAtoms); return; }

            var sign = block as ReportSignatures;
            if (sign != null) { FlattenSignatures(sign, outAtoms); return; }
        }

        private void FlattenHeading(ReportHeading h, List<Atom> outAtoms)
        {
            if (h.PageBreakBefore) outAtoms.Add(new Atom { Height = -1 });

            string text = h.Text ?? "";
            string note = h.Note ?? "";

            outAtoms.Add(new Atom
            {
                Height = 30f,
                KeepWithNext = true,
                Draw = delegate (Graphics g, RectangleF r)
                {
                    // میلهٔ رنگیِ سمتِ راستِ عنوان — نشانهٔ شروعِ بخش.
                    using (var br = new SolidBrush(Accent))
                        g.FillRectangle(br, r.Right - 3f, r.Y + 4f, 3f, 18f);

                    using (var br = new SolidBrush(Ink))
                        g.DrawString(text, FnHeading, br,
                            new RectangleF(r.X, r.Y + 2f, r.Width - 10f, 22f), SfRight);

                    if (note.Length > 0)
                    {
                        using (var br = new SolidBrush(InkMuted))
                            g.DrawString(note, FnFooter, br,
                                new RectangleF(r.X, r.Y + 4f, r.Width - 10f, 20f), SfLeft);
                    }

                    using (var pen = new Pen(RuleSoft, 1f))
                        g.DrawLine(pen, r.X, r.Y + 26f, r.Right, r.Y + 26f);
                }
            });
        }

        private void FlattenParagraph(Graphics g, ReportParagraph p, List<Atom> outAtoms)
        {
            string text = p.Text ?? "";
            if (text.Length == 0) return;

            Color color = p.Muted ? InkMuted : Ink;
            foreach (string line in WrapLines(g, text, FnValue, _contentWidth))
            {
                string captured = line;
                outAtoms.Add(new Atom
                {
                    Height = 17f,
                    Draw = delegate (Graphics gr, RectangleF r)
                    {
                        using (var br = new SolidBrush(color))
                            gr.DrawString(captured, FnValue, br, r, SfRight);
                    }
                });
            }
            outAtoms.Add(new Atom { Height = 6f, Draw = delegate { } });
        }

        private void FlattenCallout(Graphics g, ReportCallout c, List<Atom> outAtoms)
        {
            string text = c.Text ?? "";
            if (text.Length == 0) return;

            List<string> lines = WrapLines(g, text, FnValue, _contentWidth - 24f);
            float height = lines.Count * 17f + 14f;
            Color accent = c.Accent;

            outAtoms.Add(new Atom
            {
                Height = height + 8f,
                Draw = delegate (Graphics gr, RectangleF r)
                {
                    var box = new RectangleF(r.X, r.Y, r.Width, height);
                    using (var fill = new SolidBrush(Color.FromArgb(22, accent)))
                        gr.FillRectangle(fill, box);
                    using (var br = new SolidBrush(accent))
                        gr.FillRectangle(br, box.Right - 3f, box.Y, 3f, box.Height);

                    float ly = box.Y + 7f;
                    foreach (string line in lines)
                    {
                        using (var br = new SolidBrush(Ink))
                            gr.DrawString(line, FnValue, br,
                                new RectangleF(box.X + 8f, ly, box.Width - 20f, 17f), SfRight);
                        ly += 17f;
                    }
                }
            });
        }

        private void FlattenKeyValues(ReportKeyValues kv, List<Atom> outAtoms)
        {
            int cols = Math.Max(1, kv.Columns);
            int rows = (int)Math.Ceiling(kv.Items.Count / (double)cols);
            float width = _contentWidth;

            for (int r = 0; r < rows; r++)
            {
                int rowStart = r * cols;
                int rowIndex = r;
                outAtoms.Add(new Atom
                {
                    Height = 22f,
                    Draw = delegate (Graphics g, RectangleF rect)
                    {
                        if (rowIndex % 2 == 1)
                            using (var fill = new SolidBrush(Zebra))
                                g.FillRectangle(fill, rect.X, rect.Y, rect.Width, 22f);

                        float cellW = width / cols;
                        for (int c = 0; c < cols; c++)
                        {
                            int idx = rowStart + c;
                            if (idx >= kv.Items.Count) break;
                            var cell = new RectangleF(rect.Right - (c + 1) * cellW + 4f,
                                                      rect.Y, cellW - 8f, 22f);
                            DrawLabelValue(g, cell, kv.Items[idx].Key, kv.Items[idx].Value);
                        }

                        using (var pen = new Pen(RuleSoft, 1f))
                            g.DrawLine(pen, rect.X, rect.Y + 22f, rect.Right, rect.Y + 22f);
                    }
                });
            }
            outAtoms.Add(new Atom { Height = 8f, Draw = delegate { } });
        }

        // ─── جدول ────────────────────────────────────────────────────────────
        private void FlattenTable(Graphics g, ReportTable t, List<Atom> outAtoms)
        {
            if (t.Data == null || t.Data.Rows.Count == 0)
            {
                outAtoms.Add(new Atom
                {
                    Height = 26f,
                    Draw = delegate (Graphics gr, RectangleF r)
                    {
                        using (var br = new SolidBrush(InkMuted))
                            gr.DrawString(t.EmptyText, FnValue, br, r, SfRight);
                    }
                });
                outAtoms.Add(new Atom { Height = 8f, Draw = delegate { } });
                return;
            }

            var columns = new List<ReportDocColumn>(t.Columns);
            if (columns.Count == 0)
            {
                foreach (DataColumn c in t.Data.Columns)
                    columns.Add(ReportDocColumn.Of(c.ColumnName, c.ColumnName, 1f));
            }

            // ستونِ ردیف در راست‌ترین جای جدول.
            float numberWidth = t.ShowRowNumbers ? 30f : 0f;
            float tableWidth = _contentWidth;
            float usable = tableWidth - numberWidth;

            float totalWeight = 0f;
            foreach (ReportDocColumn c in columns) totalWeight += Math.Max(0.2f, c.Weight);

            var widths = new float[columns.Count];
            for (int i = 0; i < columns.Count; i++)
                widths[i] = usable * Math.Max(0.2f, columns[i].Weight) / totalWeight;

            // سرستون — همان اتمی که در هر صفحهٔ تازه تکرار می‌شود.
            var header = new Atom
            {
                Height = 26f,
                IsRepeatHeader = true,
                Draw = delegate (Graphics gr, RectangleF r)
                {
                    using (var fill = new SolidBrush(HeadBand))
                        gr.FillRectangle(fill, r.X, r.Y, tableWidth, 26f);

                    float x = r.Right;
                    if (numberWidth > 0)
                    {
                        gr.DrawString("#", FnTableHead, Brushes.White,
                            new RectangleF(x - numberWidth, r.Y, numberWidth, 26f), SfCenter);
                        x -= numberWidth;
                    }

                    for (int i = 0; i < columns.Count; i++)
                    {
                        x -= widths[i];
                        gr.DrawString(columns[i].Header ?? "", FnTableHead, Brushes.White,
                            new RectangleF(x + 3f, r.Y, widths[i] - 6f, 26f), SfCenter);
                        if (i < columns.Count - 1)
                            using (var pen = new Pen(Color.FromArgb(70, Color.White), 1f))
                                gr.DrawLine(pen, x, r.Y + 5f, x, r.Y + 21f);
                    }
                }
            };
            outAtoms.Add(header);

            // سطرها — ارتفاعِ هر سطر از بلندترین سلولِ شکسته‌شده می‌آید.
            for (int rowIdx = 0; rowIdx < t.Data.Rows.Count; rowIdx++)
            {
                DataRow row = t.Data.Rows[rowIdx];
                var cellLines = new List<string>[columns.Count];
                int maxLines = 1;

                for (int i = 0; i < columns.Count; i++)
                {
                    string text = CellText(row, columns[i].Field);
                    cellLines[i] = WrapLines(g, text, FnTableCell, widths[i] - 10f);
                    if (cellLines[i].Count > maxLines) maxLines = cellLines[i].Count;
                }

                float rowHeight = Math.Max(26f, maxLines * 16f + 10f);
                int captureIndex = rowIdx;
                var captureLines = cellLines;

                outAtoms.Add(new Atom
                {
                    Height = rowHeight,
                    RepeatHeader = header,
                    Draw = delegate (Graphics gr, RectangleF r)
                    {
                        if (captureIndex % 2 == 1)
                            using (var fill = new SolidBrush(Zebra))
                                gr.FillRectangle(fill, r.X, r.Y, tableWidth, r.Height);

                        using (var pen = new Pen(RuleSoft, 1f))
                            gr.DrawRectangle(pen, r.X, r.Y, tableWidth, r.Height);

                        float x = r.Right;
                        if (numberWidth > 0)
                        {
                            using (var br = new SolidBrush(InkMuted))
                                gr.DrawString(Fa(captureIndex + 1), FnTableCell, br,
                                    new RectangleF(x - numberWidth, r.Y, numberWidth, r.Height), SfCenter);
                            x -= numberWidth;
                            using (var pen = new Pen(RuleSoft, 1f))
                                gr.DrawLine(pen, x, r.Y, x, r.Bottom);
                        }

                        for (int i = 0; i < columns.Count; i++)
                        {
                            x -= widths[i];
                            StringFormat sf = Wrapped(
                                columns[i].Align == ReportAlign.Center ? StringAlignment.Center :
                                columns[i].Align == ReportAlign.Left ? StringAlignment.Far :
                                StringAlignment.Near);

                            float ty = r.Y + Math.Max(5f, (r.Height - captureLines[i].Count * 16f) / 2f);
                            using (var br = new SolidBrush(Ink))
                            {
                                foreach (string line in captureLines[i])
                                {
                                    gr.DrawString(line, FnTableCell, br,
                                        new RectangleF(x + 5f, ty, widths[i] - 10f, 16f), sf);
                                    ty += 16f;
                                }
                            }

                            if (i < columns.Count - 1)
                                using (var pen = new Pen(RuleSoft, 1f))
                                    gr.DrawLine(pen, x, r.Y, x, r.Bottom);
                        }
                    }
                });
            }

            outAtoms.Add(new Atom { Height = 10f, Draw = delegate { } });
        }

        private void FlattenImageGrid(Graphics g, ReportImageGrid grid, List<Atom> outAtoms)
        {
            if (grid.Items == null || grid.Items.Count == 0)
            {
                outAtoms.Add(new Atom
                {
                    Height = 24f,
                    Draw = delegate (Graphics gr, RectangleF r)
                    {
                        using (var br = new SolidBrush(InkMuted))
                            gr.DrawString(grid.EmptyText, FnValue, br, r, SfRight);
                    }
                });
                outAtoms.Add(new Atom { Height = 8f, Draw = delegate { } });
                return;
            }

            int columns = Math.Max(1, grid.Columns);
            float cellWidth = _contentWidth / columns;
            float captionHeight = 30f;
            float rowHeight = grid.CellHeight + captionHeight + 10f;
            int rows = (int)Math.Ceiling(grid.Items.Count / (double)columns);

            for (int row = 0; row < rows; row++)
            {
                int first = row * columns;
                List<ReportImageItem> line = new List<ReportImageItem>();
                for (int i = first; i < Math.Min(first + columns, grid.Items.Count); i++)
                    line.Add(grid.Items[i]);

                var captions = new List<List<string>>();
                foreach (ReportImageItem item in line)
                    captions.Add(WrapLines(g, item.Caption ?? "", FnFooter, cellWidth - 16f));

                float imageHeight = grid.CellHeight;
                List<ReportImageItem> capturedLine = line;
                List<List<string>> capturedCaptions = captions;

                outAtoms.Add(new Atom
                {
                    Height = rowHeight,
                    Draw = delegate (Graphics gr, RectangleF r)
                    {
                        float x = r.Right;
                        for (int i = 0; i < capturedLine.Count; i++)
                        {
                            x -= cellWidth;
                            var cell = new RectangleF(x + 6f, r.Y, cellWidth - 12f, imageHeight);

                            using (var fill = new SolidBrush(Zebra))
                                gr.FillRectangle(fill, cell);
                            using (var pen = new Pen(Rule, 1f))
                                gr.DrawRectangle(pen, cell.X, cell.Y, cell.Width, cell.Height);

                            DrawFitted(gr, capturedLine[i].Path, cell);

                            float cy = r.Y + imageHeight + 4f;
                            foreach (string capLine in capturedCaptions[i])
                            {
                                if (cy > r.Y + imageHeight + captionHeight) break;
                                using (var br = new SolidBrush(InkMuted))
                                    gr.DrawString(capLine, FnFooter, br,
                                        new RectangleF(cell.X, cy, cell.Width, 13f), SfCenter);
                                cy += 13f;
                            }
                        }
                    }
                });
            }

            outAtoms.Add(new Atom { Height = 10f, Draw = delegate { } });
        }

        // عکس با حفظِ نسبت داخلِ کادر. فایل با FileShare.ReadWrite خوانده
        // می‌شود تا هیچ‌وقت روی دیسک قفل نشود.
        private static void DrawFitted(Graphics g, string path, RectangleF box)
        {
            bool drawn = false;
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var img = Image.FromStream(fs, false, true))
                    {
                        float ratio = Math.Min((box.Width - 6f) / img.Width, (box.Height - 6f) / img.Height);
                        float w = img.Width * ratio, h = img.Height * ratio;
                        g.DrawImage(img, box.X + (box.Width - w) / 2f, box.Y + (box.Height - h) / 2f, w, h);
                        drawn = true;
                    }
                }
            }
            catch { }

            if (drawn) return;

            using (var br = new SolidBrush(InkMuted))
                g.DrawString("فایل عکس پیدا نشد", FnFooter, br, box, SfCenter);
        }

        private void FlattenSignatures(ReportSignatures s, List<Atom> outAtoms)
        {
            string[] roles = s.Roles ?? new string[0];
            if (roles.Length == 0) return;

            float width = _contentWidth;
            outAtoms.Add(new Atom
            {
                Height = 76f,
                Draw = delegate (Graphics g, RectangleF r)
                {
                    float boxW = width / roles.Length;
                    for (int i = 0; i < roles.Length; i++)
                    {
                        var box = new RectangleF(r.Right - (i + 1) * boxW + 6f, r.Y + 8f, boxW - 12f, 60f);
                        using (var pen = new Pen(Rule, 1f) { DashStyle = DashStyle.Dot })
                            g.DrawRectangle(pen, box.X, box.Y, box.Width, box.Height);

                        using (var br = new SolidBrush(InkMuted))
                            g.DrawString(roles[i], FnLabel, br,
                                new RectangleF(box.X, box.Y + 4f, box.Width, 16f), SfCenter);

                        using (var br = new SolidBrush(InkMuted))
                            g.DrawString("نام و امضاء", FnFooter, br,
                                new RectangleF(box.X, box.Bottom - 18f, box.Width, 14f), SfCenter);
                    }
                }
            });
        }

        // ═════════════════════════════════════════════════════════════════════
        // کمکی‌ها
        // ═════════════════════════════════════════════════════════════════════
        private static string CellText(DataRow row, string field)
        {
            if (row == null || string.IsNullOrEmpty(field)) return "";
            if (!row.Table.Columns.Contains(field)) return "";
            object v = row[field];
            if (v == null || v == DBNull.Value) return "";
            if (v is DateTime) return PersianDateHelper.ToPersianDateString((DateTime)v);
            if (v is bool) return ((bool)v) ? "بلی" : "خیر";
            if (v is decimal || v is double || v is float)
                return Fa(Convert.ToDecimal(v, CultureInfo.InvariantCulture)
                                 .ToString("#,0.##", CultureInfo.InvariantCulture));
            if (v is int || v is long || v is short)
                return Fa(Convert.ToInt64(v, CultureInfo.InvariantCulture)
                                 .ToString("#,0", CultureInfo.InvariantCulture));
            return Convert.ToString(v);
        }

        // شکستنِ متن به سطرهایی که در عرضِ داده‌شده جا می‌شوند. MeasureString
        // با StringFormat پیش‌فرض اندازه می‌گیرد؛ برای شکستِ سطر همین کافی
        // است چون جهتِ متن روی *عرضِ* رشته اثری ندارد.
        private static List<string> WrapLines(Graphics g, string text, Font font, float maxWidth)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(text)) { result.Add(""); return result; }
            if (maxWidth < 20f) maxWidth = 20f;

            foreach (string rawLine in text.Replace("\r\n", "\n").Split('\n'))
            {
                if (rawLine.Length == 0) { result.Add(""); continue; }

                string[] words = rawLine.Split(' ');
                string current = "";

                foreach (string word in words)
                {
                    string candidate = current.Length == 0 ? word : current + " " + word;
                    if (g.MeasureString(candidate, font).Width <= maxWidth)
                    {
                        current = candidate;
                        continue;
                    }

                    if (current.Length > 0) { result.Add(current); current = word; }
                    else
                    {
                        // یک کلمهٔ بلندتر از عرضِ ستون — نویسه‌به‌نویسه شکسته می‌شود.
                        string chunk = "";
                        foreach (char ch in word)
                        {
                            if (g.MeasureString(chunk + ch, font).Width > maxWidth && chunk.Length > 0)
                            {
                                result.Add(chunk);
                                chunk = "";
                            }
                            chunk += ch;
                        }
                        current = chunk;
                    }
                }

                result.Add(current);
                if (result.Count > 400) break;   // مهارِ متنِ غیرعادی
            }

            if (result.Count == 0) result.Add("");
            return result;
        }

        // رقم‌های فارسی — سندِ رسمیِ فارسی با رقمِ لاتین ناهماهنگ دیده می‌شود.
        public static string Fa(int value) { return Fa(value.ToString(CultureInfo.InvariantCulture)); }

        public static string Fa(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            char[] chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (chars[i] >= '0' && chars[i] <= '9')
                    chars[i] = (char)('۰' + (chars[i] - '0'));
            return new string(chars);
        }

        private static Image TryLoadImage(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var img = Image.FromStream(fs))
                    return new Bitmap(img);
            }
            catch { return null; }
        }
    }
}
