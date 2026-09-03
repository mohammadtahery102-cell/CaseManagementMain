using System;
using System.Data.SQLite;
using CaseManagement.Helpers;

namespace CaseManagement.Sync
{
    // ═════════════════════════════════════════════════════════════════════════
    // شماره‌گذاری فرم با آگاهی از مرکز — پیش‌نیازِ کار آفلاینِ چندشعبه‌ای.
    //
    // مسئلهٔ واقعی (نتیجهٔ تحلیل فاز ۱): شمارهٔ فرم با
    // «MAX(FormNo) + 1» روی *کل* جدول ساخته می‌شود و ستون UNIQUE است. دو شعبه
    // که آفلاین کار می‌کنند هر دو شمارهٔ ۵۱۳ را می‌سازند و در همگام‌سازی یکی
    // با خطای یکتایی شکست می‌خورد.
    //
    // چرا «بلوکِ عددی» و نه «پیشوند متنی»:
    //   • FormNo از نوع INTEGER است و مصرف‌کننده‌های تایپ‌شده دارد — مثلاً
    //     CaseCardRepository با Convert.ToInt32 آن را می‌خواند و جست‌وجوی
    //     بازه‌ای در FrmCase آن را عددی مقایسه می‌کند. یک مقدار مثل
    //     "001-513" همان‌جا استثنا می‌داد.
    //   • بارکد، نام پوشهٔ پرونده و نگاشتِ عکس/سند همگی به «کد اختصاصی»
    //     (Code) وابسته‌اند، نه FormNo. با دست‌نخوردن آن‌ها، بارکد و
    //     همگام‌سازی رسانه دقیقاً مثل قبل کار می‌کنند.
    //
    // سیاست (طبق تصمیم صریح): مرکز اصلی دنبالهٔ فعلی‌اش را *بدون هیچ تغییری*
    // ادامه می‌دهد؛ فقط مراکز دیگر بلوک عددیِ جدا می‌گیرند. رکوردهای موجود
    // هرگز تغییر نمی‌کنند.
    //
    //   مرکز ۱ (اصلی) : ۱، ۲، ۳ … (دقیقاً رفتار قبلی)
    //   مرکز ۲        : ۲٬۰۰۰٬۰۰۱ به بعد
    //   مرکز ۳        : ۳٬۰۰۰٬۰۰۱ به بعد
    // ═════════════════════════════════════════════════════════════════════════
    public static class CaseNumbering
    {
        // اندازهٔ هر بلوک. یک میلیون شماره برای هر شعبه، با سقفِ INTEGER
        // ۶۴ بیتیِ SQLite جای فراوانی باقی می‌گذارد.
        public const int BlockSize = 1000000;

        // مرکزی که دنبالهٔ تاریخیِ برنامه به آن تعلق دارد و نباید جابه‌جا شود.
        public const int PrimaryCenterId = 1;

        // شروعِ بلوکِ یک مرکز. مرکز اصلی (و حالتِ «همه مراکز» که CenterID
        // مشخصی ندارد) بلوک ندارد ⇒ صفر یعنی «همان رفتار قبلی».
        public static long BlockStart(int centerId)
        {
            if (centerId <= PrimaryCenterId) return 0;
            return (long)centerId * BlockSize;
        }

        // شمارهٔ فرمِ بعدی برای مرکز داده‌شده، داخل همان تراکنشِ فراخوان.
        //
        // آموزش — چرا تراکنش پاس داده می‌شود: خواندنِ MAX و درجِ رکورد باید
        // اتمیک باشند، وگرنه دو کاربرِ هم‌زمان یک شماره می‌گیرند. این همان
        // الگویی است که DatabaseHelper.ExecuteInTransaction با
        // BeginImmediate تضمین می‌کند.
        public static int GetNextFormNo(SQLiteConnection con, SQLiteTransaction tr, int centerId)
        {
            long blockStart = BlockStart(centerId);

            // ── مرکز اصلی: دقیقاً همان کوئریِ قبلی، بدون هیچ تغییر رفتاری ──
            // (شمارهٔ فرم‌های غیرعددیِ قدیمی نادیده گرفته می‌شوند — رفتار موجود)
            string sql = blockStart == 0
                ? @"SELECT COALESCE(MAX(CAST(CASE WHEN FormNo GLOB '*[0-9]*' AND FormNo NOT GLOB '*[^0-9]*'
                                                  THEN FormNo ELSE '0' END AS INTEGER)), 0) + 1
                    FROM TblCase
                    WHERE CAST(CASE WHEN FormNo GLOB '*[0-9]*' AND FormNo NOT GLOB '*[^0-9]*'
                                    THEN FormNo ELSE '0' END AS INTEGER) < @BlockFloor"

                // ── مراکز دیگر: بیشینه *داخل همان بلوک* ──
                : @"SELECT COALESCE(MAX(CAST(CASE WHEN FormNo GLOB '*[0-9]*' AND FormNo NOT GLOB '*[^0-9]*'
                                                  THEN FormNo ELSE '0' END AS INTEGER)), 0) + 1
                    FROM TblCase
                    WHERE CAST(CASE WHEN FormNo GLOB '*[0-9]*' AND FormNo NOT GLOB '*[^0-9]*'
                                    THEN FormNo ELSE '0' END AS INTEGER) BETWEEN @BlockStart AND @BlockEnd";

            int next;
            using (var cmd = tr == null ? new SQLiteCommand(sql, con) : new SQLiteCommand(sql, con, tr))
            {
                if (blockStart == 0)
                {
                    // مرکز اصلی هرگز نباید به بلوکِ مراکز دیگر سرریز کند.
                    cmd.Parameters.AddWithValue("@BlockFloor", (long)2 * BlockSize);
                }
                else
                {
                    cmd.Parameters.AddWithValue("@BlockStart", blockStart);
                    cmd.Parameters.AddWithValue("@BlockEnd",   blockStart + BlockSize - 1);
                }

                object result = cmd.ExecuteScalar();
                long value = result == null || result == DBNull.Value ? 1 : Convert.ToInt64(result);

                // بلوکِ خالی ⇒ اولین شمارهٔ همان بلوک.
                if (blockStart > 0 && value <= blockStart) value = blockStart + 1;

                next = (int)value;
            }

            // ── تنظیم «شماره شروع پرونده» ──
            // این تنظیم برای مرکز اصلی دقیقاً مثل قبل اعمال می‌شود. برای بلوکِ
            // مراکز دیگر بی‌معناست (عددی به‌مراتب کوچک‌تر از بلوک) و اگر
            // اعمال می‌شد شمارهٔ خارج از بلوک تولید می‌کرد.
            if (blockStart == 0)
            {
                int start = SettingsHelper.GetInt(SettingsHelper.StartCaseNo, 0);
                if (start > next) next = start;
            }

            return next;
        }

        // نسخهٔ راحت: مرکزِ کاربر جاری.
        //
        // ⚠ حالت «همه مراکز» (SuperAdmin) مقدار CenterFilterId صفر می‌دهد؛ در
        // آن حالت رکورد به مرکز اصلی تعلق می‌گیرد و همان دنبالهٔ قبلی ادامه
        // پیدا می‌کند — یعنی رفتارِ امروز، بدون تغییر.
        public static int GetNextFormNo(SQLiteConnection con, SQLiteTransaction tr)
        {
            int centerId = SecurityContext.CurrentCenterId > 0
                ? SecurityContext.CurrentCenterId
                : PrimaryCenterId;

            return GetNextFormNo(con, tr, centerId);
        }
    }
}
