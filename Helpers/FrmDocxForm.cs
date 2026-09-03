using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace CaseManagement.Helpers
{
    // ─────────────────────────────────────────────────────────────────────────
    // دیالوگِ عمومیِ «پر کردن و خروجی گرفتنِ یک فورمِ رسمی».
    //
    // آموزش — چرا یک دیالوگِ مشترک و نه شش فورمِ جداگانه: وکالت موقت، رخصتی،
    // ماموریت، استخدام، دریافت حقوق و قرارداد ترانسپورت همگی یک کار می‌کنند —
    // چند خانه پر می‌شود، دکمهٔ Word و دکمهٔ PDF زده می‌شود. نوشتنِ شش فورمِ
    // تقریباً یکسان یعنی شش جای متفاوت برای خراب شدن. اینجا رفتار یک‌بار
    // نوشته می‌شود و هر فراخواننده فقط «فهرست خانه‌ها» را می‌دهد.
    //
    // فراخواننده هیچ‌چیز دربارهٔ OpenXml یا PDF نمی‌داند؛ آن‌ها در
    // DocxFormExport هستند.
    // ─────────────────────────────────────────────────────────────────────────
    public class FrmDocxForm : Form
    {
        // یک خانهٔ فورم. اگر Token خالی باشد، این ردیف فقط یک عنوانِ بخش است.
        public sealed class FieldDef
        {
            public string Caption;
            public string Token;
            public string Value;
            public bool ReadOnly;

            // اگر پر باشد، به‌جای TextBox یک ComboBoxِ قابلِ تایپ ساخته می‌شود.
            public string[] Choices;

            public static FieldDef Section(string caption)
            {
                return new FieldDef { Caption = caption };
            }

            public static FieldDef Text(string caption, string token, string value = "", bool readOnly = false)
            {
                return new FieldDef { Caption = caption, Token = token, Value = value, ReadOnly = readOnly };
            }

            public static FieldDef Choice(string caption, string token, string[] choices, string value = "")
            {
                return new FieldDef { Caption = caption, Token = token, Choices = choices, Value = value };
            }
        }

        private readonly string _templateFileName;
        private readonly string _fileNameHint;
        private readonly List<FieldDef> _fields;
        private readonly Dictionary<string, Control> _editors =
            new Dictionary<string, Control>(StringComparer.Ordinal);

        // توکن‌هایی که کاربر نمی‌بیند (مثلاً تاریخ روز یا نام مرکز).
        private readonly Dictionary<string, string> _hidden =
            new Dictionary<string, string>(StringComparer.Ordinal);

        // توکن‌هایی که حتماً باید پر باشند، وگرنه خروجی گرفته نمی‌شود.
        private readonly HashSet<string> _required = new HashSet<string>(StringComparer.Ordinal);

        // پس از ساختِ موفقِ خروجی صدا زده می‌شود (مسیر فایل). برای ثبتِ
        // تاریخچه یا ضمیمه‌کردنِ سند به کار می‌رود؛ اختیاری است.
        public Action<string> OnExported;

        // اگر پر باشد، یک دکمهٔ اضافی با همین برچسب کنارِ بقیه ظاهر می‌شود.
        public string ExtraButtonText;
        public Action<string> OnExtraButton;   // آخرین مسیرِ خروجی را می‌گیرد

        public string LastOutputPath { get; private set; }

        public FrmDocxForm(string title, string templateFileName,
                           IEnumerable<FieldDef> fields, string fileNameHint)
        {
            _templateFileName = templateFileName;
            _fileNameHint = fileNameHint ?? title;
            _fields = new List<FieldDef>(fields);
            LastOutputPath = "";
            Text = title;
        }

        public FrmDocxForm Hidden(string token, string value)
        {
            _hidden["{{" + token + "}}"] = (value ?? "").Trim();
            return this;
        }

        public FrmDocxForm Require(params string[] tokens)
        {
            foreach (string t in tokens) _required.Add(t);
            return this;
        }

        // UI عمداً در OnLoad ساخته می‌شود نه در سازنده، تا فراخواننده فرصت
        // داشته باشد Hidden/Require/OnExported را قبلش تنظیم کند.
        protected override void OnLoad(EventArgs e)
        {
            BuildUi();
            base.OnLoad(e);
        }

        private void BuildUi()
        {
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            BackColor = UiTheme.Background;
            Font = UiTheme.Font(UiTheme.SizeBody);

            var host = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = UiTheme.Background };
            int y = 10;

            foreach (FieldDef f in _fields)
            {
                if (string.IsNullOrEmpty(f.Token)) { AddSection(host, f.Caption, ref y); continue; }
                AddEditor(host, f, ref y);
            }

            var bar = new Panel { Dock = DockStyle.Bottom, Height = 52, BackColor = UiTheme.CardBack };

            var btnWord = UiTheme.CreateButton("خروجی Word", "➤", UiTheme.Primary);
            btnWord.SetBounds(12, 9, 140, 34);
            btnWord.Click += delegate { Export(false); };

            var btnPdf = UiTheme.CreateButton("خروجی PDF", "➤", UiTheme.Primary);
            btnPdf.SetBounds(160, 9, 140, 34);
            btnPdf.Click += delegate { Export(true); };

            var btnClose = UiTheme.CreateSecondaryButton("بستن", "✕");
            btnClose.SetBounds(308, 9, 100, 34);
            btnClose.Click += delegate { Close(); };

            bar.Controls.Add(btnWord);
            bar.Controls.Add(btnPdf);
            bar.Controls.Add(btnClose);

            if (!string.IsNullOrWhiteSpace(ExtraButtonText))
            {
                btnClose.SetBounds(486, 9, 100, 34);
                var btnExtra = UiTheme.CreateSecondaryButton(ExtraButtonText, "▤");
                btnExtra.SetBounds(308, 9, 170, 34);
                btnExtra.Click += delegate
                {
                    if (OnExtraButton != null) OnExtraButton(LastOutputPath);
                };
                bar.Controls.Add(btnExtra);
            }

            Controls.Add(host);
            Controls.Add(bar);
            CancelButton = btnClose;

            int width = string.IsNullOrWhiteSpace(ExtraButtonText) ? 620 : 700;
            ClientSize = new Size(width, Math.Min(y + 70, 660));
        }

        private void AddSection(Control host, string caption, ref int y)
        {
            var lbl = new Label
            {
                Text = caption,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = UiTheme.Primary,
                Font = UiTheme.FontBold(UiTheme.SizeBody),
                BackColor = Color.Transparent
            };
            lbl.SetBounds(12, y + 6, 560, 26);
            host.Controls.Add(lbl);
            y += 36;
        }

        private void AddEditor(Control host, FieldDef f, ref int y)
        {
            var lbl = new Label
            {
                Text = f.Caption,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = UiTheme.TextMuted,
                BackColor = Color.Transparent
            };
            lbl.SetBounds(12, y, 220, 26);
            host.Controls.Add(lbl);

            Control editor;
            if (f.Choices != null && f.Choices.Length > 0)
            {
                // DropDown و نه DropDownList: اگر مقدارِ موردنظر در فهرست
                // نبود، کاربر باید بتواند تایپ کند.
                var cmb = new ComboBox { RightToLeft = RightToLeft.Yes, DropDownStyle = ComboBoxStyle.DropDown };
                cmb.Items.AddRange(f.Choices);
                cmb.Text = f.Value ?? "";
                editor = cmb;
            }
            else
            {
                var box = new TextBox { RightToLeft = RightToLeft.Yes, Text = f.Value ?? "", ReadOnly = f.ReadOnly };
                if (f.ReadOnly) box.BackColor = UiTheme.Background;
                editor = box;
            }

            editor.SetBounds(238, y, 330, 26);
            host.Controls.Add(editor);
            _editors["{{" + f.Token + "}}"] = editor;

            y += 30;
        }

        // مقدارِ فعلیِ یک توکن — برای فراخوانندگانی که پس از ساختِ خروجی
        // می‌خواهند همان مقادیر را در دیتابیس هم ثبت کنند.
        public string ValueOf(string token)
        {
            Control editor;
            if (_editors.TryGetValue("{{" + token + "}}", out editor))
                return (editor.Text ?? "").Trim();

            string hidden;
            if (_hidden.TryGetValue("{{" + token + "}}", out hidden))
                return hidden;

            return "";
        }

        // ═══════════════════════════════════════════════════════════════════
        private Dictionary<string, string> CollectTokens()
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in _hidden) map[pair.Key] = pair.Value;
            foreach (var pair in _editors) map[pair.Key] = (pair.Value.Text ?? "").Trim();
            return map;
        }

        private bool CheckRequired()
        {
            foreach (string token in _required)
            {
                Control editor;
                if (!_editors.TryGetValue("{{" + token + "}}", out editor)) continue;
                if ((editor.Text ?? "").Trim().Length > 0) continue;

                UiTheme.ShowWarning(this, "این خانه باید پر شود، وگرنه فورم ناقص چاپ می‌شود.");
                editor.Focus();
                return false;
            }
            return true;
        }

        private void Export(bool asPdf)
        {
            if (!CheckRequired()) return;

            if (!DocxFormExport.TemplateExists(_templateFileName))
            {
                UiTheme.ShowError(this, "قالب این فورم پیدا نشد:" + Environment.NewLine +
                                        DocxFormExport.ResolveTemplate(_templateFileName));
                return;
            }

            using (var sfd = new SaveFileDialog
            {
                Filter = asPdf ? "فایل PDF|*.pdf" : "سند ورد|*.docx",
                FileName = FileHelper.CleanName(_fileNameHint) + (asPdf ? ".pdf" : ".docx")
            })
            {
                if (sfd.ShowDialog(this) != DialogResult.OK) return;

                Cursor previous = Cursor;
                Cursor = Cursors.WaitCursor;
                try
                {
                    var tokens = CollectTokens();
                    if (asPdf) DocxFormExport.WritePdf(_templateFileName, sfd.FileName, tokens);
                    else DocxFormExport.WriteDocx(_templateFileName, sfd.FileName, tokens);

                    LastOutputPath = sfd.FileName;

                    if (OnExported != null) OnExported(sfd.FileName);

                    UiTheme.ShowSuccess(this, "فورم ساخته شد:" + Environment.NewLine + sfd.FileName);
                }
                catch (Exception ex)
                {
                    UiTheme.ShowError(this, "خطا در ساخت فورم: " + ex.Message);
                }
                finally
                {
                    Cursor = previous;
                }
            }
        }

        // کمکیِ ضمیمه‌کردنِ سندِ امضاشده به اسنادِ یک پرونده — چند فورم به آن
        // نیاز دارند، پس یک‌جا نوشته شده.
        public static void AttachToCase(IWin32Window owner, DAL.DatabaseHelper db,
                                        int caseId, string caseCode, string docType,
                                        string description, string startFolder)
        {
            if (caseId <= 0) { UiTheme.ShowWarning(owner, "پرونده مشخص نیست."); return; }
            if (string.IsNullOrWhiteSpace(caseCode))
            {
                UiTheme.ShowWarning(owner, "کد اختصاصی پرونده مشخص نیست؛ سند ذخیره نمی‌شود.");
                return;
            }

            using (var ofd = new OpenFileDialog
            {
                Title = "نسخهٔ امضاشده را انتخاب کنید",
                Filter = "اسناد|*.pdf;*.docx;*.jpg;*.jpeg;*.png|همه فایل‌ها|*.*"
            })
            {
                if (!string.IsNullOrWhiteSpace(startFolder) && Directory.Exists(startFolder))
                    ofd.InitialDirectory = startFolder;

                if (ofd.ShowDialog(owner) != DialogResult.OK) return;

                try
                {
                    // همان مسیرِ ذخیره‌سازیِ استانداردی که FrmDocs استفاده
                    // می‌کند — فایل به پوشهٔ Docs همان پرونده کپی می‌شود.
                    string savedPath = FileHelper.SaveFileToCaseFolder(
                        ofd.FileName, caseCode, FileHelper.SectionDocs,
                        FileHelper.CleanName(caseCode) + "-" + FileHelper.CleanName(docType));

                    if (string.IsNullOrWhiteSpace(savedPath))
                    {
                        UiTheme.ShowError(owner, "فایل سند ذخیره نشد: " + FileHelper.LastError);
                        return;
                    }

                    long docId = db.ExecuteInsertReturningId(@"
INSERT INTO TblDocs (CasID, DocType, OriginalFileName, DocFilePath, DocDescription)
VALUES (@cas, @type, @orig, @path, @desc)",
                        new System.Data.SQLite.SQLiteParameter("@cas", caseId),
                        new System.Data.SQLite.SQLiteParameter("@type", docType),
                        new System.Data.SQLite.SQLiteParameter("@orig", Path.GetFileName(ofd.FileName)),
                        new System.Data.SQLite.SQLiteParameter("@path", savedPath),
                        new System.Data.SQLite.SQLiteParameter("@desc", description ?? ""));

                    try { AuditLogger.Log("ثبت", "TblDocs", (int)docId, "", docType); }
                    catch { }

                    UiTheme.ShowSuccess(owner,
                        "سند امضاشده در اسناد پرونده ثبت شد." + Environment.NewLine +
                        "برای دیدن آن، تب «اسناد» پرونده را تازه کنید.");
                }
                catch (Exception ex)
                {
                    UiTheme.ShowError(owner, "خطا در ثبت سند: " + ex.Message);
                }
            }
        }
    }
}
