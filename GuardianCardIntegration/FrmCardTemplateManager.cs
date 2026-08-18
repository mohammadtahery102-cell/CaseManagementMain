using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using CaseManagement.DAL;
using CaseManagement.Helpers;

namespace CaseManagement.GuardianCardIntegration
{
    // ─────────────────────────────────────────────────────────────────────────
    // مدیریت قالب‌های کارت («CARD TEMPLATE MANAGEMENT» + «Card Designer سبک») —
    // فقط مدیر. به‌خاطرِ محدودیتِ معماریِ طرحِ ثابتِ HTML/CSS (نگاه کنید آموزشِ
    // TblCardTemplate در DatabaseInitializer)، «قالب» یعنی:
    //   ۱) کدام فیلدهای اختیاریِ متنی روشن باشند (بدونِ ترتیب‌دهیِ آزاد).
    //   ۲) رنگ/فونت/پس‌زمینهٔ هر رو/واترمارک/هولوگرام/QR/بارکد — نگاه کنید
    //      CardTemplateDesign و GuardianCardRenderer.ApplyDesignOverrides.
    // این هنوز طراحِ کاملِ drag-and-drop نیست — عمداً، طبقِ توافق.
    // ─────────────────────────────────────────────────────────────────────────
    public class FrmCardTemplateManager : Form
    {
        private readonly CardTemplateRepository _repo = new CardTemplateRepository();
        private readonly DatabaseHelper _db = new DatabaseHelper();

        private ListBox _lstTemplates;
        private TextBox _txtName;
        private CheckedListBox _chkFields;
        private Label _lblDefaultTag;

        // ─── کنترل‌های طراح (بخش جدید) ────────────────────────────────────
        private Panel _pnlPrimaryColor;
        private Panel _pnlSecondaryColor;
        private ComboBox _cmbFont;
        private TextBox _txtBgFront;
        private TextBox _txtBgBack;
        private TextBox _txtWatermark;
        private NumericUpDown _numWatermarkOpacity;
        private CheckBox _chkHologram;
        private CheckBox _chkQRCode;
        private CheckBox _chkBarcode;

        private static readonly Dictionary<string, string> FieldLabels = new Dictionary<string, string>
        {
            { "PublicCode", "کد عمومی" },
            { "Website", "وبسایت" },
            { "Email", "ایمیل" },
            { "IssuedBy", "نام صادرکننده" },
            { "Position", "سمت صادرکننده" },
            { "Logo", "لوگوی مؤسسه" },
            { "Signature", "امضا" },
            { "Stamp", "مهر" }
        };

        private static readonly string[] FontChoices = { "Vazirmatn", "Tahoma", "Segoe UI", "B Nazanin", "IRANSans" };

        private List<CardTemplate> _templates = new List<CardTemplate>();
        private int _currentTemplateId = 0;

        public FrmCardTemplateManager()
        {
            BuildUi();
            Load += delegate { LoadTemplateList(); };
        }

        private void BuildUi()
        {
            Text = "مدیریت قالب‌ها و طراحِ کارت شناسایی";
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor = UiTheme.Background;
            Font = UiTheme.Font(UiTheme.SizeBody);
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(900, 700);
            MinimumSize = new Size(760, 560);

            Label lblHint = new Label
            {
                Text = "به‌خاطرِ طرحِ ثابتِ چاپیِ کارت، فقط رنگ/فونت/پس‌زمینه/فیلدهای اختیاری قابل‌تنظیم‌اند — نه ترتیب‌دهیِ آزادِ فیلد.",
                AutoSize = false, TextAlign = ContentAlignment.MiddleRight,
                ForeColor = UiTheme.TextMuted, Font = UiTheme.Font(9F),
                Dock = DockStyle.Top, Height = 30, Padding = new Padding(10, 6, 10, 0)
            };

            Panel left = new Panel { Dock = DockStyle.Right, Width = 220, Padding = new Padding(10) };
            Label lblList = new Label { Text = "قالب‌ها", Dock = DockStyle.Top, Height = 22, TextAlign = ContentAlignment.MiddleRight };
            _lstTemplates = new ListBox { Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes };
            _lstTemplates.SelectedIndexChanged += delegate { LoadSelectedIntoEditor(); };
            left.Controls.Add(_lstTemplates);
            left.Controls.Add(lblList);

            FlowLayoutPanel buttonRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom, Height = 44, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 6, 0, 0)
            };

            Button btnNew = UiTheme.CreateSecondaryButton("قالب جدید", "+");
            btnNew.Size = new Size(100, 32); btnNew.Margin = new Padding(3);
            btnNew.Click += delegate { StartNewTemplate(); };

            Button btnSave = UiTheme.CreateButton("ذخیره", "💾", UiTheme.Success);
            btnSave.Size = new Size(100, 32); btnSave.Margin = new Padding(3);
            btnSave.Click += delegate { SaveCurrent(); };

            Button btnDelete = UiTheme.CreateSecondaryButton("حذف", "✕");
            btnDelete.Size = new Size(90, 32); btnDelete.Margin = new Padding(3);
            btnDelete.Click += delegate { DeleteCurrent(); };

            Button btnPreview = UiTheme.CreateSecondaryButton("پیش‌نمایش", "👁");
            btnPreview.Size = new Size(110, 32); btnPreview.Margin = new Padding(3);
            btnPreview.Click += delegate { PreviewCurrent(); };

            Button btnExport = UiTheme.CreateSecondaryButton("Export", "⇓");
            btnExport.Size = new Size(90, 32); btnExport.Margin = new Padding(3);
            btnExport.Click += delegate { ExportCurrent(); };

            Button btnImport = UiTheme.CreateSecondaryButton("Import", "⇑");
            btnImport.Size = new Size(90, 32); btnImport.Margin = new Padding(3);
            btnImport.Click += delegate { ImportTemplate(); };

            buttonRow.Controls.Add(btnNew);
            buttonRow.Controls.Add(btnSave);
            buttonRow.Controls.Add(btnDelete);
            buttonRow.Controls.Add(btnPreview);
            buttonRow.Controls.Add(btnExport);
            buttonRow.Controls.Add(btnImport);

            Panel right = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10), AutoScroll = true };
            BuildEditorControls(right);

            right.Controls.Add(buttonRow);
            Controls.Add(right);
            Controls.Add(left);
            Controls.Add(lblHint);
        }

        private void BuildEditorControls(Panel right)
        {
            int y = 0;

            Label lblName = MakeFieldLabel("نام قالب", y); y += 22;
            _txtName = new TextBox { RightToLeft = RightToLeft.Yes };
            _txtName.SetBounds(0, y, 500, 24); y += 30;

            _lblDefaultTag = new Label
            {
                Text = "", ForeColor = UiTheme.Primary, Font = UiTheme.FontBold(9F),
                TextAlign = ContentAlignment.MiddleRight, AutoSize = false
            };
            _lblDefaultTag.SetBounds(0, y, 500, 20); y += 26;

            Label lblFields = MakeFieldLabel("فیلدهای اختیاریِ متنی — روشن/خاموش", y); y += 24;
            _chkFields = new CheckedListBox { RightToLeft = RightToLeft.Yes, CheckOnClick = true };
            _chkFields.SetBounds(0, y, 500, 140);
            foreach (string field in CardTemplateRepository.ToggleableFields)
                _chkFields.Items.Add(FieldLabels.ContainsKey(field) ? FieldLabels[field] : field, true);
            y += 150;

            Label lblDesignTitle = new Label
            {
                Text = "── طراحِ ظاهریِ کارت ──", Font = UiTheme.FontBold(10F),
                TextAlign = ContentAlignment.MiddleRight, AutoSize = false
            };
            lblDesignTitle.SetBounds(0, y, 500, 24); y += 30;

            // رنگ اصلی
            Label lblPrimary = MakeFieldLabel("رنگ اصلی", y);
            _pnlPrimaryColor = MakeColorSwatch(140, y);
            y += 34;

            // رنگ فرعی
            Label lblSecondary = MakeFieldLabel("رنگ فرعی", y);
            _pnlSecondaryColor = MakeColorSwatch(140, y);
            y += 34;

            // فونت
            Label lblFont = MakeFieldLabel("فونت", y); y += 22;
            _cmbFont = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown };
            _cmbFont.Items.Add("");
            _cmbFont.Items.AddRange(FontChoices);
            _cmbFont.SetBounds(0, y, 250, 24); y += 30;

            // پس‌زمینهٔ روی
            y = AddImagePicker(right, "پس‌زمینهٔ روی کارت", y, out _txtBgFront);
            // پس‌زمینهٔ پشت
            y = AddImagePicker(right, "پس‌زمینهٔ پشت کارت", y, out _txtBgBack);
            // واترمارک
            y = AddImagePicker(right, "واترمارک", y, out _txtWatermark);

            Label lblOpacity = MakeFieldLabel("شفافیتِ واترمارک (٪)", y);
            _numWatermarkOpacity = new NumericUpDown { Minimum = 0, Maximum = 100, Value = 15 };
            _numWatermarkOpacity.SetBounds(140, y, 70, 24);
            y += 32;

            _chkHologram = new CheckBox { Text = "هولوگرامِ امنیتی", AutoSize = true, RightToLeft = RightToLeft.Yes, Checked = true };
            _chkHologram.SetBounds(0, y, 200, 24); y += 28;

            _chkQRCode = new CheckBox { Text = "نمایش QR Code", AutoSize = true, RightToLeft = RightToLeft.Yes };
            _chkQRCode.SetBounds(0, y, 200, 24); y += 28;

            _chkBarcode = new CheckBox { Text = "نمایش بارکد", AutoSize = true, RightToLeft = RightToLeft.Yes, Checked = true };
            _chkBarcode.SetBounds(0, y, 200, 24); y += 34;

            right.Controls.Add(lblName);
            right.Controls.Add(_txtName);
            right.Controls.Add(_lblDefaultTag);
            right.Controls.Add(lblFields);
            right.Controls.Add(_chkFields);
            right.Controls.Add(lblDesignTitle);
            right.Controls.Add(lblPrimary);
            right.Controls.Add(_pnlPrimaryColor);
            right.Controls.Add(lblSecondary);
            right.Controls.Add(_pnlSecondaryColor);
            right.Controls.Add(lblFont);
            right.Controls.Add(_cmbFont);
            right.Controls.Add(lblOpacity);
            right.Controls.Add(_numWatermarkOpacity);
            right.Controls.Add(_chkHologram);
            right.Controls.Add(_chkQRCode);
            right.Controls.Add(_chkBarcode);
        }

        private static Label MakeFieldLabel(string text, int y)
        {
            var lbl = new Label { Text = text, TextAlign = ContentAlignment.MiddleRight, AutoSize = false };
            lbl.SetBounds(0, y, 130, 24);
            return lbl;
        }

        private Panel MakeColorSwatch(int x, int y)
        {
            var pnl = new Panel { BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Cursor = Cursors.Hand };
            pnl.SetBounds(x, y, 60, 24);
            pnl.Click += delegate
            {
                using (var dlg = new ColorDialog { Color = pnl.BackColor })
                {
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                        pnl.BackColor = dlg.Color;
                }
            };
            return pnl;
        }

        // برچسب + textbox (فقط‌خواندنیِ مسیر) + دکمهٔ Browse — یک ردیف کامل.
        private int AddImagePicker(Panel parent, string label, int y, out TextBox txt)
        {
            Label lbl = MakeFieldLabel(label, y);
            TextBox txtLocal = new TextBox { RightToLeft = RightToLeft.Yes, ReadOnly = true };
            txtLocal.SetBounds(140, y, 260, 24);

            Button btnBrowse = UiTheme.CreateSecondaryButton("انتخاب", "");
            btnBrowse.SetBounds(406, y - 3, 70, 28);
            btnBrowse.Click += delegate
            {
                using (var ofd = new OpenFileDialog { Filter = "تصویر|*.png;*.jpg;*.jpeg;*.webp;*.bmp" })
                {
                    if (ofd.ShowDialog(this) == DialogResult.OK)
                        txtLocal.Text = ofd.FileName;
                }
            };

            Button btnClear = UiTheme.CreateSecondaryButton("حذف", "");
            btnClear.SetBounds(482, y - 3, 60, 28);
            btnClear.Click += delegate { txtLocal.Text = ""; };

            parent.Controls.Add(lbl);
            parent.Controls.Add(txtLocal);
            parent.Controls.Add(btnBrowse);
            parent.Controls.Add(btnClear);

            txt = txtLocal;
            return y + 32;
        }

        private void LoadTemplateList()
        {
            _templates = _repo.GetAll();
            _lstTemplates.Items.Clear();
            foreach (var t in _templates)
                _lstTemplates.Items.Add(t.Name + (t.IsDefault ? "  (پیش‌فرض)" : ""));

            if (_lstTemplates.Items.Count > 0)
                _lstTemplates.SelectedIndex = 0;
            else
                StartNewTemplate();
        }

        private void LoadSelectedIntoEditor()
        {
            int idx = _lstTemplates.SelectedIndex;
            if (idx < 0 || idx >= _templates.Count) return;
            ApplyTemplateToEditor(_templates[idx]);
        }

        private void ApplyTemplateToEditor(CardTemplate t)
        {
            _currentTemplateId = t.TemplateID;
            _txtName.Text = t.Name;
            _lblDefaultTag.Text = t.IsDefault ? "این قالبِ پیش‌فرض است (حذف نمی‌شود)" : "";

            for (int i = 0; i < CardTemplateRepository.ToggleableFields.Length; i++)
            {
                string field = CardTemplateRepository.ToggleableFields[i];
                bool enabled = !t.Fields.TryGetValue(field, out bool v) || v;
                _chkFields.SetItemChecked(i, enabled);
            }

            CardTemplateDesign d = t.Design ?? new CardTemplateDesign();
            _pnlPrimaryColor.BackColor = ParseColorOrDefault(d.PrimaryColor, Color.White);
            _pnlSecondaryColor.BackColor = ParseColorOrDefault(d.SecondaryColor, Color.White);
            _cmbFont.Text = d.FontFamily ?? "";
            _txtBgFront.Text = d.BackgroundFrontPath ?? "";
            _txtBgBack.Text = d.BackgroundBackPath ?? "";
            _txtWatermark.Text = d.WatermarkPath ?? "";
            _numWatermarkOpacity.Value = Math.Max(0, Math.Min(100, d.WatermarkOpacityPercent));
            _chkHologram.Checked = d.HologramEnabled;
            _chkQRCode.Checked = d.ShowQRCode;
            _chkBarcode.Checked = d.ShowBarcode;
        }

        private static Color ParseColorOrDefault(string hex, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(hex)) return fallback;
            try { return ColorTranslator.FromHtml(hex); }
            catch { return fallback; }
        }

        private static string ColorToHex(Color c, Color unsetSentinel)
        {
            return c.ToArgb() == unsetSentinel.ToArgb() ? "" : ColorTranslator.ToHtml(c);
        }

        private void StartNewTemplate()
        {
            _currentTemplateId = 0;
            _txtName.Text = "";
            _lblDefaultTag.Text = "";
            for (int i = 0; i < _chkFields.Items.Count; i++)
                _chkFields.SetItemChecked(i, true);

            _pnlPrimaryColor.BackColor = Color.White;
            _pnlSecondaryColor.BackColor = Color.White;
            _cmbFont.Text = "";
            _txtBgFront.Text = "";
            _txtBgBack.Text = "";
            _txtWatermark.Text = "";
            _numWatermarkOpacity.Value = 15;
            _chkHologram.Checked = true;
            _chkQRCode.Checked = false;
            _chkBarcode.Checked = true;

            _txtName.Focus();
        }

        private Dictionary<string, bool> CollectFields()
        {
            var fields = new Dictionary<string, bool>();
            for (int i = 0; i < CardTemplateRepository.ToggleableFields.Length; i++)
                fields[CardTemplateRepository.ToggleableFields[i]] = _chkFields.GetItemChecked(i);
            return fields;
        }

        private CardTemplateDesign CollectDesign()
        {
            return new CardTemplateDesign
            {
                PrimaryColor = ColorToHex(_pnlPrimaryColor.BackColor, Color.White),
                SecondaryColor = ColorToHex(_pnlSecondaryColor.BackColor, Color.White),
                FontFamily = _cmbFont.Text.Trim(),
                BackgroundFrontPath = _txtBgFront.Text.Trim(),
                BackgroundBackPath = _txtBgBack.Text.Trim(),
                WatermarkPath = _txtWatermark.Text.Trim(),
                WatermarkOpacityPercent = (int)_numWatermarkOpacity.Value,
                HologramEnabled = _chkHologram.Checked,
                ShowQRCode = _chkQRCode.Checked,
                ShowBarcode = _chkBarcode.Checked
            };
        }

        private void SaveCurrent()
        {
            string name = _txtName.Text.Trim();
            if (name.Length == 0)
            {
                Msg.Show("نام قالب را وارد کن.");
                return;
            }

            try
            {
                _currentTemplateId = _repo.Save(_currentTemplateId, name, CollectFields(), CollectDesign());
                UiTheme.ShowSuccess(this, "قالب ذخیره شد.");
                LoadTemplateList();
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در ذخیره قالب (شاید نامش تکراری است): " + ex.Message);
            }
        }

        private void DeleteCurrent()
        {
            if (_currentTemplateId <= 0) return;

            var t = _templates.Find(x => x.TemplateID == _currentTemplateId);
            if (t != null && t.IsDefault)
            {
                Msg.Show("قالبِ پیش‌فرض قابل حذف نیست.");
                return;
            }

            DialogResult confirm = MessageBox.Show("این قالب حذف شود؟", "تأیید حذف",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2,
                MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
            if (confirm != DialogResult.Yes) return;

            _repo.Delete(_currentTemplateId);
            LoadTemplateList();
        }

        // ─── پیش‌نمایش — روی اولین پروندهٔ در دسترس (بخش محدودهٔ مرکز) ────────
        // آموزش — پیش‌نمایشِ زنده نیاز به یک پروندهٔ واقعی دارد (چون کارت از
        // رویِ CardService.BuildCardData ساخته می‌شود، نه یک نمونهٔ ساختگی).
        // برای اینکه پیش‌نمایش «همان قالبِ در حالِ ویرایش» را نشان دهد (نه
        // نسخهٔ قبلاً ذخیره‌شده)، اول ذخیره می‌شود.
        private void PreviewCurrent()
        {
            SaveCurrent();
            if (_currentTemplateId <= 0) return;

            int caseId = FindAnyAccessibleCaseId();
            if (caseId <= 0)
            {
                Msg.Show("هیچ پرونده‌ای برای پیش‌نمایش پیدا نشد.");
                return;
            }

            using (var frm = new FrmGuardianCardPreview(caseId, _currentTemplateId))
                frm.ShowDialog(this);
        }

        private int FindAnyAccessibleCaseId()
        {
            int cid = SecurityContext.CenterFilterId;
            using (SQLiteConnection con = _db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(
                "SELECT CasID FROM TblCase WHERE IsArchived = 0 AND (@CID = 0 OR CenterID = @CID) ORDER BY CasID DESC LIMIT 1", con))
            {
                cmd.Parameters.AddWithValue("@CID", cid);
                con.Open();
                object result = cmd.ExecuteScalar();
                return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
            }
        }

        // ─── Export/Import — یک فایلِ JSON خودکفا (تصاویر base64) ────────────
        private void ExportCurrent()
        {
            if (_currentTemplateId <= 0)
            {
                Msg.Show("اول قالب را ذخیره کن.");
                return;
            }

            CardTemplate t = _repo.GetById(_currentTemplateId);
            if (t == null) return;

            using (var sfd = new SaveFileDialog { Filter = "قالبِ کارت|*.cardtemplate.json", FileName = t.Name + ".cardtemplate.json" })
            {
                if (sfd.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    var export = new Dictionary<string, object>
                    {
                        { "Name", t.Name },
                        { "Fields", t.Fields },
                        { "Design", new Dictionary<string, object>
                            {
                                { "PrimaryColor", t.Design.PrimaryColor },
                                { "SecondaryColor", t.Design.SecondaryColor },
                                { "FontFamily", t.Design.FontFamily },
                                { "WatermarkOpacityPercent", t.Design.WatermarkOpacityPercent },
                                { "HologramEnabled", t.Design.HologramEnabled },
                                { "ShowQRCode", t.Design.ShowQRCode },
                                { "ShowBarcode", t.Design.ShowBarcode },
                                { "BackgroundFrontImageBase64", EncodeImageFile(t.Design.BackgroundFrontPath) },
                                { "BackgroundBackImageBase64", EncodeImageFile(t.Design.BackgroundBackPath) },
                                { "WatermarkImageBase64", EncodeImageFile(t.Design.WatermarkPath) }
                            }
                        }
                    };

                    string json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Serialize(export);
                    File.WriteAllText(sfd.FileName, json, new System.Text.UTF8Encoding(false));
                    UiTheme.ShowSuccess(this, "قالب Export شد:\n" + sfd.FileName);
                }
                catch (Exception ex)
                {
                    Msg.Show("خطا در Export: " + ex.Message);
                }
            }
        }

        private void ImportTemplate()
        {
            using (var ofd = new OpenFileDialog { Filter = "قالبِ کارت|*.json" })
            {
                if (ofd.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    string json = File.ReadAllText(ofd.FileName);
                    var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                    var raw = serializer.Deserialize<Dictionary<string, object>>(json);

                    string name = raw.ContainsKey("Name") ? raw["Name"].ToString() : Path.GetFileNameWithoutExtension(ofd.FileName);
                    name = MakeUniqueTemplateName(name);

                    var fieldsRaw = raw.ContainsKey("Fields")
                        ? serializer.Deserialize<Dictionary<string, object>>(serializer.Serialize(raw["Fields"]))
                        : new Dictionary<string, object>();
                    var fields = new Dictionary<string, bool>();
                    foreach (var kv in fieldsRaw) fields[kv.Key] = Convert.ToBoolean(kv.Value);

                    var designRaw = raw.ContainsKey("Design")
                        ? serializer.Deserialize<Dictionary<string, object>>(serializer.Serialize(raw["Design"]))
                        : new Dictionary<string, object>();

                    string importFolder = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "CaseManagement", "CardTemplateImports", Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(importFolder);

                    var design = new CardTemplateDesign
                    {
                        PrimaryColor = GetStr(designRaw, "PrimaryColor"),
                        SecondaryColor = GetStr(designRaw, "SecondaryColor"),
                        FontFamily = GetStr(designRaw, "FontFamily"),
                        WatermarkOpacityPercent = designRaw.ContainsKey("WatermarkOpacityPercent") ? Convert.ToInt32(designRaw["WatermarkOpacityPercent"]) : 15,
                        HologramEnabled = !designRaw.ContainsKey("HologramEnabled") || Convert.ToBoolean(designRaw["HologramEnabled"]),
                        ShowQRCode = designRaw.ContainsKey("ShowQRCode") && Convert.ToBoolean(designRaw["ShowQRCode"]),
                        ShowBarcode = !designRaw.ContainsKey("ShowBarcode") || Convert.ToBoolean(designRaw["ShowBarcode"]),
                        BackgroundFrontPath = DecodeImageToFile(GetStr(designRaw, "BackgroundFrontImageBase64"), importFolder, "bg_front"),
                        BackgroundBackPath = DecodeImageToFile(GetStr(designRaw, "BackgroundBackImageBase64"), importFolder, "bg_back"),
                        WatermarkPath = DecodeImageToFile(GetStr(designRaw, "WatermarkImageBase64"), importFolder, "watermark")
                    };

                    int newId = _repo.Save(0, name, fields, design);
                    UiTheme.ShowSuccess(this, "قالب «" + name + "» وارد شد.");
                    LoadTemplateList();

                    for (int i = 0; i < _templates.Count; i++)
                        if (_templates[i].TemplateID == newId) { _lstTemplates.SelectedIndex = i; break; }
                }
                catch (Exception ex)
                {
                    Msg.Show("خطا در Import (فایل معتبر نیست؟): " + ex.Message);
                }
            }
        }

        private string MakeUniqueTemplateName(string baseName)
        {
            var existing = _repo.GetAll();
            string name = baseName;
            int suffix = 2;
            while (existing.Exists(t => t.Name == name))
            {
                name = baseName + " (" + suffix + ")";
                suffix++;
            }
            return name;
        }

        private static string GetStr(Dictionary<string, object> dict, string key)
        {
            return dict.ContainsKey(key) && dict[key] != null ? dict[key].ToString() : "";
        }

        private static string EncodeImageFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return "";
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                return Path.GetExtension(path).TrimStart('.') + ";" + Convert.ToBase64String(bytes);
            }
            catch { return ""; }
        }

        private static string DecodeImageToFile(string encoded, string destFolder, string baseName)
        {
            if (string.IsNullOrWhiteSpace(encoded)) return "";
            int sep = encoded.IndexOf(';');
            if (sep < 0) return "";

            string ext = encoded.Substring(0, sep);
            string base64 = encoded.Substring(sep + 1);
            try
            {
                byte[] bytes = Convert.FromBase64String(base64);
                string destPath = Path.Combine(destFolder, baseName + "." + ext);
                File.WriteAllBytes(destPath, bytes);
                return destPath;
            }
            catch { return ""; }
        }
    }
}
