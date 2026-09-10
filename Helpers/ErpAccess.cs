using System;
using System.Windows.Forms;
using CaseManagement.Enterprise;

namespace CaseManagement.Helpers
{
    public static class ErpAccess
    {
        public static void RequirePermission(Form form, string permissionKey)
        {
            if (form == null || string.IsNullOrWhiteSpace(permissionKey)) return;
            form.Load += delegate
            {
                if (PermissionService.HasPermission(permissionKey)) return;
                MessageBox.Show(form, "شما مجوز مشاهده این بخش را ندارید.", form.Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                form.BeginInvoke(new Action(delegate { form.DialogResult = DialogResult.Abort; form.Close(); }));
            };
        }

        public static void BlockCharityScreenInErp(Form form)
        {
            if (form == null) return;
            form.Load += delegate
            {
                if (!ProductMode.IsErp) return;
                if (SecurityContext.IsSuperAdmin()) return;
                if (!SecurityContext.IsLoggedIn) return;
                MessageBox.Show(form, "این بخش در حالت سیستم کسب‌وکار در دسترس نیست.", form.Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                form.BeginInvoke(new Action(delegate { form.DialogResult = DialogResult.Abort; form.Close(); }));
            };
        }
    }
}
