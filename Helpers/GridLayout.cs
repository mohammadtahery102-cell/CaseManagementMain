using System;
using System.Drawing;
using System.Windows.Forms;

namespace CaseManagement.Helpers
{
    // چیدمان فشردهٔ DataGridView: فونت خوانا، ردیف کوتاه‌تر، عرض ستون بر اساس
    // نوع و محتوا. Fill حذف می‌شود تا ستون کوتاه (کد/سن/جنسیت) نصف صفحه را
    // نگیرد. هیچ رویداد/فیلتر/ستون منطقی‌ای حذف نمی‌شود.
    public static class GridLayout
    {
        private const int ImageRowMinHeight = 74;
        private const int SampleRows = 48;
        private const string LayoutHiddenPhoto = "GridLayout.HiddenPhoto";

        public static void Apply(DataGridView grid)
        {
            if (grid == null) return;

            GridDisplayPrefs prefs = GridDisplaySettings.CurrentFor(grid);
            ApplyChrome(grid, prefs);
            FitColumns(grid, prefs);
        }

        public static void ApplyChrome(DataGridView grid)
        {
            ApplyChrome(grid, GridDisplaySettings.CurrentFor(grid));
        }

        public static void ApplyChrome(DataGridView grid, GridDisplayPrefs prefs)
        {
            if (grid == null || prefs == null) return;

            grid.ColumnHeadersDefaultCellStyle.Font = UiTheme.FontBold(prefs.HeaderFont);
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(2, 0, 2, 0);
            grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersHeight = Scale(grid, prefs.HeaderHeight);

            grid.DefaultCellStyle.Font = UiTheme.Font(Math.Max(GridDisplaySettings.MinCellFont, prefs.CellFont));
            grid.DefaultCellStyle.Padding = new Padding(
                prefs.CellPadding.Left, prefs.CellPadding.Top,
                prefs.CellPadding.Right, prefs.CellPadding.Bottom);
            grid.DefaultCellStyle.WrapMode = DataGridViewTriState.False;

            int rowHeight = Scale(grid, prefs.RowHeight);
            if (HasVisibleImageColumn(grid))
                rowHeight = Math.Max(rowHeight, Scale(grid, ImageRowMinHeight));

            grid.RowTemplate.Height = rowHeight;
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.IsNewRow) continue;
                if (row.Height != rowHeight)
                    row.Height = rowHeight;
            }

            grid.AllowUserToResizeRows = false;
        }

        [ThreadStatic]
        private static bool _fitting;

        public static void FitColumns(DataGridView grid)
        {
            FitColumns(grid, GridDisplaySettings.CurrentFor(grid));
        }

        public static void FitColumns(DataGridView grid, GridDisplayPrefs prefs)
        {
            if (grid == null || grid.Columns.Count == 0) return;
            if (_fitting) return;
            _fitting = true;
            try
            {
                FitColumnsCore(grid, prefs);
            }
            finally
            {
                _fitting = false;
            }
        }

        private static void FitColumnsCore(DataGridView grid, GridDisplayPrefs prefs)
        {
            if (prefs == null) prefs = GridDisplaySettings.Defaults();
            if (!prefs.AutoFit)
            {
                CapFilledColumns(grid);
                return;
            }

            RestoreLayoutHiddenPhotos(grid);

            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

            Font cellFont = grid.DefaultCellStyle.Font ?? grid.Font;
            Font headerFont = grid.ColumnHeadersDefaultCellStyle.Font ?? cellFont;

            using (Graphics g = SafeGraphics(grid))
            {
                if (g == null) return;
                foreach (DataGridViewColumn col in grid.Columns)
                {
                    if (!col.Visible) continue;
                    if (col is DataGridViewImageColumn)
                    {
                        col.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                        continue;
                    }

                    ColumnKind kind = Classify(col);
                    int keyMin = Scale(grid, KeyMinWidth(col));
                    int min = Math.Max(Scale(grid, kind.Min), keyMin);
                    int max = Scale(grid, kind.Max);
                    int headerPref = PreferredHeaderWidth(grid, col, g, headerFont);
                    int cellPref = MeasurePreferred(g, grid, col, cellFont, headerFont, min);
                    int preferred = Math.Max(min, Math.Max(headerPref, cellPref));
                    int cap = Math.Max(headerPref, max);
                    if (keyMin == 0 && preferred > cap)
                        preferred = cap;
                    preferred = Math.Max(min, preferred);

                    col.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                    col.MinimumWidth = min;
                    col.FillWeight = 1f;
                    col.Width = preferred;
                }
            }

            DemotePhotosIfNeeded(grid);
            PlacePhotosLast(grid);
            DistributeLeftover(grid);
        }

        private static void RestoreLayoutHiddenPhotos(DataGridView grid)
        {
            foreach (DataGridViewColumn col in grid.Columns)
            {
                if (col is DataGridViewImageColumn
                    && object.Equals(col.Tag, LayoutHiddenPhoto))
                {
                    col.Visible = true;
                    col.Tag = null;
                }
            }
        }

        private static void PlacePhotosLast(DataGridView grid)
        {
            int last = grid.Columns.Count - 1;
            for (int i = grid.Columns.Count - 1; i >= 0; i--)
            {
                DataGridViewColumn col = grid.Columns[i];
                if (!col.Visible || !(col is DataGridViewImageColumn)) continue;
                col.DisplayIndex = last;
                last--;
            }
        }

        private static void DemotePhotosIfNeeded(DataGridView grid)
        {
            int available = AvailableWidth(grid);
            if (available < 80) return;

            while (UsedWidth(grid) > available)
            {
                DataGridViewColumn photo = null;
                foreach (DataGridViewColumn col in grid.Columns)
                {
                    if (col.Visible && col is DataGridViewImageColumn)
                    {
                        photo = col;
                        break;
                    }
                }
                if (photo == null) break;
                photo.Visible = false;
                photo.Tag = LayoutHiddenPhoto;
            }
        }

        private static int AvailableWidth(DataGridView grid)
        {
            int width = grid.ClientSize.Width;
            if (grid.RowHeadersVisible) width -= grid.RowHeadersWidth;
            if (grid.Controls != null)
            {
                foreach (Control child in grid.Controls)
                {
                    if (child is VScrollBar && child.Visible)
                        width -= child.Width;
                }
            }
            return Math.Max(40, width - 4);
        }

        private static int UsedWidth(DataGridView grid)
        {
            int used = 0;
            foreach (DataGridViewColumn col in grid.Columns)
            {
                if (col.Visible) used += col.Width;
            }
            return used;
        }

        private static int KeyMinWidth(DataGridViewColumn col)
        {
            string name = ((col.Name ?? "") + " " + (col.HeaderText ?? "")).Trim();
            if (ContainsAny(name, "کد اختصاصی") || string.Equals(col.Name, "Code", StringComparison.OrdinalIgnoreCase))
                return 100;
            if (ContainsAny(name, "نام سرپرست") || string.Equals(col.Name, "HeadFullName", StringComparison.OrdinalIgnoreCase))
                return 140;
            if (ContainsAny(name, "شماره تماس") || string.Equals(col.Name, "Phone", StringComparison.OrdinalIgnoreCase))
                return 110;
            if (ContainsAny(name, "شماره تذکره", "HeadTazkiraNo") || string.Equals(col.Name, "HeadTazkiraNo", StringComparison.OrdinalIgnoreCase))
                return 110;
            return 0;
        }

        private static void CapFilledColumns(DataGridView grid)
        {
            foreach (DataGridViewColumn col in grid.Columns)
            {
                if (!col.Visible || col is DataGridViewImageColumn) continue;
                ColumnKind kind = Classify(col);
                int max = Scale(grid, kind.Max);
                if (col.Width > max)
                    col.Width = max;
            }
        }

        private static void DistributeLeftover(DataGridView grid)
        {
            int available = grid.ClientSize.Width;
            if (available < 80) return;

            int used = grid.RowHeadersVisible ? grid.RowHeadersWidth : 0;
            used += 2;
            int longCount = 0;
            foreach (DataGridViewColumn col in grid.Columns)
            {
                if (!col.Visible) continue;
                used += col.Width;
                if (!(col is DataGridViewImageColumn) && Classify(col).IsLong)
                    longCount++;
            }

            int extra = available - used;
            if (extra < 12 || longCount == 0) return;

            int share = extra / longCount;
            foreach (DataGridViewColumn col in grid.Columns)
            {
                if (!col.Visible || col is DataGridViewImageColumn) continue;
                ColumnKind kind = Classify(col);
                if (!kind.IsLong) continue;
                int max = Scale(grid, kind.Max + 80);
                col.Width = Math.Min(max, col.Width + share);
            }
        }

        private static int PreferredHeaderWidth(DataGridView grid, DataGridViewColumn col, Graphics g, Font headerFont)
        {
            int measured = MeasureText(g, col.HeaderText, headerFont) + 24;
            try
            {
                int native = col.GetPreferredWidth(DataGridViewAutoSizeColumnMode.ColumnHeader, true);
                if (native > measured) measured = native;
            }
            catch { }
            return measured;
        }

        private static Graphics SafeGraphics(Control control)
        {
            try { return control.CreateGraphics(); }
            catch { return null; }
        }

        private static int MeasurePreferred(Graphics g, DataGridView grid, DataGridViewColumn col,
            Font cellFont, Font headerFont, int min)
        {
            int header = MeasureText(g, col.HeaderText, headerFont) + 18;
            int cells = min;
            int seen = 0;
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.IsNewRow) continue;
                if (seen >= SampleRows) break;
                object value = row.Cells[col.Index].FormattedValue;
                string text = value == null ? "" : Convert.ToString(value);
                int w = MeasureText(g, text, cellFont) + 16;
                if (w > cells) cells = w;
                seen++;
            }
            return Math.Max(header, cells);
        }

        private static int MeasureText(Graphics g, string text, Font font)
        {
            if (string.IsNullOrEmpty(text) || font == null) return 0;
            Size size = TextRenderer.MeasureText(text, font);
            return size.Width + 8;
        }

        private static bool HasVisibleImageColumn(DataGridView grid)
        {
            foreach (DataGridViewColumn col in grid.Columns)
            {
                if (col.Visible && col is DataGridViewImageColumn)
                    return true;
            }
            return false;
        }

        private static int Scale(Control control, int px)
        {
            int dpi = 96;
            try { dpi = control.DeviceDpi; }
            catch { dpi = 96; }
            if (dpi <= 0) dpi = 96;
            return (int)Math.Round(px * dpi / 96.0);
        }

        private struct ColumnKind
        {
            public int Min;
            public int Max;
            public bool IsLong;
        }

        private static ColumnKind Classify(DataGridViewColumn col)
        {
            string name = ((col.Name ?? "") + " " + (col.HeaderText ?? "")).Trim();
            if (col is DataGridViewCheckBoxColumn)
                return new ColumnKind { Min = 36, Max = 48, IsLong = false };

            if (IsShort(name))
                return new ColumnKind { Min = 52, Max = 88, IsLong = false };
            if (IsLong(name))
                return new ColumnKind { Min = 96, Max = 210, IsLong = true };
            if (IsMedium(name))
                return new ColumnKind { Min = 70, Max = 128, IsLong = false };
            return new ColumnKind { Min = 80, Max = 150, IsLong = false };
        }

        private static bool IsShort(string text)
        {
            return ContainsAny(text,
                "Code", "کد", "FormNo", "Gender", "جنسیت", "Age", "سن",
                "CasID", "FamID", "DocID", "LogID", "ID", "شناسه",
                "Status", "وضعیت", "Count", "تعداد", "درجه", "Degree",
                "Enabled", "فعال", "نقش", "Role", "sel", "انتخاب");
        }

        private static bool IsMedium(string text)
        {
            return ContainsAny(text,
                "Phone", "تماس", "Province", "ولایت", "District", "ولسوالی",
                "Date", "تاریخ", "Type", "نوع", "DocType", "RequestType",
                "تذکره", "Tazkira");
        }

        private static bool IsLong(string text)
        {
            return ContainsAny(text,
                "Name", "نام", "Address", "آدرس", "Residence", "سکونت",
                "Description", "توضیحات", "شرح", "HeadFullName", "سرپرست",
                "OriginalFileName", "فایل", "پیام", "Message", "Path", "مسیر");
        }

        private static bool ContainsAny(string text, params string[] tokens)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (string token in tokens)
            {
                if (text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }
    }
}
