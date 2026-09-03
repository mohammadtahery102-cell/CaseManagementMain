using System;
using System.Drawing;
using System.Windows.Forms;

namespace CaseManagement.Helpers
{
    // دیالوگِ مشترکِ فیلترِ پیشرفته که پیش از ساخت هر گزارش/خروجی جمعی نمایش
    // داده می‌شود: ولایت، ولسوالی، وضعیت خدمات، بازه‌ی تاریخ ثبت، نوع خانواده،
    // فعال/غیرفعال. هم‌سبک با بقیه‌ی دیالوگ‌های برنامه (نگاه کنید FrmTemplatePicker)،
    // RTL کامل. خروجی null یعنی کاربر انصراف داد.
    public static class FrmReportFilter
    {
        // آموزش — رفعِ اشکالِ گزارش‌شده: ولایت/وضعیت خدمات/نوع خانواده قبلاً اینجا
        // آرایه‌ی سخت‌کدِ جداگانه بودند که می‌توانست با مقادیرِ واقعیِ سیستم (که در
        // TblLookup و از صفحه‌ی تنظیمات قابل ویرایش‌اند — همان منبعی که
        // FrmCase از طریق LookupHelper.FillCombo(txtProvince, "Province") و
        // FrmSettings برای "ServiceStatus"/"RequestType"/"Province" استفاده می‌کنند)
        // ناهم‌خوان شود. حالا هر سه دقیقاً از همان منبع (LookupHelper → TblLookup)
        // خوانده می‌شوند؛ هیچ لیستِ جداگانه‌ای اینجا نگه‌داری نمی‌شود.
        private static readonly string[] ActiveOptions = { "همه", "فقط فعال", "فقط غیرفعال (بایگانی)" };

        // آموزش — به درخواستِ کاربر: فیلترِ «تعداد اعضای خانواده» تا بشود فقط
        // خانواده‌های یک‌نفره یا چندنفره را جدا چاپ/خروجی گرفت.
        private static readonly string[] MemberCountOptions = { "همه", "دقیقاً ۱ نفر", "۲ نفر یا بیشتر" };

        public static ReportFilterCriteria Ask(IWin32Window owner)
        {
            using (var dlg = new Form())
            {
                dlg.Text = "فیلترهای پیشرفته گزارش";
                dlg.RightToLeft = RightToLeft.Yes;
                dlg.RightToLeftLayout = true;
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.MinimizeBox = false; dlg.MaximizeBox = false;
                dlg.ClientSize = new Size(420, 400);
                dlg.BackColor = UiTheme.Background;
                dlg.Font = UiTheme.Font(UiTheme.SizeBody);

                var cmbProvince = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, RightToLeft = RightToLeft.Yes };
                LookupHelper.FillCombo(cmbProvince, "Province", "همه");

                // آموزش — رفعِ اشکالِ گزارش‌شده: ولسوالی قبلاً TextBoxِ آزاد بود
                // (امکان غلط‌تایپی/ناهم‌خوانی با داده‌ی واقعی). حالا کمبوی
                // آبشاری است؛ دقیقاً همان الگو و همان منبعِ داده
                // (Helpers.AfghanGeoData.GetDistricts) که از قبل در
                // FrmCase.cs:1789 و FrmDashboard.cs:570 برای همین کار استفاده
                // می‌شود — نه فهرستِ جدید. تا وقتی ولایتی انتخاب نشده، فقط
                // «همه» در فهرست است.
                var cmbDistrict = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, RightToLeft = RightToLeft.Yes };
                Action reloadDistricts = delegate
                {
                    string selectedProvince = cmbProvince.SelectedIndex <= 0 ? "" : cmbProvince.Text;
                    cmbDistrict.Items.Clear();
                    cmbDistrict.Items.Add("همه");
                    foreach (string d in AfghanGeoData.GetDistricts(selectedProvince))
                        cmbDistrict.Items.Add(d);
                    cmbDistrict.SelectedIndex = 0;
                };
                cmbProvince.SelectedIndexChanged += delegate { reloadDistricts(); };
                reloadDistricts();

                var cmbStatus = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, RightToLeft = RightToLeft.Yes };
                LookupHelper.FillCombo(cmbStatus, "ServiceStatus", "همه");

                var cmbFamilyType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, RightToLeft = RightToLeft.Yes };
                LookupHelper.FillCombo(cmbFamilyType, "RequestType", "همه");

                var cmbActive = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, RightToLeft = RightToLeft.Yes };
                cmbActive.Items.AddRange(ActiveOptions);
                cmbActive.SelectedIndex = 0;

                var cmbMemberCount = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, RightToLeft = RightToLeft.Yes };
                cmbMemberCount.Items.AddRange(MemberCountOptions);
                cmbMemberCount.SelectedIndex = 0;

                var dtpFrom = new PersianDatePicker { ShowCheckBox = true, Checked = false };
                var dtpTo = new PersianDatePicker { ShowCheckBox = true, Checked = false };

                int y = 12;
                Action<string, Control> addRow = (label, ctl) =>
                {
                    var lbl = new Label
                    {
                        Text = label,
                        TextAlign = ContentAlignment.MiddleRight,
                        ForeColor = UiTheme.TextMuted
                    };
                    lbl.SetBounds(280, y + 3, 128, 24);
                    ctl.SetBounds(14, y, 258, ctl is PersianDatePicker ? 30 : 27);
                    dlg.Controls.Add(lbl);
                    dlg.Controls.Add(ctl);
                    y += 40;
                };

                addRow("ولایت", cmbProvince);
                addRow("ولسوالی", cmbDistrict);
                addRow("وضعیت خدمات", cmbStatus);
                addRow("نوع خانواده", cmbFamilyType);
                addRow("فعال / غیرفعال", cmbActive);
                addRow("تعداد اعضای خانواده", cmbMemberCount);
                addRow("از تاریخ ثبت", dtpFrom);
                addRow("تا تاریخ ثبت", dtpTo);

                var btnOk = UiTheme.CreateButton("اعمال فیلتر و ادامه", "✔", UiTheme.Primary);
                btnOk.SetBounds(14, y + 6, 190, 34);
                var btnCancel = UiTheme.CreateSecondaryButton("انصراف", "✕");
                btnCancel.SetBounds(214, y + 6, 100, 34);

                btnOk.Click += delegate { dlg.DialogResult = DialogResult.OK; };
                btnCancel.Click += delegate { dlg.DialogResult = DialogResult.Cancel; };

                dlg.Controls.Add(btnOk);
                dlg.Controls.Add(btnCancel);
                dlg.AcceptButton = btnOk;
                dlg.CancelButton = btnCancel;
                dlg.ClientSize = new Size(420, y + 54);

                if (dlg.ShowDialog(owner) != DialogResult.OK) return null;

                var result = new ReportFilterCriteria
                {
                    Province = cmbProvince.SelectedIndex <= 0 ? "" : cmbProvince.Text,
                    District = cmbDistrict.SelectedIndex <= 0 ? "" : cmbDistrict.Text,
                    ServiceStatus = cmbStatus.SelectedIndex <= 0 ? "" : cmbStatus.Text,
                    FamilyType = cmbFamilyType.SelectedIndex <= 0 ? "" : cmbFamilyType.Text,
                    RegistrationDateFrom = dtpFrom.Checked ? dtpFrom.Value.Date : (DateTime?)null,
                    RegistrationDateTo = dtpTo.Checked ? dtpTo.Value.Date : (DateTime?)null,
                    ActiveOnly = cmbActive.SelectedIndex == 1 ? true : (cmbActive.SelectedIndex == 2 ? false : (bool?)null),
                    MinMemberCount = cmbMemberCount.SelectedIndex == 1 ? 1 : (cmbMemberCount.SelectedIndex == 2 ? 2 : (int?)null),
                    MaxMemberCount = cmbMemberCount.SelectedIndex == 1 ? 1 : (int?)null
                };

                return result;
            }
        }
    }
}
