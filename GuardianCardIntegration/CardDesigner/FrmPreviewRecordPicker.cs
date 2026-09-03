using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using CaseManagement.Helpers;

namespace CaseManagement.GuardianCardIntegration.CardDesigner
{
    // ─────────────────────────────────────────────────────────────────────────
    // انتخابِ «پروندهٔ پیش‌نمایش» برای طراحِ کارت.
    //
    // قبلاً طراح خودش یک پروندهٔ دلخواه (آخرین CasID) برمی‌داشت و کاربر هیچ
    // کنترلی نداشت — یعنی نمی‌شد کارتِ همان مددجویی را دید که قرار است چاپ
    // شود. این دیالوگ همان انتخاب را صریح می‌کند.
    //
    // نکتهٔ مهمِ امنیتی: فهرست از CaseCardRepository.PreviewBatch می‌آید —
    // همان کوئریِ موجودی که فیلترِ مرکز (SecurityContext.CenterFilterId) را
    // از قبل اعمال می‌کند. اینجا هیچ کوئریِ تازه‌ای نوشته نشده تا محدودهٔ
    // دسترسیِ کاربر دور زده نشود.
    // ─────────────────────────────────────────────────────────────────────────
    public class FrmPreviewRecordPicker : Form
    {
        private readonly CaseCardRepository _repo = new CaseCardRepository();
        private TextBox _txtSearch;
        private ListBox _lst;
        private Label _lblCount;
        private List<GuardianCardBatchPreviewRow> _rows = new List<GuardianCardBatchPreviewRow>();

        // ۰ = «دادهٔ نمونه» انتخاب شد (نه یک پروندهٔ واقعی).
        public int SelectedCaseId { get; private set; }
        public bool UseDemoData { get; private set; }

        public FrmPreviewRecordPicker(int currentCaseId)
        {
            Text = "انتخاب پروندهٔ پیش‌نمایش";
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor = UiTheme.Background;
            Font = UiTheme.Font(UiTheme.SizeBody);
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(560, 520);
            MinimumSize = new Size(460, 400);
            ShowInTaskbar = false;
            MaximizeBox = false;
            MinimizeBox = false;

            SelectedCaseId = currentCaseId;

            Panel body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };

            _lst = new ListBox
            {
                Dock = DockStyle.Fill,
                RightToLeft = RightToLeft.Yes,
                BorderStyle = BorderStyle.FixedSingle,
                Font = UiTheme.Font(10F),
                IntegralHeight = false,
                ItemHeight = 24
            };
            _lst.DoubleClick += delegate { AcceptSelection(); };

            _lblCount = new Label
            {
                Dock = DockStyle.Bottom, Height = 24,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = UiTheme.TextMuted, Font = UiTheme.Font(9F)
            };

            _txtSearch = new TextBox { Dock = DockStyle.Top, RightToLeft = RightToLeft.Yes };
            UiTheme.StyleTextBox(_txtSearch);
            UiTheme.SetTip(_txtSearch, "جستجو بر اساس کد پرونده یا نام سرپرست…");
            _txtSearch.TextChanged += delegate { LoadRows(); };

            Panel searchWrap = new Panel { Dock = DockStyle.Top, Height = 38, Padding = new Padding(0, 0, 0, 8) };
            searchWrap.Controls.Add(_txtSearch);

            body.Controls.Add(_lst);
            body.Controls.Add(_lblCount);
            body.Controls.Add(searchWrap);

            // ── نوارِ دکمه‌ها ────────────────────────────────────────────────
            Panel buttons = new Panel { Dock = DockStyle.Bottom, Height = 52, Padding = new Padding(12, 8, 12, 12) };

            Button btnOk = UiTheme.CreateButton("انتخاب", "✓", UiTheme.Success);
            btnOk.Dock = DockStyle.Right; btnOk.Width = 110;
            btnOk.Click += delegate { AcceptSelection(); };

            Button btnCancel = UiTheme.CreateSecondaryButton("انصراف", "");
            btnCancel.Dock = DockStyle.Right; btnCancel.Width = 100;
            btnCancel.Margin = new Padding(8, 0, 0, 0);
            btnCancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };

            Button btnDemo = UiTheme.CreateSecondaryButton("دادهٔ نمونه", "");
            btnDemo.Dock = DockStyle.Left; btnDemo.Width = 130;
            UiTheme.SetTip(btnDemo, "پیش‌نمایش با دادهٔ ساختگیِ داخلی، بدون نیاز به پرونده");
            btnDemo.Click += delegate
            {
                UseDemoData = true;
                SelectedCaseId = 0;
                DialogResult = DialogResult.OK;
                Close();
            };

            buttons.Controls.Add(btnOk);
            buttons.Controls.Add(btnCancel);
            buttons.Controls.Add(btnDemo);

            Controls.Add(body);
            Controls.Add(buttons);

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            Load += delegate { LoadRows(); };
        }

        private void LoadRows()
        {
            try
            {
                var filter = new GuardianCardBatchFilter { SearchText = (_txtSearch.Text ?? "").Trim() };
                bool truncated;
                _rows = _repo.PreviewBatch(filter, out truncated);

                _lst.Items.Clear();
                foreach (GuardianCardBatchPreviewRow r in _rows)
                {
                    string name = string.IsNullOrWhiteSpace(r.GuardianName) ? "(بدون نام)" : r.GuardianName;
                    string code = string.IsNullOrWhiteSpace(r.CaseCode) ? r.CasID.ToString() : r.CaseCode;
                    string place = string.Join(" / ", new[] { r.Province, r.District });
                    _lst.Items.Add(name + "  —  " + code + (string.IsNullOrWhiteSpace(place.Trim('/', ' ')) ? "" : "  —  " + place));
                }

                _lblCount.Text = _rows.Count == 0
                    ? "هیچ پرونده‌ای پیدا نشد."
                    : (truncated ? _rows.Count + " پرونده (فهرست محدود شده)" : _rows.Count + " پرونده");

                // انتخابِ فعلی را برجسته کن.
                for (int i = 0; i < _rows.Count; i++)
                    if (_rows[i].CasID == SelectedCaseId) { _lst.SelectedIndex = i; break; }

                if (_lst.SelectedIndex < 0 && _rows.Count > 0) _lst.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                _lst.Items.Clear();
                _lblCount.Text = "خطا در خواندن فهرست پرونده‌ها: " + ex.Message;
            }
        }

        private void AcceptSelection()
        {
            int idx = _lst.SelectedIndex;
            if (idx < 0 || idx >= _rows.Count)
            {
                Msg.Show("یک پرونده را انتخاب کنید، یا «دادهٔ نمونه» را بزنید.");
                return;
            }
            SelectedCaseId = _rows[idx].CasID;
            UseDemoData = false;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
