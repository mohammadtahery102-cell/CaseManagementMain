using System;

namespace CaseManagement.Models
{
    // ساختار ساده متناظر با جدول TblFamily.
    public class FamilyModel
    {
        public int FamID { get; set; }
        public int CasID { get; set; }
        public string MemberName { get; set; }
        public string MemberFatherName { get; set; }
        public string MemberIdCardType { get; set; }
        public string MemberTazkiraNo { get; set; }
        public DateTime? BirthDate { get; set; }
        public string MemberSadat { get; set; }
        public string Gender { get; set; }
        public string PhysicalStatus { get; set; }
        public string HasDisability { get; set; }
        public string MemberDisabilityDegree { get; set; }
        public string MemberEducation { get; set; }
        public string SchoolName { get; set; }
        public string GradeLevel { get; set; }
        public string UniversityName { get; set; }
        public string StudyYear { get; set; }
        public string Major { get; set; }
        public string StudyField { get; set; }
        public string OfficialStatus { get; set; }
        public string Details { get; set; }
        public string MemberPhotoPath { get; set; }
        public string Skill { get; set; }
        public string LeaveReason { get; set; }

        // نقش این عضو در خانواده (یتیم/پدر/مادر/فرزند/سرپرست/سایر) — Lookup
        // دسته «MemberRole». خالی = هنوز بازبینی نشده.
        public string MemberRole { get; set; }
    }
}
