using System;
using System.Security.Cryptography;

namespace CaseManagement.Helpers
{
    public static class PasswordHelper
    {
        private const int SaltSize = 32;
        private const int HashSize = 32;

        // آموزش — ارتقای امنیتی سازگار با نسخه‌های قبلی: تعداد Iteration قبلی
        // (۱۰٬۰۰۰) طبق استانداردهای امروزی OWASP کم است. چون تعداد Iteration
        // مستقیماً روی بایت‌های هش تأثیر می‌گذارد، نمی‌شود این مقدار را برای
        // کاربران موجود عوض کرد بدون این‌که رمز قبلی‌شان دیگر قابل تأیید نباشد.
        // راه‌حل: تعداد Iteration به‌ازای هر کاربر در دیتابیس ذخیره می‌شود
        // (ستون PasswordIterations). رمزهای جدید/تغییریافته با مقدار بالاتر
        // هش می‌شوند؛ رمزهای قدیمی همچنان با همان مقدار قدیمی خودشان تأیید
        // می‌شوند تا زمانی که کاربر رمزش را عوض کند.
        public const int DefaultIterations = 100000;
        public const int LegacyIterations  = 10000;

        // ⚠ یکسان‌سازی با سرورِ همگام‌سازی: SyncServer رمز را با PBKDF2-HMAC-SHA256
        // می‌سازد و می‌سنجد (نگاه کنید SyncServer/Infrastructure/Security.cs). نسخهٔ
        // قبلیِ این برنامه SHA1 (پیش‌فرضِ Rfc2898DeriveBytes) بود؛ نتیجه این‌که یک
        // کاربرِ ساخته‌شده در ویندوز رمزش در وب کار نمی‌کرد و برعکس. با ساختِ رمزهای
        // جدید روی SHA256، یک رمزِ واحد در هر دو طرف کار می‌کند. Verify هم SHA1 را
        // به‌عنوان fallback نگه می‌دارد تا رمزهای قدیمی همچنان تأیید شوند.
        public static void CreateHash(string password, out byte[] hash, out byte[] salt, out int iterations)
        {
            if (password == null)
                password = "";

            iterations = DefaultIterations;
            salt = new byte[SaltSize];

            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
                rng.GetBytes(salt);

            using (Rfc2898DeriveBytes pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256))
                hash = pbkdf2.GetBytes(HashSize);
        }

        // اورلود قدیمی — برای سازگاری با هر کد قدیمی‌تری که هنوز صدا می‌زند
        public static void CreateHash(string password, out byte[] hash, out byte[] salt)
        {
            int iterations;
            CreateHash(password, out hash, out salt, out iterations);
        }

        public static bool Verify(string password, byte[] expectedHash, byte[] salt, int iterations)
        {
            if (password == null)
                password = "";

            if (expectedHash == null || salt == null)
                return false;

            if (iterations <= 0)
                iterations = LegacyIterations; // رمزهای ثبت‌شده قبل از این ارتقا

            // اول SHA256 (روشِ جدید و روشِ سرور) امتحان می‌شود. اگر نخورد، SHA1
            // (رمزهای قدیمیِ این برنامه) امتحان می‌شود تا هیچ رمزِ موجودی نشکند.
            // چون طولِ هش برای هر دو یکی است، از روی طول نمی‌شود تشخیص داد؛ پس
            // هر دو امتحان می‌شوند.
            if (DeriveAndCompare(password, expectedHash, salt, iterations, HashAlgorithmName.SHA256))
                return true;

            if (DeriveAndCompare(password, expectedHash, salt, iterations, HashAlgorithmName.SHA1))
                return true;

            return false;
        }

        private static bool DeriveAndCompare(string password, byte[] expectedHash, byte[] salt,
                                             int iterations, HashAlgorithmName algorithm)
        {
            using (Rfc2898DeriveBytes pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, algorithm))
            {
                byte[] actualHash = pbkdf2.GetBytes(expectedHash.Length);
                return FixedTimeEquals(actualHash, expectedHash);
            }
        }

        // اورلود قدیمی — فرض می‌کند رمز با تعداد Iteration قدیمی ساخته شده
        public static bool Verify(string password, byte[] expectedHash, byte[] salt)
        {
            return Verify(password, expectedHash, salt, LegacyIterations);
        }

        private static bool FixedTimeEquals(byte[] first, byte[] second)
        {
            if (first == null || second == null || first.Length != second.Length)
                return false;

            int diff = 0;

            for (int i = 0; i < first.Length; i++)
                diff |= first[i] ^ second[i];

            return diff == 0;
        }
    }
}
