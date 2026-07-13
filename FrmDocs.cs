using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using CaseManagement.DAL;
using CaseManagement.Helpers;
using static CaseManagement.Helpers.SqlHelpers;

namespace CaseManagement
{
    public partial class FrmDocs : Form
    {
        private const string DocsSectionName = "Docs";

        private static readonly HashSet<string> ImagePreviewExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff" };

        private const int DocTypeLength = 100;
        private const int OriginalFileNameLength = 255;
        private const int DocFilePathLength = 500;
        private const int RelatedCaseRefLength = 100;

        private readonly DatabaseHelper db = new DatabaseHelper();

        public int CurrentCaseId { get; set; } = 0;
        public string CurrentCaseCode { get; set; } = "";

        private int currentDocId = 0;
        private string storedDocFilePath = "";
        private string pendingSourceFilePath = "";
        private string pendingOriginalFileName = "";

        public FrmDocs()
        {
            InitializeComponent();
            ApplyCustomTheme();
        }

        // ─── اعمال ظاهر یکسان روی فرمی که با طراح (Designer) ساخته شده ──────
        private void ApplyCustomTheme()
        {
            UiTheme.ApplySweep(this);

            UiTheme.SetButtonIcon(btnNew, "+");
            UiTheme.SetButtonIcon(btnSave, "✔");
            UiTheme.SetButtonIcon(btnEdit, "✎");
            UiTheme.SetButtonIcon(btnDelete, "✕");
            UiTheme.SetButtonIcon(btnBrowseDocFile, "▤");
            UiTheme.SetButtonIcon(btnOpenDoc, "➤");

            btnDelete.BackColor = UiTheme.Danger;
            btnDelete.FlatAppearance.MouseOverBackColor = ControlPaint.Light(UiTheme.Danger, 0.18f);
            btnDelete.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(UiTheme.Danger, 0.08f);

            btnSave.BackColor = UiTheme.Success;
            btnSave.FlatAppearance.MouseOverBackColor = ControlPaint.Light(UiTheme.Success, 0.18f);
            btnSave.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(UiTheme.Success, 0.08f);
        }

        private void FrmDocs_Load(object sender, EventArgs e)
        {
            Text = "اسناد" +
                   (string.IsNullOrEmpty(CurrentCaseCode) ? "" : "  —  پرونده: " + CurrentCaseCode) +
                   "  [" + SecurityContext.CenterDisplay + "]";
            txtOriginalFileName.ReadOnly = true;
            txtDocFilePath.ReadOnly = true;

            ConfigureGrid();
            LoadDocs();
            ClearForm();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            UpdatePreview("");
            base.OnFormClosed(e);
        }

        private void ConfigureGrid()
        {
            dgvDocs.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvDocs.MultiSelect = false;
            dgvDocs.ReadOnly = true;
            dgvDocs.AllowUserToAddRows = false;
            dgvDocs.AllowUserToDeleteRows = false;
            dgvDocs.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        }

        private void ClearForm()
        {
            currentDocId = 0;
            storedDocFilePath = "";
            pendingSourceFilePath = "";
            pendingOriginalFileName = "";

            txtDocType.Text = "";
            txtOriginalFileName.Text = "";
            txtDocFilePath.Text = "";
            txtRelatedCaseRef.Text = "";
            txtDocDescription.Text = "";

            UpdatePreview("");

            txtDocType.Focus();
        }

        // پیش‌نمایش بزرگ سند: اگر عکس باشد نمایش داده می‌شود، در غیر اینصورت خالی می‌ماند
        private void UpdatePreview(string filePath)
        {
            if (picPreview.Image != null)
            {
                var old = picPreview.Image;
                picPreview.Image = null;
                old.Dispose();
            }

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return;

            string ext = Path.GetExtension(filePath);
            if (!ImagePreviewExtensions.Contains(ext))
                return;

            try
            {
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (Image img = Image.FromStream(fs, false, true))
                    picPreview.Image = new Bitmap(img);
            }
            catch
            {
                // پیش‌نمایش غیرحیاتی است؛ خطا نادیده گرفته می‌شود
            }
        }

        private bool ValidateForm(bool isNewRecord)
        {
            if (CurrentCaseId <= 0)
            {
                Msg.Show("ابتدا پرونده اصلی را ذخیره یا انتخاب کن");
                return false;
            }

            if (string.IsNullOrWhiteSpace(CurrentCaseCode))
            {
                Msg.Show("کد پرونده اصلی مشخص نیست");
                return false;
            }

            if (string.IsNullOrWhiteSpace(txtDocType.Text))
            {
                Msg.Show("نوع سند را وارد کنید");
                txtDocType.Focus();
                return false;
            }

            if (txtDocType.Text.Trim().Length > DocTypeLength)
            {
                Msg.Show("نوع سند نباید بیشتر از 100 کاراکتر باشد");
                txtDocType.Focus();
                return false;
            }

            if (txtRelatedCaseRef.Text.Trim().Length > RelatedCaseRefLength)
            {
                Msg.Show("مرجع مرتبط نباید بیشتر از 100 کاراکتر باشد");
                txtRelatedCaseRef.Focus();
                return false;
            }

            if (isNewRecord && string.IsNullOrWhiteSpace(pendingSourceFilePath))
            {
                Msg.Show("فایل سند را انتخاب کنید");
                return false;
            }

            if (!isNewRecord &&
                string.IsNullOrWhiteSpace(pendingSourceFilePath) &&
                string.IsNullOrWhiteSpace(storedDocFilePath))
            {
                Msg.Show("فایل سند را انتخاب کنید");
                return false;
            }

            return true;
        }

        private void LoadDocs()
        {
            if (CurrentCaseId <= 0)
            {
                dgvDocs.DataSource = null;
                return;
            }

            try
            {
                using (var con = db.GetConnection())
                using (var cmd = new SQLiteCommand(@"
                    SELECT d.DocID, d.DocType, d.OriginalFileName, d.RelatedCaseRef,
                           c.Code, c.HeadFullName
                    FROM TblDocs d
                    JOIN TblCase c ON c.CasID = d.CasID
                    WHERE d.CasID = @CasID
                    ORDER BY d.DocID DESC", con))
                {
                    AddInt(cmd, "@CasID", CurrentCaseId);

                    con.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        DataTable dt = new DataTable();
                        dt.Load(reader);
                        dgvDocs.DataSource = dt;
                    }
                }

                ApplyGridHeaders();
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در بارگذاری اسناد: " + ex.Message);
            }
        }

        // آموزش — به درخواست کاربر: گرید اسناد (سمت چپ فرم) فقط سه ستون «نوع
        // سند»، «کد اختصاصی پرونده» و «نام سرپرست خانواده» را نشان می‌دهد؛
        // بقیه ستون‌ها (شناسه/نام فایل اصلی/مرجع مرتبط) پنهان می‌مانند نه حذف
        // — چون DocID برای dgvDocs_CellClick و بقیه منطق هنوز لازم است.
        private void ApplyGridHeaders()
        {
            if (dgvDocs.Columns.Count == 0)
                return;

            if (dgvDocs.Columns.Contains("DocType"))
                dgvDocs.Columns["DocType"].HeaderText = "نوع سند";

            if (dgvDocs.Columns.Contains("Code"))
                dgvDocs.Columns["Code"].HeaderText = "کد اختصاصی پرونده";

            if (dgvDocs.Columns.Contains("HeadFullName"))
                dgvDocs.Columns["HeadFullName"].HeaderText = "نام سرپرست خانواده";

            if (dgvDocs.Columns.Contains("DocID"))
                dgvDocs.Columns["DocID"].Visible = false;

            if (dgvDocs.Columns.Contains("OriginalFileName"))
                dgvDocs.Columns["OriginalFileName"].Visible = false;

            if (dgvDocs.Columns.Contains("RelatedCaseRef"))
                dgvDocs.Columns["RelatedCaseRef"].Visible = false;

            if (dgvDocs.Columns.Contains("DocType"))
                dgvDocs.Columns["DocType"].DisplayIndex = 0;
            if (dgvDocs.Columns.Contains("Code"))
                dgvDocs.Columns["Code"].DisplayIndex = 1;
            if (dgvDocs.Columns.Contains("HeadFullName"))
                dgvDocs.Columns["HeadFullName"].DisplayIndex = 2;

            dgvDocs.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        }

        private void btnBrowseDocFile_Click(object sender, EventArgs e)
        {
            if (CurrentCaseId <= 0 || string.IsNullOrWhiteSpace(CurrentCaseCode))
            {
                Msg.Show("اول پرونده اصلی را ذخیره یا انتخاب کن");
                return;
            }

            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "انتخاب فایل سند";
                ofd.CheckFileExists = true;
                ofd.Multiselect = false;
                ofd.Filter =
                    "اسناد مجاز|*.pdf;*.doc;*.docx;*.xls;*.xlsx;*.txt;*.rtf;*.jpg;*.jpeg;*.png;*.tif;*.tiff|" +
                    "فایل‌های PDF|*.pdf|" +
                    "فایل‌های تصویری|*.jpg;*.jpeg;*.png;*.tif;*.tiff";

                if (ofd.ShowDialog() != DialogResult.OK)
                    return;

                pendingSourceFilePath = ofd.FileName;
                pendingOriginalFileName = Path.GetFileName(ofd.FileName);

                txtOriginalFileName.Text = pendingOriginalFileName;
                txtDocFilePath.Text = pendingSourceFilePath;
                UpdatePreview(pendingSourceFilePath);
            }
        }

        private void btnNew_Click(object sender, EventArgs e)
        {
            ClearForm();
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            if (!SecurityContext.CanEdit())
            {
                Msg.Show("کاربر فقط مشاهده اجازه ثبت سند ندارد.");
                return;
            }

            if (!ValidateForm(true))
                return;

            string savedPath = "";
            bool copiedNewFile = false;

            try
            {
                savedPath = SavePendingFileToCaseFolder();
                if (string.IsNullOrWhiteSpace(savedPath))
                {
                    Msg.Show("فایل سند ذخیره نشد: " + FileHelper.LastError);
                    return;
                }

                copiedNewFile = !AreSamePath(savedPath, pendingSourceFilePath);

                using (var con = db.GetConnection())
                using (var cmd = new SQLiteCommand(@"
                    INSERT INTO TblDocs
                    (
                        CasID, DocType, OriginalFileName, DocFilePath, RelatedCaseRef, DocDescription, GlobalID
                    )
                    VALUES
                    (
                        @CasID, @DocType, @OriginalFileName, @DocFilePath, @RelatedCaseRef, @DocDescription,
                        lower(hex(randomblob(4))) || '-' || lower(hex(randomblob(2))) || '-' ||
                        lower(hex(randomblob(2))) || '-' || lower(hex(randomblob(2))) || '-' || lower(hex(randomblob(6)))
                    )", con))
                {
                    AddInt(cmd, "@CasID", CurrentCaseId);
                    AddNVarChar(cmd, "@DocType", txtDocType.Text.Trim(), DocTypeLength);
                    AddNVarChar(cmd, "@OriginalFileName", pendingOriginalFileName, OriginalFileNameLength);
                    AddNVarChar(cmd, "@DocFilePath", savedPath, DocFilePathLength);
                    AddNVarChar(cmd, "@RelatedCaseRef", txtRelatedCaseRef.Text.Trim(), RelatedCaseRefLength);
                    AddNVarCharMax(cmd, "@DocDescription", txtDocDescription.Text.Trim());
                    con.Open();
                    cmd.ExecuteNonQuery();
                    var idObj = new SQLiteCommand("SELECT last_insert_rowid()", con).ExecuteScalar();
                    currentDocId = Convert.ToInt32((long)idObj);
                }

                AuditLogger.Log("ثبت", "TblDocs", currentDocId, "", BuildDocAuditText(savedPath, pendingOriginalFileName));

                Msg.Show("سند ذخیره شد");
                LoadDocs();
                ClearForm();
            }
            catch (Exception ex)
            {
                if (copiedNewFile)
                    DeleteStoredFileSafely(savedPath);

                Msg.Show("خطا در ذخیره سند: " + ex.Message);
            }
        }

        private void btnEdit_Click(object sender, EventArgs e)
        {
            if (!SecurityContext.CanEdit())
            {
                Msg.Show("کاربر فقط مشاهده اجازه ویرایش سند ندارد.");
                return;
            }

            if (currentDocId <= 0)
            {
                Msg.Show("اول یک سند را انتخاب کن");
                return;
            }

            if (!ValidateForm(false))
                return;

            string oldPath = "";
            string finalPath = storedDocFilePath;
            string finalOriginalFileName = txtOriginalFileName.Text.Trim();
            string newlyCopiedPath = "";

            try
            {
                string oldAuditText = GetDocAuditText(currentDocId);
                oldPath = GetStoredDocPath(currentDocId);
                if (oldPath == null)
                {
                    Msg.Show("سند انتخاب‌شده پیدا نشد");
                    LoadDocs();
                    ClearForm();
                    return;
                }

                finalPath = oldPath;

                if (!string.IsNullOrWhiteSpace(pendingSourceFilePath))
                {
                    if (AreSamePath(pendingSourceFilePath, oldPath))
                    {
                        finalPath = oldPath;
                    }
                    else
                    {
                        newlyCopiedPath = SavePendingFileToCaseFolder();
                        if (string.IsNullOrWhiteSpace(newlyCopiedPath))
                        {
                            Msg.Show("فایل جدید سند ذخیره نشد: " + FileHelper.LastError);
                            return;
                        }

                        finalPath = newlyCopiedPath;
                    }

                    finalOriginalFileName = pendingOriginalFileName;
                }

                using (SQLiteConnection con = db.GetConnection())
                using (SQLiteCommand cmd = new SQLiteCommand(@"
                    UPDATE TblDocs SET
                        DocType = @DocType,
                        OriginalFileName = @OriginalFileName,
                        DocFilePath = @DocFilePath,
                        RelatedCaseRef = @RelatedCaseRef,
                        DocDescription = @DocDescription
                    WHERE DocID = @DocID AND CasID = @CasID", con))
                {
                    AddNVarChar(cmd, "@DocType", txtDocType.Text.Trim(), DocTypeLength);
                    AddNVarChar(cmd, "@OriginalFileName", finalOriginalFileName, OriginalFileNameLength);
                    AddNVarChar(cmd, "@DocFilePath", finalPath, DocFilePathLength);
                    AddNVarChar(cmd, "@RelatedCaseRef", txtRelatedCaseRef.Text.Trim(), RelatedCaseRefLength);
                    AddNVarCharMax(cmd, "@DocDescription", txtDocDescription.Text.Trim());
                    AddInt(cmd, "@DocID", currentDocId);
                    AddInt(cmd, "@CasID", CurrentCaseId);

                    con.Open();

                    int affectedRows = cmd.ExecuteNonQuery();
                    if (affectedRows == 0)
                        throw new InvalidOperationException("هیچ سندی برای ویرایش پیدا نشد.");
                }

                if (!string.IsNullOrWhiteSpace(newlyCopiedPath) && !AreSamePath(oldPath, newlyCopiedPath))
                    DeleteStoredFileSafely(oldPath);

                AuditLogger.Log("ویرایش", "TblDocs", currentDocId, oldAuditText, BuildDocAuditText(finalPath, finalOriginalFileName));

                Msg.Show("سند ویرایش شد");
                LoadDocs();
                ClearForm();
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrWhiteSpace(newlyCopiedPath))
                    DeleteStoredFileSafely(newlyCopiedPath);

                Msg.Show("خطا در ویرایش سند: " + ex.Message);
            }
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            if (!SecurityContext.CanDelete())
            {
                Msg.Show("حذف سند فقط برای مدیر سیستم مجاز است.");
                return;
            }

            if (currentDocId <= 0)
            {
                Msg.Show("اول یک سند را انتخاب کن");
                return;
            }

            DialogResult dr = Msg.Show(
                "آیا این سند حذف شود؟",
                "حذف",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (dr == DialogResult.No)
                return;

            string filePathToDelete = "";
            string oldAuditText = GetDocAuditText(currentDocId);
            int deletedDocId = currentDocId;

            try
            {
                using (SQLiteConnection con = db.GetConnection())
                {
                    con.Open();

                    using (SQLiteTransaction tr = con.BeginTransaction())
                    {
                        using (SQLiteCommand selectCmd = new SQLiteCommand(@"
                            SELECT DocFilePath
                            FROM TblDocs
                            WHERE DocID = @DocID AND CasID = @CasID", con, tr))
                        {
                            AddInt(selectCmd, "@DocID", currentDocId);
                            AddInt(selectCmd, "@CasID", CurrentCaseId);

                            object value = selectCmd.ExecuteScalar();
                            if (value == null || value == DBNull.Value)
                            {
                                tr.Rollback();
                                Msg.Show("سند انتخاب‌شده پیدا نشد");
                                LoadDocs();
                                ClearForm();
                                return;
                            }

                            filePathToDelete = value.ToString();
                        }

                        using (SQLiteCommand deleteCmd = new SQLiteCommand(@"
                            DELETE FROM TblDocs
                            WHERE DocID = @DocID AND CasID = @CasID", con, tr))
                        {
                            AddInt(deleteCmd, "@DocID", currentDocId);
                            AddInt(deleteCmd, "@CasID", CurrentCaseId);

                            int affectedRows = deleteCmd.ExecuteNonQuery();
                            if (affectedRows == 0)
                            {
                                tr.Rollback();
                                Msg.Show("سند حذف نشد");
                                return;
                            }
                        }

                        tr.Commit();
                    }
                }

                DeleteStoredFileSafely(filePathToDelete);

                AuditLogger.Log("حذف", "TblDocs", deletedDocId, oldAuditText, "");

                Msg.Show("سند حذف شد");
                LoadDocs();
                ClearForm();
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در حذف سند: " + ex.Message);
            }
        }

        private void btnPrint_Click(object sender, EventArgs e)
        {
            DataTable table = dgvDocs.DataSource as DataTable;
            if (table == null || table.Rows.Count == 0)
            {
                Msg.Show("داده‌ای برای چاپ وجود ندارد");
                return;
            }

            PrintHelper.PrintDataTable(this, "اسناد — پرونده " + CurrentCaseCode, table);
        }

        private void dgvDocs_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
                return;

            if (!dgvDocs.Columns.Contains("DocID"))
                return;

            object idValue = dgvDocs.Rows[e.RowIndex].Cells["DocID"].Value;
            if (idValue == null || idValue == DBNull.Value)
                return;

            int docId = Convert.ToInt32(idValue);
            LoadDocToForm(docId);
        }

        private void dgvDocs_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            dgvDocs_CellClick(sender, e);
        }

        private void LoadDocToForm(int docId)
        {
            try
            {
                using (SQLiteConnection con = db.GetConnection())
                using (SQLiteCommand cmd = new SQLiteCommand(@"
                    SELECT DocID, DocType, OriginalFileName, DocFilePath, RelatedCaseRef, DocDescription
                    FROM TblDocs
                    WHERE DocID = @DocID AND CasID = @CasID", con))
                {
                    AddInt(cmd, "@DocID", docId);
                    AddInt(cmd, "@CasID", CurrentCaseId);

                    con.Open();

                    using (var dr = cmd.ExecuteReader())
                    {
                        if (!dr.Read())
                            return;

                        currentDocId = Convert.ToInt32(dr["DocID"]);
                        storedDocFilePath = DbString(dr["DocFilePath"]);

                        pendingSourceFilePath = "";
                        pendingOriginalFileName = "";

                        txtDocType.Text = DbString(dr["DocType"]);
                        txtOriginalFileName.Text = DbString(dr["OriginalFileName"]);
                        txtDocFilePath.Text = storedDocFilePath;
                        txtRelatedCaseRef.Text = DbString(dr["RelatedCaseRef"]);
                        txtDocDescription.Text = DbString(dr["DocDescription"]);
                        UpdatePreview(storedDocFilePath);
                    }
                }
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در انتخاب سند: " + ex.Message);
            }
        }

        private void btnOpenDoc_Click(object sender, EventArgs e)
        {
            string path = txtDocFilePath.Text.Trim();

            if (string.IsNullOrWhiteSpace(path))
            {
                Msg.Show("مسیر فایل خالی است");
                return;
            }

            if (!File.Exists(path))
            {
                Msg.Show("فایل پیدا نشد");
                return;
            }

            if (!IsAllowedToOpenPath(path))
            {
                Msg.Show("باز کردن این مسیر مجاز نیست");
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo()
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در باز کردن فایل: " + ex.Message);
            }
        }

        private string SavePendingFileToCaseFolder()
        {
            if (string.IsNullOrWhiteSpace(pendingSourceFilePath))
                return "";

            if (string.IsNullOrWhiteSpace(CurrentCaseCode))
            {
                Msg.Show("کد اختصاصی پرونده مشخص نیست");
                return "";
            }

            if (!File.Exists(pendingSourceFilePath))
            {
                Msg.Show("فایل انتخاب‌شده پیدا نشد");
                return "";
            }

            string cleanCode = FileHelper.CleanName(CurrentCaseCode.Trim());
            string cleanDocType = FileHelper.CleanName(txtDocType.Text.Trim());
            string baseFileName = cleanCode + "-" + cleanDocType;

            string savedPath = FileHelper.SaveFileToCaseFolder(
                pendingSourceFilePath,
                CurrentCaseCode,
                FileHelper.SectionDocs,
                baseFileName,
                storedDocFilePath);

            if (string.IsNullOrWhiteSpace(savedPath))
                return "";

            if (savedPath.Length > DocFilePathLength)
            {
                DeleteStoredFileSafely(savedPath);
                Msg.Show("مسیر فایل سند بیش از حد طولانی است");
                return "";
            }

            return savedPath;
        }

        private string GetStoredDocPath(int docId)
        {
            using (SQLiteConnection con = db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(@"
                SELECT DocFilePath
                FROM TblDocs
                WHERE DocID = @DocID AND CasID = @CasID", con))
            {
                AddInt(cmd, "@DocID", docId);
                AddInt(cmd, "@CasID", CurrentCaseId);

                con.Open();

                object result = cmd.ExecuteScalar();
                if (result == null || result == DBNull.Value)
                    return null;

                return result.ToString();
            }
        }

        private string GetDocAuditText(int docId)
        {
            using (SQLiteConnection con = db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(@"
SELECT DocType, OriginalFileName, DocFilePath, RelatedCaseRef
FROM TblDocs
WHERE DocID = @DocID AND CasID = @CasID", con))
            {
                AddInt(cmd, "@DocID", docId);
                AddInt(cmd, "@CasID", CurrentCaseId);
                con.Open();
                using (var dr = cmd.ExecuteReader())
                {
                    if (!dr.Read())
                        return "";

                    return
                        "DocType=" + DbString(dr["DocType"]) +
                        "; OriginalFileName=" + DbString(dr["OriginalFileName"]) +
                        "; DocFilePath=" + DbString(dr["DocFilePath"]) +
                        "; RelatedCaseRef=" + DbString(dr["RelatedCaseRef"]);
                }
            }
        }

        private string BuildDocAuditText(string filePath, string originalFileName)
        {
            return
                "DocType=" + txtDocType.Text.Trim() +
                "; OriginalFileName=" + (originalFileName ?? "") +
                "; DocFilePath=" + (filePath ?? "") +
                "; RelatedCaseRef=" + txtRelatedCaseRef.Text.Trim();
        }

        private bool IsAllowedToOpenPath(string path)
        {
            if (!string.IsNullOrWhiteSpace(pendingSourceFilePath) && AreSamePath(path, pendingSourceFilePath))
                return true;

            return IsStoredFilePathAllowed(path);
        }

        private bool IsStoredFilePathAllowed(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            string docsFolder = FileHelper.GetSectionFolder(CurrentCaseCode, DocsSectionName);
            if (string.IsNullOrWhiteSpace(docsFolder))
                return false;

            return IsPathInsideFolder(path, docsFolder);
        }

        private void DeleteStoredFileSafely(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            if (!IsStoredFilePathAllowed(path))
                return;

            FileHelper.DeleteFileIfExists(path);
        }

    }
}
