using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace CaseManagement.Helpers
{
    // هاب مرکز گزارشات — دو سطح منو: گروه «گزارشات» و دسته. خودِ گزارش‌ها روی صفحه هستند.
    public sealed class FrmReportingCenter : Form
    {
        private readonly string _startCategory;
        private ListBox _nav;
        private Panel _host;
        private TextBox _search;
        private Label _heading;

        public FrmReportingCenter()
            : this(ReportingCatalog.Dashboards)
        {
        }

        public FrmReportingCenter(string categoryKey)
        {
            _startCategory = string.IsNullOrWhiteSpace(categoryKey)
                ? ReportingCatalog.Dashboards
                : categoryKey;

            Text = "گزارشات  ·  " + ProductBranding.CommercialName;
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor = UiTheme.Background;
            Font = UiTheme.Font(UiTheme.SizeBody);
            UiTheme.MakeMainWindow(this, 1100, 680);

            _nav = new ListBox
            {
                Dock = DockStyle.Right,
                Width = 220,
                IntegralHeight = false,
                Font = UiTheme.Font(10.5F)
            };
            for (int i = 0; i < ReportingCatalog.CategoryOrder.Length; i++)
                _nav.Items.Add(ReportingCatalog.CategoryTitle(ReportingCatalog.CategoryOrder[i]));
            _nav.SelectedIndexChanged += delegate { RenderCategory(); };

            Panel top = new Panel { Dock = DockStyle.Top, Height = 52, Padding = new Padding(12, 8, 12, 8), BackColor = UiTheme.CardBack };
            _search = new TextBox { Dock = DockStyle.Fill, Font = UiTheme.Font(UiTheme.SizeBody) };
            UiTheme.StyleTextBox(_search);
            _search.TextChanged += delegate { RenderCategory(); };
            Label searchLbl = new Label
            {
                Text = "جستجوی گزارش",
                Dock = DockStyle.Right,
                Width = 110,
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(0, 0, 8, 0)
            };
            top.Controls.Add(_search);
            top.Controls.Add(searchLbl);

            _heading = new Label
            {
                Dock = DockStyle.Top,
                Height = 36,
                Font = UiTheme.FontBold(12.5F),
                ForeColor = UiTheme.TextDark,
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(16, 0, 16, 0)
            };

            _host = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(12) };

            Controls.Add(_host);
            Controls.Add(_heading);
            Controls.Add(top);
            Controls.Add(_nav);
            Controls.Add(ErpFormChrome.Header("گزارشات"));

            int start = IndexOfCategory(_startCategory);
            _nav.SelectedIndex = start >= 0 ? start : 0;
        }

        private static int IndexOfCategory(string key)
        {
            for (int i = 0; i < ReportingCatalog.CategoryOrder.Length; i++)
            {
                if (ReportingCatalog.CategoryOrder[i] == key)
                    return i;
            }
            return -1;
        }

        private string SelectedCategoryKey()
        {
            int i = _nav.SelectedIndex;
            if (i < 0 || i >= ReportingCatalog.CategoryOrder.Length)
                return ReportingCatalog.Dashboards;
            return ReportingCatalog.CategoryOrder[i];
        }

        private void RenderCategory()
        {
            _host.Controls.Clear();
            string q = _search == null ? "" : _search.Text.Trim();
            IList<ReportNavItem> items;
            if (q.Length > 0)
            {
                items = ReportingCatalog.Search(q);
                _heading.Text = items.Count == 0
                    ? "نتیجه‌ای یافت نشد"
                    : ("نتایج جستجو  ·  " + items.Count + " گزارش");
            }
            else
            {
                string key = SelectedCategoryKey();
                items = ReportingCatalog.ForCategory(key);
                _heading.Text = ReportingCatalog.CategoryTitle(key);
            }

            FlowLayoutPanel flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = true,
                Padding = new Padding(4)
            };

            string lastSection = "\0";
            for (int i = 0; i < items.Count; i++)
            {
                ReportNavItem it = items[i];
                if (!string.IsNullOrWhiteSpace(it.Section) && it.Section != lastSection)
                {
                    lastSection = it.Section;
                    flow.Controls.Add(SectionLabel(it.Section));
                }
                flow.Controls.Add(ReportCard(it));
            }
            _host.Controls.Add(flow);
        }

        private static Control SectionLabel(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = false,
                Width = 980,
                Height = 28,
                Font = UiTheme.FontBold(10.5F),
                ForeColor = UiTheme.TextMuted,
                TextAlign = ContentAlignment.MiddleRight,
                Margin = new Padding(8, 10, 8, 2)
            };
        }

        private Control ReportCard(ReportNavItem item)
        {
            ReportNavItem copy = item;
            Panel card = new Panel
            {
                Width = 320,
                Height = 88,
                Margin = new Padding(8),
                BackColor = item.Ready ? UiTheme.CardBack : ColorTranslator.FromHtml("#F1F5F9"),
                Cursor = Cursors.Hand,
                Padding = new Padding(12)
            };
            UiTheme.RoundCorners(card, 10);

            Label title = new Label
            {
                Text = item.Title,
                Dock = DockStyle.Top,
                Height = 32,
                Font = UiTheme.FontBold(11F),
                ForeColor = item.Ready ? UiTheme.TextDark : UiTheme.TextMuted,
                TextAlign = ContentAlignment.MiddleRight
            };
            Label status = new Label
            {
                Text = item.Ready ? "آماده" : ReportingCatalog.SoonHint,
                Dock = DockStyle.Fill,
                Font = UiTheme.Font(UiTheme.SizeSmall),
                ForeColor = item.Ready ? ColorTranslator.FromHtml("#15803D") : UiTheme.TextMuted,
                TextAlign = ContentAlignment.TopRight
            };
            card.Controls.Add(status);
            card.Controls.Add(title);
            EventHandler open = delegate { OpenItem(copy); };
            card.Click += open;
            title.Click += open;
            status.Click += open;
            return card;
        }

        private void OpenItem(ReportNavItem item)
        {
            if (item == null) return;
            if (!item.Ready || string.IsNullOrWhiteSpace(item.Dest))
            {
                using (FrmErpPlaceholder frm = new FrmErpPlaceholder(item.Title))
                    frm.ShowDialog(this);
                return;
            }

            if (item.Dest == "home" || item.Dest == "audit-tab")
            {
                FrmDashboard dash = FindDashboard();
                Close();
                if (dash != null)
                {
                    if (item.Dest == "home") dash.ShowErpHome();
                    else dash.ShowAuditTab();
                }
                return;
            }

            ErpWorkspace.Open(this, item.Dest);
        }

        private FrmDashboard FindDashboard()
        {
            Form f = Owner;
            while (f != null)
            {
                FrmDashboard d = f as FrmDashboard;
                if (d != null) return d;
                f = f.Owner;
            }
            return null;
        }
    }
}
