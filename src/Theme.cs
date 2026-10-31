using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace QingLi
{
    /// <summary>主题档位：跟随系统 / 始终浅色 / 始终深色。</summary>
    public enum ThemeMode
    {
        System = 0,
        Light = 1,
        Dark = 2
    }

    /// <summary>控件上声明的语义配色角色（放在 Control.Tag 里，主题切换时按角色重新着色）。</summary>
    public enum ThemeRole
    {
        None = 0,
        Primary,
        Secondary,
        Muted,
        Accent,
        Danger,
        OnAccent,
        Window,
        Surface,
        SurfaceHi,
        SurfaceHover,
        StatusBar
    }

    /// <summary>
    /// 主题：深浅两套调色板、语义令牌、系统主题读取、窗口外观辅助。
    /// 视图代码只允许引用令牌（如 <see cref="Theme.Surface"/>），不允许出现颜色字面量。
    /// </summary>
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

        // ---------- 调色板 ----------

        /// <summary>一套完整的语义色值。</summary>
        internal sealed class Palette
        {
            public bool Dark;

            public Color Window;
            public Color Surface;
            public Color SurfaceHi;
            public Color SurfaceHover;
            public Color Border;
            public Color BorderStrong;

            public Color TextPrimary;
            public Color TextSecondary;
            public Color TextMuted;
            public Color Accent;
            public Color OnAccent;
            public Color DisabledText;
            public Color DisabledFill;

            public Color AccentFill;
            public Color AccentFillHover;
            public Color AccentSoft;

            public Color Danger;
            public Color DangerSoftBg;
            public Color DangerSoftHover;
            public Color DangerSoftText;

            public Color TodayFill;
            public Color Weekend;
            public Color NowLine;
            public Color Holiday;
            public Color HolidayBadgeBg;
            public Color WorkdayBadge;
            public Color WorkdayBadgeText;

            public Color[] Events;
        }

        /// <summary>深色：沿用 1.1.0 的 Win11 Dark 外观，只微调弱化文字 / 周末 / 按钮悬停三处。</summary>
        private static readonly Palette DarkPalette = new Palette
        {
            Dark = true,
            Window = Hex("#1B1B1F"),
            Surface = Hex("#232328"),
            SurfaceHi = Hex("#2C2C32"),
            SurfaceHover = Hex("#34343B"),
            Border = Hex("#3A3A42"),
            BorderStrong = Hex("#4A4A54"),
            TextPrimary = Hex("#F3F3F5"),
            TextSecondary = Hex("#A9A9B4"),
            TextMuted = Hex("#9494A1"),
            Accent = Hex("#4CC2FF"),
            OnAccent = Hex("#FFFFFF"),
            DisabledText = Hex("#6E6E7A"),
            DisabledFill = Hex("#232328"),
            AccentFill = Hex("#0F6CBD"),
            AccentFillHover = Hex("#1173C0"),
            AccentSoft = Hex("#20303C"),
            Danger = Hex("#FF6B6B"),
            DangerSoftBg = Hex("#3E2A2C"),
            DangerSoftHover = Hex("#4A2B2B"),
            DangerSoftText = Hex("#FF6B6B"),
            TodayFill = Hex("#2A3B49"),
            Weekend = Hex("#9B9BA8"),
            NowLine = Hex("#FF7A7A"),
            Holiday = Hex("#FF8C9B"),
            HolidayBadgeBg = Hex("#533A41"),
            WorkdayBadge = Hex("#33333B"),
            WorkdayBadgeText = Hex("#A9A9B4"),
            Events = new Color[]
            {
                Hex("#4CC2FF"), Hex("#7FD8A0"), Hex("#FFB86B"), Hex("#C79BFF"),
                Hex("#FF8FA3"), Hex("#F2D06B"), Hex("#6FD8D2"), Hex("#9FB4FF")
            }
        };

        /// <summary>浅色：白底深字，对比度按 WCAG 2.1 AA 校验（见 docs/theme-modes.md 第 4 章）。</summary>
        private static readonly Palette LightPalette = new Palette
        {
            Dark = false,
            Window = Hex("#F6F6F8"),
            Surface = Hex("#FFFFFF"),
            SurfaceHi = Hex("#F1F2F5"),
            SurfaceHover = Hex("#E4E7EC"),
            Border = Hex("#E1E3E8"),
            BorderStrong = Hex("#C8CBD3"),
            TextPrimary = Hex("#1B1B1F"),
            TextSecondary = Hex("#4A4A55"),
            TextMuted = Hex("#6B6B78"),
            Accent = Hex("#0F6CBD"),
            OnAccent = Hex("#FFFFFF"),
            DisabledText = Hex("#9A9AA4"),
            DisabledFill = Hex("#F1F2F5"),
            AccentFill = Hex("#0F6CBD"),
            AccentFillHover = Hex("#0C5CA3"),
            AccentSoft = Hex("#E5F1FB"),
            Danger = Hex("#C42B1C"),
            DangerSoftBg = Hex("#FBE9E6"),
            DangerSoftHover = Hex("#F3D6D0"),
            DangerSoftText = Hex("#B02418"),
            TodayFill = Hex("#CFE6F7"),
            Weekend = Hex("#5E6B7A"),
            NowLine = Hex("#D13438"),
            Holiday = Hex("#C42B1C"),
            HolidayBadgeBg = Hex("#FBE9E6"),
            WorkdayBadge = Hex("#EDEFF3"),
            WorkdayBadgeText = Hex("#4A4A55"),
            Events = new Color[]
            {
                Hex("#0F6CBD"), Hex("#1E7A46"), Hex("#A15C00"), Hex("#6B3FA0"),
                Hex("#B3264A"), Hex("#8A6D00"), Hex("#0E6F6A"), Hex("#3B5BA5")
            }
        };

        private static Palette _current = DarkPalette;

        /// <summary>当前档位（system / light / dark 三选一，不是最终生效的深浅色）。</summary>
        public static ThemeMode Mode { get; private set; }

        /// <summary>当前实际生效的是否深色。</summary>
        public static bool IsDark
        {
            get { return _current.Dark; }
        }

        /// <summary>调色板或生效档位发生变化时触发（订阅方负责重绘，并在关闭时取消订阅）。</summary>
        public static event EventHandler Changed;

        // ---------- 语义令牌 ----------

        public static Color Window { get { return _current.Window; } }
        public static Color Surface { get { return _current.Surface; } }
        public static Color SurfaceHi { get { return _current.SurfaceHi; } }
        public static Color SurfaceHover { get { return _current.SurfaceHover; } }
        public static Color Border { get { return _current.Border; } }
        public static Color BorderStrong { get { return _current.BorderStrong; } }

        public static Color TextPrimary { get { return _current.TextPrimary; } }
        public static Color TextSecondary { get { return _current.TextSecondary; } }
        public static Color TextMuted { get { return _current.TextMuted; } }
        public static Color Accent { get { return _current.Accent; } }
        public static Color OnAccent { get { return _current.OnAccent; } }
        public static Color DisabledText { get { return _current.DisabledText; } }
        public static Color DisabledFill { get { return _current.DisabledFill; } }

        public static Color AccentFill { get { return _current.AccentFill; } }
        public static Color AccentFillHover { get { return _current.AccentFillHover; } }
        public static Color AccentSoft { get { return _current.AccentSoft; } }

        public static Color Danger { get { return _current.Danger; } }
        public static Color DangerSoftBg { get { return _current.DangerSoftBg; } }
        public static Color DangerSoftHover { get { return _current.DangerSoftHover; } }
        public static Color DangerSoftText { get { return _current.DangerSoftText; } }

        public static Color TodayFill { get { return _current.TodayFill; } }
        public static Color Weekend { get { return _current.Weekend; } }
        public static Color NowLine { get { return _current.NowLine; } }

        /// <summary>节假日（休）强调色，用于法定放假日期。</summary>
        public static Color Holiday { get { return _current.Holiday; } }

        /// <summary>「休」徽标底色。</summary>
        public static Color HolidayBadgeBg { get { return _current.HolidayBadgeBg; } }

        /// <summary>调休上班（班）徽标底色。</summary>
        public static Color WorkdayBadge { get { return _current.WorkdayBadge; } }

        /// <summary>「班」徽标文字色。</summary>
        public static Color WorkdayBadgeText { get { return _current.WorkdayBadgeText; } }

        /// <summary>8 色事件色板（索引 0–7 与 1.1.0 稳定对应，已在 data.xml 持久化）。</summary>
        public static Color[] EventColors
        {
            get { return _current.Events; }
        }

        public static Color EventColor(int index)
        {
            Color[] list = _current.Events;
            if (index < 0 || index >= list.Length) index = 0;
            return list[index];
        }

        /// <summary>按语义角色取色（用于 Control.Tag 驱动的重新着色）。</summary>
        public static Color ColorFor(ThemeRole role)
        {
            switch (role)
            {
                case ThemeRole.Primary: return TextPrimary;
                case ThemeRole.Secondary: return TextSecondary;
                case ThemeRole.Muted: return TextMuted;
                case ThemeRole.Accent: return Accent;
                case ThemeRole.Danger: return Danger;
                case ThemeRole.OnAccent: return OnAccent;
                case ThemeRole.Window: return Window;
                case ThemeRole.Surface: return Surface;
                case ThemeRole.SurfaceHi: return SurfaceHi;
                case ThemeRole.SurfaceHover: return SurfaceHover;
                case ThemeRole.StatusBar: return Mix(Window, Surface, 0.6f);
                default: return TextPrimary;
            }
        }

        private static Color Hex(string html)
        {
            return ColorTranslator.FromHtml(html);
        }

        // ---------- 档位解析与生效 ----------

        public static string ModeToString(ThemeMode mode)
        {
            if (mode == ThemeMode.Light) return "light";
            if (mode == ThemeMode.Dark) return "dark";
            return "system";
        }

        /// <summary>解析持久化值：大小写不敏感、去首尾空白；空值与非法值一律回退 system。</summary>
        public static ThemeMode ParseMode(string value)
        {
            if (value == null) return ThemeMode.System;
            string v = value.Trim().ToLowerInvariant();
            if (v == "light") return ThemeMode.Light;
            if (v == "dark") return ThemeMode.Dark;
            return ThemeMode.System;
        }

        /// <summary>
        /// 启动时决定档位：
        /// 全新安装（data.xml 不存在）= system；升级（文件存在但没有 &lt;Theme&gt; 元素）= dark；
        /// 元素为空或非法 = system。
        /// </summary>
        public static ThemeMode ResolveInitialMode(string savedValue, bool dataFileExisted, bool hasThemeElement)
        {
            bool explicitValue = savedValue != null && savedValue.Trim().Length > 0;
            if (explicitValue) return ParseMode(savedValue);
            if (!dataFileExisted) return ThemeMode.System;
            if (!hasThemeElement) return ThemeMode.Dark;
            return ThemeMode.System;
        }

        /// <summary>应用档位：system 时实时读取系统深浅色。仅在色彩实际变化时广播 Changed。</summary>
        public static void Apply(ThemeMode mode)
        {
            bool wantDark = mode == ThemeMode.Dark || (mode == ThemeMode.System && SystemPrefersDark());
            Palette next = wantDark ? DarkPalette : LightPalette;
            bool changed = next.Dark != _current.Dark || mode != Mode;
            Mode = mode;
            _current = next;
            if (changed && Changed != null) Changed(null, EventArgs.Empty);
        }

        /// <summary>系统主题变化后由调用方触发：仅在 system 档位下重新取色。</summary>
        public static void RefreshFromSystem()
        {
            if (Mode != ThemeMode.System) return;
            Apply(ThemeMode.System);
        }

        /// <summary>读取 HKCU\...\Personalize\AppsUseLightTheme；1 = 浅色，0 = 深色，缺失或异常按浅色。</summary>
        public static bool SystemPrefersDark()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", false))
                {
                    if (key == null) return false;
                    object raw = key.GetValue("AppsUseLightTheme");
                    if (raw is int) return ((int)raw) == 0;
                }
            }
            catch
            {
            }
            return false;
        }

        // ---------- 字体 ----------

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

        /// <summary>居中绘制小徽标文字：去掉内边距，避免「休」「班」等单个 CJK 字符在小方框内被省略号截断。</summary>
        public static void TextCBare(Graphics g, string s, Font f, Color c, Rectangle r)
        {
            TextRenderer.DrawText(g, s, f, r, c,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
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

        /// <summary>标题栏深色属性的唯一入口：深色写 1，浅色必须写 0，否则切回浅色后标题栏仍是深色。</summary>
        public static void ApplyTitleBar(IntPtr hwnd, bool dark)
        {
            if (hwnd == IntPtr.Zero) return;
            try
            {
                int flag = dark ? 1 : 0;
                if (DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref flag, 4) != 0)
                    DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref flag, 4);
                int round = DWMWCP_ROUND;
                DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref round, 4);
            }
            catch
            {
            }
        }

        /// <summary>按当前主题设置标题栏（窗口创建与主题切换时都应调用）。</summary>
        public static void ApplyTitleBar(IntPtr hwnd)
        {
            ApplyTitleBar(hwnd, IsDark);
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
