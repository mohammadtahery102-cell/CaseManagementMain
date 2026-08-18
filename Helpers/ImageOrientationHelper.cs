using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace CaseManagement.Helpers
{
    // ═════════════════════════════════════════════════════════════════════════
    // اصلاحِ جهت (EXIF) و فشرده‌سازیِ محافظه‌کارانه‌ی تصویر.
    //
    // ⚠ قانونِ اصلی: فایلِ منبعِ کاربر هرگز تغییر نمی‌کند. این توابع همیشه روی
    // «کپی» کار می‌کنند و خروجی را در مسیر مقصد می‌نویسند. اگر هر مرحله شکست
    // بخورد، به کپیِ ساده‌ی بایت‌به‌بایت برمی‌گردیم تا هیچ عکسی از دست نرود.
    // ═════════════════════════════════════════════════════════════════════════
    public static class ImageOrientationHelper
    {
        private const int ExifOrientationTag = 0x0112;

        // آیا این تصویر بر اساس EXIF نیاز به چرخش دارد؟
        public static bool NeedsRotation(Image image)
        {
            int orientation = ReadOrientation(image);
            return orientation > 1 && orientation <= 8;
        }

        private static int ReadOrientation(Image image)
        {
            try
            {
                if (image == null || image.PropertyIdList == null) return 1;
                if (!image.PropertyIdList.Contains(ExifOrientationTag)) return 1;

                PropertyItem item = image.GetPropertyItem(ExifOrientationTag);
                if (item == null || item.Value == null || item.Value.Length < 2) return 1;

                return BitConverter.ToUInt16(item.Value, 0);
            }
            catch { return 1; }
        }

        private static RotateFlipType FlipFor(int orientation)
        {
            switch (orientation)
            {
                case 2: return RotateFlipType.RotateNoneFlipX;
                case 3: return RotateFlipType.Rotate180FlipNone;
                case 4: return RotateFlipType.Rotate180FlipX;
                case 5: return RotateFlipType.Rotate90FlipX;
                case 6: return RotateFlipType.Rotate90FlipNone;
                case 7: return RotateFlipType.Rotate270FlipX;
                case 8: return RotateFlipType.Rotate270FlipNone;
                default: return RotateFlipType.RotateNoneFlipNone;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // تصویر را از مبدأ می‌خواند، در صورت نیاز می‌چرخاند و اگر از سقف
        // بزرگ‌تر باشد فشرده می‌کند، و در مقصد می‌نویسد.
        //
        // خروجی: true اگر پردازش انجام شد؛ false یعنی باید کپیِ ساده انجام شود
        // (مثلاً webp که GDI+ رمزگشایی‌اش نمی‌کند).
        // ─────────────────────────────────────────────────────────────────────
        // نسخه‌ی سازگار با فراخوان‌های قبلی (بدون تبدیلِ اجباری به JPG).
        public static bool TryProcessCopy(string sourcePath, string destinationPath,
                                          bool compress, long maxBytes,
                                          out bool rotated, out bool compressed)
        {
            return TryProcessCopy(sourcePath, destinationPath, compress, maxBytes,
                                  out rotated, out compressed, false);
        }

        // forceJpeg=true یعنی خروجی حتماً JPG باشد، حتی اگر نه چرخش لازم باشد و
        // نه فشرده‌سازی (برای یکسان‌سازیِ فرمتِ همه‌ی عکس‌های سیستم).
        public static bool TryProcessCopy(string sourcePath, string destinationPath,
                                          bool compress, long maxBytes,
                                          out bool rotated, out bool compressed,
                                          bool forceJpeg)
        {
            rotated = false;
            compressed = false;

            try
            {
                if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath)) return false;

                string ext = Path.GetExtension(sourcePath ?? "").ToLowerInvariant();
                if (ext == ".webp") return false;   // GDI+ رمزگشایی نمی‌کند

                using (var input = new FileStream(sourcePath, FileMode.Open, FileAccess.Read))
                using (Image original = Image.FromStream(input, false, false))
                {
                    int orientation = ReadOrientation(original);
                    bool needsRotate = orientation > 1 && orientation <= 8;

                    long sourceSize = new FileInfo(sourcePath).Length;
                    bool needsCompress = compress && sourceSize > maxBytes;

                    // هیچ کاری لازم نیست → کپیِ ساده انجام شود (کیفیت دست‌نخورده)
                    if (!needsRotate && !needsCompress && !forceJpeg) return false;

                    using (var bitmap = new Bitmap(original))
                    {
                        if (needsRotate)
                        {
                            bitmap.RotateFlip(FlipFor(orientation));
                            // پس از چرخش، تگِ EXIF باید پاک شود وگرنه نمایش‌دهنده‌ها
                            // دوباره می‌چرخانند و تصویر برعکس می‌شود.
                            try { bitmap.RemovePropertyItem(ExifOrientationTag); } catch { }
                            rotated = true;
                        }

                        // تبدیلِ فرمت بدونِ کاهشِ ابعاد: کیفیت بالا (۹۵) تا
                        // تفاوتی با اصل دیده نشود. فقط ظرف عوض می‌شود، نه محتوا.
                        if (forceJpeg && !needsCompress)
                        {
                            SaveJpeg(bitmap, destinationPath, 95L);
                            return File.Exists(destinationPath);
                        }

                        if (needsCompress)
                        {
                            // آموزش — فشرده‌سازیِ محافظه‌کارانه: کیفیت ۸۵٪ عملاً
                            // برای چشم غیرقابل تشخیص است ولی حجم را چشمگیر کم
                            // می‌کند. ابعاد فقط وقتی کوچک می‌شود که واقعاً بزرگ
                            // باشد، و هرگز زیر حدِ توصیه‌شده نمی‌رود.
                            SaveCompressed(bitmap, destinationPath);
                            compressed = true;
                        }
                        else
                        {
                            SaveSameFormat(bitmap, destinationPath, ext);
                        }
                    }
                }

                return File.Exists(destinationPath);
            }
            catch
            {
                // هر مشکلی پیش بیاید، کپیِ ساده انجام می‌شود تا عکس از دست نرود.
                rotated = false;
                compressed = false;
                return false;
            }
        }

        private const int MaxLongEdge = 2000;   // بیش از این برای کارت/گزارش لازم نیست
        private const long JpegQuality = 85L;

        private static void SaveCompressed(Bitmap bitmap, string destinationPath)
        {
            int w = bitmap.Width, h = bitmap.Height;
            int longEdge = Math.Max(w, h);

            Bitmap toSave = bitmap;
            bool resized = false;

            if (longEdge > MaxLongEdge)
            {
                double scale = MaxLongEdge / (double)longEdge;
                int nw = Math.Max(1, (int)Math.Round(w * scale));
                int nh = Math.Max(1, (int)Math.Round(h * scale));

                var resizedBmp = new Bitmap(nw, nh);
                using (Graphics g = Graphics.FromImage(resizedBmp))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawImage(bitmap, 0, 0, nw, nh);
                }
                toSave = resizedBmp;
                resized = true;
            }

            try { SaveJpeg(toSave, destinationPath, JpegQuality); }
            finally { if (resized) toSave.Dispose(); }
        }

        // ذخیره‌ی JPG با کیفیتِ مشخص. اگر رمزگذارِ JPEG در دسترس نبود، به
        // ذخیره‌ی پیش‌فرض برمی‌گردیم تا هیچ عکسی از دست نرود.
        private static void SaveJpeg(Bitmap bitmap, string destinationPath, long quality)
        {
            ImageCodecInfo jpeg = ImageCodecInfo.GetImageEncoders()
                .FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);

            if (jpeg != null)
            {
                using (var parameters = new EncoderParameters(1))
                {
                    parameters.Param[0] = new EncoderParameter(Encoder.Quality, quality);
                    bitmap.Save(destinationPath, jpeg, parameters);
                }
            }
            else
            {
                bitmap.Save(destinationPath, ImageFormat.Jpeg);
            }
        }

        private static void SaveSameFormat(Bitmap bitmap, string destinationPath, string extension)
        {
            ImageFormat format = ImageFormat.Jpeg;
            switch (extension)
            {
                case ".png": format = ImageFormat.Png; break;
                case ".bmp": format = ImageFormat.Bmp; break;
            }
            bitmap.Save(destinationPath, format);
        }
    }
}
