using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace CaseManagement.Helpers
{
    public sealed class ErpDashboardHost : Panel
    {
        public event Action<string> ActionRequested;

        private WebView2 _web;
        private bool _ready;
        private bool _started;
        private string _pendingJson;

        public ErpDashboardHost()
        {
            Dock = DockStyle.Fill;
            BackColor = ColorTranslator.FromHtml("#F5F7FB");
            _web = new WebView2 { Dock = DockStyle.Fill };
            Controls.Add(_web);
        }

        public void BeginLoad()
        {
            if (_started) return;
            _started = true;
            if (IsHandleCreated)
                LoadAsync();
            else
                HandleCreated += delegate { LoadAsync(); };
        }

        public void ReloadData()
        {
            string json = SerializePayload();
            _pendingJson = json;
            if (_ready && _web != null && _web.CoreWebView2 != null)
                PushJson(json);
        }

        private async void LoadAsync()
        {
            try
            {
                if (IsDisposed) return;
                string runtime = CoreWebView2Environment.GetAvailableBrowserVersionString();
                if (string.IsNullOrWhiteSpace(runtime))
                    throw new WebView2RuntimeNotFoundException();

                string userData = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "CaseManagement", "WebView2Dashboard");
                Directory.CreateDirectory(userData);
                CoreWebView2Environment env = await CoreWebView2Environment.CreateAsync(null, userData);
                if (IsDisposed) return;
                await _web.EnsureCoreWebView2Async(env);
                if (IsDisposed || _web.CoreWebView2 == null) return;

                string folder = Path.Combine(Application.StartupPath, "Ui", "ErpDashboard");
                if (!File.Exists(Path.Combine(folder, "index.html")))
                    throw new FileNotFoundException("dashboard assets missing", folder);

                _web.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "erp.dashboard.local", folder, CoreWebView2HostResourceAccessKind.Allow);
                _web.CoreWebView2.WebMessageReceived += OnMessage;
                _web.CoreWebView2.NavigationCompleted += OnNavigated;
                _web.CoreWebView2.Navigate("https://erp.dashboard.local/index.html");
            }
            catch
            {
                ShowFallback();
            }
        }

        private void OnNavigated(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (IsDisposed) return;
            _ready = e.IsSuccess;
            if (!_ready)
            {
                ShowFallback();
                return;
            }
            PushJson(_pendingJson ?? SerializePayload());
        }

        private void OnMessage(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string raw = e.TryGetWebMessageAsString();
            if (string.IsNullOrWhiteSpace(raw)) return;
            try
            {
                var map = new JavaScriptSerializer().Deserialize<System.Collections.Generic.Dictionary<string, object>>(raw);
                object action;
                if (map != null && map.TryGetValue("action", out action) && ActionRequested != null)
                    ActionRequested(Convert.ToString(action));
            }
            catch { }
        }

        private void PushJson(string json)
        {
            if (_web == null || _web.CoreWebView2 == null) return;
            string script = "window.__ERP_DASHBOARD__=" + json +
                "; if (window.renderDashboard) renderDashboard(window.__ERP_DASHBOARD__);";
            _web.CoreWebView2.ExecuteScriptAsync(script);
        }

        private static string SerializePayload()
        {
            return new JavaScriptSerializer().Serialize(ErpDashboardMetrics.LoadPayload());
        }

        private void ShowFallback()
        {
            if (IsDisposed) return;
            Controls.Clear();
            _web = null;
            _ready = false;
            ErpDashboardSnapshot snap = ErpDashboardMetrics.Load();

            Panel host = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(24),
                BackColor = ColorTranslator.FromHtml("#F5F7FB")
            };
            Label note = new Label
            {
                Dock = DockStyle.Top,
                Height = 36,
                Text = "داشبورد فشرده — برای نمای کامل، Microsoft Edge WebView2 Runtime را نصب کنید.",
                Font = UiTheme.Font(12F),
                ForeColor = UiTheme.TextMuted,
                TextAlign = ContentAlignment.MiddleRight
            };
            TableLayoutPanel grid = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 280,
                ColumnCount = 3,
                RowCount = 2,
                Padding = new Padding(0, 8, 0, 0)
            };
            for (int c = 0; c < 3; c++)
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            grid.Controls.Add(FallbackCard("موجودی بانک‌ها", snap.Bank), 0, 0);
            grid.Controls.Add(FallbackCard("صندوق نقدی", snap.Cash), 1, 0);
            grid.Controls.Add(FallbackCard("سود خالص", snap.ProfitLoss), 2, 0);
            grid.Controls.Add(FallbackCard("بدهکاران", snap.Receivables), 0, 1);
            grid.Controls.Add(FallbackCard("بستانکاران", snap.Payables), 1, 1);
            grid.Controls.Add(FallbackCard("فاکتورهای باز", snap.OpenInvoices), 2, 1);
            host.Controls.Add(grid);
            host.Controls.Add(note);
            Controls.Add(host);
        }

        private static Panel FallbackCard(string title, string value)
        {
            Panel card = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(8),
                BackColor = Color.White,
                Padding = new Padding(16)
            };
            card.Controls.Add(new Label
            {
                Text = string.IsNullOrWhiteSpace(value) ? ProductBranding.MetricUnavailable : value,
                Dock = DockStyle.Fill,
                Font = UiTheme.FontBold(16F),
                ForeColor = ColorTranslator.FromHtml("#0F172A"),
                TextAlign = ContentAlignment.MiddleRight
            });
            card.Controls.Add(new Label
            {
                Text = title,
                Dock = DockStyle.Top,
                Height = 24,
                Font = UiTheme.Font(12F),
                ForeColor = ColorTranslator.FromHtml("#64748B"),
                TextAlign = ContentAlignment.MiddleRight
            });
            return card;
        }
    }
}
