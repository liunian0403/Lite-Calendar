# Stage E: schedule a near-future event directly in the data file, restart the app, catch the reminder toast.
chcp 65001 | Out-Null
[Console]::OutputEncoding=[Text.Encoding]::UTF8
. .\qingli\tools\uilib.ps1

Get-Process QingLi -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 700

$start = (Get-Date).AddSeconds(80)
$end = $start.AddMinutes(30)
$sStart = $start.ToString("yyyy-MM-ddTHH:mm:sszzz")
$sEnd = $end.ToString("yyyy-MM-ddTHH:mm:sszzz")
Write-Host "scheduled test event at $sStart (now $(Get-Date -Format 'HH:mm:ss'))"

$path = "$env:APPDATA\QingLi\data.xml"
$xml = [IO.File]::ReadAllText($path, [Text.Encoding]::UTF8)
$marker = "</Events>"
$entry = "    <EventItem Id=`"testreminder001`">`r`n" +
         "      <Title>提醒测试会议</Title>`r`n" +
         "      <Start>$sStart</Start>`r`n" +
         "      <End>$sEnd</End>`r`n" +
         "      <AllDay>false</AllDay>`r`n" +
         "      <Location>线上</Location>`r`n" +
         "      <Notes>用于验证到点提醒</Notes>`r`n" +
         "      <ReminderMinutes>0</ReminderMinutes>`r`n" +
         "      <Recurrence>None</Recurrence>`r`n" +
         "      <RecurrenceUntil>0001-01-01T00:00:00</RecurrenceUntil>`r`n" +
         "      <ExcludedStarts />`r`n" +
         "      <ColorIndex>3</ColorIndex>`r`n" +
         "    </EventItem>`r`n" +
         "  </Events>"
$xml = $xml.Replace($marker, $entry)
[IO.File]::WriteAllText($path, $xml, (New-Object Text.UTF8Encoding($false)))

Start-Process -FilePath ".\qingli\build\QingLi.exe"
Start-Sleep -Seconds 3
Refresh-AppWindow

# wait for the toast
$deadline = (Get-Date).AddMinutes(4)
$found = $null
while ((Get-Date) -lt $deadline) {
    $hit = Get-ToastRect
    if ($hit -ne $null) { $found = $hit; break }
    Start-Sleep -Seconds 3
}
if ($found -eq $null) { Write-Host "TOAST NOT SEEN"; exit 1 }
Write-Host "toast at $($found.X),$($found.Y) $($found.W)x$($found.H)"
Start-Sleep -Milliseconds 500
Add-Type -AssemblyName System.Drawing
$bmp = New-Object System.Drawing.Bitmap($found.W, $found.H)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($found.X, $found.Y, 0, 0, (New-Object System.Drawing.Size($found.W, $found.H)))
$g.Dispose()
$out = Join-Path (Get-Location) "qingli\shots\11-toast.png"
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Host "saved $out"
