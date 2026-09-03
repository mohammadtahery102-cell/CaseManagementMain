using System;
using System.Data;
using System.Data.SQLite;
using System.Windows.Forms;
using CaseManagement.DAL;
using CaseManagement.Helpers;
using static CaseManagement.Helpers.SqlHelpers;

namespace CaseManagement
{
    // ═════════════════════════════════════════════════════════════════════════
    // «پرونده‌های مرتبط» — مثلِ FrmDocs/FrmFamily از FrmCase باز می‌شود.
    // پرونده‌های دیگر را با نوع رابطه (مثلاً خواهر/برادر، خویشاوند) به این
    // پرونده لینک می‌کند. تاریخچه از طریق AuditLogger (همان مکانیزم موجود
    // در بقیه‌ی فرم‌ها) ثبت می‌شود، نه یک جدول تاریخچه‌ی جداگانه.
    // ═════════════════════════════════════════════════════════════════════════
    public partial class FrmCaseRelations : Form
    {
        private readonly DatabaseHelper db = new DatabaseHelper();

        public int CurrentCaseId { get; set; } = 0;
        public string CurrentCaseCode { get; set; } = "";

        private DataGridView dgvRelations;
        private TextBox txtRelatedCode;
        private TextBox txtRelationType;
        private Button btnAdd, btnDelete, btnClose;

        public FrmCaseRelations()
        {
            BuildUi();
        }

        private void BuildUi()
        {
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor = UiTheme.Background;
            Font = UiTheme.Font(UiTheme.SizeBody);
            UiTheme.MakeMainWindow(this, 820, 560);

            dgvRelations = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowTemplate = { Height = 28 }
            };
            UiTheme.StyleGrid(dgvRelations);

            Panel gridWrap = new Panel { Dock = DockStyle.Fill, Padding = new System.Windows.Forms.Padding(12, 6, 12, 6) };
            gridWrap.Controls.Add(dgvRelations);
            Controls.Add(gridWrap);

            Controls.Add(BuildToolbar());

            Panel header = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = UiTheme.PrimaryDark };
            var title = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = System.Drawing.Color.White,
                Font = UiTheme.FontBold(UiTheme.SizeLarge),
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Padding = new System.Windows.Forms.Padding(0, 0, 20, 0),
                Text = "پرونده‌های مرتبط"
            };
            header.Controls.Add(title);
            Controls.Add(header);

            Load += FrmCaseRelations_Load;
        }

        private Control BuildToolbar()
        {
            var bar = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = UiTheme.CardBack };

            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false, Padding = new System.Windows.Forms.Padding(12, 10, 12, 10)
            };

            flow.Controls.Add(new Label
            {
                Text = "کد پرونده مرتبط:", AutoSize = true,
                Font = UiTheme.Font(UiTheme.SizeSmall), ForeColor = UiTheme.TextMuted,
                Margin = new System.Windows.Forms.Padding(0, 8, 6, 0)
            });

            txtRelatedCode = new TextBox { Width = 140, Font = UiTheme.Font(UiTheme.SizeBody), Margin = new System.Windows.Forms.Padding(0, 2, 10, 0) };
            flow.Controls.Add(txtRelatedCode);

            flow.Controls.Add(new Label
            {
                Text = "نوع رابطه:", AutoSize = true,
                Font = UiTheme.Font(UiTheme.SizeSmall), ForeColor = UiTheme.TextMuted,
                Margin = new System.Windows.Forms.Padding(0, 8, 6, 0)
            });

            txtRelationType = new TextBox { Width = 180, Font = UiTheme.Font(UiTheme.SizeBody), Margin = new System.Windows.Forms.Padding(0, 2, 10, 0) };
            flow.Controls.Add(txtRelationType);

            btnAdd = UiTheme.CreateButton("افزودن", "+", UiTheme.Primary);
            btnAdd.Size = new System.Drawing.Size(110, 34); btnAdd.Margin = new System.Windows.Forms.Padding(0, 0, 6, 0);
            btnAdd.Click += btnAdd_Click;
            flow.Controls.Add(btnAdd);

            btnDelete = UiTheme.CreateSecondaryButton("حذف رابطه", "✕");
            btnDelete.Size = new System.Drawing.Size(120, 34); btnDelete.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            btnDelete.Click += btnDelete_Click;
            flow.Controls.Add(btnDelete);

            btnClose = UiTheme.CreateSecondaryButton("بستن", "");
            btnClose.Size = new System.Drawing.Size(90, 34); btnClose.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            btnClose.Click += delegate { Close(); };
            flow.Controls.Add(btnClose);

            bar.Controls.Add(flow);
            return bar;
        }

        private void FrmCaseRelations_Load(object sender, EventArgs e)
        {
            Text = "پرونده‌های مرتبط" +
                   (string.IsNullOrEmpty(CurrentCaseCode) ? "" : "  —  پرونده: " + CurrentCaseCode);

            LoadRelations();
        }

        private void LoadRelations()
        {
            if (CurrentCaseId <= 0)
            {
                dgvRelations.DataSource = null;
                return;
            }

            try
            {
                using (SQLiteConnection con = db.GetConnection())
                using (SQLiteCommand cmd = new SQLiteCommand(@"
                    SELECT r.RelationID, c.Code AS ""کد پرونده مرتبط"", c.HeadFullName AS ""نام سرپرست"",
                           r.RelationType AS ""نوع رابطه"", r.CreatedAt AS ""تاریخ ثبت"", r.CreatedBy AS ""ثبت‌شده توسط""
                    FROM TblCaseRelation r
                    JOIN TblCase c ON c.CasID = r.RelatedCasID
                    WHERE r.CasID = @CasID
                    ORDER BY r.RelationID DESC", con))
                {
                    AddInt(cmd, "@CasID", CurrentCaseId);
                    con.Open();

                    using (var reader = cmd.ExecuteReader())
                    {
                        DataTable dt = new DataTable();
                        dt.Load(reader);
                        dgvRelations.DataSource = dt;
                    }
                }

                if (dgvRelations.Columns.Contains("RelationID"))
                    dgvRelations.Columns["RelationID"].Visible = false;
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در بارگذاری پرونده‌های مرتبط: " + ex.Message);
            }
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            if (!CaseManagement.Enterprise.PermissionService.Require("CaseRelation.Edit"))
            {
                Msg.Show("کاربر فقط مشاهده اجازه افزودن رابطه ندارد.");
                return;
            }

            if (CurrentCaseId <= 0)
            {
                Msg.Show("پرونده اصلی مشخص نیست");
                return;
            }

            string relatedCode = txtRelatedCode.Text.Trim();
            if (relatedCode == "")
            {
                Msg.Show("کد پرونده مرتبط را وارد کنید");
                txtRelatedCode.Focus();
                return;
            }

            try
            {
                int relatedCasId;
                using (SQLiteConnection con = db.GetConnection())
                using (SQLiteCommand cmd = new SQLiteCommand(
                    "SELECT CasID FROM TblCase WHERE Code = @Code AND (@CID = 0 OR CenterID = @CID) LIMIT 1", con))
                {
                    AddNVarChar(cmd, "@Code", relatedCode, 100);
                    AddInt(cmd, "@CID", SecurityContext.CenterFilterId);
                    con.Open();

                    object result = cmd.ExecuteScalar();
                    if (result == null || result == DBNull.Value)
                    {
                        Msg.Show("پرونده‌ای با این کد پیدا نشد");
                        return;
                    }

                    relatedCasId = Convert.ToInt32(result);
                }

                if (relatedCasId == CurrentCaseId)
                {
                    Msg.Show("پرونده نمی‌تواند با خودش مرتبط شود");
                    return;
                }

                using (SQLiteConnection con = db.GetConnection())
                using (SQLiteCommand checkCmd = new SQLiteCommand(
                    "SELECT COUNT(1) FROM TblCaseRelation WHERE CasID = @CasID AND RelatedCasID = @RelatedCasID", con))
                {
                    AddInt(checkCmd, "@CasID", CurrentCaseId);
                    AddInt(checkCmd, "@RelatedCasID", relatedCasId);
                    con.Open();

                    if (Convert.ToInt32(checkCmd.ExecuteScalar()) > 0)
                    {
                        Msg.Show("این ارتباط قبلاً ثبت شده است");
                        return;
                    }
                }

                int newRelationId;
                using (SQLiteConnection con = db.GetConnection())
                using (SQLiteCommand insertCmd = new SQLiteCommand(@"
                    INSERT INTO TblCaseRelation (CasID, RelatedCasID, RelationType, CreatedBy)
                    VALUES (@CasID, @RelatedCasID, @RelationType, @CreatedBy)", con))
                {
                    AddInt(insertCmd, "@CasID", CurrentCaseId);
                    AddInt(insertCmd, "@RelatedCasID", relatedCasId);
                    AddNVarChar(insertCmd, "@RelationType", txtRelationType.Text.Trim(), 100);
                    AddNVarChar(insertCmd, "@CreatedBy", SecurityContext.Username, 100);
                    con.Open();
                    insertCmd.ExecuteNonQuery();

                    using (var idCmd = new SQLiteCommand("SELECT last_insert_rowid()", con))
                        newRelationId = Convert.ToInt32((long)idCmd.ExecuteScalar());
                }

                AuditLogger.Log("ثبت", "TblCaseRelation", newRelationId, "",
                    "CasID=" + CurrentCaseId + "; RelatedCasID=" + relatedCasId + "; RelationType=" + txtRelationType.Text.Trim());

                txtRelatedCode.Text = "";
                txtRelationType.Text = "";
                LoadRelations();
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در افزودن رابطه: " + ex.Message);
            }
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            if (!CaseManagement.Enterprise.PermissionService.Require("CaseRelation.Delete"))
            {
                Msg.Show("حذف رابطه فقط برای مدیر سیستم مجاز است.");
                return;
            }

            if (dgvRelations.CurrentRow == null)
            {
                Msg.Show("اول یک رابطه را انتخاب کن");
                return;
            }

            object idValue = dgvRelations.CurrentRow.Cells["RelationID"].Value;
            if (idValue == null || idValue == DBNull.Value)
                return;

            int relationId = Convert.ToInt32(idValue);

            DialogResult dr = Msg.Show(
                "آیا این رابطه حذف شود؟", "حذف",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (dr == DialogResult.No)
                return;

            try
            {
                using (SQLiteConnection con = db.GetConnection())
                using (SQLiteCommand cmd = new SQLiteCommand(
                    "DELETE FROM TblCaseRelation WHERE RelationID = @RelationID AND CasID = @CasID", con))
                {
                    AddInt(cmd, "@RelationID", relationId);
                    AddInt(cmd, "@CasID", CurrentCaseId);
                    con.Open();
                    cmd.ExecuteNonQuery();
                }

                AuditLogger.Log("حذف", "TblCaseRelation", relationId, "", "");
                LoadRelations();
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در حذف رابطه: " + ex.Message);
            }
        }
    }
}
