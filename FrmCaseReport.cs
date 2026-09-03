using System;
using System.Data;
using System.Data.SQLite;
using System.Windows.Forms;
using CaseManagement.DAL;
using CaseManagement.Helpers;
using Microsoft.Reporting.WinForms;

namespace CaseManagement
{
    public partial class FrmCaseReport : Form
    {
        private readonly DatabaseHelper db = new DatabaseHelper();

        public int CaseId { get; set; }

        public FrmCaseReport()
        {
            InitializeComponent();
        }

        public FrmCaseReport(int caseId)
            : this()
        {
            CaseId = caseId;
        }

        private void FrmCaseReport_Load(object sender, EventArgs e)
        {
            Text = "گزارش پرونده";
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = true;
            try { Icon = CaseManagement.Helpers.LogoHelper.GetAppIcon(); } catch { }
            // به درخواست کاربر: هیچ فرمی تمام‌صفحه باز نمی‌شود و اندازه‌اش ثابت است.
            WindowState = FormWindowState.Normal;
            CaseManagement.Helpers.UiTheme.MakeFixedSize(this, 1100, 700);

            reportViewer1.Dock = DockStyle.Fill;
            reportViewer1.ProcessingMode = ProcessingMode.Local;
            ConfigureLocalReport();
            reportViewer1.RefreshReport();
        }

        private void ConfigureLocalReport()
        {
            reportViewer1.LocalReport.DataSources.Clear();
            reportViewer1.LocalReport.ReportEmbeddedResource = "CaseManagement.RptFullCase.rdlc";

            if (CaseId <= 0)
                return;

            try
            {
                CenterGuard.EnsureCaseAccess(db, CaseId);
            }
            catch (CenterAccessDeniedException ex)
            {
                Msg.Show(ex.Message);
                Close();
                return;
            }

            reportViewer1.LocalReport.DataSources.Add(
                new ReportDataSource("CaseData", GetDataTable("SELECT * FROM TblCase WHERE CasID = @CasID")));

            reportViewer1.LocalReport.DataSources.Add(
                new ReportDataSource("FamilyData", GetDataTable("SELECT * FROM TblFamily WHERE CasID = @CasID ORDER BY FamID")));

            reportViewer1.LocalReport.DataSources.Add(
                new ReportDataSource("DocsData", GetDataTable("SELECT * FROM TblDocs WHERE CasID = @CasID ORDER BY DocID")));
        }

        private DataTable GetDataTable(string query)
        {
            using (var con = db.GetConnection())
            using (var cmd = new SQLiteCommand(query, con))
            {
                cmd.Parameters.AddWithValue("@CasID", CaseId);
                con.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    DataTable table = new DataTable();
                    table.Load(reader);
                    return table;
                }
            }
        }
    }
}
