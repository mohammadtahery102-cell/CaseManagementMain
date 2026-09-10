using System;
using System.Drawing;
using System.Windows.Forms;

namespace CaseManagement.Helpers
{
    // ظاهر مشترک فرم‌های ماژول ERP — فقط چیدمان. منطق فرم دست‌نخورده است.
    public static class ErpFormChrome
    {
        public static Panel Header(string title)
        {
            Panel banner = new Panel
            {
                Dock = DockStyle.Top,
                Height = 54,
                BackColor = UiTheme.PrimaryDark
            };
            banner.Controls.Add(new Label
            {
                Text = title ?? "",
                Dock = DockStyle.Fill,
                ForeColor = Color.White,
                Font = UiTheme.FontBold(15F),
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(0, 0, 20, 0)
            });
            return banner;
        }

        public static FlowLayoutPanel Toolbar()
        {
            return new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 48,
                RightToLeft = RightToLeft.Yes,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(10, 6, 10, 6),
                BackColor = UiTheme.CardBack
            };
        }

        public static Button RefreshButton(EventHandler onClick)
        {
            Button b = UiTheme.CreateButton("تازه‌سازی", "↻", UiTheme.PrimaryLight);
            b.Size = new Size(120, 34);
            if (onClick != null) b.Click += onClick;
            return b;
        }

        public static void SelectListItem(ComboBox combo, string kind)
        {
            if (combo == null || string.IsNullOrWhiteSpace(kind)) return;
            int idx = combo.Items.IndexOf(kind);
            if (idx >= 0) combo.SelectedIndex = idx;
        }

        public static void SelectListItem(ListBox list, string kind)
        {
            if (list == null || string.IsNullOrWhiteSpace(kind)) return;
            int idx = list.Items.IndexOf(kind);
            if (idx >= 0) list.SelectedIndex = idx;
        }

        public static Control WrapGrid(DataGridView grid, string emptyText)
        {
            Panel host = new Panel { Dock = DockStyle.Fill };
            Label empty = new Label
            {
                Text = string.IsNullOrWhiteSpace(emptyText) ? ProductBranding.EmptyList : emptyText,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = UiTheme.TextMuted,
                Font = UiTheme.Font(10.5F),
                Visible = false
            };
            grid.Dock = DockStyle.Fill;
            host.Controls.Add(empty);
            host.Controls.Add(grid);
            grid.DataBindingComplete += delegate
            {
                bool has = grid.Rows.Count > 0;
                grid.Visible = has;
                empty.Visible = !has;
            };
            return host;
        }
    }
}
