using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using CaseManagement.DAL;
using CaseManagement.Helpers;

namespace CaseManagement.Sync
{
    // ═════════════════════════════════════════════════════════════════════════
    // FrmSyncSimple — صفحه‌ی جدید و اصلیِ همگام‌سازی (به درخواستِ کاربر).
    //
    // شش بخشِ کاملاً مستقل، هر کدام یک مسیر + یک دکمه‌ی «آپلود / بروزرسانی»:
    //   سرپرستان (Guardians.html) · اعضا (Members.html) · عکسِ سرپرست (Photos)
    //   · عکسِ جمعیِ خانواده (FamilyPhoto) · عکسِ اعضا (MemberPhotos) · اسناد (Documents)
    //
    // هر دکمه کاملاً جداگانه کار می‌کند — اگر فقط یکی از این شش مسیر پر شده
    // باشد، فقط همان یکی وارد/بروزرسانی می‌شود؛ نیازی به بقیه نیست.
    //
    // ⚠ همان موتورِ قدیمی و آزموده‌شده: HtmlSyncProvider → SyncComparer →
    // SyncEngine برای سرپرست/عضو، و MediaScanner → MediaSyncEngine برای عکس‌ها
    // و اسناد. این فرم فقط یک رابطِ کاربریِ ساده‌تر روی همان موتور است؛ هیچ
    // منطقِ تطبیق/نوشتن جدیدی اینجا اختراع نشده. ویزارد قدیمیِ ۸مرحله‌ای
    // (FrmSyncWizard) برای «بستهٔ ترکیبی/دسته‌ای» هنوز از دکمه‌ی «روش پیشرفته»
    // در دسترس است — چیزی حذف نشده.
    // ═════════════════════════════════════════════════════════════════════════
    public sealed class FrmSyncSimple : Form
    {
        private TextBox _txtGuardians, _txtMembers, _txtPhotos, _txtFamilyPhoto, _txtMemberPhotos, _txtDocs;
        private Label _lblGuardiansStatus, _lblMembersStatus, _lblPhotosStatus, _lblFamilyPhotoStatus, _lblMemberPhotosStatus, _lblDocsStatus;
        private Button _btnGuardiansUpload, _btnMembersUpload, _btnPhotosUpload, _btnFamilyPhotoUpload, _btnMemberPhotosUpload, _btnDocsUpload;
        private CheckBox _chkBackup;
        private Label _lblBusy;
        private Button _btnCancelOp;
        private bool _busy;

        // آموزش — لغو فقط برای ردیف‌های عکس (Photos/FamilyPhoto/MemberPhotos)
        // معنا دارد: MediaScanner/MediaSyncEngine از CancellationToken
        // پشتیبانی می‌کنند. SyncEngine.Apply (سرپرستان/اعضا) چنین پارامتری
        // ندارد — برای همان دو ردیف، دکمه‌ی لغو غیرفعال می‌ماند (فایلِ HTML
        // معمولاً کوچک و سریع است، نه هزاران فایلِ عکس).
        private System.Threading.CancellationTokenSource _activeCancel;

        public FrmSyncSimple()
        {
            BuildUi();
        }

        private void BuildUi()
        {
            Text = "همگام‌سازی — عکس‌ها و اطلاعات از سامانه مرکزی";
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor = UiTheme.Background;
            Font = UiTheme.Font(UiTheme.SizeBody);
            UiTheme.MakeMainWindow(this, 980, 760);

            var header = new Panel { Dock = DockStyle.Top, Height = 58, BackColor = UiTheme.PrimaryDark };
            header.Controls.Add(new Label
            {
                Text = "همگام‌سازی — عکس‌ها و اطلاعات از سامانه مرکزی",
                Dock = DockStyle.Fill, ForeColor = Color.White,
                Font = UiTheme.FontBold(UiTheme.SizeLarge),
                TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(0, 0, 20, 0)
            });
            Controls.Add(header);

            var footer = new Panel { Dock = DockStyle.Bottom, Height = 60, BackColor = UiTheme.CardBack };
            var btnHelp = UiTheme.CreateSecondaryButton("راهنما", "؟");
            btnHelp.SetBounds(16, 13, 110, 34);
            btnHelp.Click += delegate { FrmSyncHelp.ShowHelp(this); };

            var btnAdvanced = UiTheme.CreateSecondaryButton("روش پیشرفته (بستهٔ ترکیبی)", "⚙");
            btnAdvanced.SetBounds(134, 13, 220, 34);
            btnAdvanced.Click += delegate { using (var frm = new FrmSyncWizard()) frm.ShowDialog(this); };

            // آموزش — رفعِ اشکال: footer.Width اینجا هنوز صفر است (کنترل هنوز به
            // فرم اضافه/چیده نشده)؛ محاسبه‌ی مکان بر اساسِ آن، دکمه را در
            // مکانِ نادرست می‌گذاشت. عرضِ ثابتِ فرم (۹۸۰، از MakeMainWindow)
            // برای محاسبه‌ی مکانِ اولیه استفاده می‌شود؛ Anchor بقیه‌ی کار را
            // اگر فرم بزرگ‌تر شود انجام می‌دهد.
            var btnClose = UiTheme.CreateButton("بستن", "✕", UiTheme.Primary);
            btnClose.SetBounds(980 - 146 - 20, 13, 130, 34);
            btnClose.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnClose.Click += delegate { Close(); };

            footer.Controls.Add(btnHelp);
            footer.Controls.Add(btnAdvanced);
            footer.Controls.Add(btnClose);
            Controls.Add(footer);
            CancelButton = btnClose;

            var scroller = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(20, 16, 20, 16) };

            var intro = new Label
            {
                Text = "هر بخش کاملاً مستقل است. فقط مسیرِ همان چیزی را که می‌خواهید بفرستید انتخاب کنید و دکمه‌ی «آپلود / بروزرسانی» را بزنید — نیازی نیست همه را با هم پر کنید.",
                AutoSize = false, Location = new Point(0, 0), Size = new Size(900, 44),
                ForeColor = UiTheme.TextMuted, TextAlign = ContentAlignment.TopRight
            };
            scroller.Controls.Add(intro);

            _chkBackup = new CheckBox
            {
                Text = "قبل از هر آپلود، بکاپ کامل گرفته شود (توصیه می‌شود)",
                Checked = true, AutoSize = true, Location = new Point(500, 52)
            };
            scroller.Controls.Add(_chkBackup);

            int y = 90;
            y = AddHtmlRow(scroller, y, "فایل سرپرستان (Guardians.html)",
                "اطلاعاتِ سرپرست‌ها را از این فایل می‌خواند و پرونده‌های تازه می‌سازد یا پرونده‌های موجود را به‌روز می‌کند.",
                out _txtGuardians, out _lblGuardiansStatus, out _btnGuardiansUpload, isFolder: false);

            y = AddHtmlRow(scroller, y, "فایل اعضا (Members.html)",
                "اعضای خانواده را از این فایل می‌خواند. خانواده باید یا از قبل در برنامه باشد یا همزمان با فایل سرپرستان ساخته شود.",
                out _txtMembers, out _lblMembersStatus, out _btnMembersUpload, isFolder: false);

            y = AddFolderRow(scroller, y, "عکسِ تکیِ سرپرست (پوشه‌ی Photos)",
                "نامِ هر عکس باید دقیقاً کدِ اختصاصیِ همان پرونده باشد. مثال: 100245.jpg",
                out _txtPhotos, out _lblPhotosStatus, out _btnPhotosUpload);
            _btnPhotosUpload.Click += async delegate { await RunMediaAsync(_txtPhotos.Text, MediaCategoryKind.Photo, _lblPhotosStatus, "عکسِ سرپرست"); };

            y = AddFolderRow(scroller, y, "عکسِ جمعیِ خانواده (پوشه‌ی FamilyPhoto)",
                "نامِ هر عکس، مثلِ بالا، کدِ اختصاصیِ پرونده است.",
                out _txtFamilyPhoto, out _lblFamilyPhotoStatus, out _btnFamilyPhotoUpload);
            _btnFamilyPhotoUpload.Click += async delegate { await RunMediaAsync(_txtFamilyPhoto.Text, MediaCategoryKind.FamilyPhoto, _lblFamilyPhotoStatus, "عکسِ جمعیِ خانواده"); };

            y = AddFolderRow(scroller, y, "عکسِ اعضا (پوشه‌ی MemberPhotos)",
                "داخلِ این پوشه، به‌ازای هر پرونده یک پوشه با نامِ کدِ اختصاصی بسازید و عکسِ هر عضو را با نامِ همان عضو در آن بگذارید.",
                out _txtMemberPhotos, out _lblMemberPhotosStatus, out _btnMemberPhotosUpload);
            _btnMemberPhotosUpload.Click += async delegate { await RunMediaAsync(_txtMemberPhotos.Text, MediaCategoryKind.MemberPhoto, _lblMemberPhotosStatus, "عکسِ اعضا"); };

            y = AddFolderRow(scroller, y, "اسنادِ پرونده‌ها (پوشه‌ی Documents)",
                "داخلِ این پوشه، به‌ازای هر پرونده یک پوشه با نامِ کدِ اختصاصی بسازید و اسنادش (تذکره، قباله و مانند آن) را در آن بگذارید.",
                out _txtDocs, out _lblDocsStatus, out _btnDocsUpload);
            _btnDocsUpload.Click += async delegate { await RunMediaAsync(_txtDocs.Text, MediaCategoryKind.Document, _lblDocsStatus, "اسناد"); };

            _btnGuardiansUpload.Click += async delegate { await RunHtmlAsync(_txtGuardians.Text, isGuardians: true, _lblGuardiansStatus); };
            _btnMembersUpload.Click += async delegate { await RunHtmlAsync(_txtMembers.Text, isGuardians: false, _lblMembersStatus); };

            _lblBusy = new Label
            {
                Text = "", ForeColor = UiTheme.Primary, AutoSize = false,
                Location = new Point(120, y + 6), Size = new Size(780, 24), TextAlign = ContentAlignment.MiddleRight
            };
            scroller.Controls.Add(_lblBusy);

            _btnCancelOp = UiTheme.CreateSecondaryButton("لغو", "✕");
            _btnCancelOp.SetBounds(0, y, 110, 32);
            _btnCancelOp.Visible = false;
            _btnCancelOp.Click += delegate { try { _activeCancel?.Cancel(); } catch { } };
            scroller.Controls.Add(_btnCancelOp);

            Controls.Add(scroller);
            scroller.BringToFront();
        }

        private enum MediaCategoryKind { Photo, FamilyPhoto, MemberPhoto, Document }

        // ─── ردیفِ فایل (Guardians/Members) ────────────────────────────────────
        private int AddHtmlRow(Panel host, int y, string title, string hint,
            out TextBox txt, out Label status, out Button btnUpload, bool isFolder)
        {
            var card = BuildCard(host, y, out int cardHeight);

            var lblTitle = new Label
            {
                Text = title, Font = UiTheme.FontBold(UiTheme.SizeBody), ForeColor = UiTheme.TextDark,
                AutoSize = false, TextAlign = ContentAlignment.MiddleRight
            };
            lblTitle.SetBounds(16, 10, 500, 24);

            var lblHint = new Label
            {
                Text = hint, Font = UiTheme.Font(9F), ForeColor = UiTheme.TextMuted,
                AutoSize = false, TextAlign = ContentAlignment.MiddleRight
            };
            lblHint.SetBounds(16, 34, 850, 20);

            txt = new TextBox { ReadOnly = true, RightToLeft = RightToLeft.Yes };
            txt.SetBounds(16, 60, 560, 27);

            var btnPick = UiTheme.CreateSecondaryButton("انتخاب فایل...", "📄");
            btnPick.SetBounds(584, 58, 140, 31);
            TextBox capturedTxt = txt;
            btnPick.Click += delegate
            {
                using (var ofd = new OpenFileDialog { Filter = "فایل HTML|*.html;*.htm|همه فایل‌ها|*.*" })
                    if (ofd.ShowDialog(this) == DialogResult.OK) capturedTxt.Text = ofd.FileName;
            };

            btnUpload = UiTheme.CreateButton("آپلود / بروزرسانی", "⇑", UiTheme.Primary);
            btnUpload.SetBounds(732, 58, 150, 31);

            status = new Label
            {
                Text = "", Font = UiTheme.Font(9F), ForeColor = UiTheme.TextMuted,
                AutoSize = false, TextAlign = ContentAlignment.MiddleRight
            };
            status.SetBounds(16, 92, 850, 20);

            card.Controls.Add(lblTitle);
            card.Controls.Add(lblHint);
            card.Controls.Add(txt);
            card.Controls.Add(btnPick);
            card.Controls.Add(btnUpload);
            card.Controls.Add(status);

            return y + cardHeight + 14;
        }

        // ─── ردیفِ پوشه (Photos/FamilyPhoto/MemberPhotos) ──────────────────────
        private int AddFolderRow(Panel host, int y, string title, string hint,
            out TextBox txt, out Label status, out Button btnUpload)
        {
            var card = BuildCard(host, y, out int cardHeight);

            var lblTitle = new Label
            {
                Text = title, Font = UiTheme.FontBold(UiTheme.SizeBody), ForeColor = UiTheme.TextDark,
                AutoSize = false, TextAlign = ContentAlignment.MiddleRight
            };
            lblTitle.SetBounds(16, 10, 500, 24);

            var lblHint = new Label
            {
                Text = hint, Font = UiTheme.Font(9F), ForeColor = UiTheme.TextMuted,
                AutoSize = false, TextAlign = ContentAlignment.MiddleRight
            };
            lblHint.SetBounds(16, 34, 850, 20);

            txt = new TextBox { ReadOnly = true, RightToLeft = RightToLeft.Yes };
            txt.SetBounds(16, 60, 560, 27);

            var btnPick = UiTheme.CreateSecondaryButton("انتخاب پوشه...", "📁");
            btnPick.SetBounds(584, 58, 140, 31);
            TextBox capturedTxt = txt;
            btnPick.Click += delegate
            {
                using (var fbd = new FolderBrowserDialog())
                    if (fbd.ShowDialog(this) == DialogResult.OK) capturedTxt.Text = fbd.SelectedPath;
            };

            btnUpload = UiTheme.CreateButton("آپلود / بروزرسانی", "⇑", UiTheme.Primary);
            btnUpload.SetBounds(732, 58, 150, 31);

            status = new Label
            {
                Text = "", Font = UiTheme.Font(9F), ForeColor = UiTheme.TextMuted,
                AutoSize = false, TextAlign = ContentAlignment.MiddleRight
            };
            status.SetBounds(16, 92, 850, 20);

            card.Controls.Add(lblTitle);
            card.Controls.Add(lblHint);
            card.Controls.Add(txt);
            card.Controls.Add(btnPick);
            card.Controls.Add(btnUpload);
            card.Controls.Add(status);

            return y + cardHeight + 14;
        }

        private Panel BuildCard(Panel host, int y, out int height)
        {
            height = 122;
            var card = new Panel
            {
                Location = new Point(0, y),
                Size = new Size(900, height),
                BackColor = UiTheme.CardBack,
                BorderStyle = BorderStyle.FixedSingle
            };
            host.Controls.Add(card);
            return card;
        }

        // ─── اجرای دکمه‌های Guardians/Members ──────────────────────────────────
        private async Task RunHtmlAsync(string filePath, bool isGuardians, Label status)
        {
            if (_busy) return;
            string what = isGuardians ? "فایل سرپرستان" : "فایل اعضا";

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                Msg.Show("اول " + what + " را انتخاب کنید.");
                return;
            }

            SetBusy(true, "در حال خواندن " + what + "...");
            try
            {
                var source = new SyncSource();
                if (isGuardians) source.GuardiansFilePath = filePath; else source.MembersFilePath = filePath;

                var provider = new HtmlSyncProvider();
                var progress = new Progress<SyncProgress>(p => SetBusy(true, p.Phase));
                ParsedSyncData parsed = await Task.Run(() => provider.Parse(source, progress));

                int rowCount = isGuardians ? parsed.Guardians.Count : parsed.Members.Count;
                if (rowCount == 0)
                {
                    status.Text = "هیچ ردیفی در فایل پیدا نشد.";
                    UiTheme.ShowWarning(this, "هیچ ردیفی در " + what + " پیدا نشد. فایل را بررسی کنید.");
                    return;
                }

                var comparer = new SyncComparer(new DatabaseHelper());
                SyncPlan plan = await Task.Run(() => comparer.BuildPlan(parsed, progress));

                var records = isGuardians ? plan.Guardians : plan.Members;
                int newCount = isGuardians ? plan.NewGuardians : plan.Members.Count(r => r.Action == SyncAction.New);
                int updCount = isGuardians ? plan.UpdatedGuardians : plan.Members.Count(r => r.Action == SyncAction.Update);
                var errRecords = records.Where(r => r.Action == SyncAction.Error).ToList();

                string summary = what + ": " + newCount + " جدید، " + updCount + " به‌روزرسانی" +
                                  (errRecords.Count > 0 ? "، " + errRecords.Count + " رد شده" : "") + ".\nادامه و ثبت در برنامه؟" +
                                  BuildDetailLines(errRecords.Select(r => "کد «" + r.PublicCode + "»: " + r.ErrorMessage));

                if (!UiTheme.ShowConfirm(this, summary, "تأیید " + what))
                {
                    status.Text = "لغو شد.";
                    return;
                }

                if (_chkBackup.Checked)
                {
                    SetBusy(true, "در حال گرفتنِ بکاپ...");
                    // آموزش — بکاپ اینجا جداگانه لازم نیست: SyncEngine.Apply خودش
                    // پیش از نوشتن بکاپ می‌گیرد (options.TakeBackup)؛ تکرارش
                    // فقط زمان تلف می‌کرد.
                }

                var engine = new SyncEngine(new DatabaseHelper());
                var options = new SyncOptions { TakeBackup = _chkBackup.Checked };
                SetBusy(true, "در حال ثبت در برنامه...");
                SyncReport report = await Task.Run(() => engine.Apply(plan, options, progress));

                if (!report.Success)
                {
                    status.Text = "ناموفق: " + report.ErrorMessage;
                    UiTheme.ShowError(this, what + " ثبت نشد: " + report.ErrorMessage);
                    return;
                }

                int inserted = isGuardians ? report.GuardiansInserted : report.MembersInserted;
                int updated = isGuardians ? report.GuardiansUpdated : report.MembersUpdated;
                status.Text = "انجام شد — " + inserted + " تازه، " + updated + " به‌روزرسانی. " +
                              DateTime.Now.ToString("HH:mm:ss");
                UiTheme.ShowSuccess(this, what + " با موفقیت ثبت شد.\nتازه: " + inserted + "   به‌روزرسانی: " + updated);
            }
            catch (Exception ex)
            {
                status.Text = "خطا: " + ex.Message;
                UiTheme.ShowError(this, "خطا در پردازشِ " + what + ": " + ex.Message);
            }
            finally
            {
                SetBusy(false, null);
            }
        }

        // ─── اجرای دکمه‌های Photos/FamilyPhoto/MemberPhotos ────────────────────
        private async Task RunMediaAsync(string folder, MediaCategoryKind kind, Label status, string title)
        {
            if (_busy) return;

            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                Msg.Show("اول پوشه‌ی " + title + " را انتخاب کنید.");
                return;
            }

            _activeCancel = new System.Threading.CancellationTokenSource();
            var cancel = _activeCancel.Token;
            SetBusy(true, "در حال بررسیِ " + title + "...", cancellable: true);
            try
            {
                var scanner = new MediaScanner(new DatabaseHelper());
                var progress = new Progress<SyncProgress>(p => SetBusy(true, p.Phase, cancellable: true));

                MediaPlan plan;
                switch (kind)
                {
                    case MediaCategoryKind.FamilyPhoto:
                        plan = await Task.Run(() => scanner.ScanFamilyPhotoOnly(folder, progress, cancel));
                        break;
                    case MediaCategoryKind.MemberPhoto:
                        plan = await Task.Run(() => scanner.ScanMemberPhotosOnly(folder, progress, cancel));
                        break;
                    case MediaCategoryKind.Document:
                        plan = await Task.Run(() => scanner.ScanDocumentsOnly(folder, progress, cancel));
                        break;
                    default:
                        plan = await Task.Run(() => scanner.ScanPhotosOnly(folder, progress, cancel));
                        break;
                }

                if (plan.ValidationErrors.Count > 0)
                {
                    status.Text = plan.ValidationErrors[0];
                    UiTheme.ShowError(this, plan.ValidationErrors[0]);
                    return;
                }

                // آموزش — اسناد در plan.Documents است، بقیه (Photo/FamilyPhoto/
                // MemberPhoto) همه در plan.Photos (با Kind متفاوت) — دقیقاً همان
                // ساختاری که MediaScanner/MediaSyncEngine از قبل استفاده می‌کنند.
                bool isDoc = kind == MediaCategoryKind.Document;
                var itemList = isDoc ? plan.Documents : plan.Photos;
                var rejected = itemList.Where(p => !p.IsApplicable).ToList();
                int toAdd = isDoc ? plan.DocsToAdd : plan.PhotosToAdd;
                int toReplace = isDoc ? plan.DocsToReplace : plan.PhotosToReplace;
                int noMatchCount = isDoc ? plan.DocsNoMatch : plan.PhotosNoMatch;
                int totalApplicable = itemList.Count(i => i.Selected && i.IsApplicable);
                string itemWord = isDoc ? "سند" : "عکس";

                if (totalApplicable == 0)
                {
                    string msg = itemList.Count == 0
                        ? "هیچ " + itemWord + "ی در این پوشه پیدا نشد."
                        : noMatchCount + " " + itemWord + " پیدا شد ولی هیچ‌کدام کدشان با پرونده‌ای در دیتابیس (" +
                          plan.TotalCasesInDatabase + " پرونده) مطابقت نداشت. مطمئن شوید نامِ پوشه/فایل دقیقاً کدِ اختصاصیِ پرونده است و پرونده‌ها از قبل در برنامه ثبت شده‌اند." +
                          BuildDetailLines(rejected.Select(p => p.FileName + ": " + p.Message));
                    status.Text = "هیچ‌چیزِ قابل‌اعمالی پیدا نشد.";
                    UiTheme.ShowWarning(this, msg);
                    return;
                }

                string summary = title + ": " + toAdd + " " + itemWord + "ِ تازه" +
                    (toReplace > 0 ? "، " + toReplace + " مورد که از قبل " + itemWord + " دارند" : "") +
                    (noMatchCount > 0 ? "، " + noMatchCount + " بدون پرونده‌ی مطابق (وارد نمی‌شود)" : "") + ".\nادامه و ثبت؟" +
                    BuildDetailLines(rejected.Select(p => p.FileName + ": " + p.Message));

                if (!UiTheme.ShowConfirm(this, summary, "تأیید " + title))
                {
                    status.Text = "لغو شد.";
                    return;
                }

                // آموزش — رفعِ اشکالِ واقعی (پیدا شده در خودبررسی): Classify به‌صورتِ
                // پیش‌فرض هر موردِ «جایگزینی» (پرونده‌ای که از قبل عکس/سند دارد) را
                // Selected=false می‌گذارد تا خودکار رونویسی نشود. صفحه‌ی قدیمیِ
                // ویزارد یک جدولِ پیش‌نمایش داشت که کاربر می‌توانست تک‌تک تأیید
                // کند؛ این صفحه‌ی ساده چنین جدولی ندارد، پس بدونِ این پرسش، آن
                // موارد همیشه بی‌صدا نادیده گرفته می‌شدند — نه خطا، نه گزارش.
                if (toReplace > 0)
                {
                    bool replaceApproved = UiTheme.ShowConfirm(this,
                        toReplace + " مورد از قبل " + itemWord + " دارند. " + itemWord + "ِ قبلی با تازه جایگزین شود؟\n" +
                        "(اگر «خیر» را بزنید، فقط پرونده‌های بدونِ " + itemWord + "ِ قبلی ثبت می‌شوند.)",
                        "جایگزینیِ موجود");

                    if (replaceApproved)
                        foreach (var item in itemList.Where(p => p.Action == MediaAction.Replace))
                            item.Selected = true;
                }

                if (_chkBackup.Checked)
                {
                    SetBusy(true, "در حال گرفتنِ بکاپ...", cancellable: true);
                    try
                    {
                        string parent = SyncEngine.ResolveBackupFolder();
                        new BackupHelper().ExportBackup(parent);
                    }
                    catch (Exception bex)
                    {
                        status.Text = "بکاپ ناموفق بود؛ چیزی ثبت نشد.";
                        UiTheme.ShowError(this, "بکاپ گرفته نشد، برای ایمنی عملیات متوقف شد:\n" + bex.Message);
                        return;
                    }
                }

                var mediaEngine = new MediaSyncEngine(new DatabaseHelper());
                SetBusy(true, "در حال ثبتِ " + title + "...", cancellable: true);
                MediaReport report = await Task.Run(() => mediaEngine.Apply(plan, progress, cancel));

                if (!report.Success)
                {
                    status.Text = "ناموفق: " + report.ErrorMessage;
                    UiTheme.ShowError(this, title + " ثبت نشد: " + report.ErrorMessage);
                    return;
                }

                int imported = isDoc ? report.DocumentsImported : report.PhotosImported;
                status.Text = "انجام شد — " + imported + " " + itemWord + " وارد شد. " + DateTime.Now.ToString("HH:mm:ss");
                UiTheme.ShowSuccess(this, title + " با موفقیت ثبت شد.\n" + itemWord + "ِ واردشده: " + imported);
            }
            catch (OperationCanceledException)
            {
                status.Text = "با کلیکِ شما لغو شد.";
            }
            catch (Exception ex)
            {
                status.Text = "خطا: " + ex.Message;
                UiTheme.ShowError(this, "خطا در پردازشِ " + title + ": " + ex.Message);
            }
            finally
            {
                SetBusy(false, null);
                if (_activeCancel != null) { _activeCancel.Dispose(); _activeCancel = null; }
            }
        }

        private void SetBusy(bool busy, string message)
        {
            SetBusy(busy, message, cancellable: false);
        }

        private void SetBusy(bool busy, string message, bool cancellable)
        {
            _busy = busy;
            _lblBusy.Text = message ?? "";
            _btnGuardiansUpload.Enabled = !busy;
            _btnMembersUpload.Enabled = !busy;
            _btnPhotosUpload.Enabled = !busy;
            _btnFamilyPhotoUpload.Enabled = !busy;
            _btnMemberPhotosUpload.Enabled = !busy;
            _btnDocsUpload.Enabled = !busy;
            _btnCancelOp.Visible = busy && cancellable;
            UseWaitCursor = busy;
        }

        // فهرستِ خلاصه‌ی دلایلِ رد شدن/هشدار — حداکثر چند مورد نشان داده
        // می‌شود تا پیام غیرقابل‌خواندن نشود؛ بقیه با یک شمارنده جمع می‌شوند.
        private static string BuildDetailLines(System.Collections.Generic.IEnumerable<string> lines, int max = 12)
        {
            var list = lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
            if (list.Count == 0) return "";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine();
            sb.AppendLine("جزئیات:");
            for (int i = 0; i < Math.Min(max, list.Count); i++)
                sb.AppendLine("• " + list[i]);
            if (list.Count > max)
                sb.AppendLine("... و " + (list.Count - max) + " موردِ دیگر");
            return sb.ToString();
        }
    }
}
