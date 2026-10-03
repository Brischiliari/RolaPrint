$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$bitmap = New-Object Drawing.Bitmap(256,256)
$g = [Drawing.Graphics]::FromImage($bitmap)
$g.SmoothingMode = 'AntiAlias'
$g.Clear([Drawing.Color]::Transparent)
$blue = New-Object Drawing.SolidBrush([Drawing.Color]::FromArgb(53,106,220))
$dark = New-Object Drawing.SolidBrush([Drawing.Color]::FromArgb(23,29,41))
$white = New-Object Drawing.SolidBrush([Drawing.Color]::FromArgb(235,242,255))
$pen = New-Object Drawing.Pen([Drawing.Color]::FromArgb(130,172,255),10)
$thin = New-Object Drawing.Pen([Drawing.Color]::FromArgb(130,172,255),7)
try {
 $g.FillRectangle($blue,16,27,224,155)
 $g.FillRectangle($dark,26,37,204,126)
 $g.FillRectangle($blue,113,181,30,25)
 $g.FillRectangle($blue,77,204,102,12)
 $g.DrawLine($thin,46,63,135,63)
 $g.DrawLine($thin,46,86,117,86)
 $g.DrawLine($thin,46,109,129,109)
 $g.FillEllipse($dark,142,104,92,134)
 $g.FillEllipse($white,151,112,74,118)
 $g.FillRectangle($white,151,150,74,24)
 $g.DrawLine($thin,188,117,188,147)
 $g.FillRectangle($blue,183,128,10,20)
 $g.DrawLine($pen,122,125,122,170)
 $g.DrawLine($pen,122,170,105,151)
 $g.DrawLine($pen,122,170,139,151)
 $png = New-Object IO.MemoryStream
 try {
  $bitmap.Save($png,[Drawing.Imaging.ImageFormat]::Png)
  $data=$png.ToArray()
  $file=[IO.File]::Create((Join-Path $PSScriptRoot 'RolaPrint.ico'))
  $writer=New-Object IO.BinaryWriter($file)
  try {
   $writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]1)
   $writer.Write([byte]0);$writer.Write([byte]0);$writer.Write([byte]0);$writer.Write([byte]0)
   $writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$data.Length);$writer.Write([uint32]22);$writer.Write($data)
  } finally {$writer.Dispose()}
 } finally {$png.Dispose()}
 $bitmap.Save((Join-Path $PSScriptRoot 'RolaPrint-icon.png'),[Drawing.Imaging.ImageFormat]::Png)
} finally {$g.Dispose();$bitmap.Dispose();$blue.Dispose();$dark.Dispose();$white.Dispose();$pen.Dispose();$thin.Dispose()}
