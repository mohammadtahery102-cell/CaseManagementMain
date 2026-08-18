using System;

namespace CaseManagement.Helpers
{
    // معیارهای فیلترِ مشترکِ همه‌ی گزارش‌ها/خروجی‌های جمعی: ولایت، ولسوالی،
    // وضعیت خدمات، بازه‌ی تاریخ ثبت، نوع خانواده (RequestType) و فعال/غیرفعال.
    // مقدار خالی/null در هر فیلد یعنی «همه» (بدون شرط) — دقیقاً رفتار قبلی
    // وقتی کاربر فیلتری انتخاب نکند.
    public class ReportFilterCriteria
    {
        public string Province { get; set; } = "";
        public string District { get; set; } = "";
        public string ServiceStatus { get; set; } = "";

        // نوع خانواده = ستون RequestType در TblCase (یتیم/معلول/مهاجر/بدسرپرست/
        // کهولت سن/بی‌سرپرست) — نزدیک‌ترین معادلِ موجود در دیتابیس به «نوع خانواده».
        public string FamilyType { get; set; } = "";

        public DateTime? RegistrationDateFrom { get; set; }
        public DateTime? RegistrationDateTo { get; set; }

        // null = همه (فعال و غیرفعال)، true = فقط فعال (IsArchived=0)، false = فقط غیرفعال (IsArchived=1)
        public bool? ActiveOnly { get; set; }

        // آموزش — به درخواستِ کاربر: فیلترِ «تعداد اعضای خانواده» (مثلاً فقط
        // خانواده‌هایی که یک یتیم دارند، یا چند یتیم). null یعنی بدون محدودیت.
        public int? MinMemberCount { get; set; }
        public int? MaxMemberCount { get; set; }

        public bool HasAnyFilter =>
            !string.IsNullOrWhiteSpace(Province) ||
            !string.IsNullOrWhiteSpace(District) ||
            !string.IsNullOrWhiteSpace(ServiceStatus) ||
            !string.IsNullOrWhiteSpace(FamilyType) ||
            RegistrationDateFrom.HasValue ||
            RegistrationDateTo.HasValue ||
            ActiveOnly.HasValue ||
            MinMemberCount.HasValue ||
            MaxMemberCount.HasValue;
    }
}
