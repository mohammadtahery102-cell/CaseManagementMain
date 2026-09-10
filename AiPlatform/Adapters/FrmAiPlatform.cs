using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;
using CaseManagement.Accounting.Ledger.Adapters;
using CaseManagement.Accounting.Ledger.Application;
using CaseManagement.AiPlatform.Application;
using CaseManagement.AiPlatform.Domain;
using CaseManagement.Helpers;

namespace CaseManagement.AiPlatform.Adapters
{
    public sealed class FrmAiPlatform : Form
    {
        private readonly ILedgerIdentity _identity;
        private readonly AiLicenseService _license;
        private readonly AiDashboardService _dashboard;
        private readonly SalesAnalysisService _sales;
        private readonly PurchaseAnalysisService _purchase;
        private readonly InventoryAnalysisService _inventory;
        private readonly FinancialAnalysisService _financial;
        private readonly CustomerAnalysisService _customer;
        private readonly AiChatService _chat;
        private readonly AiSettingsService _settings;
        private readonly AiExecutiveDashboardService _execDash;
        private readonly AiForecastService _forecast;
        private readonly AiAlertEngine _alerts;
        private readonly AiReportGenerator _reports;

        private ListBox _nav;
        private Panel _host;
        private DataGridView _grid;
        private TextBox _chatLog;
        private TextBox _chatInput;
        private ComboBox _cmbProvider;
        private ComboBox _cmbLicense;
        private TextBox _txtEndpoint;
        private TextBox _txtModel;
        private TextBox _txtApiKey;
        private TextBox _txtTimeout;
        private TextBox _txtMaxTokens;
        private TextBox _txtTemperature;
        private long _conversationId;
        private int _section;

        public FrmAiPlatform()
            : this(null)
        {
        }

        public FrmAiPlatform(string startSection)
        {
            _identity = DesktopLedgerIdentity.FromSession();
            _license = new AiLicenseService();
            _dashboard = new AiDashboardService();
            _sales = new SalesAnalysisService();
            _purchase = new PurchaseAnalysisService();
            _inventory = new InventoryAnalysisService();
            _financial = new FinancialAnalysisService();
            _customer = new CustomerAnalysisService();
            _chat = new AiChatService();
            _settings = new AiSettingsService();
            _execDash = new AiExecutiveDashboardService();
            _forecast = new AiForecastService();
            _alerts = new AiAlertEngine();
            _reports = new AiReportGenerator();

            Text = "دستیار هوشمند گنجینه";
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
            _nav.Items.AddRange(new object[]
            {
                "داشبورد هوشمند",
                "داشبورد مدیریتی",
                "تحلیل فروش",
                "تحلیل خرید",
                "تحلیل موجودی",
                "تحلیل مالی",
                "تحلیل مشتریان",
                "پیش‌بینی",
                "هشدارهای هوشمند",
                "گزارش‌ها",
                "گفتگو با هوش مصنوعی",
                "تنظیمات هوش مصنوعی"
            });
            _nav.SelectedIndexChanged += delegate { ShowSection(_nav.SelectedIndex); };

            _host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
            Controls.Add(_host);
            Controls.Add(_nav);
            Controls.Add(ErpFormChrome.Header("دستیار هوشمند گنجینه"));
            int start = 0;
            if (!string.IsNullOrWhiteSpace(startSection))
            {
                int idx = _nav.Items.IndexOf(startSection);
                if (idx >= 0) start = idx;
            }
            _nav.SelectedIndex = start;
            ErpAccess.RequirePermission(this, AiPermissions.View);
        }

        private void ShowSection(int index)
        {
            _section = index;
            _host.Controls.Clear();
            string name = _nav.SelectedItem == null ? "" : _nav.SelectedItem.ToString();
            if (!_license.CanView(_identity) && name != "تنظیمات هوش مصنوعی")
            {
                _host.Controls.Add(Hint("برای مشاهده این بخش مجوز AI.View و لایسنس Free لازم است."));
                return;
            }
            if (name == "داشبورد هوشمند") BindGrid(DashboardTable(), null);
            else if (name == "داشبورد مدیریتی") BindGrid(ExecutiveTable(), null);
            else if (name == "تحلیل فروش")
            {
                AiAnalysisReport r = _sales.Analyze(_identity);
                BindGrid(ReportTable(r), r);
            }
            else if (name == "تحلیل خرید")
            {
                AiAnalysisReport r = _purchase.Analyze(_identity);
                BindGrid(ReportTable(r), r);
            }
            else if (name == "تحلیل موجودی")
            {
                AiAnalysisReport r = _inventory.Analyze(_identity);
                BindGrid(ReportTable(r), r);
            }
            else if (name == "تحلیل مالی")
            {
                AiAnalysisReport r = _financial.Analyze(_identity);
                BindGrid(ReportTable(r), r);
            }
            else if (name == "تحلیل مشتریان")
            {
                AiAnalysisReport r = _customer.Analyze(_identity);
                BindGrid(ReportTable(r), r);
            }
            else if (name == "پیش‌بینی")
            {
                AiAnalysisReport r = _forecast.Forecast(_identity);
                BindGrid(ReportTable(r), r);
            }
            else if (name == "هشدارهای هوشمند") ShowAlerts();
            else if (name == "گزارش‌ها") ShowReports();
            else if (name == "گفتگو با هوش مصنوعی") ShowChat();
            else ShowSettings();
        }

        private void BindGrid(System.Data.DataTable table, AiAnalysisReport export)
        {
            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            UiTheme.StyleGrid(_grid);
            _grid.DataSource = table;
            if (export != null && _license.CanReports(_identity))
            {
                FlowLayoutPanel bar = ErpFormChrome.Toolbar();
                Button exp = UiTheme.CreateButton("خروجی متن", "↓", UiTheme.PrimaryLight);
                AiAnalysisReport copy = export;
                exp.Click += delegate { ExportText(AiReportGenerator.ToText(copy)); };
                bar.Controls.Add(exp);
                _host.Controls.Add(ErpFormChrome.WrapGrid(_grid, ProductBranding.EmptyList));
                _host.Controls.Add(bar);
                return;
            }
            _host.Controls.Add(ErpFormChrome.WrapGrid(_grid, ProductBranding.EmptyList));
        }

        private System.Data.DataTable DashboardTable()
        {
            AiDashboardSnapshot snap = _dashboard.GetSnapshot(_identity);
            System.Data.DataTable t = new System.Data.DataTable();
            t.Columns.Add("بخش");
            t.Columns.Add("شاخص");
            t.Columns.Add("مقدار");
            AddRows(t, "داشبورد", snap.Widgets);
            AddRows(t, "هشدار ERP", snap.Alerts);
            if (_license.CanExecutive(_identity))
                AddRows(t, "مدیریتی", snap.Executive);
            return t;
        }

        private System.Data.DataTable ExecutiveTable()
        {
            AiExecutiveSnapshot snap = _execDash.Get(_identity);
            System.Data.DataTable t = new System.Data.DataTable();
            t.Columns.Add("شاخص");
            t.Columns.Add("مقدار");
            if (snap.Widgets != null)
                for (int i = 0; i < snap.Widgets.Count; i++)
                    t.Rows.Add(snap.Widgets[i].Label, snap.Widgets[i].Value);
            return t;
        }

        private void ShowAlerts()
        {
            if (!_license.CanAlerts(_identity))
            {
                _host.Controls.Add(Hint("هشدارهای هوشمند نیازمند لایسنس Executive است."));
                return;
            }
            IList<AiAlert> list = _alerts.Evaluate(_identity);
            System.Data.DataTable t = new System.Data.DataTable();
            t.Columns.Add("شدت");
            t.Columns.Add("عنوان");
            t.Columns.Add("شرح");
            for (int i = 0; i < list.Count; i++)
                t.Rows.Add(list[i].Severity, list[i].Title, list[i].Detail);
            BindGrid(t, null);
        }

        private void ShowReports()
        {
            if (!_license.CanReports(_identity))
            {
                _host.Controls.Add(Hint("گزارش‌ها نیازمند لایسنس Pro و مجوز AI.Analytics است."));
                return;
            }
            FlowLayoutPanel bar = ErpFormChrome.Toolbar();
            ComboBox cmb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240 };
            cmb.Items.AddRange(new object[] { "Financial", "Sales", "Purchase", "Inventory", "Executive" });
            cmb.SelectedIndex = 0;
            Button gen = UiTheme.CreateButton("تولید و خروجی", "↓", UiTheme.PrimaryLight);
            gen.Click += delegate
            {
                string kind = cmb.SelectedItem == null ? "Financial" : cmb.SelectedItem.ToString();
                if (kind == "Executive" && !_license.CanExecutive(_identity))
                {
                    MessageBox.Show(this, "خلاصه مدیریتی نیازمند لایسنس Executive است.", Text);
                    return;
                }
                ExportText(_reports.Generate(kind, _identity));
            };
            bar.Controls.Add(cmb);
            bar.Controls.Add(gen);
            Label hint = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Text = "گزارش متنی آمادهٔ خروجی برای مالکان، مدیران، حسابداران و اپراتورها."
            };
            _host.Controls.Add(hint);
            _host.Controls.Add(bar);
        }

        private void ExportText(string body)
        {
            using (SaveFileDialog dlg = new SaveFileDialog())
            {
                dlg.Filter = "Text (*.txt)|*.txt";
                dlg.FileName = "ganjineh-ai-report.txt";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                File.WriteAllText(dlg.FileName, body ?? "", Encoding.UTF8);
            }
        }

        private static void AddRows(System.Data.DataTable t, string section, IList<AiMetricRow> rows)
        {
            if (rows == null) return;
            for (int i = 0; i < rows.Count; i++)
                t.Rows.Add(section, rows[i].Label, rows[i].Value);
        }

        private static System.Data.DataTable ReportTable(AiAnalysisReport report)
        {
            System.Data.DataTable t = new System.Data.DataTable();
            t.Columns.Add("شاخص");
            t.Columns.Add("مقدار");
            if (report == null || report.Rows == null) return t;
            for (int i = 0; i < report.Rows.Count; i++)
                t.Rows.Add(report.Rows[i].Label, report.Rows[i].Value);
            return t;
        }

        private void ShowChat()
        {
            if (!_license.CanChat(_identity))
            {
                _host.Controls.Add(Hint("گفتگو نیازمند لایسنس Pro و مجوز AI.Chat است."));
                return;
            }
            _chatLog = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = UiTheme.Font(10F)
            };
            Panel bottom = new Panel { Dock = DockStyle.Bottom, Height = 48 };
            _chatInput = new TextBox { Dock = DockStyle.Fill };
            Button send = UiTheme.CreateButton("ارسال", "➤", UiTheme.PrimaryLight);
            send.Dock = DockStyle.Left;
            send.Width = 110;
            send.Click += delegate { SendChat(); };
            bottom.Controls.Add(_chatInput);
            bottom.Controls.Add(send);
            _host.Controls.Add(_chatLog);
            _host.Controls.Add(bottom);
        }

        private void SendChat()
        {
            if (_chatInput == null) return;
            string q = _chatInput.Text.Trim();
            if (q.Length == 0) return;
            AiResponse r = _chat.Ask(new AiPrompt { Text = q, ConversationId = _conversationId }, _identity);
            _conversationId = r.ConversationId;
            StringBuilder sb = new StringBuilder(_chatLog.Text);
            sb.AppendLine("شما: " + q);
            sb.AppendLine("دستیار: " + (r.Ok ? r.Text : r.Message));
            sb.AppendLine();
            _chatLog.Text = sb.ToString();
            _chatInput.Clear();
        }

        private void ShowSettings()
        {
            if (!_license.CanSettings(_identity))
            {
                _host.Controls.Add(Hint("تنظیمات نیازمند مجوز AI.Settings است."));
                return;
            }
            AiSetting s = _settings.Get(_identity);
            if (s == null)
            {
                _host.Controls.Add(Hint("تنظیمات شرکت یافت نشد."));
                return;
            }
            TableLayoutPanel table = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 360,
                ColumnCount = 2,
                RowCount = 9,
                Padding = new Padding(8)
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65F));
            _cmbProvider = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            _cmbProvider.Items.AddRange(new object[]
            {
                AiProviderNames.None, AiProviderNames.OpenAI, AiProviderNames.Claude,
                AiProviderNames.Gemini, AiProviderNames.Grok, AiProviderNames.Fake
            });
            SelectCombo(_cmbProvider, s.Provider);
            _txtEndpoint = Field(s.Endpoint);
            _txtModel = Field(s.Model);
            _txtApiKey = Field(s.ApiKey);
            _txtTimeout = Field(s.TimeoutMs.ToString(CultureInfo.InvariantCulture));
            _txtMaxTokens = Field(s.MaxTokens.ToString(CultureInfo.InvariantCulture));
            _txtTemperature = Field(s.Temperature.ToString("0.###", CultureInfo.InvariantCulture));
            _cmbLicense = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            _cmbLicense.Items.AddRange(new object[] { AiLicenseLevels.Free, AiLicenseLevels.Pro, AiLicenseLevels.Executive });
            SelectCombo(_cmbLicense, s.LicenseLevel);

            AddRow(table, 0, "ارائه‌دهنده", _cmbProvider);
            AddRow(table, 1, "Endpoint", _txtEndpoint);
            AddRow(table, 2, "Model", _txtModel);
            AddRow(table, 3, "کلید API", _txtApiKey);
            AddRow(table, 4, "Timeout (ms)", _txtTimeout);
            AddRow(table, 5, "Max Tokens", _txtMaxTokens);
            AddRow(table, 6, "Temperature", _txtTemperature);
            AddRow(table, 7, "License Level", _cmbLicense);

            Button save = UiTheme.CreateButton("ذخیره", "💾", UiTheme.PrimaryLight);
            save.Click += delegate { SaveSettings(s); };
            table.Controls.Add(save, 1, 8);
            _host.Controls.Add(table);
        }

        private void SaveSettings(AiSetting current)
        {
            int timeout;
            int maxTokens;
            double temp;
            if (!int.TryParse(_txtTimeout.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out timeout)) timeout = 30000;
            if (!int.TryParse(_txtMaxTokens.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out maxTokens)) maxTokens = 1024;
            if (!double.TryParse(_txtTemperature.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out temp)) temp = 0.2;
            current.Provider = _cmbProvider.SelectedItem == null ? AiProviderNames.None : _cmbProvider.SelectedItem.ToString();
            current.Endpoint = _txtEndpoint.Text.Trim();
            current.Model = _txtModel.Text.Trim();
            current.ApiKey = _txtApiKey.Text.Trim();
            current.TimeoutMs = timeout;
            current.MaxTokens = maxTokens;
            current.Temperature = temp;
            current.LicenseLevel = _cmbLicense.SelectedItem == null ? AiLicenseLevels.Free : _cmbLicense.SelectedItem.ToString();
            bool ok = _settings.Save(current, _identity);
            MessageBox.Show(this, ok ? ProductBranding.SavedOk : "ذخیره انجام نشد (نسخه ردیف یا مجوز).", Text,
                MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            if (ok) ShowSection(_section);
        }

        private static void AddRow(TableLayoutPanel table, int row, string label, Control editor)
        {
            table.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 0, row);
            table.Controls.Add(editor, 1, row);
        }

        private static TextBox Field(string value)
        {
            return new TextBox { Dock = DockStyle.Fill, Text = value ?? "" };
        }

        private static void SelectCombo(ComboBox cmb, string value)
        {
            int idx = 0;
            for (int i = 0; i < cmb.Items.Count; i++)
            {
                if (string.Equals(Convert.ToString(cmb.Items[i]), value, StringComparison.OrdinalIgnoreCase))
                {
                    idx = i;
                    break;
                }
            }
            cmb.SelectedIndex = idx;
        }

        private static Label Hint(string text)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = UiTheme.TextMuted
            };
        }
    }
}
