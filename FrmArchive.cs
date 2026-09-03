using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.Windows.Forms;
using CaseManagement.DAL;
using CaseManagement.Helpers;
using static CaseManagement.Helpers.SqlHelpers;

namespace CaseManagement
{
    // ═════════════════════════════════════════════════════════════════════════
    // «بایگانی» — فهرست پرونده‌های بایگانی‌شده (IsArchived=1)، بازگردانی،
    // تاریخچه‌ی بایگانی، و حذفِ همیشگی (فقط SuperAdmin، فقط روی رکوردهای از
    // قبل بایگانی‌شده — همان قابلیتِ قبلیِ «حذف کامل» که به این صفحه منتقل شد).
    // ═════════════════════════════════════════════════════════════════════════
    public sealed class FrmArchive : Form
    {
        private readonly DatabaseHelper db = new DatabaseHelper();
        private readonly DataTable _table = new DataTable();

        private DataGridView _grid;
        private TextBox _txtSearch;
        private Label _lblSummary, _lblHistory;
        private Button _btnRefresh, _btnRestore, _btnPurge, _btnOpenCase;
        private ComboBox _cmbMode;

        // آموزش — گسترشِ همین صفحه برای اسناد بایگانی‌شده (FrmDocs.IsArchived)؛
        // پیش‌فرض همچنان «پرونده‌ها» است تا رفتار قبلی صفحه دست‌نخورده بماند.
        private bool IsDocMode { get { return _cmbMode != null && _cmbMode.SelectedIndex == 1; } }

        public FrmArchive()
        {
            BuildUi();
            Load += delegate { LoadArchived(); };

            // ⚠ «حذف همیشگی» عمداً میان‌بُر ندارد. تنها عملیاتِ برگشت‌ناپذیرِ
            // این صفحه است و یک کلیدِ اشتباه نباید بتواند شروعش کند؛ برای چنین
            // کاری، رفتنِ آگاهانه به سمتِ خودِ دکمه ارزشش را دارد.
            Helpers.FormShortcuts.For(this)
                .Refresh(_btnRefresh)
                .Bind(Keys.Control | Keys.O, "مشاهده پرونده", _btnOpenCase)
                .Bind(Keys.Control | Keys.R, "بازگردانی از بایگانی", _btnRestore);
        }

        private void BuildUi()
        {
            Text = "بایگانی پرونده‌ها";
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor = UiTheme.Background;
            Font = UiTheme.Font(UiTheme.SizeBody);
            UiTheme.MakeMainWindow(this, 1100, 680);

            _grid = new DataGridView
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
            UiTheme.StyleGrid(_grid);
            _grid.SelectionChanged += delegate { ShowHistory(); };

            Panel gridWrap = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 6, 12, 6) };
            gridWrap.Controls.Add(_grid);
            Controls.Add(gridWrap);

            Panel historyPanel = new Panel { Dock = DockStyle.Bottom, Height = 110, BackColor = UiTheme.CardBack, Padding = new Padding(14, 8, 14, 8) };
            _lblHistory = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font(FontFamily.GenericMonospace, 9F),
                ForeColor = UiTheme.TextDark,
                TextAlign = ContentAlignment.TopLeft,
                Text = "برای دیدنِ تاریخچه‌ی بایگانی، یک ردیف را انتخاب کنید."
            };
            historyPanel.Controls.Add(_lblHistory);
            Controls.Add(historyPanel);

            Controls.Add(BuildToolbar());

            Panel header = new Panel { Dock = DockStyle.Top, Height = 76, BackColor = UiTheme.PrimaryDark };
            var title = new Label
            {
                Text = "بایگانی پرونده‌ها",
                Dock = DockStyle.Top, Height = 40, ForeColor = Color.White,
                Font = UiTheme.FontBold(UiTheme.SizeLarge),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(0, 6, 20, 0)
            };
            _lblSummary = new Label
            {
                Dock = DockStyle.Fill, ForeColor = Color.FromArgb(0xCF, 0xDD, 0xEE),
                Font = UiTheme.Font(UiTheme.SizeSmall),
                TextAlign = ContentAlignment.TopLeft,
                Padding = new Padding(0, 0, 20, 0)
            };
            header.Controls.Add(_lblSummary);
            header.Controls.Add(title);
            Controls.Add(header);
        }

        private Control BuildToolbar()
        {
            var bar = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = UiTheme.CardBack };

            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false, Padding = new Padding(12, 8, 12, 8)
            };

            _btnRefresh = UiTheme.CreateButton("بازخوانی", "⟲", UiTheme.Primary);
            _btnRefresh.Size = new Size(120, 34); _btnRefresh.Margin = new Padding(0, 0, 8, 0);
            _btnRefresh.Click += delegate { LoadArchived(); };
            flow.Controls.Add(_btnRefresh);

            _cmbMode = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 110, Font = UiTheme.Font(UiTheme.SizeBody),
                Margin = new Padding(0, 2, 8, 0)
            };
            _cmbMode.Items.AddRange(new object[] { "پرونده‌ها", "اسناد" });
            _cmbMode.SelectedIndex = 0;
            _cmbMode.SelectedIndexChanged += delegate { OnModeChanged(); };
            flow.Controls.Add(_cmbMode);

            _txtSearch = new TextBox { Width = 220, Font = UiTheme.Font(UiTheme.SizeBody), Margin = new Padding(0, 2, 8, 0) };
            _txtSearch.TextChanged += delegate { ApplyFilter(); };
            flow.Controls.Add(_txtSearch);

            flow.Controls.Add(new Label
            {
                Text = "جستجوی کد یا نام:", AutoSize = true,
                Font = UiTheme.Font(UiTheme.SizeSmall), ForeColor = UiTheme.TextMuted,
                Margin = new Padding(0, 8, 10, 0)
            });

            _btnOpenCase = UiTheme.CreateSecondaryButton("مشاهده پرونده", "▤");
            _btnOpenCase.Size = new Size(140, 34); _btnOpenCase.Margin = new Padding(6, 0, 4, 0);
            _btnOpenCase.Click += delegate { OpenCase(); };
            flow.Controls.Add(_btnOpenCase);

            _btnRestore = UiTheme.CreateSecondaryButton("بازگردانی", "↺");
            _btnRestore.Size = new Size(120, 34); _btnRestore.Margin = new Padding(4, 0, 4, 0);
            _btnRestore.Click += delegate { RestoreSelected(); };
            flow.Controls.Add(_btnRestore);

            _btnPurge = UiTheme.CreateButton("حذف همیشگی", "✕", UiTheme.Danger);
            _btnPurge.Size = new Size(140, 34); _btnPurge.Margin = new Padding(4, 0, 4, 0);
            _btnPurge.Click += delegate { PurgeSelected(); };
            flow.Controls.Add(_btnPurge);

            bar.Controls.Add(flow);
            return bar;
        }

        private void OnModeChanged()
        {
            _btnPurge.Visible = !IsDocMode;
            LoadArchived();
        }

        private void LoadArchived()
        {
            if (IsDocMode)
                LoadArchivedDocuments();
            else
                LoadArchivedCases();
        }

        private void LoadArchivedCases()
        {
            try
            {
                using (SQLiteConnection con = db.GetConnection())
                using (SQLiteCommand cmd = new SQLiteCommand(@"
                    SELECT CasID, Code AS ""کد اختصاصی"", HeadFullName AS ""نام سرپرست"",
                           ArchivedAt AS ""تاریخ بایگانی"", ArchivedBy AS ""بایگانی‌شده توسط""
                    FROM TblCase
                    WHERE IsArchived = 1 AND (@CID = 0 OR CenterID = @CID)
                    ORDER BY ArchivedAt DESC", con))
                {
                    AddInt(cmd, "@CID", SecurityContext.CenterFilterId);
                    con.Open();

                    using (var reader = cmd.ExecuteReader())
                    {
                        _table.Clear();
                        _table.Columns.Clear();
                        _table.Load(reader);
                    }
                }

                ApplyFilter();
                _lblSummary.Text = "تعداد پرونده‌های بایگانی‌شده: " + _table.Rows.Count.ToString("N0");
            }
            catch (Exception ex)
            {
                UiTheme.ShowError(this, "بارگذاری بایگانی ممکن نشد: " + ex.Message);
            }
        }

        // آموزش — اسناد از طریق FrmDocs.btnDelete_Click بایگانی می‌شوند
        // (TblDocs.IsArchived) اما تا این تغییر هیچ صفحه‌ای برای دیدن/بازگردانیِ
        // آن‌ها نبود. همان الگوی LoadArchivedCases، فقط با JOIN به TblCase
        // برای نمایشِ کد پرونده/نام سرپرست و فیلترِ مرکز.
        private void LoadArchivedDocuments()
        {
            try
            {
                using (SQLiteConnection con = db.GetConnection())
                using (SQLiteCommand cmd = new SQLiteCommand(@"
                    SELECT d.DocID, d.CasID, COALESCE(d.DocNo,'') AS ""شماره سند"",
                           COALESCE(d.DocType,'') AS ""نوع سند"",
                           c.Code AS ""کد پرونده"", c.HeadFullName AS ""نام سرپرست"",
                           d.ArchivedAt AS ""تاریخ بایگانی"", d.ArchivedBy AS ""بایگانی‌شده توسط""
                    FROM TblDocs d
                    JOIN TblCase c ON c.CasID = d.CasID
                    WHERE d.IsArchived = 1 AND (@CID = 0 OR c.CenterID = @CID)
                    ORDER BY d.ArchivedAt DESC", con))
                {
                    AddInt(cmd, "@CID", SecurityContext.CenterFilterId);
                    con.Open();

                    using (var reader = cmd.ExecuteReader())
                    {
                        _table.Clear();
                        _table.Columns.Clear();
                        _table.Load(reader);
                    }
                }

                ApplyFilter();
                _lblSummary.Text = "تعداد اسناد بایگانی‌شده: " + _table.Rows.Count.ToString("N0");
            }
            catch (Exception ex)
            {
                UiTheme.ShowError(this, "بارگذاری بایگانی ممکن نشد: " + ex.Message);
            }
        }

        private void ApplyFilter()
        {
            if (_table.Columns.Count == 0) return;

            var view = new DataView(_table);
            string search = EscapeDataViewLike(_txtSearch.Text.Trim());
            if (search.Length > 0)
            {
                if (IsDocMode)
                    view.RowFilter = "[شماره سند] LIKE '%" + search + "%' OR [کد پرونده] LIKE '%" + search + "%' OR [نام سرپرست] LIKE '%" + search + "%'";
                else
                    view.RowFilter = "[کد اختصاصی] LIKE '%" + search + "%' OR [نام سرپرست] LIKE '%" + search + "%'";
            }

            _grid.DataSource = view;
            if (_grid.Columns.Contains("CasID")) _grid.Columns["CasID"].Visible = false;
            if (_grid.Columns.Contains("DocID")) _grid.Columns["DocID"].Visible = false;

            ShowHistory();
        }

        private int? SelectedCaseId()
        {
            if (_grid.CurrentRow == null) return null;
            object val = _grid.CurrentRow.Cells["CasID"].Value;
            if (val == null || val == DBNull.Value) return null;
            return Convert.ToInt32(val);
        }

        private int? SelectedDocId()
        {
            if (_grid.CurrentRow == null || !_grid.Columns.Contains("DocID")) return null;
            object val = _grid.CurrentRow.Cells["DocID"].Value;
            if (val == null || val == DBNull.Value) return null;
            return Convert.ToInt32(val);
        }

        private void ShowHistory()
        {
            if (IsDocMode)
            {
                _lblHistory.Text = "تاریخچه‌ی بایگانی فقط برای پرونده‌ها ثبت می‌شود؛ برای اسناد، رویداد بایگانی/بازگردانی در گزارش رویدادها قابل مشاهده است.";
                return;
            }

            int? casId = SelectedCaseId();
            if (casId == null)
            {
                _lblHistory.Text = "برای دیدنِ تاریخچه‌ی بایگانی، یک ردیف را انتخاب کنید.";
                return;
            }

            try
            {
                using (SQLiteConnection con = db.GetConnection())
                using (SQLiteCommand cmd = new SQLiteCommand(@"
                    SELECT Action, ActionAt, ActionBy FROM TblArchiveHistory
                    WHERE CasID = @CasID ORDER BY ArchiveHistoryID DESC LIMIT 10", con))
                {
                    AddInt(cmd, "@CasID", casId.Value);
                    con.Open();

                    var sb = new System.Text.StringBuilder();
                    using (var dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                            sb.AppendLine(
                                Convert.ToString(dr["ActionAt"]) + "   " +
                                Convert.ToString(dr["Action"]) + "   " +
                                Convert.ToString(dr["ActionBy"]));
                    }

                    _lblHistory.Text = sb.Length > 0 ? sb.ToString() : "تاریخچه‌ای برای این پرونده ثبت نشده.";
                }
            }
            catch (Exception ex)
            {
                _lblHistory.Text = "خطا در خواندن تاریخچه: " + ex.Message;
            }
        }

        private void OpenCase()
        {
            int? casId = SelectedCaseId();
            if (casId == null)
            {
                UiTheme.ShowWarning(this, "ابتدا یک ردیف را انتخاب کنید.");
                return;
            }

            try
            {
                using (FrmCase frm = new FrmCase(casId.Value))
                    frm.ShowDialog(this);
            }
            catch (Exception ex)
            {
                UiTheme.ShowError(this, "باز کردن پرونده ممکن نشد: " + ex.Message);
            }
        }

        private void RestoreSelected()
        {
            if (!CaseManagement.Enterprise.PermissionService.Require("Archive.Restore"))
            {
                UiTheme.ShowWarning(this, "بازگردانی فقط برای مدیر سیستم مجاز است.");
                return;
            }

            if (IsDocMode)
            {
                RestoreSelectedDocument();
                return;
            }

            int? casId = SelectedCaseId();
            if (casId == null)
            {
                UiTheme.ShowWarning(this, "ابتدا یک ردیف را انتخاب کنید.");
                return;
            }

            DialogResult confirm = Msg.Show(this,
                "این پرونده بازگردانی و دوباره در فهرست اصلی نمایش داده شود؟",
                "بازگردانی", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes)
                return;

            try
            {
                using (SQLiteConnection con = db.GetConnection())
                {
                    con.Open();

                    using (SQLiteCommand cmd = new SQLiteCommand(@"
                        UPDATE TblCase SET IsArchived = 0, ArchivedAt = NULL, ArchivedBy = NULL
                        WHERE CasID = @CasID", con))
                    {
                        AddInt(cmd, "@CasID", casId.Value);
                        cmd.ExecuteNonQuery();
                    }

                    using (SQLiteCommand historyCmd = new SQLiteCommand(@"
                        INSERT INTO TblArchiveHistory (CasID, Action, ActionBy)
                        VALUES (@CasID, 'بازگردانی', @ActionBy)", con))
                    {
                        AddInt(historyCmd, "@CasID", casId.Value);
                        AddNVarChar(historyCmd, "@ActionBy", SecurityContext.Username, 100);
                        historyCmd.ExecuteNonQuery();
                    }
                }

                AuditLogger.Log("بازگردانی", "TblCase", casId.Value, "IsArchived=1", "IsArchived=0");

                UiTheme.ShowSuccess(this, "پرونده بازگردانی شد.");
                LoadArchived();
            }
            catch (Exception ex)
            {
                UiTheme.ShowError(this, "بازگردانی ممکن نشد: " + ex.Message);
            }
        }

        // بازگردانیِ سند — همان الگوی بازگردانیِ پرونده، بدون TblArchiveHistory
        // (آن جدول فقط برای پرونده طراحی شده)؛ ثبتِ رویداد از طریق AuditLogger،
        // دقیقاً همان مکانیزمی که FrmDocs.btnDelete_Click برای بایگانی استفاده می‌کند.
        private void RestoreSelectedDocument()
        {
            int? docId = SelectedDocId();
            if (docId == null)
            {
                UiTheme.ShowWarning(this, "ابتدا یک ردیف را انتخاب کنید.");
                return;
            }

            DialogResult confirm = Msg.Show(this,
                "این سند بازگردانی و دوباره در فهرست اسناد پرونده نمایش داده شود؟",
                "بازگردانی", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes)
                return;

            try
            {
                using (SQLiteConnection con = db.GetConnection())
                using (SQLiteCommand cmd = new SQLiteCommand(@"
                    UPDATE TblDocs SET IsArchived = 0, ArchivedAt = NULL, ArchivedBy = NULL
                    WHERE DocID = @DocID", con))
                {
                    AddInt(cmd, "@DocID", docId.Value);
                    con.Open();
                    cmd.ExecuteNonQuery();
                }

                AuditLogger.Log("بازگردانی", "TblDocs", docId.Value, "IsArchived=1", "IsArchived=0");

                UiTheme.ShowSuccess(this, "سند بازگردانی شد.");
                LoadArchived();
            }
            catch (Exception ex)
            {
                UiTheme.ShowError(this, "بازگردانی ممکن نشد: " + ex.Message);
            }
        }

        // حذف همیشگی — فقط SuperAdmin، فقط روی پرونده‌های از قبل بایگانی‌شده.
        // همان منطق CollectCaseFilePaths/DELETE قبلیِ FrmCase.btnDelete_Click،
        // که با تبدیلِ آن دکمه به «بایگانی» به این صفحه منتقل شده است.
        private void PurgeSelected()
        {
            if (IsDocMode)
                return;

            if (!CaseManagement.Enterprise.PermissionService.Require("Archive.PermanentDelete"))
            {
                UiTheme.ShowWarning(this, "حذف همیشگی فقط برای مدیر ارشد سیستم مجاز است.");
                return;
            }

            int? casId = SelectedCaseId();
            if (casId == null)
            {
                UiTheme.ShowWarning(this, "ابتدا یک ردیف را انتخاب کنید.");
                return;
            }

            DialogResult confirm = Msg.Show(this,
                "این پرونده برای همیشه حذف شود؟ این عمل غیرقابل بازگشت است و فایل‌های پیوست‌شده نیز حذف می‌شوند.",
                "حذف همیشگی", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes)
                return;

            try
            {
                string oldValue = "CasID=" + casId.Value;
                List<string> filePaths = CollectFilePaths(casId.Value);

                // ⚠ هویت رکورد *پیش از* DELETE برداشته می‌شود (بعد از حذف
                // GlobalID خواندنی نیست)، ولی فقط پس از حذفِ موفق ثبت می‌شود —
                // این DELETE شرطِ IsArchived دارد و ممکن است چیزی حذف نکند.
                // فرزندان هم برداشته می‌شوند چون FK آبشاری بی‌صدا پاکشان می‌کند.
                var pendingDelete =
                    CaseManagement.Sync.SyncOutboxService.PrepareDelete("TblCase", casId.Value);

                // به همان دلیل، عکسِ فوریِ کاملِ رکورد هم پیش از DELETE برداشته
                // می‌شود و نسخهٔ «حذف» فقط پس از حذفِ موفق ثبت می‌گردد.
                string deletedSnapshot = CaseManagement.Enterprise.VersionService
                    .ReadSnapshotText("TblCase", casId.Value);

                using (SQLiteConnection con = db.GetConnection())
                using (SQLiteCommand cmd = new SQLiteCommand(
                    "DELETE FROM TblCase WHERE CasID = @CasID AND IsArchived = 1", con))
                {
                    AddInt(cmd, "@CasID", casId.Value);
                    con.Open();

                    if (cmd.ExecuteNonQuery() == 0)
                    {
                        UiTheme.ShowWarning(this, "رکورد پیدا نشد یا بایگانی نیست.");
                        return;
                    }
                }

                // حذف واقعاً انجام شد ⇒ ثبت در صفِ همگام‌سازی.
                CaseManagement.Sync.SyncOutboxService.CommitDelete(pendingDelete);

                foreach (string path in filePaths)
                    FileHelper.DeleteFileIfExists(path);

                AuditLogger.Log("حذف همیشگی", "TblCase", casId.Value, oldValue, "");

                CaseManagement.Enterprise.VersionService.CaptureDeleted(
                    "TblCase", casId.Value, deletedSnapshot);

                UiTheme.ShowSuccess(this, "پرونده برای همیشه حذف شد.");
                LoadArchived();
            }
            catch (Exception ex)
            {
                UiTheme.ShowError(this, "حذف همیشگی ممکن نشد: " + ex.Message);
            }
        }

        private List<string> CollectFilePaths(int caseId)
        {
            var paths = new List<string>();

            using (SQLiteConnection con = db.GetConnection())
            {
                con.Open();

                using (SQLiteCommand cmd = new SQLiteCommand(
                    "SELECT PhotoPath, FamilyPhotoPath FROM TblCase WHERE CasID = @CasID", con))
                {
                    AddInt(cmd, "@CasID", caseId);
                    using (var dr = cmd.ExecuteReader())
                    {
                        if (dr.Read())
                        {
                            AddPath(paths, dr, "PhotoPath");
                            AddPath(paths, dr, "FamilyPhotoPath");
                        }
                    }
                }

                using (SQLiteCommand cmd = new SQLiteCommand(
                    "SELECT MemberPhotoPath FROM TblFamily WHERE CasID = @CasID", con))
                {
                    AddInt(cmd, "@CasID", caseId);
                    using (var dr = cmd.ExecuteReader())
                        while (dr.Read())
                            AddPath(paths, dr, "MemberPhotoPath");
                }

                using (SQLiteCommand cmd = new SQLiteCommand(
                    "SELECT DocFilePath FROM TblDocs WHERE CasID = @CasID", con))
                {
                    AddInt(cmd, "@CasID", caseId);
                    using (var dr = cmd.ExecuteReader())
                        while (dr.Read())
                            AddPath(paths, dr, "DocFilePath");
                }
            }

            return paths;
        }

        private static void AddPath(List<string> paths, SQLiteDataReader dr, string column)
        {
            object val = dr[column];
            if (val != null && val != DBNull.Value)
            {
                string s = Convert.ToString(val);
                if (!string.IsNullOrWhiteSpace(s))
                    paths.Add(s);
            }
        }
    }
}
