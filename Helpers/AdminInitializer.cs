using System;
using System.Data.SQLite;
using CaseManagement.DAL;

namespace CaseManagement.Helpers
{
    // ─────────────────────────────────────────────────────────────────────────
    // ماژول «اداری و کارمندان» — ساخت جداول پایگاه‌داده (کاملاً افزایشی).
    //
    // آموزش — چرا یک ماژول تازه: تا امروز پروژه هیچ جدولِ کارمندی نداشت؛
    // تنها ردپای کارمند، ستونِ متنیِ AccSalary.EmployeeName بود که فقط یک نام
    // است، نه یک رکورد. فورم‌های رخصتی، ماموریت و درخواست استخدام به یک
    // موجودیتِ واقعیِ «کارمند» نیاز دارند.
    //
    // آموزش — چرا هیچ جدول Acc* یا جدول پرونده لمس نمی‌شود: بخش مالی در حال
    // استفادهٔ روزمره است و ماژولِ نو نباید هیچ ریسکی برایش بسازد. اتصالِ
    // AdmEmployee به AccSalary عمداً انجام *نشده* و به تصمیمِ بعدیِ کاربر
    // واگذار شده است.
    //
    // همهٔ جدول‌ها با پیشوند Adm ساخته می‌شوند و همگی CenterID دارند تا
    // فیلترِ چندمرکزیِ پروژه روی آن‌ها هم کار کند.
    // ─────────────────────────────────────────────────────────────────────────
    public static class AdminInitializer
    {
        public static void EnsureAdminObjects()
        {
            using (SQLiteConnection con = new DatabaseHelper().GetConnection())
            {
                con.Open();

                // ─── کارمند ──────────────────────────────────────────────────
                Exec(con, @"
CREATE TABLE IF NOT EXISTS AdmEmployee (
    EmployeeID  INTEGER PRIMARY KEY AUTOINCREMENT,
    FullName    TEXT    NOT NULL,
    FatherName  TEXT    NULL,
    TazkiraNo   TEXT    NULL,
    Position    TEXT    NULL,      -- وظیفه
    Department  TEXT    NULL,      -- بخش
    Phone       TEXT    NULL,
    Province    TEXT    NULL,
    District    TEXT    NULL,
    HireDate    TEXT    NULL,
    Status      TEXT    NOT NULL DEFAULT 'فعال',  -- فعال / غیرفعال
    Note        TEXT    NULL,
    CenterID    INTEGER NULL,
    CreatedBy   TEXT    NULL,
    CreatedAt   TEXT    NOT NULL DEFAULT (datetime('now'))
);");
                Exec(con, "CREATE INDEX IF NOT EXISTS IX_AdmEmployee_Name ON AdmEmployee(FullName);");
                Exec(con, "CREATE INDEX IF NOT EXISTS IX_AdmEmployee_Center ON AdmEmployee(CenterID);");

                // ─── درخواست رخصتی ───────────────────────────────────────────
                Exec(con, @"
CREATE TABLE IF NOT EXISTS AdmLeave (
    LeaveID      INTEGER PRIMARY KEY AUTOINCREMENT,
    EmployeeID   INTEGER NULL,
    LeaveType    TEXT    NULL,     -- عادی / مریضی / اضطراری / سایر
    OtherType    TEXT    NULL,
    FromDate     TEXT    NULL,
    ToDate       TEXT    NULL,
    TotalDays    TEXT    NULL,
    Reason       TEXT    NULL,
    ContactInfo  TEXT    NULL,
    ApprovalDate TEXT    NULL,
    ApprovedBy   TEXT    NULL,
    CenterID     INTEGER NULL,
    CreatedBy    TEXT    NULL,
    CreatedAt    TEXT    NOT NULL DEFAULT (datetime('now'))
);");
                Exec(con, "CREATE INDEX IF NOT EXISTS IX_AdmLeave_Emp ON AdmLeave(EmployeeID);");

                // ─── شروع و ختم ماموریت ──────────────────────────────────────
                Exec(con, @"
CREATE TABLE IF NOT EXISTS AdmMission (
    MissionID    INTEGER PRIMARY KEY AUTOINCREMENT,
    EmployeeID   INTEGER NULL,
    MissionPlace TEXT    NULL,     -- ولایت / ولسوالی محل ماموریت
    Purpose      TEXT    NULL,
    StartDate    TEXT    NULL,
    EndDate      TEXT    NULL,
    Allowance    REAL    NOT NULL DEFAULT 0,  -- حق‌الماموریت (افغانی)
    CenterID     INTEGER NULL,
    CreatedBy    TEXT    NULL,
    CreatedAt    TEXT    NOT NULL DEFAULT (datetime('now'))
);");
                Exec(con, "CREATE INDEX IF NOT EXISTS IX_AdmMission_Emp ON AdmMission(EmployeeID);");

                // ─── درخواست استخدام ─────────────────────────────────────────
                // متقاضی هنوز کارمند نیست، پس EmployeeID ندارد؛ اگر استخدام
                // شد، کاربر خودش یک رکورد کارمند می‌سازد.
                Exec(con, @"
CREATE TABLE IF NOT EXISTS AdmJobApplication (
    ApplicationID   INTEGER PRIMARY KEY AUTOINCREMENT,
    FullName        TEXT    NOT NULL,
    FatherName      TEXT    NULL,
    TazkiraNo       TEXT    NULL,
    BirthDate       TEXT    NULL,
    Address         TEXT    NULL,
    Phone           TEXT    NULL,
    MaritalStatus   TEXT    NULL,
    ChildrenCount   TEXT    NULL,
    Department      TEXT    NULL,
    JobTitle        TEXT    NULL,
    EducationLevel  TEXT    NULL,
    FieldOfStudy    TEXT    NULL,
    Institute       TEXT    NULL,
    InstituteCity   TEXT    NULL,
    StudyFrom       TEXT    NULL,
    StudyTo         TEXT    NULL,
    LastOrg         TEXT    NULL,
    LastPosition    TEXT    NULL,
    ExperienceYears TEXT    NULL,
    ExperienceEnd   TEXT    NULL,
    LeavingReason   TEXT    NULL,
    Language1       TEXT    NULL,
    Language1Level  TEXT    NULL,
    Language2       TEXT    NULL,
    Language2Level  TEXT    NULL,
    ComputerSkills  TEXT    NULL,
    Skills          TEXT    NULL,
    RefName         TEXT    NULL,
    RefPhone        TEXT    NULL,
    RefPosition     TEXT    NULL,
    CooperationType TEXT    NULL,
    CooperationNote TEXT    NULL,
    SalaryFigure    TEXT    NULL,
    SalaryWords     TEXT    NULL,
    JobDescription  TEXT    NULL,
    Status          TEXT    NOT NULL DEFAULT 'در انتظار',
    CenterID        INTEGER NULL,
    CreatedBy       TEXT    NULL,
    CreatedAt       TEXT    NOT NULL DEFAULT (datetime('now'))
);");
                Exec(con, "CREATE INDEX IF NOT EXISTS IX_AdmJobApp_Name ON AdmJobApplication(FullName);");

                // ─── قرارداد خدمات ترانسپورت ─────────────────────────────────
                // آموزش — چرا اینجا و نه در Acc*: قرارداد یک سندِ اداری است،
                // نه یک تراکنشِ مالی. پرداختِ مربوط به آن مثل همیشه در
                // AccTransaction ثبت می‌شود؛ TxnID زیر فقط پیوند است و
                // اختیاری، تا هیچ جدولِ مالی‌ای تغییر نکند.
                Exec(con, @"
CREATE TABLE IF NOT EXISTS AdmDriverContract (
    ContractID   INTEGER PRIMARY KEY AUTOINCREMENT,
    ContractNo   TEXT    NULL,
    DriverName   TEXT    NOT NULL,
    DriverPhone  TEXT    NULL,
    CarModel     TEXT    NULL,
    PlateNo      TEXT    NULL,
    PartyName    TEXT    NULL,     -- طرف حساب
    Areas        TEXT    NULL,     -- مناطق
    FuelType     TEXT    NULL,     -- باسوخت / بدون سوخت
    FromDate     TEXT    NULL,
    ToDate       TEXT    NULL,
    DailyWage    REAL    NOT NULL DEFAULT 0,
    ExtraPlace   TEXT    NULL,
    ExtraAmount  TEXT    NULL,
    TxnID        INTEGER NULL,     -- تراکنشِ مالیِ مرتبط (اختیاری)
    FilePath     TEXT    NULL,     -- نسخهٔ امضاشدهٔ آپلودشده
    CenterID     INTEGER NULL,
    CreatedBy    TEXT    NULL,
    CreatedAt    TEXT    NOT NULL DEFAULT (datetime('now'))
);");
                Exec(con, "CREATE INDEX IF NOT EXISTS IX_AdmDriverContract_Txn ON AdmDriverContract(TxnID);");
            }
        }

        private static void Exec(SQLiteConnection con, string sql)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(sql, con))
                cmd.ExecuteNonQuery();
        }
    }
}
