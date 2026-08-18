using System;
using System.Drawing;
using System.Windows.Forms;
using CaseManagement.Helpers;

namespace CaseManagement.GuardianCardIntegration
{
    // ─────────────────────────────────────────────────────────────────────────
    // ویرایش محتوای قابل‌چاپِ کارت شناسایی برای یک پرونده — پاسخ به دو
    // درخواست کاربر:
    //   ۱) شرایط/هشدارها/تلفن/آدرسِ کارت باید برای هر پرونده قابل ویرایش
    //      باشند، نه فقط یک متن سراسری (خالی‌گذاشتنِ هر فیلد یعنی «همان
    //      پیش‌فرضِ مؤسسه»).
    //   ۲) «CARD CUSTOM CONTENT EDITING» — ویرایش نباید همیشه دیتابیس را
    //      تغییر دهد. دو حالت وجود دارد:
    //        • «فقط برای این چاپ» (پیش‌فرض): هیچ‌چیز در DB نوشته نمی‌شود؛
    //          فقط آبجکتِ در حالِ نمایشِ فعلی (GuardianCardData) موقتاً
    //          override و دوباره رندر می‌شود.
    //        • «ذخیرهٔ دائمی در دیتابیس»: دقیقاً رفتار قبلی — از این پس
    //          همیشه همین مقدار روی کارت این پرونده چاپ می‌شود.
    // ─────────────────────────────────────────────────────────────────────────
    public class FrmCardNoticesEdit : Form
    {
        private readonly int _caseId;
        private readonly GuardianCardData _currentData;
        private readonly CaseCardRepository _repo = new CaseCardRepository();

        private TextBox[] _txtNotices;
        private TextBox _txtLegalLine;
        private TextBox _txtPhone;
        private TextBox _txtAddress;
        private Label[] _lblDefaults;
        private Label _lblLegalDefault;
        private Label _lblPhoneDefault;
        private Label _lblAddressDefault;
        private RadioButton _radPrintOnly;
        private RadioButton _radSaveDb;

        // نتیجه برای فراخوان (FrmGuardianCardPreview) — فقط وقتی DialogResult
        // برابر OK است معتبرند.
        public bool SavedPermanently { get; private set; }
        public GuardianCardData PrintOnlyData { get; private set; }

        public FrmCardNoticesEdit(int caseId, GuardianCardData currentData)
        {
            _caseId = caseId;
            _currentData = currentData;
            BuildUi();
            Load += delegate { LoadValues(); };
        }

        private void BuildUi()
        {
            Text = "ویرایش محتوای کارت شناسایی این پرونده";
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor = UiTheme.Background;
            Font = UiTheme.Font(UiTheme.SizeBody);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(640, 640);

            Label lblHint = new Label
            {
                Text = "هر فیلد را خالی بگذارید تا متنِ پیش‌فرضِ مؤسسه (تنظیمات) روی این کارت چاپ شود.",
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = UiTheme.TextMuted,
                Font = UiTheme.Font(9F)
            };
            lblHint.SetBounds(16, 12, 608, 24);
            Controls.Add(lblHint);

            _txtNotices = new TextBox[5];
            _lblDefaults = new Label[5];
            int y = 44;

            for (int i = 0; i < 5; i++)
            {
                Label lblTitle = new Label
                {
                    Text = "شرط " + (i + 1),
                    AutoSize = false,
                    TextAlign = ContentAlignment.MiddleRight,
                    Font = UiTheme.FontBold(9.5F)
                };
                lblTitle.SetBounds(540, y, 84, 22);
                Controls.Add(lblTitle);

                TextBox txt = new TextBox { RightToLeft = RightToLeft.Yes };
                txt.SetBounds(16, y, 516, 24);
                Controls.Add(txt);
                _txtNotices[i] = txt;

                Label lblDefault = new Label
                {
                    AutoSize = false,
                    TextAlign = ContentAlignment.MiddleRight,
                    ForeColor = UiTheme.TextMuted,
                    Font = UiTheme.Font(8F)
                };
                lblDefault.SetBounds(16, y + 25, 608, 18);
                Controls.Add(lblDefault);
                _lblDefaults[i] = lblDefault;

                y += 50;
            }

            y = AddSingleField(y, "متن حقوقی", out _txtLegalLine, out _lblLegalDefault);
            y = AddSingleField(y, "تلفن", out _txtPhone, out _lblPhoneDefault);
            y = AddSingleField(y, "آدرس", out _txtAddress, out _lblAddressDefault);

            y += 8;
            Label lblMode = new Label
            {
                Text = "حالتِ ذخیره:",
                AutoSize = false, TextAlign = ContentAlignment.MiddleRight,
                Font = UiTheme.FontBold(9.5F)
            };
            lblMode.SetBounds(16, y, 100, 22);
            Controls.Add(lblMode);
            y += 26;

            _radPrintOnly = new RadioButton
            {
                Text = "فقط برای این چاپ (چیزی در دیتابیس ذخیره نمی‌شود)",
                Checked = true, AutoSize = true, RightToLeft = RightToLeft.Yes
            };
            _radPrintOnly.SetBounds(16, y, 500, 22);
            Controls.Add(_radPrintOnly);
            y += 28;

            _radSaveDb = new RadioButton
            {
                Text = "ذخیرهٔ دائمی در دیتابیس (این تغییر برای همیشه روی کارت این پرونده می‌ماند)",
                AutoSize = true, RightToLeft = RightToLeft.Yes
            };
            _radSaveDb.SetBounds(16, y, 560, 22);
            Controls.Add(_radSaveDb);
            y += 36;

            var btnSave = UiTheme.CreateButton("ادامه", "💾", UiTheme.Success);
            btnSave.SetBounds(16, y, 120, 36);
            btnSave.Click += delegate { Save(); };
            Controls.Add(btnSave);

            var btnCancel = UiTheme.CreateSecondaryButton("انصراف", "");
            btnCancel.SetBounds(144, y, 100, 36);
            btnCancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(btnCancel);

            ClientSize = new Size(640, y + 56);
        }

        private int AddSingleField(int y, string title, out TextBox textBox, out Label defaultLabel)
        {
            Label lblTitle = new Label
            {
                Text = title, AutoSize = false,
                TextAlign = ContentAlignment.MiddleRight, Font = UiTheme.FontBold(9.5F)
            };
            lblTitle.SetBounds(540, y, 84, 22);
            Controls.Add(lblTitle);

            textBox = new TextBox { RightToLeft = RightToLeft.Yes };
            textBox.SetBounds(16, y, 516, 24);
            Controls.Add(textBox);

            defaultLabel = new Label
            {
                AutoSize = false, TextAlign = ContentAlignment.MiddleRight,
                ForeColor = UiTheme.TextMuted, Font = UiTheme.Font(8F)
            };
            defaultLabel.SetBounds(16, y + 25, 608, 18);
            Controls.Add(defaultLabel);

            return y + 50;
        }

        private void LoadValues()
        {
            try
            {
                var c = _repo.GetCase(_caseId);

                string[] overrides = { c.CardNotice1, c.CardNotice2, c.CardNotice3, c.CardNotice4, c.CardNotice5 };
                string[] defaults = { _currentData.Notice1, _currentData.Notice2, _currentData.Notice3, _currentData.Notice4, _currentData.Notice5 };

                for (int i = 0; i < 5; i++)
                {
                    _txtNotices[i].Text = overrides[i] ?? "";
                    _lblDefaults[i].Text = "مقدار فعلی روی کارت: " + (defaults[i] ?? "");
                }

                _txtLegalLine.Text = c.CardLegalLine ?? "";
                _lblLegalDefault.Text = "مقدار فعلی روی کارت: " + (_currentData.LegalLine ?? "");

                _txtPhone.Text = c.CardPhone ?? "";
                _lblPhoneDefault.Text = "مقدار فعلی روی کارت: " + (_currentData.Phone ?? "");

                _txtAddress.Text = c.CardAddress ?? "";
                _lblAddressDefault.Text = "مقدار فعلی روی کارت: " + (_currentData.Address ?? "");
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در بارگذاری محتوای این پرونده: " + ex.Message);
            }
        }

        private void Save()
        {
            try
            {
                if (_radSaveDb.Checked)
                {
                    _repo.UpdateCardOverrides(
                        _caseId,
                        _txtNotices[0].Text, _txtNotices[1].Text, _txtNotices[2].Text,
                        _txtNotices[3].Text, _txtNotices[4].Text, _txtLegalLine.Text,
                        _txtPhone.Text, _txtAddress.Text);

                    SavedPermanently = true;
                    PrintOnlyData = null;
                }
                else
                {
                    // آموزش — «فقط برای این چاپ»: هیچ نوشتنی در DB انجام نمی‌شود.
                    // فقط آبجکتِ داده‌ی در حالِ نمایشِ فعلی کپی می‌شود و فقط
                    // فیلدهای غیرخالی روی آن override می‌شوند؛ فیلدهای خالی
                    // دست‌نخورده می‌مانند (یعنی همان مقدارِ مؤثرِ فعلی که از قبل
                    // روی آبجکت هست، نه اینکه دوباره به پیش‌فرض بازگردد).
                    GuardianCardData clone = _currentData.Clone();
                    ApplyIfNotEmpty(_txtNotices[0].Text, v => clone.Notice1 = v);
                    ApplyIfNotEmpty(_txtNotices[1].Text, v => clone.Notice2 = v);
                    ApplyIfNotEmpty(_txtNotices[2].Text, v => clone.Notice3 = v);
                    ApplyIfNotEmpty(_txtNotices[3].Text, v => clone.Notice4 = v);
                    ApplyIfNotEmpty(_txtNotices[4].Text, v => clone.Notice5 = v);
                    ApplyIfNotEmpty(_txtLegalLine.Text, v => clone.LegalLine = v);
                    ApplyIfNotEmpty(_txtPhone.Text, v => clone.Phone = v);
                    ApplyIfNotEmpty(_txtAddress.Text, v => clone.Address = v);

                    SavedPermanently = false;
                    PrintOnlyData = clone;
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در ذخیره محتوای این پرونده: " + ex.Message);
            }
        }

        private static void ApplyIfNotEmpty(string text, Action<string> setter)
        {
            string trimmed = (text ?? "").Trim();
            if (trimmed.Length > 0) setter(trimmed);
        }
    }
}
