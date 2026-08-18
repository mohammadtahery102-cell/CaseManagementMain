using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;

namespace CaseManagement.Helpers
{
    public enum ReportColumnType { Text, Number, Date }

    // یک ستونِ قابل‌انتخاب در گزارش‌ساز. Expression همیشه از یک لیستِ ثابتِ
    // سفیدِ از پیش‌نوشته‌شده می‌آید — هرگز از ورودیِ کاربر ساخته نمی‌شود، پس
    // تزریق SQL از طریق انتخابِ ستون ممکن نیست.
    public sealed class ReportColumn
    {
        public string Key;
        public string DisplayName;
        public string Expression; // مثل c.HeadFullName یا c.Code
        public ReportColumnType Type;

        public ReportColumn(string key, string displayName, string expression, ReportColumnType type)
        {
            Key = key; DisplayName = displayName; Expression = expression; Type = type;
        }
    }

    // یک منبعِ گزارش (Cases/Family/Documents/Assistance) با FROM/JOIN ثابت و
    // فهرستِ ستون‌های مجاز.
    public sealed class ReportSource
    {
        public string Key;
        public string DisplayName;
        public string FromClause;      // مثل "TblCase c"
        public string CenterColumn;    // برای فیلترِ مرکز، مثل "c.CenterID"
        public string ArchivedColumn;  // اگر null باشد یعنی این منبع IsArchived ندارد
        public List<ReportColumn> Columns = new List<ReportColumn>();

        public ReportColumn FindColumn(string key)
        {
            return Columns.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase));
        }
    }

    public enum ReportFilterOperator { Equals, Contains, GreaterThan, LessThan, GreaterOrEqual, LessOrEqual }

    public sealed class ReportFilter
    {
        public string ColumnKey;
        public ReportFilterOperator Op;
        public string Value;
    }

    // تعریفِ کاملِ یک گزارش، قابلِ سریالایز به JSON برای ذخیره در TblReportTemplate.
    public sealed class ReportDefinition
    {
        public string SourceKey;
        public List<string> ColumnKeys = new List<string>();
        public List<ReportFilterDto> Filters = new List<ReportFilterDto>();
        public string GroupByKey;
        public string SortColumnKey;
        public bool SortDescending;
    }

    // نسخه‌ی ساده‌ی ReportFilter برای سریالایزِ JSON (Enum به‌صورت رشته).
    public sealed class ReportFilterDto
    {
        public string ColumnKey;
        public string Op;
        public string Value;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // فهرستِ ثابتِ منابع/ستون‌های مجاز گزارش‌ساز. هر منبعِ جدید فقط اینجا اضافه
    // می‌شود — فرم و اجراکننده‌ی کوئری هیچ نامِ ستون/جدولی را از کاربر مستقیماً
    // در SQL نمی‌گذارند.
    // ─────────────────────────────────────────────────────────────────────────
    public static class ReportCatalog
    {
        public static readonly List<ReportSource> Sources = BuildSources();

        public static ReportSource FindSource(string key)
        {
            return Sources.FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.OrdinalIgnoreCase));
        }

        private static List<ReportSource> BuildSources()
        {
            var list = new List<ReportSource>();

            var cases = new ReportSource
            {
                Key = "Cases", DisplayName = "پرونده‌ها",
                FromClause = "TblCase c",
                CenterColumn = "c.CenterID",
                ArchivedColumn = "c.IsArchived"
            };
            cases.Columns.Add(new ReportColumn("Code", "کد اختصاصی", "c.Code", ReportColumnType.Text));
            cases.Columns.Add(new ReportColumn("FormNo", "شماره فرم", "c.FormNo", ReportColumnType.Text));
            cases.Columns.Add(new ReportColumn("HeadFullName", "نام سرپرست", "c.HeadFullName", ReportColumnType.Text));
            cases.Columns.Add(new ReportColumn("HeadFatherName", "نام پدر سرپرست", "c.HeadFatherName", ReportColumnType.Text));
            cases.Columns.Add(new ReportColumn("HeadIdCardType", "نوع تذکره", "c.HeadIdCardType", ReportColumnType.Text));
            cases.Columns.Add(new ReportColumn("HeadTazkiraNo", "شماره تذکره", "c.HeadTazkiraNo", ReportColumnType.Text));
            cases.Columns.Add(new ReportColumn("PhysicalStatusNotes", "یادداشت وضعیت جسمی", "c.PhysicalStatusNotes", ReportColumnType.Text));
            cases.Columns.Add(new ReportColumn("Phone", "شماره تماس", "c.Phone", ReportColumnType.Text));
            cases.Columns.Add(new ReportColumn("Province", "ولایت", "c.Province", ReportColumnType.Text));
            cases.Columns.Add(new ReportColumn("District", "ولسوالی", "c.District", ReportColumnType.Text));
            cases.Columns.Add(new ReportColumn("ServiceStatus", "وضعیت خدمات", "c.ServiceStatus", ReportColumnType.Text));
            cases.Columns.Add(new ReportColumn("RequestType", "نوع درخواست", "c.RequestType", ReportColumnType.Text));
            cases.Columns.Add(new ReportColumn("PriorityLevel", "اولویت بندی اقتصادی", "c.PriorityLevel", ReportColumnType.Text));
            cases.Columns.Add(new ReportColumn("CoveredByOrg", "تحت پوشش دیگر مؤسسات", "c.CoveredByOrg", ReportColumnType.Text));
            cases.Columns.Add(new ReportColumn("CoveredByOrgNames", "اسامی مؤسسات تحت پوشش", "c.CoveredByOrgNames", ReportColumnType.Text));
            cases.Columns.Add(new ReportColumn("CaseDate", "تاریخ پرونده", "c.CaseDate", ReportColumnType.Date));
            list.Add(cases);

            var family = new ReportSource
            {
                Key = "Family", DisplayName = "اعضای خانواده",
                FromClause = "TblFamily f JOIN TblCase c ON c.CasID = f.CasID",
                CenterColumn = "c.CenterID",
                ArchivedColumn = "c.IsArchived"
            };
            family.Columns.Add(new ReportColumn("CaseCode", "کد پرونده", "c.Code", ReportColumnType.Text));
            family.Columns.Add(new ReportColumn("HeadFullName", "نام سرپرست", "c.HeadFullName", ReportColumnType.Text));
            family.Columns.Add(new ReportColumn("MemberName", "نام عضو", "f.MemberName", ReportColumnType.Text));
            family.Columns.Add(new ReportColumn("MemberFatherName", "نام پدر عضو", "f.MemberFatherName", ReportColumnType.Text));
            family.Columns.Add(new ReportColumn("MemberIdCardType", "نوع تذکره", "f.MemberIdCardType", ReportColumnType.Text));
            family.Columns.Add(new ReportColumn("MemberTazkiraNo", "شماره تذکره", "f.MemberTazkiraNo", ReportColumnType.Text));
            family.Columns.Add(new ReportColumn("Gender", "جنسیت", "f.Gender", ReportColumnType.Text));
            family.Columns.Add(new ReportColumn("BirthDate", "تاریخ تولد", "f.BirthDate", ReportColumnType.Date));
            family.Columns.Add(new ReportColumn("MemberEducation", "تحصیلات", "f.MemberEducation", ReportColumnType.Text));
            family.Columns.Add(new ReportColumn("HasDisability", "معلولیت", "f.HasDisability", ReportColumnType.Text));
            // وضعیت خدماتِ پرونده — محورِ اصلی گزارش‌گیری سامانه. با افزوده شدن
            // اینجا، کاربر می‌تواند در گزارش اعضای خانواده هم آن را نمایش دهد،
            // هم رویش فیلتر بگذارد و هم بر اساسش گروه‌بندی کند.
            family.Columns.Add(new ReportColumn("ServiceStatus", "وضعیت خدمات", "c.ServiceStatus", ReportColumnType.Text));
            // نوع درخواستِ پرونده — تا در گزارشِ اعضا بتوان ایتام را از سایر
            // بخش‌ها (معلول، مهاجر، بدسرپرست، …) جدا کرد؛ هم به‌عنوان ستون، هم
            // فیلتر و هم محورِ گروه‌بندی.
            family.Columns.Add(new ReportColumn("RequestType", "نوع درخواست", "c.RequestType", ReportColumnType.Text));
            list.Add(family);

            var docs = new ReportSource
            {
                Key = "Documents", DisplayName = "اسناد",
                FromClause = "TblDocs d JOIN TblCase c ON c.CasID = d.CasID",
                CenterColumn = "c.CenterID",
                ArchivedColumn = "c.IsArchived"
            };
            docs.Columns.Add(new ReportColumn("CaseCode", "کد پرونده", "c.Code", ReportColumnType.Text));
            docs.Columns.Add(new ReportColumn("HeadFullName", "نام سرپرست", "c.HeadFullName", ReportColumnType.Text));
            docs.Columns.Add(new ReportColumn("DocNo", "شماره سند", "d.DocNo", ReportColumnType.Text));
            docs.Columns.Add(new ReportColumn("DocType", "نوع سند", "d.DocType", ReportColumnType.Text));
            docs.Columns.Add(new ReportColumn("DocCategory", "دسته‌بندی سند", "d.DocCategory", ReportColumnType.Text));
            docs.Columns.Add(new ReportColumn("OriginalFileName", "نام فایل", "d.OriginalFileName", ReportColumnType.Text));
            docs.Columns.Add(new ReportColumn("ServiceStatus", "وضعیت خدمات", "c.ServiceStatus", ReportColumnType.Text));
            docs.Columns.Add(new ReportColumn("RequestType", "نوع درخواست", "c.RequestType", ReportColumnType.Text));
            list.Add(docs);

            var assistance = new ReportSource
            {
                Key = "Assistance", DisplayName = "کمک‌های مالی",
                FromClause = "TblAssistance a JOIN TblCase c ON c.CasID = a.CasID",
                CenterColumn = "c.CenterID",
                ArchivedColumn = "c.IsArchived"
            };
            assistance.Columns.Add(new ReportColumn("CaseCode", "کد پرونده", "c.Code", ReportColumnType.Text));
            assistance.Columns.Add(new ReportColumn("HeadFullName", "نام سرپرست", "c.HeadFullName", ReportColumnType.Text));
            assistance.Columns.Add(new ReportColumn("AssistanceDate", "تاریخ کمک", "a.AssistanceDate", ReportColumnType.Date));
            assistance.Columns.Add(new ReportColumn("Amount", "مبلغ", "a.Amount", ReportColumnType.Number));
            assistance.Columns.Add(new ReportColumn("AssistanceType", "نوع کمک", "a.AssistanceType", ReportColumnType.Text));
            assistance.Columns.Add(new ReportColumn("ServiceStatus", "وضعیت خدمات", "c.ServiceStatus", ReportColumnType.Text));
            list.Add(assistance);

            return list;
        }

        public static string OperatorDisplayName(ReportFilterOperator op)
        {
            switch (op)
            {
                case ReportFilterOperator.Equals: return "برابر است با";
                case ReportFilterOperator.Contains: return "شامل";
                case ReportFilterOperator.GreaterThan: return "بزرگ‌تر از";
                case ReportFilterOperator.LessThan: return "کوچک‌تر از";
                case ReportFilterOperator.GreaterOrEqual: return "بزرگ‌تر یا مساوی";
                case ReportFilterOperator.LessOrEqual: return "کوچک‌تر یا مساوی";
                default: return op.ToString();
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // اجرای گزارش: از تعریفِ بالا یک SQL پارامتری‌شده می‌سازد و اجرا می‌کند.
    // نامِ ستون/جدول هرگز از ورودیِ آزادِ کاربر نمی‌آید (فقط از ReportCatalog)،
    // مقدارهای فیلتر همیشه با SQLiteParameter پاس داده می‌شوند.
    // ─────────────────────────────────────────────────────────────────────────
    public static class ReportRunner
    {
        public static DataTable Run(ReportDefinition def, DAL.DatabaseHelper db)
        {
            ReportSource source = ReportCatalog.FindSource(def.SourceKey);
            if (source == null)
                throw new InvalidOperationException("منبع گزارش نامعتبر است.");

            List<ReportColumn> selectedColumns = def.ColumnKeys
                .Select(source.FindColumn)
                .Where(c => c != null)
                .ToList();

            if (selectedColumns.Count == 0)
                throw new InvalidOperationException("حداقل یک ستون را انتخاب کنید.");

            ReportColumn groupColumn = string.IsNullOrEmpty(def.GroupByKey) ? null : source.FindColumn(def.GroupByKey);

            var sql = new System.Text.StringBuilder();
            var parameters = new List<SQLiteParameter>();

            if (groupColumn != null)
            {
                sql.Append("SELECT ").Append(groupColumn.Expression).Append(" AS [").Append(groupColumn.DisplayName).Append("], ")
                   .Append("COUNT(1) AS [تعداد]");
            }
            else
            {
                sql.Append("SELECT ");
                sql.Append(string.Join(", ", selectedColumns.Select(c => c.Expression + " AS [" + c.DisplayName + "]")));
            }

            sql.Append(" FROM ").Append(source.FromClause).Append(" WHERE 1 = 1");

            if (source.ArchivedColumn != null)
                sql.Append(" AND ").Append(source.ArchivedColumn).Append(" = 0");

            if (source.CenterColumn != null)
            {
                sql.Append(" AND (@CID = 0 OR ").Append(source.CenterColumn).Append(" = @CID)");
                parameters.Add(new SQLiteParameter("@CID", SecurityContext.CenterFilterId));
            }

            int paramIndex = 0;
            foreach (ReportFilterDto filterDto in def.Filters ?? new List<ReportFilterDto>())
            {
                ReportColumn col = source.FindColumn(filterDto.ColumnKey);
                if (col == null || string.IsNullOrWhiteSpace(filterDto.Value)) continue;

                ReportFilterOperator op;
                if (!Enum.TryParse(filterDto.Op, out op)) continue;

                string pname = "@p" + (paramIndex++);
                string sqlOp;
                object value = filterDto.Value;

                switch (op)
                {
                    case ReportFilterOperator.Equals: sqlOp = "="; break;
                    case ReportFilterOperator.Contains:
                        sqlOp = "LIKE"; value = "%" + filterDto.Value + "%";
                        break;
                    case ReportFilterOperator.GreaterThan: sqlOp = ">"; break;
                    case ReportFilterOperator.LessThan: sqlOp = "<"; break;
                    case ReportFilterOperator.GreaterOrEqual: sqlOp = ">="; break;
                    case ReportFilterOperator.LessOrEqual: sqlOp = "<="; break;
                    default: continue;
                }

                if (col.Type == ReportColumnType.Number && op != ReportFilterOperator.Contains)
                {
                    decimal numVal;
                    if (!decimal.TryParse(filterDto.Value, out numVal)) continue;
                    value = numVal;
                }

                sql.Append(" AND ").Append(col.Expression).Append(' ').Append(sqlOp).Append(' ').Append(pname);
                parameters.Add(new SQLiteParameter(pname, value));
            }

            if (groupColumn != null)
            {
                sql.Append(" GROUP BY ").Append(groupColumn.Expression);
                sql.Append(" ORDER BY ");
                sql.Append(string.Equals(def.SortColumnKey, "Count", StringComparison.OrdinalIgnoreCase)
                    ? "[تعداد]" : groupColumn.Expression);
                sql.Append(def.SortDescending ? " DESC" : " ASC");
            }
            else
            {
                ReportColumn sortColumn = string.IsNullOrEmpty(def.SortColumnKey) ? null : source.FindColumn(def.SortColumnKey);
                if (sortColumn != null)
                    sql.Append(" ORDER BY ").Append(sortColumn.Expression).Append(def.SortDescending ? " DESC" : " ASC");
            }

            using (SQLiteConnection con = db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(sql.ToString(), con))
            {
                cmd.Parameters.AddRange(parameters.ToArray());
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
