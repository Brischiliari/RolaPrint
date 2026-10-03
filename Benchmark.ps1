param([string]$Engine='CaptureEngine.cs')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing,System.Windows.Forms
Add-Type -TypeDefinition ([IO.File]::ReadAllText((Join-Path $PSScriptRoot $Engine))) -ReferencedAssemblies System.Drawing,System.Windows.Forms
$src=New-Object System.Drawing.Bitmap(1900,1800)
$g=[System.Drawing.Graphics]::FromImage($src);$g.Clear([System.Drawing.Color]::White)
$rng=New-Object System.Random(42)
for($i=0;$i -lt 900;$i++){$brush=New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb($rng.Next(256),$rng.Next(256),$rng.Next(256)));$g.FillRectangle($brush,$rng.Next(1800),$rng.Next(1750),$rng.Next(20,90),$rng.Next(5,25));$brush.Dispose()};$g.Dispose()
$a=$src.Clone((New-Object System.Drawing.Rectangle(0,0,1900,1200)),[System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
$b=$src.Clone((New-Object System.Drawing.Rectangle(0,240,1900,1200)),[System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
try {
$watch=[Diagnostics.Stopwatch]::StartNew();$shift=[CaptureEngine]::Match($a,$b);$watch.Stop();Write-Output "Match: $($watch.ElapsedMilliseconds) ms; deslocamento=$shift"
if($shift -ne 240){throw 'Alinhamento incorreto no benchmark de alta resolucao'}
$watch.Restart();[void][CaptureEngine]::Seam($a,$b,240,2);$watch.Stop();Write-Output "Seam: $($watch.ElapsedMilliseconds) ms"
} finally {$a.Dispose();$b.Dispose();$src.Dispose()}
