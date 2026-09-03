using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace CaseManagement.Helpers
{
    // پنجره‌ی کوچکِ انتخابِ الگو — هم‌سبک با بقیه‌ی دیالوگ‌های برنامه، RTL کامل.
    // خروجی null یعنی کاربر انصراف داد.
    public static class FrmTemplatePicker
    {
        public static ReportTemplateHelper.TemplateOption Ask(
            IWin32Window owner,
            List<ReportTemplateHelper.TemplateOption> options,
            string rememberedPath)
        {
            if (options == null || options.Count == 0) return null;

            using (var dlg = new Form())
            {
                dlg.Text = "انتخاب الگوی خروجی";
                dlg.RightToLeft = RightToLeft.Yes;
                dlg.RightToLeftLayout = true;
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.MinimizeBox = false; dlg.MaximizeBox = false;
                dlg.ClientSize = new Size(460, 250);
                dlg.BackColor = UiTheme.Background;
                dlg.Font = UiTheme.Font(UiTheme.SizeBody);

                var info = new Label
                {
                    Text = "خروجی با کدام الگو ساخته شود؟",
                    Dock = DockStyle.Top, Height = 44, AutoSize = false,
                    TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(14, 8, 14, 4),
                    ForeColor = UiTheme.TextMuted, BackColor = Color.Transparent
                };

                var list = new ListBox
                {
                    RightToLeft = RightToLeft.Yes,
                    IntegralHeight = false,
                    Font = UiTheme.Font(UiTheme.SizeBody)
                };
                list.SetBounds(14, 52, 430, 132);

                int preselect = 0;
                for (int i = 0; i < options.Count; i++)
                {
                    list.Items.Add(options[i]);
                    if (!string.IsNullOrEmpty(rememberedPath) &&
                        string.Equals(options[i].FilePath, rememberedPath, StringComparison.OrdinalIgnoreCase))
                        preselect = i;
                }
                list.SelectedIndex = preselect;

                var btnOk = UiTheme.CreateButton("ساخت خروجی", "✔", UiTheme.Primary);
                btnOk.SetBounds(14, 196, 150, 34);
                var btnCancel = UiTheme.CreateSecondaryButton("انصراف", "✕");
                btnCancel.SetBounds(174, 196, 110, 34);

                btnOk.Click += delegate { dlg.DialogResult = DialogResult.OK; };
                btnCancel.Click += delegate { dlg.DialogResult = DialogResult.Cancel; };

                // دابل‌کلیک روی نام الگو = انتخاب و تأیید.
                list.DoubleClick += delegate { dlg.DialogResult = DialogResult.OK; };

                dlg.Controls.Add(list);
                dlg.Controls.Add(btnOk);
                dlg.Controls.Add(btnCancel);
                dlg.Controls.Add(info);
                dlg.AcceptButton = btnOk;
                dlg.CancelButton = btnCancel;

                if (dlg.ShowDialog(owner) != DialogResult.OK) return null;
                return list.SelectedItem as ReportTemplateHelper.TemplateOption;
            }
        }
    }
}
