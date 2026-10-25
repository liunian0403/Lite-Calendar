# Stage F: search, ICS export and ICS import through the UI.
chcp 65001 | Out-Null
[Console]::OutputEncoding=[Text.Encoding]::UTF8
. .\qingli\tools\uilib.ps1

function Click-Menu([int]$index) {
    # dropdown items: index 0 = 导入, 1 = 导出, 2 = 打开数据文件夹, 3 = 关于
    $w = Get-AppWindow
    $sx = $w.Left + 1214
    $sy = $w.Top + 59 + 30 + 8 + ($index * 26)
    [void][Ui32]::SetCursorPos($sx, $sy)
    Start-Sleep -Milliseconds 250
    [Ui32]::mouse_event(0x0002, 0, 0, 0, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 60
    [Ui32]::mouse_event(0x0004, 0, 0, 0, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 900
}

$w = Get-AppWindow
Refresh-AppWindow

# ---- search ----
$cx = $w.Left + 809
$cy = $w.Top + 60
[void][Ui32]::SetCursorPos($cx, $cy)
Start-Sleep -Milliseconds 200
[Ui32]::mouse_event(0x0002, 0, 0, 0, [IntPtr]::Zero)
Start-Sleep -Milliseconds 60
[Ui32]::mouse_event(0x0004, 0, 0, 0, [IntPtr]::Zero)
Start-Sleep -Milliseconds 400
TypeText "站会"
Start-Sleep -Milliseconds 900
Capture-WindowByTitle "轻历 - 日程与提醒" (Join-Path (Get-Location) "qingli\shots\12-search.png") | Out-Null
# clear search with Escape
[System.Windows.Forms.SendKeys]::SendWait("{ESC}")
Start-Sleep -Milliseconds 500

# ---- ICS export ----
$docs = Join-Path $env:USERPROFILE "Documents"
$icsPath = Join-Path $docs "qingli-test.ics"
if (Test-Path $icsPath) { Remove-Item $icsPath -Force }

$w = Get-AppWindow
[void][Ui32]::SetCursorPos(($w.Left + 1214), ($w.Top + 59))
Start-Sleep -Milliseconds 250
[Ui32]::mouse_event(0x0002, 0, 0, 0, [IntPtr]::Zero)
Start-Sleep -Milliseconds 60
[Ui32]::mouse_event(0x0004, 0, 0, 0, [IntPtr]::Zero)
Start-Sleep -Milliseconds 700
# item 1 = 导出
$sx = $w.Left + 1214
$sy = $w.Top + 59 + 30 + 8 + (1 * 26)
[void][Ui32]::SetCursorPos($sx, $sy)
Start-Sleep -Milliseconds 250
[Ui32]::mouse_event(0x0002, 0, 0, 0, [IntPtr]::Zero)
Start-Sleep -Milliseconds 60
[Ui32]::mouse_event(0x0004, 0, 0, 0, [IntPtr]::Zero)
Start-Sleep -Milliseconds 1200

# save dialog: type full path and press Enter
TypeText $icsPath
Start-Sleep -Milliseconds 400
[System.Windows.Forms.SendKeys]::SendWait("{ENTER}")
Start-Sleep -Milliseconds 1500
# dismiss the confirmation message box
[System.Windows.Forms.SendKeys]::SendWait("{ENTER}")
Start-Sleep -Milliseconds 800

if (Test-Path $icsPath) {
    Write-Host "ICS exported: $icsPath ($((Get-Item $icsPath).Length) bytes)"
    Get-Content $icsPath -Encoding UTF8 | Select-Object -First 22
} else {
    Write-Host "ICS EXPORT FAILED"
}

# ---- ICS import ----
$w = Get-AppWindow
[void][Ui32]::SetCursorPos(($w.Left + 1214), ($w.Top + 59))
Start-Sleep -Milliseconds 250
[Ui32]::mouse_event(0x0002, 0, 0, 0, [IntPtr]::Zero)
Start-Sleep -Milliseconds 60
[Ui32]::mouse_event(0x0004, 0, 0, 0, [IntPtr]::Zero)
Start-Sleep -Milliseconds 700
$sx = $w.Left + 1214
$sy = $w.Top + 59 + 30 + 8 + (0 * 26)
[void][Ui32]::SetCursorPos($sx, $sy)
Start-Sleep -Milliseconds 250
[Ui32]::mouse_event(0x0002, 0, 0, 0, [IntPtr]::Zero)
Start-Sleep -Milliseconds 60
[Ui32]::mouse_event(0x0004, 0, 0, 0, [IntPtr]::Zero)
Start-Sleep -Milliseconds 1200
TypeText $icsPath
Start-Sleep -Milliseconds 400
[System.Windows.Forms.SendKeys]::SendWait("{ENTER}")
Start-Sleep -Milliseconds 2000
[System.Windows.Forms.SendKeys]::SendWait("{ENTER}")
Start-Sleep -Milliseconds 800
Refresh-AppWindow
Capture-WindowByTitle "轻历 - 日程与提醒" (Join-Path (Get-Location) "qingli\shots\13-after-import.png") | Out-Null
$c = (Select-String -Path "$env:APPDATA\QingLi\data.xml" -Pattern "<EventItem" -AllMatches).Count
Write-Host "events after import: $c"
