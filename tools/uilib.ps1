# UI automation helpers for driving the QingLi window during verification.
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

Add-Type @"
using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public class Ui32 {
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
    [DllImport("user32.dll")] public static extern IntPtr FindWindow(string c, string n);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, IntPtr extra);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int cmd);

    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);

    public static List<string> ListWindows(uint target) {
        var res = new List<string>();
        EnumWindows(delegate(IntPtr h, IntPtr l) {
            uint pid; GetWindowThreadProcessId(h, out pid);
            if (pid == target && IsWindowVisible(h)) {
                var sb = new StringBuilder(256); GetWindowText(h, sb, 256);
                if (sb.Length > 0) res.Add(h + "|" + sb.ToString());
            }
            return true;
        }, IntPtr.Zero);
        return res;
    }

    [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT p);
    [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr h, uint flags);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }

    public static string At(int x, int y) {
        POINT p; p.X = x; p.Y = y;
        IntPtr h = WindowFromPoint(p);
        if (h == IntPtr.Zero) return "(none)";
        IntPtr root = GetAncestor(h, 2);
        if (root == IntPtr.Zero) root = h;
        uint pid; GetWindowThreadProcessId(root, out pid);
        var sb = new StringBuilder(256); GetWindowText(root, sb, 256);
        return "pid=" + pid + " title=[" + sb.ToString() + "]";
    }

    public static void TopMost(IntPtr h, bool on) {
        SetWindowPos(h, on ? new IntPtr(-1) : new IntPtr(-2), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0040);
    }

    public static string ListRects(uint target) {
        var res = new System.Collections.Generic.List<string>();
        EnumWindows(delegate(IntPtr h, IntPtr l) {
            uint pid; GetWindowThreadProcessId(h, out pid);
            if (pid == target && IsWindowVisible(h)) {
                RECT r; GetWindowRect(h, out r);
                var sb = new StringBuilder(256); GetWindowText(h, sb, 256);
                res.Add(h + "|" + r.Left + "," + r.Top + "," + r.Right + "," + r.Bottom + "|" + sb.ToString());
            }
            return true;
        }, IntPtr.Zero);
        return string.Join(";;", res.ToArray());
    }

    public static bool FindByTitle(uint target, string part, out RECT rect, out IntPtr hwnd) {
        rect = new RECT(); rect.Left = -1; hwnd = IntPtr.Zero;
        IntPtr[] all = new IntPtr[512];
        int n = 0;
        EnumWindows(delegate(IntPtr h, IntPtr l) {
            if (n >= 512) return false;
            uint pid; GetWindowThreadProcessId(h, out pid);
            if (pid == target) { all[n] = h; n++; }
            return true;
        }, IntPtr.Zero);
        for (int i = 0; i < n; i++) {
            var sb = new StringBuilder(256); GetWindowText(all[i], sb, 256);
            if (sb.ToString().Contains(part)) {
                hwnd = all[i];
                GetWindowRect(all[i], out rect);
                return true;
            }
        }
        return false;
    }
}
"@

$script:Win = $null

function Get-AppWindow([string]$Title = "轻历") {
    $p = Get-Process | Where-Object { $_.MainWindowTitle -like "*$Title*" } | Select-Object -First 1
    if ($p -eq $null) { throw "window not found: $Title" }
    $r = New-Object Ui32+RECT
    [void][Ui32]::ShowWindow($p.MainWindowHandle, 9)
    [void][Ui32]::SetForegroundWindow($p.MainWindowHandle)
    [void][Ui32]::TopMost($p.MainWindowHandle, $true)
    Start-Sleep -Milliseconds 400
    [void][Ui32]::GetWindowRect($p.MainWindowHandle, [ref]$r)
    $script:Win = $r
    return $r
}

function RelToScreen([int]$rx, [int]$ry) {
    $sx = $script:Win.Left + $rx
    $sy = $script:Win.Top + $ry
    return ,@($sx, $sy)
}

function MoveTo([int]$rx, [int]$ry) {
    $p = RelToScreen $rx $ry
    [void][Ui32]::SetCursorPos($p[0], $p[1])
    Start-Sleep -Milliseconds 120
}

function LeftDown { [Ui32]::mouse_event(0x0002, 0, 0, 0, [IntPtr]::Zero) }
function LeftUp { [Ui32]::mouse_event(0x0004, 0, 0, 0, [IntPtr]::Zero) }

function ClickAt([int]$rx, [int]$ry) {
    MoveTo $rx $ry
    LeftDown; Start-Sleep -Milliseconds 40; LeftUp
    Start-Sleep -Milliseconds 350
}

function DoubleClickAt([int]$rx, [int]$ry) {
    MoveTo $rx $ry
    LeftDown; Start-Sleep -Milliseconds 40; LeftUp
    Start-Sleep -Milliseconds 60
    LeftDown; Start-Sleep -Milliseconds 40; LeftUp
    Start-Sleep -Milliseconds 450
}

function RightClickAt([int]$rx, [int]$ry) {
    MoveTo $rx $ry
    [Ui32]::mouse_event(0x0008, 0, 0, 0, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 40
    [Ui32]::mouse_event(0x0010, 0, 0, 0, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 450
}

function TypeText([string]$text) {
    if ($text -eq "") { return }
    Set-Clipboard -Value $text
    Start-Sleep -Milliseconds 120
    [System.Windows.Forms.SendKeys]::SendWait("^v")
    Start-Sleep -Milliseconds 250
}

function SendKey([string]$keys) {
    [System.Windows.Forms.SendKeys]::SendWait($keys)
    Start-Sleep -Milliseconds 300
}

function Shot([string]$path) {
    & "$PSScriptRoot\screenshot.ps1" -Out $path -Title "轻历" | Out-Null
}

function Refresh-AppWindow {
    Add-Type @"
using System;
using System.Runtime.InteropServices;
public class UiRedraw2 {
    [DllImport("user32.dll")] public static extern bool RedrawWindow(IntPtr h, IntPtr rgn, IntPtr upd, uint flags);
}
"@
    $q = Get-Process QingLi | Select-Object -First 1
    if ($q -eq $null) { return }
    [void][Ui32]::TopMost($q.MainWindowHandle, $true)
    [void][UiRedraw2]::RedrawWindow($q.MainWindowHandle, [IntPtr]::Zero, [IntPtr]::Zero, (0x1 -bor 0x4 -bor 0x80 -bor 0x400 -bor 0x100))
    Start-Sleep -Milliseconds 600
}

# Returns the rect of the reminder toast window (a small top-level window of QingLi that is not the main window).
function Get-ToastRect {
    $q = Get-Process QingLi -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($q -eq $null) { return $null }
    $raw = [Ui32]::ListRects([uint32]$q.Id)
    if ([string]::IsNullOrEmpty($raw)) { return $null }
    foreach ($entry in ($raw -split ';;')) {
        $bits = $entry -split '\|', 3
        if ($bits.Count -lt 2) { continue }
        $hwnd = [IntPtr]::Zero
        try { $hwnd = [IntPtr]([long]$bits[0]) } catch { continue }
        if ($hwnd -eq $q.MainWindowHandle) { continue }
        $xy = $bits[1] -split ','
        if ($xy.Count -ne 4) { continue }
        $x = [int]$xy[0]; $y = [int]$xy[1]; $w = [int]$xy[2] - $x; $h = [int]$xy[3] - $y
        if ($w -ge 300 -and $w -le 500 -and $h -ge 90 -and $h -le 200) {
            return @{ X = $x; Y = $y; W = $w; H = $h; Hwnd = $hwnd }
        }
    }
    return $null
}

function Get-WindowRectByTitle([string]$title) {
    $proc = Get-Process QingLi | Select-Object -First 1
    if ($proc -eq $null) { return $null }
    $r = New-Object Ui32+RECT
    $hwnd = [IntPtr]::Zero
    if ([Ui32]::FindByTitle([uint32]$proc.Id, $title, [ref]$r, [ref]$hwnd)) {
        return @{ X = $r.Left; Y = $r.Top; W = ($r.Right - $r.Left); H = ($r.Bottom - $r.Top); Hwnd = $hwnd }
    }
    return $null
}

function Capture-WindowByTitle([string]$title, [string]$out) {
    $rect = Get-WindowRectByTitle $title
    if ($rect -eq $null) { Write-Host "window not found: $title"; return $false }
    Add-Type -AssemblyName System.Drawing
    $bmp = New-Object System.Drawing.Bitmap($rect.W, $rect.H)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($rect.X, $rect.Y, 0, 0, (New-Object System.Drawing.Size($rect.W, $rect.H)))
    $g.Dispose()
    $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "saved $out ($($rect.W) x $($rect.H)) at $($rect.X),$($rect.Y)"
    return $true
}

function Zoom-Crop([string]$srcPath, [string]$outPath, [int]$x, [int]$y, [int]$w, [int]$h, [int]$zoom) {
    Add-Type -AssemblyName System.Drawing
    $src = [System.Drawing.Image]::FromFile((Resolve-Path $srcPath))
    $rect = New-Object System.Drawing.Rectangle($x, $y, $w, $h)
    $bmp = New-Object System.Drawing.Bitmap(($w * $zoom), ($h * $zoom))
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
    $g.DrawImage($src, (New-Object System.Drawing.Rectangle(0, 0, $bmp.Width, $bmp.Height)), $rect, [System.Drawing.GraphicsUnit]::Pixel)
    $g.Dispose()
    $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose(); $src.Dispose()
    Write-Host "zoomed -> $outPath"
}
