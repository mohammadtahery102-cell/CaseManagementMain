using System;
using System.IO;
using System.Security.Cryptography;

namespace CaseManagement.Helpers
{
    // ─────────────────────────────────────────────────────────────────────────
    // رمزنگاریِ فایل‌های بکاپ — AES-256-CBC + HMAC-SHA256 (الگوی Encrypt-then-MAC).
    //
    // آموزش — چرا GCM نه: .NET Framework 4.7.2 (نسخه‌ی این پروژه) کلاس AesGcm
    // ندارد؛ آن فقط از .NET Core 3.0 / .NET Standard 2.1 به بعد موجود است.
    // الگوی استانداردِ رمزنگاریِ احرازشده روی .NET Framework، AES-CBC به‌همراه
    // یک HMAC جداگانه روی (Salt‖IV‖Ciphertext) است — دقیقاً همان چیزی که
    // اینجا پیاده‌سازی شده. HMAC هم صحتِ داده (دستکاری/خرابی) را تضمین
    // می‌کند و هم — چون رمزِ اشتباه، کلیدِ اشتباه و در نتیجه HMAC نامعتبر
    // می‌سازد — تشخیصِ «رمز غلط» را بدونِ نیاز به تلاش برای رمزگشاییِ AES
    // ممکن می‌کند (شکستِ ایمن، پیش از هر پردازشِ دیگر).
    //
    // مشتقِ کلید: PBKDF2 (Rfc2898DeriveBytes) با همان تعداد Iteration
    // پیش‌فرضِ PasswordHelper — یکدستیِ رمزنگاری در کل برنامه (نیازِ صریحِ
    // این ویژگی). خروجیِ ۶۴ بایتیِ PBKDF2 به دو کلیدِ ۳۲ بایتی تقسیم می‌شود:
    // یکی برای AES، یکی برای HMAC — طبق اصولِ رمزنگاری، هرگز یک کلید برای
    // دو کاربردِ متفاوت استفاده نمی‌شود.
    //
    // قالبِ فایلِ خروجی (هیچ‌کدام از این‌ها محرمانه نیستند، فقط ciphertext است):
    //   [8 بایت آشکارساز/نسخه] [16 بایت Salt] [16 بایت IV] [32 بایت HMAC] [ciphertext]
    //
    // آموزش — چرا کل فایل در حافظه (نه استریمِ تکه‌تکه): حجمِ بکاپ‌های این
    // برنامه (چند صد تا چند هزار رکورد) در مرتبه‌ی چند ده مگابایت است، نه
    // گیگابایت. پردازشِ کاملِ فایل در حافظه، پیاده‌سازی را به‌طرزِ محسوسی
    // ساده‌تر و کم‌خطاتر می‌کند (بدون نگرانیِ هم‌ترازیِ بلاک‌های CBC میانِ
    // چند Read) بدون هزینه‌ی عملیِ قابل‌توجه در این مقیاس.
    // ─────────────────────────────────────────────────────────────────────────
    public static class BackupEncryption
    {
        private static readonly byte[] MagicHeader = { (byte)'C', (byte)'M', (byte)'B', (byte)'A', (byte)'K', (byte)'1', 0, 0 };

        private const int SaltSize = 16;
        private const int IvSize = 16;
        private const int HmacSize = 32;
        private const int DerivedKeyMaterialSize = 64; // 32 برای AES + 32 برای HMAC

        // پسوندِ فایلِ بکاپِ رمزنگاری‌شده — برای فیلترِ کادرهای انتخابِ فایل.
        public const string EncryptedExtension = ".cmbak";

        // خطای اختصاصی: رمزِ اشتباه یا فایلِ دستکاری‌شده/خراب. عمداً از هم
        // تفکیک نشده‌اند (نه «رمز غلط» جدا از «فایل خراب») چون افشای این
        // تفاوت به کاربر، اطلاعاتی به مهاجمِ احتمالی می‌دهد.
        public class IntegrityException : Exception
        {
            public IntegrityException(string message) : base(message) { }
        }

        public static void EncryptFile(string sourcePath, string destPath, string password)
        {
            if (!File.Exists(sourcePath))
                throw new FileNotFoundException("فایل مبدأ برای رمزنگاری پیدا نشد.", sourcePath);
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("رمز عبور نمی‌تواند خالی باشد.");

            byte[] salt = RandomBytes(SaltSize);
            byte[] iv = RandomBytes(IvSize);
            byte[] aesKey, hmacKey;
            DeriveKeys(password, salt, out aesKey, out hmacKey);

            byte[] plaintext = File.ReadAllBytes(sourcePath);
            byte[] ciphertext;

            try
            {
                using (Aes aes = CreateAes(aesKey, iv))
                using (ICryptoTransform encryptor = aes.CreateEncryptor())
                    ciphertext = encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length);

                byte[] tag = ComputeTag(hmacKey, salt, iv, ciphertext);

                using (FileStream fs = new FileStream(destPath, FileMode.Create, FileAccess.Write))
                using (BinaryWriter bw = new BinaryWriter(fs))
                {
                    bw.Write(MagicHeader);
                    bw.Write(salt);
                    bw.Write(iv);
                    bw.Write(tag);
                    bw.Write(ciphertext);
                }
            }
            finally
            {
                Array.Clear(aesKey, 0, aesKey.Length);
                Array.Clear(hmacKey, 0, hmacKey.Length);
                Array.Clear(plaintext, 0, plaintext.Length);
            }
        }

        public static void DecryptFile(string sourcePath, string destPath, string password)
        {
            if (!File.Exists(sourcePath))
                throw new FileNotFoundException("فایل بکاپِ رمزنگاری‌شده پیدا نشد.", sourcePath);
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("رمز عبور نمی‌تواند خالی باشد.");

            byte[] fileBytes = File.ReadAllBytes(sourcePath);

            int headerLength = MagicHeader.Length + SaltSize + IvSize + HmacSize;
            if (fileBytes.Length < headerLength)
                throw new IntegrityException("فایل بکاپ خراب یا ناقص است.");

            int pos = 0;
            byte[] magic = Slice(fileBytes, ref pos, MagicHeader.Length);
            if (!BytesEqual(magic, MagicHeader))
                throw new IntegrityException("این فایل یک بکاپ رمزنگاری‌شده معتبر نیست.");

            byte[] salt = Slice(fileBytes, ref pos, SaltSize);
            byte[] iv = Slice(fileBytes, ref pos, IvSize);
            byte[] storedTag = Slice(fileBytes, ref pos, HmacSize);
            byte[] ciphertext = Slice(fileBytes, ref pos, fileBytes.Length - pos);

            byte[] aesKey, hmacKey;
            DeriveKeys(password, salt, out aesKey, out hmacKey);

            try
            {
                byte[] computedTag = ComputeTag(hmacKey, salt, iv, ciphertext);

                // ─── شکستِ ایمن: اول صحت/رمز بررسی می‌شود، بعد رمزگشاییِ AES.
                // اگر HMAC مطابقت نداشته باشد (رمزِ اشتباه یا فایلِ دستکاری‌شده)،
                // هیچ تلاشی برای رمزگشایی یا نوشتنِ خروجی انجام نمی‌شود.
                if (!BytesEqual(computedTag, storedTag))
                    throw new IntegrityException("رمز عبور اشتباه است یا فایل بکاپ دستکاری/خراب شده است.");

                byte[] plaintext;
                try
                {
                    using (Aes aes = CreateAes(aesKey, iv))
                    using (ICryptoTransform decryptor = aes.CreateDecryptor())
                        plaintext = decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
                }
                catch (CryptographicException)
                {
                    // نگهبانِ اضافی — عملاً غیرِ قابلِ رسیدن چون HMAC از قبل رد شده،
                    // ولی هر مسیرِ شکستِ رمزگشایی باید همان پیامِ ایمن را بدهد.
                    throw new IntegrityException("رمز عبور اشتباه است یا فایل بکاپ دستکاری/خراب شده است.");
                }

                File.WriteAllBytes(destPath, plaintext);
                Array.Clear(plaintext, 0, plaintext.Length);
            }
            finally
            {
                Array.Clear(aesKey, 0, aesKey.Length);
                Array.Clear(hmacKey, 0, hmacKey.Length);
            }
        }

        // بررسیِ صحت/رمز بدون نوشتنِ هیچ خروجی‌ای روی دیسک — برای صفحه‌ی
        // «بررسیِ بکاپ» که فقط می‌خواهد مطمئن شود فایل قابلِ بازگشایی است.
        public static void VerifyIntegrity(string sourcePath, string password)
        {
            string tempPath = Path.Combine(Path.GetTempPath(), "cmbak_verify_" + Guid.NewGuid().ToString("N") + ".zip");
            try
            {
                DecryptFile(sourcePath, tempPath, password);
            }
            finally
            {
                TryDelete(tempPath);
            }
        }

        public static bool LooksLikeEncryptedBackup(string path)
        {
            try
            {
                if (!File.Exists(path)) return false;
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    if (fs.Length < MagicHeader.Length) return false;
                    byte[] magic = new byte[MagicHeader.Length];
                    int read = fs.Read(magic, 0, magic.Length);
                    return read == MagicHeader.Length && BytesEqual(magic, MagicHeader);
                }
            }
            catch
            {
                return false;
            }
        }

        // ─── کمکی‌های داخلی ─────────────────────────────────────────────────

        private static void DeriveKeys(string password, byte[] salt, out byte[] aesKey, out byte[] hmacKey)
        {
            using (Rfc2898DeriveBytes pbkdf2 = new Rfc2898DeriveBytes(password, salt, PasswordHelper.DefaultIterations))
            {
                byte[] derived = pbkdf2.GetBytes(DerivedKeyMaterialSize);
                aesKey = new byte[32];
                hmacKey = new byte[32];
                Buffer.BlockCopy(derived, 0, aesKey, 0, 32);
                Buffer.BlockCopy(derived, 32, hmacKey, 0, 32);
                Array.Clear(derived, 0, derived.Length);
            }
        }

        private static byte[] ComputeTag(byte[] hmacKey, byte[] salt, byte[] iv, byte[] ciphertext)
        {
            using (HMACSHA256 hmac = new HMACSHA256(hmacKey))
            {
                byte[] combined = new byte[salt.Length + iv.Length + ciphertext.Length];
                Buffer.BlockCopy(salt, 0, combined, 0, salt.Length);
                Buffer.BlockCopy(iv, 0, combined, salt.Length, iv.Length);
                Buffer.BlockCopy(ciphertext, 0, combined, salt.Length + iv.Length, ciphertext.Length);
                return hmac.ComputeHash(combined);
            }
        }

        private static Aes CreateAes(byte[] key, byte[] iv)
        {
            Aes aes = Aes.Create();
            aes.KeySize = 256;
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            return aes;
        }

        private static byte[] RandomBytes(int size)
        {
            byte[] bytes = new byte[size];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
                rng.GetBytes(bytes);
            return bytes;
        }

        private static byte[] Slice(byte[] source, ref int pos, int length)
        {
            byte[] result = new byte[length];
            Buffer.BlockCopy(source, pos, result, 0, length);
            pos += length;
            return result;
        }

        // مقایسه‌ی زمان‌ثابت — همان الگوی PasswordHelper.FixedTimeEquals، تا
        // زمانِ پاسخ اطلاعاتی درباره‌ی میزانِ درستیِ رمز لو ندهد.
        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;

            int diff = 0;
            for (int i = 0; i < a.Length; i++)
                diff |= a[i] ^ b[i];

            return diff == 0;
        }

        internal static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { /* پاک‌سازیِ فایلِ موقت حیاتی نیست */ }
        }

        internal static void TryDeleteDirectory(string path)
        {
            try { if (Directory.Exists(path)) Directory.Delete(path, true); }
            catch { /* پاک‌سازیِ پوشه‌ی موقت حیاتی نیست */ }
        }
    }
}
