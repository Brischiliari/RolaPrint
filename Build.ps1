$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
Push-Location $PSScriptRoot
try {
    & $compiler /nologo /optimize+ /target:winexe /out:RolaPrint.exe /win32icon:RolaPrint.ico /resource:RolaPrint.ico,RolaPrint.ico /reference:System.Drawing.dll /reference:System.Windows.Forms.dll CaptureEngine.cs NativeApp.cs
    if ($LASTEXITCODE -ne 0) { throw 'Falha na compilação do RolaPrint.' }
    Write-Host 'RolaPrint.exe compilado. O aplicativo não depende do PowerShell para executar.'
} finally { Pop-Location }
