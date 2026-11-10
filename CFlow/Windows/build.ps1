$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$refs = @('/reference:System.dll','/reference:System.Core.dll','/reference:System.Drawing.dll','/reference:System.Windows.Forms.dll','/reference:System.ServiceProcess.dll','/reference:System.Security.dll','/reference:System.Xml.dll','/reference:System.Xml.Linq.dll','/reference:System.Web.Extensions.dll')
Push-Location $root
try {
    Copy-Item -LiteralPath assets\CFlow.png -Destination CFlow.png -Force
    Add-Type -AssemblyName System.Drawing
    $image = [Drawing.Image]::FromFile((Join-Path $root 'assets\CFlow.png'))
    $bitmap = [Drawing.Bitmap]::new($image, [Drawing.Size]::new(48,48))
    $icon = [Drawing.Icon]::FromHandle($bitmap.GetHicon())
    $stream = [IO.File]::Create((Join-Path $root 'CFlow.ico'))
    try { $icon.Save($stream) } finally { $stream.Dispose(); $icon.Dispose(); $bitmap.Dispose(); $image.Dispose() }
    & $compiler /nologo /optimize+ /target:winexe /platform:x64 /out:CFlow.exe /main:CFlow.Desktop /win32manifest:Windows\ui.manifest /win32icon:CFlow.ico @refs Windows\Engine.cs Windows\DisplayApi.cs Windows\Control.cs Windows\Desktop.cs
    if ($LASTEXITCODE -ne 0) { throw 'CFlow 构建失败；请先退出正在运行的 CFlow。' }
    & $compiler /nologo /optimize+ /target:winexe /platform:x64 /out:CFlow.Helper.exe /main:CFlow.HelperMain /win32manifest:Windows\helper.manifest @refs Windows\Engine.cs Windows\DisplayApi.cs Windows\Control.cs Windows\HelperMain.cs
    if ($LASTEXITCODE -ne 0) { throw '管理员助手构建失败。' }
    Copy-Item -LiteralPath assets\CFlow.png -Destination CFlow.png -Force
} finally { Pop-Location }
