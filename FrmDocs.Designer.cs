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
        /// چیدمان با مختصات مطلق (Location/Size صریح روی هر کنترل) — قابل
        /// ویرایش با موس در Visual Studio Designer. نام کنترل‌ها و رویدادها
        /// دست‌نخورده مانده‌اند تا منطق موجود در FrmDocs.cs کار کند.
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
            ((System.ComponentModel.ISupportInitialize)(this.dgvDocs)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.picPreview)).BeginInit();
            this.SuspendLayout();

            // ═══ ستون فیلدها (سمت راست در RTL) — مختصات مطلق ══════════════════
            this.label1.Text = "نوع سند";
            this.label1.AutoSize = false;
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.label1.Location = new System.Drawing.Point(210, 15);
            this.label1.Size = new System.Drawing.Size(135, 22);
            this.txtDocType.Name = "txtDocType";
            this.txtDocType.Location = new System.Drawing.Point(15, 13);
            this.txtDocType.Size = new System.Drawing.Size(185, 26);
            this.txtDocType.TabIndex = 0;

            // نام فایل و مسیر فایل داخلی هستند و در UI نمایش داده نمی‌شوند
            // (کاربر فقط سند را انتخاب می‌کند؛ داده در دیتابیس ذخیره می‌ماند).
            this.label2.Text = "نام فایل";
            this.label2.AutoSize = false;
            this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.label2.Location = new System.Drawing.Point(210, 400);
            this.label2.Size = new System.Drawing.Size(135, 22);
            this.label2.Visible = false;
            this.txtOriginalFileName.Name = "txtOriginalFileName";
            this.txtOriginalFileName.Location = new System.Drawing.Point(15, 398);
            this.txtOriginalFileName.Size = new System.Drawing.Size(185, 26);
            this.txtOriginalFileName.TabIndex = 1;
            this.txtOriginalFileName.Visible = false;

            System.Windows.Forms.Label lblFile = new System.Windows.Forms.Label();
            lblFile.Text = "مسیر فایل";
            lblFile.AutoSize = false;
            lblFile.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            lblFile.Location = new System.Drawing.Point(210, 430);
            lblFile.Size = new System.Drawing.Size(135, 22);
            lblFile.Visible = false;
            this.txtDocFilePath.Name = "txtDocFilePath";
            this.txtDocFilePath.Location = new System.Drawing.Point(15, 428);
            this.txtDocFilePath.Size = new System.Drawing.Size(185, 26);
            this.txtDocFilePath.TabIndex = 2;
            this.txtDocFilePath.Visible = false;

            this.label3.Text = "شماره پرونده مرتبط";
            this.label3.AutoSize = false;
            this.label3.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.label3.Location = new System.Drawing.Point(210, 55);
            this.label3.Size = new System.Drawing.Size(135, 22);
            this.txtRelatedCaseRef.Name = "txtRelatedCaseRef";
            this.txtRelatedCaseRef.Location = new System.Drawing.Point(15, 53);
            this.txtRelatedCaseRef.Size = new System.Drawing.Size(185, 26);
            this.txtRelatedCaseRef.TabIndex = 3;

            this.label4.Text = "توضیح سند";
            this.label4.AutoSize = false;
            this.label4.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.label4.Location = new System.Drawing.Point(15, 95);
            this.label4.Size = new System.Drawing.Size(330, 22);
            this.txtDocDescription.Name = "txtDocDescription";
            this.txtDocDescription.Multiline = true;
            this.txtDocDescription.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtDocDescription.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.txtDocDescription.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.txtDocDescription.Location = new System.Drawing.Point(15, 120);
            this.txtDocDescription.Size = new System.Drawing.Size(330, 150);
            this.txtDocDescription.TabIndex = 4;

            this.label5.Text = "دسته‌بندی سند";
            this.label5.AutoSize = false;
            this.label5.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.label5.Location = new System.Drawing.Point(210, 335);
            this.label5.Size = new System.Drawing.Size(135, 22);
            this.txtDocCategory.Name = "txtDocCategory";
            this.txtDocCategory.Location = new System.Drawing.Point(15, 333);
            this.txtDocCategory.Size = new System.Drawing.Size(185, 26);
            this.txtDocCategory.TabIndex = 16;

            this.label6.Text = "برچسب‌ها (با ، جدا کنید)";
            this.label6.AutoSize = false;
            this.label6.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.label6.Location = new System.Drawing.Point(210, 375);
            this.label6.Size = new System.Drawing.Size(135, 22);
            this.txtDocTags.Name = "txtDocTags";
            this.txtDocTags.Location = new System.Drawing.Point(15, 373);
            this.txtDocTags.Size = new System.Drawing.Size(185, 26);
            this.txtDocTags.TabIndex = 17;

            this.label7.Text = "شماره سند (خودکار)";
            this.label7.AutoSize = false;
            this.label7.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.label7.Location = new System.Drawing.Point(210, 415);
            this.label7.Size = new System.Drawing.Size(135, 22);
            this.txtDocNo.Name = "txtDocNo";
            this.txtDocNo.Location = new System.Drawing.Point(15, 413);
            this.txtDocNo.Size = new System.Drawing.Size(185, 26);
            this.txtDocNo.TabIndex = 18;
            this.txtDocNo.ReadOnly = true;
            this.txtDocNo.TabStop = false;

            // دکمه‌های انتخاب/باز کردن فایل کنار هم (مختصات مطلق)
            this.btnBrowseDocFile.Name = "btnBrowseDocFile";
            this.btnBrowseDocFile.Text = "انتخاب فایل سند";
            this.btnBrowseDocFile.Location = new System.Drawing.Point(155, 285);
            this.btnBrowseDocFile.Size = new System.Drawing.Size(150, 36);
            this.btnBrowseDocFile.TabIndex = 5;
            this.btnBrowseDocFile.Click += new System.EventHandler(this.btnBrowseDocFile_Click);
            this.btnOpenDoc.Name = "btnOpenDoc";
            this.btnOpenDoc.Text = "بازکردن فایل";
            this.btnOpenDoc.Location = new System.Drawing.Point(15, 285);
            this.btnOpenDoc.Size = new System.Drawing.Size(130, 36);
            this.btnOpenDoc.TabIndex = 15;
            this.btnOpenDoc.Click += new System.EventHandler(this.btnOpenDoc_Click);

            System.Windows.Forms.Panel formPanel = new System.Windows.Forms.Panel();
            formPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            formPanel.Controls.Add(this.label1);
            formPanel.Controls.Add(this.txtDocType);
            formPanel.Controls.Add(this.label2);
            formPanel.Controls.Add(this.txtOriginalFileName);
            formPanel.Controls.Add(lblFile);
            formPanel.Controls.Add(this.txtDocFilePath);
            formPanel.Controls.Add(this.label3);
            formPanel.Controls.Add(this.txtRelatedCaseRef);
            formPanel.Controls.Add(this.label4);
            formPanel.Controls.Add(this.txtDocDescription);
            formPanel.Controls.Add(this.btnBrowseDocFile);
            formPanel.Controls.Add(this.btnOpenDoc);
            formPanel.Controls.Add(this.label5);
            formPanel.Controls.Add(this.txtDocCategory);
            formPanel.Controls.Add(this.label6);
            formPanel.Controls.Add(this.txtDocTags);
            formPanel.Controls.Add(this.label7);
            formPanel.Controls.Add(this.txtDocNo);

            //
            // picPreview — پیش‌نمایش بزرگ تصویر سند
            //
            this.picPreview.Name = "picPreview";
            this.picPreview.Dock = System.Windows.Forms.DockStyle.Fill;
            this.picPreview.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.picPreview.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.picPreview.BackColor = System.Drawing.Color.White;
            this.picPreview.TabStop = false;

            System.Windows.Forms.Label lblPreviewTitle = new System.Windows.Forms.Label();
            lblPreviewTitle.Text = "پیش‌نمایش سند";
            lblPreviewTitle.Dock = System.Windows.Forms.DockStyle.Top;
            lblPreviewTitle.Height = 24;
            lblPreviewTitle.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            lblPreviewTitle.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold);

            System.Windows.Forms.Panel previewPanel = new System.Windows.Forms.Panel();
            previewPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            previewPanel.Padding = new System.Windows.Forms.Padding(6);
            previewPanel.Controls.Add(this.picPreview);
            previewPanel.Controls.Add(lblPreviewTitle);

            //
            // dgvDocs — مرکز، پرکننده
            //
            this.dgvDocs.Name = "dgvDocs";
            this.dgvDocs.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvDocs.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvDocs.RowHeadersWidth = 51;
            this.dgvDocs.RowTemplate.Height = 24;
            this.dgvDocs.TabIndex = 10;
            this.dgvDocs.CellClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dgvDocs_CellClick);
            this.dgvDocs.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dgvDocs_CellContentClick);

            System.Windows.Forms.Label lblSearch = new System.Windows.Forms.Label();
            lblSearch.Text = "جستجو";
            lblSearch.Dock = System.Windows.Forms.DockStyle.Right;
            lblSearch.Width = 55;
            lblSearch.TextAlign = System.Drawing.ContentAlignment.MiddleRight;

            this.txtSearch.Name = "txtSearch";
            this.txtSearch.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtSearch.TextChanged += new System.EventHandler(this.txtSearch_TextChanged);

            System.Windows.Forms.Panel searchPanel = new System.Windows.Forms.Panel();
            searchPanel.Dock = System.Windows.Forms.DockStyle.Top;
            searchPanel.Height = 32;
            searchPanel.Padding = new System.Windows.Forms.Padding(0, 0, 0, 6);
            searchPanel.Controls.Add(this.txtSearch);
            searchPanel.Controls.Add(lblSearch);

            System.Windows.Forms.Panel gridPanel = new System.Windows.Forms.Panel();
            gridPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            gridPanel.Padding = new System.Windows.Forms.Padding(6);
            gridPanel.Controls.Add(this.dgvDocs);
            gridPanel.Controls.Add(searchPanel);

            // نوار دکمه‌های اقدام (پایین)
            System.Windows.Forms.FlowLayoutPanel buttonBar = new System.Windows.Forms.FlowLayoutPanel();
            buttonBar.Dock = System.Windows.Forms.DockStyle.Fill;
            buttonBar.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
            buttonBar.Padding = new System.Windows.Forms.Padding(10, 10, 10, 10);

            this.btnNew.Name = "btnNew";
            this.btnNew.Text = "جدید";
            this.btnNew.Size = new System.Drawing.Size(110, 38);
            this.btnNew.TabIndex = 8;
            this.btnNew.Click += new System.EventHandler(this.btnNew_Click);
            this.btnSave.Name = "btnSave";
            this.btnSave.Text = "ذخیره";
            this.btnSave.Size = new System.Drawing.Size(110, 38);
            this.btnSave.TabIndex = 7;
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            this.btnEdit.Name = "btnEdit";
            this.btnEdit.Text = "ویرایش";
            this.btnEdit.Size = new System.Drawing.Size(110, 38);
            this.btnEdit.TabIndex = 6;
            this.btnEdit.Click += new System.EventHandler(this.btnEdit_Click);
            this.btnDelete.Name = "btnDelete";
            this.btnDelete.Text = "حذف";
            this.btnDelete.Size = new System.Drawing.Size(110, 38);
            this.btnDelete.TabIndex = 9;
            this.btnDelete.Click += new System.EventHandler(this.btnDelete_Click);
            this.btnPrint = new System.Windows.Forms.Button();
            this.btnPrint.Name = "btnPrint";
            this.btnPrint.Text = "چاپ فهرست اسناد";
            this.btnPrint.Size = new System.Drawing.Size(150, 38);
            this.btnPrint.Click += new System.EventHandler(this.btnPrint_Click);
            buttonBar.Controls.Add(this.btnNew);
            buttonBar.Controls.Add(this.btnSave);
            buttonBar.Controls.Add(this.btnEdit);
            buttonBar.Controls.Add(this.btnDelete);
            buttonBar.Controls.Add(this.btnPrint);

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
            this.ClientSize = new System.Drawing.Size(1240, 560);

            // آموزش — فاز A4 (تب اسناد پرونده داخل FrmCase): بلوکِ قفلِ اندازهٔ
            // پنجره (FormBorderStyle/MinimumSize/MaximumSize/...) که این‌جا
            // بود، به FrmDocs_Load منتقل شد و پشتِ شرطِ IsEmbedded گذاشته شد —
            // دقیقاً همان اصلاحی که برای FrmFamily در فاز ۱ انجام شد. علتش:
            // وقتی این فرم TopLevel=false و Dock=Fill داخل یک تب جاسازی شود،
            // MinimumSize/MaximumSize قفل‌شده (روی اندازهٔ طراحی) مانع از این
            // می‌شود که اندازه‌اش را با تب هماهنگ کند.

            // ریشه چیدمان: TableLayoutPanel که فضا را صریح تقسیم می‌کند تا
            // مستقل از ترتیب Dock، همیشه فرم/پیش‌نمایش/گرید/دکمه‌ها سرجای خود باشند.
            // ستون‌ها به‌ترتیب: فیلدها (راست) → پیش‌نمایش بزرگ سند (وسط/چپ) → گرید (چپ).
            System.Windows.Forms.TableLayoutPanel rootLayout = new System.Windows.Forms.TableLayoutPanel();
            rootLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            rootLayout.ColumnCount = 3;
            rootLayout.RowCount = 2;
            rootLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 360F));
            rootLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 300F));
            rootLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 64F));
            rootLayout.Controls.Add(formPanel, 0, 0);
            rootLayout.Controls.Add(previewPanel, 1, 0);
            rootLayout.Controls.Add(gridPanel, 2, 0);
            rootLayout.Controls.Add(buttonBar, 0, 1);
            rootLayout.SetColumnSpan(buttonBar, 3);

            this.Controls.Add(rootLayout);
            this.Name = "FrmDocs";
            this.Text = "اسناد";
            this.Load += new System.EventHandler(this.FrmDocs_Load);
            ((System.ComponentModel.ISupportInitialize)(this.dgvDocs)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.picPreview)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

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
    }
}
