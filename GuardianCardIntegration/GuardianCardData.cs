using System.Collections.Generic;

namespace CaseManagement.GuardianCardIntegration
{
    // ─────────────────────────────────────────────────────────────────────────
    // مدل داده کارت شناسایی سرپرست — دقیقاً منطبق بر قرارداد JSON مستندسازی‌شده
    // در GuardianCard\docs\FIELD_MAPPING.md و GuardianCard\docs\TEMPLATE_SCHEMA.json
    // (نام و املای هر پراپرتی عیناً همان کلید JSON مورد انتظار guardian-card.js
    // است، چون این کلاس مستقیماً به همان ساختار سریالایز می‌شود).
    // این پروژه چیزی داخل پوشه GuardianCard را تغییر نمی‌دهد؛ این کلاس فقط
    // «قرارداد ورودی» آن را در سمت C# نمایندگی می‌کند.
    // ─────────────────────────────────────────────────────────────────────────
    public class GuardianCardData
    {
        public string OrganizationName { get; set; }
        public string BranchName { get; set; }
        public string BranchCode { get; set; }
        public string CardNumber { get; set; }

        public string PublicCode { get; set; }
        public string GuardianName { get; set; }
        public string FatherName { get; set; }
        public string NationalID { get; set; }
        public string OrphansCount { get; set; }
        public string IssueDate { get; set; }
        public string ExpiryDate { get; set; }

        public string Province { get; set; }
        public string District { get; set; }
        public string Village { get; set; }

        public string Notice1 { get; set; }
        public string Notice2 { get; set; }
        public string Notice3 { get; set; }
        public string Notice4 { get; set; }
        public string Notice5 { get; set; }

        public string Address { get; set; }
        public string Phone { get; set; }
        public string Website { get; set; }
        public string Email { get; set; }

        public string SignatureLabel { get; set; }
        public string IssuedBy { get; set; }
        public string Position { get; set; }
        public string LegalLine { get; set; }

        // مسیرهای نسبی داخل پوشه کاریِ staged (نسبت به index.html)؛ هرکدام خالی
        // بماند، guardian-card.js خودش placeholder را نگه می‌دارد (رفتار موجود آن).
        public string Portrait { get; set; }
        public string Logo { get; set; }
        public string Photo { get; set; }
        public string QRCode { get; set; }
        public string Barcode { get; set; }
        public string Signature { get; set; }
        public string Stamp { get; set; }

        public List<PaymentLedgerRow> PaymentLedger { get; set; } = new List<PaymentLedgerRow>();
    }

    public class PaymentLedgerRow
    {
        public string Month { get; set; }
        public int MonthIndex { get; set; }
        public string PaymentDate { get; set; }
        public string MonthlyAmount { get; set; }
        public string OfficerSignature { get; set; }
        public string RecipientSignature { get; set; }
    }
}
