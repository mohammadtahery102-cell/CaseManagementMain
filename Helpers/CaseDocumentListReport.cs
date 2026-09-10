using CaseManagement.DAL;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;

namespace CaseManagement.Helpers
{
    // ═════════════════════════════════════════════════════════════════════════
    // گزارشِ «فهرست اسنادِ پرونده».
    //
    // آموزش — چرا این کلاس لازم شد: دکمهٔ چاپِ قبلی مستقیم
    // PrintHelper.PrintDataTable(dgvDocs.DataSource) را صدا می‌زد. آن جدول
    // برای *گرید* ساخته شده بود، نه برای *چاپ* — یعنی ستونِ داخلیِ DocID هم
    // چاپ می‌شد، نُه ستون با عرضِ کاملاً برابر کنارِ هم می‌نشستند، متنِ بلند
    // بریده می‌شد، و سند نه شناسنامهٔ پرونده داشت، نه شمارهٔ صفحه، نه جای
    // امضاء. اینجا داده مخصوصِ چاپ خوانده می‌شود و چیدمان با ReportDoc است.
    //
    // سند سه بخش دارد: شناسنامهٔ پرونده، فهرستِ اسناد، و کنترلِ اسنادِ
    // الزامی — چون کاربرِ این برگه معمولاً می‌خواهد ببیند «چه داریم و چه کم
    // داریم»، نه فقط فهرستِ خام.
    // ═════════════════════════════════════════════════════════════════════════
    public static class CaseDocumentListReport
    {
        public static ReportDoc Build(DatabaseHelper db, int caseId, string caseCode,
                                      ICollection<int> visibleDocIds, string searchTerm)
        {
            if (db == null) db = new DatabaseHelper();

            DataRow info = CaseInfo(db, caseId);
            DataTable docs = Docs(db, caseId);

            // فقط ردیف‌هایی که کاربر روی صفحه می‌بیند — اگر فیلترِ جستجو فعال
            // باشد، برگهٔ چاپی هم باید همان را نشان دهد، وگرنه کاربر چیزی را
            // چاپ می‌کند که ندیده است.
            if (visibleDocIds != null && visibleDocIds.Count > 0)
            {
                for (int i = docs.Rows.Count - 1; i >= 0; i--)
                {
                    int id = Convert.ToInt32(docs.Rows[i]["DocID"]);
                    if (!visibleDocIds.Contains(id)) docs.Rows.RemoveAt(i);
                }
            }

            int verified = 0, withFile = 0;
            foreach (DataRow r in docs.Rows)
            {
                if (Convert.ToString(r["وضعیت تأیید"]) == "تأیید شده") verified++;
                if (Convert.ToString(r["فایل"]) == "موجود") withFile++;
            }

            var doc = new ReportDoc
            {
                Title = "فهرست اسناد پرونده",
                Subtitle = Str(info, "HeadFullName"),
                DocumentCode = caseCode,
            };

            doc.HeaderFields.Add(Kv("کد پرونده", caseCode));
            doc.HeaderFields.Add(Kv("شماره فرم", Str(info, "FormNo")));
            doc.HeaderFields.Add(Kv("نام سرپرست", Str(info, "HeadFullName")));
            doc.HeaderFields.Add(Kv("نوع درخواست", Str(info, "RequestTypeName")));
            doc.HeaderFields.Add(Kv("وضعیت خدمات", Str(info, "ServiceStatusName")));
            doc.HeaderFields.Add(Kv("شماره تذکره", Str(info, "HeadTazkiraNo")));

            // ─── خلاصهٔ آماری ────────────────────────────────────────────────
            var summary = new ReportKeyValues { Columns = 4 };
            summary.Items.Add(Kv("تعداد اسناد", ReportDoc.Fa(docs.Rows.Count)));
            summary.Items.Add(Kv("دارای فایل", ReportDoc.Fa(withFile)));
            summary.Items.Add(Kv("تأییدشده", ReportDoc.Fa(verified)));
            summary.Items.Add(Kv("درصد تکمیل پرونده", ReportDoc.Fa(Str(info, "CompletionPercent")) + "٪"));

            doc.Blocks.Add(new ReportHeading { Text = "خلاصهٔ وضعیت" });
            doc.Blocks.Add(summary);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                doc.Blocks.Add(new ReportCallout
                {
                    Text = "این فهرست با فیلترِ جستجوی «" + searchTerm.Trim() +
                           "» چاپ شده و همهٔ اسنادِ پرونده را نشان نمی‌دهد.",
                    Accent = UiTheme.Warning
                });
            }

            // ─── جدولِ اسناد ─────────────────────────────────────────────────
            var table = new ReportTable
            {
                Data = docs,
                ShowRowNumbers = true,
                EmptyText = "برای این پرونده هنوز سندی ثبت نشده است.",
                Columns =
                {
                    ReportDocColumn.Of("شماره سند", "شماره سند", 1.3f, ReportAlign.Center),
                    ReportDocColumn.Of("نوع سند", "نوع سند", 2.0f),
                    ReportDocColumn.Of("دسته", "دسته", 1.7f),
                    ReportDocColumn.Of("برچسب‌ها", "برچسب‌ها", 1.1f),
                    ReportDocColumn.Of("نام فایل", "نام فایل", 2.2f),
                    ReportDocColumn.Of("فایل", "فایل", 0.8f, ReportAlign.Center),
                    ReportDocColumn.Of("تأیید", "وضعیت تأیید", 1.1f, ReportAlign.Center),
                }
            };

            doc.Blocks.Add(new ReportHeading
            {
                Text = "فهرست اسناد",
                Note = ReportDoc.Fa(docs.Rows.Count) + " ردیف"
            });
            doc.Blocks.Add(table);

            // ─── اسنادِ الزامیِ کم ────────────────────────────────────────────
            DataTable missing = CaseExportDataProvider.GetMissingDocuments(caseId);
            doc.Blocks.Add(new ReportHeading { Text = "کنترل اسناد الزامی" });

            if (missing == null || missing.Rows.Count == 0)
            {
                doc.Blocks.Add(new ReportCallout
                {
                    Text = "همهٔ اسنادِ الزامیِ این نوعِ پرونده موجود است.",
                    Accent = UiTheme.Success
                });
            }
            else
            {
                doc.Blocks.Add(new ReportTable
                {
                    Data = missing,
                    ShowRowNumbers = true,
                    Columns =
                    {
                        ReportDocColumn.Of("دسته سند الزامی", "دسته سند الزامی", 3f),
                        ReportDocColumn.Of("حداقل لازم", "حداقل لازم", 1f, ReportAlign.Center),
                        ReportDocColumn.Of("موجود", "موجود", 1f, ReportAlign.Center),
                    }
                });
                doc.Blocks.Add(new ReportCallout
                {
                    Text = "تا زمانی که اسنادِ بالا ضمیمه نشود، پرونده از نظرِ سیستم ناقص شمرده می‌شود.",
                    Accent = UiTheme.Danger
                });
            }

            doc.Blocks.Add(new ReportSpacer { Height = 12f });
            doc.Blocks.Add(new ReportSignatures
            {
                Roles = new[] { "تهیه‌کنندهٔ فهرست", "مسئول اسناد", "مدیر مرکز" }
            });

            return doc;
        }

        // ─── داده ────────────────────────────────────────────────────────────
        private static DataRow CaseInfo(DatabaseHelper db, int caseId)
        {
            DataTable t = db.Query(@"
SELECT c.Code, IFNULL(c.FormNo,'') AS FormNo, IFNULL(c.HeadFullName,'') AS HeadFullName,
       IFNULL(c.HeadTazkiraNo,'') AS HeadTazkiraNo,
       IFNULL(c.CompletionPercent, 0) AS CompletionPercent,
       IFNULL(rt.Name, IFNULL(c.RequestType, '')) AS RequestTypeName,
       IFNULL(ss.Name, IFNULL(c.ServiceStatus, '')) AS ServiceStatusName
FROM TblCase c
LEFT JOIN TblRequestType   rt ON rt.RequestTypeID   = c.RequestTypeID
LEFT JOIN TblServiceStatus ss ON ss.ServiceStatusID = c.ServiceStatusID
WHERE c.CasID = @id", new SQLiteParameter("@id", caseId));

            return t.Rows.Count > 0 ? t.Rows[0] : null;
        }

        private static DataTable Docs(DatabaseHelper db, int caseId)
        {
            DataTable t = db.Query(@"
SELECT d.DocID,
       IFNULL(d.DocNo, '')                                AS [شماره سند],
       IFNULL(d.DocType, '')                              AS [نوع سند],
       IFNULL(dc.Name, IFNULL(d.DocCategory, ''))         AS [دسته],
       IFNULL(d.DocTags, '')                              AS [برچسب‌ها],
       IFNULL(d.OriginalFileName, '')                     AS [نام فایل],
       IFNULL(d.DocFilePath, '')                          AS [مسیر],
       CASE WHEN IFNULL(d.IsVerified, 0) = 1
            THEN 'تأیید شده' ELSE 'در انتظار' END         AS [وضعیت تأیید]
FROM TblDocs d
LEFT JOIN TblDocumentCategory dc ON dc.DocumentCategoryID = d.DocumentCategoryID
WHERE d.CasID = @id AND IFNULL(d.IsArchived, 0) = 0
ORDER BY d.DocID", new SQLiteParameter("@id", caseId));

            // «فایل موجود است یا نه» تنها چیزی است که با SQL به‌دست نمی‌آید و
            // دقیقاً همان چیزی است که گیتِ فعال‌سازی به آن نگاه می‌کند.
            t.Columns.Add("فایل", typeof(string));
            foreach (DataRow r in t.Rows)
            {
                string path = Convert.ToString(r["مسیر"]);
                bool exists = false;
                try { exists = !string.IsNullOrWhiteSpace(path) && File.Exists(path); }
                catch { }
                r["فایل"] = exists ? "موجود" : "ندارد";
            }
            t.Columns.Remove("مسیر");

            return t;
        }

        private static string Str(DataRow row, string column)
        {
            if (row == null || !row.Table.Columns.Contains(column)) return "";
            object v = row[column];
            return v == null || v == DBNull.Value ? "" : Convert.ToString(v);
        }

        private static KeyValuePair<string, string> Kv(string key, string value)
        {
            return new KeyValuePair<string, string>(key, value ?? "");
        }
    }
}
