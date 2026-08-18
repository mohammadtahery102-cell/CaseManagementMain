using System;

namespace CaseManagement.Models
{
    // ساختار ساده متناظر با جدول TblCase — فعلاً فقط برای آماده‌سازی
    // توسعه آینده است و هیچ کد فعلی از آن استفاده نمی‌کند.
    public class CaseModel
    {
        public int CasID { get; set; }
        public int? FormNo { get; set; }
        public string Code { get; set; }
        public string Phone { get; set; }
        public string PhotoPath { get; set; }
        public string FamilyPhotoPath { get; set; }
        public string CaseNo { get; set; }
        public DateTime? CaseDate { get; set; }
        public string District { get; set; }
        public string RequestType { get; set; }
        public string PriorityLevel { get; set; }
        public string HeadFullName { get; set; }
        public string HeadFatherName { get; set; }
        public string HeadSadat { get; set; }
        public string Religion { get; set; }
        public string HeadIdCardType { get; set; }
        public string HeadTazkiraNo { get; set; }
        public string PhysicalStatusNotes { get; set; }
        public string HeadOriginalResidence { get; set; }
        public string HeadCurrentResidence { get; set; }
        public string RelationshipToFamily { get; set; }
        public string RelativePhone { get; set; }
        public string CoveredByOrg { get; set; }
        public string Job { get; set; }
        public string Skill { get; set; }
        public string DisabilityDegree { get; set; }
        public string DisabilityType { get; set; }
        public string MigrationCardType { get; set; }
        public string MaritalStatus { get; set; }
        public string Surveyors { get; set; }
        public DateTime? SurveyDate { get; set; }
        public string LocationAddress { get; set; }
        public string EducationLevel { get; set; }
        public string ServiceStatus { get; set; }
        public string UrgentSituation { get; set; }
        public string Zone { get; set; }
        public string Province { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // متن شرایط/هشدارهای کارت شناسایی — override اختصاصی این پرونده.
        // NULL/خالی یعنی «مقدار سراسری تنظیمات مؤسسه» استفاده شود
        // (نگاه کنید CardService.CardText).
        public string CardNotice1 { get; set; }
        public string CardNotice2 { get; set; }
        public string CardNotice3 { get; set; }
        public string CardNotice4 { get; set; }
        public string CardNotice5 { get; set; }
        public string CardLegalLine { get; set; }
        public string CardPhone { get; set; }
        public string CardAddress { get; set; }
    }
}
