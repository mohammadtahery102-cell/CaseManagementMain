using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using CaseManagement.DAL;
using CaseManagement.Helpers;

namespace CaseManagement.AssistanceReceiptIntegration
{
    // ─────────────────────────────────────────────────────────────────────────
    // لایه دادهٔ بسته‌های مساعدتِ غیرنقدی — تعریف/ویرایش (تنظیمات) و کوئریِ
    // فیلترشدهٔ پرونده‌ها برای چاپِ گروهی (تبِ ثبت کمک / فرمِ چاپِ گروهیِ بسته).
    // ─────────────────────────────────────────────────────────────────────────
    public class AssistancePackageRepository
    {
        public const int MaxPackages = 5;

        private readonly DatabaseHelper _db = new DatabaseHelper();

        public List<AssistancePackage> GetAllPackages()
        {
            var packages = new List<AssistancePackage>();
            var byId = new Dictionary<int, AssistancePackage>();

            using (SQLiteConnection con = _db.GetConnection())
            {
                con.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(
                    "SELECT PackageID, Name, SortOrder FROM TblAssistancePackage ORDER BY SortOrder, PackageID", con))
                using (SQLiteDataReader dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        var pkg = new AssistancePackage
                        {
                            PackageID = Convert.ToInt32(dr["PackageID"]),
                            Name = dr["Name"].ToString(),
                            SortOrder = Convert.ToInt32(dr["SortOrder"])
                        };
                        packages.Add(pkg);
                        byId[pkg.PackageID] = pkg;
                    }
                }

                using (SQLiteCommand cmd = new SQLiteCommand(
                    "SELECT ItemID, PackageID, ItemName, Quantity, Unit, SortOrder FROM TblAssistancePackageItem ORDER BY SortOrder, ItemID", con))
                using (SQLiteDataReader dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        int packageId = Convert.ToInt32(dr["PackageID"]);
                        AssistancePackage pkg;
                        if (!byId.TryGetValue(packageId, out pkg)) continue;

                        pkg.Items.Add(new AssistancePackageItem
                        {
                            ItemID = Convert.ToInt32(dr["ItemID"]),
                            PackageID = packageId,
                            ItemName = dr["ItemName"].ToString(),
                            Quantity = Convert.ToDecimal(dr["Quantity"]),
                            Unit = dr["Unit"] == DBNull.Value ? "" : dr["Unit"].ToString(),
                            SortOrder = Convert.ToInt32(dr["SortOrder"])
                        });
                    }
                }
            }

            return packages;
        }

        public AssistancePackage GetPackage(int packageId)
        {
            foreach (AssistancePackage pkg in GetAllPackages())
                if (pkg.PackageID == packageId)
                    return pkg;
            return null;
        }

        // درج/به‌روزرسانیِ یک بسته + جایگزینیِ کاملِ اقلامش. برای بستهٔ تازه
        // (PackageID <= 0)، سقفِ ۵ بسته (طبقِ خواستهٔ کاربر) اینجا اجرا می‌شود.
        public int SavePackage(AssistancePackage pkg)
        {
            if (pkg == null) throw new ArgumentNullException("pkg");
            if (string.IsNullOrWhiteSpace(pkg.Name))
                throw new ArgumentException("نامِ بسته نمی‌تواند خالی باشد.");

            int packageId = pkg.PackageID;

            _db.ExecuteInTransaction((con, tr) =>
            {
                if (packageId <= 0)
                {
                    using (SQLiteCommand count = new SQLiteCommand("SELECT COUNT(*) FROM TblAssistancePackage", con, tr))
                    {
                        int existing = Convert.ToInt32(count.ExecuteScalar());
                        if (existing >= MaxPackages)
                            throw new InvalidOperationException("حداکثر " + MaxPackages + " بسته قابل تعریف است.");
                    }

                    using (SQLiteCommand insert = new SQLiteCommand(
                        "INSERT INTO TblAssistancePackage (Name, SortOrder) VALUES (@Name, @Sort); SELECT last_insert_rowid();", con, tr))
                    {
                        insert.Parameters.AddWithValue("@Name", pkg.Name.Trim());
                        insert.Parameters.AddWithValue("@Sort", pkg.SortOrder);
                        packageId = Convert.ToInt32(insert.ExecuteScalar());
                    }
                }
                else
                {
                    using (SQLiteCommand update = new SQLiteCommand(
                        "UPDATE TblAssistancePackage SET Name=@Name, SortOrder=@Sort WHERE PackageID=@Id", con, tr))
                    {
                        update.Parameters.AddWithValue("@Name", pkg.Name.Trim());
                        update.Parameters.AddWithValue("@Sort", pkg.SortOrder);
                        update.Parameters.AddWithValue("@Id", packageId);
                        update.ExecuteNonQuery();
                    }

                    using (SQLiteCommand del = new SQLiteCommand(
                        "DELETE FROM TblAssistancePackageItem WHERE PackageID=@Id", con, tr))
                    {
                        del.Parameters.AddWithValue("@Id", packageId);
                        del.ExecuteNonQuery();
                    }
                }

                int order = 0;
                foreach (AssistancePackageItem item in pkg.Items)
                {
                    if (string.IsNullOrWhiteSpace(item.ItemName)) continue;

                    using (SQLiteCommand insertItem = new SQLiteCommand(
                        "INSERT INTO TblAssistancePackageItem (PackageID, ItemName, Quantity, Unit, SortOrder) " +
                        "VALUES (@Pid, @Name, @Qty, @Unit, @Sort)", con, tr))
                    {
                        insertItem.Parameters.AddWithValue("@Pid", packageId);
                        insertItem.Parameters.AddWithValue("@Name", item.ItemName.Trim());
                        insertItem.Parameters.AddWithValue("@Qty", item.Quantity);
                        insertItem.Parameters.AddWithValue("@Unit", (item.Unit ?? "").Trim());
                        insertItem.Parameters.AddWithValue("@Sort", order++);
                        insertItem.ExecuteNonQuery();
                    }
                }
            });

            return packageId;
        }

        public void DeletePackage(int packageId)
        {
            _db.ExecuteNonQuery("DELETE FROM TblAssistancePackage WHERE PackageID=@Id",
                new SQLiteParameter("@Id", packageId));
        }

        // نمایشِ خلاصهٔ اقلام برای گرید/رسید — مثلاً «آرد 1 بوجی، روغن 1 بشکه، شکر 4 کیلو».
        public static string FormatItemsSummary(AssistancePackage pkg)
        {
            if (pkg == null || pkg.Items == null || pkg.Items.Count == 0) return "";

            var parts = new List<string>();
            foreach (AssistancePackageItem item in pkg.Items)
            {
                string qty = item.Quantity == Math.Floor(item.Quantity)
                    ? ((long)item.Quantity).ToString()
                    : item.Quantity.ToString("0.##");
                parts.Add(item.ItemName + " " + qty + (string.IsNullOrEmpty(item.Unit) ? "" : (" " + item.Unit)));
            }
            return string.Join("، ", parts);
        }

        // شناسه‌های مساعدتِ متعلق به یک بسته، برای چاپِ گروهی — همان الگوی
        // امنیتیِ چندمرکزیِ AssistanceReceiptRepository.GetAssistanceIdsByFilter.
        public List<int> GetAssistanceIdsByPackageFilter(int packageId, string province = "", string district = "", int formNo = 0)
        {
            var ids = new List<int>();
            int cid = SecurityContext.CenterFilterId;

            using (SQLiteConnection con = _db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(@"
SELECT a.AssistanceID
FROM TblAssistance a
JOIN TblCase c ON c.CasID = a.CasID
WHERE a.PackageID = @Pid
  AND (@CID = 0 OR c.CenterID = @CID)
  AND (@Province = '' OR c.Province = @Province)
  AND (@District = '' OR c.District LIKE '%' || @District || '%')
  AND (@FormNo = 0 OR c.FormNo = @FormNo)
ORDER BY a.AssistanceDate DESC, a.AssistanceID DESC", con))
            {
                cmd.Parameters.AddWithValue("@Pid", packageId);
                cmd.Parameters.AddWithValue("@CID", cid);
                cmd.Parameters.AddWithValue("@Province", (province ?? "").Trim());
                cmd.Parameters.AddWithValue("@District", (district ?? "").Trim());
                cmd.Parameters.AddWithValue("@FormNo", formNo);
                con.Open();
                using (SQLiteDataReader dr = cmd.ExecuteReader())
                    while (dr.Read())
                        ids.Add(Convert.ToInt32(dr["AssistanceID"]));
            }

            return ids;
        }

        // نسخهٔ جدول‌محورِ همان کوئری، برای نمایشِ ردیف‌ها در گرید پیش از چاپ
        // (طبقِ خواستهٔ کاربر: گرید فقط پس از اجرای فیلتر پر می‌شود).
        public DataTable GetFilteredTable(int packageId, string province = "", string district = "", int formNo = 0)
        {
            int cid = SecurityContext.CenterFilterId;

            using (SQLiteConnection con = _db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(@"
SELECT a.AssistanceID, c.Code, c.HeadFullName, c.Province, c.District, c.FormNo, a.AssistanceDate
FROM TblAssistance a
JOIN TblCase c ON c.CasID = a.CasID
WHERE a.PackageID = @Pid
  AND (@CID = 0 OR c.CenterID = @CID)
  AND (@Province = '' OR c.Province = @Province)
  AND (@District = '' OR c.District LIKE '%' || @District || '%')
  AND (@FormNo = 0 OR c.FormNo = @FormNo)
ORDER BY a.AssistanceDate DESC, a.AssistanceID DESC", con))
            using (SQLiteDataAdapter da = new SQLiteDataAdapter(cmd))
            {
                cmd.Parameters.AddWithValue("@Pid", packageId);
                cmd.Parameters.AddWithValue("@CID", cid);
                cmd.Parameters.AddWithValue("@Province", (province ?? "").Trim());
                cmd.Parameters.AddWithValue("@District", (district ?? "").Trim());
                cmd.Parameters.AddWithValue("@FormNo", formNo);

                DataTable table = new DataTable();
                da.Fill(table);
                return table;
            }
        }
    }
}
