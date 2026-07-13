using System;
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
        // (عکس سرپرست/لوگو مؤسسه) داخل پوشه کاری. خروجی: مسیر پوشه کاری (ریشه‌ای
        // که index.html در آن است) برای نگاشت virtual-host توسط فراخوان.
        public string StageAndPopulate(GuardianCardData data, string guardianPhotoPath, string orgLogoPath)
        {
            if (data == null) throw new ArgumentNullException("data");
            if (!IsBundledPackagePresent())
                throw new DirectoryNotFoundException(
                    "پوشه GuardianCard کنار برنامه پیدا نشد: " + BundledSourceFolder +
                    "\nاین پوشه باید همراه نصب برنامه دیپلوی شده باشد.");

            if (Directory.Exists(WorkingFolder))
                Directory.Delete(WorkingFolder, true);
            CopyDirectory(BundledSourceFolder, WorkingFolder);

            string uploadsDir = Path.Combine(WorkingFolder, "uploads");
            Directory.CreateDirectory(uploadsDir);

            data.Photo = StageImage(guardianPhotoPath, uploadsDir, "guardian_photo");
            data.Logo = StageImage(orgLogoPath, uploadsDir, "org_logo");

            string json = new JavaScriptSerializer().Serialize(data);
            File.WriteAllText(Path.Combine(WorkingFolder, "sample", "SAMPLE_DATA.json"), json, new UTF8Encoding(false));

            return WorkingFolder;
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
