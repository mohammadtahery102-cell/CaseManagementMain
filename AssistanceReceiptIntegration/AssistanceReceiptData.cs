namespace CaseManagement.AssistanceReceiptIntegration
{
    // ─────────────────────────────────────────────────────────────────────────
    // مدل داده برگه دریافتی مساعدت — دقیقاً منطبق بر قرارداد JSON مستندسازی‌شده
    // در Templates\assistance-receipt\docs\FIELD_MAPPING.md (نام هر پراپرتی
    // عیناً همان کلید JSON مورد انتظار receipt.js است). آینه GuardianCardData.
    //
    // آموزش — DisplacedCardNo عمداً اینجا نیست: طبق تصمیم صریح کاربر، فعلاً
    // ستونی برای آن ساخته نمی‌شود (معادلی در دیتابیس پیدا نشد). چون
    // receipt.js از قبل d.DisplacedCardNo را می‌خواند (بستهٔ فریزشده تغییر
    // نکرده)، این پراپرتی نگه داشته می‌شود تا کلید JSON موجود باشد، ولی
    // AssistanceReceiptService همیشه رشتهٔ خالی در آن می‌گذارد — دقیقاً همان
    // رفتاری که خودِ FIELD_MAPPING.md برای مقادیر نامعلوم پیشنهاد داده.
    // ─────────────────────────────────────────────────────────────────────────
    public class AssistanceReceiptData
    {
        public string OrganizationName { get; set; }
        public string Logo { get; set; }

        public string ReceiptCode { get; set; }
        public string SerialNo { get; set; }

        public string RecipientName { get; set; }
        public string FatherName { get; set; }
        public string TazkiraNo { get; set; }
        public string Phone { get; set; }

        public string AidTypeAndAmount { get; set; }
        public string DistributionDate { get; set; }
        public string ProvinceDistrict { get; set; }
        public string FamilyMembersCount { get; set; }
        public string ProgramName { get; set; }
        public string RequestType { get; set; }
        public string PickupLocation { get; set; }
        public string CoordinatorPhone { get; set; }
        public string DisplacedCardNo { get; set; }

        public string Photo { get; set; }
        public string Barcode { get; set; }
    }
}
