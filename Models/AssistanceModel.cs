using System;

namespace CaseManagement.Models
{
    // ساختار ساده متناظر با جدول TblAssistance.
    public class AssistanceModel
    {
        public int AssistanceID { get; set; }
        public int CasID { get; set; }
        public DateTime AssistanceDate { get; set; }
        public decimal Amount { get; set; }
        public string AssistanceType { get; set; }
        public string Description { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string CreatedBy { get; set; }

        // ─── افزوده برای ماژول AssistanceReceiptIntegration (افزایشی) ────────
        public int? ReceiptNo { get; set; }
        public DateTime? PrintedAt { get; set; }
        public string ProgramName { get; set; }
        public string PickupLocation { get; set; }
        public string CoordinatorPhone { get; set; }

        // ─── افزوده برای بستهٔ مساعدتِ غیرنقدی (افزایشی) ─────────────────────
        public int? PackageID { get; set; }
    }
}
