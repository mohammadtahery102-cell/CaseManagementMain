using System;
using System.Data.SQLite;

namespace CaseManagement.AI
{
    // کمکیِ کوچکِ مشترک برای ساختِ SQLiteParameter — DatabaseHelper.ExecuteNonQuery/
    // ExecuteInsertReturningId آرایه‌ای از SQLiteParameter می‌خواهند (نه
    // AddWithValue روی یک SQLiteCommand موجود)، پس این کمکی جایگزینِ الگوی
    // «new SQLiteParameter(name, value)» است که در همه‌ی نگارش‌های
    // System.Data.SQLite تضمین‌شده نیست؛ خصوصیت‌های ParameterName/Value همیشه
    // موجودند.
    internal static class SqlParam
    {
        public static SQLiteParameter P(string name, object value)
        {
            SQLiteParameter p = new SQLiteParameter();
            p.ParameterName = name;
            p.Value = value ?? DBNull.Value;
            return p;
        }
    }
}
