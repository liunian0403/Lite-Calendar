# 轻历 (QingLi) build script
# 1) generate app.ico  2) compile with .NET Framework csc  3) build Inno Setup installer
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$src = Join-Path $root "src"
$build = Join-Path $root "build"
$dist = Join-Path $root "dist"
New-Item -ItemType Directory -Force -Path $build, $dist | Out-Null

Add-Type -AssemblyName System.Drawing

function Add-RoundRect($path, [double]$x, [double]$y, [double]$w, [double]$h, [double]$r) {
    $d = $r * 2
    $path.AddArc([float]$x, [float]$y, [float]$d, [float]$d, 180, 90)
    $path.AddArc([float]($x + $w - $d), [float]$y, [float]$d, [float]$d, 270, 90)
    $path.AddArc([float]($x + $w - $d), [float]($y + $h - $d), [float]$d, [float]$d, 0, 90)
    $path.AddArc([float]$x, [float]($y + $h - $d), [float]$d, [float]$d, 90, 90)
    $path.CloseFigure()
}

function New-IconFile([string]$path) {
    $sizes = @(16, 24, 32, 48, 64, 128, 256)
    $blobs = New-Object System.Collections.ArrayList

    foreach ($s in $sizes) {
        $bmp = New-Object System.Drawing.Bitmap($s, $s)
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $g.Clear([System.Drawing.Color]::Transparent)

        $m = [double]$s * 0.03
        $bw = [double]$s - 2 * $m
        $bg = New-Object System.Drawing.Drawing2D.GraphicsPath
        Add-RoundRect $bg $m $m $bw $bw ([double]$s * 0.22)
        $c1 = [System.Drawing.Color]::FromArgb(255, 92, 200, 255)
        $c2 = [System.Drawing.Color]::FromArgb(255, 15, 108, 189)
        $rect = New-Object System.Drawing.RectangleF([float]$m, [float]$m, [float]$bw, [float]$bw)
        $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, $c1, $c2, 55)
        $g.FillPath($brush, $bg)
        $brush.Dispose()

        # calendar sheet
        $sx = [double]$s * 0.17
        $sy = [double]$s * 0.24
        $sw = [double]$s * 0.66
        $sh = [double]$s * 0.56
        $sheet = New-Object System.Drawing.Drawing2D.GraphicsPath
        Add-RoundRect $sheet $sx $sy $sw $sh ([double]$s * 0.09)
        $white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 250, 252, 255))
        $g.FillPath($white, $sheet)

        # header band
        $clip = $g.Clip
        $g.SetClip($sheet, [System.Drawing.Drawing2D.CombineMode]::Intersect)
        $band = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 15, 108, 189))
        $g.FillRectangle($band, [float]$sx, [float]$sy, [float]$sw, [float]($s * 0.15))
        $band.Dispose()
        $g.Clip = $clip

        # rings
        $ringBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 15, 108, 189))
        foreach ($rx in @(0.31, 0.57)) {
            $rp = New-Object System.Drawing.Drawing2D.GraphicsPath
            Add-RoundRect $rp ([double]$s * $rx) ([double]$s * 0.15) ([double]$s * 0.07) ([double]$s * 0.17) ([double]$s * 0.035)
            $g.FillPath($ringBrush, $rp)
            $rp.Dispose()
        }
        $ringBrush.Dispose()

        # day dots
        if ($s -ge 24) {
            $dot = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 60, 110, 160))
            $dr = [double]$s * 0.075
            foreach ($dy in @(0.46, 0.60)) {
                foreach ($dx in @(0.25, 0.42, 0.59)) {
                    $dp = New-Object System.Drawing.Drawing2D.GraphicsPath
                    Add-RoundRect $dp ([double]$s * $dx) ([double]$s * $dy) $dr $dr ([double]$s * 0.02)
                    $g.FillPath($dot, $dp)
                    $dp.Dispose()
                }
            }
            $dot.Dispose()
        }

        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        [void]$blobs.Add($ms.ToArray())
        $ms.Dispose()
        $g.Dispose()
        $bmp.Dispose()
        $sheet.Dispose()
        $bg.Dispose()
    }

    $fs = New-Object System.IO.FileStream($path, [System.IO.FileMode]::Create)
    $bw2 = New-Object System.IO.BinaryWriter($fs)
    $bw2.Write([UInt16]0)
    $bw2.Write([UInt16]1)
    $bw2.Write([UInt16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $s = $sizes[$i]
        $len = $blobs[$i].Length
        if ($s -ge 256) { $bw2.Write([byte]0) } else { $bw2.Write([byte]$s) }
        if ($s -ge 256) { $bw2.Write([byte]0) } else { $bw2.Write([byte]$s) }
        $bw2.Write([byte]0)
        $bw2.Write([byte]0)
        $bw2.Write([UInt16]1)
        $bw2.Write([UInt16]32)
        $bw2.Write([UInt32]$len)
        $bw2.Write([UInt32]$offset)
        $offset += $len
    }
    for ($i = 0; $i -lt $sizes.Count; $i++) { $bw2.Write($blobs[$i]) }
    $bw2.Flush()
    $bw2.Close()
    $fs.Close()
    Write-Host "icon -> $path"
}

$icoPath = Join-Path $build "app.ico"
New-IconFile $icoPath

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { $csc = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe" }

$exe = Join-Path $build "QingLi.exe"
if (Test-Path $exe) { Remove-Item $exe -Force }
$sources = Get-ChildItem (Join-Path $src "*.cs") | ForEach-Object { $_.FullName }

& $csc /nologo /target:winexe /platform:anycpu /langversion:5 /codepage:65001 /optimize+ `
    "/out:$exe" "/win32icon:$icoPath" "/win32manifest:$(Join-Path $src 'app.manifest')" `
    /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Xml.dll `
    $sources
if ($LASTEXITCODE -ne 0) { throw "csc failed with exit code $LASTEXITCODE" }
Write-Host "compiled -> $exe"

$iscc = Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $iscc)) { $iscc = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" }
& $iscc (Join-Path $root "installer.iss")
if ($LASTEXITCODE -ne 0) { throw "ISCC failed with exit code $LASTEXITCODE" }
Write-Host "installer ready in $dist"
