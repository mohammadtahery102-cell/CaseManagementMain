using System.Collections.Generic;

namespace CaseManagement.Payroll.Domain
{
    public static class PayrollCodes
    {
        public const string EntityRun = "PrPayrollRun";
        public const string DocRun = "PrRun";
        public const string AccountExpense = "5100";
        public const string AccountPayable = "2200";
    }

    public static class PayrollPermissions
    {
        public const string View = "Payroll.View";
        public const string Create = "Payroll.Create";
        public const string Approve = "Payroll.Approve";
        public const string Post = "Payroll.Post";
    }

    public class PrEmployee
    {
        public long EmployeeId { get; set; }
        public int CompanyId { get; set; }
        public int CenterId { get; set; }
        public long DepartmentId { get; set; }
        public long PositionId { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public long GrossMinor { get; set; }
        public long RowVersion { get; set; }
    }

    public class PrRun
    {
        public long RunId { get; set; }
        public int CompanyId { get; set; }
        public int CenterId { get; set; }
        public long PeriodId { get; set; }
        public string DocNo { get; set; }
        public string Status { get; set; }
        public string RunDate { get; set; }
        public long TotalMinor { get; set; }
        public long RowVersion { get; set; }
    }

    public class EmployeeListRow
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public long GrossMinor { get; set; }
    }

    public class PayrollSummaryRow
    {
        public string DocNo { get; set; }
        public string Status { get; set; }
        public long TotalMinor { get; set; }
    }

    public class PayrollRegisterRow
    {
        public string EmployeeCode { get; set; }
        public string EmployeeName { get; set; }
        public long AmountMinor { get; set; }
    }
}
