using CaseManagement.DAL;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace CaseManagement.Helpers
{
    // ═════════════════════════════════════════════════════════════════════════
    // مرکزِ فورم‌های رسمیِ پرونده — جایگزینِ منویِ کرکره‌ایِ قبلی.
    //
    // آموزش — چه چیزی عوض شد و چرا:
    //   • قبلاً: یک ContextMenuStrip با پنج عنوان. کاربر نمی‌دانست هر فورم
    //     چیست، نمی‌دید کدام فورم قبلاً امضا و ضمیمه شده، و برای هر خروجی
    //     باید در SaveFileDialog دستی پوشه پیدا می‌کرد.
    //   • حالا: یک فهرستِ کارت‌مانند با توضیح و وضعیتِ «ضمیمه شده / نشده /
    //     قالب موجود نیست»، پنلِ «چه چیزی خودکار پر می‌شود»، خانه‌های دستی
    //     با علامتِ اجباری، و ذخیرهٔ خودکار در پوشهٔ خودِ پرونده + بازکردنِ
    //     فایل. مسیرِ دلخواه هنوز هست، ولی دیگر پیش‌فرض نیست.
    //
    // این فرم هیچ SQLای ندارد جز از راهِ CaseFormTokens/CaseOfficialForms، و
    // ثبتِ سندِ امضاشده را به FrmDocxForm.AttachToCase واگذار می‌کند — همان
    // متدِ آزموده‌ای که قلّاب‌های sync/version/timeline را می‌زند.
    // ═════════════════════════════════════════════════════════════════════════
    public class FrmOfficialForms : Form
    {
        private readonly DatabaseHelper _db;
        private readonly int _caseId;
        private readonly string _caseCode;
        private readonly string _preselectKey;

        private Dictionary<string, string> _tokens = new Dictionary<string, string>(StringComparer.Ordinal);
        private List<CaseFormDef> _forms = new List<CaseFormDef>();
        private CaseFormDef _current;
        private string _lastOutputPath = "";

        private readonly Dictionary<string, Control> _editors =
            new Dictionary<string, Control>(StringComparer.Ordinal);

        // پس از ضمیمه‌شدنِ نسخهٔ امضاشده صدا زده می‌شود (رفرشِ فهرستِ اسناد).
        public Action OnDocumentAttached;

        private ListBox lstForms;
        private Label lblCaseLine;
        private Label lblFormTitle;
        private Label lblFormDesc;
        private Label lblAutoTitle;
        private Panel panAuto;
        private Label lblFieldsTitle;
        private Panel panFields;
        private Label lblStatus;
        private Button btnWord, btnPdf, btnSaveAs, btnAttach, btnOpenCustom, btnClose;

        // آموزش — چیدمانِ راست‌به‌چپ: وقتی RightToLeft = Yes باشد، WinForms
        // مقدارِ ContentAlignment را *آینه* می‌کند. پس برای اینکه متنِ یک
        // Label واقعاً سمتِ راست بنشیند باید MiddleLeft داده شود، نه
        // MiddleRight (با MiddleRight متن به چپ می‌رود — در وارسیِ تصویری
        // دیده شد). همین قاعده برای TopRight/BottomRight هم هست.
        private const int ItemHeight = 74;

        // وضعیتِ «چند نسخهٔ امضاشده ضمیمه شده» یک‌بار خوانده و کش می‌شود:
        // DrawItem در هر بازترسیمِ فهرست اجرا می‌شود و کوئریِ داخلِ آن یعنی
        // ده‌ها رفت‌وبرگشت به دیتابیس در هر اسکرول.
        private readonly Dictionary<string, string> _attachedCache =
            new Dictionary<string, string>(StringComparer.Ordinal);

        // قلم‌ها یک‌بار ساخته می‌شوند؛ ساختِ Font داخلِ DrawItem هندلِ GDI را
        // نشت می‌دهد.
        private static readonly Font FnItemTitle = UiTheme.FontBold(UiTheme.SizeSmall);
        private static readonly Font FnItemDesc  = UiTheme.Font(8f);
        private static readonly Font FnItemPill  = UiTheme.Font(8f);

        public FrmOfficialForms(DatabaseHelper db, int caseId, string caseCode, string preselectKey)
        {
            _db = db ?? new DatabaseHelper();
            _caseId = caseId;
            _caseCode = caseCode ?? "";
            _preselectKey = preselectKey;

            BuildUi();
        }

        // ═════════════════════════════════════════════════════════════════════
        // چیدمان
        // ═════════════════════════════════════════════════════════════════════
        private void BuildUi()
        {
            Text = "فورم‌های رسمی پرونده";
            RightToLeft = RightToLeft.Yes;
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(920, 600);
            ClientSize = new Size(1040, 660);
            BackColor = UiTheme.Background;
            Font = UiTheme.Font(UiTheme.SizeBody);
            ShowInTaskbar = false;
            MinimizeBox = false;

            // ── سربرگ ────────────────────────────────────────────────────────
            var head = new Panel { Dock = DockStyle.Top, Height = 66, BackColor = UiTheme.PrimaryDark };

            var lblTitle = new Label
            {
                Text = "مرکز فورم‌های رسمی",
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Font = UiTheme.FontBold(UiTheme.SizeLarge),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoSize = false
            };
            lblTitle.SetBounds(0, 10, 600, 26);
            lblTitle.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            lblCaseLine = new Label
            {
                Text = "",
                ForeColor = Color.FromArgb(200, 220, 240),
                BackColor = Color.Transparent,
                Font = UiTheme.Font(UiTheme.SizeSmall),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoSize = false
            };
            lblCaseLine.SetBounds(0, 36, 900, 20);
            lblCaseLine.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            head.Controls.Add(lblTitle);
            head.Controls.Add(lblCaseLine);
            head.Resize += delegate
            {
                lblTitle.SetBounds(head.Width - 616, 10, 600, 26);
                lblCaseLine.SetBounds(head.Width - 916, 36, 900, 20);
            };

            // ── نوارِ دکمه‌ها ────────────────────────────────────────────────
            var bar = new Panel { Dock = DockStyle.Bottom, Height = 64, BackColor = UiTheme.CardBack };

            btnWord = UiTheme.CreateButton("ساخت و باز کردن (Word)", "➤", UiTheme.Primary);
            btnPdf = UiTheme.CreateSecondaryButton("ساخت PDF", "▤");
            btnSaveAs = UiTheme.CreateSecondaryButton("ذخیره در مسیر دیگر", "⇑");
            btnAttach = UiTheme.CreateSecondaryButton("ثبت نسخهٔ امضاشده", "✔");
            btnOpenCustom = UiTheme.CreateButton("باز کردن فورم اختصاصی", "➤", UiTheme.Primary);
            btnClose = UiTheme.CreateSecondaryButton("بستن", "✕");

            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(12, 13, 12, 13),
                BackColor = UiTheme.CardBack
            };

            foreach (Button b in new[] { btnWord, btnOpenCustom, btnPdf, btnSaveAs, btnAttach, btnClose })
            {
                b.Height = 38;
                b.Margin = new Padding(0, 0, 8, 0);
                b.TabStop = true;
                flow.Controls.Add(b);
            }

            btnWord.Width = 210;
            btnOpenCustom.Width = 200;
            btnPdf.Width = 132;
            btnSaveAs.Width = 186;
            btnAttach.Width = 194;
            btnClose.Width = 96;

            bar.Controls.Add(flow);

            btnWord.Click += delegate { Generate(false, false); };
            btnPdf.Click += delegate { Generate(true, false); };
            btnSaveAs.Click += delegate { Generate(false, true); };
            btnAttach.Click += delegate { AttachSigned(); };
            btnOpenCustom.Click += delegate { OpenCustom(); };
            btnClose.Click += delegate { Close(); };
            CancelButton = btnClose;

            // ── فهرستِ فورم‌ها (سمتِ راست) ───────────────────────────────────
            var listHost = new Panel
            {
                Dock = DockStyle.Right,
                Width = 336,
                Padding = new Padding(12, 12, 12, 12),
                BackColor = UiTheme.Background
            };

            lstForms = new ListBox
            {
                Dock = DockStyle.Fill,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = ItemHeight,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                IntegralHeight = false
            };
            lstForms.DrawItem += LstForms_DrawItem;
            lstForms.SelectedIndexChanged += delegate { ShowSelected(); };
            listHost.Controls.Add(lstForms);

            var listCaption = new Label
            {
                Dock = DockStyle.Top,
                Height = 26,
                Text = "فورم‌های قابل استفاده برای این پرونده",
                Font = UiTheme.FontBold(UiTheme.SizeSmall),
                ForeColor = UiTheme.TextMuted,
                TextAlign = ContentAlignment.MiddleLeft
            };
            // آموزش — Dock از بالاترین اندیس به پایین‌ترین اعمال می‌شود، پس
            // کنترلِ Fill باید *اول* اضافه شود و لبه‌ها بعد از آن. جابه‌جا کردنِ
            // اندیسِ عنوان به ۰ آن را آخر می‌داخت و زیرِ فهرست پنهان می‌شد.
            listHost.Controls.Add(listCaption);

            // ── پنلِ جزئیات (سمتِ چپ، فضای باقیمانده) ────────────────────────
            var detail = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(14, 12, 6, 12),
                BackColor = UiTheme.Background,
                AutoScroll = false
            };

            lblFormTitle = new Label
            {
                Dock = DockStyle.Top,
                Height = 28,
                Font = UiTheme.FontBold(UiTheme.SizeMedium),
                ForeColor = UiTheme.PrimaryDark,
                TextAlign = ContentAlignment.MiddleLeft
            };

            lblFormDesc = new Label
            {
                Dock = DockStyle.Top,
                Height = 36,
                Font = UiTheme.Font(UiTheme.SizeSmall),
                ForeColor = UiTheme.TextMuted,
                TextAlign = ContentAlignment.TopLeft
            };

            lblStatus = new Label
            {
                Dock = DockStyle.Top,
                Height = 26,
                Font = UiTheme.FontBold(UiTheme.SizeSmall),
                ForeColor = UiTheme.Warning,
                TextAlign = ContentAlignment.MiddleLeft,
                Visible = false
            };

            lblAutoTitle = new Label
            {
                Dock = DockStyle.Top,
                Height = 24,
                Text = "از پرونده خودکار پر می‌شود",
                Font = UiTheme.FontBold(UiTheme.SizeSmall),
                ForeColor = UiTheme.Primary,
                TextAlign = ContentAlignment.MiddleLeft
            };

            panAuto = new Panel
            {
                Dock = DockStyle.Top,
                Height = 92,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(8, 6, 8, 6),
                AutoScroll = true
            };

            lblFieldsTitle = new Label
            {
                Dock = DockStyle.Top,
                Height = 30,
                Text = "خانه‌هایی که باید تکمیل شوند",
                Font = UiTheme.FontBold(UiTheme.SizeSmall),
                ForeColor = UiTheme.Primary,
                TextAlign = ContentAlignment.BottomLeft
            };

            panFields = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(10, 10, 10, 10),
                AutoScroll = true
            };

            // ترتیبِ Dock معکوسِ ترتیبِ افزودن است؛ Fill آخر اضافه می‌شود تا
            // فضای باقیمانده را بگیرد.
            detail.Controls.Add(panFields);
            detail.Controls.Add(lblFieldsTitle);
            detail.Controls.Add(panAuto);
            detail.Controls.Add(lblAutoTitle);
            detail.Controls.Add(lblStatus);
            detail.Controls.Add(lblFormDesc);
            detail.Controls.Add(lblFormTitle);

            Controls.Add(detail);
            Controls.Add(listHost);
            Controls.Add(bar);
            Controls.Add(head);
        }

        // ═════════════════════════════════════════════════════════════════════
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            LoadTokens();
            LoadForms();
        }

        private void LoadTokens()
        {
            try
            {
                _tokens = CaseFormTokens.Build(_db, _caseId);
            }
            catch (Exception ex)
            {
                UiTheme.ShowError(this, "خطا در خواندن اطلاعات پرونده: " + ex.Message);
                _tokens = new Dictionary<string, string>(StringComparer.Ordinal);
            }

            string head = Tok("HeadName");
            string type = Tok("RequestTypeName");
            var parts = new List<string>();
            if (_caseCode.Length > 0) parts.Add("کد پرونده: " + _caseCode);
            if (head.Length > 0) parts.Add(head);
            if (type.Length > 0) parts.Add(type);
            parts.Add(SecurityContext.CenterDisplay ?? "");
            lblCaseLine.Text = string.Join("   ·   ", parts.ToArray());
        }

        private void RefreshAttachedCache()
        {
            _attachedCache.Clear();
            foreach (CaseFormDef d in _forms)
            {
                if (string.IsNullOrEmpty(d.Key)) continue;
                _attachedCache[d.Key] = CaseOfficialForms.AttachedSummary(_db, _caseId, d.DocType);
            }
        }

        private string Tok(string key)
        {
            string v;
            return _tokens.TryGetValue(key, out v) ? (v ?? "") : "";
        }

        private void LoadForms()
        {
            _forms = CaseOfficialForms.Available(CaseOfficialForms.RequestTypeCodeOf(_db, _caseId));
            RefreshAttachedCache();

            lstForms.BeginUpdate();
            lstForms.Items.Clear();
            foreach (CaseFormDef d in _forms) lstForms.Items.Add(d.Title);
            lstForms.EndUpdate();

            int index = 0;
            if (!string.IsNullOrEmpty(_preselectKey))
            {
                for (int i = 0; i < _forms.Count; i++)
                    if (string.Equals(_forms[i].Key, _preselectKey, StringComparison.OrdinalIgnoreCase))
                    { index = i; break; }
            }

            if (_forms.Count > 0) lstForms.SelectedIndex = index;
        }

        // ─── نقاشیِ کارتِ هر فورم ───────────────────────────────────────────
        private void LstForms_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _forms.Count) return;

            CaseFormDef def = _forms[e.Index];
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Rectangle r = e.Bounds;

            using (var back = new SolidBrush(selected ? UiTheme.HoverTint : Color.White))
                g.FillRectangle(back, r);

            if (selected)
                using (var accent = new SolidBrush(UiTheme.Primary))
                    g.FillRectangle(accent, r.Right - 4, r.Y, 4, r.Height);

            using (var pen = new Pen(UiTheme.Border, 1f))
                g.DrawLine(pen, r.Left + 6, r.Bottom - 1, r.Right - 6, r.Bottom - 1);

            var sfRight = new StringFormat(StringFormatFlags.DirectionRightToLeft)
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Near,
                Trimming = StringTrimming.EllipsisCharacter
            };

            var sfDesc = new StringFormat(StringFormatFlags.DirectionRightToLeft | StringFormatFlags.LineLimit)
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Near,
                Trimming = StringTrimming.EllipsisWord
            };

            using (var br = new SolidBrush(UiTheme.TextDark))
                g.DrawString(def.Title, FnItemTitle, br,
                    new RectangleF(r.Left + 10, r.Y + 6, r.Width - 22, 18), sfRight);

            // توضیح حداکثر دو سطر، و بالای نوارِ وضعیت تمام می‌شود — وگرنه
            // متن و نشانِ وضعیت روی هم می‌افتادند.
            using (var br = new SolidBrush(UiTheme.TextMuted))
                g.DrawString(def.Description ?? "", FnItemDesc, br,
                    new RectangleF(r.Left + 10, r.Y + 25, r.Width - 22, 26), sfDesc);

            // نشانِ وضعیت
            string status;
            Color statusColor;
            if (!def.IsAvailable) { status = "قالب پیدا نشد"; statusColor = UiTheme.Danger; }
            else
            {
                string attached;
                if (!_attachedCache.TryGetValue(def.Key ?? "", out attached)) attached = "";
                if (attached.Length > 0) { status = attached; statusColor = UiTheme.Success; }
                else { status = "نسخهٔ امضاشده ضمیمه نشده"; statusColor = UiTheme.TextMuted; }
            }

            SizeF size = g.MeasureString(status, FnItemPill);
            var pill = new RectangleF(r.Right - 14 - size.Width - 14, r.Bottom - 21, size.Width + 14, 17);
            using (var fill = new SolidBrush(Color.FromArgb(28, statusColor)))
                g.FillRectangle(fill, pill);
            using (var br = new SolidBrush(statusColor))
                g.DrawString(status, FnItemPill, br, pill,
                    new StringFormat(StringFormatFlags.DirectionRightToLeft)
                    { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });

            sfRight.Dispose();
            sfDesc.Dispose();
        }

        // ═════════════════════════════════════════════════════════════════════
        // نمایشِ فورمِ انتخاب‌شده
        // ═════════════════════════════════════════════════════════════════════
        private void ShowSelected()
        {
            int i = lstForms.SelectedIndex;
            _current = (i >= 0 && i < _forms.Count) ? _forms[i] : null;
            _lastOutputPath = "";

            _editors.Clear();
            panFields.Controls.Clear();
            panAuto.Controls.Clear();

            if (_current == null)
            {
                lblFormTitle.Text = "";
                lblFormDesc.Text = "";
                return;
            }

            lblFormTitle.Text = _current.Title;
            lblFormDesc.Text = _current.Description ?? "";

            bool custom = _current.CustomOpen != null;
            bool available = _current.IsAvailable;

            btnOpenCustom.Visible = custom;
            btnWord.Visible = !custom;
            btnPdf.Visible = !custom;
            btnSaveAs.Visible = !custom;
            btnWord.Enabled = btnPdf.Enabled = btnSaveAs.Enabled = available;
            btnAttach.Enabled = true;

            if (!available)
            {
                lblStatus.Visible = true;
                lblStatus.ForeColor = UiTheme.Danger;
                lblStatus.Text = "قالب این فورم پیدا نشد: " +
                                 DocxFormExport.ResolveTemplate(_current.TemplateFile);
            }
            else
            {
                lblStatus.Visible = false;
            }

            BuildAutoPanel();
            BuildFieldsPanel();
        }

        private void BuildAutoPanel()
        {
            List<KeyValuePair<string, string>> items =
                CaseOfficialForms.AutoSummary(_tokens, _current);

            if (items.Count == 0)
            {
                panAuto.Controls.Add(MutedLabel("این فورم اطلاعاتِ خودکاری از پرونده نمی‌گیرد.", 8, 6, 500));
                return;
            }

            int x = panAuto.ClientSize.Width - 12;
            int y = 4;
            int cellW = 250;
            int col = 0;

            foreach (var kv in items)
            {
                var lbl = new Label
                {
                    AutoSize = false,
                    Text = kv.Key + ": ",
                    Font = UiTheme.FontBold(8.5f),
                    ForeColor = UiTheme.TextMuted,
                    TextAlign = ContentAlignment.MiddleLeft
                };
                lbl.SetBounds(x - 100 - col * cellW, y, 100, 20);

                var val = new Label
                {
                    AutoSize = false,
                    Text = kv.Value,
                    Font = UiTheme.Font(9f),
                    ForeColor = UiTheme.TextDark,
                    TextAlign = ContentAlignment.MiddleLeft
                };
                val.SetBounds(x - cellW - col * cellW + 6, y, cellW - 106, 20);

                panAuto.Controls.Add(lbl);
                panAuto.Controls.Add(val);

                col++;
                if (col >= 2) { col = 0; y += 22; }
            }
        }

        private void BuildFieldsPanel()
        {
            string[] manual = _current.ManualOrEmpty;

            if (manual.Length == 0)
            {
                panFields.Controls.Add(MutedLabel(
                    "همهٔ خانه‌های این فورم از پرونده پر می‌شود؛ چیزی برای تایپ نیست.", 10, 10, 560));
                return;
            }

            int right = panFields.ClientSize.Width - 10;
            int y = 6;

            foreach (string token in manual)
            {
                bool required = Contains(_current.RequiredTokens, token);
                bool multiline = Contains(_current.MultilineTokens, token);
                string[] choices = null;
                if (_current.Choices != null) _current.Choices.TryGetValue(token, out choices);

                var lbl = new Label
                {
                    AutoSize = false,
                    Text = CaseOfficialForms.ManualCaption(token) + (required ? " *" : ""),
                    Font = required ? UiTheme.FontBold(UiTheme.SizeSmall) : UiTheme.Font(UiTheme.SizeSmall),
                    ForeColor = required ? UiTheme.Danger : UiTheme.TextMuted,
                    TextAlign = ContentAlignment.MiddleLeft
                };
                lbl.SetBounds(right - 190, y + 2, 186, 24);

                Control editor;
                int editorHeight = multiline ? 66 : 26;

                if (choices != null && choices.Length > 0)
                {
                    var cmb = new ComboBox
                    {
                        RightToLeft = RightToLeft.Yes,
                        DropDownStyle = ComboBoxStyle.DropDown,
                        Font = UiTheme.Font(UiTheme.SizeSmall)
                    };
                    cmb.Items.AddRange(choices);
                    cmb.Text = Tok(token);
                    editor = cmb;
                }
                else
                {
                    var box = new TextBox
                    {
                        RightToLeft = RightToLeft.Yes,
                        Multiline = multiline,
                        ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None,
                        Text = Tok(token),
                        Font = UiTheme.Font(UiTheme.SizeSmall)
                    };
                    UiTheme.StyleTextBox(box);
                    editor = box;
                }

                editor.SetBounds(right - 190 - 330 - 8, y, 330, editorHeight);
                editor.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                lbl.Anchor = AnchorStyles.Top | AnchorStyles.Right;

                panFields.Controls.Add(lbl);
                panFields.Controls.Add(editor);
                _editors[token] = editor;

                y += editorHeight + 8;
            }

            var hint = MutedLabel("خانه‌های ستاره‌دار اجباری‌اند. تاریخ‌ها به شکل ۱۴۰۴/۰۱/۰۲ نوشته می‌شوند.",
                                  10, y + 6, 520);
            hint.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            hint.SetBounds(right - 520, y + 6, 520, 20);
            panFields.Controls.Add(hint);
        }

        private static bool Contains(string[] arr, string value)
        {
            if (arr == null) return false;
            foreach (string s in arr)
                if (string.Equals(s, value, StringComparison.Ordinal)) return true;
            return false;
        }

        private static Label MutedLabel(string text, int x, int y, int width)
        {
            var lbl = new Label
            {
                AutoSize = false,
                Text = text,
                Font = UiTheme.Font(UiTheme.SizeSmall),
                ForeColor = UiTheme.TextMuted,
                TextAlign = ContentAlignment.MiddleLeft
            };
            lbl.SetBounds(x, y, width, 22);
            return lbl;
        }

        // ═════════════════════════════════════════════════════════════════════
        // ساختِ خروجی
        // ═════════════════════════════════════════════════════════════════════
        private bool CheckRequired()
        {
            if (_current == null || _current.RequiredTokens == null) return true;

            foreach (string token in _current.RequiredTokens)
            {
                Control editor;
                if (!_editors.TryGetValue(token, out editor)) continue;
                if ((editor.Text ?? "").Trim().Length > 0) continue;

                UiTheme.ShowWarning(this,
                    "«" + CaseOfficialForms.ManualCaption(token) + "» باید پر شود، وگرنه فورم ناقص چاپ می‌گردد.");
                editor.Focus();
                return false;
            }
            return true;
        }

        // نگاشتِ نهایی: مقدارهای خودکار + آنچه کاربر تایپ کرده، با کلیدِ {{...}}
        // که DocxFormExport انتظار دارد.
        private Dictionary<string, string> CollectTokens()
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in _tokens) map["{{" + pair.Key + "}}"] = pair.Value ?? "";
            foreach (var pair in _editors) map["{{" + pair.Key + "}}"] = (pair.Value.Text ?? "").Trim();
            return map;
        }

        private void Generate(bool asPdf, bool chooseLocation)
        {
            if (_current == null || _current.CustomOpen != null) return;
            if (!CheckRequired()) return;

            if (!DocxFormExport.TemplateExists(_current.TemplateFile))
            {
                UiTheme.ShowError(this, "قالب این فورم پیدا نشد:" + Environment.NewLine +
                                        DocxFormExport.ResolveTemplate(_current.TemplateFile));
                return;
            }

            string extension = asPdf ? ".pdf" : ".docx";
            string fileName = FileHelper.CleanName(
                (_caseCode.Length > 0 ? _caseCode + " - " : "") + _current.Title) +
                " - " + DateTime.Now.ToString("yyyyMMdd-HHmm",
                    System.Globalization.CultureInfo.InvariantCulture) + extension;

            string outPath = chooseLocation ? "" : AutoFolderFile(fileName);

            if (string.IsNullOrEmpty(outPath))
            {
                using (var sfd = new SaveFileDialog
                {
                    Filter = asPdf ? "فایل PDF|*.pdf" : "سند ورد|*.docx",
                    FileName = fileName
                })
                {
                    if (sfd.ShowDialog(this) != DialogResult.OK) return;
                    outPath = sfd.FileName;
                }
            }

            Cursor previous = Cursor;
            Cursor = Cursors.WaitCursor;
            try
            {
                Dictionary<string, string> tokens = CollectTokens();

                if (asPdf) DocxFormExport.WritePdf(_current.TemplateFile, outPath, tokens);
                else DocxFormExport.WriteDocx(_current.TemplateFile, outPath, tokens);

                _lastOutputPath = outPath;
                LogGenerated(outPath);

                Cursor = previous;
                OpenFile(outPath);

                UiTheme.ShowSuccess(this,
                    "فورم ساخته شد و باز می‌شود:" + Environment.NewLine + outPath +
                    Environment.NewLine + Environment.NewLine +
                    "پس از چاپ و امضا، با دکمهٔ «ضمیمهٔ نسخهٔ امضاشده» آن را در اسناد پرونده ثبت کنید.");
            }
            catch (Exception ex)
            {
                Cursor = previous;
                UiTheme.ShowError(this, "خطا در ساخت فورم: " + ex.Message);
            }
            finally
            {
                Cursor = previous;
            }
        }

        // پوشهٔ اسنادِ خودِ پرونده. اگر محلِ ذخیرهٔ برنامه تنظیم نشده باشد،
        // رشتهٔ خالی برمی‌گردد و فراخواننده به SaveFileDialog برمی‌گردد.
        private string AutoFolderFile(string fileName)
        {
            try
            {
                if (_caseCode.Length == 0) return "";
                string folder = FileHelper.GetSectionFolder(_caseCode, FileHelper.SectionDocs);
                if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return "";

                string formsFolder = Path.Combine(folder, "OfficialForms");
                Directory.CreateDirectory(formsFolder);
                return Path.Combine(formsFolder, fileName);
            }
            catch { return ""; }
        }

        private void LogGenerated(string path)
        {
            try
            {
                TimelineService.Log(_caseId, TimelineService.CategoryDocument,
                    "OFFICIAL_FORM_GENERATED", null, null, null,
                    "ساخت فورم رسمی",
                    _current.Title + " ساخته شد: " + Path.GetFileName(path));
            }
            catch { }

            try { AuditLogger.Log("چاپ", "TblDocs", _caseId, "", _current.Title); }
            catch { }
        }

        private void OpenFile(string path)
        {
            try
            {
                if (File.Exists(path)) Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                UiTheme.ShowWarning(this, "فایل ساخته شد ولی باز نشد: " + ex.Message);
            }
        }

        private void OpenCustom()
        {
            if (_current == null || _current.CustomOpen == null) return;
            try { _current.CustomOpen(this, _caseId); }
            catch (Exception ex) { UiTheme.ShowError(this, "خطا در باز کردن فورم: " + ex.Message); }
            lstForms.Invalidate();
        }

        private void AttachSigned()
        {
            if (_current == null) return;

            string startFolder = "";
            try
            {
                startFolder = string.IsNullOrWhiteSpace(_lastOutputPath)
                    ? FileHelper.GetSectionFolder(_caseCode, FileHelper.SectionDocs)
                    : Path.GetDirectoryName(_lastOutputPath);
            }
            catch { }

            bool saved = FrmDocxForm.AttachToCase(this, _db, _caseId, _caseCode,
                _current.DocType, _current.Title + " — نسخهٔ امضاشده", startFolder,
                _current.DocumentCategoryCode);

            if (!saved) return;

            RefreshAttachedCache();
            lstForms.Invalidate();          // نشانِ «ضمیمه شده» تازه می‌شود
            if (OnDocumentAttached != null) OnDocumentAttached();
        }
    }
}
