using System;
using System.Drawing;
using System.IO;
using QingLi;

/// <summary>
/// 主题回归测试：档位解析与默认值、深浅两套调色板取值、WCAG 对比度、
/// &lt;Theme&gt; 持久化与升级/损坏回退、事件色映射稳定性、主题变更广播。
/// </summary>
class TestTheme
{
    static int failed = 0;
    static int passed = 0;

    static void Check(bool cond, string name)
    {
        if (cond) { passed++; Console.WriteLine("  PASS  " + name); }
        else { failed++; Console.WriteLine("  FAIL  " + name); }
    }

    static bool Same(Color c, string hex)
    {
        Color want = ColorTranslator.FromHtml(hex);
        return c.ToArgb() == want.ToArgb();
    }

    static double Lum(Color c)
    {
        return 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
    }

    static double Channel(int v)
    {
        double x = v / 255.0;
        return x <= 0.03928 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4);
    }

    static double Contrast(Color fg, Color bg)
    {
        double a = Lum(fg), b = Lum(bg);
        if (a < b) { double t = a; a = b; b = t; }
        return (a + 0.05) / (b + 0.05);
    }

    static string Code(Color c)
    {
        return "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
    }

    /// <summary>文字 / 背景对 ≥ 4.5:1（WCAG 2.1 AA）。</summary>
    static void TextPair(string label, Color fg, Color bg)
    {
        double cr = Contrast(fg, bg);
        Check(cr >= 4.5, label + " " + Code(fg) + " / " + Code(bg) + " = " + cr.ToString("0.00") + " ≥ 4.5");
    }

    /// <summary>图形 / 边界对 ≥ 3:1（WCAG 1.4.11）。</summary>
    static void GraphicPair(string label, Color fg, Color bg)
    {
        double cr = Contrast(fg, bg);
        Check(cr >= 3.0, label + " " + Code(fg) + " / " + Code(bg) + " = " + cr.ToString("0.00") + " ≥ 3.0");
    }

    static void CheckPalette(string name)
    {
        Console.WriteLine("== " + name + " 对比度（WCAG 2.1 AA）==");
        TextPair(name + " 主文字/卡片", Theme.TextPrimary, Theme.Surface);
        TextPair(name + " 次要文字/卡片", Theme.TextSecondary, Theme.Surface);
        TextPair(name + " 弱化文字/卡片", Theme.TextMuted, Theme.Surface);
        TextPair(name + " 弱化文字/悬停底", Theme.TextMuted, Theme.SurfaceHi);
        TextPair(name + " 周末/卡片", Theme.Weekend, Theme.Surface);
        TextPair(name + " 周末/悬停底", Theme.Weekend, Theme.SurfaceHi);
        TextPair(name + " 节假日/卡片", Theme.Holiday, Theme.Surface);
        TextPair(name + " 节假日/休徽标底", Theme.Holiday, Theme.HolidayBadgeBg);
        TextPair(name + " 班徽标文字/底", Theme.WorkdayBadgeText, Theme.WorkdayBadge);
        TextPair(name + " 反色文字/强调填充", Theme.OnAccent, Theme.AccentFill);
        TextPair(name + " 反色文字/强调填充悬停", Theme.OnAccent, Theme.AccentFillHover);
        TextPair(name + " 危险按钮文字/默认底", Theme.DangerSoftText, Theme.DangerSoftBg);
        TextPair(name + " 危险按钮文字/悬停底", Theme.DangerSoftText, Theme.DangerSoftHover);
        TextPair(name + " 现在时刻线/窗口", Theme.NowLine, Theme.Window);
        TextPair(name + " 分段选中文字/柔和底", Theme.Accent, Theme.AccentSoft);

        Console.WriteLine("== " + name + " 事件 8 色 vs 卡片（≥ 3:1）==");
        for (int i = 0; i < Theme.EventColors.Length; i++)
            GraphicPair(name + " 事件色 " + i, Theme.EventColor(i), Theme.Surface);

        Console.WriteLine("== " + name + " 选中日期格（描边 / 格底 / 格内文字）==");
        // 缺陷 1 回归：描边必须与选中格底 ≥ 3:1（HYPE-26 修复前为深 3.38 / 浅 2.45，浅色不达标）
        GraphicPair(name + " 选中描边/选中格底", Theme.SelectionBorder, Theme.SelectionFill);
        Check(Theme.SelectionFill.ToArgb() != Theme.Surface.ToArgb(),
            name + " 选中格底 " + Code(Theme.SelectionFill) + " 区别于卡片底 " + Code(Theme.Surface) +
            " = " + Contrast(Theme.SelectionFill, Theme.Surface).ToString("0.00") + ":1（辅助，不单独构成状态）");
        Check(Theme.SelectionBorderWidth >= 2f, name + " 选中描边宽度 2px（实际 " + Theme.SelectionBorderWidth + "f）");
        // 选中格内的文字仍须 ≥ 4.5:1（选中格不叠加悬停底色，故只需校验这一种底）
        TextPair(name + " 主文字/选中格底", Theme.TextPrimary, Theme.SelectionFill);
        TextPair(name + " 次要文字/选中格底", Theme.TextSecondary, Theme.SelectionFill);
        TextPair(name + " 周末/选中格底", Theme.Weekend, Theme.SelectionFill);
        TextPair(name + " 节假日/选中格底", Theme.Holiday, Theme.SelectionFill);
    }

    static int Main()
    {
        Console.WriteLine("== 1. 档位字符串解析（大小写不敏感 / 非法回退 system）==");
        Check(Theme.ParseMode("dark") == ThemeMode.Dark, "dark -> Dark");
        Check(Theme.ParseMode("DARK") == ThemeMode.Dark, "DARK -> Dark");
        Check(Theme.ParseMode(" light ") == ThemeMode.Light, "带空格 light -> Light");
        Check(Theme.ParseMode("System") == ThemeMode.System, "System -> System");
        Check(Theme.ParseMode("") == ThemeMode.System, "空串 -> System");
        Check(Theme.ParseMode(null) == ThemeMode.System, "null -> System");
        Check(Theme.ParseMode("blue") == ThemeMode.System, "非法值 blue -> System");

        Console.WriteLine("== 2. 启动默认值（全新安装 / 1.1.0 升级 / 空元素）==");
        Check(Theme.ResolveInitialMode("", false, false) == ThemeMode.System, "全新安装（无 data.xml）-> system");
        Check(Theme.ResolveInitialMode("", true, false) == ThemeMode.Dark, "1.1.0 升级（无 <Theme>）-> dark");
        Check(Theme.ResolveInitialMode("", true, true) == ThemeMode.System, "存在空 <Theme> -> system");
        Check(Theme.ResolveInitialMode("  ", true, true) == ThemeMode.System, "空白 <Theme> -> system");
        Check(Theme.ResolveInitialMode("blue", true, true) == ThemeMode.System, "非法 <Theme> -> system");
        Check(Theme.ResolveInitialMode("dark", true, false) == ThemeMode.Dark, "显式 dark 优先 -> dark");
        Check(Theme.ResolveInitialMode("light", false, false) == ThemeMode.Light, "显式 light -> light");
        Check(Theme.ResolveInitialMode("Dark", true, true) == ThemeMode.Dark, "显式 Dark（大写）-> dark");

        Console.WriteLine("== 3. 浅色调色板取值 ==");
        Theme.Apply(ThemeMode.Light);
        Check(!Theme.IsDark, "应用 light 后 IsDark = false");
        Check(Theme.Mode == ThemeMode.Light, "应用 light 后 Mode = Light");
        Check(Same(Theme.Window, "#F6F6F8"), "浅色 Window = #F6F6F8");
        Check(Same(Theme.Surface, "#FFFFFF"), "浅色 Surface = #FFFFFF");
        Check(Same(Theme.SurfaceHi, "#F1F2F5"), "浅色 SurfaceHi = #F1F2F5");
        Check(Same(Theme.Border, "#E1E3E8"), "浅色 Border = #E1E3E8");
        Check(Same(Theme.TextPrimary, "#1B1B1F"), "浅色 TextPrimary = #1B1B1F");
        Check(Same(Theme.TextMuted, "#6B6B78"), "浅色 TextMuted = #6B6B78");
        Check(Same(Theme.Weekend, "#5E6B7A"), "浅色 Weekend = #5E6B7A");
        Check(Same(Theme.Holiday, "#C42B1C"), "浅色 Holiday = #C42B1C");
        Check(Same(Theme.OnAccent, "#FFFFFF"), "浅色 OnAccent = #FFFFFF");
        Check(Same(Theme.AccentFillHover, "#0C5CA3"), "浅色 AccentFillHover = #0C5CA3");

        Console.WriteLine("== 4. 深色调色板取值（沿用 1.1.0，仅 3 处微调）==");
        Theme.Apply(ThemeMode.Dark);
        Check(Theme.IsDark, "应用 dark 后 IsDark = true");
        Check(Same(Theme.Window, "#1B1B1F"), "深色 Window = #1B1B1F");
        Check(Same(Theme.Surface, "#232328"), "深色 Surface = #232328");
        Check(Same(Theme.SurfaceHi, "#2C2C32"), "深色 SurfaceHi = #2C2C32");
        Check(Same(Theme.SurfaceHover, "#34343B"), "深色 SurfaceHover = #34343B");
        Check(Same(Theme.Border, "#3A3A42"), "深色 Border = #3A3A42");
        Check(Same(Theme.BorderStrong, "#4A4A54"), "深色 BorderStrong = #4A4A54");
        Check(Same(Theme.TextPrimary, "#F3F3F5"), "深色 TextPrimary = #F3F3F5");
        Check(Same(Theme.TextSecondary, "#A9A9B4"), "深色 TextSecondary = #A9A9B4");
        Check(Same(Theme.TextMuted, "#9494A1"), "深色 TextMuted = #9494A1（微调 1）");
        Check(Same(Theme.Weekend, "#9B9BA8"), "深色 Weekend = #9B9BA8（微调 2）");
        Check(Same(Theme.AccentFillHover, "#1173C0"), "深色 AccentFillHover = #1173C0（微调 3）");
        Check(Same(Theme.AccentFill, "#0F6CBD"), "深色 AccentFill = #0F6CBD");
        Check(Same(Theme.Holiday, "#FF8C9B"), "深色 Holiday = #FF8C9B");
        Check(Same(Theme.OnAccent, "#FFFFFF"), "深色 OnAccent = #FFFFFF");

        Console.WriteLine("== 5. 事件色映射（0–7 与 1.1.0 稳定对应）==");
        Check(Theme.EventColors.Length == 8, "事件色共 8 个");
        Check(Same(Theme.EventColor(0), "#4CC2FF"), "深色 事件色 0 = #4CC2FF");
        Check(Same(Theme.EventColor(7), "#9FB4FF"), "深色 事件色 7 = #9FB4FF");
        Check(Same(Theme.EventColor(99), "#4CC2FF"), "越界索引回退 0");
        Check(Same(Theme.EventColor(-3), "#4CC2FF"), "负索引回退 0");
        Theme.Apply(ThemeMode.Light);
        Check(Same(Theme.EventColor(0), "#0F6CBD"), "浅色 事件色 0 = #0F6CBD");
        Check(Same(Theme.EventColor(7), "#3B5BA5"), "浅色 事件色 7 = #3B5BA5");

        Console.WriteLine("== 6. 深色对比度 ==");
        Theme.Apply(ThemeMode.Dark);
        CheckPalette("深色");

        Console.WriteLine("== 7. 浅色对比度 ==");
        Theme.Apply(ThemeMode.Light);
        CheckPalette("浅色");

        Console.WriteLine("== 8. 主题变更广播（仅色彩实际变化时触发）==");
        Theme.Apply(ThemeMode.Dark);
        int events = 0;
        EventHandler handler = delegate { events++; };
        Theme.Changed += handler;
        Theme.Apply(ThemeMode.Dark);
        Check(events == 0, "重复应用同一档位不广播");
        Theme.Apply(ThemeMode.Light);
        Check(events == 1, "深色 -> 浅色广播一次 (实际 " + events + ")");
        Theme.Apply(ThemeMode.Light);
        Check(events == 1, "浅色 -> 浅色不广播");
        Theme.Changed -= handler;

        Console.WriteLine("== 9. <Theme> 持久化（写入 / 读回）==");
        string dir = Path.Combine(Path.GetTempPath(), "qingli-theme-test-" + Guid.NewGuid().ToString("N"));
        EventStore fresh = new EventStore(dir);
        fresh.Load();
        Check(!fresh.FileExistedAtLoad, "全新目录 FileExistedAtLoad = false");
        Check(Theme.ResolveInitialMode(fresh.Data.Settings.Theme, fresh.FileExistedAtLoad, fresh.FileHasThemeElement) == ThemeMode.System,
            "全新安装解析为 system");

        fresh.Data.Settings.Theme = "light";
        fresh.Save();
        string xml = File.ReadAllText(Path.Combine(dir, "data.xml"));
        Check(xml.IndexOf("<Theme>light</Theme>", StringComparison.Ordinal) >= 0, "data.xml 写入 <Theme>light</Theme>");

        EventStore reload = new EventStore(dir);
        reload.Load();
        Check(reload.FileExistedAtLoad, "重新加载 FileExistedAtLoad = true");
        Check(reload.FileHasThemeElement, "重新加载 FileHasThemeElement = true");
        Check(reload.Data.Settings.Theme == "light", "读回的主题值 = light");
        Check(Theme.ResolveInitialMode(reload.Data.Settings.Theme, reload.FileExistedAtLoad, reload.FileHasThemeElement) == ThemeMode.Light,
            "读回后解析为 light");

        Console.WriteLine("== 10. 升级 / 空值 / 损坏回退 ==");
        string dirUpgrade = Path.Combine(Path.GetTempPath(), "qingli-theme-upgrade-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dirUpgrade);
        File.WriteAllText(Path.Combine(dirUpgrade, "data.xml"),
            "<?xml version=\"1.0\"?><QingLiData><Version>1</Version><Settings><View>0</View></Settings><Events /></QingLiData>");
        EventStore upgrade = new EventStore(dirUpgrade);
        upgrade.Load();
        Check(upgrade.FileExistedAtLoad && !upgrade.FileHasThemeElement, "1.1.0 文件无 <Theme> 元素");
        Check(Theme.ResolveInitialMode(upgrade.Data.Settings.Theme, upgrade.FileExistedAtLoad, upgrade.FileHasThemeElement) == ThemeMode.Dark,
            "1.1.0 升级默认 dark");

        string dirEmpty = Path.Combine(Path.GetTempPath(), "qingli-theme-empty-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dirEmpty);
        File.WriteAllText(Path.Combine(dirEmpty, "data.xml"),
            "<?xml version=\"1.0\"?><QingLiData><Version>1</Version><Settings><Theme></Theme></Settings><Events /></QingLiData>");
        EventStore empty = new EventStore(dirEmpty);
        empty.Load();
        Check(empty.FileHasThemeElement, "空 <Theme> 元素被识别");
        Check(Theme.ResolveInitialMode(empty.Data.Settings.Theme, empty.FileExistedAtLoad, empty.FileHasThemeElement) == ThemeMode.System,
            "空 <Theme> 回退 system");

        string dirCorrupt = Path.Combine(Path.GetTempPath(), "qingli-theme-corrupt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dirCorrupt);
        File.WriteAllText(Path.Combine(dirCorrupt, "data.xml"), "<QingLiData><Settings><Theme>dark");
        EventStore corrupt = new EventStore(dirCorrupt);
        corrupt.Load();
        Check(corrupt.LoadFailed, "非法 XML 标记 LoadFailed");
        Check(!corrupt.FileExistedAtLoad && !corrupt.FileHasThemeElement, "损坏文件按全新安装判定");
        Check(Directory.GetFiles(dirCorrupt, "data.xml.corrupt-*").Length == 1, "生成 data.xml.corrupt-* 备份");
        Check(Theme.ResolveInitialMode(corrupt.Data.Settings.Theme, corrupt.FileExistedAtLoad, corrupt.FileHasThemeElement) == ThemeMode.System,
            "损坏文件回退 system");

        try { Directory.Delete(dir, true); } catch { }
        try { Directory.Delete(dirUpgrade, true); } catch { }
        try { Directory.Delete(dirEmpty, true); } catch { }
        try { Directory.Delete(dirCorrupt, true); } catch { }

        Console.WriteLine();
        Console.WriteLine("结果: " + passed + " 通过, " + failed + " 失败");
        return failed == 0 ? 0 : 1;
    }
}
