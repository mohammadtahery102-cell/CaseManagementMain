using CaseManagement.Helpers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace CaseManagement
{
    // ═══════════════════════════════════════════════════════════════════════
    // Feature 1 — «تنظیمات سیستم: نوع پرونده و وضعیت خدمات»
    //
    // چرا فرمِ جدا و نه یک تبِ تازه در FrmSettings: آن فرم مدیریتِ TblLookup
    // است — فهرست‌های سادهٔ متنی که حذفشان بی‌خطر است. این دو جدول دادهٔ
    // *مرجع*اند: ستونِ Code هویتِ برنامه‌ای است و منطقِ کد با آن مقایسه
    // می‌کند. قواعدِ ایمنی‌شان (بدونِ حذف، Codeِ تغییرناپذیر، بازگردانیِ
    // پیش‌فرض) با قواعدِ TblLookup یکی نیست و مخلوط‌کردنشان در یک صفحه،
    // کاربر را به این تصور می‌انداخت که این‌ها هم مثلِ بقیه حذف‌شدنی‌اند.
    //
    // همهٔ نوشتن‌ها از ReferenceDataAdminService عبور می‌کنند؛ این فرم هیچ
    // SQL ای ندارد.
    // ═══════════════════════════════════════════════════════════════════════
    public class FrmReferenceDataSettings : Form
    {
        private TabControl _tabs;
        private DataGridView _gridTypes, _gridStatuses;
        private Label _lblHelp;

        public FrmReferenceDataSettings()
        {
            BuildUi();
            ReloadAll();
        }

        private void BuildUi()
        {
            Text = "تنظیمات سیستم — نوع پرونده و وضعیت خدمات";
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = false;
            BackColor = UiTheme.Background;
            Font = UiTheme.Font(UiTheme.SizeBody);
            UiTheme.MakeMainWindow(this, 1040, 660);

            _tabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = UiTheme.FontBold(10F),
                RightToLeft = RightToLeft.Yes,
                RightToLeftLayout = true
            };

            _gridTypes    = MakeGrid();
            _gridStatuses = MakeGrid();

            _tabs.TabPages.Add(MakeTab("نوع پرونده",
                ReferenceDataAdminService.TableRequestType, _gridTypes));
            _tabs.TabPages.Add(MakeTab("وضعیت خدمات",
                ReferenceDataAdminService.TableServiceStatus, _gridStatuses));

            // ─── متنِ راهنما (خواستهٔ ۸) ─────────────────────────────────────
            _lblHelp = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 96,
                RightToLeft = RightToLeft.Yes,
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(12, 6, 12, 6),
                BackColor = UiTheme.WarningLight,
                ForeColor = UiTheme.TextDark,
                Text =
                    "راهنما: مقادیرِ «سیستمی» پایهٔ کارکردِ نرم‌افزارند و حذف نمی‌شوند. " +
                    "تنها نامِ نمایشی‌شان قابلِ تغییر است؛ شناسهٔ داخلی (Code) ثابت می‌ماند تا " +
                    "گزارش‌ها، جستجو و قواعدِ کاری نشکنند." + Environment.NewLine +
                    "به‌جای حذف، مقدار را «غیرفعال» کنید: از فهرست‌های ورودی برداشته می‌شود ولی " +
                    "پرونده‌های موجود دست‌نخورده می‌مانند." + Environment.NewLine +
                    "با «بازگردانی به پیش‌فرض» فقط مقادیرِ سیستمی به نامِ اولیه برمی‌گردند؛ " +
                    "مقادیری که خودتان افزوده‌اید پاک نمی‌شوند."
            };

            Controls.Add(_tabs);
            Controls.Add(_lblHelp);
        }

        private TabPage MakeTab(string title, string table, DataGridView grid)
        {
            var page = new TabPage(title) { BackColor = UiTheme.Background };

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 46,
                RightToLeft = RightToLeft.Yes,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(8, 8, 8, 4)
            };

            buttons.Controls.Add(MakeButton("افزودن مقدار", delegate { AddValue(table, grid); }));
            buttons.Controls.Add(MakeButton("ویرایش نام", delegate { RenameValue(table, grid); }));
            buttons.Controls.Add(MakeButton("فعال / غیرفعال", delegate { ToggleActive(table, grid); }));
            buttons.Controls.Add(MakeButton("بازگردانی به پیش‌فرض", delegate { RestoreDefaults(table, grid); }));

            var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
            host.Controls.Add(grid);

            page.Controls.Add(host);
            page.Controls.Add(buttons);
            return page;
        }

        private Button MakeButton(string text, EventHandler onClick)
        {
            var b = new Button
            {
                Text = text,
                AutoSize = true,
                Padding = new Padding(10, 4, 10, 4),
                Margin = new Padding(4, 0, 4, 0)
            };
            b.Click += onClick;
            return b;
        }

        private DataGridView MakeGrid()
        {
            var g = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                RightToLeft = RightToLeft.Yes
            };
            return g;
        }

        private void ReloadAll()
        {
            Reload(ReferenceDataAdminService.TableRequestType, _gridTypes);
            Reload(ReferenceDataAdminService.TableServiceStatus, _gridStatuses);
        }

        private void Reload(string table, DataGridView grid)
        {
            var rows = ReferenceDataAdminService.Load(table);

            var t = new System.Data.DataTable();
            t.Columns.Add("Id", typeof(int));
            t.Columns.Add("شناسه داخلی", typeof(string));
            t.Columns.Add("نام نمایشی", typeof(string));
            t.Columns.Add("مقدار پیش‌فرض", typeof(string));
            t.Columns.Add("نوع", typeof(string));
            t.Columns.Add("وضعیت", typeof(string));
            t.Columns.Add("تعداد پرونده", typeof(int));

            foreach (ReferenceRow r in rows)
            {
                t.Rows.Add(r.ID, r.Code, r.Name,
                    r.IsBuiltIn ? r.DefaultName : "—",
                    // خواستهٔ ۶ — مقادیرِ داخلی باید صریحاً مشخص باشند.
                    r.IsBuiltIn ? "سیستمی" : "افزوده‌شده",
                    r.IsActive ? "فعال" : "غیرفعال",
                    r.UsageCount);
            }

            grid.DataSource = t;
            if (grid.Columns.Contains("Id")) grid.Columns["Id"].Visible = false;

            // ردیف‌های غیرفعال و سیستمی از هم قابلِ تفکیک باشند.
            foreach (DataGridViewRow gr in grid.Rows)
            {
                if ((string)gr.Cells["وضعیت"].Value == "غیرفعال")
                    gr.DefaultCellStyle.ForeColor = Color.Gray;
                if ((string)gr.Cells["نوع"].Value == "سیستمی")
                    gr.Cells["نوع"].Style.ForeColor = UiTheme.Primary;
            }
        }

        private ReferenceRow Selected(string table, DataGridView grid)
        {
            if (grid.CurrentRow == null) { Msg.Show("اول یک ردیف را انتخاب کنید."); return null; }

            int id = Convert.ToInt32(grid.CurrentRow.Cells["Id"].Value);
            foreach (ReferenceRow r in ReferenceDataAdminService.Load(table))
                if (r.ID == id) return r;

            return null;
        }

        // ─── عملیات ──────────────────────────────────────────────────────────

        private void AddValue(string table, DataGridView grid)
        {
            if (!Guard()) return;

            string name = Enterprise.EntPrompt.AskText(this, "افزودن مقدار تازه", "نام نمایشی:", "");
            if (string.IsNullOrWhiteSpace(name)) return;

            // خواستهٔ ۴ — هشدار پیش از ذخیره.
            if (!UiTheme.ShowConfirm(this,
                    "مقدارِ «" + name.Trim() + "» افزوده شود؟" + Environment.NewLine + Environment.NewLine +
                    "این مقدار بلافاصله در فهرست‌های ورودیِ پرونده ظاهر می‌شود.",
                    "تأیید افزودن"))
                return;

            try
            {
                ReferenceDataAdminService.Add(table, name);
                Reload(table, grid);
                Msg.Show("مقدار افزوده شد.");
            }
            catch (Exception ex) { Msg.Show(ex.Message); }
        }

        private void RenameValue(string table, DataGridView grid)
        {
            if (!Guard()) return;

            ReferenceRow row = Selected(table, grid);
            if (row == null) return;

            string name = Enterprise.EntPrompt.AskText(this, "ویرایش نام نمایشی",
                "نام تازه برای «" + row.Name + "»:", row.Name);
            if (string.IsNullOrWhiteSpace(name)) return;
            if (string.Equals(name.Trim(), row.Name, StringComparison.Ordinal)) return;

            string warning =
                "نامِ «" + row.Name + "» به «" + name.Trim() + "» تغییر کند؟" +
                Environment.NewLine + Environment.NewLine +
                "شناسهٔ داخلی (" + row.Code + ") تغییر نمی‌کند." + Environment.NewLine +
                (row.UsageCount > 0
                    ? "⚠ " + row.UsageCount + " پرونده از این مقدار استفاده می‌کنند و همگی به نامِ تازه منتقل می‌شوند."
                    : "هیچ پرونده‌ای از این مقدار استفاده نمی‌کند.");

            if (!UiTheme.ShowConfirm(this, warning, "تأیید تغییر نام")) return;

            try
            {
                ReferenceDataAdminService.Rename(table, row.ID, name);
                Reload(table, grid);
                Msg.Show("نام تغییر کرد.");
            }
            catch (Exception ex) { Msg.Show(ex.Message); }
        }

        private void ToggleActive(string table, DataGridView grid)
        {
            if (!Guard()) return;

            ReferenceRow row = Selected(table, grid);
            if (row == null) return;

            bool target = !row.IsActive;

            if (!target)
            {
                // ─── خواستهٔ ۲ و ۳: تأییدِ چندمرحله‌ای ────────────────────────
                // غیرفعال‌کردن نزدیک‌ترین چیز به «حذف» است که این صفحه دارد،
                // پس عمداً دو مرحله دارد: یک تأییدِ صریح و بعد تایپِ نامِ
                // مقدار. کلیکِ اتفاقی نباید یک مقدارِ پرکاربرد را از فهرست
                // بردارد.
                string first =
                    "مقدارِ «" + row.Name + "» غیرفعال شود؟" + Environment.NewLine + Environment.NewLine +
                    (row.IsBuiltIn ? "این یک مقدارِ سیستمی است." + Environment.NewLine : "") +
                    (row.UsageCount > 0
                        ? "⚠ " + row.UsageCount + " پرونده از آن استفاده می‌کنند. آن پرونده‌ها تغییر نمی‌کنند، " +
                          "ولی این مقدار دیگر برای پرونده‌های تازه قابلِ انتخاب نیست."
                        : "هیچ پرونده‌ای از آن استفاده نمی‌کند.");

                if (!UiTheme.ShowConfirm(this, first, "غیرفعال‌سازی — مرحلهٔ ۱ از ۲")) return;

                string typed = Enterprise.EntPrompt.AskText(this, "غیرفعال‌سازی — مرحلهٔ ۲ از ۲",
                    "برای تأیید، نامِ مقدار را دقیقاً تایپ کنید:" + Environment.NewLine + row.Name, "");

                if (typed == null) return;
                if (!string.Equals(typed.Trim(), row.Name, StringComparison.Ordinal))
                {
                    Msg.Show("نامِ واردشده مطابقت ندارد؛ هیچ تغییری انجام نشد.");
                    return;
                }
            }
            else
            {
                if (!UiTheme.ShowConfirm(this,
                        "مقدارِ «" + row.Name + "» دوباره فعال شود؟", "تأیید فعال‌سازی"))
                    return;
            }

            try
            {
                ReferenceDataAdminService.SetActive(table, row.ID, target);
                Reload(table, grid);
                Msg.Show(target ? "مقدار فعال شد." : "مقدار غیرفعال شد.");
            }
            catch (Exception ex) { Msg.Show(ex.Message); }
        }

        private void RestoreDefaults(string table, DataGridView grid)
        {
            if (!Guard()) return;

            // تأییدِ چندمرحله‌ای — این عمل چند ردیف را هم‌زمان عوض می‌کند.
            if (!UiTheme.ShowConfirm(this,
                    "همهٔ مقادیرِ سیستمی به نام و وضعیتِ اولیه برگردند؟" +
                    Environment.NewLine + Environment.NewLine +
                    "مقادیری که خودتان افزوده‌اید دست‌نخورده می‌مانند." + Environment.NewLine +
                    "پرونده‌هایی که نامِ تغییریافته را داشتند، به نامِ پیش‌فرض منتقل می‌شوند.",
                    "بازگردانی به پیش‌فرض — مرحلهٔ ۱ از ۲"))
                return;

            string typed = Enterprise.EntPrompt.AskText(this, "بازگردانی به پیش‌فرض — مرحلهٔ ۲ از ۲",
                "برای تأیید، عبارتِ زیر را تایپ کنید:" + Environment.NewLine + "بازگردانی", "");

            if (typed == null) return;
            if (!string.Equals(typed.Trim(), "بازگردانی", StringComparison.Ordinal))
            {
                Msg.Show("عبارتِ واردشده مطابقت ندارد؛ هیچ تغییری انجام نشد.");
                return;
            }

            try
            {
                int changed = ReferenceDataAdminService.RestoreDefaults(table);
                Reload(table, grid);
                Msg.Show(changed == 0
                    ? "همهٔ مقادیرِ سیستمی از قبل در حالتِ پیش‌فرض بودند."
                    : "بازگردانی انجام شد (" + changed + " تغییر).");
            }
            catch (Exception ex) { Msg.Show(ex.Message); }
        }

        // خواستهٔ ۱۰ — فقط کاربرِ مجاز.
        private bool Guard()
        {
            if (ReferenceDataAdminService.CanModify()) return true;

            Enterprise.PermissionService.Require(ReferenceDataAdminService.PermissionKey);
            Msg.Show("شما اجازهٔ تغییرِ این مقادیر را ندارید.");
            return false;
        }
    }
}
