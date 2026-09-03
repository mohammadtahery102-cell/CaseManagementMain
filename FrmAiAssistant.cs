using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Drawing;
using System.Windows.Forms;
using CaseManagement.AI;
using CaseManagement.DAL;
using CaseManagement.Helpers;

namespace CaseManagement
{
    // دستیار هوشمند — فاز ۱. پنجره‌ی گفتگویِ راست‌چین. کاملاً کدنویسی‌شده
    // (بدون Designer)، دقیقاً مثلِ الگویِ FrmDashboard/StatCard موجود.
    public class FrmAiAssistant : Form
    {
        private int _conversationId = 0;
        private FlowLayoutPanel _flowChat;
        private Panel _chatScroll;
        private TextBox _txtInput;
        private Button _btnSend;
        private ListBox _lstHistory;

        public FrmAiAssistant()
        {
            Text = "دستیار هوشمند";
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = false;
            StartPosition = FormStartPosition.CenterParent;
            Width = 620;
            Height = 680;
            MinimumSize = new Size(520, 480);
            BackColor = UiTheme.Background;

            BuildLayout();
            LoadHistory();
        }

        private void BuildLayout()
        {
            Panel header = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = Color.White };
            Label lblTitle = new Label
            {
                Text = "🤖 دستیار هوشمند",
                Font = UiTheme.FontBold(11f),
                Dock = DockStyle.Top,
                Height = 26,
                TextAlign = ContentAlignment.MiddleRight,
                RightToLeft = RightToLeft.Yes
            };
            Label lblSubtitle = new Label
            {
                Text = "آفلاین — بدون اتصال به اینترنت",
                Font = UiTheme.Font(8f),
                ForeColor = Color.Gray,
                Dock = DockStyle.Top,
                Height = 18,
                TextAlign = ContentAlignment.MiddleRight,
                RightToLeft = RightToLeft.Yes
            };
            header.Controls.Add(lblSubtitle);
            header.Controls.Add(lblTitle);

            // ─── تاریخچه‌ی پرسش‌ها ───────────────────────────────────────────
            Panel historyPanel = new Panel { Dock = DockStyle.Right, Width = 190, BackColor = Color.White, Padding = new Padding(6) };
            Label lblHistory = new Label
            {
                Text = "تاریخچه",
                Font = UiTheme.FontBold(9f),
                Dock = DockStyle.Top,
                Height = 22,
                TextAlign = ContentAlignment.MiddleRight,
                RightToLeft = RightToLeft.Yes
            };
            _lstHistory = new ListBox
            {
                Dock = DockStyle.Fill,
                RightToLeft = RightToLeft.Yes,
                Font = UiTheme.Font(8.5f)
            };
            _lstHistory.DoubleClick += delegate
            {
                if (_lstHistory.SelectedItem != null)
                    _txtInput.Text = _lstHistory.SelectedItem.ToString();
            };
            historyPanel.Controls.Add(_lstHistory);
            historyPanel.Controls.Add(lblHistory);

            // ─── ورودی ────────────────────────────────────────────────────
            Panel inputPanel = new Panel { Dock = DockStyle.Bottom, Height = 52, BackColor = Color.White, Padding = new Padding(8, 8, 8, 8) };
            _btnSend = new Button { Text = "ارسال", Dock = DockStyle.Right, Width = 90, FlatStyle = FlatStyle.Flat };
            _btnSend.Click += delegate { SendCurrentInput(); };
            _txtInput = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = UiTheme.Font(10f),
                RightToLeft = RightToLeft.Yes
            };
            _txtInput.KeyDown += delegate (object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    SendCurrentInput();
                }
            };
            inputPanel.Controls.Add(_txtInput);
            inputPanel.Controls.Add(_btnSend);

            // ─── گفتگو ────────────────────────────────────────────────────
            _flowChat = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                RightToLeft = RightToLeft.Yes,
                Padding = new Padding(10)
            };
            _chatScroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = UiTheme.Background };
            _chatScroll.Controls.Add(_flowChat);

            Controls.Add(_chatScroll);
            Controls.Add(inputPanel);
            Controls.Add(historyPanel);
            Controls.Add(header);
        }

        private void SendCurrentInput()
        {
            string text = _txtInput.Text.Trim();
            if (string.IsNullOrEmpty(text)) return;

            AppendBubble(text, isUser: true);
            _txtInput.Text = "";
            _txtInput.Enabled = false;
            _btnSend.Enabled = false;

            try
            {
                AiResponse response = AiOrchestrator.Handle(text, ref _conversationId);
                AppendBubble(response.ResponseText, isUser: false);

                foreach (AiResultItem item in response.Results)
                {
                    AiResultCard card = new AiResultCard(item, OpenResult);
                    _flowChat.Controls.Add(card);
                }

                LoadHistory();
            }
            finally
            {
                _txtInput.Enabled = true;
                _btnSend.Enabled = true;
                _txtInput.Focus();
                ScrollToBottom();
            }
        }

        private void OpenResult(AiResultItem item)
        {
            if (!string.Equals(item.EntityType, "Case", StringComparison.OrdinalIgnoreCase))
                return;

            try
            {
                using (FrmCase frm = new FrmCase(item.EntityId))
                    frm.ShowDialog(this);
            }
            catch (CenterAccessDeniedException ex)
            {
                UiTheme.ShowWarning(this, ex.Message);
            }
        }

        private void AppendBubble(string text, bool isUser)
        {
            Panel bubble = new Panel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MaximumSize = new Size(420, 0),
                BackColor = isUser ? ColorTranslator.FromHtml("#DCF2E3") : Color.White,
                Padding = new Padding(10, 8, 10, 8),
                Margin = new Padding(4),
                BorderStyle = BorderStyle.FixedSingle
            };
            Label lbl = new Label
            {
                Text = text,
                AutoSize = true,
                MaximumSize = new Size(400, 0),
                Font = UiTheme.Font(9.5f),
                RightToLeft = RightToLeft.Yes,
                TextAlign = ContentAlignment.MiddleRight
            };
            bubble.Controls.Add(lbl);
            _flowChat.Controls.Add(bubble);
            ScrollToBottom();
        }

        private void ScrollToBottom()
        {
            try
            {
                _chatScroll.VerticalScroll.Value = _chatScroll.VerticalScroll.Maximum;
                _chatScroll.PerformLayout();
            }
            catch { /* اسکرول صرفاً زیبایی‌شناختی است */ }
        }

        private void LoadHistory()
        {
            try
            {
                _lstHistory.Items.Clear();
                DatabaseHelper db = new DatabaseHelper();
                using (SQLiteConnection con = db.GetConnection())
                using (SQLiteCommand cmd = new SQLiteCommand(@"
SELECT DISTINCT RawQuery FROM AiIntentLog
WHERE (@UserID = 0 OR UserID = @UserID)
ORDER BY IntentLogID DESC LIMIT 20;", con))
                {
                    cmd.Parameters.AddWithValue("@UserID", SecurityContext.UserId);
                    con.Open();
                    using (SQLiteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            _lstHistory.Items.Add(reader["RawQuery"].ToString());
                    }
                }
            }
            catch { /* تاریخچه غیرحیاتی است */ }
        }
    }
}
