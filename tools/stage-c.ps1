# Stage C: create a recurring (daily) event with a soon reminder, for reminder + recurrence verification.
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
    Start-Sleep -Milliseconds 500
}

function Combo-Set([int]$cx, [int]$cy, [int]$itemIndex, [string]$value) {
    # click combo to open, type value (type-ahead), then dismiss by clicking a neutral spot
    Click-Dlg $cx $cy
    Start-Sleep -Milliseconds 300
    [System.Windows.Forms.SendKeys]::SendWait($value)
    Start-Sleep -Milliseconds 400
    Click-Dlg 250 16
    Start-Sleep -Milliseconds 300
}

$w = Get-AppWindow
# start time: now + 6 min, floored to a 5-minute step (>= 2 min in the future)
$base = (Get-Date).AddMinutes(6)
$minute = [int][math]::Floor($base.Minute / 5) * 5
$startH = $base.Hour
$startM = $minute
$endTotal = $minute + 30
$endH = ($startH + [int][math]::Floor($endTotal / 60)) % 24
$endM = $endTotal % 60
Write-Host "start=$($startH):$($startM) end=$($endH):$($endM)"

# open new event dialog
ClickAt 1122 58
Start-Sleep -Milliseconds 900
if ((Get-WindowRectByTitle "新建日程") -eq $null) { "dialog did not open"; exit 1 }
$r = Get-WindowRectByTitle "新建日程"
[void][Ui32]::TopMost($r.Hwnd, $true)
Start-Sleep -Milliseconds 300

Click-Dlg 250 53
TypeText "每日站会"

# times via type-ahead
Combo-Set 371 112 0 ("{0:00}" -f $startH)
Combo-Set 439 112 0 ("{0:00}" -f $startM)
Combo-Set 371 172 0 ("{0:00}" -f $endH)
Combo-Set 439 172 0 ("{0:00}" -f $endM)

# reminder -> item 1 (事件开始时)
Click-Dlg 364 235
Start-Sleep -Milliseconds 400
$r2 = Get-WindowRectByTitle "新建日程"
$sx = $r2.X + 3 + 364
$sy = $r2.Y + 26 + 250 + 11 + (1 * 22)
[void][Ui32]::SetCursorPos($sx, $sy)
Start-Sleep -Milliseconds 250
[Ui32]::mouse_event(0x0002, 0, 0, 0, [IntPtr]::Zero)
Start-Sleep -Milliseconds 60
[Ui32]::mouse_event(0x0004, 0, 0, 0, [IntPtr]::Zero)
Start-Sleep -Milliseconds 500

# repeat -> item 1 (每天)
Click-Dlg 127 235
Start-Sleep -Milliseconds 400
$r2 = Get-WindowRectByTitle "新建日程"
$sx = $r2.X + 3 + 127
$sy = $r2.Y + 26 + 250 + 11 + (1 * 22)
[void][Ui32]::SetCursorPos($sx, $sy)
Start-Sleep -Milliseconds 250
[Ui32]::mouse_event(0x0002, 0, 0, 0, [IntPtr]::Zero)
Start-Sleep -Milliseconds 60
[Ui32]::mouse_event(0x0004, 0, 0, 0, [IntPtr]::Zero)
Start-Sleep -Milliseconds 500

# screenshot before saving (evidence of the filled dialog)
$r = Get-WindowRectByTitle "新建日程"
[void][Ui32]::TopMost($r.Hwnd, $true)
Start-Sleep -Milliseconds 300
Capture-WindowByTitle "新建日程" (Join-Path (Get-Location) "qingli\shots\08-recurring-filled.png") | Out-Null

# save
Click-Dlg 434 579
Start-Sleep -Milliseconds 1300
if ((Get-WindowRectByTitle "新建日程") -eq $null) { "saved (dialog closed)" } else { "DIALOG STILL OPEN" }

Refresh-AppWindow
Capture-WindowByTitle "轻历 - 日程与提醒" (Join-Path (Get-Location) "qingli\shots\09-month-recurring.png") | Out-Null

"--- data ---"
Get-Content "$env:APPDATA\QingLi\data.xml" -Encoding UTF8 | Select-String -Pattern "Title|Start>|ReminderMinutes|Recurrence" | ForEach-Object { $_.Line.Trim() }
"next fire window: $startH`:$startM"
