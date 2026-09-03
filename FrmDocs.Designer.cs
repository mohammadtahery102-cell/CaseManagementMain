namespace CaseManagement
{
    partial class FrmDocs
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// آموزش — درخواستِ کاربر: «تب اسناد باید دقیقاً مثل بخش سرپرست باشد
        /// (فیلدها، رنگ‌آمیزی‌ها، سایه‌ها، فریم و قالب پنجره‌ها)».
        ///
        /// چیدمانِ قبلی این فرم با مختصاتِ مطلق (Location/Size روی هر کنترل) و
        /// برچسبِ کنارِ فیلد بود — یعنی تنها فرمی که هنوز زبانِ بصریِ قدیمی را
        /// داشت، در حالی که FrmCase و FrmFamily هر دو از FieldBox (برچسبِ بالا
        /// + ورودیِ گردگوشه) داخلِ SectionCard (کارتِ سفیدِ گردگوشه با سربرگ)
        /// استفاده می‌کنند. این فایل حالا همان دو پوسته را دارد.
        ///
        /// بسیار مهم — هیچ کنترلی حذف یا تغییرِ نام نداد و هیچ رویدادی عوض
        /// نشد: همان txtDocType/txtDocNo/dgvDocs/btnSave/... با همان نام‌ها،
        /// همان TabIndexها و همان Clickها هستند؛ فقط والد و ظاهرشان عوض شده،
        /// پس کلِ منطقِ FrmDocs.cs دست‌نخورده کار می‌کند.
        /// </summary>
        private void InitializeComponent()
        {
            this.txtDocType = new System.Windows.Forms.TextBox();
            this.txtOriginalFileName = new System.Windows.Forms.TextBox();
            this.txtDocFilePath = new System.Windows.Forms.TextBox();
            this.txtRelatedCaseRef = new System.Windows.Forms.TextBox();
            this.txtDocDescription = new System.Windows.Forms.TextBox();
            this.btnBrowseDocFile = new System.Windows.Forms.Button();
            this.btnEdit = new System.Windows.Forms.Button();
            this.btnSave = new System.Windows.Forms.Button();
            this.btnNew = new System.Windows.Forms.Button();
            this.btnDelete = new System.Windows.Forms.Button();
            this.btnPrint = new System.Windows.Forms.Button();
            this.dgvDocs = new System.Windows.Forms.DataGridView();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.btnOpenDoc = new System.Windows.Forms.Button();
            this.picPreview = new System.Windows.Forms.PictureBox();
            this.txtDocCategory = new System.Windows.Forms.TextBox();
            this.txtDocTags = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.txtSearch = new System.Windows.Forms.TextBox();
            this.txtDocNo = new System.Windows.Forms.TextBox();
            this.label7 = new System.Windows.Forms.Label();
            this.lblHeadInfo = new System.Windows.Forms.Label();
            this.lblDocsHeader = new System.Windows.Forms.Label();
            this.btnPrevCase = new System.Windows.Forms.Button();
            this.btnNextCase = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.dgvDocs)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.picPreview)).BeginInit();
            this.SuspendLayout();

            // ═══ نوار سرِ فرم + پیمایشِ پرونده ═══════════════════════════════
            // عیناً همان نوارِ سرمه‌ای که FrmFamily دارد، تا دو تبِ «اعضاء» و
            // «اسناد» از یک قالب پیروی کنند.
            this.lblHeadInfo.Name         = "lblHeadInfo";
            this.lblHeadInfo.Dock         = System.Windows.Forms.DockStyle.Fill;
            this.lblHeadInfo.BackColor    = CaseManagement.Helpers.UiTheme.PrimaryDark;
            this.lblHeadInfo.ForeColor    = System.Drawing.Color.White;
            this.lblHeadInfo.Font         = CaseManagement.Helpers.UiTheme.FontBold(CaseManagement.Helpers.UiTheme.SizeSmall);
            this.lblHeadInfo.TextAlign    = System.Drawing.ContentAlignment.MiddleRight;
            this.lblHeadInfo.Padding      = new System.Windows.Forms.Padding(0, 0, 16, 0);
            this.lblHeadInfo.AutoEllipsis = true;
            this.lblHeadInfo.Text         = "اسناد پرونده: —";

            SetCaseNavButton(this.btnPrevCase, "btnPrevCase", "◀  پروندهٔ قبلی", this.btnPrevCase_Click);
            SetCaseNavButton(this.btnNextCase, "btnNextCase", "پروندهٔ بعدی  ▶", this.btnNextCase_Click);

            this.panCaseNav = new System.Windows.Forms.FlowLayoutPanel();
            this.panCaseNav.Name          = "panCaseNav";
            this.panCaseNav.Dock          = System.Windows.Forms.DockStyle.Left;
            this.panCaseNav.Width         = 300;
            this.panCaseNav.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
            this.panCaseNav.WrapContents  = false;
            this.panCaseNav.Padding       = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.panCaseNav.BackColor     = CaseManagement.Helpers.UiTheme.PrimaryDark;
            this.panCaseNav.Visible       = false; // فقط در حالتِ embedded روشن می‌شود
            this.panCaseNav.Controls.Add(this.btnPrevCase);
            this.panCaseNav.Controls.Add(this.btnNextCase);

            var headBar = new System.Windows.Forms.Panel();
            headBar.Name      = "headBar";
            headBar.Dock      = System.Windows.Forms.DockStyle.Fill;
            headBar.BackColor = CaseManagement.Helpers.UiTheme.PrimaryDark;
            headBar.Controls.Add(this.lblHeadInfo);
            headBar.Controls.Add(this.panCaseNav);
            this.headBarPanel = headBar;

            // ═══ ستون فیلدها — همان FieldBox/SectionCard بخش سرپرست ══════════
            this.txtDocNo.Name     = "txtDocNo";
            this.txtDocNo.TabIndex = 18;
            this.txtDocNo.ReadOnly = true;
            this.txtDocNo.TabStop  = false;

            this.txtDocType.Name        = "txtDocType";
            this.txtDocType.TabIndex    = 0;
            this.txtRelatedCaseRef.Name = "txtRelatedCaseRef";
            this.txtRelatedCaseRef.TabIndex = 3;
            this.txtDocCategory.Name    = "txtDocCategory";
            this.txtDocCategory.TabIndex = 16;
            this.txtDocTags.Name        = "txtDocTags";
            this.txtDocTags.TabIndex    = 17;

            var tlpDocFields = MkFieldGrid(2);
            AddField(tlpDocFields, this.label1, "نوع سند",                 this.txtDocType);
            AddField(tlpDocFields, this.label3, "شماره پرونده مرتبط",      this.txtRelatedCaseRef);
            AddField(tlpDocFields, this.label5, "دسته‌بندی سند",           this.txtDocCategory);
            AddField(tlpDocFields, this.label6, "برچسب‌ها (با ، جدا کنید)", this.txtDocTags);
            AddField(tlpDocFields, this.label7, "شماره سند (خودکار)",      this.txtDocNo);

            // «توضیح سند» — چندخطی و تمام‌عرض، دقیقاً همان الگوی «شرح تفصیلی
            // معلولیت» در FrmFamily و «شرح وضعیت فوری» در FrmCase.
            this.txtDocDescription.Name        = "txtDocDescription";
            this.txtDocDescription.Multiline   = true;
            this.txtDocDescription.ScrollBars  = System.Windows.Forms.ScrollBars.Vertical;
            this.txtDocDescription.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.txtDocDescription.TextAlign   = System.Windows.Forms.HorizontalAlignment.Right;
            this.txtDocDescription.TabIndex    = 4;

            var boxDocDescription = new CaseManagement.Helpers.FieldBox(
                this.label4, "توضیح سند", this.txtDocDescription);
            // ابعاد عمداً همان اعدادِ «شرح وضعیت فوری» در FrmCase است تا این
            // کادر با کادرِ معادلش در بخشِ سرپرست دقیقاً هم‌اندازه دیده شود.
            boxDocDescription.Dock   = System.Windows.Forms.DockStyle.Top;
            boxDocDescription.Height = 132;
            boxDocDescription.Margin = new System.Windows.Forms.Padding(18, 4, 18, 12);

            var descHost = new System.Windows.Forms.Panel();
            descHost.Name      = "descHost";
            descHost.Dock      = System.Windows.Forms.DockStyle.Top;
            descHost.Height    = 140;
            descHost.Padding   = new System.Windows.Forms.Padding(18, 0, 18, 10);
            descHost.BackColor = System.Drawing.Color.Transparent;
            descHost.Controls.Add(boxDocDescription);

            this.btnBrowseDocFile.Name     = "btnBrowseDocFile";
            this.btnBrowseDocFile.Text     = "انتخاب فایل سند";
            this.btnBrowseDocFile.Size     = new System.Drawing.Size(150, 36);
            this.btnBrowseDocFile.Margin   = new System.Windows.Forms.Padding(3, 3, 3, 3);
            this.btnBrowseDocFile.TabIndex = 5;
            this.btnBrowseDocFile.Click   += new System.EventHandler(this.btnBrowseDocFile_Click);

            this.btnOpenDoc.Name     = "btnOpenDoc";
            this.btnOpenDoc.Text     = "بازکردن فایل";
            this.btnOpenDoc.Size     = new System.Drawing.Size(130, 36);
            this.btnOpenDoc.Margin   = new System.Windows.Forms.Padding(3, 3, 3, 3);
            this.btnOpenDoc.TabIndex = 15;
            this.btnOpenDoc.Click   += new System.EventHandler(this.btnOpenDoc_Click);

            var fileButtons = new System.Windows.Forms.FlowLayoutPanel();
            fileButtons.Name          = "fileButtons";
            fileButtons.Dock          = System.Windows.Forms.DockStyle.Top;
            fileButtons.Height        = 50;
            fileButtons.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
            fileButtons.WrapContents  = false;
            fileButtons.Padding       = new System.Windows.Forms.Padding(16, 2, 16, 6);
            fileButtons.BackColor     = System.Drawing.Color.Transparent;
            fileButtons.Controls.Add(this.btnBrowseDocFile);
            fileButtons.Controls.Add(this.btnOpenDoc);

            // ترتیبِ افزودن عمدی است (همان قاعدهٔ Dock=Top در FrmCase): کنترلی
            // که آخر اضافه شود بالاتر می‌نشیند ⇒ شبکهٔ فیلدها بالا، بعد توضیح،
            // بعد دکمه‌های فایل.
            var docFieldsContent = new System.Windows.Forms.Panel();
            docFieldsContent.Name         = "docFieldsContent";
            docFieldsContent.Dock         = System.Windows.Forms.DockStyle.Top;
            docFieldsContent.AutoSize     = true;
            docFieldsContent.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            docFieldsContent.BackColor    = System.Drawing.Color.Transparent;
            docFieldsContent.Controls.Add(fileButtons);
            docFieldsContent.Controls.Add(descHost);
            docFieldsContent.Controls.Add(tlpDocFields);

            var formPanel = MkTabScroller(MkSectionCard("مشخصات سند", docFieldsContent));
            formPanel.Name = "formPanel";

            // ═══ کنترل‌های داخلی (نمایش داده نمی‌شوند) ═══════════════════════
            // نام فایل و مسیر فایل داخلی هستند و در UI دیده نمی‌شوند، ولی کدِ
            // FrmDocs.cs مقدارشان را می‌خواند/می‌نویسد — پس مثلِ الگوی
            // hiddenCaseControls در FrmCase، فقط پنهان می‌شوند نه حذف.
            this.label2.Text = "نام فایل";
            this.label2.Visible = false;
            this.txtOriginalFileName.Name = "txtOriginalFileName";
            this.txtOriginalFileName.TabIndex = 1;
            this.txtOriginalFileName.Visible = false;

            System.Windows.Forms.Label lblFile = new System.Windows.Forms.Label();
            lblFile.Text = "مسیر فایل";
            lblFile.Visible = false;
            this.txtDocFilePath.Name = "txtDocFilePath";
            this.txtDocFilePath.TabIndex = 2;
            this.txtDocFilePath.Visible = false;

            var hiddenDocControls = new System.Windows.Forms.Panel();
            hiddenDocControls.Name     = "hiddenDocControls";
            hiddenDocControls.Size     = new System.Drawing.Size(2, 2);
            hiddenDocControls.Location = new System.Drawing.Point(-4000, -4000);
            hiddenDocControls.Visible  = false;
            hiddenDocControls.TabStop  = false;
            hiddenDocControls.Controls.Add(this.label2);
            hiddenDocControls.Controls.Add(this.txtOriginalFileName);
            hiddenDocControls.Controls.Add(lblFile);
            hiddenDocControls.Controls.Add(this.txtDocFilePath);

            // ═══ پیش‌نمایش سند — داخل همان کارتِ سفیدِ گردگوشه ════════════════
            this.picPreview.Name        = "picPreview";
            this.picPreview.Dock        = System.Windows.Forms.DockStyle.Fill;
            this.picPreview.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.picPreview.SizeMode    = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.picPreview.BackColor   = CaseManagement.Helpers.UiTheme.Background;
            this.picPreview.TabStop     = false;

            System.Windows.Forms.Label lblPreviewTitle = new System.Windows.Forms.Label();
            lblPreviewTitle.Text      = "پیش‌نمایش سند";
            lblPreviewTitle.Dock      = System.Windows.Forms.DockStyle.Top;
            lblPreviewTitle.Height    = 40;
            lblPreviewTitle.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            lblPreviewTitle.Padding   = new System.Windows.Forms.Padding(0, 0, 18, 0);
            lblPreviewTitle.Font      = CaseManagement.Helpers.UiTheme.FontBold(CaseManagement.Helpers.UiTheme.SizeMedium);
            lblPreviewTitle.ForeColor = CaseManagement.Helpers.UiTheme.TextDark;
            lblPreviewTitle.BackColor = System.Drawing.Color.Transparent;

            var previewCard = new CaseManagement.Helpers.SectionCard();
            previewCard.Name    = "previewCard";
            previewCard.Dock    = System.Windows.Forms.DockStyle.Fill;
            previewCard.Padding = new System.Windows.Forms.Padding(12, 2, 12, 12);
            previewCard.Controls.Add(this.picPreview);
            previewCard.Controls.Add(lblPreviewTitle);

            System.Windows.Forms.Panel previewPanel = new System.Windows.Forms.Panel();
            previewPanel.Name    = "previewPanel";
            previewPanel.Dock    = System.Windows.Forms.DockStyle.Fill;
            previewPanel.Padding = new System.Windows.Forms.Padding(4, 10, 4, 10);
            previewPanel.Controls.Add(previewCard);

            // ═══ فهرست اسناد — همان سربرگ/کارتِ فهرستِ اعضاء در FrmFamily ═════
            this.dgvDocs.Name = "dgvDocs";
            this.dgvDocs.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvDocs.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvDocs.RowHeadersWidth = 51;
            this.dgvDocs.RowTemplate.Height = 24;
            this.dgvDocs.TabIndex = 10;
            this.dgvDocs.CellClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dgvDocs_CellClick);
            this.dgvDocs.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dgvDocs_CellContentClick);

            System.Windows.Forms.Label lblSearch = new System.Windows.Forms.Label();
            lblSearch.Text      = "جستجو";
            lblSearch.Dock      = System.Windows.Forms.DockStyle.Right;
            lblSearch.Width     = 55;
            lblSearch.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            lblSearch.Font      = CaseManagement.Helpers.UiTheme.FontBold(CaseManagement.Helpers.UiTheme.SizeSmall);

            this.txtSearch.Name = "txtSearch";
            this.txtSearch.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtSearch.TextChanged += new System.EventHandler(this.txtSearch_TextChanged);

            System.Windows.Forms.Panel searchPanel = new System.Windows.Forms.Panel();
            searchPanel.Name    = "searchPanel";
            searchPanel.Dock    = System.Windows.Forms.DockStyle.Top;
            searchPanel.Height  = 34;
            searchPanel.Padding = new System.Windows.Forms.Padding(0, 0, 0, 6);
            searchPanel.Controls.Add(this.txtSearch);
            searchPanel.Controls.Add(lblSearch);

            System.Windows.Forms.Panel gridPanel = new System.Windows.Forms.Panel();
            gridPanel.Name      = "gridPanel";
            gridPanel.Dock      = System.Windows.Forms.DockStyle.Fill;
            gridPanel.Padding   = new System.Windows.Forms.Padding(10, 4, 10, 10);
            gridPanel.BackColor = CaseManagement.Helpers.UiTheme.CardBack;
            gridPanel.Controls.Add(this.dgvDocs);
            gridPanel.Controls.Add(searchPanel);

            this.lblDocsHeader.Name      = "lblDocsHeader";
            this.lblDocsHeader.Dock      = System.Windows.Forms.DockStyle.Fill;
            this.lblDocsHeader.Text      = "فهرست اسناد پرونده";
            this.lblDocsHeader.Font      = CaseManagement.Helpers.UiTheme.FontBold(CaseManagement.Helpers.UiTheme.SizeMedium);
            this.lblDocsHeader.ForeColor = CaseManagement.Helpers.UiTheme.TextDark;
            this.lblDocsHeader.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.lblDocsHeader.BackColor = System.Drawing.Color.Transparent;

            System.Windows.Forms.Panel listHeader = new System.Windows.Forms.Panel();
            listHeader.Name      = "listHeader";
            listHeader.Dock      = System.Windows.Forms.DockStyle.Top;
            listHeader.Height    = 46;
            listHeader.BackColor = CaseManagement.Helpers.UiTheme.CardBack;
            listHeader.Padding   = new System.Windows.Forms.Padding(14, 6, 14, 0);
            listHeader.Controls.Add(this.lblDocsHeader);

            System.Windows.Forms.Panel listPanel = new System.Windows.Forms.Panel();
            listPanel.Name      = "listPanel";
            listPanel.Dock      = System.Windows.Forms.DockStyle.Fill;
            listPanel.BackColor = CaseManagement.Helpers.UiTheme.CardBack;
            listPanel.Padding   = new System.Windows.Forms.Padding(0, 0, 6, 0);
            listPanel.Controls.Add(gridPanel);
            listPanel.Controls.Add(listHeader);

            // ═══ نوار دکمه‌ها — همان چیدمان/رنگ‌بندی FrmFamily ════════════════
            SetActionButton(this.btnNew,    "btnNew",    "جدید",             8, this.btnNew_Click);
            SetActionButton(this.btnSave,   "btnSave",   "ذخیره",            7, this.btnSave_Click);
            SetActionButton(this.btnEdit,   "btnEdit",   "ویرایش",           6, this.btnEdit_Click);
            SetActionButton(this.btnDelete, "btnDelete", "حذف",              9, this.btnDelete_Click);
            SetActionButton(this.btnPrint,  "btnPrint",  "چاپ فهرست اسناد", 11, this.btnPrint_Click);
            this.btnPrint.Size = new System.Drawing.Size(160, 38);

            var mainActions = new System.Windows.Forms.FlowLayoutPanel();
            mainActions.Name          = "mainActions";
            mainActions.Dock          = System.Windows.Forms.DockStyle.Fill;
            mainActions.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
            mainActions.WrapContents  = false;
            mainActions.BackColor     = System.Drawing.Color.Transparent;
            mainActions.Controls.Add(this.btnNew);
            mainActions.Controls.Add(this.btnSave);
            mainActions.Controls.Add(this.btnEdit);
            mainActions.Controls.Add(this.btnDelete);

            var secondaryActions = new System.Windows.Forms.FlowLayoutPanel();
            secondaryActions.Name          = "secondaryActions";
            secondaryActions.Dock          = System.Windows.Forms.DockStyle.Left;
            secondaryActions.Width         = 180;
            secondaryActions.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
            secondaryActions.WrapContents  = false;
            secondaryActions.BackColor     = System.Drawing.Color.Transparent;
            secondaryActions.Controls.Add(this.btnPrint);

            System.Windows.Forms.Panel buttonBar = new System.Windows.Forms.Panel();
            buttonBar.Name      = "buttonBar";
            buttonBar.Dock      = System.Windows.Forms.DockStyle.Fill;
            buttonBar.BackColor = CaseManagement.Helpers.UiTheme.CardBack;
            buttonBar.Padding   = new System.Windows.Forms.Padding(14, 8, 14, 8);
            buttonBar.Controls.Add(mainActions);
            buttonBar.Controls.Add(secondaryActions);

            //
            // FrmDocs
            //
            // آموزش — این یادداشت قبلاً می‌گفت «چون چیدمان Dock/TableLayout است
            // AutoScale لازم نیست». آن استدلال فقط برای «تغییر اندازه‌ی پنجره»
            // درست است، نه برای «تغییر DPI»: با AutoScaleMode.None، روی
            // نمایشگر ۱۵۰٪ خودِ کنترل‌ها و فونت‌ها کوچک می‌مانند و فرم ریز و
            // ناخوانا دیده می‌شود (حالا که برنامه واقعاً DPI-aware شده، ویندوز
            // دیگر تصویر را کش نمی‌آورد تا این را بپوشاند).
            // مهاجرت به مقیاسِ DPI — لایه ۲ چارچوب چیدمان واکنش‌گرا؛ توضیح کامل
            // اینکه چرا هر دو خط لازم است در FrmCase.Designer.cs آمده.
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.Font = new System.Drawing.Font("Segoe UI", 9.75F);
            this.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.RightToLeftLayout = true;
            this.ClientSize = new System.Drawing.Size(1240, 620);

            // آموزش — فاز A4 (تب اسناد پرونده داخل FrmCase): بلوکِ قفلِ اندازهٔ
            // پنجره (FormBorderStyle/MinimumSize/MaximumSize/...) که این‌جا
            // بود، به FrmDocs_Load منتقل شد و پشتِ شرطِ IsEmbedded گذاشته شد —
            // دقیقاً همان اصلاحی که برای FrmFamily در فاز ۱ انجام شد. علتش:
            // وقتی این فرم TopLevel=false و Dock=Fill داخل یک تب جاسازی شود،
            // MinimumSize/MaximumSize قفل‌شده (روی اندازهٔ طراحی) مانع از این
            // می‌شود که اندازه‌اش را با تب هماهنگ کند.

            // ریشه چیدمان: ۳ ستون (فیلدها | پیش‌نمایش | فهرست) و ۳ ردیف
            // (نوارِ سرِ پرونده | محتوا | دکمه‌ها) — همان اسکلتِ rootLayout در
            // FrmFamily، تا دو تب کنارِ هم یک قالبِ پنجره داشته باشند.
            System.Windows.Forms.TableLayoutPanel rootLayout = new System.Windows.Forms.TableLayoutPanel();
            rootLayout.Name = "rootLayout";
            rootLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            rootLayout.ColumnCount = 3;
            rootLayout.RowCount = 3;
            rootLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 380F));
            rootLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 300F));
            rootLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 60F));

            rootLayout.Controls.Add(headBar, 0, 0);
            rootLayout.SetColumnSpan(headBar, 3);
            rootLayout.Controls.Add(formPanel, 0, 1);
            rootLayout.Controls.Add(previewPanel, 1, 1);
            rootLayout.Controls.Add(listPanel, 2, 1);
            rootLayout.Controls.Add(buttonBar, 0, 2);
            rootLayout.SetColumnSpan(buttonBar, 3);

            this.Controls.Add(rootLayout);
            this.Controls.Add(hiddenDocControls);
            this.Name = "FrmDocs";
            this.Text = "اسناد";
            this.Load += new System.EventHandler(this.FrmDocs_Load);
            ((System.ComponentModel.ISupportInitialize)(this.dgvDocs)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.picPreview)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        // ─── Helpers ─────────────────────────────────────────────────────────
        // آموزش — این متدها عمداً همان رفتارِ MkCaseTab/MkCaseCard/MkFieldGrid
        // در FrmCase.Designer.cs و FrmFamily.Designer.cs را تکرار می‌کنند (نه
        // ارجاع به آن‌ها، چون آن‌ها private و مخصوصِ همان فرم‌اند). نتیجه: هر
        // سه فرم یک زبانِ بصریِ واحد دارند.

        private static System.Windows.Forms.Panel MkTabScroller(System.Windows.Forms.Control card)
        {
            FieldsScrollPanel scroller = new FieldsScrollPanel();
            scroller.Dock       = System.Windows.Forms.DockStyle.Fill;
            scroller.AutoScroll = true;
            scroller.Padding    = new System.Windows.Forms.Padding(10, 10, 10, 10);
            scroller.BackColor  = System.Drawing.Color.Transparent;
            scroller.Controls.Add(card);
            return scroller;
        }

        private static CaseManagement.Helpers.SectionCard MkSectionCard(
            string title, System.Windows.Forms.Control content)
        {
            content.Dock = System.Windows.Forms.DockStyle.Top;

            var header = new System.Windows.Forms.Label();
            header.Dock      = System.Windows.Forms.DockStyle.Top;
            header.Height    = 40;
            header.Text      = title;
            header.Font      = CaseManagement.Helpers.UiTheme.FontBold(CaseManagement.Helpers.UiTheme.SizeMedium);
            header.ForeColor = CaseManagement.Helpers.UiTheme.TextDark;
            header.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            header.Padding   = new System.Windows.Forms.Padding(0, 0, 18, 0);
            header.BackColor = System.Drawing.Color.Transparent;

            var card = new CaseManagement.Helpers.SectionCard();
            card.Dock         = System.Windows.Forms.DockStyle.Top;
            card.AutoSize     = true;
            card.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            card.Margin       = new System.Windows.Forms.Padding(0, 0, 0, 12);
            card.Padding      = new System.Windows.Forms.Padding(2, 2, 2, 10);
            card.Controls.Add(content);
            card.Controls.Add(header);
            return card;
        }

        private static System.Windows.Forms.TableLayoutPanel MkFieldGrid(int columns)
        {
            var tlp = new System.Windows.Forms.TableLayoutPanel();
            tlp.Dock         = System.Windows.Forms.DockStyle.Top;
            tlp.AutoSize     = true;
            tlp.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            tlp.Padding      = new System.Windows.Forms.Padding(18, 14, 18, 10);
            tlp.ColumnCount  = columns;
            tlp.BackColor    = System.Drawing.Color.Transparent;
            for (int i = 0; i < columns; i++)
                tlp.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(
                    System.Windows.Forms.SizeType.Percent, 100F / columns));
            return tlp;
        }

        // خودِ کنترلِ ورودی همان شیء قبلی می‌ماند، پس نام، رویدادها و هر کدی
        // که با آن کار می‌کند دست‌نخورده باقی است.
        private static CaseManagement.Helpers.FieldBox AddField(
            System.Windows.Forms.TableLayoutPanel grid,
            System.Windows.Forms.Label captionLabel, string captionText,
            System.Windows.Forms.Control field)
        {
            var box = new CaseManagement.Helpers.FieldBox(captionLabel, captionText, field);
            box.Dock = System.Windows.Forms.DockStyle.Top;
            grid.Controls.Add(box);
            return box;
        }

        private static void SetActionButton(System.Windows.Forms.Button btn, string name,
            string text, int tabIndex, System.EventHandler onClick)
        {
            btn.Name     = name;
            btn.Text     = text;
            btn.Size     = new System.Drawing.Size(110, 38);
            btn.Margin   = new System.Windows.Forms.Padding(3, 3, 3, 3);
            btn.TabIndex = tabIndex;
            btn.Click   += onClick;
        }

        // دکمهٔ «پروندهٔ قبلی/بعدی» روی نوارِ سرمه‌ایِ بالای فرم.
        private static void SetCaseNavButton(System.Windows.Forms.Button btn, string name,
            string text, System.EventHandler onClick)
        {
            btn.Name      = name;
            btn.Text      = text;
            btn.Size      = new System.Drawing.Size(140, 30);
            btn.Margin    = new System.Windows.Forms.Padding(3, 1, 3, 1);
            btn.Font      = CaseManagement.Helpers.UiTheme.FontBold(CaseManagement.Helpers.UiTheme.SizeSmall);
            btn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.BackColor = CaseManagement.Helpers.UiTheme.Primary;
            btn.ForeColor = System.Drawing.Color.White;
            btn.UseVisualStyleBackColor = false;
            btn.Cursor    = System.Windows.Forms.Cursors.Hand;
            btn.TabStop   = false;
            btn.Click    += onClick;
        }

        // همان الگوی FieldsScrollPanel در FrmCase.Designer.cs — معاف‌کردنِ پنل
        // از ارثِ WS_EX_LAYOUTRTL تا اسکرول‌بار سمتِ راستِ واقعی بماند.
        private class FieldsScrollPanel : System.Windows.Forms.Panel
        {
            protected override System.Windows.Forms.CreateParams CreateParams
            {
                get
                {
                    const int WS_EX_NOINHERITLAYOUT = 0x00100000;
                    System.Windows.Forms.CreateParams cp = base.CreateParams;
                    cp.ExStyle |= WS_EX_NOINHERITLAYOUT;
                    return cp;
                }
            }
        }

        private System.Windows.Forms.TextBox txtDocType;
        private System.Windows.Forms.TextBox txtOriginalFileName;
        private System.Windows.Forms.TextBox txtDocFilePath;
        private System.Windows.Forms.TextBox txtRelatedCaseRef;
        private System.Windows.Forms.TextBox txtDocDescription;
        private System.Windows.Forms.Button btnBrowseDocFile;
        private System.Windows.Forms.Button btnEdit;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.Button btnNew;
        private System.Windows.Forms.Button btnDelete;
        private System.Windows.Forms.DataGridView dgvDocs;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Button btnOpenDoc;
        private System.Windows.Forms.PictureBox picPreview;
        private System.Windows.Forms.Button btnPrint;
        private System.Windows.Forms.TextBox txtDocCategory;
        private System.Windows.Forms.TextBox txtDocTags;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.TextBox txtSearch;
        private System.Windows.Forms.TextBox txtDocNo;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label lblHeadInfo;
        private System.Windows.Forms.Label lblDocsHeader;
        private System.Windows.Forms.Button btnPrevCase;
        private System.Windows.Forms.Button btnNextCase;
        private System.Windows.Forms.FlowLayoutPanel panCaseNav;
        private System.Windows.Forms.Panel headBarPanel;
    }
}
