using System.Collections.Generic;
using CaseManagement.GuardianCardIntegration;
using CaseManagement.Helpers;
using CaseManagement.Models;

namespace CaseManagement.AssistanceReceiptIntegration
{
    // ─────────────────────────────────────────────────────────────────────────
    // لایه تجاری ماژول برگه دریافتی مساعدت — آینه CardService. مشخصاتِ
    // سرپرست/تعدادِ اعضا از CaseCardRepository موجود خوانده می‌شود (بدون
    // تکرارِ کوئری)؛ این کلاس فقط فیلدهای مخصوصِ TblAssistance را می‌سازد و
    // همه‌چیز را طبقِ docs/FIELD_MAPPING.md به AssistanceReceiptData می‌ریزد.
    // ─────────────────────────────────────────────────────────────────────────
    public class AssistanceReceiptService
    {
        private readonly AssistanceReceiptRepository _repo;
        private readonly CaseCardRepository _caseRepo;
        private readonly AssistancePackageRepository _packageRepo;

        public AssistanceReceiptService()
            : this(new AssistanceReceiptRepository(), new CaseCardRepository(), new AssistancePackageRepository()) { }

        public AssistanceReceiptService(AssistanceReceiptRepository repo, CaseCardRepository caseRepo, AssistancePackageRepository packageRepo = null)
        {
            _repo = repo;
            _caseRepo = caseRepo;
            _packageRepo = packageRepo ?? new AssistancePackageRepository();
        }

        // چاپ/PDفِ واقعی — شمارهٔ ترتیبی را (اگر نبود) تخصیص و ذخیره می‌کند.
        public AssistanceReceiptData BuildReceiptData(int assistanceId)
        {
            return Build(assistanceId, commit: true);
        }

        // پیش‌نمایش — هیچ نوشتنی در دیتابیس انجام نمی‌دهد؛ اگر شمارهٔ برگه
        // قبلاً (از چاپِ واقعیِ قبلی) وجود داشته باشد همان را نشان می‌دهد،
        // وگرنه جعبهٔ کد/بارکد خالی می‌ماند تا وقتی چاپ/PDفِ واقعی انجام شود.
        public AssistanceReceiptData PreviewReceiptData(int assistanceId)
        {
            return Build(assistanceId, commit: false);
        }

        private AssistanceReceiptData Build(int assistanceId, bool commit)
        {
            AssistanceModel a = _repo.GetAssistance(assistanceId);
            CaseModel c = _caseRepo.GetCase(a.CasID);
            int familyCount = _caseRepo.GetFamilyMemberCount(a.CasID);

            // آموزش — طبق تصمیمِ کاربر: ReceiptNo/PrintedAt فقط در اولینِ
            // چاپِ واقعی (چاپ یا PDF) ثبت می‌شود، نه در بازِ کردنِ پیش‌نمایش.
            int receiptNo = commit
                ? _repo.EnsureReceiptNumberAssigned(assistanceId, SecurityContext.CenterFilterId)
                : (a.ReceiptNo ?? 0);
            bool isDraft = receiptNo == 0;

            return new AssistanceReceiptData
            {
                OrganizationName = SettingsHelper.Get(SettingsHelper.OrgName),
                Logo = SettingsHelper.Get(SettingsHelper.LogoPath),

                // آموزش — دقیقاً همان الگویِ CardService.CardNumber
                // (FormNo.ToString("D6"))، اینجا برای ReceiptNo. در حالتِ
                // پیش‌نمایشِ بدونِ شمارهٔ قطعی، کد/سریال خالی می‌ماند (بدونِ
                // بارکد ساختگی) تا با نسخهٔ چاپ‌شدهٔ نهایی اشتباه گرفته نشود.
                ReceiptCode = isDraft ? "" : ("AFG-" + SecurityContext.CurrentCenterCode + "-" + receiptNo.ToString("D6")),
                SerialNo = isDraft ? "" : ("SN-" + receiptNo.ToString("D6")),

                RecipientName = c.HeadFullName,
                FatherName = c.HeadFatherName,
                TazkiraNo = c.HeadTazkiraNo,
                Phone = c.Phone,

                // آموزش — برای کمکِ غیرنقدی، به‌جای مبلغ، خلاصهٔ اقلامِ همان
                // بستهٔ انتخاب‌شده نشان داده می‌شود (بستهٔ مساعدت، از تنظیمات).
                AidTypeAndAmount = a.PackageID.HasValue
                    ? (a.AssistanceType ?? "") + " — " + AssistancePackageRepository.FormatItemsSummary(_packageRepo.GetPackage(a.PackageID.Value))
                    : (a.AssistanceType ?? "") + " — " + a.Amount.ToString("N0") + " افغانی",
                DistributionDate = PersianDateHelper.ToPersianDateString(a.AssistanceDate),
                ProvinceDistrict = (c.Province ?? "") + " — " + (c.District ?? ""),
                FamilyMembersCount = familyCount + " نفر",
                ProgramName = a.ProgramName,
                // آموزش — طبق تأییدِ کاربر: RequestType همان ستونِ موجودِ
                // TblCase است، بدونِ هیچ ستون/نگاشتِ تازه.
                RequestType = c.RequestType,
                PickupLocation = a.PickupLocation,
                CoordinatorPhone = a.CoordinatorPhone,
                // آموزش — طبق تصمیمِ کاربر: فعلاً ستونی برای این وجود ندارد؛
                // خالی می‌ماند (بستهٔ فریزشده خودش جعبهٔ خالی/placeholder را نگه می‌دارد).
                DisplacedCardNo = "",

                // Photo/Barcode خام اینجا؛ Stage شدن (کپی به uploads/ + مسیر نسبی)
                // مسئولیتِ AssistanceReceiptRenderer است — دقیقاً مثلِ GuardianCardRenderer.
                Photo = c.PhotoPath,
                Barcode = ""
            };
        }

        // چاپ گروهیِ واقعی — شمارهٔ هر رکورد (اگر نبود) تخصیص و ذخیره می‌شود.
        public List<AssistanceReceiptData> BuildReceiptDataBatch(
            string province, string district, int formNo, string programName,
            System.DateTime? dateFrom, System.DateTime? dateTo, string assistanceType, bool? isPrinted,
            out int failedCount)
        {
            return BuildBatch(province, district, formNo, programName, dateFrom, dateTo, assistanceType, isPrinted, commit: true, out failedCount);
        }

        // پیش‌نمایشِ گروهی — بدونِ نوشتن در دیتابیس (نگاه کنید PreviewReceiptData).
        public List<AssistanceReceiptData> PreviewReceiptDataBatch(
            string province, string district, int formNo, string programName,
            System.DateTime? dateFrom, System.DateTime? dateTo, string assistanceType, bool? isPrinted,
            out int failedCount)
        {
            return BuildBatch(province, district, formNo, programName, dateFrom, dateTo, assistanceType, isPrinted, commit: false, out failedCount);
        }

        // نسخهٔ عمومی برای مجموعه‌ای از شناسه‌های از‌پیش‌تعیین‌شده (مثلاً از
        // AssistancePackageRepository.GetFilteredTable) — چاپِ گروهیِ بستهٔ
        // مساعدت از این استفاده می‌کند، نه از فیلترِ AssistanceReceiptRepository.
        public List<AssistanceReceiptData> BuildReceiptDataForIds(IEnumerable<int> assistanceIds, bool commit, out int failedCount)
        {
            failedCount = 0;
            var result = new List<AssistanceReceiptData>();
            foreach (int id in assistanceIds)
            {
                try { result.Add(Build(id, commit)); }
                catch { failedCount++; }
            }
            return result;
        }

        // چاپ گروهی — یک رکوردِ ناقص/خراب کلِ دسته را متوقف نمی‌کند.
        private List<AssistanceReceiptData> BuildBatch(
            string province, string district, int formNo, string programName,
            System.DateTime? dateFrom, System.DateTime? dateTo, string assistanceType, bool? isPrinted,
            bool commit, out int failedCount)
        {
            failedCount = 0;
            var result = new List<AssistanceReceiptData>();

            foreach (int assistanceId in _repo.GetAssistanceIdsByFilter(
                province, district, formNo, programName, dateFrom, dateTo, assistanceType, isPrinted))
            {
                try { result.Add(Build(assistanceId, commit)); }
                catch { failedCount++; }
            }

            return result;
        }
    }
}
