# Stage B: edit an event, then delete it.
chcp 65001 | Out-Null
[Console]::OutputEncoding=[Text.Encoding]::UTF8
. .\qingli\tools\uilib.ps1

function Click-Dlg([int]$cx, [int]$cy) {
    $r = Get-WindowRectByTitle "编辑日程"
    if ($r -eq $null) { $r = Get-WindowRectByTitle "新建日程" }
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

$w = Get-AppWindow
# make sure we are in month view
ClickAt 951 58
Start-Sleep -Milliseconds 700
# click the event chip on Oct 25
ClickAt 1160 507
Start-Sleep -Milliseconds 900

$r = Get-WindowRectByTitle "编辑日程"
if ($r -eq $null) { Write-Host "EDIT DIALOG DID NOT OPEN"; exit 1 }
[void][Ui32]::TopMost($r.Hwnd, $true)
Start-Sleep -Milliseconds 300
Capture-WindowByTitle "编辑日程" (Join-Path (Get-Location) "qingli\shots\05-edit.png") | Out-Null

# change title: select-all then type
Click-Dlg 250 53
[System.Windows.Forms.SendKeys]::SendWait("^a")
Start-Sleep -Milliseconds 200
TypeText "项目评审周会（已改期）"
Start-Sleep -Milliseconds 300
Capture-WindowByTitle "编辑日程" (Join-Path (Get-Location) "qingli\shots\05-edit2.png") | Out-Null

# save
Click-Dlg 434 579
Start-Sleep -Milliseconds 1200
$still = Get-WindowRectByTitle "编辑日程"
if ($still -eq $null) { Write-Host "edit dialog closed (saved)" } else { Write-Host "edit dialog STILL OPEN" }

$q = Get-Process QingLi | Select-Object -First 1
Refresh-AppWindow
Start-Sleep -Milliseconds 400
Capture-WindowByTitle "轻历 - 日程与提醒" (Join-Path (Get-Location) "qingli\shots\06-after-edit.png") | Out-Null

"--- data.xml title ---"
Select-String -Path "$env:APPDATA\QingLi\data.xml" -Pattern "Title|Start>" | ForEach-Object { $_.Line.Trim() }
