using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using CaseManagement.Helpers;

namespace CaseManagement.Accounting
{
    /// <summary>
    /// پوسته یکنواخت فرم‌های حسابداری — SAP/Dynamics-style workspace.
    /// هیچ منطقی اینجا نیست؛ فقط چیدمان، فونت، گرید، کمبو و نوار وضعیت.
    /// </summary>
    public static class AccountingChrome
    {
        public static void MakeWorkspace(Form form, int width, int height)
        {
            if (form == null) return;
            form.RightToLeft = RightToLeft.Yes;
            form.RightToLeftLayout = true;
            form.BackColor = UiTheme.Background;
            form.Font = UiTheme.Font(UiTheme.SizeBody);
            form.AutoScaleMode = AutoScaleMode.Dpi;
            form.AutoScaleDimensions = new SizeF(96F, 96F);
            form.FormBorderStyle = FormBorderStyle.Sizable;
            form.MaximizeBox = true;
            form.MinimizeBox = true;
            form.StartPosition = FormStartPosition.CenterScreen;
            form.Padding = new Padding(0);

            Size area = SafeWorkingArea();
            int minW = Math.Min(Math.Max(1024, width / 2), area.Width);
            int minH = Math.Min(Math.Max(620, height / 2), area.Height);
            form.MinimumSize = new Size(minW, minH);

            int w = Math.Min(Math.Max(width, (int)(area.Width * 0.88)), area.Width);
            int h = Math.Min(Math.Max(height, (int)(area.Height * 0.88)), area.Height);
            form.ClientSize = new Size(w, h);

            try { form.Icon = LogoHelper.GetAppIcon(); } catch { }
        }

        public static Panel BuildHeader(string title, string breadcrumb)
        {
            Panel banner = new Panel
            {
                Dock = DockStyle.Top,
                Height = 64,
                BackColor = UiTheme.PrimaryDark,
                Padding = new Padding(16, 8, 16, 8)
            };
            Label crumb = new Label
            {
                Dock = DockStyle.Top,
                Height = 18,
                ForeColor = Color.FromArgb(200, 220, 235),
                Font = UiTheme.Font(8.5F),
                TextAlign = ContentAlignment.MiddleRight,
                Text = string.IsNullOrWhiteSpace(breadcrumb) ? "حسابداری" : breadcrumb,
                AutoEllipsis = true
            };
            Label lbl = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = Color.White,
                Font = UiTheme.FontBold(15F),
                TextAlign = ContentAlignment.MiddleRight,
                Text = title ?? "",
                AutoEllipsis = true
            };
            banner.Controls.Add(lbl);
            banner.Controls.Add(crumb);
            return banner;
        }

        public static Panel BuildStatusBar()
        {
            Panel bar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 28,
                BackColor = ColorTranslator.FromHtml("#E8EEF4"),
                Padding = new Padding(10, 0, 10, 0)
            };
            string text = "مرکز: " + (SecurityContext.CenterDisplay ?? "—")
                + "   ·   کاربر: " + (SecurityContext.Username ?? "—")
                + "   ·   نقش: " + UiTheme.RoleDisplay(SecurityContext.Role)
                + "   ·   " + DateTime.Now.ToString("yyyy/MM/dd");
            bar.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = UiTheme.TextMuted,
                Font = UiTheme.Font(8.5F),
                Text = text,
                AutoEllipsis = true
            });
            return bar;
        }

        public static string Breadcrumb(params string[] parts)
        {
            if (parts == null || parts.Length == 0) return "حسابداری";
            return "حسابداری  ›  " + string.Join("  ›  ", parts);
        }

        public static void Polish(Form form)
        {
            if (form == null) return;
            Walk(form, delegate (Control c)
            {
                DataGridView grid = c as DataGridView;
                if (grid != null) { PolishGrid(grid); return; }

                ComboBox combo = c as ComboBox;
                if (combo != null) { StyleCombo(combo); return; }

                TextBox tb = c as TextBox;
                if (tb != null && !tb.ReadOnly && tb.Multiline == false)
                {
                    UiTheme.StyleTextBox(tb);
                    return;
                }

                Button btn = c as Button;
                if (btn != null)
                {
                    btn.FlatStyle = FlatStyle.Flat;
                    btn.Cursor = Cursors.Hand;
                    btn.Padding = new Padding(8, 2, 8, 2);
                    if (btn.Enabled && btn.Width > 8 && btn.Height > 8)
                    {
                        UiTheme.RoundCorners(btn, 8);
                        btn.SizeChanged -= RoundBtn;
                        btn.SizeChanged += RoundBtn;
                    }
                    if (btn.Enabled == false)
                    {
                        btn.ForeColor = UiTheme.TextMuted;
                        btn.BackColor = ColorTranslator.FromHtml("#EEF1F5");
                    }
                    return;
                }

                TabControl tabs = c as TabControl;
                if (tabs != null)
                {
                    tabs.Font = UiTheme.FontBold(10F);
                    tabs.Padding = new Point(10, 6);
                    tabs.RightToLeft = RightToLeft.Yes;
                    tabs.RightToLeftLayout = true;
                }

                NumericUpDown num = c as NumericUpDown;
                if (num != null)
                    num.Font = UiTheme.Font(UiTheme.SizeBody);
            });
        }

        public static void PolishGrid(DataGridView grid)
        {
            if (grid == null) return;
            UiTheme.StyleGrid(grid);
            grid.RightToLeft = RightToLeft.Yes;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersHeight = 36;
            grid.EnableHeadersVisualStyles = false;
            grid.AllowUserToOrderColumns = true;
            grid.AllowUserToResizeColumns = true;
            grid.MultiSelect = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            grid.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
            grid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            grid.AlternatingRowsDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#F4F7FA");
            grid.DefaultCellStyle.BackColor = Color.White;
            grid.RowTemplate.Height = 32;
            grid.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText;
            EnableDoubleBuffer(grid);

            if (grid.ContextMenuStrip == null)
            {
                ContextMenuStrip menu = new ContextMenuStrip { RightToLeft = RightToLeft.Yes, Font = UiTheme.Font(10F) };
                ToolStripMenuItem exp = new ToolStripMenuItem("خروجی اکسل");
                exp.Click += delegate { ExportGrid(grid); };
                menu.Items.Add(exp);
                grid.ContextMenuStrip = menu;
            }
        }

        public static void StyleCombo(ComboBox combo)
        {
            if (combo == null) return;
            combo.Font = UiTheme.Font(UiTheme.SizeBody);
            combo.FlatStyle = FlatStyle.Flat;
            combo.IntegralHeight = false;
            combo.MaxDropDownItems = 12;
            combo.RightToLeft = RightToLeft.Yes;
            if (combo.DropDownStyle == ComboBoxStyle.DropDownList && combo.Items.Count > 8)
            {
                combo.DropDownStyle = ComboBoxStyle.DropDown;
                combo.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
                combo.AutoCompleteSource = AutoCompleteSource.ListItems;
            }
            else if (combo.DropDownStyle == ComboBoxStyle.DropDown)
            {
                combo.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
                combo.AutoCompleteSource = AutoCompleteSource.ListItems;
            }
        }

        public static TextBox AttachQuickSearch(Control parent, DataGridView grid, string placeholder)
        {
            TextBox box = new TextBox
            {
                Width = 220,
                Font = UiTheme.Font(UiTheme.SizeBody),
                RightToLeft = RightToLeft.Yes
            };
            UiTheme.StyleTextBox(box);
            UiTheme.SetTip(box, "جستجو در جدول — با تایپ فیلتر می‌شود");
            string hint = string.IsNullOrWhiteSpace(placeholder) ? "جستجو..." : placeholder;
            box.ForeColor = UiTheme.TextMuted;
            box.Text = hint;
            box.GotFocus += delegate
            {
                if (box.Text == hint) { box.Text = ""; box.ForeColor = UiTheme.TextDark; }
            };
            box.LostFocus += delegate
            {
                if (string.IsNullOrWhiteSpace(box.Text)) { box.Text = hint; box.ForeColor = UiTheme.TextMuted; }
            };
            box.TextChanged += delegate
            {
                string q = box.Text;
                if (q == hint) q = "";
                FilterGrid(grid, q);
            };
            if (parent != null) parent.Controls.Add(box);
            return box;
        }

        public static void FilterGrid(DataGridView grid, string query)
        {
            if (grid == null) return;
            DataTable table = grid.DataSource as DataTable;
            if (table == null)
            {
                BindingSource bs = grid.DataSource as BindingSource;
                if (bs != null) table = bs.DataSource as DataTable;
            }
            if (table == null) return;

            string q = (query ?? "").Trim().Replace("'", "''");
            if (q.Length == 0)
            {
                table.DefaultView.RowFilter = "";
                return;
            }
            List<string> parts = new List<string>();
            foreach (DataColumn col in table.Columns)
            {
                parts.Add("CONVERT([" + col.ColumnName.Replace("]", "") + "], 'System.String') LIKE '%" + q + "%'");
            }
            try { table.DefaultView.RowFilter = string.Join(" OR ", parts.ToArray()); }
            catch { table.DefaultView.RowFilter = ""; }
        }

        public static void ExportGrid(DataGridView grid)
        {
            if (grid == null) return;
            DataTable table = grid.DataSource as DataTable;
            if (table == null)
            {
                BindingSource bs = grid.DataSource as BindingSource;
                if (bs != null) table = bs.DataSource as DataTable;
            }
            if (table == null || table.Rows.Count == 0)
            {
                UiTheme.ShowWarning(grid.FindForm(), "داده‌ای برای خروجی وجود ندارد.");
                return;
            }
            using (SaveFileDialog sfd = new SaveFileDialog { Filter = "فایل اکسل|*.xlsx", FileName = "گزارش.xlsx" })
            {
                if (sfd.ShowDialog(grid.FindForm()) != DialogResult.OK) return;
                try
                {
                    using (ClosedXML.Excel.XLWorkbook wb = new ClosedXML.Excel.XLWorkbook())
                    {
                        ClosedXML.Excel.IXLWorksheet ws = wb.Worksheets.Add("گزارش");
                        for (int c = 0; c < table.Columns.Count; c++)
                            ws.Cell(1, c + 1).Value = table.Columns[c].ColumnName;
                        for (int r = 0; r < table.Rows.Count; r++)
                            for (int c = 0; c < table.Columns.Count; c++)
                                ws.Cell(r + 2, c + 1).Value = Convert.ToString(table.Rows[r][c]);
                        wb.SaveAs(sfd.FileName);
                    }
                    UiTheme.ShowSuccess(grid.FindForm(), "فایل اکسل ذخیره شد.");
                }
                catch (Exception ex) { UiTheme.ShowError(grid.FindForm(), ex.Message); }
            }
        }

        public static Form PrepareDialog(Form dlg)
        {
            if (dlg == null) return dlg;
            dlg.RightToLeft = RightToLeft.Yes;
            dlg.RightToLeftLayout = true;
            dlg.Font = UiTheme.Font(UiTheme.SizeBody);
            dlg.BackColor = UiTheme.Background;
            dlg.StartPosition = FormStartPosition.CenterParent;
            dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
            dlg.MaximizeBox = false;
            dlg.MinimizeBox = false;
            dlg.AutoScaleMode = AutoScaleMode.Dpi;
            Polish(dlg);
            return dlg;
        }

        public static string StatusFa(string status)
        {
            if (status == "Draft") return "پیش‌نویس";
            if (status == "Approved") return "تأییدشده";
            if (status == "Posted") return "ثبت‌شده";
            if (status == "Reversed") return "برگشت‌خورده";
            if (status == "Open") return "باز";
            if (status == "Closed") return "بسته";
            if (status == "Locked") return "قفل";
            return status ?? "";
        }

        public static string SourceFa(string source)
        {
            if (source == "Manual") return "دستی";
            if (source == "Opening") return "افتتاحیه";
            if (source == "Close") return "اختتامیه";
            if (source == "Revaluation") return "تسعیر ارز";
            if (source == "Reversal") return "برگشت";
            if (source == "CashBook") return "دفتر صندوق";
            if (source == "Inventory") return "انبار";
            if (source == "Purchase") return "خرید";
            if (source == "Sales") return "فروش";
            if (source == "Payroll") return "حقوق";
            if (source == "FixedAsset") return "دارایی ثابت";
            if (source == "POS") return "فروشگاهی";
            return source ?? "";
        }

        private static void RoundBtn(object sender, EventArgs e)
        {
            Button b = sender as Button;
            if (b != null && b.Width > 8 && b.Height > 8)
                UiTheme.RoundCorners(b, 8);
        }

        private static void EnableDoubleBuffer(DataGridView grid)
        {
            try
            {
                PropertyInfo p = typeof(DataGridView).GetProperty("DoubleBuffered",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (p != null) p.SetValue(grid, true, null);
            }
            catch { }
        }

        private static Size SafeWorkingArea()
        {
            try
            {
                Rectangle r = Screen.FromPoint(Cursor.Position).WorkingArea;
                return r.Size;
            }
            catch
            {
                return new Size(1280, 720);
            }
        }

        private static void Walk(Control root, Action<Control> visit)
        {
            visit(root);
            foreach (Control child in root.Controls)
                Walk(child, visit);
        }
    }
}
