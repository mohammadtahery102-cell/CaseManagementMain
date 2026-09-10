using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace CaseManagement.Helpers
{
    public sealed class ErpQuickActionDto
    {
        public bool enabled { get; set; }
        public string title { get; set; }
        public string icon { get; set; }
        public string color { get; set; }
        public string dest { get; set; }
        public string shortcut { get; set; }
    }

    public static class ErpQuickActions
    {
        public const string SettingsKey = "ErpQuickActions";

        public static List<ErpQuickActionDto> Load()
        {
            try
            {
                string raw = SettingsHelper.Get(SettingsKey, "");
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    var list = new JavaScriptSerializer().Deserialize<List<ErpQuickActionDto>>(raw);
                    if (list != null && list.Count > 0)
                        return Normalize(list);
                }
            }
            catch { }
            return Defaults();
        }

        public static void Save(List<ErpQuickActionDto> items)
        {
            SettingsHelper.Set(SettingsKey, new JavaScriptSerializer().Serialize(Normalize(items)));
        }

        public static List<ErpQuickActionDto> Defaults()
        {
            return new List<ErpQuickActionDto>
            {
                Item(true, "ثبت سند", "journal", "#2563EB", "journal", "F1"),
                Item(true, "صدور فاکتور", "invoice", "#0F766E", "invoice", "F2"),
                Item(true, "دریافت وجه", "receive", "#D97706", "receive", "F3"),
                Item(true, "گزارش مالی", "chart", "#7C3AED", "trial", "F4"),
                Item(true, "مشتریان", "people", "#0891B2", "crm", "F5")
            };
        }

        private static ErpQuickActionDto Item(bool on, string title, string icon, string color, string dest, string shortcut)
        {
            return new ErpQuickActionDto
            {
                enabled = on, title = title, icon = icon, color = color, dest = dest, shortcut = shortcut
            };
        }

        private static List<ErpQuickActionDto> Normalize(List<ErpQuickActionDto> items)
        {
            var result = new List<ErpQuickActionDto>();
            List<ErpQuickActionDto> fallback = Defaults();
            for (int i = 0; i < 5; i++)
            {
                ErpQuickActionDto src = (items != null && i < items.Count && items[i] != null) ? items[i] : fallback[i];
                if (string.IsNullOrWhiteSpace(src.title)) src.title = fallback[i].title;
                if (string.IsNullOrWhiteSpace(src.icon)) src.icon = fallback[i].icon;
                if (string.IsNullOrWhiteSpace(src.color)) src.color = fallback[i].color;
                if (string.IsNullOrWhiteSpace(src.dest)) src.dest = fallback[i].dest;
                if (string.IsNullOrWhiteSpace(src.shortcut)) src.shortcut = "F" + (i + 1);
                result.Add(src);
            }
            return result;
        }
    }
}
