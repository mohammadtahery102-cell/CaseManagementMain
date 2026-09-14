using System;
using System.Globalization;
using System.Windows.Forms;

namespace CaseManagement.Helpers
{
    // تنظیمات نمایش DataGridView، به تفکیک کاربر و فرم.
    // کلید: GridDisplay.u{UserId}[.{FormKey}].{leaf}
    // اگر برای فرم مقدار نباشد، پیش‌فرض همان کاربر و بعد پیش‌فرض برنامه خوانده می‌شود.
    public sealed class GridDisplayPrefs
    {
        public GridDensity Density;
        public float CellFont;
        public int RowHeight;
        public bool AutoFit;
        public float HeaderFont { get { return Math.Min(11f, Math.Max(10f, CellFont + 1f)); } }
        public int HeaderHeight
        {
            get
            {
                if (Density == GridDensity.Compact) return 28;
                if (Density == GridDensity.Large) return 36;
                return 32;
            }
        }
        public PaddingLike CellPadding
        {
            get
            {
                if (Density == GridDensity.Compact) return new PaddingLike(2, 0, 2, 0);
                if (Density == GridDensity.Large) return new PaddingLike(4, 1, 4, 1);
                return new PaddingLike(3, 0, 3, 0);
            }
        }

        public struct PaddingLike
        {
            public readonly int Left, Top, Right, Bottom;
            public PaddingLike(int left, int top, int right, int bottom)
            {
                Left = left; Top = top; Right = right; Bottom = bottom;
            }
        }
    }

    public enum GridDensity
    {
        Compact = 0,
        Normal = 1,
        Large = 2
    }

    public static class GridDisplaySettings
    {
        public const string DensityKey = "Density";
        public const string FontSizeKey = "FontSize";
        public const string RowHeightKey = "RowHeight";
        public const string AutoFitKey = "AutoFit";
        public const float MinCellFont = 9f;
        public const float MaxCellFont = 12f;

        public const string DefaultFormKey = "";
        public static readonly string[] FormKeys =
        {
            DefaultFormKey, "FrmCase", "FrmFamily", "FrmDocs", "FrmDashboard", "FrmFinance"
        };

        public static readonly string[] FormTitles =
        {
            "پیش‌فرض همه فرم‌ها",
            "فهرست پرونده‌ها",
            "اعضای خانواده",
            "اسناد",
            "داشبورد",
            "پرداخت‌ها"
        };

        public static GridDisplayPrefs Current
        {
            get { return LoadFor(SecurityContext.UserId, DefaultFormKey); }
        }

        public static GridDisplayPrefs CurrentFor(Control control)
        {
            return LoadFor(SecurityContext.UserId, FormKeyFrom(control));
        }

        public static string FormKeyFrom(Control control)
        {
            if (control == null) return DefaultFormKey;
            Form form = control as Form ?? control.FindForm();
            if (form == null) return DefaultFormKey;
            string name = form.GetType().Name;
            foreach (string key in FormKeys)
            {
                if (key.Length > 0 && string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
                    return key;
            }
            return name ?? DefaultFormKey;
        }

        public static GridDisplayPrefs Defaults()
        {
            return FromDensity(GridDensity.Normal);
        }

        public static GridDisplayPrefs FromDensity(GridDensity density)
        {
            var prefs = new GridDisplayPrefs { Density = density, AutoFit = true };
            switch (density)
            {
                case GridDensity.Compact:
                    prefs.CellFont = MinCellFont;
                    prefs.RowHeight = 24;
                    break;
                case GridDensity.Large:
                    prefs.CellFont = 10.5f;
                    prefs.RowHeight = 34;
                    break;
                default:
                    prefs.CellFont = 9.5f;
                    prefs.RowHeight = 28;
                    break;
            }
            return prefs;
        }

        public static GridDisplayPrefs LoadFor(int userId)
        {
            return LoadFor(userId, DefaultFormKey);
        }

        public static GridDisplayPrefs LoadFor(int userId, string formKey)
        {
            formKey = NormalizeFormKey(formKey);
            GridDensity density = ParseDensity(Read(userId, formKey, DensityKey, ""));
            GridDisplayPrefs prefs = FromDensity(density);

            float font;
            if (TryParseFloat(Read(userId, formKey, FontSizeKey, ""), out font))
                prefs.CellFont = Clamp(font, MinCellFont, MaxCellFont);
            else
                prefs.CellFont = Math.Max(MinCellFont, prefs.CellFont);

            int row;
            if (int.TryParse(Read(userId, formKey, RowHeightKey, ""), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out row))
                prefs.RowHeight = ClampInt(row, 22, 48);

            string auto = Read(userId, formKey, AutoFitKey, "");
            if (auto.Length > 0)
                prefs.AutoFit = auto == "1" || auto.Equals("true", StringComparison.OrdinalIgnoreCase);

            return prefs;
        }

        public static void SaveFor(int userId, GridDisplayPrefs prefs)
        {
            SaveFor(userId, DefaultFormKey, prefs);
        }

        public static void SaveFor(int userId, string formKey, GridDisplayPrefs prefs)
        {
            if (prefs == null) prefs = Defaults();
            prefs.CellFont = Clamp(prefs.CellFont, MinCellFont, MaxCellFont);
            formKey = NormalizeFormKey(formKey);
            Write(userId, formKey, DensityKey, DensityName(prefs.Density));
            Write(userId, formKey, FontSizeKey, prefs.CellFont.ToString("0.##", CultureInfo.InvariantCulture));
            Write(userId, formKey, RowHeightKey, prefs.RowHeight.ToString(CultureInfo.InvariantCulture));
            Write(userId, formKey, AutoFitKey, prefs.AutoFit ? "1" : "0");
        }

        public static string DensityName(GridDensity density)
        {
            switch (density)
            {
                case GridDensity.Compact: return "Compact";
                case GridDensity.Large: return "Large";
                default: return "Normal";
            }
        }

        public static GridDensity ParseDensity(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return GridDensity.Normal;
            if (raw.Equals("Compact", StringComparison.OrdinalIgnoreCase) || raw == "0")
                return GridDensity.Compact;
            if (raw.Equals("Large", StringComparison.OrdinalIgnoreCase) || raw == "2")
                return GridDensity.Large;
            return GridDensity.Normal;
        }

        public static string NormalizeFormKey(string formKey)
        {
            return string.IsNullOrWhiteSpace(formKey) ? DefaultFormKey : formKey.Trim();
        }

        private static string Key(int userId, string formKey, string leaf)
        {
            if (string.IsNullOrEmpty(formKey))
                return "GridDisplay.u" + userId + "." + leaf;
            return "GridDisplay.u" + userId + "." + formKey + "." + leaf;
        }

        private static string Read(int userId, string formKey, string leaf, string fallback)
        {
            if (!string.IsNullOrEmpty(formKey))
            {
                string formValue = SettingsHelper.Get(Key(userId, formKey, leaf), null);
                if (!string.IsNullOrEmpty(formValue)) return formValue;
            }

            string userValue = SettingsHelper.Get(Key(userId, DefaultFormKey, leaf), null);
            if (!string.IsNullOrEmpty(userValue)) return userValue;

            return SettingsHelper.Get("GridDisplay." + leaf, fallback);
        }

        private static void Write(int userId, string formKey, string leaf, string value)
        {
            SettingsHelper.Set(Key(userId, formKey, leaf), value ?? "");
        }

        private static bool TryParseFloat(string raw, out float value)
        {
            return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                || float.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static int ClampInt(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
