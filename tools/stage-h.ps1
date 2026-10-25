# Stage H: v1.1.0 UI verification —— 节假日徽标（休 / 班）与英文搜索（calander / national day）。
# 前置：需要未锁定的交互式桌面会话。本会话 SendKeys 不可用、WinForms 控件在 UIA 中均为 Pane，
#       故通过 EnumChildWindows 定位 WindowsForms10.EDIT 搜索框并 PostMessage WM_CHAR 输入。
# 用法：在仓库目录执行  powershell -ExecutionPolicy Bypass -File tools\stage-h.ps1
# 产物：shots\11-month-holidays.png、12-search-calander.png、13-search-nationalday.png、14-jumped-to-holiday.png
chcp 65001 | Out-Null
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot "uilib.ps1")

Add-Type @"
using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public class Win32Edit {
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr parent, EnumProc cb, IntPtr l);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint msg, UIntPtr w, IntPtr l);

    public static IntPtr FindEdit(IntPtr main) {
        IntPtr found = IntPtr.Zero;
        List<IntPtr> all = new List<IntPtr>();
        EnumChildWindows(main, delegate(IntPtr h, IntPtr l) { all.Add(h); return true; }, IntPtr.Zero);
        foreach (IntPtr h in all) {
            StringBuilder sb = new StringBuilder(256);
            GetClassName(h, sb, 256);
            if (sb.ToString().StartsWith("WindowsForms10.EDIT")) { found = h; break; }
        }
        return found;
    }

    public static bool TypeInto(IntPtr main, string text) {
        IntPtr edit = FindEdit(main);
        if (edit == IntPtr.Zero) return false;
        // 全选现有文本（EM_SETSEL: lParam = MAKELPARAM(0, 32767)），随后 WM_CHAR 逐字替换
        SendMessage(edit, 0x00B1, IntPtr.Zero, (IntPtr)2147418112);
        foreach (char c in text) PostMessage(edit, 0x0102, (UIntPtr)c, IntPtr.Zero);
        return true;
    }
}
"@

$shots = Join-Path $repo "shots"
New-Item -ItemType Directory -Force -Path $shots | Out-Null

function Get-MainRect {
    $q = Get-Process QingLi -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($q -eq $null) { throw "QingLi not running" }
    $rr = New-Object Ui32+RECT
    [void][Ui32]::GetWindowRect($q.MainWindowHandle, [ref]$rr)
    return @{ Hwnd = $q.MainWindowHandle; X = $rr.Left; Y = $rr.Top; W = ($rr.Right - $rr.Left); H = ($rr.Bottom - $rr.Top) }
}

function Shot-Main([string]$name) {
    $r = Get-MainRect
    $bmp = New-Object System.Drawing.Bitmap($r.W, $r.H)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($r.X, $r.Y, 0, 0, (New-Object System.Drawing.Size($r.W, $r.H)))
    $g.Dispose()
    $bmp.Save((Join-Path $shots $name), [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "saved $name ($($r.W)x$($r.H))"
}

function Set-SearchText([string]$text) {
    $r = Get-MainRect
    if (-not [Win32Edit]::TypeInto($r.Hwnd, $text)) { throw "search TextBox HWND not found" }
    Start-Sleep -Milliseconds 800
}

Get-Process QingLi -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 800

Start-Process -FilePath (Join-Path $repo "build\QingLi.exe")
Start-Sleep -Seconds 5

$r = Get-MainRect
Write-Host "window: $($r.X),$($r.Y) $($r.W)x$($r.H)"
[void][Ui32]::TopMost($r.Hwnd, $true)
Start-Sleep -Milliseconds 300
Shot-Main "11-month-holidays.png"

Set-SearchText "calander"          # 拼写变体，应显示"回到今天（Calendar）"
Shot-Main "12-search-calander.png"

Set-SearchText "national day"      # 英文别名，应显示"国庆节 · 法定放假"
Shot-Main "13-search-nationalday.png"

ClickAt 890 105                    # 点击下拉第一行 -> 跳转到 2026-10-01
Start-Sleep -Milliseconds 900
Shot-Main "14-jumped-to-holiday.png"

Get-Process QingLi -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 600
Start-Process -FilePath "$env:LOCALAPPDATA\Programs\QingLi\QingLi.exe"   # 恢复用户已安装的版本
Write-Host "stage H done"
