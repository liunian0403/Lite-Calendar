# Stage H: v1.1.0 UI verification —— 节假日徽标（休 / 班）与英文搜索（calander / national day）。
# 前置：需要未锁定的交互式桌面会话（SendKeys 在部分会话不可用，故用 UI Automation 设置搜索框文本；
#       锁屏状态下 UIA 与截图均不可用）。以管理员以外的普通权限运行即可。
# 用法：在仓库父目录执行  powershell -ExecutionPolicy Bypass -File tools\stage-h.ps1
# 产物：shots\11-month-holidays.png（月视图节假日）、12-search-calander.png、13-search-nationalday.png、14-jumped-to-holiday.png
chcp 65001 | Out-Null
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot "uilib.ps1")
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

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
    $win = [System.Windows.Automation.AutomationElement]::FromHandle($r.Hwnd)
    $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Edit)
    $edits = $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)
    if ($edits.Count -lt 1) { throw "search TextBox not found (会话锁定或窗口未显示?)" }
    $vp = $edits.Item(0).GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
    $vp.SetValue($text)
    Start-Sleep -Milliseconds 700
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