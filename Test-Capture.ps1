param([string]$Executable = 'RolaPrint.exe')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing,System.Windows.Forms
Add-Type -TypeDefinition ([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'CaptureEngine.cs'))) -ReferencedAssemblies System.Drawing,System.Windows.Forms
$a=New-Object System.Drawing.Bitmap(220,300)
$b=New-Object System.Drawing.Bitmap(220,300)
try {
for($y=0;$y -lt 300;$y++){for($x=0;$x -lt 220;$x++){
$a.SetPixel($x,$y,[System.Drawing.Color]::FromArgb(($y*17 % 256),($y*31 % 256),($x*7 % 256)))
$b.SetPixel($x,$y,[System.Drawing.Color]::FromArgb((($y+60)*17 % 256),(($y+60)*31 % 256),($x*7 % 256)))
}}
if([CaptureEngine]::Match($a,$b) -ne 60){throw 'Falha no alinhamento'}
if([CaptureEngine]::Match($a,$a) -ne 0){throw 'Falha na deteccao de fim'}
Write-Output 'PASS: sintaxe, compilacao, alinhamento e deteccao de imagem repetida.'
} finally {$a.Dispose();$b.Dispose()}

# Text on a mostly white document, with unequal line spacing and a partial final scroll.
$document=New-Object System.Drawing.Bitmap(480,900)
$g=[System.Drawing.Graphics]::FromImage($document)
$font=New-Object System.Drawing.Font('Arial',12)
try {
 $g.Clear([System.Drawing.Color]::White)
 for($i=0;$i -lt 25;$i++){$g.DrawString("Linha $i - exemplo de documento $($i * 137)",$font,[System.Drawing.Brushes]::Black,65,(10+$i*33+($i%3)*2))}
} finally {$g.Dispose();$font.Dispose()}
$frames=New-Object 'System.Collections.Generic.List[System.Drawing.Bitmap]'
try {
 foreach($y in @(0,73,146,165)){$r=New-Object System.Drawing.Rectangle(0,$y,480,350);$frames.Add($document.Clone($r,[System.Drawing.Imaging.PixelFormat]::Format24bppRgb))}
 $expected=@(73,73,19)
 for($i=1;$i -lt $frames.Count;$i++){
  $actual=[CaptureEngine]::Match($frames[$i-1],$frames[$i])
  if($actual -ne $expected[$i-1]){throw "Falha em documento com margens: esperado $($expected[$i-1]), obtido $actual"}
 }
 $merged=New-Object System.Drawing.Bitmap(480,515)
 $g=[System.Drawing.Graphics]::FromImage($merged)
 try {
  $g.DrawImageUnscaled($frames[0],0,0);$dest=350
  for($i=1;$i -lt $frames.Count;$i++){
   $n=$expected[$i-1];$r=New-Object System.Drawing.Rectangle(0,(350-$n),480,$n)
   $piece=$frames[$i].Clone($r,[System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
   try {$g.DrawImageUnscaled($piece,0,$dest)} finally {$piece.Dispose()};$dest+=$n
  }
 } finally {$g.Dispose()}
 try {
  for($y=0;$y -lt 515;$y++){for($x=0;$x -lt 480;$x++){
   if($merged.GetPixel($x,$y).ToArgb() -ne $document.GetPixel($x,$y).ToArgb()){throw "Montagem incorreta em $x,$y"}
  }}
 } finally {$merged.Dispose()}
 Write-Output 'PASS: margens brancas, rolagem parcial e montagem sem cortes ou repeticoes.'
 $unrelated=New-Object System.Drawing.Bitmap(480,350)
 $g=[System.Drawing.Graphics]::FromImage($unrelated)
 try {$g.Clear([System.Drawing.Color]::Navy)} finally {$g.Dispose()}
 try {if([CaptureEngine]::Match($frames[0],$unrelated) -ne -1){throw 'Aceitou imagens sem correspondencia'}} finally {$unrelated.Dispose()}
 Write-Output 'PASS: rejeicao de imagens sem correspondencia.'
} finally {foreach($b in $frames){$b.Dispose()};$document.Dispose()}

$a=New-Object System.Drawing.Bitmap(220,300)
$b=New-Object System.Drawing.Bitmap(220,300)
try {
 for($y=0;$y -lt 300;$y++){for($x=0;$x -lt 220;$x++){
  $a.SetPixel($x,$y,[System.Drawing.Color]::FromArgb((($y%20)*12),($x*7%256),0))
  $b.SetPixel($x,$y,[System.Drawing.Color]::FromArgb(((($y+7)%20)*12),($x*7%256),0))
 }}
 if([CaptureEngine]::Match($a,$b) -ne -1){throw 'Aceitou padrao repetitivo ambiguo'}
 Write-Output 'PASS: rejeicao de padrao repetitivo ambiguo.'
} finally {$a.Dispose();$b.Dispose()}

# Construct the full UI and register its event handlers without displaying or capturing.
$assembly=[Reflection.Assembly]::LoadFrom((Join-Path $PSScriptRoot $Executable))
$type=$assembly.GetType('MainWindow',$true)
$form=[Activator]::CreateInstance($type,$true)
$capture=$type.GetField('capture',[Reflection.BindingFlags]'Instance,NonPublic').GetValue($form)
$save=$type.GetField('save',[Reflection.BindingFlags]'Instance,NonPublic').GetValue($form)
try {
 if($form.Controls.Count -ne 1){throw 'Layout principal inesperado'}
 if($capture.Enabled -or $save.Enabled){throw 'Estado inicial invalido'}
 Write-Output 'PASS: construcao da interface e estado inicial.'
} finally {$form.Dispose()}

$controls=New-Object CaptureControls
try {
 $controls.ClientSize=New-Object System.Drawing.Size(300,100)
 $stop=New-Object System.Windows.Forms.Button
 $stop.Text='Parar';$controls.Controls.Add($stop)
 $stop.Add_Click({[CaptureEngine]::StopRequested=$true})
 [CaptureEngine]::StopRequested=$false
 # Dispatch the click without showing the test window (PerformClick requires visibility).
 $clickMethod=$stop.GetType().GetMethod('OnClick',[System.Reflection.BindingFlags]'Instance,NonPublic')
 [void]$clickMethod.Invoke($stop,@([System.EventArgs]::Empty))
 if(-not [CaptureEngine]::Cancelled()){throw 'Botao Parar nao cancela'}
 if([CaptureEngine]::Wait(100)){throw 'Espera ignorou cancelamento'}
 $testA=New-Object Drawing.Bitmap(100,100);$testB=New-Object Drawing.Bitmap(100,100)
 try{if([CaptureEngine]::Match($testA,$testB) -ne -2){throw 'Alinhamento ignorou cancelamento'}}finally{$testA.Dispose();$testB.Dispose()}
 Write-Output 'PASS: botao Parar e cancelamento durante espera.'
} finally {[CaptureEngine]::StopRequested=$false;$controls.Dispose()}

$nativeSource=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'NativeApp.cs'))
if($nativeSource.Contains('Process.Start') -or $nativeSource.Contains('powershell.exe')){throw 'Aplicativo ainda depende de iniciador externo'}
if(-not $nativeSource.Contains('while(!CaptureEngine.Cancelled())')){throw 'Loop de captura manual ausente'}
Write-Output 'PASS: limites de tentativas e encerramento por imagem repetida removidos.'

# Regression: sticky header + bottom floating button across five scroll steps.
$doc=New-Object System.Drawing.Bitmap(480,900)
$g=[System.Drawing.Graphics]::FromImage($doc)
$random=New-Object System.Random(42)
try {
 for($y=0;$y -lt 900;$y+=5){for($x=0;$x -lt 480;$x+=8){
  $brush=New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb($random.Next(256),$random.Next(256),$random.Next(256)))
  try {$g.FillRectangle($brush,$x,$y,8,5)}finally{$brush.Dispose()}
 }}
}finally{$g.Dispose()}
$parts=New-Object 'System.Collections.Generic.List[System.Drawing.Bitmap]'
$previous=$null;$tailStart=0;$height=350
try {
 foreach($offset in @(0,60,120,180,240,257)){
  $frame=[CaptureEngine]::Crop($doc,$offset,350)
  $g=[System.Drawing.Graphics]::FromImage($frame)
  try {
   $g.FillRectangle([System.Drawing.Brushes]::Black,0,0,480,24)
   $g.FillRectangle([System.Drawing.Brushes]::Gold,360,295,110,35)
   $g.DrawRectangle([System.Drawing.Pens]::White,360,295,109,34)
  }finally{$g.Dispose()}
  if($null -eq $previous){$parts.Add($frame.Clone());$previous=$frame;continue}
  $shift=[CaptureEngine]::Match($previous,$frame)
  $wanted=if($offset -eq 257){17}else{60}
  if($shift -ne $wanted){$frame.Dispose();throw "Alinhamento com elementos fixos: esperado $wanted, obtido $shift"}
  $seam=[CaptureEngine]::Seam($previous,$frame,$shift,[Math]::Max(2,($tailStart-$shift+1)))
  $last=$parts.Count-1;$old=$parts[$last]
  $parts[$last]=[CaptureEngine]::Crop($old,0,($shift+$seam-$tailStart));$old.Dispose()
  $parts.Add([CaptureEngine]::Crop($frame,$seam,(350-$seam)))
  $tailStart=$seam;$height+=$shift;$previous.Dispose();$previous=$frame
 }
 $actual=New-Object System.Drawing.Bitmap(480,$height)
 $g=[System.Drawing.Graphics]::FromImage($actual)
 try {$dest=0;foreach($piece in $parts){$g.DrawImageUnscaled($piece,0,$dest);$dest+=$piece.Height}}finally{$g.Dispose()}
 $expected=[CaptureEngine]::Crop($doc,0,607)
 $g=[System.Drawing.Graphics]::FromImage($expected)
 try {
  $g.FillRectangle([System.Drawing.Brushes]::Black,0,0,480,24)
  $g.FillRectangle([System.Drawing.Brushes]::Gold,360,552,110,35)
  $g.DrawRectangle([System.Drawing.Pens]::White,360,552,109,34)
 }finally{$g.Dispose()}
 try {
  if($height -ne 607 -or $dest -ne 607){throw 'Altura final incorreta'}
  for($y=0;$y -lt 607;$y++){for($x=0;$x -lt 480;$x++){
   if($actual.GetPixel($x,$y).ToArgb() -ne $expected.GetPixel($x,$y).ToArgb()){throw "Elemento repetido ou emenda incorreta em $x,$y"}
  }}
  Write-Output 'PASS: montagem pixel a pixel com cabecalho e botao fixos, sem repeticoes, incluindo ultima rolagem curta.'
 }finally{$actual.Dispose();$expected.Dispose()}
}finally{if($previous){$previous.Dispose()};foreach($piece in $parts){$piece.Dispose()};$doc.Dispose()}

# A playing video changes independently of the document's vertical scroll.
$doc=New-Object System.Drawing.Bitmap(480,700)
$g=[System.Drawing.Graphics]::FromImage($doc)
$rng=New-Object System.Random(23)
try {for($y=0;$y -lt 700;$y+=5){for($x=0;$x -lt 480;$x+=8){
 $brush=New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb($rng.Next(256),$rng.Next(256),$rng.Next(256)))
 try{$g.FillRectangle($brush,$x,$y,8,5)}finally{$brush.Dispose()}
}}}finally{$g.Dispose()}
$a=[CaptureEngine]::Crop($doc,0,350);$a2=$a.Clone()
$b=[CaptureEngine]::Crop($doc,60,350);$b2=$b.Clone()
try {
 $frames=@($a,$a2,$b,$b2);$colors=@([Drawing.Color]::Red,[Drawing.Color]::Green,[Drawing.Color]::Blue,[Drawing.Color]::Yellow)
 for($i=0;$i -lt 4;$i++){
  $g=[Drawing.Graphics]::FromImage($frames[$i])
  try{$videoY=if($i -lt 2){100}else{40};$brush=New-Object Drawing.SolidBrush($colors[$i]);try{$g.FillRectangle($brush,80,$videoY,240,100)}finally{$brush.Dispose()}}finally{$g.Dispose()}
 }
 $old=[CaptureEngine]::DetectMotion($a,$a2);$new=[CaptureEngine]::DetectMotion($b,$b2)
 if($new.Fraction -le 0 -or $new.Fraction -gt .65){throw 'Mascara de video localizada incorreta'}
 if([CaptureEngine]::Match($a,$b,$old,$new) -ne 60){throw 'Video bloqueou alinhamento da pagina'}
 if([CaptureEngine]::Match($a,$a2,$old,$old) -ne 0){throw 'Video foi confundido com rolagem'}
 $seam=[CaptureEngine]::Seam($a,$b,60,2,$old,$new)
 if($seam -ge 32 -and $seam -lt 160){throw 'Emenda atravessou o video apesar de haver area livre'}
 Write-Output 'PASS: video em movimento, rolagem de 60 pixels, ausencia de rolagem e emenda fora do video.'
 $largeA=[CaptureEngine]::Crop($doc,0,350);$largeA2=$largeA.Clone()
 $largeB=[CaptureEngine]::Crop($doc,60,350);$largeB2=$largeB.Clone()
 try {
  $largeFrames=@($largeA,$largeA2,$largeB,$largeB2)
  for($i=0;$i -lt 4;$i++){
   $g=[Drawing.Graphics]::FromImage($largeFrames[$i]);$brush=New-Object Drawing.SolidBrush($colors[$i])
   try{$g.FillRectangle($brush,110,0,370,350)}finally{$g.Dispose();$brush.Dispose()}
  }
  $oldLarge=[CaptureEngine]::DetectMotion($largeA,$largeA2);$newLarge=[CaptureEngine]::DetectMotion($largeB,$largeB2)
  if($newLarge.Fraction -le .65){throw 'Teste nao reproduziu video grande'}
  if([CaptureEngine]::Match($largeA,$largeB,$oldLarge,$newLarge) -ne 60){throw 'Video grande bloqueou alinhamento dos detalhes estaticos'}
  if([CaptureEngine]::Consensus($largeA,$largeB,$false) -ne 60){throw 'Consenso nao recuperou alinhamento com video grande'}
  if([CaptureEngine]::Consensus($largeA,$largeA2,$true) -ne 0){throw 'Consenso nao confirmou retorno com video alterado'}
  if([CaptureEngine]::Match($largeA,$largeA2,$oldLarge,$oldLarge) -ne 0){throw 'Video grande foi confundido com rolagem'}
  foreach($frame in $largeFrames){$g=[Drawing.Graphics]::FromImage($frame);try{$g.Clear([Drawing.Color]::Red)}finally{$g.Dispose()}}
  $g=[Drawing.Graphics]::FromImage($largeB);try{$g.Clear([Drawing.Color]::Blue)}finally{$g.Dispose()}
  $full=[CaptureEngine]::DetectMotion($largeA,$largeB)
  if([CaptureEngine]::Match($largeA,$largeB,$full,$full) -ne -1){throw 'Aceitou alinhamento sem nenhuma evidencia estatica'}
  if([CaptureEngine]::Consensus($largeA,$largeB,$true) -ne -1){throw 'Consenso confirmou retorno sem detalhes'}
  Write-Output 'PASS: video acima de 65% com rolagem e rejeicao segura de video sem detalhes estaticos.'
 }finally{$largeA.Dispose();$largeA2.Dispose();$largeB.Dispose();$largeB2.Dispose()}
}finally{$a.Dispose();$a2.Dispose();$b.Dispose();$b2.Dispose();$doc.Dispose()}
