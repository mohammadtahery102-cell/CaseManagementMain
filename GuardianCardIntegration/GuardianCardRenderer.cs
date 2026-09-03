using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace CaseManagement.GuardianCardIntegration
{
    // ─────────────────────────────────────────────────────────────────────────
    // لایه یکپارچه‌سازی (Infrastructure) — تنها مسئولیتش «تحویل داده به بسته
    // فریز‌شده GuardianCard بدون دست‌زدن به آن» است.
    //
    // چرا یک پوشه کاری موقت؟ چون:
    //   ۱) قانون صریح: هیچ فایلی داخل پوشه GuardianCard تغییر نمی‌کند.
    //   ۲) خودِ مستندات GuardianCard (docs/README.md بخش ۳) دقیقاً همین روش را
    //      برای تولید کارت واقعی از C# پیشنهاد داده: «sample/SAMPLE_DATA.json را
    //      بازنویسی کن» — فقط ما این بازنویسی را روی یک کپی کاری انجام می‌دهیم،
    //      نه نسخه اصلی.
    //   ۳) guardian-card.js با fetch("sample/SAMPLE_DATA.json") کار می‌کند؛ طبق
    //      مستندات همان پروژه، fetch روی file:// در مرورگرهای Chromium (پایه
    //      WebView2) مسدود است و به داده fallback داخلی برمی‌گردد. راه‌حل
    //      مستندشدهٔ خودشان: «برای WebView2 از SetVirtualHostNameToFolderMapping
    //      استفاده کن» — این باعث می‌شود صفحه از مبدأ https://<virtual-host>
    //      بارگذاری شود، نه file://، و fetch واقعی (نه fallback) اجرا شود.
    //      این کلاس فقط فایل‌ها را آماده می‌کند؛ نگاشت virtual-host در
    //      FrmGuardianCardPreview (چون به CoreWebView2 نیاز دارد) انجام می‌شود.
    // ─────────────────────────────────────────────────────────────────────────
    public class GuardianCardRenderer
    {
        // نام هاست مجازی برای WebView2 (در FrmGuardianCardPreview استفاده می‌شود)
        public const string VirtualHostName = "guardiancard.local";

        // پوشه اصلیِ فریزشده — کنار exe دیپلوی می‌شود (Content در csproj)؛
        // هرگز نوشته/تغییر داده نمی‌شود.
        private static string BundledSourceFolder
        {
            get { return Path.Combine(Application.StartupPath, "GuardianCard"); }
        }

        // پوشه کاریِ یک‌بارمصرف — هر بار رندر یک کارت، از نو ساخته می‌شود تا هیچ
        // داده‌ای از کارت قبلی باقی نماند.
        private static string WorkingFolder
        {
            get { return Path.Combine(Path.GetTempPath(), "CaseManagement_GuardianCardWork"); }
        }

        public bool IsBundledPackagePresent()
        {
            return Directory.Exists(BundledSourceFolder) && File.Exists(Path.Combine(BundledSourceFolder, "index.html"));
        }

        // کپی کامل بسته + نوشتن JSON واقعی این پرونده + کپی تصاویر اختصاصی
        // (عکس سرپرست/لوگو مؤسسه/امضا/مهر) داخل پوشه کاری + ساخت بارکد واقعی
        // (Code128) از شماره کارت. خروجی: مسیر پوشه کاری (ریشه‌ای که
        // index.html در آن است) برای نگاشت virtual-host توسط فراخوان.
        public string StageAndPopulate(
            GuardianCardData data, string guardianPhotoPath, string orgLogoPath,
            string signaturePath = null, string stampPath = null, IEnumerable<string> disabledFields = null,
            CardTemplateDesign design = null, string layoutVariant = "Full")
        {
            if (data == null) throw new ArgumentNullException("data");
            EnsureBundledPackagePresent();

            if (Directory.Exists(WorkingFolder))
                Directory.Delete(WorkingFolder, true);
            CopyDirectory(BundledSourceFolder, WorkingFolder);

            string uploadsDir = Path.Combine(WorkingFolder, "uploads");
            Directory.CreateDirectory(uploadsDir);

            data.Photo = StageImage(guardianPhotoPath, uploadsDir, "guardian_photo");
            data.Logo = StageImage(orgLogoPath, uploadsDir, "org_logo");
            data.Signature = StageImage(signaturePath, uploadsDir, "signature");
            data.Stamp = StageImage(stampPath, uploadsDir, "stamp");
            data.Barcode = StageBarcode(BarcodeValue(data), uploadsDir, "barcode");
            data.QRCode = StageQr(BarcodeValue(data), uploadsDir, "qrcode");

            // آموزش — FamilyPhoto فقط برای قالبِ «ساده» است (index.html/
            // guardian-card.js کاری با آن ندارند). Orphans حالا هم روی
            // قالبِ کامل (فهرستِ اعضای خانواده) و هم قالبِ ساده استفاده
            // می‌شود. عکسِ خانواده و عکسِ هر عضو باید مثلِ عکسِ سرپرست Stage
            // شوند وگرنه مسیرِ خام/مطلق در JSON می‌ماند و در WebView2
            // بارگذاری نمی‌شود.
            data.FamilyPhoto = StageImage(data.FamilyPhoto, uploadsDir, "family_photo");

            // آموزش — سقفِ تعدادِ ردیفِ فهرستِ اعضا روی کارتِ کامل (اگر قالب
            // تنظیم کرده باشد)؛ باید قبل از Stage کردنِ عکس‌ها اعمال شود تا
            // عکسِ اعضایی که حذف می‌شوند بی‌جهت Stage نشوند.
            if (design != null && design.FamilyListMaxRows > 0 && data.Orphans.Count > design.FamilyListMaxRows)
                data.Orphans = data.Orphans.GetRange(0, design.FamilyListMaxRows);

            for (int i = 0; i < data.Orphans.Count; i++)
                data.Orphans[i].Photo = StageImage(data.Orphans[i].Photo, uploadsDir, "orphan_" + i);

            // آموزش — override محتوای متن‌های ثابتِ کارت (بسمه‌تعالی/موتو/
            // تیتر/پیام‌ها)، اگر قالب چیزی تنظیم کرده باشد؛ باید قبل از
            // نوشتنِ JSON اجرا شود.
            CardTemplateRepository.ApplyTextOverrides(data, design);

            string json = new JavaScriptSerializer().Serialize(data);
            File.WriteAllText(Path.Combine(WorkingFolder, "sample", "SAMPLE_DATA.json"), json, new UTF8Encoding(false));

            ApplyDisabledFieldCleanup(WorkingFolder, disabledFields);
            ApplyDisabledFieldCleanupSimple(WorkingFolder, disabledFields);
            ApplyDesignOverrides(WorkingFolder, uploadsDir, design, layoutVariant);
            ApplyFieldOrderOverrides(WorkingFolder, design, layoutVariant);
            ApplyFamilyNameWidthFix(WorkingFolder);

            return WorkingFolder;
        }

        // نسخه‌ی چاپ جمعی — همان بسته یک‌بار کپی می‌شود، لوگو/امضا/مهر (که
        // مؤسسه‌ای و مشترک بین همه کارت‌هاست) فقط یک‌بار Stage می‌شوند، اما هر
        // پرونده عکس و بارکد اختصاصی خودش را می‌گیرد (نام فایل با اندیس یکتا
        // می‌شود تا رونویسی نشود). خروجی JSON یک آرایه است؛ guardian-card.js
        // این حالت را با Array.isArray تشخیص می‌دهد و کارت را به تعداد آیتم‌ها
        // Clone می‌کند (نگاه کنید populateBatch در guardian-card.js).
        public string StageAndPopulateBatch(
            IList<GuardianCardData> items, Func<GuardianCardData, string> guardianPhotoPathSelector,
            string orgLogoPath, string signaturePath = null, string stampPath = null, IEnumerable<string> disabledFields = null,
            CardTemplateDesign design = null, string layoutVariant = "Full")
        {
            if (items == null) throw new ArgumentNullException("items");
            EnsureBundledPackagePresent();

            if (Directory.Exists(WorkingFolder))
                Directory.Delete(WorkingFolder, true);
            CopyDirectory(BundledSourceFolder, WorkingFolder);

            string uploadsDir = Path.Combine(WorkingFolder, "uploads");
            Directory.CreateDirectory(uploadsDir);

            string logoRel = StageImage(orgLogoPath, uploadsDir, "org_logo");
            string signatureRel = StageImage(signaturePath, uploadsDir, "signature");
            string stampRel = StageImage(stampPath, uploadsDir, "stamp");

            for (int i = 0; i < items.Count; i++)
            {
                GuardianCardData data = items[i];
                string photoPath = guardianPhotoPathSelector != null ? guardianPhotoPathSelector(data) : data.Photo;
                data.Photo = StageImage(photoPath, uploadsDir, "guardian_photo_" + i);
                data.Logo = logoRel;
                data.Signature = signatureRel;
                data.Stamp = stampRel;
                data.Barcode = StageBarcode(BarcodeValue(data), uploadsDir, "barcode_" + i);
                data.QRCode = StageQr(BarcodeValue(data), uploadsDir, "qrcode_" + i);

                // آموزش — عکسِ جمعیِ خانواده فقط برایِ قالبِ «ساده» است (نگاه
                // کنید توضیحِ همین خط در StageAndPopulate)؛ اینجا هم افتاده
                // بود — بدونش، چاپِ جمعیِ قالبِ ساده عکسِ جمعی را خراب نشان
                // می‌داد.
                data.FamilyPhoto = StageImage(data.FamilyPhoto, uploadsDir, "family_photo_" + i);

                // آموزش — رفعِ ناهماهنگیِ گزارش‌شده («تغییراتِ تنظیماتِ کارت در
                // چاپِ جمعی اعمال نمی‌شود»): این سه خط دقیقاً هم‌تراز با
                // StageAndPopulate (تک‌کارت) بودند ولی اینجا افتاده بودند —
                // سقفِ فهرستِ اعضا/Stage‌کردنِ عکسِ هر عضو/override محتوای
                // متن‌های ثابت (بسمه‌تعالی، موتو، تیتر، پیام‌ها، تماس/ایمیل)
                // باید per-item همینجا اجرا شوند، وگرنه فقط پیش‌نمایشِ
                // تک‌کارت این تنظیمات را می‌دید، نه خروجیِ دسته‌ای.
                if (design != null && design.FamilyListMaxRows > 0 && data.Orphans.Count > design.FamilyListMaxRows)
                    data.Orphans = data.Orphans.GetRange(0, design.FamilyListMaxRows);

                for (int m = 0; m < data.Orphans.Count; m++)
                    data.Orphans[m].Photo = StageImage(data.Orphans[m].Photo, uploadsDir, "orphan_" + i + "_" + m);

                CardTemplateRepository.ApplyTextOverrides(data, design);
            }

            string json = new JavaScriptSerializer().Serialize(items);
            File.WriteAllText(Path.Combine(WorkingFolder, "sample", "SAMPLE_DATA.json"), json, new UTF8Encoding(false));

            ApplyDisabledFieldCleanup(WorkingFolder, disabledFields);
            ApplyDisabledFieldCleanupSimple(WorkingFolder, disabledFields);
            ApplyDesignOverrides(WorkingFolder, uploadsDir, design, layoutVariant);
            ApplyFieldOrderOverrides(WorkingFolder, design, layoutVariant);
            ApplyFamilyNameWidthFix(WorkingFolder);

            return WorkingFolder;
        }

        // آموزش — رفعِ باگِ گزارش‌شده («خروجیِ قالب‌هایی که یک فیلدِ اختیاری را
        // خاموش می‌کنند»): بستهٔ GuardianCard برای حالتِ ویرایشِ تعاملی طراحی
        // شده، نه «فیلدِ کاملاً غایب» — وقتی مقدار خالی است، فقط چیزی نوشته
        // نمی‌شود؛ برچسبِ ثابتِ HTML (مثلاً «وبسایت: ») و placeholder ویرایشی
        // (مثلاً «or browse files») همچنان روی کارتِ چاپی/PDF می‌مانند. چون
        // تغییرِ خودِ پوشهٔ GuardianCard ممنوع است (نگاه کنید آموزشِ بالای این
        // فایل)، این متد فقط داخلِ WorkingFolder (کپیِ یک‌بارمصرفِ همین رندر)
        // دو چیز اضافه می‌کند: یک فایلِ اسکریپتِ ثابت (RemoveDisabledFieldsScript)
        // که بعد از رویدادِ «guardiancard:populated» (که guardian-card.js خودش
        // بعد از هر بار populateCard شلیک می‌کند — هم برای تک‌کارت هم برای هر
        // Clone در چاپ جمعی) ردیف/جایگاهِ کاملِ هر فیلدِ خاموش را از DOM حذف
        // می‌کند، و یک تگِ <script> کوچک که فهرستِ فیلدهای خاموش را *قبل* از
        // اجرای guardian-card.js تزریق می‌کند تا listener به‌موقع ثبت شود.
        // اگر هیچ فیلدی خاموش نباشد (مثلاً قالبِ پیش‌فرض)، هیچ فایلی نوشته/
        // تغییر داده نمی‌شود — رفتارِ قبلی دقیقاً حفظ می‌شود.
        // ─────────────────────────────────────────────────────────────────────
        // رفعِ باگِ گزارش‌شدهٔ کاربر: در جدولِ «اعضای خانواده» روی کارتِ کامل،
        // نام‌های بلند (مثلاً «سید حسین علی») با «…» بریده می‌شدند.
        //
        // ریشه در guardian-card.css است:
        //     .family-table td { white-space:nowrap; overflow:hidden;
        //                        text-overflow:ellipsis; max-width:0; }
        // یعنی هیچ سهمی از عرض به ستون‌ها داده نشده و همه با هم رقابت می‌کنند؛
        // `max-width:0` باعث می‌شود ستونِ نام به کوچک‌ترین اندازه جمع شود.
        //
        // چون تغییرِ پوشهٔ GuardianCard ممنوع است، اینجا فقط داخلِ کپیِ
        // یک‌بارمصرفِ همین رندر یک <style> تزریق می‌شود که سهمِ عرض را
        // بازتوزیع می‌کند: ستون‌های نام و نام پدر پهن‌تر، و دو ستونِ عددیِ
        // «تاریخ تولد» و «شماره تذکره» (که طولشان ثابت و قابل‌پیش‌بینی است)
        // باریک‌تر.
        //
        // چرا nth-last-child و نه nth-child؟ چون وقتی «عکسِ هر عضو»
        // (FamilyListPhotos) خاموش باشد، guardian-card.js اصلاً سلولِ عکس را
        // نمی‌سازد و شمارشِ ستون‌ها از ابتدا یکی جابه‌جا می‌شود؛ ولی از
        // انتها همیشه ثابت است: تذکره(۱)، تولد(۲)، نام پدر(۳)، نام(۴) —
        // در هر دو حالت.
        //
        // ارتفاعِ ردیف عمداً دست‌نخورده می‌ماند (nowrap حفظ شده) تا باگِ
        // شناخته‌شدهٔ سرریزِ ردیف‌ها از کادرِ ثابتِ کارت دوباره برنگردد —
        // نگاه کنید توضیحِ .family-table td.family-photo-col در همان CSS.
        // ─────────────────────────────────────────────────────────────────────
        private static void ApplyFamilyNameWidthFix(string workingFolder)
        {
            string indexPath = Path.Combine(workingFolder, "index.html");
            if (!File.Exists(indexPath)) return;

            string html = File.ReadAllText(indexPath);
            const string headAnchor = "</head>";
            if (html.IndexOf(headAnchor, StringComparison.Ordinal) < 0) return;

            var css = new StringBuilder();
            css.Append("<style>\n");
            css.Append("/* فضای بیشتر برای نامِ اعضای خانواده — تزریقِ CaseManagement */\n");
            // آموزش — سهم‌ها با مقایسهٔ بصریِ واقعی تنظیم شدند، نه با حدس:
            // در تلاشِ اول فقط «نام» پهن شد و «نام پدر» شروع به بریده‌شدن
            // کرد — یعنی مشکل صرفاً جابه‌جا شده بود. حالا هر دو ستونِ نام
            // سهمِ برابر و بزرگ می‌گیرند و در عوض دو ستونِ عددی (که طولشان
            // ثابت و کوتاه است) به حداقلِ لازم می‌رسند.
            // نام
            css.Append(".family-table th:nth-last-child(4),\n");
            css.Append(".family-table td:not(.family-empty):nth-last-child(4){width:32%;max-width:none;padding-inline:0.5mm;}\n");
            // نام پدر
            css.Append(".family-table th:nth-last-child(3),\n");
            css.Append(".family-table td:not(.family-empty):nth-last-child(3){width:35%;max-width:none;padding-inline:0.5mm;}\n");
            // سالِ تولد — فقط ۴ رقم چاپ می‌شود (نگاه کنید
            // CaseCardRepository.GetOrphans)، پس باریک‌ترین ستون است و
            // فضای آزادشده به «نام پدر» و «شماره تذکره» رسید.
            css.Append(".family-table th:nth-last-child(2),\n");
            css.Append(".family-table td:not(.family-empty):nth-last-child(2){width:9%;max-width:none;padding-inline:0.3mm;}\n");
            // شماره تذکره
            css.Append(".family-table th:nth-last-child(1),\n");
            css.Append(".family-table td:not(.family-empty):nth-last-child(1){width:24%;max-width:none;padding-inline:0.3mm;}\n");
            css.Append("</style>\n");
            css.Append(headAnchor);

            html = html.Replace(headAnchor, css.ToString());
            File.WriteAllText(indexPath, html, new UTF8Encoding(false));
        }

        private static void ApplyDisabledFieldCleanup(string workingFolder, IEnumerable<string> disabledFields)
        {
            var fields = new List<string>();
            if (disabledFields != null)
                foreach (string f in disabledFields)
                    if (!string.IsNullOrWhiteSpace(f))
                        fields.Add(f);

            if (fields.Count == 0) return;

            string jsDir = Path.Combine(workingFolder, "js");
            Directory.CreateDirectory(jsDir);
            File.WriteAllText(Path.Combine(jsDir, "template-overrides.js"), RemoveDisabledFieldsScript, new UTF8Encoding(false));

            string indexPath = Path.Combine(workingFolder, "index.html");
            if (!File.Exists(indexPath)) return;

            string html = File.ReadAllText(indexPath);
            const string anchor = "<script src=\"js/guardian-card.js\"></script>";
            if (html.IndexOf(anchor, StringComparison.Ordinal) < 0) return;

            var fieldList = new StringBuilder();
            for (int i = 0; i < fields.Count; i++)
            {
                if (i > 0) fieldList.Append(",");
                fieldList.Append("\"").Append(fields[i].Replace("\"", "")).Append("\"");
            }

            string injected =
                "<script>window.__cardTemplateDisabledFields = [" + fieldList + "];</script>\n" +
                "<script src=\"js/template-overrides.js\"></script>\n" +
                anchor;

            html = html.Replace(anchor, injected);
            File.WriteAllText(indexPath, html, new UTF8Encoding(false));
        }

        // آموزش — همتای ApplyDisabledFieldCleanup بالا، فقط برای simple.html
        // (قالبِ «ساده»). چون خودِ آن HTML را من نوشته‌ام (نه یک بستهٔ
        // خارجیِ ثابت با ساختارِ پیچیده)، هر عنصرِ قابل‌خاموشی از قبل یک
        // ویژگیِ data-simple-field="X" دارد، پس یک حلقهٔ عمومی کافی است —
        // برخلافِ سوییچِ دستیِ RemoveDisabledFieldsScript. رویداد/ویژگیِ
        // جدا (simplecard:populated / data-simple-field، نه data-field) تا
        // با قالبِ کامل تداخل نکند — دقیقاً همان باگی که در تلاشِ قبلیِ
        // کارتِ جدا رخ داد و باعثِ رونویسیِ مقادیر شد.
        private static void ApplyDisabledFieldCleanupSimple(string workingFolder, IEnumerable<string> disabledFields)
        {
            var fields = new List<string>();
            if (disabledFields != null)
                foreach (string f in disabledFields)
                    if (!string.IsNullOrWhiteSpace(f))
                        fields.Add(f);

            if (fields.Count == 0) return;

            string jsDir = Path.Combine(workingFolder, "js");
            Directory.CreateDirectory(jsDir);
            File.WriteAllText(Path.Combine(jsDir, "simple-overrides.js"), RemoveDisabledFieldsScriptSimple, new UTF8Encoding(false));

            string simplePath = Path.Combine(workingFolder, "simple.html");
            if (!File.Exists(simplePath)) return;

            string html = File.ReadAllText(simplePath);
            const string anchor = "<script src=\"js/simple.js\"></script>";
            if (html.IndexOf(anchor, StringComparison.Ordinal) < 0) return;

            var fieldList = new StringBuilder();
            for (int i = 0; i < fields.Count; i++)
            {
                if (i > 0) fieldList.Append(",");
                fieldList.Append("\"").Append(fields[i].Replace("\"", "")).Append("\"");
            }

            string injected =
                "<script>window.__simpleCardDisabledFields = [" + fieldList + "];</script>\n" +
                "<script src=\"js/simple-overrides.js\"></script>\n" +
                anchor;

            html = html.Replace(anchor, injected);
            File.WriteAllText(simplePath, html, new UTF8Encoding(false));
        }

        // آموزش — رنگ/فونت/پس‌زمینهٔ هر رو/واترمارک («Card Designer»، بخش
        // طراحِ سبک). دقیقاً همان اصلِ ApplyDisabledFieldCleanup: فقط داخلِ
        // WorkingFolder (کپیِ یک‌بارمصرف) تغییر می‌دهد، هرگز خودِ GuardianCard.
        //   • رنگ/فونت: یک <style> با override رویِ همان custom propertyهای
        //     :root که خودِ guardian-card.css تعریف کرده (--primary-color/
        //     --secondary-color/--font-family) — چون این <style> بعد از
        //     لینکِ استایلِ اصلی در <head> می‌آید، طبق قاعدهٔ cascade برنده
        //     می‌شود، بدونِ نیاز به !important.
        //   • پس‌زمینه: background-image رویِ .card-front/.card-back. آموزش —
        //     محدودیتِ صادقانه: چون پنل‌های داخلی (.panel) پس‌زمینهٔ مات دارند،
        //     این تصویر فقط در حاشیه‌ها/شکاف‌ها دیده می‌شود، نه زیرِ کل کارت
        //     (طرحِ کامل drag-and-drop نیست — دقیقاً طبقِ توافق).
        //   • واترمارک: یک <img> کم‌شفافیت که با اسکریپت (بعد از
        //     guardiancard:populated، مثلِ بقیه‌ی افزونه‌ها) به هر .card-trim
        //     اضافه می‌شود.
        private static void ApplyDesignOverrides(string workingFolder, string uploadsDir, CardTemplateDesign design, string layoutVariant = "Full")
        {
            if (design == null) return;

            bool hasColor = !string.IsNullOrWhiteSpace(design.PrimaryColor) || !string.IsNullOrWhiteSpace(design.SecondaryColor);
            bool hasFont = !string.IsNullOrWhiteSpace(design.FontFamily);
            // آموزش — به‌درخواستِ کاربر: پس‌زمینه/رنگِ متن/اندازهٔ فونت هم
            // اضافه شد. FontScalePercent همیشه یک مقدار دارد (پیش‌فرض ۱۰۰)
            // پس شرطش «فرق با ۱۰۰» است، نه «خالی نبودن» (مثل رشته‌ها).
            bool hasBackground = !string.IsNullOrWhiteSpace(design.BackgroundColor);
            bool hasTextColor = !string.IsNullOrWhiteSpace(design.TextColor);
            bool hasFontScale = design.FontScalePercent != 100;
            // آموزش — به‌درخواستِ کاربر: رنگِ پس‌زمینهٔ فقط نوارِ هدر، و
            // اندازهٔ عکسِ گردِ بالا-راست، مستقل از بقیهٔ رنگ‌ها/اندازه‌ها.
            bool hasHeaderBg = !string.IsNullOrWhiteSpace(design.HeaderBackgroundColor);
            bool hasPortraitScale = design.PortraitScalePercent != 100;
            // آموزش — به‌درخواستِ کاربر: ارتفاعِ نوارِ رنگیِ بالای کارت.
            bool hasHeaderHeight = design.HeaderHeightScalePercent != 100;
            // آموزش — ابعاد/اندازهٔ قابِ عکسِ جمعیِ خانواده — نگاه کنید
            // CardTemplateDesign.FamilyPhotoAspectRatio/FamilyPhotoScalePercent.
            bool hasFamilyPhotoSize = design.FamilyPhotoScalePercent != 100 ||
                (!string.IsNullOrWhiteSpace(design.FamilyPhotoAspectRatio) && design.FamilyPhotoAspectRatio != "1:1");
            // آموزش — به‌درخواستِ کاربر: «نمایشِ کاملِ عکسِ جمعی بدونِ برش».
            bool hasFamilyPhotoFit = design.FamilyPhotoFitContain;
            string bgFrontRel = StageImage(design.BackgroundFrontPath, uploadsDir, "bg_front");
            string bgBackRel = StageImage(design.BackgroundBackPath, uploadsDir, "bg_back");
            string watermarkRel = StageImage(design.WatermarkPath, uploadsDir, "watermark");

            string indexPath = Path.Combine(workingFolder, "index.html");
            if (!File.Exists(indexPath)) return;
            string html = File.ReadAllText(indexPath);

            // ─── ۱) رنگ/فونت/پس‌زمینه: <style> قبل از </head> ────────────────
            if (hasColor || hasFont || hasBackground || hasTextColor || hasFontScale || hasHeaderBg || hasPortraitScale || hasHeaderHeight || hasFamilyPhotoSize || hasFamilyPhotoFit || bgFrontRel.Length > 0 || bgBackRel.Length > 0)
            {
                var css = new StringBuilder();
                css.Append("<style>\n:root{\n");
                if (!string.IsNullOrWhiteSpace(design.PrimaryColor))
                    css.Append("  --primary-color: ").Append(design.PrimaryColor).Append(";\n")
                       .Append("  --primary-color-dark: ").Append(design.PrimaryColor).Append(";\n");
                if (!string.IsNullOrWhiteSpace(design.SecondaryColor))
                    css.Append("  --secondary-color: ").Append(design.SecondaryColor).Append(";\n")
                       .Append("  --accent-color: ").Append(design.SecondaryColor).Append(";\n");
                if (hasFont)
                    css.Append("  --font-family: \"").Append(design.FontFamily).Append("\", Tahoma, \"Segoe UI\", Arial, sans-serif;\n");
                if (hasBackground)
                    css.Append("  --surface-color: ").Append(design.BackgroundColor).Append(";\n")
                       .Append("  --surface-muted-color: ").Append(design.BackgroundColor).Append(";\n");
                if (hasTextColor)
                    css.Append("  --text-color: ").Append(design.TextColor).Append(";\n")
                       .Append("  --text-muted-color: ").Append(design.TextColor).Append(";\n")
                       .Append("  --text-faint-color: ").Append(design.TextColor).Append(";\n");
                if (hasFontScale)
                    css.Append("  --font-scale: ").Append((design.FontScalePercent / 100.0).ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(";\n");
                if (hasHeaderHeight)
                {
                    double headerScale = Math.Max(0.3, Math.Min(2.0, design.HeaderHeightScalePercent / 100.0));
                    css.Append("  --header-min-height: calc(22.4mm * ").Append(headerScale.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(");\n");
                }
                css.Append("}\n");
                if (bgFrontRel.Length > 0)
                    css.Append(".card-front{background-image:url('").Append(bgFrontRel).Append("');background-size:cover;background-position:center;}\n");
                if (bgBackRel.Length > 0)
                    css.Append(".card-back{background-image:url('").Append(bgBackRel).Append("');background-size:cover;background-position:center;}\n");
                if (hasHeaderBg)
                    css.Append(".card-header{background: ").Append(design.HeaderBackgroundColor).Append(";}\n");
                if (hasPortraitScale)
                {
                    double scale = Math.Max(0.5, Math.Min(3.0, design.PortraitScalePercent / 100.0));
                    string scaleStr = scale.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    css.Append(".portrait-medallion{width:calc(14mm * ").Append(scaleStr).Append(");height:calc(14mm * ").Append(scaleStr).Append(");}\n");
                }
                if (hasFamilyPhotoSize)
                {
                    // آموزش — ارتفاعِ پایه ثابت (۱۴mm، دقیقاً اندازهٔ پیش‌فرضِ
                    // جدیدِ .family-photo-slot در guardian-card.css)، عرض از
                    // رویِ نسبتِ ابعاد؛ سپس هر دو در مقیاس ضرب می‌شوند. به‌
                    // درخواستِ صریحِ کاربر، این قاب از داخلِ فهرستِ تنگِ
                    // اعضای خانواده به فریمِ بزرگ‌ترِ زیرِ تاریخِ صدور/انقضا
                    // منتقل شد (نگاه کنید index.html)، پس سقفِ مقیاس هم
                    // مثلِ Portrait به ۳۰۰٪ رسید (قبلاً ۲۵۰٪ بود، محدودیتِ
                    // فضایِ قبلی دیگر برقرار نیست).
                    double famScale = Math.Max(0.5, Math.Min(3.0, design.FamilyPhotoScalePercent / 100.0));
                    double ratio = ParseAspectRatio(design.FamilyPhotoAspectRatio);
                    double heightMm = 14.0 * famScale;
                    double widthMm = heightMm * ratio;
                    string wStr = widthMm.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                    string hStr = heightMm.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                    css.Append(".family-photo-slot{width:").Append(wStr).Append("mm;height:").Append(hStr).Append("mm;}\n");
                }
                // آموزش — به‌درخواستِ صریحِ کاربر: تیکِ جداگانه («نمایشِ کاملِ
                // عکسِ جمعی بدونِ برش») چون بعضی قالب‌ها همان برشِ cover
                // فعلی را می‌خواهند — نگاه کنید CardTemplateDesign.
                // FamilyPhotoFitContain بالا.
                if (hasFamilyPhotoFit)
                    css.Append(".family-photo-slot img{object-fit:contain;}\n");
                css.Append("</style>\n</head>");

                const string headAnchor = "</head>";
                if (html.IndexOf(headAnchor, StringComparison.Ordinal) >= 0)
                    html = html.Replace(headAnchor, css.ToString());
            }

            // ─── ۲) واترمارک: اسکریپتِ افزودنِ <img> بعد از populated ────────
            if (watermarkRel.Length > 0)
            {
                string jsDir = Path.Combine(workingFolder, "js");
                Directory.CreateDirectory(jsDir);
                double opacity = Math.Max(0, Math.Min(100, design.WatermarkOpacityPercent)) / 100.0;
                string watermarkScript = BuildWatermarkScript(watermarkRel, opacity);
                File.WriteAllText(Path.Combine(jsDir, "watermark-overrides.js"), watermarkScript, new UTF8Encoding(false));

                const string scriptAnchor = "<script src=\"js/guardian-card.js\"></script>";
                if (html.IndexOf(scriptAnchor, StringComparison.Ordinal) >= 0)
                {
                    string injected = "<script src=\"js/watermark-overrides.js\"></script>\n" + scriptAnchor;
                    html = html.Replace(scriptAnchor, injected);
                }
            }

            // ─── ۳) عکسِ گردِ خالی + رنگ/سایزِ متن‌های خاص: یک اسکریپت ──────
            string overrideScript = BuildTextOverrideScript(design);
            if (overrideScript != null)
            {
                string jsDir = Path.Combine(workingFolder, "js");
                Directory.CreateDirectory(jsDir);
                File.WriteAllText(Path.Combine(jsDir, "text-overrides.js"), overrideScript, new UTF8Encoding(false));

                const string scriptAnchor2 = "<script src=\"js/guardian-card.js\"></script>";
                if (html.IndexOf(scriptAnchor2, StringComparison.Ordinal) >= 0)
                {
                    string injected = "<script src=\"js/text-overrides.js\"></script>\n" + scriptAnchor2;
                    html = html.Replace(scriptAnchor2, injected);
                }
            }

            File.WriteAllText(indexPath, html, new UTF8Encoding(false));

            // ─── ۴) هماهنگیِ قالبِ «ساده» (Card Designer Phase 1) ────────────
            // آموزش — رفعِ ناهماهنگیِ گزارش‌شده («تنظیماتِ Card Designer روی
            // قالبِ ساده اعمال نمی‌شود»): تا اینجا هرچه بالا رفت فقط
            // index.html را نوشت — حتی وقتی layoutVariant واقعاً «ساده» بود
            // و اصلاً index.html چاپ نمی‌شود. این بلوک همان override را با
            // سلکتورهای معادلِ simple.html دوباره می‌نویسد، فقط وقتی قالب
            // واقعاً ساده است؛ برای قالبِ کامل هیچ کاری اضافه نمی‌کند.
            if (layoutVariant == "Simple")
            {
                string simplePath = Path.Combine(workingFolder, "simple.html");
                if (File.Exists(simplePath))
                {
                    string simpleHtml = File.ReadAllText(simplePath);

                    if (hasColor || hasFont || hasBackground || hasTextColor || hasFontScale || hasHeaderHeight || hasFamilyPhotoFit || bgFrontRel.Length > 0 || bgBackRel.Length > 0)
                    {
                        var simpleCss = new StringBuilder();
                        simpleCss.Append("<style>\n:root{\n");
                        if (!string.IsNullOrWhiteSpace(design.PrimaryColor))
                            simpleCss.Append("  --primary-color: ").Append(design.PrimaryColor).Append(";\n")
                                     .Append("  --primary-color-dark: ").Append(design.PrimaryColor).Append(";\n");
                        if (!string.IsNullOrWhiteSpace(design.SecondaryColor))
                            simpleCss.Append("  --secondary-color: ").Append(design.SecondaryColor).Append(";\n")
                                     .Append("  --accent-color: ").Append(design.SecondaryColor).Append(";\n");
                        if (hasFont)
                            simpleCss.Append("  --font-family: \"").Append(design.FontFamily).Append("\", Tahoma, \"Segoe UI\", Arial, sans-serif;\n");
                        if (hasBackground)
                            simpleCss.Append("  --surface-color: ").Append(design.BackgroundColor).Append(";\n")
                                     .Append("  --surface-muted-color: ").Append(design.BackgroundColor).Append(";\n");
                        if (hasTextColor)
                            simpleCss.Append("  --text-color: ").Append(design.TextColor).Append(";\n")
                                     .Append("  --text-muted-color: ").Append(design.TextColor).Append(";\n")
                                     .Append("  --text-faint-color: ").Append(design.TextColor).Append(";\n");
                        if (hasFontScale)
                            simpleCss.Append("  --font-scale: ").Append((design.FontScalePercent / 100.0).ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(";\n");
                        if (hasHeaderHeight)
                        {
                            double headerScale = Math.Max(0.3, Math.Min(2.0, design.HeaderHeightScalePercent / 100.0));
                            simpleCss.Append("  --header-min-height: calc(22.4mm * ").Append(headerScale.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(");\n");
                        }
                        simpleCss.Append("}\n");
                        // آموزش — .simple-page معادلِ .card-front برایِ برگهٔ اولِ
                        // قالبِ ساده است؛ .card-back عیناً همان کلاسِ قالبِ کامل
                        // است چون برگهٔ دومِ simple.html همان بخشِ کپی‌شده از
                        // index.html است (نگاه کنید simple.html).
                        if (bgFrontRel.Length > 0)
                            simpleCss.Append(".simple-page{background-image:url('").Append(bgFrontRel).Append("');background-size:cover;background-position:center;}\n");
                        if (bgBackRel.Length > 0)
                            simpleCss.Append(".card-back{background-image:url('").Append(bgBackRel).Append("');background-size:cover;background-position:center;}\n");
                        if (hasFamilyPhotoFit)
                            simpleCss.Append(".simple-photo-slot img{object-fit:contain;}\n");
                        simpleCss.Append("</style>\n</head>");

                        const string simpleHeadAnchor = "</head>";
                        if (simpleHtml.IndexOf(simpleHeadAnchor, StringComparison.Ordinal) >= 0)
                            simpleHtml = simpleHtml.Replace(simpleHeadAnchor, simpleCss.ToString());
                    }

                    // واترمارک — همان فایلِ js/watermark-overrides.js که بالا (اگر
                    // watermarkRel خالی نبود) نوشته شد؛ اینجا فقط ارجاعش از
                    // simple.js اضافه می‌شود، بدونِ بازنویسیِ خودِ فایل.
                    if (watermarkRel.Length > 0)
                    {
                        const string simpleWatermarkAnchor = "<script src=\"js/simple.js\"></script>";
                        if (simpleHtml.IndexOf(simpleWatermarkAnchor, StringComparison.Ordinal) >= 0)
                        {
                            string injected2 = "<script src=\"js/watermark-overrides.js\"></script>\n" + simpleWatermarkAnchor;
                            simpleHtml = simpleHtml.Replace(simpleWatermarkAnchor, injected2);
                        }
                    }

                    string simpleOverrideScript = BuildTextOverrideScriptSimple(design);
                    if (simpleOverrideScript != null)
                    {
                        string jsDir = Path.Combine(workingFolder, "js");
                        Directory.CreateDirectory(jsDir);
                        File.WriteAllText(Path.Combine(jsDir, "text-overrides-simple.js"), simpleOverrideScript, new UTF8Encoding(false));

                        const string simpleTextAnchor = "<script src=\"js/simple.js\"></script>";
                        if (simpleHtml.IndexOf(simpleTextAnchor, StringComparison.Ordinal) >= 0)
                        {
                            string injected3 = "<script src=\"js/text-overrides-simple.js\"></script>\n" + simpleTextAnchor;
                            simpleHtml = simpleHtml.Replace(simpleTextAnchor, injected3);
                        }
                    }

                    File.WriteAllText(simplePath, simpleHtml, new UTF8Encoding(false));
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // آموزش — Card Designer Phase 1، «ترتیبِ واقعیِ فیلدها»: تزریقِ CSS
        // order در پوشهٔ کاری. هر سه ظرف (dl.field-list، security-band،
        // ستون‌های simple-body) از قبل flex/grid هستند (نگاه کنید
        // guardian-card.css/simple.css) — پس فقط نوشتنِ
        // [data-order-field="X"]{order:N} کافی است، بدونِ هیچ تغییرِ
        // ساختاری/CSSِ دیگر. هیچ رویداد/JS لازم نیست (برخلافِ متن‌ها) چون
        // order یک ویژگیِ خالصِ CSS است، همیشه (نه فقط بعدِ populated).
        //
        // نکتهٔ کلیدی برایِ درستیِ چاپِ جمعی/تک‌کارت هردو: برایِ هر گروه،
        // برایِ *همهٔ* عضوهای آن گروه (نه فقط فیلدهایی که کاربر ترتیبشان
        // داده) باید یک order صریح نوشته شود؛ وگرنه عضوهایی که کاربر لمس
        // نکرده (مثلاً OrphansCount، یا ردیف‌های غیرقابل‌ترتیبِ قالبِ ساده)
        // با order پیش‌فرضِ ۰ به‌جای موقعیتِ طبیعیِ خودشان به ابتدای لیست
        // می‌پرند — نگاه کنید BuildFinalOrder پایین.
        //
        // اگر هیچ‌کدام از سه تنظیم خالی نباشد، هیچ فایلی نوشته/تغییر داده
        // نمی‌شود — رفتارِ پیش‌فرض (بدونِ تنظیم) دقیقاً همان چیدمانِ فعلی
        // می‌ماند، چون CSS order بدونِ قاعده همیشه ۰ (= ترتیبِ منبع) است.
        private static void ApplyFieldOrderOverrides(string workingFolder, CardTemplateDesign design, string layoutVariant)
        {
            if (design == null) return;

            bool hasFieldOrder = !string.IsNullOrWhiteSpace(design.FieldOrderCsv);
            bool hasPhotoPos = design.PhotoPosition == "After";
            bool hasBandOrder = !string.IsNullOrWhiteSpace(design.SecurityBandOrderCsv);
            if (!hasFieldOrder && !hasPhotoPos && !hasBandOrder) return;

            bool isSimple = layoutVariant == "Simple";
            var css = new StringBuilder();
            css.Append("<style>\n");

            if (hasFieldOrder)
            {
                string[] allowedKeys = isSimple ? CardTemplateRepository.FieldOrderableKeysSimple : CardTemplateRepository.FieldOrderableKeys;
                List<string> customOrder = CardTemplateRepository.ParseFieldOrder(design.FieldOrderCsv, allowedKeys);
                if (customOrder.Count > 0)
                {
                    string[] naturalOrder = isSimple
                        ? new[] { "CaseNo", "Province", "District", "Phone", "NationalID", "GuardianName", "FatherName", "RelationshipToFamily" }
                        : new[] { "PublicCode", "GuardianName", "FatherName", "NationalID", "RequestType", "OrphansCount" };
                    AppendOrderRules(css, naturalOrder, customOrder);
                }
            }

            if (hasPhotoPos)
            {
                if (isSimple)
                {
                    // آموزش — سه‌ستونیِ .simple-body (عکس/فیلدها/ایتام)؛ «بعد از
                    // فیلدها» یعنی عکس و فیلدها جا عوض کنند. ستونِ ایتام همیشه
                    // صریحاً پین می‌شود تا با order پیش‌فرضِ ۰ به ابتدا نپرد.
                    css.Append("[data-order-field=\"FieldsColumn\"]{order:1;}\n");
                    css.Append("[data-order-field=\"PhotoColumn\"]{order:2;}\n");
                    css.Append("[data-order-field=\"OrphansColumn\"]{order:3;}\n");
                }
                else
                {
                    css.Append("[data-order-field=\"FieldsGroup\"]{order:1;}\n");
                    css.Append("[data-order-field=\"Photo\"]{order:2;}\n");
                }
            }

            if (hasBandOrder && !isSimple)
            {
                List<string> customBand = CardTemplateRepository.ParseFieldOrder(design.SecurityBandOrderCsv, CardTemplateRepository.SecurityBandOrderableKeys);
                if (customBand.Count > 0)
                {
                    string[] naturalBand = { "QRCode", "Barcode", "Signature", "Stamp", "Hologram" };
                    List<string> finalBand = BuildFinalOrder(naturalBand, customBand);
                    // آموزش — سلول‌ها اندیسِ فرد می‌گیرند (۱،۳،۵،۷،۹) تا جایِ
                    // خالی برایِ ۴ دیوایدرِ تزئینیِ بینِ‌شان (زوج: ۲،۴،۶،۸)
                    // بماند. خودِ دیوایدرها بدونِ دیتا/محتوا هستند — این‌که
                    // کدام دیوایدرِ DOM کدام شکاف را می‌گیرد اهمیتی ندارد،
                    // فقط باید ۴تا باشند که همیشه هستند.
                    for (int i = 0; i < finalBand.Count; i++)
                        css.Append("[data-order-field=\"").Append(finalBand[i]).Append("\"]{order:").Append(i * 2 + 1).Append(";}\n");
                    for (int d = 1; d <= 4; d++)
                        css.Append("[data-order-field=\"Divider").Append(d).Append("\"]{order:").Append(d * 2).Append(";}\n");
                }
            }

            css.Append("</style>\n</head>");
            string cssBlock = css.ToString();

            string indexPath = Path.Combine(workingFolder, "index.html");
            if (File.Exists(indexPath))
            {
                string html = File.ReadAllText(indexPath);
                const string headAnchor = "</head>";
                if (html.IndexOf(headAnchor, StringComparison.Ordinal) >= 0)
                {
                    html = html.Replace(headAnchor, cssBlock);
                    File.WriteAllText(indexPath, html, new UTF8Encoding(false));
                }
            }

            if (isSimple)
            {
                string simplePath = Path.Combine(workingFolder, "simple.html");
                if (File.Exists(simplePath))
                {
                    string simpleHtml = File.ReadAllText(simplePath);
                    const string simpleHeadAnchor = "</head>";
                    if (simpleHtml.IndexOf(simpleHeadAnchor, StringComparison.Ordinal) >= 0)
                    {
                        simpleHtml = simpleHtml.Replace(simpleHeadAnchor, cssBlock);
                        File.WriteAllText(simplePath, simpleHtml, new UTF8Encoding(false));
                    }
                }
            }
        }

        // آموزش — فهرستِ نهاییِ ترتیب: اول کلیدهایی که کاربر صریحاً ترتیب
        // داده (به همان ترتیب)، سپس بقیهٔ کلیدهایی که در naturalOrder
        // هستند ولی کاربر لمسشان نکرده — به همان ترتیبِ طبیعیِ خودشان.
        // این تضمین می‌کند فیلدهایِ لمس‌نشده همیشه یک order صریح بگیرند
        // (نه پیش‌فرضِ ۰) و جایشان عوض نشود.
        private static List<string> BuildFinalOrder(string[] naturalOrder, List<string> customOrder)
        {
            var result = new List<string>(customOrder);
            foreach (string key in naturalOrder)
                if (!result.Contains(key))
                    result.Add(key);
            return result;
        }

        private static void AppendOrderRules(StringBuilder css, string[] naturalOrder, List<string> customOrder)
        {
            List<string> finalOrder = BuildFinalOrder(naturalOrder, customOrder);
            for (int i = 0; i < finalOrder.Count; i++)
                css.Append("[data-order-field=\"").Append(finalOrder[i]).Append("\"]{order:").Append(i + 1).Append(";}\n");
        }

        // آموزش — دو چیزِ جداگانه که هر دو باید *بعدِ* پرشدنِ داده (چون
        // getComputedStyle به مقدارِ نهاییِ فونت نیاز دارد) با جاوااسکریپت
        // انجام شوند، نه CSS ساده:
        //   ۱) PortraitBlank: عکسِ پیش‌فرضِ تزئینی پاک شود (فقط یک قابِ خالی
        //      بماند) — چون index.html خودش یک src ثابت دارد که فقط با JS
        //      قابلِ پاک‌کردن است.
        //   ۲) TextOverrides.Color/FontSizePercent: چون سایزِ پایه‌ی هر فیلد
        //      در خودِ CSS (به‌صورتِ pt) نوشته شده، برای «ضربدرِ Y درصد»
        //      باید اندازهٔ محاسبه‌شدهٔ فعلی (که خودش از قبل با --font-scale
        //      سراسری ضرب شده) را خواند و دوباره ضرب کرد — این کار را فقط
        //      JS می‌تواند بکند.
        private static string BuildTextOverrideScript(CardTemplateDesign design)
        {
            return BuildTextOverrideScriptCore(design, "data-field", "guardiancard:populated");
        }

        // آموزش — رفعِ ناهماهنگیِ گزارش‌شده («Card Designer روی قالبِ ساده
        // اعمال نمی‌شود»): همتای بالا برای simple.html — فیلدهای خودِ آن
        // صفحه با data-simple-value (نه data-field) علامت‌گذاری شده‌اند و
        // رویدادِ خودشان simplecard:populated است (نه guardiancard:
        // populated؛ نگاه کنید simple.js). منطق عیناً همان است — همان
        // هستهٔ مشترکِ BuildTextOverrideScriptCore — تا فیکسِ idempotentِ
        // اندازهٔ فونت (چاپِ جمعی) اینجا هم دقیقاً برقرار بماند، نه یک
        // کپیِ جداگانه که ممکن است از آن عقب بیفتد. اگر فیلدی در قالبِ
        // ساده اصلاً وجود نداشته باشد (مثلاً OrganizationName)، سلکتور
        // چیزی پیدا نمی‌کند و بی‌ضرر nothing انجام می‌شود.
        private static string BuildTextOverrideScriptSimple(CardTemplateDesign design)
        {
            return BuildTextOverrideScriptCore(design, "data-simple-value", "simplecard:populated");
        }

        private static string BuildTextOverrideScriptCore(CardTemplateDesign design, string selectorAttr, string populatedEvent)
        {
            bool hasPortraitBlank = design.PortraitBlank;
            var styleEntries = new List<string>();
            if (design.TextOverrides != null)
            {
                foreach (var kv in design.TextOverrides)
                {
                    if (kv.Value == null) continue;
                    bool hasColor = !string.IsNullOrWhiteSpace(kv.Value.Color);
                    bool hasScale = kv.Value.FontSizePercent != 100 && kv.Value.FontSizePercent > 0;
                    bool hasFont = !string.IsNullOrWhiteSpace(kv.Value.FontFamily);
                    bool hasLineHeight = kv.Value.LineHeightPercent != 100 && kv.Value.LineHeightPercent > 0;
                    // آموزش — Card Designer Phase 1: تراز و وزنِ فونت، دقیقاً
                    // هم‌الگویِ رنگ/فونت‌فمیلی/فاصلهٔ‌خط بالا — انتساب مطلق
                    // (نه خواندن-و-ضرب)، پس idempotent است و در چاپِ جمعی
                    // (شلیکِ تکراریِ رویداد) هرگز تصاعدی نمی‌شود.
                    bool hasAlignment = !string.IsNullOrWhiteSpace(kv.Value.Alignment);
                    bool hasWeight = !string.IsNullOrWhiteSpace(kv.Value.FontWeight);
                    if (!hasColor && !hasScale && !hasFont && !hasLineHeight && !hasAlignment && !hasWeight) continue;

                    string field = kv.Key.Replace("\"", "").Replace("\\", "");
                    string colorJs = hasColor ? "\"" + kv.Value.Color.Replace("\"", "") + "\"" : "null";
                    string scaleJs = hasScale ? (kv.Value.FontSizePercent / 100.0).ToString(System.Globalization.CultureInfo.InvariantCulture) : "null";
                    string fontJs = hasFont ? "\"" + kv.Value.FontFamily.Replace("\"", "") + "\"" : "null";
                    string lineHeightJs = hasLineHeight ? (kv.Value.LineHeightPercent / 100.0).ToString(System.Globalization.CultureInfo.InvariantCulture) : "null";
                    string alignJs = hasAlignment ? "\"" + kv.Value.Alignment.Replace("\"", "") + "\"" : "null";
                    string weightJs = hasWeight ? "\"" + kv.Value.FontWeight.Replace("\"", "") + "\"" : "null";
                    styleEntries.Add("    applyOne(\"" + field + "\", " + colorJs + ", " + scaleJs + ", " + fontJs + ", " + lineHeightJs + ", " + alignJs + ", " + weightJs + ");");
                }
            }

            if (!hasPortraitBlank && styleEntries.Count == 0) return null;

            var sb = new StringBuilder();
            sb.Append(@"(function () {
  ""use strict"";
  function apply() {
");
            if (hasPortraitBlank)
            {
                // آموزش — رفعِ همان باگِ کلاسِ «چاپِ جمعی/فقط کارتِ اول»: قبلاً با
                // querySelector مفرد فقط اولین img[Portrait] در کلِ سند پاک
                // می‌شد؛ چون apply() به‌ازای هر Clone دوباره اجرا می‌شود،
                // querySelectorAll + حلقه لازم است تا همهٔ Cloneها را بگیرد.
                sb.Append(@"    var portraits = document.querySelectorAll('img[" + selectorAttr + @"=""Portrait""]');
    for (var p = 0; p < portraits.length; p++) portraits[p].removeAttribute(""src"");
");
            }
            if (styleEntries.Count > 0)
            {
                sb.Append(@"    function applyOne(field, color, scale, fontFamily, lineHeight, textAlign, fontWeight) {
      var els = document.querySelectorAll('[" + selectorAttr + @"=""' + field + '""]');
      for (var i = 0; i < els.length; i++) {
        var el = els[i];
        if (color) el.style.color = color;
        if (scale) {
          // آموزش — رفعِ باگِ «چاپِ جمعی»: چون " + populatedEvent + @" به‌ازای
          // هر کارتِ Clone شده دوباره روی کلِ document شلیک می‌شود (نگاه کنید
          // GuardianCardRenderer بالا)، این تابع بارها روی همان فیلدهای
          // قبلاً-مقیاس‌شده هم اجرا می‌شود. قبلاً هر بار basePx را از
          // getComputedStyle می‌خواند — که خودش نتیجهٔ اجرای قبلی بود — پس
          // مقیاس هر بار روی مقدارِ از قبل بزرگ‌شده دوباره ضرب می‌شد و در
          // چاپِ جمعی (ده‌ها بار شلیک) تصاعدی منفجر می‌شد. حالا مقدارِ پایهٔ
          // اصلی فقط یک‌بار در data-base-font-size کش می‌شود و همیشه از
          // همان مبنا ضرب می‌شود — idempotent، صرف‌نظر از تعداد دفعاتِ اجرا.
          var baseAttr = el.getAttribute(""data-base-font-size"");
          var basePx = baseAttr ? parseFloat(baseAttr) : parseFloat(getComputedStyle(el).fontSize);
          if (!isNaN(basePx)) {
            if (!baseAttr) el.setAttribute(""data-base-font-size"", String(basePx));
            el.style.fontSize = (basePx * scale) + ""px"";
          }
        }
        if (fontFamily) el.style.fontFamily = fontFamily + "", var(--font-family)"";
        if (lineHeight) el.style.lineHeight = String(lineHeight);
        // آموزش — انتسابِ مطلق (نه خواندن-و-ضرب مثلِ scale بالا)، پس
        // اجرای تکراری (چاپِ جمعی) هرگز مقدار را عوض نمی‌کند.
        if (textAlign) el.style.textAlign = textAlign;
        if (fontWeight) el.style.fontWeight = fontWeight;
      }
    }
");
                sb.Append(string.Join("\n", styleEntries)).Append("\n");
            }
            sb.Append(@"  }
  document.addEventListener(""" + populatedEvent + @""", apply);
})();
");
            return sb.ToString();
        }

        // آموزش — "W:H" → W/H به‌صورتِ عدد؛ هر ورودیِ نامعتبر/خالی بی‌صدا
        // به مربعِ پیش‌فرض (۱:۱) برمی‌گردد — هرگز استثنا نمی‌دهد (این تابع
        // از داده‌ای که کاربر در UI انتخاب می‌کند می‌آید، نه ورودیِ آزادِ
        // متنی، ولی محافظه‌کاری اینجا رایگان است).
        private static double ParseAspectRatio(string ratio)
        {
            if (!string.IsNullOrWhiteSpace(ratio))
            {
                string[] parts = ratio.Split(':');
                if (parts.Length == 2)
                {
                    double w, h;
                    if (double.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out w) &&
                        double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out h) &&
                        w > 0 && h > 0)
                        return w / h;
                }
            }
            return 1.0;
        }

        private static string BuildWatermarkScript(string watermarkRelPath, double opacity)
        {
            return @"(function () {
  ""use strict"";
  function addWatermark(trim) {
    if (!trim || trim.querySelector("".__watermark"")) return;
    var img = document.createElement(""img"");
    img.src = """ + watermarkRelPath.Replace("\"", "") + @""";
    img.className = ""__watermark"";
    img.style.position = ""absolute"";
    img.style.inset = ""0"";
    img.style.width = ""100%"";
    img.style.height = ""100%"";
    img.style.objectFit = ""contain"";
    img.style.opacity = """ + opacity.ToString(System.Globalization.CultureInfo.InvariantCulture) + @""";
    img.style.pointerEvents = ""none"";
    img.style.zIndex = ""1"";
    trim.style.position = trim.style.position || ""relative"";
    trim.appendChild(img);
  }
  function apply() {
    // آموزش — .simple-trim معادلِ .card-trim برایِ برگهٔ اولِ قالبِ ساده
    // است (برگهٔ دومِ آن همان .card-trim را دارد چون عیناً کپیِ قالبِ
    // کامل است)؛ همین یک سلکتورِ ترکیبی برایِ هر دو قالب کافی است — روی
    // قالبِ کامل چیزِ اضافه‌ای پیدا نمی‌کند، بی‌ضرر.
    var trims = document.querySelectorAll("".card-trim, .simple-trim"");
    for (var i = 0; i < trims.length; i++) addWatermark(trims[i]);
  }
  document.addEventListener(""guardiancard:populated"", apply);
})();
";
        }

        // آموزش — هر بخش دقیقاً نظیرِ یک فیلدِ Toggleable در
        // CardTemplateRepository.ToggleableFields است. چون چاپِ جمعی هر
        // کارت را با cloneNode تکثیر می‌کند (نگاه کنید populateBatch در
        // guardian-card.js) و رویدادِ «guardiancard:populated» به‌ازای هر
        // Clone دوباره روی document شلیک می‌شود، cleanup() با querySelectorAll
        // (نه querySelector) روی کلِ document اجرا می‌شود تا همهٔ Cloneها را
        // بگیرد؛ idempotent است (اجرای دوباره روی چیزی که قبلاً مخفی شده
        // بی‌ضرر است)، پس شلیکِ تکراریِ رویداد در چاپ جمعی مشکلی ایجاد نمی‌کند.
        private const string RemoveDisabledFieldsScript = @"(function () {
  ""use strict"";
  var disabled = window.__cardTemplateDisabledFields;
  if (!disabled || !disabled.length) return;
  var disabledSet = {};
  for (var i = 0; i < disabled.length; i++) disabledSet[disabled[i]] = true;

  function hide(el) { if (el) el.style.display = ""none""; }
  function isHidden(el) { return !el || el.style.display === ""none""; }

  function collapseIfEmpty(container) {
    if (!container) return;
    var children = container.children;
    for (var i = 0; i < children.length; i++) {
      if (!isHidden(children[i])) return;
    }
    hide(container);
  }

  function hideDividerBefore(el) {
    if (!el) return;
    var prev = el.previousElementSibling;
    if (prev && prev.classList.contains(""divider"")) hide(prev);
  }

  // آموزش — رفعِ باگِ حیاتیِ گزارش‌شده («چاپِ جمعی از صفحهٔ دوم به بعد
  // فیلدهای خاموش‌شدهٔ قالب را دوباره نشان می‌دهد»): این توابع قبلاً با
  // document.querySelector (مفرد) فقط اولینِ آن سلکتور را در کلِ سند
  // مخفی می‌کردند. چون cleanup() به‌ازای هر Clone در چاپِ جمعی دوباره
  // اجرا می‌شود، فقط کارتِ اول (که هنگامِ اجرایِ اولِ رویداد تنها Cloneِ
  // موجود بود) واقعاً تمیز می‌شد؛ Cloneهای بعدی هرگز لمس نمی‌شدند.
  // querySelectorAll + حلقه، دقیقاً هم‌الگویِ removeSimpleRow/removeIssuerPart/
  // removeImageSlot بالا، این را روی همهٔ Cloneها اعمال می‌کند.
  function hideAll(selector) {
    var els = document.querySelectorAll(selector);
    for (var i = 0; i < els.length; i++) hide(els[i]);
  }

  function removeSimpleRow(field, rowSelector) {
    var values = document.querySelectorAll('[data-field=""' + field + '""]');
    for (var i = 0; i < values.length; i++) {
      var row = values[i].closest(rowSelector);
      hide(row || values[i]);
    }
  }

  function removeIssuerPart(field) {
    var values = document.querySelectorAll('[data-field=""' + field + '""]');
    for (var i = 0; i < values.length; i++) {
      var part = values[i].parentElement;
      if (!part) continue;
      hide(part);
      var dot = part.nextElementSibling;
      if (dot && dot.classList.contains(""dot"")) hide(dot);
      else {
        dot = part.previousElementSibling;
        if (dot && dot.classList.contains(""dot"")) hide(dot);
      }
      collapseIfEmpty(part.parentElement);
    }
  }

  function removeImageSlot(field, slotSelector) {
    var imgs = document.querySelectorAll('img[data-field=""' + field + '""]');
    var slots = [];
    for (var i = 0; i < imgs.length; i++) {
      var slot = imgs[i].closest(slotSelector);
      hide(slot || imgs[i]);
      slots.push(slot);
    }
    return slots;
  }

  function cleanup() {
    if (disabledSet.PublicCode) removeSimpleRow(""PublicCode"", "".field-row"");
    if (disabledSet.Website) removeSimpleRow(""Website"", "".contact-row"");
    if (disabledSet.Email) removeSimpleRow(""Email"", "".contact-row"");
    if (disabledSet.IssuedBy) removeIssuerPart(""IssuedBy"");
    if (disabledSet.Position) removeIssuerPart(""Position"");
    if (disabledSet.Logo) removeImageSlot(""Logo"", "".emblem"");

    if (disabledSet.Signature) {
      var sigSlots = removeImageSlot(""Signature"", "".upload-slot"");
      for (var i = 0; i < sigSlots.length; i++) {
        var cell = sigSlots[i] ? sigSlots[i].closest("".cell"") : null;
        if (!cell) continue;
        hide(cell.querySelector("".signature-caption""));
        collapseIfEmpty(cell);
        if (isHidden(cell)) hideDividerBefore(cell);
      }
    }

    if (disabledSet.Stamp) {
      var stampImgs = document.querySelectorAll('img[data-field=""Stamp""]');
      for (var i = 0; i < stampImgs.length; i++) {
        var stampCell = stampImgs[i].closest("".cell"");
        hide(stampCell);
        hideDividerBefore(stampCell);
      }
    }

    // آموزش — QRCode/Barcode همیشه Stage می‌شوند (نگاه کنید StageQr/
    // StageBarcode)؛ اگر قالب نخواهدشان، فقط سلولِ نمایششان مخفی می‌شود —
    // دقیقاً همان الگویِ Stamp بالا.
    if (disabledSet.QRCode) {
      var qrImgs = document.querySelectorAll('img[data-field=""QRCode""]');
      for (var i = 0; i < qrImgs.length; i++) {
        var qrCell = qrImgs[i].closest("".cell"");
        hide(qrCell);
        hideDividerBefore(qrCell);
      }
    }

    if (disabledSet.Barcode) {
      var barcodeImgs = document.querySelectorAll('img[data-field=""Barcode""]');
      for (var i = 0; i < barcodeImgs.length; i++) {
        var barcodeCell = barcodeImgs[i].closest("".cell"");
        hide(barcodeCell);
        hideDividerBefore(barcodeCell);
      }
    }

    // آموزش — هولوگرام از قبل یک سوییچِ آماده در خودِ GuardianCard دارد:
    // body.show-security کارتِ گرافیکیِ واقعی را نشان می‌دهد، نبودش دایرهٔ
    // خطچین (.hologram-off) را. guardian-card.js خودش این کلاس را روی
    // body اضافه می‌کند (رفتارِ پیش‌فرض = روشن). آموزش — رفعِ فضای خالی:
    // به‌درخواستِ کاربر، دیگر فقط کلاس برداشته نمی‌شود؛ کلِ سلولِ هولوگرام
    // هم مخفی می‌شود (نه فقط دایرهٔ خطچینِ جایگزین) تا اگر همهٔ سلول‌های
    // فوتر خاموش بودند، خودِ نوار هم بتواند جمع شود.
    if (disabledSet.Hologram) {
      document.body.classList.remove(""show-security"");
      var holoCells = document.querySelectorAll("".hologram"");
      for (var h = 0; h < holoCells.length; h++) {
        var holoCell = holoCells[h].closest("".cell"");
        hide(holoCell);
        if (isHidden(holoCell)) hideDividerBefore(holoCell);
      }
    }

    // آموزش — به‌درخواستِ کاربر: «شناسه کارت» بالا-چپِ هدر کاملاً اختیاری شد.
    if (disabledSet.CardCode) hideAll("".card-code-chip"");

    // آموزش — به‌درخواستِ صریحِ کاربر: به‌جای پاک‌کردنِ فیزیکیِ این عناصر از
    // index.html (که رویِ همهٔ قالب‌ها/نصب‌ها اثر می‌گذاشت)، toggleable
    // شدند — پیش‌فرض روشن، فقط قالبی که کاربر خودش خاموش می‌کند دیگر
    // نمی‌بیندشان.
    if (disabledSet.Besmellah) hideAll('[data-field=""Besmellah""]');
    if (disabledSet.OrganizationName) hideAll('[data-field=""OrganizationName""]');
    if (disabledSet.Portrait) hideAll("".portrait-medallion"");
    if (disabledSet.BranchName) hideAll("".branch-line"");
    if (disabledSet.GuardianName) removeSimpleRow(""GuardianName"", "".field-row"");
    if (disabledSet.FatherName) removeSimpleRow(""FatherName"", "".field-row"");
    if (disabledSet.NationalID) removeSimpleRow(""NationalID"", "".field-row"");
    if (disabledSet.RequestType) removeSimpleRow(""RequestType"", "".field-row"");
    if (disabledSet.OrphansCount) removeSimpleRow(""OrphansCount"", "".field-row"");
    if (disabledSet.Address) removeSimpleRow(""Address"", "".contact-row"");
    if (disabledSet.Phone) removeSimpleRow(""Phone"", "".contact-row"");
    if (disabledSet.FamilyList) hideAll('[data-field=""FamilyList""]');

    // آموزش — به‌درخواستِ صریحِ کاربر: عکسِ جمعیِ خانواده (قابِ مستقل بالایِ
    // فهرستِ اعضا) جدا از خودِ FamilyList روشن/خاموش می‌شود — دقیقاً هم‌الگویِ
    // Logo بالا (removeImageSlot). ستونِ عکسِ هر عضو (FamilyListPhotos) اینجا
    // نیست چون آن سلول‌ها پویا هستند و خودِ guardian-card.js حینِ ساختنِ
    // جدول چک‌شان می‌کند (نگاه کنید populateFamilyList/isFieldDisabled).
    //
    // آموزش — رفعِ باگِ گزارش‌شده («با خاموش‌کردن، زیرنویسِ متنی تنها
    // می‌ماند»): removeImageSlot فقط .family-photo-slot (خودِ قاب/عکس) را
    // مخفی می‌کرد؛ اما .family-photo-caption («عکس جمعی خانواده») در
    // index.html خواهرِ آن است (بیرون از .family-photo-slot، داخلِ
    // .family-photo-strip)، پس هرگز مخفی نمی‌شد. حالا زیرنویس هم مستقیم
    // مخفی می‌شود و کلِ نوار (.family-photo-strip) — که دیگر چیزی داخلش
    // دیده نمی‌شود — با collapseIfEmpty جمع می‌شود تا فاصلهٔ خالی/margin هم
    // برای فهرستِ اعضا آزاد شود.
    if (disabledSet.FamilyPhoto) {
      removeImageSlot(""FamilyPhoto"", "".family-photo-slot"");
      hideAll("".family-photo-caption"");
      var photoStrips = document.querySelectorAll("".family-photo-strip"");
      for (var fp = 0; fp < photoStrips.length; fp++) collapseIfEmpty(photoStrips[fp]);
    }

    // آموزش — به‌درخواستِ کاربر: اگر QR/بارکد/امضا/مهر همه خاموش باشند (و
    // در نتیجه سلولِ خودشان از قبل مخفی شده)، خودِ نوارِ پایین (فریم) هم
    // جمع شود تا فضای خالیِ الکی نماند و متنِ بالا (legal-line) بیاید
    // پایین. هولوگرام حساب نمی‌شود چون همیشه یک نشانه (روشن یا خاموش)
    // نمایش می‌دهد، نه یک سلولِ کاملاً مخفی.
    var bands = document.querySelectorAll("".security-band"");
    for (var b = 0; b < bands.length; b++) collapseIfEmpty(bands[b]);
  }

  document.addEventListener(""guardiancard:populated"", cleanup);
})();
";

        // آموزش — نسخهٔ سادهٔ اسکریپتِ بالا برای simple.html؛ نگاه کنید
        // ApplyDisabledFieldCleanupSimple.
        private const string RemoveDisabledFieldsScriptSimple = @"(function () {
  ""use strict"";
  var disabled = window.__simpleCardDisabledFields;
  if (!disabled || !disabled.length) return;
  var disabledSet = {};
  for (var i = 0; i < disabled.length; i++) disabledSet[disabled[i]] = true;

  function cleanup() {
    for (var field in disabledSet) {
      if (!disabledSet.hasOwnProperty(field)) continue;
      var els = document.querySelectorAll('[data-simple-field=""' + field + '""]');
      for (var i = 0; i < els.length; i++) els[i].style.display = ""none"";
    }

    // آموزش — به‌درخواستِ کاربر: وقتی «نام‌های ایتام» خاموش است، ستونِ سومِ
    // شبکه (بزرگ‌ترین ستون) کاملاً خالی می‌ماند و یک حفرهٔ زشت ایجاد
    // می‌کند. با افزودنِ این کلاس، simple.css به یک شبکهٔ دوستونه سوییچ
    // می‌کند تا فضای خالی هوشمندانه توسط ستونِ فیلدها/تذکرات پر شود.
    if (disabledSet.Orphans) {
      var bodies = document.querySelectorAll("".simple-body"");
      for (var i = 0; i < bodies.length; i++) bodies[i].classList.add(""simple-body--no-orphans"");
    }
  }

  document.addEventListener(""simplecard:populated"", cleanup);
})();
";

        private void EnsureBundledPackagePresent()
        {
            if (!IsBundledPackagePresent())
                throw new DirectoryNotFoundException(
                    "پوشه GuardianCard کنار برنامه پیدا نشد: " + BundledSourceFolder +
                    "\nاین پوشه باید همراه نصب برنامه دیپلوی شده باشد.");
        }

        // مقدارِ بارکدِ هر خانواده — آموزش (رفعِ باگِ «همه کارت‌ها یک بارکد»):
        // قبلاً وقتی CardNumber خالی بود، به PublicCode (ستون Code، متنِ آزادِ
        // کاربر که معمولاً فارسی است) برمی‌گشت؛ Code128Barcode هر کاراکترِ
        // غیرِ ASCII را بی‌صدا حذف می‌کند و اگر چیزی نمی‌ماند مقدار را «0»
        // می‌گذارد — پس چند پرونده‌ی بدون CardNumber همه بارکدِ یکسانِ «0»
        // می‌گرفتند. اکنون:
        //   ۱) اگر CardNumber هست، دقیقاً همان مقداری که روی کارت چاپ می‌شود
        //      (BranchCode-CardNumber) کد می‌شود — این‌طور بارکد بین مراکزِ
        //      مختلف هم یکتا می‌ماند (قبلاً فقط CardNumber کد می‌شد و دو مرکز
        //      با شماره فرم یکسان بارکدِ یکسان می‌گرفتند).
        //   ۲) وگرنه از CasID (کلید یکتای واقعیِ دیتابیس، همیشه ASCII و همیشه
        //      یکتا) استفاده می‌شود — نه از PublicCode که ممکن است فارسی باشد.
        private static string BarcodeValue(GuardianCardData data)
        {
            if (data == null) return "";
            if (!string.IsNullOrWhiteSpace(data.CardNumber))
            {
                return !string.IsNullOrWhiteSpace(data.BranchCode)
                    ? data.BranchCode + "-" + data.CardNumber
                    : data.CardNumber;
            }
            return "C" + data.CasID;
        }

        // بارکد Code128 واقعی از مقدار بالا می‌سازد (هر پرونده بارکد خودش را
        // دارد، چون آن مقدار یکتاست)؛ اگر هر دو خالی باشند، بارکدی
        // ساخته نمی‌شود (placeholder خالیِ guardian-card.js جایش می‌ماند).
        private static string StageBarcode(string cardNumber, string uploadsDir, string destBaseName)
        {
            if (string.IsNullOrWhiteSpace(cardNumber))
                return "";

            string destName = destBaseName + ".png";
            Code128Barcode.SaveToFile(cardNumber, Path.Combine(uploadsDir, destName));
            return "uploads/" + destName;
        }

        // آموزش — QR واقعی از همان مقدارِ بارکد (BarcodeValue) ساخته می‌شود
        // تا اسکنِ هرکدام همان شناسهٔ کارت را بدهد. همیشه Stage می‌شود (مثلِ
        // بارکد)؛ اگر قالب QR را نخواهد، caller «QRCode» را به disabledFields
        // اضافه می‌کند تا فقط از DOM مخفی شود (نگاه کنید ApplyDisabledFieldCleanup) —
        // نه اینکه اینجا تولید نشود، تا رفتار هردو (بارکد/QR) یکسان بماند.
        private static string StageQr(string content, string uploadsDir, string destBaseName)
        {
            if (string.IsNullOrWhiteSpace(content))
                return "";

            string destName = destBaseName + ".png";
            QrCodeHelper.SaveToFile(content, Path.Combine(uploadsDir, destName));
            return "uploads/" + destName;
        }

        // کپی یک تصویر منبع (اگر موجود باشد) به uploads/ داخل پوشه کاری و
        // برگرداندن مسیر نسبیِ مناسب برای JSON؛ اگر منبع نبود، رشته خالی
        // برمی‌گردد (guardian-card.js خودش placeholder را برای مقدار خالی نگه می‌دارد).
        private static string StageImage(string sourcePath, string uploadsDir, string destBaseName)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                return "";

            string destName = destBaseName + Path.GetExtension(sourcePath);
            File.Copy(sourcePath, Path.Combine(uploadsDir, destName), true);
            return "uploads/" + destName;
        }

        private static void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (string file in Directory.GetFiles(sourceDir))
                File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), true);
            foreach (string subDir in Directory.GetDirectories(sourceDir))
                CopyDirectory(subDir, Path.Combine(destDir, Path.GetFileName(subDir)));
        }
    }
}
