using System;
using System.Data.SQLite;
using System.IO;

namespace CaseManagement.Helpers
{
    // آموزش — کیفیت کد: این متدها قبلاً به‌صورت کپی‌شده در FrmFamily.cs و
    // FrmDocs.cs جداگانه تعریف شده بودند (دقیقاً یکسان). این‌جا یک‌بار
    // نگه‌داری می‌شوند؛ فایل‌های فراخوان با «using static» همان امضای
    // بدون‌پیشوند قبلی را حفظ می‌کنند، پس هیچ محل فراخوانی تغییر نمی‌کند.
    public static class SqlHelpers
    {
        public static bool IsPathInsideFolder(string path, string folder)
        {
            try
            {
                string fullPath = Path.GetFullPath(path);
                string fullFolder = Path.GetFullPath(folder)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;

                return fullPath.StartsWith(fullFolder, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public static bool AreSamePath(string firstPath, string secondPath)
        {
            if (string.IsNullOrWhiteSpace(firstPath) || string.IsNullOrWhiteSpace(secondPath))
                return false;

            try
            {
                string first = Path.GetFullPath(firstPath)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                string second = Path.GetFullPath(secondPath)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                return string.Equals(first, second, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public static string DbString(object value)
        {
            if (value == null || value == DBNull.Value)
                return "";

            return value.ToString();
        }

        public static void AddInt(SQLiteCommand cmd, string name, int value)
        {
            cmd.Parameters.AddWithValue(name, value);
        }

        public static void AddNVarChar(SQLiteCommand cmd, string name, string value, int length)
        {
            if (value == null)
                value = "";

            if (value.Length > length)
                throw new InvalidOperationException("طول مقدار " + name + " بیش از حد مجاز است.");

            cmd.Parameters.AddWithValue(name, value);
        }

        public static void AddNVarCharMax(SQLiteCommand cmd, string name, string value)
        {
            if (value == null)
                value = "";

            cmd.Parameters.AddWithValue(name, value);
        }
    }
}
