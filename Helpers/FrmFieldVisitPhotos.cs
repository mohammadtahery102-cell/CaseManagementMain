using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace CaseManagement.Helpers
{
    // ═════════════════════════════════════════════════════════════════════════
    // عکس‌های بازدید میدانی.
    //
    // آموزش — چرا این فرم تازه ساخته شد: جدولِ TblFieldVisitPhoto، متدهای
    // FieldVisitService.AddPhoto/GetPhotos/DeletePhoto، پوشهٔ
    // FileHelper.SectionVisitPhotos و حتی قلّاب‌های بکاپ و همگام‌سازی همه از
    // فاز ۵ نوشته شده بودند — ولی **هیچ فرمی صدایشان نمی‌زد**. یعنی ستونِ
    // «تعداد عکس» در گریدِ بازدیدها همیشه صفر بود و کاربر هیچ راهی برای
    // افزودنِ عکسِ بازدید نداشت. اینجا فقط همان API موجود به یک رابط وصل
    // می‌شود؛ یک خط منطقِ تازه در سرویس نوشته نشد.
    //
    // آموزش — چرا دیالوگِ جدا و نه پنل داخلِ تبِ بازدیدها: آن تب از قبل
    // گرید + شش فیلد + سه دکمه دارد؛ افزودنِ نوارِ عکس به آن، تب را در
    // ارتفاع‌های کوچک می‌شکست. الگوی «دکمه → دیالوگِ اختصاصی» همان چیزی است
    // که فورم‌های رسمی و کارت شناسایی هم دارند.
    // ═════════════════════════════════════════════════════════════════════════
    public class FrmFieldVisitPhotos : Form
    {
        private readonly int _visitId;
        private readonly int _caseId;
        private readonly string _caseCode;
        private readonly string _visitTitle;

        private ListView lstPhotos;
        private ImageList _thumbs;
        private Label lblEmpty;
        private Label lblCount;
        private Button btnAdd, btnOpen, btnDelete, btnClose;

        private const int ThumbSize = 132;

        // عکس‌ها با همان قاعده‌ای که بقیهٔ عکس‌های پرونده دارند پذیرفته
        // می‌شوند — تعریفِ دومی ساخته نمی‌شود.
        private const long MinPhotoBytes = PhotoRules.MinVisitPhotoBytes;
        private const long MaxPhotoBytes = PhotoRules.MaxVisitPhotoBytes;

        public FrmFieldVisitPhotos(int visitId, int caseId, string caseCode, string visitTitle)
        {
            _visitId = visitId;
            _caseId = caseId;
            _caseCode = caseCode ?? "";
            _visitTitle = visitTitle ?? "";
            BuildUi();
        }

        // ═════════════════════════════════════════════════════════════════════
        private void BuildUi()
        {
            Text = "عکس‌های بازدید میدانی";
            RightToLeft = RightToLeft.Yes;
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(760, 520);
            ClientSize = new Size(880, 600);
            BackColor = UiTheme.Background;
            Font = UiTheme.Font(UiTheme.SizeBody);
            ShowInTaskbar = false;
            MinimizeBox = false;

            // ── سربرگ ────────────────────────────────────────────────────────
            // آموزش — عمداً Dock و نه SetBounds+Anchor: عرضِ واقعیِ پنلِ سربرگ
            // در زمانِ ساخت هنوز معلوم نیست، و کنترلِ چسبیده به راست پس از
            // اجرای Dock به بیرونِ پنل پرتاب می‌شد (در وارسیِ تصویری، عنوان
            // اصلاً دیده نمی‌شد). با RightToLeft = Yes مقدارِ ContentAlignment
            // آینه می‌شود، پس MiddleLeft یعنی «سمتِ راست».
            var head = new Panel
            {
                Dock = DockStyle.Top,
                Height = 62,
                BackColor = UiTheme.PrimaryDark,
                Padding = new Padding(16, 8, 16, 6)
            };

            var lblTitle = new Label
            {
                Dock = DockStyle.Top,
                Height = 26,
                Text = "عکس‌های بازدید میدانی",
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Font = UiTheme.FontBold(UiTheme.SizeLarge),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoSize = false
            };

            var lblSub = new Label
            {
                Dock = DockStyle.Top,
                Height = 20,
                Text = Subtitle(),
                ForeColor = Color.FromArgb(200, 220, 240),
                BackColor = Color.Transparent,
                Font = UiTheme.Font(UiTheme.SizeSmall),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoSize = false
            };

            // Dock از بالاترین اندیس به پایین‌ترین اعمال می‌شود: عنوان آخر
            // اضافه می‌شود تا بالا بنشیند و زیرنویس زیرِ آن.
            head.Controls.Add(lblSub);
            head.Controls.Add(lblTitle);

            // ── نوارِ دکمه‌ها ────────────────────────────────────────────────
            var bar = new Panel { Dock = DockStyle.Bottom, Height = 60, BackColor = UiTheme.CardBack };

            btnAdd = UiTheme.CreateButton("افزودن عکس", "+", UiTheme.Primary);
            btnOpen = UiTheme.CreateSecondaryButton("باز کردن", "➤");
            btnDelete = UiTheme.CreateSecondaryButton("حذف عکس", "✕");
            btnClose = UiTheme.CreateSecondaryButton("بستن", "");

            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(12, 12, 12, 12),
                BackColor = UiTheme.CardBack
            };

            foreach (Button b in new[] { btnAdd, btnOpen, btnDelete, btnClose })
            {
                b.Height = 36;
                b.Width = 140;
                b.Margin = new Padding(0, 0, 8, 0);
                flow.Controls.Add(b);
            }
            btnClose.Width = 100;

            btnDelete.ForeColor = UiTheme.Danger;
            btnDelete.FlatAppearance.BorderColor = UiTheme.Danger;

            lblCount = new Label
            {
                AutoSize = false,
                Font = UiTheme.Font(UiTheme.SizeSmall),
                ForeColor = UiTheme.TextMuted,
                TextAlign = ContentAlignment.MiddleRight,
                Width = 220,
                Height = 36,
                Margin = new Padding(16, 0, 0, 0)
            };
            flow.Controls.Add(lblCount);

            bar.Controls.Add(flow);

            btnAdd.Click += delegate { AddPhotos(); };
            btnOpen.Click += delegate { OpenSelected(); };
            btnDelete.Click += delegate { DeleteSelected(); };
            btnClose.Click += delegate { Close(); };
            CancelButton = btnClose;

            // ── فهرستِ عکس‌ها ────────────────────────────────────────────────
            var body = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(14, 12, 14, 12),
                BackColor = UiTheme.Background
            };

            _thumbs = new ImageList
            {
                ImageSize = new Size(ThumbSize, ThumbSize),
                ColorDepth = ColorDepth.Depth32Bit
            };

            lstPhotos = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.LargeIcon,
                LargeImageList = _thumbs,
                MultiSelect = true,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                RightToLeft = RightToLeft.Yes,
                RightToLeftLayout = true,
                HideSelection = false
            };
            lstPhotos.DoubleClick += delegate { OpenSelected(); };
            lstPhotos.KeyDown += delegate (object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Delete) DeleteSelected();
            };

            lblEmpty = new Label
            {
                Dock = DockStyle.Fill,
                Text = "برای این بازدید هنوز عکسی ثبت نشده است." + Environment.NewLine +
                       "با دکمهٔ «افزودن عکس» می‌توانید چند عکس را یک‌جا انتخاب کنید.",
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = UiTheme.TextMuted,
                BackColor = Color.White,
                Font = UiTheme.Font(UiTheme.SizeSmall),
                Visible = false
            };

            body.Controls.Add(lblEmpty);
            body.Controls.Add(lstPhotos);

            Controls.Add(body);
            Controls.Add(bar);
            Controls.Add(head);
        }

        private string Subtitle()
        {
            var parts = new List<string>();
            if (_caseCode.Length > 0) parts.Add("پرونده: " + _caseCode);
            if (_visitTitle.Length > 0) parts.Add("بازدید: " + _visitTitle);
            parts.Add(SecurityContext.CenterDisplay ?? "");
            return string.Join("   ·   ", parts.ToArray());
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            LoadPhotos();
        }

        // تصاویرِ ImageList دستی آزاد می‌شوند: هر بندانگشتی یک Bitmap مستقل
        // است و Dispose خودکارِ فرم سراغِ محتویاتِ ImageList نمی‌رود.
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            DisposeThumbs();
            base.OnFormClosed(e);
        }

        private void DisposeThumbs()
        {
            try
            {
                foreach (Image img in _thumbs.Images) img.Dispose();
                _thumbs.Images.Clear();
            }
            catch { }
        }

        // ═════════════════════════════════════════════════════════════════════
        private void LoadPhotos()
        {
            Cursor previous = Cursor;
            Cursor = Cursors.WaitCursor;
            try
            {
                lstPhotos.BeginUpdate();
                lstPhotos.Items.Clear();
                DisposeThumbs();

                DataTable table = FieldVisitService.GetPhotos(_visitId);
                int missing = 0;

                foreach (DataRow row in table.Rows)
                {
                    int photoId = Convert.ToInt32(row["PhotoID"]);
                    string path = Str(row["FilePath"]);
                    string description = Str(row["Description"]);

                    bool exists = false;
                    try { exists = !string.IsNullOrWhiteSpace(path) && File.Exists(path); }
                    catch { }
                    if (!exists) missing++;

                    _thumbs.Images.Add(photoId.ToString(), MakeThumb(exists ? path : null));

                    string caption = description.Length > 0
                        ? description
                        : (string.IsNullOrWhiteSpace(path) ? "بدون فایل" : Path.GetFileName(path));
                    if (!exists) caption += "  (فایل پیدا نشد)";

                    var item = new ListViewItem(caption, photoId.ToString());
                    item.Tag = new PhotoRef { PhotoID = photoId, Path = path, Exists = exists };
                    if (!exists) item.ForeColor = UiTheme.Danger;
                    lstPhotos.Items.Add(item);
                }

                lblCount.Text = table.Rows.Count == 0
                    ? ""
                    : ReportDoc.Fa(table.Rows.Count) + " عکس" +
                      (missing > 0 ? "  ·  " + ReportDoc.Fa(missing) + " فایل مفقود" : "");

                lblEmpty.Visible = table.Rows.Count == 0;
                lblEmpty.BringToFront();
            }
            catch (Exception ex)
            {
                UiTheme.ShowError(this, "خطا در خواندن عکس‌های بازدید: " + ex.Message);
            }
            finally
            {
                lstPhotos.EndUpdate();
                Cursor = previous;
            }

            UpdateButtons();
        }

        private void UpdateButtons()
        {
            bool any = lstPhotos.SelectedItems.Count > 0;
            btnOpen.Enabled = any;
            btnDelete.Enabled = any;
        }

        private sealed class PhotoRef
        {
            public int PhotoID;
            public string Path;
            public bool Exists;
        }

        // بندانگشتیِ مربعی با پس‌زمینهٔ روشن — فایل با FileShare.ReadWrite و
        // داخلِ حافظه خوانده می‌شود تا روی دیسک قفل نماند.
        private Image MakeThumb(string path)
        {
            var bmp = new Bitmap(ThumbSize, ThumbSize);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(UiTheme.Background);

                if (string.IsNullOrWhiteSpace(path))
                {
                    using (var br = new SolidBrush(UiTheme.TextMuted))
                    using (var sf = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    })
                        g.DrawString("؟", UiTheme.FontBold(20f), br,
                            new RectangleF(0, 0, ThumbSize, ThumbSize), sf);
                    return bmp;
                }

                try
                {
                    using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var src = Image.FromStream(fs, false, true))
                    {
                        float ratio = Math.Min((float)ThumbSize / src.Width, (float)ThumbSize / src.Height);
                        int w = Math.Max(1, (int)(src.Width * ratio));
                        int h = Math.Max(1, (int)(src.Height * ratio));
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.DrawImage(src, (ThumbSize - w) / 2, (ThumbSize - h) / 2, w, h);
                    }
                }
                catch
                {
                    using (var br = new SolidBrush(UiTheme.Danger))
                    using (var sf = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    })
                        g.DrawString("✕", UiTheme.FontBold(20f), br,
                            new RectangleF(0, 0, ThumbSize, ThumbSize), sf);
                }
            }
            return bmp;
        }

        // ═════════════════════════════════════════════════════════════════════
        private void AddPhotos()
        {
            if (!CaseManagement.Enterprise.PermissionService.Require("Case.Edit"))
            {
                UiTheme.ShowWarning(this, "کاربر فقط مشاهده اجازه ثبت عکس بازدید ندارد.");
                return;
            }

            if (_visitId <= 0 || _caseId <= 0)
            {
                UiTheme.ShowWarning(this, "اول بازدید را ذخیره کنید.");
                return;
            }

            if (string.IsNullOrWhiteSpace(_caseCode))
            {
                UiTheme.ShowWarning(this, "کد اختصاصی پرونده مشخص نیست؛ عکس ذخیره نمی‌شود.");
                return;
            }

            string[] files;
            using (var ofd = new OpenFileDialog
            {
                Title = "انتخاب عکس‌های بازدید",
                Multiselect = true,
                CheckFileExists = true,
                Filter = "فایل‌های تصویری|*.jpg;*.jpeg;*.png|فایل‌های JPG|*.jpg;*.jpeg|فایل‌های PNG|*.png"
            })
            {
                if (ofd.ShowDialog(this) != DialogResult.OK) return;
                files = ofd.FileNames;
            }

            // یک توضیح برای کلِ دسته — TblFieldVisitPhoto.Description ستونِ
            // آزاد است و سرویس متدِ ویرایشِ توضیح ندارد، پس همین‌جا پرسیده
            // می‌شود و بعداً با حذف/افزودنِ دوباره اصلاح می‌گردد.
            Dictionary<string, string> answer = CaseManagement.Enterprise.EntPrompt.Edit(this,
                "توضیح عکس‌ها (اختیاری)",
                CaseManagement.Enterprise.EntField.Text("Desc", "توضیح", ""));
            if (answer == null) return;

            string description = "";
            answer.TryGetValue("Desc", out description);

            int added = 0;
            var rejected = new List<string>();

            Cursor previous = Cursor;
            Cursor = Cursors.WaitCursor;
            try
            {
                foreach (string file in files)
                {
                    string reason;
                    if (!PhotoRules.IsValidPhoto(file, MinPhotoBytes, MaxPhotoBytes, out reason))
                    {
                        rejected.Add(Path.GetFileName(file) + " — " + reason);
                        continue;
                    }

                    string savedPath = FileHelper.SaveFileToCaseFolder(
                        file, _caseCode, FileHelper.SectionVisitPhotos,
                        FileHelper.CleanName(_caseCode) + "-Visit" + _visitId, "");

                    if (string.IsNullOrWhiteSpace(savedPath) || !File.Exists(savedPath))
                    {
                        rejected.Add(Path.GetFileName(file) + " — " + FileHelper.LastError);
                        continue;
                    }

                    if (FieldVisitService.AddPhoto(_visitId, _caseId, savedPath, description) > 0)
                        added++;
                }
            }
            catch (Exception ex)
            {
                UiTheme.ShowError(this, "خطا در ثبت عکس: " + ex.Message);
            }
            finally { Cursor = previous; }

            LoadPhotos();

            if (rejected.Count == 0)
            {
                if (added > 0)
                    UiTheme.ShowSuccess(this, ReportDoc.Fa(added) + " عکس ثبت شد.");
                return;
            }

            UiTheme.ShowWarning(this,
                ReportDoc.Fa(added) + " عکس ثبت شد و " + ReportDoc.Fa(rejected.Count) +
                " عکس پذیرفته نشد:" + Environment.NewLine +
                string.Join(Environment.NewLine, rejected.ToArray()));
        }

        private void OpenSelected()
        {
            if (lstPhotos.SelectedItems.Count == 0) return;
            var reference = lstPhotos.SelectedItems[0].Tag as PhotoRef;
            if (reference == null) return;

            if (!reference.Exists)
            {
                UiTheme.ShowWarning(this, "فایل این عکس روی دیسک پیدا نشد:" +
                    Environment.NewLine + reference.Path);
                return;
            }

            try { Process.Start(new ProcessStartInfo(reference.Path) { UseShellExecute = true }); }
            catch (Exception ex) { UiTheme.ShowError(this, "عکس باز نشد: " + ex.Message); }
        }

        private void DeleteSelected()
        {
            if (!CaseManagement.Enterprise.PermissionService.Require("Case.Delete"))
            {
                UiTheme.ShowWarning(this, "کاربر اجازه حذف عکس بازدید را ندارد.");
                return;
            }

            int count = lstPhotos.SelectedItems.Count;
            if (count == 0) return;

            if (!UiTheme.ShowConfirm(this,
                    "آیا " + ReportDoc.Fa(count) + " عکس انتخاب‌شده از پرونده حذف شود؟",
                    "حذف عکس بازدید"))
                return;

            int deleted = 0;
            try
            {
                foreach (ListViewItem item in lstPhotos.SelectedItems)
                {
                    var reference = item.Tag as PhotoRef;
                    if (reference == null) continue;

                    // فایل روی دیسک عمداً دست‌نخورده می‌ماند — همان محافظه‌کاریِ
                    // FrmDocs و عکسِ نماینده: رکورد حذف می‌شود، فایل نه.
                    if (FieldVisitService.DeletePhoto(reference.PhotoID)) deleted++;
                }
            }
            catch (Exception ex)
            {
                UiTheme.ShowError(this, "خطا در حذف عکس: " + ex.Message);
            }

            LoadPhotos();
            if (deleted > 0)
                UiTheme.ShowSuccess(this, ReportDoc.Fa(deleted) + " عکس حذف شد.");
        }

        private static string Str(object value)
        {
            return value == null || value == DBNull.Value ? "" : Convert.ToString(value);
        }
    }
}
