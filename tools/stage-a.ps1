# Stage A: create an event through the UI and capture evidence.
chcp 65001 | Out-Null
[Console]::OutputEncoding=[Text.Encoding]::UTF8
. .\qingli\tools\uilib.ps1

function Click-Dlg([int]$cx, [int]$cy) {
    $r = Get-WindowRectByTitle "新建日程"
    if ($r -eq $null) { throw "dialog not open" }
    $sx = $r.X + 3 + $cx
    $sy = $r.Y + 26 + $cy
    [void][Ui32]::SetCursorPos($sx, $sy)
    Start-Sleep -Milliseconds 150
    [Ui32]::mouse_event(0x0002, 0, 0, 0, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 50
    [Ui32]::mouse_event(0x0004, 0, 0, 0, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 450
}

function Open-Dialog {
    Get-AppWindow | Out-Null
    ClickAt 1122 58
    Start-Sleep -Milliseconds 900
    $r = Get-WindowRectByTitle "新建日程"
    if ($r -eq $null) { throw "dialog did not open" }
    [void][Ui32]::TopMost($r.Hwnd, $true)
    Start-Sleep -Milliseconds 300
    return $r
}

Get-Process QingLi -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 600
Start-Process -FilePath ".\qingli\build\QingLi.exe"
Start-Sleep -Seconds 3

$r = Open-Dialog
Write-Host "dialog at $($r.X),$($r.Y)"

# title
Click-Dlg 250 53
TypeText "项目评审周会"
# location
Click-Dlg 250 337
TypeText "会议室 3A"
# notes
Click-Dlg 250 439
TypeText "准备季度材料，带上原型演示"

# reminder combo -> pick "提前 5 分钟" (item index 2)
Click-Dlg 364 235
Start-Sleep -Milliseconds 400
$r2 = Get-WindowRectByTitle "新建日程"
$sx = $r2.X + 3 + 364
$sy = $r2.Y + 26 + 250 + 11 + (2 * 22)
[void][Ui32]::SetCursorPos($sx, $sy)
Start-Sleep -Milliseconds 200
[Ui32]::mouse_event(0x0002, 0, 0, 0, [IntPtr]::Zero)
Start-Sleep -Milliseconds 50
[Ui32]::mouse_event(0x0004, 0, 0, 0, [IntPtr]::Zero)
Start-Sleep -Milliseconds 500

Capture-WindowByTitle "新建日程" (Join-Path (Get-Location) "qingli\shots\02-dialog-filled.png") | Out-Null

# save
Click-Dlg 434 579
Start-Sleep -Milliseconds 1200

Capture-WindowByTitle "轻历 - 日程与提醒" (Join-Path (Get-Location) "qingli\shots\01-month.png") | Out-Null
Write-Host "stage A done"
