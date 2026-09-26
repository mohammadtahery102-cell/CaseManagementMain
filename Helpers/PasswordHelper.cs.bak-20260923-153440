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

        public static void CreateHash(string password, out byte[] hash, out byte[] salt, out int iterations)
        {
            if (password == null)
                password = "";

            iterations = DefaultIterations;
            salt = new byte[SaltSize];

            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
                rng.GetBytes(salt);

            using (Rfc2898DeriveBytes pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations))
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

            using (Rfc2898DeriveBytes pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations))
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
