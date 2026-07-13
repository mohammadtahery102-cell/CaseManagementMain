using System;
using System.Data;
using System.Globalization;

namespace CaseManagement.Helpers
{
    public static class PersianDateHelper
    {
        private static readonly PersianCalendar _pc = new PersianCalendar();
        private static readonly CultureInfo _fa = new CultureInfo("fa-IR");

        public static string ToPersianDateString(DateTime dt)
        {
            // yyyy/MM/dd
            return string.Format("{0:D4}/{1:D2}/{2:D2}", _pc.GetYear(dt), _pc.GetMonth(dt), _pc.GetDayOfMonth(dt));
        }

        public static DateTime ParsePersianDate(string persianDate)
        {
            if (string.IsNullOrWhiteSpace(persianDate))
                return DateTime.MinValue;

            // Accept formats: yyyy/MM/dd or yyyy-MM-dd
            persianDate = persianDate.Trim().Replace('-', '/');
            var parts = persianDate.Split('/');
            if (parts.Length < 3)
                throw new FormatException("Invalid persian date format");

            int y = int.Parse(parts[0]);
            int m = int.Parse(parts[1]);
            int d = int.Parse(parts[2]);

            return _pc.ToDateTime(y, m, d, 0, 0, 0, 0);
        }

        // آموزش — رفع باگ «گاهی تاریخ میلادی/اشتباه نمایش داده می‌شود»:
        // تاریخ‌ها در دیتابیس به‌صورت میلادی ISO ("yyyy-MM-dd") ذخیره می‌شوند،
        // اما اگر با Convert.ToDateTime خوانده شوند، چون Program.cs کالچر ترد
        // را روی تقویم شمسی (fa-IR + PersianCalendar) گذاشته، سال میلادی
        // (مثلاً 2026) به‌عنوان سال شمسی تفسیر و به تاریخی کاملاً اشتباه
        // تبدیل می‌شود که سپس به شکل «۲۰۲۶/..» (شبیه میلادی) دیده می‌شود.
        // این متد همیشه با InvariantCulture (تقویم میلادی) پارس می‌کند تا
        // مقدار ذخیره‌شده درست خوانده و بعد به‌درستی شمسی نمایش داده شود.
        public static DateTime ParseStoredDate(object value, DateTime fallback)
        {
            if (value == null || value == DBNull.Value)
                return fallback;

            if (value is DateTime)
                return ((DateTime)value).Date;

            string s = value.ToString().Trim();
            if (string.IsNullOrEmpty(s))
                return fallback;

            DateTime dt;
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                return dt.Date;

            // تلاش دوم: اگر ورودی یک رشته شمسی (yyyy/MM/dd) باشد
            try { return ParsePersianDate(s).Date; }
            catch { return fallback; }
        }

        public static DateTime Today()
        {
            DateTime now = DateTime.Now;
            return _pc.ToDateTime(_pc.GetYear(now), _pc.GetMonth(now), _pc.GetDayOfMonth(now), now.Hour, now.Minute, now.Second, now.Millisecond);
        }

        public static string ToPersianDateTimeString(DateTime dt)
        {
            return ToPersianDateString(dt) + " " + dt.ToString("HH:mm:ss");
        }

        public static CultureInfo GetPersianCulture()
        {
            CultureInfo ci = (CultureInfo)_fa.Clone();
            ci.DateTimeFormat.Calendar = _pc;
            return ci;
        }

        // ─── تبدیل ستون‌های تاریخ یک DataTable به رشته شمسی — برای خروجی‌های
        // یک‌بار‌مصرف (اکسل/چاپ) که دیگر به دیتابیس بازنویسی نمی‌شوند؛
        // مقدار اصلی جدول تغییر می‌کند، پس روی نسخه‌ای که فقط برای Export/Print
        // ساخته شده (نه جدولی که مستقیماً به گرید bind شده) استفاده شود.
        public static void ConvertDateColumnsToPersian(DataTable table, params string[] columnNames)
        {
            if (table == null || columnNames == null)
                return;

            foreach (string columnName in columnNames)
            {
                if (!table.Columns.Contains(columnName))
                    continue;

                foreach (DataRow row in table.Rows)
                {
                    if (row[columnName] == DBNull.Value)
                        continue;

                    DateTime dt;
                    if (DateTime.TryParse(row[columnName].ToString(), out dt))
                        row[columnName] = ToPersianDateString(dt);
                }
            }
        }
    }
}
