using CaseManagement.DAL;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace CaseManagement.Helpers
{
    // ═════════════════════════════════════════════════════════════════════════
    // دیالوگِ «خروجی پروندهٔ کامل».
    //
    // آموزش — چه چیزی عوض شد: قبلاً یک کرکرهٔ دوگزینه‌ای بود («اکسل» یا «چاپ»)
    // و هر دو گزینه همهٔ سیزده بخش را بی‌چون‌وچرا می‌ریخت بیرون — چاپ هم
    // سیزده پیش‌نمایشِ جدا باز می‌کرد. حالا کاربر می‌بیند هر بخش چند ردیف
    // دارد، انتخاب می‌کند کدام‌ها را می‌خواهد، و خروجی یک سندِ پیوسته است.
    //
    // این فرم هیچ داده‌ای نمی‌خواند: همه‌چیز از CaseFileReport می‌آید.
    // ═════════════════════════════════════════════════════════════════════════
    public class FrmCaseFileExport : Form
    {
        private readonly DatabaseHelper _db;
        private readonly int _caseId;
        private readonly string _caseCode;

        private CheckedListBox lstSections;
        private RadioButton rdoPreview, rdoPdf, rdoExcel, rdoPrint;
        private CheckBox chkCover, chkLandscape, chkEmpty, chkOpenAfter;
        private Label lblCaseLine, lblHint;
        private Button btnAll, btnNone, btnExport, btnClose;

        // آموزش — چیدمانِ راست‌به‌چپ: وقتی RightToLeft = Yes باشد، WinForms
        // مقدارِ ContentAlignment را *آینه* می‌کند. پس برای اینکه متنِ یک
        // Label واقعاً سمتِ راست بنشیند باید MiddleLeft داده شود، نه
        // MiddleRight (با MiddleRight متن به چپ می‌رود — در وارسیِ تصویری
        // دیده شد). همین قاعده برای TopRight/BottomRight هم هست.
        private sealed class SectionItem
        {
            public string Title;
            public int Rows;
            public override string ToString()
            {
                return Rows == 0
                    ? Title + "   (خالی)"
                    : Title + "   (" + ReportDoc.Fa(Rows) + " ردیف)";
            }
        }

        public FrmCaseFileExport(DatabaseHelper db, int caseId, string caseCode)
        {
            _db = db ?? new DatabaseHelper();
            _caseId = caseId;
            _caseCode = caseCode ?? "";
            BuildUi();
        }

        // ═════════════════════════════════════════════════════════════════════
        private void BuildUi()
        {
            Text = "خروجی پروندهٔ کامل";
            RightToLeft = RightToLeft.Yes;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(820, 560);
            BackColor = UiTheme.Background;
            Font = UiTheme.Font(UiTheme.SizeBody);

            // ── سربرگ ────────────────────────────────────────────────────────
            var head = new Panel { Dock = DockStyle.Top, Height = 62, BackColor = UiTheme.PrimaryDark };

            var lblTitle = new Label
            {
                Text = "خروجی پروندهٔ کامل",
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Font = UiTheme.FontBold(UiTheme.SizeLarge),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoSize = false
            };
            lblTitle.SetBounds(ClientSize.Width - 516, 8, 500, 26);

            lblCaseLine = new Label
            {
                ForeColor = Color.FromArgb(200, 220, 240),
                BackColor = Color.Transparent,
                Font = UiTheme.Font(UiTheme.SizeSmall),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoSize = false
            };
            lblCaseLine.SetBounds(ClientSize.Width - 796, 34, 780, 20);

            head.Controls.Add(lblTitle);
            head.Controls.Add(lblCaseLine);

            // ── نوارِ پایین ──────────────────────────────────────────────────
            var bar = new Panel { Dock = DockStyle.Bottom, Height = 56, BackColor = UiTheme.CardBack };

            btnExport = UiTheme.CreateButton("ساخت خروجی", "➤", UiTheme.Primary);
            btnExport.SetBounds(16, 11, 160, 34);

            btnClose = UiTheme.CreateSecondaryButton("بستن", "✕");
            btnClose.SetBounds(184, 11, 100, 34);

            lblHint = new Label
            {
                AutoSize = false,
                Font = UiTheme.Font(UiTheme.SizeSmall),
                ForeColor = UiTheme.TextMuted,
                TextAlign = ContentAlignment.MiddleLeft
            };
            lblHint.SetBounds(300, 11, ClientSize.Width - 316, 34);

            bar.Controls.Add(btnExport);
            bar.Controls.Add(btnClose);
            bar.Controls.Add(lblHint);

            btnExport.Click += delegate { RunExport(); };
            btnClose.Click += delegate { Close(); };
            CancelButton = btnClose;

            // ── بخش‌ها (سمتِ راست) ───────────────────────────────────────────
            var sectionsHost = new Panel
            {
                Dock = DockStyle.Right,
                Width = 420,
                Padding = new Padding(14, 12, 8, 12),
                BackColor = UiTheme.Background
            };

            var lblSections = new Label
            {
                Dock = DockStyle.Top,
                Height = 24,
                Text = "کدام بخش‌ها در خروجی بیایند؟",
                Font = UiTheme.FontBold(UiTheme.SizeSmall),
                ForeColor = UiTheme.Primary,
                TextAlign = ContentAlignment.MiddleLeft
            };

            lstSections = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                CheckOnClick = true,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                IntegralHeight = false,
                Font = UiTheme.Font(UiTheme.SizeSmall)
            };

            var selectBar = new Panel { Dock = DockStyle.Bottom, Height = 40, BackColor = UiTheme.Background };

            // آموزش — مختصاتِ دستی اینجا کار نمی‌کند: عرضِ واقعیِ selectBar در
            // زمانِ ساخت هنوز معلوم نیست و بعداً که Dock عرض را عوض می‌کند،
            // کنترلِ Anchor-شده به بیرونِ پنل پرتاب می‌شود (دو دکمه ناپدید
            // شدند). یک FlowLayoutPanel چسبیده به راست، همیشه درست می‌چیند.
            var selectFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = UiTheme.Background,
                Padding = new Padding(0, 6, 0, 0)
            };

            btnAll = UiTheme.CreateSecondaryButton("انتخاب همه", "");
            btnAll.Size = new Size(110, 28);
            btnAll.Margin = new Padding(8, 0, 0, 0);
            btnNone = UiTheme.CreateSecondaryButton("هیچ‌کدام", "");
            btnNone.Size = new Size(100, 28);
            btnNone.Margin = new Padding(0, 0, 0, 0);

            selectFlow.Controls.Add(btnAll);
            selectFlow.Controls.Add(btnNone);
            selectBar.Controls.Add(selectFlow);

            btnAll.Click += delegate { SetAll(true); };
            btnNone.Click += delegate { SetAll(false); };

            sectionsHost.Controls.Add(lstSections);
            sectionsHost.Controls.Add(selectBar);
            sectionsHost.Controls.Add(lblSections);

            // ── گزینه‌ها (فضای باقیمانده) ────────────────────────────────────
            var optionsHost = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16, 12, 8, 12),
                BackColor = UiTheme.Background
            };

            // عرضِ ستونِ گزینه‌ها را نمی‌توان از ClientSize خواند: در زمانِ ساختِ
            // کنترل‌ها هنوز Dock اجرا نشده و مقدارش پیش‌فرضِ پنل است. از عرضِ
            // شناخته‌شدهٔ فرم حساب می‌شود.
            int optWidth = ClientSize.Width - sectionsHost.Width
                           - optionsHost.Padding.Horizontal;

            int y = 4;
            optionsHost.Controls.Add(Caption("نوع خروجی", ref y, optWidth));

            rdoPreview = Radio("پیش‌نمایش چاپ  (یک سند پیوسته)", ref y, optWidth);
            rdoPdf = Radio("ذخیرهٔ PDF", ref y, optWidth);
            rdoExcel = Radio("فایل اکسل  (هر بخش یک شیت)", ref y, optWidth);
            rdoPrint = Radio("چاپ مستقیم روی پرینتر پیش‌فرض", ref y, optWidth);
            rdoPreview.Checked = true;

            foreach (RadioButton r in new[] { rdoPreview, rdoPdf, rdoExcel, rdoPrint })
            {
                optionsHost.Controls.Add(r);
                r.CheckedChanged += delegate { SyncOptionState(); };
            }

            y += 10;
            optionsHost.Controls.Add(Caption("تنظیمات سند", ref y, optWidth));

            chkCover = Check("صفحهٔ جلد داشته باشد", true, ref y, optWidth);
            chkLandscape = Check("صفحهٔ افقی  (برای جدول‌های عریض)", true, ref y, optWidth);
            chkEmpty = Check("بخش‌های خالی هم چاپ شوند", false, ref y, optWidth);
            chkOpenAfter = Check("پس از ساخت، فایل باز شود", true, ref y, optWidth);

            foreach (CheckBox c in new[] { chkCover, chkLandscape, chkEmpty, chkOpenAfter })
                optionsHost.Controls.Add(c);

            Controls.Add(optionsHost);
            Controls.Add(sectionsHost);
            Controls.Add(bar);
            Controls.Add(head);
        }

        private Label Caption(string text, ref int y, int width)
        {
            var lbl = new Label
            {
                AutoSize = false,
                Text = text,
                Font = UiTheme.FontBold(UiTheme.SizeSmall),
                ForeColor = UiTheme.Primary,
                TextAlign = ContentAlignment.MiddleLeft
            };
            lbl.SetBounds(0, y, width, 24);
            y += 28;
            return lbl;
        }

        // آموزش — CheckAlign/TextAlign اینجا *تنظیم نمی‌شوند*: روی CheckBox و
        // RadioButton، خودِ RightToLeft.Yes مقدارِ پیش‌فرض را آینه می‌کند و
        // مربع را سمتِ راست می‌برد. تنظیمِ صریحِ MiddleRight دوباره آینه
        // می‌شد و مربع به چپ برمی‌گشت (همان چیزی که در وارسیِ تصویری دیده شد).
        private RadioButton Radio(string text, ref int y, int width)
        {
            var r = new RadioButton
            {
                Text = text,
                AutoSize = false,
                RightToLeft = RightToLeft.Yes,
                Font = UiTheme.Font(UiTheme.SizeSmall),
                ForeColor = UiTheme.TextDark,
                BackColor = Color.Transparent
            };
            r.SetBounds(0, y, width, 26);
            y += 28;
            return r;
        }

        private CheckBox Check(string text, bool value, ref int y, int width)
        {
            var c = new CheckBox
            {
                Text = text,
                Checked = value,
                AutoSize = false,
                RightToLeft = RightToLeft.Yes,
                Font = UiTheme.Font(UiTheme.SizeSmall),
                ForeColor = UiTheme.TextDark,
                BackColor = Color.Transparent
            };
            c.SetBounds(0, y, width, 26);
            y += 28;
            return c;
        }

        // ═════════════════════════════════════════════════════════════════════
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            lblCaseLine.Text = "کد پرونده: " + _caseCode + "   ·   " + (SecurityContext.CenterDisplay ?? "");

            Cursor previous = Cursor;
            Cursor = Cursors.WaitCursor;
            try
            {
                Dictionary<string, int> counts = CaseFileReport.RowCounts(_caseId);
                lstSections.BeginUpdate();
                lstSections.Items.Clear();

                foreach (CaseFileSection s in CaseFileReport.Sections())
                {
                    int rows;
                    if (!counts.TryGetValue(s.Title, out rows)) rows = 0;

                    // بخشِ خالی پیش‌فرض تیک نمی‌خورد — کاربر هنوز می‌تواند
                    // دستی انتخابش کند، ولی خروجیِ پیش‌فرض صفحهٔ خالی ندارد.
                    // «خلاصه پرونده» همیشه تیک است چون هویتِ سند است.
                    bool check = rows > 0 || s.Title == "خلاصه پرونده";
                    lstSections.Items.Add(new SectionItem { Title = s.Title, Rows = rows }, check);
                }
                lstSections.EndUpdate();
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در خواندن بخش‌های پرونده: " + ex.Message);
            }
            finally { Cursor = previous; }

            SyncOptionState();
        }

        private void SetAll(bool value)
        {
            for (int i = 0; i < lstSections.Items.Count; i++)
                lstSections.SetItemChecked(i, value);
        }

        private void SyncOptionState()
        {
            bool excel = rdoExcel.Checked;
            chkCover.Enabled = !excel;
            chkLandscape.Enabled = !excel;
            chkOpenAfter.Enabled = excel || rdoPdf.Checked;

            if (rdoPdf.Checked && !ReportDoc.IsPdfPrinterAvailable())
                lblHint.Text = "«Microsoft Print to PDF» روی این سیستم نصب نیست؛ به‌جای آن پیش‌نمایش چاپ را بگیرید.";
            else if (rdoPrint.Checked)
                lblHint.Text = "سند بدون پیش‌نمایش مستقیم به پرینتر پیش‌فرض فرستاده می‌شود.";
            else
                lblHint.Text = "";
        }

        private List<string> SelectedTitles()
        {
            var list = new List<string>();
            foreach (object item in lstSections.CheckedItems)
            {
                var si = item as SectionItem;
                if (si != null) list.Add(si.Title);
            }
            return list;
        }

        // ═════════════════════════════════════════════════════════════════════
        private void RunExport()
        {
            List<string> selected = SelectedTitles();
            if (selected.Count == 0)
            {
                UiTheme.ShowWarning(this, "حداقل یک بخش را انتخاب کنید.");
                return;
            }

            try
            {
                if (rdoExcel.Checked) { ExportExcel(selected); return; }

                Cursor previous = Cursor;
                Cursor = Cursors.WaitCursor;
                ReportDoc report;
                try
                {
                    report = CaseFileReport.Build(_db, _caseId, _caseCode, selected,
                        chkCover.Checked, chkLandscape.Checked, !chkEmpty.Checked);
                }
                finally { Cursor = previous; }

                if (rdoPreview.Checked) { report.Preview(this); return; }
                if (rdoPrint.Checked) { report.PrintDirect(); UiTheme.ShowSuccess(this, "سند به پرینتر فرستاده شد."); return; }

                ExportPdf(report);
            }
            catch (Exception ex)
            {
                UiTheme.ShowError(this, "خطا در ساخت خروجی پروندهٔ کامل: " + ex.Message);
            }
        }

        private void ExportPdf(ReportDoc report)
        {
            if (!ReportDoc.IsPdfPrinterAvailable())
            {
                UiTheme.ShowError(this,
                    "برای ساخت PDF، چاپگرِ «Microsoft Print to PDF» ویندوز لازم است و روی این سیستم پیدا نشد." +
                    Environment.NewLine + "فعلاً «پیش‌نمایش چاپ» را بگیرید و از آنجا روی PDF چاپ کنید.");
                return;
            }

            string path = AskPath("PDF|*.pdf", ".pdf");
            if (path == null) return;

            Cursor previous = Cursor;
            Cursor = Cursors.WaitCursor;
            bool ok;
            try { ok = report.SaveAsPdf(path); }
            finally { Cursor = previous; }

            if (!ok)
            {
                UiTheme.ShowError(this, "ساخت PDF کامل نشد.");
                return;
            }

            Finish(path);
        }

        private void ExportExcel(List<string> selected)
        {
            string path = AskPath("فایل اکسل|*.xlsx", ".xlsx");
            if (path == null) return;

            Cursor previous = Cursor;
            Cursor = Cursors.WaitCursor;
            try { CaseFileReport.ExportExcel(_caseId, selected, path); }
            finally { Cursor = previous; }

            Finish(path);
        }

        // مسیرِ پیش‌فرض: پوشهٔ CaseFiles کنارِ بقیهٔ فایل‌های برنامه — همان
        // جایی که نسخهٔ قبلی هم می‌نوشت، ولی حالا کاربر می‌تواند عوضش کند.
        private string AskPath(string filter, string extension)
        {
            string suggested = "CaseFile_" + FileHelper.CleanName(_caseCode) + "_" +
                DateTime.Now.ToString("yyyyMMdd_HHmm",
                    System.Globalization.CultureInfo.InvariantCulture) + extension;

            string initial = "";
            try
            {
                string root = FileHelper.GetOrChooseBaseRootFolder();
                if (!string.IsNullOrWhiteSpace(root))
                {
                    initial = Path.Combine(root, "CaseFiles");
                    Directory.CreateDirectory(initial);
                }
            }
            catch { initial = ""; }

            using (var sfd = new SaveFileDialog { Filter = filter, FileName = suggested })
            {
                if (!string.IsNullOrWhiteSpace(initial) && Directory.Exists(initial))
                    sfd.InitialDirectory = initial;

                return sfd.ShowDialog(this) == DialogResult.OK ? sfd.FileName : null;
            }
        }

        private void Finish(string path)
        {
            if (chkOpenAfter.Checked)
            {
                try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
                catch { }
            }

            UiTheme.ShowSuccess(this, "خروجی ساخته شد:" + Environment.NewLine + path);
        }
    }
}
