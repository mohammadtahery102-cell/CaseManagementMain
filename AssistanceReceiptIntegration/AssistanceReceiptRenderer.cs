using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using CaseManagement.GuardianCardIntegration;

namespace CaseManagement.AssistanceReceiptIntegration
{
    // ─────────────────────────────────────────────────────────────────────────
    // لایه یکپارچه‌سازی ماژول برگه دریافتی مساعدت — آینه GuardianCardRenderer.
    // بستهٔ فریزشده (Templates\assistance-receipt کنارِ exe با نامِ
    // «AssistanceReceipt» دیپلوی می‌شود) هرگز نوشته/تغییر داده نمی‌شود؛ هر
    // رندر روی یک کپیِ کاریِ موقت انجام می‌شود. Code128Barcode عیناً از ماژولِ
    // کارتِ شناسایی استفاده مجدد می‌شود (بدونِ بازنویسی).
    // ─────────────────────────────────────────────────────────────────────────
    public class AssistanceReceiptRenderer
    {
        public const string VirtualHostName = "assistancereceipt.local";

        private static string BundledSourceFolder
        {
            get { return Path.Combine(Application.StartupPath, "AssistanceReceipt"); }
        }

        private static string WorkingFolder
        {
            get { return Path.Combine(Path.GetTempPath(), "CaseManagement_AssistanceReceiptWork"); }
        }

        public bool IsBundledPackagePresent()
        {
            return Directory.Exists(BundledSourceFolder) && File.Exists(Path.Combine(BundledSourceFolder, "index.html"));
        }

        // چاپ تکی: کپیِ بسته + نوشتنِ JSONِ واقعیِ همین رکورد + کپیِ عکس/لوگو +
        // ساختِ بارکدِ واقعی (Code128) از ReceiptCode. خروجی: مسیرِ پوشهٔ کاری.
        public string StageAndPopulate(AssistanceReceiptData data, string recipientPhotoPath, string orgLogoPath)
        {
            if (data == null) throw new ArgumentNullException("data");
            EnsureBundledPackagePresent();

            if (Directory.Exists(WorkingFolder))
                Directory.Delete(WorkingFolder, true);
            CopyDirectory(BundledSourceFolder, WorkingFolder);

            string uploadsDir = Path.Combine(WorkingFolder, "uploads");
            Directory.CreateDirectory(uploadsDir);

            data.Photo = StageImage(recipientPhotoPath, uploadsDir, "recipient_photo");
            data.Logo = StageImage(orgLogoPath, uploadsDir, "org_logo");
            data.Barcode = StageBarcode(data.ReceiptCode, uploadsDir, "barcode");

            string json = new JavaScriptSerializer().Serialize(data);
            File.WriteAllText(Path.Combine(WorkingFolder, "sample", "SAMPLE_DATA.json"), json, new UTF8Encoding(false));

            return WorkingFolder;
        }

        // چاپ گروهی — لوگو (مؤسسه‌ای/مشترک) فقط یک‌بار Stage می‌شود؛ هر رکورد
        // عکس/بارکدِ اختصاصیِ خودش را می‌گیرد (نامِ فایل با اندیس یکتا).
        public string StageAndPopulateBatch(
            IList<AssistanceReceiptData> items, Func<AssistanceReceiptData, string> recipientPhotoPathSelector,
            string orgLogoPath)
        {
            if (items == null) throw new ArgumentNullException("items");
            EnsureBundledPackagePresent();

            if (Directory.Exists(WorkingFolder))
                Directory.Delete(WorkingFolder, true);
            CopyDirectory(BundledSourceFolder, WorkingFolder);

            string uploadsDir = Path.Combine(WorkingFolder, "uploads");
            Directory.CreateDirectory(uploadsDir);

            string logoRel = StageImage(orgLogoPath, uploadsDir, "org_logo");

            for (int i = 0; i < items.Count; i++)
            {
                AssistanceReceiptData data = items[i];
                string photoPath = recipientPhotoPathSelector != null ? recipientPhotoPathSelector(data) : data.Photo;
                data.Photo = StageImage(photoPath, uploadsDir, "recipient_photo_" + i);
                data.Logo = logoRel;
                data.Barcode = StageBarcode(data.ReceiptCode, uploadsDir, "barcode_" + i);
            }

            string json = new JavaScriptSerializer().Serialize(items);
            File.WriteAllText(Path.Combine(WorkingFolder, "sample", "SAMPLE_DATA.json"), json, new UTF8Encoding(false));

            return WorkingFolder;
        }

        private void EnsureBundledPackagePresent()
        {
            if (!IsBundledPackagePresent())
                throw new DirectoryNotFoundException(
                    "پوشه AssistanceReceipt کنار برنامه پیدا نشد: " + BundledSourceFolder +
                    "\nاین پوشه باید همراه نصب برنامه دیپلوی شده باشد.");
        }

        private static string StageBarcode(string receiptCode, string uploadsDir, string destBaseName)
        {
            if (string.IsNullOrWhiteSpace(receiptCode))
                return "";

            string destName = destBaseName + ".png";
            Code128Barcode.SaveToFile(receiptCode, Path.Combine(uploadsDir, destName));
            return "uploads/" + destName;
        }

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
