# Stage D: fix the event's start time to now+X minutes, then wait for the reminder toast.
chcp 65001 | Out-Null
[Console]::OutputEncoding=[Text.Encoding]::UTF8
. .\qingli\tools\uilib.ps1

function Click-Dlg([int]$cx, [int]$cy) {
    $r = Get-WindowRectByTitle "编辑日程"
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

function Combo-Index([int]$cx, [int]$cy, [int]$index) {
    Click-Dlg $cx $cy
    Start-Sleep -Milliseconds 300
    [System.Windows.Forms.SendKeys]::SendWait("{HOME}")
    Start-Sleep -Milliseconds 200
    if ($index -gt 0) {
        [System.Windows.Forms.SendKeys]::SendWait("{DOWN " + $index + "}")
        Start-Sleep -Milliseconds 400
    }
    Click-Dlg 250 16
    Start-Sleep -Milliseconds 250
}

# target: now + 7 minutes floored to 5-minute step => between 3 and 7 minutes from now
$base = (Get-Date).AddMinutes(7)
$minute = [int][math]::Floor($base.Minute / 5) * 5
$H = $base.Hour
$M = $minute
$endTotal = $M + 30
$EH = ($H + [int][math]::Floor($endTotal / 60)) % 24
$EM = $endTotal % 60
Write-Host "target start=$($H):$($M) end=$($EH):$($EM) (now $(Get-Date -Format 'HH:mm:ss'))"

$w = Get-AppWindow
# open the recurring event (chip position: find via month view - today column, first chip)
ClickAt 1150 498
Start-Sleep -Milliseconds 1000
if ((Get-WindowRectByTitle "编辑日程") -eq $null) { "edit dialog did not open"; exit 1 }
$r = Get-WindowRectByTitle "编辑日程"
[void][Ui32]::TopMost($r.Hwnd, $true)
Start-Sleep -Milliseconds 300

Combo-Index 371 112 $H
Combo-Index 439 112 $M
Combo-Index 371 172 $EH
Combo-Index 439 172 $EM

$r = Get-WindowRectByTitle "编辑日程"
[void][Ui32]::TopMost($r.Hwnd, $true)
Start-Sleep -Milliseconds 300
Capture-WindowByTitle "编辑日程" (Join-Path (Get-Location) "qingli\shots\10-edit-time.png") | Out-Null

Click-Dlg 434 579
Start-Sleep -Milliseconds 1300
if ((Get-WindowRectByTitle "编辑日程") -eq $null) { "saved" } else { "STILL OPEN"; exit 1 }

"--- data ---"
Get-Content "$env:APPDATA\QingLi\data.xml" -Encoding UTF8 | Select-String -Pattern "<Start>|<End>" | ForEach-Object { $_.Line.Trim() }

# wait for the toast window (up to 9 minutes)
$deadline = (Get-Date).AddMinutes(9)
$found = $null
while ((Get-Date) -lt $deadline) {
    $hit = Get-ToastRect
    if ($hit -ne $null) {
        $found = $hit
        break
    }
    Start-Sleep -Seconds 5
}

if ($found -eq $null) { Write-Host "TOAST NOT SEEN within 9 minutes"; exit 1 }
Write-Host "toast at $($found.X),$($found.Y) $($found.W)x$($found.H)"
$bmpPath = Join-Path (Get-Location) "qingli\shots\11-toast.png"
Add-Type -AssemblyName System.Drawing
Start-Sleep -Milliseconds 400
$bmp = New-Object System.Drawing.Bitmap($found.W, $found.H)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($found.X, $found.Y, 0, 0, (New-Object System.Drawing.Size($found.W, $found.H)))
$g.Dispose()
$bmp.Save($bmpPath, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Host "saved $bmpPath"
