using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Windows.Forms;

namespace CaseManagement.Helpers
{
    // Material Icons Rounded — آیکون وکتورِ کارت‌های آماری داشبورد.
    // فونت در Fonts\Icons است (نه Fonts ریشه) تا UiTheme آن را به‌عنوان
    // فونت متن فارسی بر ندارد. اگر فایل نباشد، نقاشیِ وکتور جایگزین می‌شود.
    public static class MaterialIcon
    {
        public const string FolderOpen        = "\uE2C8";
        public const string Groups            = "\uF233";
        public const string Description       = "\uE873";
        public const string Business          = "\uE0AF";
        public const string CheckCircle       = "\uE86C";
        public const string Sync              = "\uE627";
        public const string HourglassTop      = "\uEA5B";
        public const string PauseCircle       = "\uE1A2";
        public const string Cancel            = "\uE5C9";
        public const string MarkEmailRead     = "\uF18C";
        public const string Assignment        = "\uE85D";
        public const string Verified          = "\uEF76";
        public const string Payments          = "\uEF63";
        public const string Shield            = "\uE9E0";
        public const string Settings          = "\uE8B8";
        public const string Analytics         = "\uEF3E";
        public const string Place             = "\uE55F";
        public const string AccountBalance    = "\uE84F";
        public const string VolunteerActivism = "\uEA70";

        private static readonly object FontLock = new object();
        private static PrivateFontCollection _fonts;
        private static FontFamily _family;

        public static FontFamily Family
        {
            get
            {
                if (_family == null)
                {
                    lock (FontLock)
                    {
                        if (_family == null)
                            _family = Load();
                    }
                }
                return _family;
            }
        }

        public static Font Get(float pixelSize)
        {
            FontFamily family = Family;
            if (family == null) return null;
            return new Font(family, pixelSize, FontStyle.Regular, GraphicsUnit.Pixel);
        }

        private static FontFamily Load()
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "Fonts", "Icons", "MaterialIconsRound-Regular.otf");
                if (!File.Exists(path)) return null;

                _fonts = new PrivateFontCollection();
                _fonts.AddFontFile(path);
                return _fonts.Families.Length > 0 ? _fonts.Families[0] : null;
            }
            catch
            {
                return null;
            }
        }
    }

    // دایرهٔ نیمه‌شفاف + گلیف Material به رنگِ کارت (۲۴–۳۲px).
    public class MaterialIconBadge : Control
    {
        private readonly string _glyph;
        private readonly Color _accent;

        public MaterialIconBadge(string glyph, Color accent)
        {
            _glyph = glyph ?? "";
            _accent = accent;
            Size = new Size(36, 36);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                      ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                      ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;

            int size = Math.Min(Width, Height) - 1;
            int x = (Width - size) / 2;
            int y = (Height - size) / 2;

            using (Brush fill = new SolidBrush(Color.FromArgb(32, _accent)))
                g.FillEllipse(fill, x, y, size, size);

            float glyphPx = Math.Max(12f, size * 0.62f);
            Font font = MaterialIcon.Get(glyphPx);
            if (font != null)
            {
                using (font)
                using (Brush b = new SolidBrush(_accent))
                using (StringFormat sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center,
                    FormatFlags = StringFormatFlags.NoWrap
                })
                {
                    g.DrawString(_glyph, font, b, new RectangleF(x, y + 0.5f, size, size), sf);
                }
                return;
            }

            float pad = size * 0.26f;
            using (Pen pen = new Pen(_accent, Math.Max(1.4f, size * 0.07f)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                ModernGlyph.Draw(g, pen, new RectangleF(x + pad, y + pad, size - pad * 2, size - pad * 2),
                    FallbackGlyph(_glyph));
            }
        }

        private static string FallbackGlyph(string material)
        {
            if (material == MaterialIcon.FolderOpen || material == MaterialIcon.Assignment) return IconFont.Folder;
            if (material == MaterialIcon.Groups) return IconFont.People;
            if (material == MaterialIcon.Description) return IconFont.Document;
            if (material == MaterialIcon.Business || material == MaterialIcon.MarkEmailRead) return IconFont.Card;
            if (material == MaterialIcon.CheckCircle || material == MaterialIcon.Verified) return IconFont.Check;
            if (material == MaterialIcon.Sync) return IconFont.Sync;
            if (material == MaterialIcon.HourglassTop || material == MaterialIcon.PauseCircle) return IconFont.Clock;
            if (material == MaterialIcon.Cancel) return IconFont.Cancel;
            if (material == MaterialIcon.Payments || material == MaterialIcon.AccountBalance
                || material == MaterialIcon.VolunteerActivism) return IconFont.Money;
            if (material == MaterialIcon.Shield) return IconFont.Shield;
            if (material == MaterialIcon.Settings) return IconFont.Settings;
            if (material == MaterialIcon.Analytics) return IconFont.Chart;
            if (material == MaterialIcon.Place) return IconFont.Check;
            return IconFont.Document;
        }
    }
}
