using System;
using Microsoft.Win32;

namespace CaseManagement.Helpers
{
    // ترجیحات ظاهری ERP — فقط کلیدهای TblAppSettings موجود؛ بدون تغییر اسکیما.
    public static class ErpUiPrefs
    {
        public const string ThemeKey = "ErpUiTheme";
        public const string SidebarKey = "ErpSidebarCollapsed";
        public const string FontScaleKey = "ErpFontScale";
        public const string DensityKey = "ErpDashboardDensity";

        public const int ExpandedWidth = 240;
        public const int CollapsedWidth = 70;

        public static string Theme
        {
            get { return NormalizeTheme(SettingsHelper.Get(ThemeKey, "light")); }
            set { SettingsHelper.Set(ThemeKey, NormalizeTheme(value)); }
        }

        public static bool SidebarCollapsed
        {
            get { return SettingsHelper.Get(SidebarKey, "0") == "1"; }
            set { SettingsHelper.Set(SidebarKey, value ? "1" : "0"); }
        }

        public static int FontScale
        {
            get
            {
                int n = SettingsHelper.GetInt(FontScaleKey, 100);
                if (n < 90) return 90;
                if (n > 115) return 115;
                return n;
            }
            set { SettingsHelper.Set(FontScaleKey, value.ToString()); }
        }

        public static string Density
        {
            get
            {
                string d = (SettingsHelper.Get(DensityKey, "compact") ?? "").Trim().ToLowerInvariant();
                if (d == "comfortable" || d == "spacious") return d;
                return "compact";
            }
            set
            {
                string d = (value ?? "").Trim().ToLowerInvariant();
                if (d != "comfortable" && d != "spacious") d = "compact";
                SettingsHelper.Set(DensityKey, d);
            }
        }

        public static string ResolvedTheme()
        {
            return Theme;
        }

        public static string NormalizeTheme(string raw)
        {
            string t = (raw ?? "").Trim().ToLowerInvariant();
            if (t == "dark" || t == "classic-dark") return "dark";
            if (t == "blue" || t == "modern-blue") return "blue";
            if (t == "green" || t == "modern-green") return "green";
            if (t == "gold" || t == "business-gold") return "gold";
            if (t == "gray" || t == "executive-gray") return "gray";
            return "light";
        }

        public static bool SystemUsesLightTheme()
        {
            try
            {
                object v = Registry.GetValue(
                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                    "AppsUseLightTheme", 1);
                if (v == null) return true;
                return Convert.ToInt32(v) != 0;
            }
            catch { return true; }
        }
    }
}
