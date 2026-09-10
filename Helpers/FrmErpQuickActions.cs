using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace CaseManagement.Helpers
{
    public sealed class FrmErpQuickActions : Form
    {
        private readonly List<Row> _rows = new List<Row>();

        public FrmErpQuickActions()
        {
            Text = "مدیریت دکمه‌های سریع";
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = UiTheme.Background;
            Font = UiTheme.Font(UiTheme.SizeBody);
            ClientSize = new Size(720, 420);

            Label hint = new Label
            {
                Dock = DockStyle.Top,
                Height = 40,
                Padding = new Padding(16, 8, 16, 0),
                Text = "پنج دکمه دسترسی سریع داشبورد. میانبرها در صفحه داشبورد فعال‌اند.",
                ForeColor = UiTheme.TextMuted
            };

            TableLayoutPanel grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 6,
                RowCount = 6,
                Padding = new Padding(16)
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22F));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 14F));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16F));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22F));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 14F));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12F));
            string[] heads = { "عنوان", "آیکون", "رنگ", "مقصد", "میانبر", "فعال" };
            for (int c = 0; c < heads.Length; c++)
                grid.Controls.Add(new Label { Text = heads[c], Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = UiTheme.FontBold(UiTheme.SizeSmall) }, c, 0);

            List<ErpQuickActionDto> data = ErpQuickActions.Load();
            for (int i = 0; i < 5; i++)
            {
                Row row = BuildRow(data[i]);
                _rows.Add(row);
                grid.Controls.Add(row.Title, 0, i + 1);
                grid.Controls.Add(row.Icon, 1, i + 1);
                grid.Controls.Add(row.Color, 2, i + 1);
                grid.Controls.Add(row.Dest, 3, i + 1);
                grid.Controls.Add(row.Shortcut, 4, i + 1);
                grid.Controls.Add(row.Enabled, 5, i + 1);
            }

            Button save = UiTheme.CreateButton("ذخیره", "", UiTheme.Primary);
            save.Dock = DockStyle.Bottom;
            save.Height = 44;
            save.Click += delegate { Persist(); Close(); };

            Controls.Add(grid);
            Controls.Add(save);
            Controls.Add(hint);
        }

        private static Row BuildRow(ErpQuickActionDto src)
        {
            var row = new Row();
            row.Title = new TextBox { Dock = DockStyle.Fill, Text = src.title };
            UiTheme.StyleTextBox(row.Title);
            row.Icon = Combo(new[] { "journal", "invoice", "receive", "chart", "people", "cash", "sale", "buy" }, src.icon);
            row.Color = Combo(new[] { "#2563EB", "#0F766E", "#D97706", "#7C3AED", "#0891B2", "#DC2626" }, src.color);
            row.Dest = Combo(new[] { "journal", "invoice", "receive", "trial", "crm", "ledger", "purchase", "inventory", "cash", "settings" }, src.dest);
            row.Shortcut = Combo(new[] { "F1", "F2", "F3", "F4", "F5" }, src.shortcut);
            row.Enabled = new CheckBox { Dock = DockStyle.Fill, Checked = src.enabled, CheckAlign = ContentAlignment.MiddleCenter };
            return row;
        }

        private static ComboBox Combo(string[] items, string selected)
        {
            ComboBox cmb = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            cmb.Items.AddRange(items);
            int idx = Array.IndexOf(items, selected ?? "");
            cmb.SelectedIndex = idx >= 0 ? idx : 0;
            return cmb;
        }

        private void Persist()
        {
            var list = new List<ErpQuickActionDto>();
            for (int i = 0; i < _rows.Count; i++)
            {
                Row r = _rows[i];
                list.Add(new ErpQuickActionDto
                {
                    enabled = r.Enabled.Checked,
                    title = r.Title.Text.Trim(),
                    icon = Convert.ToString(r.Icon.SelectedItem),
                    color = Convert.ToString(r.Color.SelectedItem),
                    dest = Convert.ToString(r.Dest.SelectedItem),
                    shortcut = Convert.ToString(r.Shortcut.SelectedItem)
                });
            }
            ErpQuickActions.Save(list);
        }

        private sealed class Row
        {
            public TextBox Title;
            public ComboBox Icon;
            public ComboBox Color;
            public ComboBox Dest;
            public ComboBox Shortcut;
            public CheckBox Enabled;
        }
    }
}
