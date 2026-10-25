using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace QingLi
{
    /// <summary>Win11 风格深色主题：颜色、字体、圆角绘制与窗口外观辅助。</summary>
    internal static class Theme
    {
        public static float Scale = 1f;

        public static int S(int v)
        {
            return (int)Math.Round(v * (double)Scale);
        }

        public static float SF(float v)
        {
            return (float)(v * (double)Scale);
        }

        // ---------- 调色板（Win11 Dark） ----------
        public static readonly Color Window = ColorTranslator.FromHtml("#1B1B1F");
        public static readonly Color Surface = ColorTranslator.FromHtml("#232328");
        public static readonly Color SurfaceHi = ColorTranslator.FromHtml("#2C2C32");
        public static readonly Color SurfaceHover = ColorTranslator.FromHtml("#34343B");
        public static readonly Color Border = ColorTranslator.FromHtml("#3A3A42");
        public static readonly Color BorderStrong = ColorTranslator.FromHtml("#4A4A54");
        public static readonly Color TextPrimary = ColorTranslator.FromHtml("#F3F3F5");
        public static readonly Color TextSecondary = ColorTranslator.FromHtml("#A9A9B4");
        public static readonly Color TextMuted = ColorTranslator.FromHtml("#6E6E7A");
        public static readonly Color Accent = ColorTranslator.FromHtml("#4CC2FF");
        public static readonly Color AccentFill = ColorTranslator.FromHtml("#0F6CBD");
        public static readonly Color AccentFillHover = ColorTranslator.FromHtml("#1A7ED0");
        public static readonly Color AccentSoft = ColorTranslator.FromHtml("#20303C");
        public static readonly Color Danger = ColorTranslator.FromHtml("#FF6B6B");
        public static readonly Color TodayFill = ColorTranslator.FromHtml("#2A3B49");
        public static readonly Color Weekend = ColorTranslator.FromHtml("#8E8E9A");
        public static readonly Color NowLine = ColorTranslator.FromHtml("#FF7A7A");

        public static readonly Color[] EventColors = new Color[]
        {
            ColorTranslator.FromHtml("#4CC2FF"),
            ColorTranslator.FromHtml("#7FD8A0"),
            ColorTranslator.FromHtml("#FFB86B"),
            ColorTranslator.FromHtml("#C79BFF"),
            ColorTranslator.FromHtml("#FF8FA3"),
            ColorTranslator.FromHtml("#F2D06B"),
            ColorTranslator.FromHtml("#6FD8D2"),
            ColorTranslator.FromHtml("#9FB4FF")
        };

        public static Color EventColor(int index)
        {
            if (index < 0 || index >= EventColors.Length) index = 0;
            return EventColors[index];
        }

        private static readonly string BodyFamily;
        private static readonly string NumberFamily;

        public static readonly Font FSmall;
        public static readonly Font FBody;
        public static readonly Font FBodyBold;
        public static readonly Font FTitle;
        public static readonly Font FBig;
        public static readonly Font FHuge;
        public static readonly Font FTime;
        public static readonly Font FTimeSmall;
        public static readonly Font FIcon;

        static Theme()
        {
            BodyFamily = PickFamily(new string[] { "Microsoft YaHei UI", "Microsoft YaHei", "Segoe UI", "Tahoma" });
            NumberFamily = PickFamily(new string[] { "Segoe UI", "Segoe UI Variable Display", "Tahoma" });

            FSmall = new Font(BodyFamily, 8.5f);
            FBody = new Font(BodyFamily, 9.75f);
            FBodyBold = new Font(BodyFamily, 9.75f, FontStyle.Bold);
            FTitle = new Font(BodyFamily, 11.5f, FontStyle.Bold);
            FBig = new Font(BodyFamily, 14f, FontStyle.Bold);
            FHuge = new Font(BodyFamily, 22f, FontStyle.Bold);
            FTime = new Font(NumberFamily, 9f);
            FTimeSmall = new Font(NumberFamily, 7.75f);
            FIcon = new Font(NumberFamily, 10f, FontStyle.Bold);
        }

        private static string PickFamily(string[] wanted)
        {
            try
            {
                using (InstalledFontCollection col = new InstalledFontCollection())
                {
                    for (int w = 0; w < wanted.Length; w++)
                    {
                        for (int i = 0; i < col.Families.Length; i++)
                        {
                            if (string.Equals(col.Families[i].Name, wanted[w], StringComparison.OrdinalIgnoreCase))
                                return wanted[w];
                        }
                    }
                }
            }
            catch
            {
            }
            return "Segoe UI";
        }

        public static Color Mix(Color a, Color b, float t)
        {
            if (t < 0f) t = 0f;
            if (t > 1f) t = 1f;
            return Color.FromArgb(
                (int)Math.Round(a.R + (b.R - a.R) * t),
                (int)Math.Round(a.G + (b.G - a.G) * t),
                (int)Math.Round(a.B + (b.B - a.B) * t));
        }

        public static Color Alpha(Color c, int a)
        {
            return Color.FromArgb(a, c.R, c.G, c.B);
        }

        // ---------- 绘制辅助 ----------
        public static GraphicsPath RoundPath(Rectangle r, int radius)
        {
            GraphicsPath p = new GraphicsPath();
            if (r.Width <= 0 || r.Height <= 0)
            {
                p.AddRectangle(r);
                return p;
            }
            int d = Math.Max(0, Math.Min(radius * 2, Math.Min(r.Width, r.Height)));
            if (d <= 0)
            {
                p.AddRectangle(r);
                return p;
            }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d - 1, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d - 1, r.Bottom - d - 1, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d - 1, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void FillRound(Graphics g, Rectangle r, int radius, Color color)
        {
            using (GraphicsPath p = RoundPath(r, radius))
            using (SolidBrush b = new SolidBrush(color))
            {
                g.FillPath(b, p);
            }
        }

        public static void DrawRound(Graphics g, Rectangle r, int radius, Color color, float width)
        {
            using (GraphicsPath p = RoundPath(r, radius))
            using (Pen pen = new Pen(color, width))
            {
                g.DrawPath(pen, p);
            }
        }

        public static void FillRoundGradient(Graphics g, Rectangle r, int radius, Color top, Color bottom)
        {
            if (r.Width <= 0 || r.Height <= 0) return;
            using (GraphicsPath p = RoundPath(r, radius))
            using (LinearGradientBrush b = new LinearGradientBrush(new Rectangle(r.X, r.Y, r.Width, r.Height + 1), top, bottom, LinearGradientMode.Vertical))
            {
                g.FillPath(b, p);
            }
        }

        public static void Text(Graphics g, string s, Font f, Color c, Rectangle r, TextFormatFlags flags)
        {
            TextRenderer.DrawText(g, s, f, r, c, flags | TextFormatFlags.NoPrefix);
        }

        public static void TextC(Graphics g, string s, Font f, Color c, Rectangle r)
        {
            TextRenderer.DrawText(g, s, f, r, c,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        }

        public static void TextLeft(Graphics g, string s, Font f, Color c, Rectangle r)
        {
            TextRenderer.DrawText(g, s, f, r, c,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        }

        public static void TextRight(Graphics g, string s, Font f, Color c, Rectangle r)
        {
            TextRenderer.DrawText(g, s, f, r, c,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        }

        // ---------- 窗口外观 ----------
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("dwmapi.dll")]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS margins);

        [StructLayout(LayoutKind.Sequential)]
        private struct MARGINS
        {
            public int Left;
            public int Right;
            public int Top;
            public int Bottom;
        }

        public static void ApplyDarkTitleBar(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;
            try
            {
                int on = 1;
                if (DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, 4) != 0)
                    DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref on, 4);
                int round = DWMWCP_ROUND;
                DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref round, 4);
            }
            catch
            {
            }
        }

        public static Graphics Smooth(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            return g;
        }
    }
}
