using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace CaseManagement.Helpers
{
    // ─────────────────────────────────────────────────────────────────────────
    // LicenseManager — زیرساخت فعال‌سازی/اعتبارسنجی لایسنس نرم‌افزار.
    //
    // آموزش — «فقط زیرساخت»: طبق درخواست، این ماژول هیچ قابلیتی را حذف یا
    // مسدود نمی‌کند. کارش این است که:
    //   ۱. یک «شناسه سخت‌افزار» پایدار برای این دستگاه بسازد (بدون نیاز به
    //      دسترسی ادمین و بدون وابستگی خارجی).
    //   ۲. توکن لایسنس را (که به‌صورت یک رشته‌ی Base64 به کاربر داده می‌شود)
    //      رمزگشایی، امضایش را با HMAC-SHA256 بررسی و انقضا/دستگاه را چک کند.
    //   ۳. توکن معتبر را در تنظیمات (TblAppSettings) ذخیره کند تا در اجراهای
    //      بعدی وضعیت لایسنس بدون نیاز به فعال‌سازی مجدد در دسترس باشد.
    //
    // قالب توکن:  Base64Url(payload) + "." + Base64Url(HMAC-SHA256(payload))
    // قالب payload (با | جدا):
    //   LicensedTo | Edition | IssuedAt(yyyy-MM-dd) | ExpiresAt(yyyy-MM-dd یا -) | HardwareId(یا ANY) | Features
    //
    // نکته امنیتی: در یک محصول واقعی برای جلوگیری از جعل توکن باید از امضای
    // نامتقارن (RSA — کلید خصوصی نزد فروشنده، کلید عمومی داخل برنامه) استفاده
    // شود. اینجا برای «زیرساخت» از HMAC با کلید نهفته استفاده شده تا کل خط
    // لوله (تولید/اعتبارسنجی/ذخیره) بدون وابستگی خارجی قابل‌آزمایش باشد؛ جای‌گزینی
    // با RSA بعداً فقط این کلاس را تغییر می‌دهد و بقیه برنامه دست‌نخورده می‌ماند.
    // ─────────────────────────────────────────────────────────────────────────
    public static class LicenseManager
    {
        // کلید امضای نهفته — در نسخه واقعی با RSA جایگزین می‌شود.
        private static readonly byte[] SigningKey =
            Encoding.UTF8.GetBytes("Guardian-OMS-License-Infra-v1-DO-NOT-SHIP-AS-IS");

        private const string LicenseTokenSettingKey = "LicenseToken";

        private static LicenseInfo _cached;
        private static bool _loaded;
        private static readonly object _lock = new object();

        // ─── شناسه سخت‌افزار پایدار این دستگاه ───────────────────────────────
        // ترکیب MachineGuid ویندوز (پایدار تا نصب مجدد OS، خواندنش نیاز به ادمین
        // ندارد) + نام دستگاه؛ سپس SHA-256 و کوتاه‌سازی به یک رشته خوانا.
        public static string GetHardwareId()
        {
            string seed = ReadMachineGuid() + "|" + SafeMachineName();
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(seed));
                // ۲۰ کاراکتر هگز با خط تیره هر ۵ تا — خوانا برای خواندن تلفنی/ایمیل.
                string hex = BitConverter.ToString(hash).Replace("-", "").Substring(0, 20).ToUpperInvariant();
                return hex.Substring(0, 5) + "-" + hex.Substring(5, 5) + "-" +
                       hex.Substring(10, 5) + "-" + hex.Substring(15, 5);
            }
        }

        private static string ReadMachineGuid()
        {
            // HKLM\SOFTWARE\Microsoft\Cryptography\MachineGuid — پایدار و خواندنی بدون ادمین.
            try
            {
                using (RegistryKey key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                    .OpenSubKey(@"SOFTWARE\Microsoft\Cryptography"))
                {
                    if (key != null)
                    {
                        object val = key.GetValue("MachineGuid");
                        if (val != null && !string.IsNullOrWhiteSpace(val.ToString()))
                            return val.ToString();
                    }
                }
            }
            catch { /* دسترسی/کلید موجود نبود؛ به fallback می‌رویم */ }

            // Fallback: نام دستگاه + نام کاربر (کمتر پایدار ولی همیشه در دسترس).
            return SafeMachineName() + "|" + SafeUserName();
        }

        private static string SafeMachineName()
        {
            try { return Environment.MachineName ?? ""; } catch { return ""; }
        }

        private static string SafeUserName()
        {
            try { return Environment.UserName ?? ""; } catch { return ""; }
        }

        // ─── وضعیت فعلی لایسنس (با کش) ───────────────────────────────────────
        public static LicenseInfo Current
        {
            get
            {
                if (_loaded) return _cached;
                lock (_lock)
                {
                    if (_loaded) return _cached;
                    _cached = LoadFromSettings();
                    _loaded = true;
                }
                return _cached;
            }
        }

        public static bool IsLicensed
        {
            get { return Current != null && Current.Status == LicenseStatus.Active; }
        }

        private static LicenseInfo LoadFromSettings()
        {
            string token = SettingsHelper.Get(LicenseTokenSettingKey);
            if (string.IsNullOrWhiteSpace(token))
                return new LicenseInfo { Status = LicenseStatus.Trial };

            LicenseInfo info = Validate(token);
            return info;
        }

        // ─── فعال‌سازی با توکن ورودی کاربر ───────────────────────────────────
        // اگر توکن معتبر بود ذخیره و true برمی‌گرداند؛ در غیر این صورت پیام خطا
        // در out message و false. هیچ قابلیتی در هیچ حالت مسدود نمی‌شود.
        public static bool Activate(string token, out string message)
        {
            token = (token ?? "").Trim();
            if (token.Length == 0)
            {
                message = "توکن لایسنس خالی است.";
                return false;
            }

            LicenseInfo info = Validate(token);
            switch (info.Status)
            {
                case LicenseStatus.Active:
                    SettingsHelper.Set(LicenseTokenSettingKey, token);
                    Invalidate();
                    message = "لایسنس با موفقیت فعال شد: " + info.StatusDisplay;
                    return true;
                case LicenseStatus.Expired:
                    // توکن معتبر ولی منقضی — ذخیره می‌شود تا وضعیت درست نمایش داده شود.
                    SettingsHelper.Set(LicenseTokenSettingKey, token);
                    Invalidate();
                    message = "این لایسنس منقضی شده است.";
                    return false;
                case LicenseStatus.MachineMismatch:
                    message = "این لایسنس برای دستگاه دیگری صادر شده است.";
                    return false;
                default:
                    message = "توکن لایسنس نامعتبر است.";
                    return false;
            }
        }

        // پاک کردن لایسنس ذخیره‌شده (بازگشت به حالت آزمایشی).
        public static void Deactivate()
        {
            SettingsHelper.Set(LicenseTokenSettingKey, "");
            Invalidate();
        }

        public static void Invalidate()
        {
            lock (_lock)
            {
                _loaded = false;
                _cached = null;
            }
        }

        // ─── اعتبارسنجی توکن (بدون ذخیره) ────────────────────────────────────
        public static LicenseInfo Validate(string token)
        {
            var info = new LicenseInfo { Status = LicenseStatus.Invalid, RawToken = token };
            if (string.IsNullOrWhiteSpace(token)) return info;

            int dot = token.IndexOf('.');
            if (dot <= 0 || dot >= token.Length - 1) return info;

            string payloadB64 = token.Substring(0, dot);
            string sigB64 = token.Substring(dot + 1);

            byte[] payloadBytes, sigBytes;
            try
            {
                payloadBytes = FromBase64Url(payloadB64);
                sigBytes = FromBase64Url(sigB64);
            }
            catch { return info; }

            // بررسی امضا با مقایسه‌ی زمان‌ثابت (جلوگیری از timing attack).
            byte[] expected;
            using (var hmac = new HMACSHA256(SigningKey))
                expected = hmac.ComputeHash(payloadBytes);

            if (!FixedTimeEquals(expected, sigBytes))
                return info; // امضا مخدوش = Invalid

            string payload = Encoding.UTF8.GetString(payloadBytes);
            string[] parts = payload.Split('|');
            if (parts.Length < 6) return info;

            info.LicensedTo = parts[0];
            info.Edition = parts[1];
            info.IssuedAt = ParseDate(parts[2]) ?? DateTime.MinValue;
            info.ExpiresAt = parts[3] == "-" ? (DateTime?)null : ParseDate(parts[3]);
            info.HardwareId = parts[4];
            info.Features = parts[5];

            // بررسی دستگاه
            if (!string.Equals(info.HardwareId, "ANY", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(info.HardwareId, GetHardwareId(), StringComparison.OrdinalIgnoreCase))
            {
                info.Status = LicenseStatus.MachineMismatch;
                return info;
            }

            // بررسی انقضا
            if (info.ExpiresAt != null && info.ExpiresAt.Value.Date < DateTime.Today)
            {
                info.Status = LicenseStatus.Expired;
                return info;
            }

            info.Status = LicenseStatus.Active;
            return info;
        }

        // ─── تولید توکن (ابزار فروشنده) ──────────────────────────────────────
        // آموزش: در محصول واقعی این متد فقط سمت فروشنده اجرا می‌شود، نه داخل
        // برنامه‌ی مشتری. اینجا برای کامل بودن زیرساخت و تست قرار داده شده تا
        // بتوان یک توکن نمونه ساخت. (امضا با همان کلید نهفته انجام می‌شود.)
        public static string GenerateToken(string licensedTo, string edition,
            DateTime issuedAt, DateTime? expiresAt, string hardwareId, string features)
        {
            string payload = string.Join("|", new[]
            {
                licensedTo ?? "",
                edition ?? "Standard",
                issuedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                expiresAt == null ? "-" : expiresAt.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                string.IsNullOrWhiteSpace(hardwareId) ? "ANY" : hardwareId,
                features ?? ""
            });

            byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
            byte[] sig;
            using (var hmac = new HMACSHA256(SigningKey))
                sig = hmac.ComputeHash(payloadBytes);

            return ToBase64Url(payloadBytes) + "." + ToBase64Url(sig);
        }

        // ─── کمکی‌ها ─────────────────────────────────────────────────────────
        private static DateTime? ParseDate(string s)
        {
            DateTime d;
            if (DateTime.TryParseExact((s ?? "").Trim(), "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
                return d;
            return null;
        }

        private static string ToBase64Url(byte[] data)
        {
            return Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private static byte[] FromBase64Url(string s)
        {
            string b64 = s.Replace('-', '+').Replace('_', '/');
            switch (b64.Length % 4)
            {
                case 2: b64 += "=="; break;
                case 3: b64 += "="; break;
            }
            return Convert.FromBase64String(b64);
        }

        // مقایسه‌ی زمان‌ثابت (بایت‌به‌بایت) — .NET Framework 4.7.2 متد داخلی ندارد.
        private static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++)
                diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}
