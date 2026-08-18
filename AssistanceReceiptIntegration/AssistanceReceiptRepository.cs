using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using CaseManagement.DAL;
using CaseManagement.Helpers;
using CaseManagement.Models;

namespace CaseManagement.AssistanceReceiptIntegration
{
    // ─────────────────────────────────────────────────────────────────────────
    // لایه داده ماژول برگه دریافتی مساعدت — آینه CaseCardRepository.
    // مشخصاتِ سرپرست/پرونده از GuardianCardIntegration.CaseCardRepository
    // خوانده می‌شود (GetCase/GetFamilyMemberCount)؛ اینجا فقط کوئری‌های
    // اختصاصیِ TblAssistance اضافه می‌شود — بدون تکرارِ کوئریِ TblCase.
    // ─────────────────────────────────────────────────────────────────────────
    public class AssistanceReceiptRepository
    {
        private readonly DatabaseHelper _db = new DatabaseHelper();

        public AssistanceModel GetAssistance(int assistanceId)
        {
            if (assistanceId <= 0)
                throw new ArgumentException("شناسه مساعدت معتبر نیست.");

            AssistanceModel model;
            using (SQLiteConnection con = _db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand("SELECT * FROM TblAssistance WHERE AssistanceID = @Id", con))
            {
                cmd.Parameters.AddWithValue("@Id", assistanceId);
                con.Open();
                using (SQLiteDataAdapter da = new SQLiteDataAdapter(cmd))
                {
                    DataTable dt = new DataTable();
                    da.Fill(dt);
                    if (dt.Rows.Count == 0)
                        throw new InvalidOperationException("رکورد مساعدت پیدا نشد.");
                    model = MapAssistance(dt.Rows[0]);
                }
            }

            // امنیت چندمرکزی — همان بررسیِ CenterGuard که کارت شناسایی/Export هم انجام می‌دهند.
            CenterGuard.EnsureCaseAccess(_db, model.CasID);

            return model;
        }

        // فهرستِ شناسه‌های مساعدت برای چاپ گروهی. هر پارامترِ خالی/صفر/null یعنی «همه».
        // isPrinted: null=همه، true=فقط چاپ‌شده، false=فقط چاپ‌نشده.
        public List<int> GetAssistanceIdsByFilter(string province = "", string district = "", int formNo = 0,
            string programName = "", DateTime? dateFrom = null, DateTime? dateTo = null,
            string assistanceType = "", bool? isPrinted = null)
        {
            var ids = new List<int>();
            int cid = SecurityContext.CenterFilterId;
            int printedMode = isPrinted.HasValue ? (isPrinted.Value ? 1 : 2) : 0;

            using (SQLiteConnection con = _db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(@"
SELECT a.AssistanceID
FROM TblAssistance a
JOIN TblCase c ON c.CasID = a.CasID
WHERE (@CID = 0 OR c.CenterID = @CID)
  AND (@Province = '' OR c.Province = @Province)
  AND (@District = '' OR c.District LIKE '%' || @District || '%')
  AND (@FormNo = 0 OR c.FormNo = @FormNo)
  AND (@ProgramName = '' OR a.ProgramName = @ProgramName)
  AND (@DateFrom = '' OR a.AssistanceDate >= @DateFrom)
  AND (@DateTo = '' OR a.AssistanceDate <= @DateTo)
  AND (@AssistanceType = '' OR a.AssistanceType = @AssistanceType)
  AND (@PrintedMode = 0
       OR (@PrintedMode = 1 AND a.PrintedAt IS NOT NULL)
       OR (@PrintedMode = 2 AND a.PrintedAt IS NULL))
ORDER BY a.AssistanceDate DESC, a.AssistanceID DESC", con))
            {
                cmd.Parameters.AddWithValue("@CID", cid);
                cmd.Parameters.AddWithValue("@Province", (province ?? "").Trim());
                cmd.Parameters.AddWithValue("@District", (district ?? "").Trim());
                cmd.Parameters.AddWithValue("@FormNo", formNo);
                cmd.Parameters.AddWithValue("@ProgramName", (programName ?? "").Trim());
                cmd.Parameters.AddWithValue("@DateFrom", dateFrom.HasValue ? dateFrom.Value.ToString("yyyy-MM-dd") : "");
                cmd.Parameters.AddWithValue("@DateTo", dateTo.HasValue ? dateTo.Value.ToString("yyyy-MM-dd") : "");
                cmd.Parameters.AddWithValue("@AssistanceType", (assistanceType ?? "").Trim());
                cmd.Parameters.AddWithValue("@PrintedMode", printedMode);
                con.Open();
                using (SQLiteDataReader dr = cmd.ExecuteReader())
                    while (dr.Read())
                        ids.Add(Convert.ToInt32(dr["AssistanceID"]));
            }

            return ids;
        }

        // آموزش — شمارندهٔ ترتیبیِ مرکز-محورِ ReceiptNo، فقط در اولین چاپ
        // مقداردهی می‌شود (اگر پیشتر مقداردهی شده، همان مقدار برمی‌گردد و
        // PrintedAt دست‌نخورده می‌ماند). چون ReceiptNo ستونی کاملاً تازه است
        // (بدون دادهٔ قدیمیِ نامنظم مثلِ FormNo)، به پیچیدگیِ CaseNumbering
        // نیازی نیست — یک MAX(ReceiptNo)+1 ساده و مرکز-محور کافی است.
        // ExecuteInTransaction (DatabaseHelper) اتمیک‌بودنِ خواندن+نوشتن را
        // تضمین می‌کند تا دو چاپِ هم‌زمان یک شماره نگیرند.
        public int EnsureReceiptNumberAssigned(int assistanceId, int centerId)
        {
            int receiptNo = 0;

            _db.ExecuteInTransaction((con, tr) =>
            {
                using (SQLiteCommand check = new SQLiteCommand(
                    "SELECT ReceiptNo FROM TblAssistance WHERE AssistanceID = @Id", con, tr))
                {
                    check.Parameters.AddWithValue("@Id", assistanceId);
                    object existing = check.ExecuteScalar();
                    if (existing != null && existing != DBNull.Value)
                    {
                        receiptNo = Convert.ToInt32(existing);
                        return;
                    }
                }

                using (SQLiteCommand max = new SQLiteCommand(@"
SELECT COALESCE(MAX(a.ReceiptNo), 0) + 1
FROM TblAssistance a
JOIN TblCase c ON c.CasID = a.CasID
WHERE a.ReceiptNo IS NOT NULL AND (@CID = 0 OR c.CenterID = @CID)", con, tr))
                {
                    max.Parameters.AddWithValue("@CID", centerId);
                    receiptNo = Convert.ToInt32(max.ExecuteScalar());
                }

                using (SQLiteCommand update = new SQLiteCommand(
                    "UPDATE TblAssistance SET ReceiptNo = @No, PrintedAt = COALESCE(PrintedAt, datetime('now')) WHERE AssistanceID = @Id",
                    con, tr))
                {
                    update.Parameters.AddWithValue("@No", receiptNo);
                    update.Parameters.AddWithValue("@Id", assistanceId);
                    update.ExecuteNonQuery();
                }
            });

            return receiptNo;
        }

        private static AssistanceModel MapAssistance(DataRow row)
        {
            return new AssistanceModel
            {
                AssistanceID = Convert.ToInt32(row["AssistanceID"]),
                CasID = Convert.ToInt32(row["CasID"]),
                AssistanceDate = row["AssistanceDate"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(row["AssistanceDate"]),
                Amount = row["Amount"] == DBNull.Value ? 0m : Convert.ToDecimal(row["Amount"]),
                AssistanceType = GetString(row, "AssistanceType"),
                Description = GetString(row, "Description"),
                CreatedAt = row["CreatedAt"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(row["CreatedAt"]),
                CreatedBy = GetString(row, "CreatedBy"),
                ReceiptNo = row.Table.Columns.Contains("ReceiptNo") && row["ReceiptNo"] != DBNull.Value ? (int?)Convert.ToInt32(row["ReceiptNo"]) : null,
                PrintedAt = row.Table.Columns.Contains("PrintedAt") && row["PrintedAt"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(row["PrintedAt"]) : null,
                ProgramName = GetString(row, "ProgramName"),
                PickupLocation = GetString(row, "PickupLocation"),
                CoordinatorPhone = GetString(row, "CoordinatorPhone"),
                PackageID = row.Table.Columns.Contains("PackageID") && row["PackageID"] != DBNull.Value ? (int?)Convert.ToInt32(row["PackageID"]) : null
            };
        }

        private static string GetString(DataRow row, string col)
        {
            if (!row.Table.Columns.Contains(col) || row[col] == DBNull.Value) return "";
            return row[col].ToString();
        }
    }
}
