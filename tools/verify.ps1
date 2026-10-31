# 轻历回归验证脚本（无第三方依赖，供阶段 2/3 与日常改动使用）
# 用法： powershell -ExecutionPolicy Bypass -File tools\verify.ps1
# 覆盖：应用可编译 + 三个测试程序退出码 + 主题相关的静态约束 + 版本号一致性
$ErrorActionPreference = "Stop"
chcp 65001 | Out-Null
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$root = Split-Path -Parent $PSScriptRoot
$src = Join-Path $root "src"
$tests = Join-Path $root "tests"
$build = Join-Path $root "build"
New-Item -ItemType Directory -Force -Path $build | Out-Null

$script:passed = 0
$script:failed = 0

function Check($cond, [string]$name) {
    if ($cond) { $script:passed++; Write-Host ("  PASS  " + $name) }
    else { $script:failed++; Write-Host ("  FAIL  " + $name) -ForegroundColor Red }
}

function Section([string]$name) {
    Write-Host ""
    Write-Host ("== " + $name + " ==")
}

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { $csc = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe" }
if (-not (Test-Path $csc)) { throw "找不到系统自带的 csc.exe（.NET Framework 4.x）" }

$refs = @("/reference:System.dll", "/reference:System.Drawing.dll", "/reference:System.Windows.Forms.dll", "/reference:System.Xml.dll")
$sources = Get-ChildItem (Join-Path $src "*.cs") | ForEach-Object { $_.FullName }
$common = @("/nologo", "/langversion:5", "/codepage:65001", "/platform:anycpu") + $refs

Section "1. 应用编译（csc / .NET Framework 4.x，无第三方依赖）"
$exe = Join-Path $build "QingLi.exe"
$manifest = Join-Path $src "app.manifest"
$icon = Join-Path $build "app.ico"
$appArgs = @("/target:winexe", "/optimize+", "/out:$exe", "/win32manifest:$manifest")
if (Test-Path $icon) { $appArgs += "/win32icon:$icon" }
& $csc @common @appArgs $sources
Check ($LASTEXITCODE -eq 0 -and (Test-Path $exe)) "src\*.cs 编译通过 -> build\QingLi.exe"

Section "2. 自动化测试（退出码 0 视为通过）"
$suites = @(
    @{ Name = "TestCore";    Out = "TestCore.exe";    Files = @("src\Models.cs", "src\Ics.cs") },
    @{ Name = "TestHoliday"; Out = "TestHoliday.exe"; Files = @("src\HolidayService.cs", "src\HolidayData.cs", "src\JsonLite.cs") },
    @{ Name = "TestTheme";   Out = "TestTheme.exe";   Files = @("src\Theme.cs", "src\Models.cs", "src\Ics.cs") }
)
foreach ($suite in $suites) {
    $testFile = Join-Path $tests ($suite.Name + ".cs")
    $outFile = Join-Path $build $suite.Out
    $files = @($testFile) + ($suite.Files | ForEach-Object { Join-Path $root $_ })
    & $csc @common "/target:exe" "/out:$outFile" $files
    Check ($LASTEXITCODE -eq 0) ($suite.Name + " 编译通过")
    if ($LASTEXITCODE -eq 0) {
        & $outFile | Out-Null
        Check ($LASTEXITCODE -eq 0) ($suite.Name + " 断言全通过（退出码 " + $LASTEXITCODE + "）")
    }
}

Section "3. 主题静态约束（颜色字面量只允许出现在 Theme.cs）"
$csFiles = Get-ChildItem (Join-Path $src "*.cs") | Where-Object { $_.Name -ne "Theme.cs" }
$literal = $csFiles | Select-String -Pattern 'Color\.(White|Black|Gray|FromArgb|FromKnownColor)|ColorTranslator\.FromHtml'
Check ($literal.Count -eq 0) ("Theme.cs 之外无颜色字面量（命中 " + $literal.Count + " 处）")
$hex = $csFiles | Select-String -Pattern '#[0-9A-Fa-f]{6}\b'
Check ($hex.Count -eq 0) ("Theme.cs 之外无裸十六进制色值（命中 " + $hex.Count + " 处）")

Section "4. 主题功能静态约束"
$main = Get-Content (Join-Path $src "MainForm.cs") -Raw -Encoding UTF8
$theme = Get-Content (Join-Path $src "Theme.cs") -Raw -Encoding UTF8
$models = Get-Content (Join-Path $src "Models.cs") -Raw -Encoding UTF8
$controls = Get-Content (Join-Path $src "Controls.cs") -Raw -Encoding UTF8
Check ($theme.Contains("public enum ThemeMode")) "Theme 定义三档 ThemeMode"
Check ($theme.Contains("System = 0") -and $theme.Contains("Light = 1") -and $theme.Contains("Dark = 2")) "档位为 system / light / dark"
Check ($theme.Contains("AppsUseLightTheme")) "跟随系统读取 AppsUseLightTheme"
Check ($theme.Contains("ResolveInitialMode")) "启动默认值判定（全新安装 / 升级）"
Check ($theme.Contains("public static event EventHandler Changed")) "Theme.Changed 广播事件"
Check ($theme.Contains("DWMWA_USE_IMMERSIVE_DARK_MODE")) "标题栏 DWM 深色属性"
Check ($theme.Contains("int flag = dark ? 1 : 0")) "标题栏深色属性双向设置（浅色写 0）"
Check ($main.Contains("WM_SETTINGCHANGE") -and $main.Contains("ImmersiveColorSet")) "监听系统主题广播并匹配 ImmersiveColorSet"
Check ($main.Contains("ImmersiveColorSet") -and $main.Contains("_themeWatch.Interval = 500")) "系统广播 500ms 去抖"
Check ($main.Contains("BuildThemeMenu") -and $main.Contains("SyncThemeChecks")) "主窗口 / 托盘共用主题菜单并同步勾选"
Check ($main.Contains("跟随系统") -and $main.Contains("始终浅色") -and $main.Contains("始终深色")) "主题菜单三项中文文案"
Check ($main.Contains("_trayMenu.BackColor = Theme.Surface")) "托盘菜单随主题重设底色"
Check ($main.Contains("Keys.L") -and $main.Contains("e.Shift")) "Ctrl+Shift+L 循环切换"
Check ($models.Contains("public string Theme")) "AppSettings.Theme 持久化字段"
Check ($controls.Contains("RepaintTheme")) "Ui.RepaintTheme 统一切换重绘入口"
Check ($theme.Contains("Theme.Changed -= OnThemeChanged") -or $main.Contains("Theme.Changed -= OnThemeChanged")) "主题订阅在窗口关闭时取消（无泄漏）"
Check ($main.Contains("Marshal.PtrToStringAuto")) "WM_SETTINGCHANGE lParam 解析"
Check ($theme.Contains("OnAccent") -and $theme.Contains("DisabledText") -and $theme.Contains("DangerSoftBg") -and $theme.Contains("HolidayBadgeBg")) "新增语义令牌齐全"

$month = Get-Content (Join-Path $src "MonthView.cs") -Raw -Encoding UTF8
Check ($theme.Contains("SelectionBorder") -and $theme.Contains("SelectionFill")) "选中格描边 / 格底为显式令牌（SelectionBorder / SelectionFill）"
Check ($month.Contains("Theme.SelectionBorder, Theme.SelectionBorderWidth")) "月视图选中格描边改用强调色令牌 + 2px"
Check ($month.Contains("inMonth && !isSel")) "选中格不叠加悬停底色（无悬停 + 选中第三态）"
Check (-not $month.Contains("Mix(Theme.Accent, Theme.Surface, 0.35f)")) "月视图不再使用低对比度描边 Mix(Accent, Surface, 0.35)"

Section "5. 版本号一致性（AssemblyVersion 与 installer.iss）"
$asmInfo = Get-Content (Join-Path $src "AssemblyInfo.cs") -Raw -Encoding UTF8
$iss = Get-Content (Join-Path $root "installer.iss") -Raw -Encoding UTF8
$asmMatch = [regex]::Match($asmInfo, 'AssemblyVersion\("(\d+)\.(\d+)\.(\d+)')
$issMatch = [regex]::Match($iss, 'AppVersion\s+"(\d+)\.(\d+)\.(\d+)"')
Check ($asmMatch.Success) "src\AssemblyInfo.cs 声明 AssemblyVersion"
Check ($issMatch.Success) "installer.iss 声明 AppVersion"
if ($asmMatch.Success -and $issMatch.Success) {
    $a = $asmMatch.Groups[1].Value + "." + $asmMatch.Groups[2].Value + "." + $asmMatch.Groups[3].Value
    $i = $issMatch.Groups[1].Value + "." + $issMatch.Groups[2].Value + "." + $issMatch.Groups[3].Value
    Check ($a -eq $i) ("AssemblyVersion (" + $a + ") 与 installer.iss AppVersion (" + $i + ") 一致")
    Check ($a -eq "1.2.0") ("目标版本 1.2.0（实际 " + $a + "）")
} else {
    Check $false "版本号一致性"
    Check $false "目标版本 1.2.0"
}

Write-Host ""
Write-Host ("结果: " + $script:passed + " 通过, " + $script:failed + " 失败")
if ($script:failed -gt 0) { exit 1 }
exit 0
