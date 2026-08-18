using System.IO;
using QRCoder;

namespace CaseManagement.GuardianCardIntegration
{
    // ─────────────────────────────────────────────────────────────────────────
    // مولدِ QR واقعی — به‌درخواستِ کاربر، به‌جای پیاده‌سازیِ دستیِ ریاضیاتِ
    // Reed-Solomon (پرریسک و بدونِ اسکنرِ واقعی قابلِ تأیید نیست)، از کتابخانهٔ
    // معتبرِ QRCoder استفاده می‌شود. الگوی این کلاس دقیقاً مثلِ
    // Code128Barcode.cs — یک متدِ ساده که PNG می‌سازد و ذخیره می‌کند.
    // ─────────────────────────────────────────────────────────────────────────
    public static class QrCodeHelper
    {
        // pixelsPerModule=8 برای اندازهٔ QR در index.html (recommendedSize
        // 300x300px طبق docs/TEMPLATE_SCHEMA.json) کیفیتِ کافی برای چاپ
        // 300dpi می‌دهد.
        public static void SaveToFile(string content, string filePath, int pixelsPerModule = 8)
        {
            using (var generator = new QRCodeGenerator())
            using (QRCodeData qrData = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.M))
            {
                var pngGenerator = new PngByteQRCode(qrData);
                byte[] pngBytes = pngGenerator.GetGraphic(pixelsPerModule);
                File.WriteAllBytes(filePath, pngBytes);
            }
        }
    }
}
