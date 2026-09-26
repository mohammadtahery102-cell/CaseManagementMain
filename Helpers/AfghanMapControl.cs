using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Windows.Forms;

namespace CaseManagement.Helpers
{
    // ═══════════════════════════════════════════════════════════════════════
    // AfghanMapControl — نقشهٔ حرارتیِ تعاملی با GDI+.
    //
    // چرخهٔ رسم در سه لایهٔ کش‌شده تا تعاملْ روان بماند:
    //   ۱) پارتیشن  : نگاشتِ پیکسل→منطقه. فقط با تغییرِ اندازه یا سطح دوباره
    //                 ساخته می‌شود (سنگین‌ترین مرحله).
    //   ۲) لایهٔ پایه: بیت‌مپِ رنگ‌آمیزی‌شده + مرزها. با تغییرِ داده یا معیارِ
    //                 نقشهٔ حرارتی دوباره ساخته می‌شود (سریع، LockBits).
    //   ۳) روکش     : برچسب‌ها، هایلایتِ hover، طیفِ رنگ. هر Paint.
    //
    // بدونِ این لایه‌بندی، حرکتِ ماوس روی نقشه هر بار کلِ پارتیشن را دوباره
    // می‌ساخت و کنترل کند می‌شد.
    // ═══════════════════════════════════════════════════════════════════════
    public sealed class AfghanMapControl : Control
    {
        // ─── داده ───────────────────────────────────────────────────────────
        private Dictionary<string, double> _values =
            new Dictionary<string, double>(StringComparer.Ordinal);
        private Dictionary<string, string> _tooltips =
            new Dictionary<string, string>(StringComparer.Ordinal);

        private string _metricTitle = "";
        private string _valueSuffix = "";

        // ─── وضعیت ──────────────────────────────────────────────────────────
        private AfghanMapGeometry.Partition _partition;
        private Bitmap _baseLayer;
        private int _hoverIndex = -1;
        private int _selectedIndex = -1;

        private string _drilledProvince = "";   // خالی = سطحِ کشور

        private const int MapPadding = 18;

        // ─── رویدادها ───────────────────────────────────────────────────────
        // نامِ منطقه (ولایت یا ولسوالی) + نامِ ولایتِ والد در سطح دوم.
        public event EventHandler<GeoRegionEventArgs> RegionSelected;
        public event EventHandler<GeoRegionEventArgs> RegionActivated;   // دوکلیک → پرونده‌ها
        public event EventHandler<GeoRegionEventArgs> RegionDrillRequested;

        public sealed class GeoRegionEventArgs : EventArgs
        {
            public string Region = "";
            public string Province = "";
            public bool IsDistrictLevel;
        }

        public AfghanMapControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);
            BackColor = UiTheme.CardBack;
            Font = UiTheme.Font(UiTheme.SizeSmall);
            DoubleBuffered = true;
        }

        // ─── API ────────────────────────────────────────────────────────────
        public string DrilledProvince { get { return _drilledProvince; } }
        public bool IsDistrictLevel { get { return _drilledProvince.Length > 0; } }
        public string SelectedRegion
        {
            get
            {
                if (_partition == null || _selectedIndex < 0 ||
                    _selectedIndex >= _partition.Names.Length) return "";
                return _partition.Names[_selectedIndex];
            }
        }

        public void SetData(Dictionary<string, double> values,
                            Dictionary<string, string> tooltips,
                            string metricTitle, string valueSuffix)
        {
            _values = values ?? new Dictionary<string, double>(StringComparer.Ordinal);
            _tooltips = tooltips ?? new Dictionary<string, string>(StringComparer.Ordinal);
            _metricTitle = metricTitle ?? "";
            _valueSuffix = valueSuffix ?? "";
            InvalidateBaseLayer();
            Invalidate();
        }

        // ورود به سطحِ ولسوالی. رشتهٔ خالی یعنی بازگشت به کلِ کشور.
        public void DrillTo(string province)
        {
            string target = province == null ? "" : province.Trim();
            if (string.Equals(_drilledProvince, target, StringComparison.Ordinal)) return;

            _drilledProvince = target;
            _selectedIndex = -1;
            _hoverIndex = -1;
            _partition = null;              // سطح عوض شد ⇒ پارتیشنِ تازه لازم است
            InvalidateBaseLayer();
            Invalidate();
        }

        public void SelectRegion(string region)
        {
            EnsurePartition();
            _selectedIndex = -1;
            if (_partition != null && !string.IsNullOrWhiteSpace(region))
            {
                for (int i = 0; i < _partition.Names.Length; i++)
                    if (string.Equals(_partition.Names[i], region.Trim(), StringComparison.Ordinal))
                    { _selectedIndex = i; break; }
            }
            Invalidate();
        }

        // تصویرِ نقشه برای گزارش/PDF — همان چیزی که روی صفحه دیده می‌شود،
        // ولی با ابعادِ دلخواه و پس‌زمینهٔ سفید (مناسبِ چاپ).
        public Bitmap RenderToBitmap(int width, int height)
        {
            AfghanMapGeometry.Partition part = _drilledProvince.Length > 0
                ? AfghanMapGeometry.BuildZoomedDistrictPartition(width, height, MapPadding, _drilledProvince)
                : AfghanMapGeometry.BuildProvincePartition(width, height, MapPadding);

            if (part == null)
                part = AfghanMapGeometry.BuildProvincePartition(width, height, MapPadding);

            Bitmap bmp = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                using (Bitmap layer = BuildBaseLayer(part, Color.White))
                    g.DrawImageUnscaled(layer, 0, 0);

                DrawLabels(g, part, -1, -1, true);
                DrawLegend(g, part, width, height);
            }
            return bmp;
        }

        // ═══════════════════════════════════════════════════════════════════
        // ساختِ لایه‌ها
        // ═══════════════════════════════════════════════════════════════════
        private void EnsurePartition()
        {
            if (_partition != null &&
                _partition.Width == Math.Max(1, ClientSize.Width) &&
                _partition.Height == Math.Max(1, ClientSize.Height)) return;

            int w = Math.Max(120, ClientSize.Width);
            int h = Math.Max(120, ClientSize.Height);

            AfghanMapGeometry.Partition provinces =
                AfghanMapGeometry.BuildProvincePartition(w, h, MapPadding);

            if (_drilledProvince.Length > 0)
            {
                // بزرگ‌نمایی‌شده تا ولایت کلِ بوم را پر کند و برچسبِ
                // ولسوالی‌ها روی هم نیفتد.
                AfghanMapGeometry.Partition districts =
                    AfghanMapGeometry.BuildZoomedDistrictPartition(w, h, MapPadding, _drilledProvince);
                _partition = districts ?? provinces;
                if (districts == null) _drilledProvince = "";   // ولسوالی نداشت ⇒ برگرد
            }
            else
            {
                _partition = provinces;
            }
            InvalidateBaseLayer();
        }

        private void InvalidateBaseLayer()
        {
            if (_baseLayer != null) { _baseLayer.Dispose(); _baseLayer = null; }
        }

        // رنگ‌آمیزیِ سریع با LockBits — حلقهٔ SetPixel روی ۶۰۰هزار پیکسل
        // محسوس کند است، اینجا یک پیمایشِ خطیِ حافظه است.
        private Bitmap BuildBaseLayer(AfghanMapGeometry.Partition part, Color background)
        {
            int w = part.Width, h = part.Height;
            Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppPArgb);

            double min, max;
            ComputeRange(part, out min, out max);

            int[] palette = new int[part.Names.Length];
            for (int i = 0; i < part.Names.Length; i++)
            {
                double v;
                bool has = _values.TryGetValue(part.Names[i], out v);
                Color c = has && max > 0 ? HeatColor(v, min, max) : NoDataColor;
                palette[i] = c.ToArgb();
            }

            int bg = background.ToArgb();
            int borderArgb = MapBorderColor.ToArgb();

            BitmapData data = bmp.LockBits(new Rectangle(0, 0, w, h),
                                           ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
            try
            {
                int[] row = new int[w];
                for (int y = 0; y < h; y++)
                {
                    int rowBase = y * w;
                    for (int x = 0; x < w; x++)
                    {
                        short r = part.Cells[rowBase + x];
                        if (r < 0) { row[x] = bg; continue; }

                        // مرزِ داخلی: پیکسلی که همسایهٔ راست یا پایینش ناحیهٔ
                        // دیگری است. دو همسایه کافی است؛ خطِ یک‌پیکسلی پیوسته
                        // می‌سازد و نیازی به بررسیِ هر چهار جهت نیست.
                        bool edge =
                            (x + 1 < w && part.Cells[rowBase + x + 1] != r) ||
                            (y + 1 < h && part.Cells[rowBase + w + x] != r) ||
                            (x > 0 && part.Cells[rowBase + x - 1] != r) ||
                            (y > 0 && part.Cells[rowBase - w + x] != r);

                        row[x] = edge ? borderArgb : palette[r];
                    }
                    System.Runtime.InteropServices.Marshal.Copy(
                        row, 0, IntPtr.Add(data.Scan0, y * data.Stride), w);
                }
            }
            finally { bmp.UnlockBits(data); }

            return bmp;
        }

        private void ComputeRange(AfghanMapGeometry.Partition part, out double min, out double max)
        {
            min = double.MaxValue; max = 0;
            foreach (string name in part.Names)
            {
                double v;
                if (!_values.TryGetValue(name, out v)) continue;
                if (v > max) max = v;
                if (v < min) min = v;
            }
            if (min == double.MaxValue) min = 0;
        }

        // ─── طیفِ رنگ ───────────────────────────────────────────────────────
        // آموزش — طیفِ ترتیبیِ تک‌رنگ (روشن→تیره در خانوادهٔ رنگِ سازمانی)
        // انتخاب شد نه رنگین‌کمان: چشم ترتیبِ روشنایی را بی‌آموزش می‌فهمد،
        // ولی ترتیبِ «سبز-زرد-قرمز» را باید از راهنما یاد بگیرد. برای
        // داده‌هایی که همه از یک جنس‌اند (تعداد پرونده و…) این درست‌تر است.
        private static readonly Color NoDataColor = ColorTranslator.FromHtml("#EDF0F4");
        private static readonly Color MapBorderColor = ColorTranslator.FromHtml("#FFFFFF");

        private static readonly Color[] HeatStops =
        {
            ColorTranslator.FromHtml("#E8F1F8"),
            ColorTranslator.FromHtml("#BBD5EA"),
            ColorTranslator.FromHtml("#84B2D6"),
            ColorTranslator.FromHtml("#4E8CBE"),
            ColorTranslator.FromHtml("#2C5A85"),
            ColorTranslator.FromHtml("#16334F")
        };

        public static Color HeatColor(double value, double min, double max)
        {
            if (max <= 0) return NoDataColor;
            if (value <= 0) return NoDataColor;

            // مقیاسِ ریشه‌ای: توزیعِ پرونده‌ها به‌شدت چوله است (کابل/هرات چند
            // برابرِ بقیه)، پس مقیاسِ خطی همهٔ ولایاتِ دیگر را هم‌رنگ می‌کند.
            double lo = Math.Max(0, min);
            double t = (Math.Sqrt(value) - Math.Sqrt(lo)) /
                       Math.Max(0.0001, Math.Sqrt(max) - Math.Sqrt(lo));
            t = Math.Max(0, Math.Min(1, t));

            double scaled = t * (HeatStops.Length - 1);
            int i = (int)Math.Floor(scaled);
            if (i >= HeatStops.Length - 1) return HeatStops[HeatStops.Length - 1];
            double f = scaled - i;

            Color a = HeatStops[i], b = HeatStops[i + 1];
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * f),
                (int)(a.G + (b.G - a.G) * f),
                (int)(a.B + (b.B - a.B) * f));
        }

        // ═══════════════════════════════════════════════════════════════════
        // رسم
        // ═══════════════════════════════════════════════════════════════════
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);

            EnsurePartition();
            if (_partition == null) return;

            if (_baseLayer == null ||
                _baseLayer.Width != _partition.Width || _baseLayer.Height != _partition.Height)
            {
                InvalidateBaseLayer();
                _baseLayer = BuildBaseLayer(_partition, BackColor);
            }

            g.DrawImageUnscaled(_baseLayer, 0, 0);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            DrawRegionOutline(g, _selectedIndex, UiTheme.PrimaryDark, 2.6f);
            DrawRegionOutline(g, _hoverIndex, Color.FromArgb(210, Color.White), 1.8f);

            DrawLabels(g, _partition, _hoverIndex, _selectedIndex, false);
            DrawLegend(g, _partition, ClientSize.Width, ClientSize.Height);
            DrawScaleNote(g);
        }

        // مرزِ برجستهٔ یک ناحیه: به‌جای استخراجِ چندضلعی، پیکسل‌های مرزیِ همان
        // ناحیه مستقیم از پارتیشن کشیده می‌شوند — دقیق و بدونِ کارِ اضافه.
        private void DrawRegionOutline(Graphics g, int index, Color color, float thickness)
        {
            if (index < 0 || _partition == null) return;

            int w = _partition.Width, h = _partition.Height;
            short target = (short)index;

            // فقط کادرِ محیطیِ همین ناحیه پیموده می‌شود، نه کلِ نقشه.
            Rectangle box = (_partition.Bounds != null && index < _partition.Bounds.Length)
                          ? _partition.Bounds[index]
                          : new Rectangle(0, 0, w, h);
            if (box.IsEmpty) return;

            int x0 = Math.Max(1, box.Left), x1 = Math.Min(w - 2, box.Right - 1);
            int y0 = Math.Max(1, box.Top), y1 = Math.Min(h - 2, box.Bottom - 1);

            using (GraphicsPath path = new GraphicsPath())
            {
                for (int y = y0; y <= y1; y++)
                {
                    int rowBase = y * w;
                    for (int x = x0; x <= x1; x++)
                    {
                        if (_partition.Cells[rowBase + x] != target) continue;
                        if (_partition.Cells[rowBase + x + 1] == target &&
                            _partition.Cells[rowBase + x - 1] == target &&
                            _partition.Cells[rowBase + w + x] == target &&
                            _partition.Cells[rowBase - w + x] == target) continue;
                        path.AddRectangle(new RectangleF(x, y, 1.4f, 1.4f));
                    }
                }
                using (Brush b = new SolidBrush(color))
                    g.FillPath(b, path);
            }
        }

        private void DrawLabels(Graphics g, AfghanMapGeometry.Partition part,
                                int hover, int selected, bool forPrint)
        {
            if (part.LabelAnchors == null) return;

            Font nameFont = UiTheme.FontBold(forPrint ? 8.5f : 8f);
            Font valueFont = UiTheme.FontBold(forPrint ? 9.5f : 9f);

            double min, max;
            ComputeRange(part, out min, out max);

            using (StringFormat sf = new StringFormat(StringFormatFlags.DirectionRightToLeft))
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;

                for (int i = 0; i < part.Names.Length; i++)
                {
                    PointF anchor = part.LabelAnchors[i];
                    if (anchor.X <= 0 && anchor.Y <= 0) continue;
                    if (part.PixelCounts[i] < 260) continue;    // ناحیهٔ خیلی کوچک ⇒ بدونِ برچسب

                    string name = part.Names[i];
                    double value;
                    bool has = _values.TryGetValue(name, out value);

                    // رنگِ متن بر اساسِ روشناییِ پس‌زمینه انتخاب می‌شود تا
                    // روی ولایتِ تیره هم خوانا بماند.
                    Color fill = has && max > 0 ? HeatColor(value, min, max) : NoDataColor;
                    double luma = (0.299 * fill.R + 0.587 * fill.G + 0.114 * fill.B) / 255.0;
                    Color ink = luma < 0.55 ? Color.White : UiTheme.TextDark;

                    string valueText = has ? FormatValue(value) : "—";

                    SizeF nameSize = g.MeasureString(name, nameFont);
                    SizeF valSize = g.MeasureString(valueText, valueFont);
                    float boxW = Math.Max(nameSize.Width, valSize.Width) + 10;
                    float boxH = nameSize.Height + valSize.Height + 4;

                    // اگر ناحیه برای هر دو خط جا ندارد، فقط عدد نوشته می‌شود.
                    bool compact = part.PixelCounts[i] < 2400;

                    RectangleF box = new RectangleF(anchor.X - boxW / 2,
                                                    anchor.Y - boxH / 2, boxW, boxH);

                    if (i == hover || i == selected)
                    {
                        using (Brush shade = new SolidBrush(Color.FromArgb(forPrint ? 0 : 60,
                                                                          luma < 0.55 ? Color.Black : Color.White)))
                            g.FillRectangle(shade, RectangleF.Inflate(box, 3, 2));
                    }

                    using (Brush br = new SolidBrush(ink))
                    {
                        if (!compact)
                        {
                            g.DrawString(name, nameFont, br,
                                new RectangleF(box.X, box.Y, box.Width, nameSize.Height), sf);
                            g.DrawString(valueText, valueFont, br,
                                new RectangleF(box.X, box.Y + nameSize.Height, box.Width, valSize.Height), sf);
                        }
                        else
                        {
                            g.DrawString(valueText, valueFont, br, box, sf);
                        }
                    }
                }
            }
        }

        private void DrawLegend(Graphics g, AfghanMapGeometry.Partition part, int width, int height)
        {
            double min, max;
            ComputeRange(part, out min, out max);
            if (max <= 0) return;

            int barW = Math.Min(240, Math.Max(140, width / 4));
            int barH = 10;
            int x = 16;
            int y = height - 40;

            // پس‌زمینهٔ کم‌رنگ زیرِ طیف: بدونِ آن، وقتی طیف روی ناحیهٔ تیرهٔ
            // نقشه می‌افتد نه عددش خوانده می‌شود نه خودِ نوار.
            using (Brush plate = new SolidBrush(Color.FromArgb(228, 255, 255, 255)))
                g.FillRectangle(plate, x - 8, y - 20, barW + 16, barH + 38);
            using (Pen edge = new Pen(Color.FromArgb(90, UiTheme.Border)))
                g.DrawRectangle(edge, x - 8, y - 20, barW + 16, barH + 38);

            using (Font f = UiTheme.Font(8f))
            using (Brush ink = new SolidBrush(UiTheme.TextMuted))
            using (StringFormat sf = new StringFormat(StringFormatFlags.DirectionRightToLeft))
            {
                sf.Alignment = StringAlignment.Near;
                g.DrawString(_metricTitle, f, ink, new RectangleF(x, y - 16, barW + 90, 14), sf);

                for (int i = 0; i < barW; i++)
                {
                    double t = i / (double)(barW - 1);
                    // معکوسِ مقیاسِ ریشه‌ای تا طیف با خودِ نقشه هم‌خوان بماند
                    double v = Math.Pow(Math.Sqrt(max) * t, 2);
                    using (Brush b = new SolidBrush(HeatColor(Math.Max(v, 0.0001), 0, max)))
                        g.FillRectangle(b, x + i, y, 1, barH);
                }
                using (Pen p = new Pen(UiTheme.Border))
                    g.DrawRectangle(p, x, y, barW, barH);

                g.DrawString("۰", f, ink, new RectangleF(x, y + barH + 1, 40, 14), sf);
                sf.Alignment = StringAlignment.Far;
                g.DrawString(ReportDoc.Fa(FormatValue(max)), f, ink,
                             new RectangleF(x, y + barH + 1, barW, 14), sf);
            }
        }

        private void DrawScaleNote(Graphics g)
        {
            using (Font f = UiTheme.Font(7.5f))
            using (Brush ink = new SolidBrush(Color.FromArgb(150, UiTheme.TextMuted)))
            using (StringFormat sf = new StringFormat(StringFormatFlags.DirectionRightToLeft))
            {
                sf.Alignment = StringAlignment.Far;
                g.DrawString("نقشهٔ شِماتیک — مرزها تقریبی‌اند", f, ink,
                             new RectangleF(8, ClientSize.Height - 18, ClientSize.Width - 16, 14), sf);
            }
        }

        private string FormatValue(double v)
        {
            if (Math.Abs(v - Math.Round(v)) < 0.05)
                return ((long)Math.Round(v)).ToString("#,0", CultureInfo.InvariantCulture) + _valueSuffix;
            return v.ToString("#,0.0", CultureInfo.InvariantCulture) + _valueSuffix;
        }

        // ═══════════════════════════════════════════════════════════════════
        // تعامل
        // ═══════════════════════════════════════════════════════════════════
        private ToolTip _tip;

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            EnsurePartition();
            if (_partition == null) return;

            int index = _partition.RegionAt(e.X, e.Y);
            if (index == _hoverIndex) return;

            _hoverIndex = index;
            Cursor = index >= 0 ? Cursors.Hand : Cursors.Default;

            if (_tip == null) _tip = new ToolTip { InitialDelay = 220, ReshowDelay = 60 };
            if (index >= 0)
            {
                string name = _partition.Names[index];
                string body;
                if (!_tooltips.TryGetValue(name, out body)) body = name + " — بدون داده";
                _tip.SetToolTip(this, body);
            }
            else _tip.SetToolTip(this, "");

            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoverIndex = -1;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            EnsurePartition();
            if (_partition == null) return;

            int index = _partition.RegionAt(e.X, e.Y);
            if (index < 0) return;

            _selectedIndex = index;
            Invalidate();

            if (e.Button == MouseButtons.Right)
            {
                // راست‌کلیک روی ولایت = ورود به سطحِ ولسوالی (و بازگشت با
                // راست‌کلیک روی فضای خارج یا دکمهٔ بازگشت).
                Raise(RegionDrillRequested, index);
                return;
            }
            Raise(RegionSelected, index);
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            EnsurePartition();
            if (_partition == null) return;

            int index = _partition.RegionAt(e.X, e.Y);
            if (index < 0) return;

            _selectedIndex = index;
            Raise(RegionActivated, index);
        }

        private void Raise(EventHandler<GeoRegionEventArgs> handler, int index)
        {
            if (handler == null || _partition == null) return;
            if (index < 0 || index >= _partition.Names.Length) return;

            handler(this, new GeoRegionEventArgs
            {
                Region = _partition.Names[index],
                Province = IsDistrictLevel ? _drilledProvince : _partition.Names[index],
                IsDistrictLevel = IsDistrictLevel
            });
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            _partition = null;
            InvalidateBaseLayer();
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                InvalidateBaseLayer();
                if (_tip != null) { _tip.Dispose(); _tip = null; }
            }
            base.Dispose(disposing);
        }
    }
}
