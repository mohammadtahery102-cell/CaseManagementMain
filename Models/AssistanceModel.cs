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
    }
}
