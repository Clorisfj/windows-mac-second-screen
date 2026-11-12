$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$refs = @('/reference:System.dll','/reference:System.Core.dll','/reference:System.Drawing.dll','/reference:System.Windows.Forms.dll','/reference:System.ServiceProcess.dll','/reference:System.Security.dll','/reference:System.Xml.dll','/reference:System.Xml.Linq.dll','/reference:System.Web.Extensions.dll')
Push-Location $root
try {
    Add-Type -AssemblyName System.Drawing
    $image = [Drawing.Image]::FromFile((Join-Path $root 'assets\CFlow.png'))
    try {
        $edge = [Math]::Min($image.Width, $image.Height)
        $crop = [Drawing.Rectangle]::new([int](($image.Width - $edge) / 2), [int](($image.Height - $edge) / 2), $edge, $edge)
        $square = $image.Clone($crop, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $square.Save((Join-Path $root 'CFlow.png'), [Drawing.Imaging.ImageFormat]::Png)
            $frames = foreach ($size in @(16,20,24,32,40,48,64,128,256)) {
                $bitmap = [Drawing.Bitmap]::new($size, $size)
                $graphics = [Drawing.Graphics]::FromImage($bitmap)
                $png = [IO.MemoryStream]::new()
                try {
                    $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                    $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
                    $graphics.DrawImage($square, [Drawing.Rectangle]::new(0, 0, $size, $size))
                    $bitmap.Save($png, [Drawing.Imaging.ImageFormat]::Png)
                    @{ Size = $size; Data = $png.ToArray() }
                } finally { $png.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
            }
            $stream = [IO.File]::Create((Join-Path $root 'CFlow.ico'))
            $writer = [IO.BinaryWriter]::new($stream)
            try {
                # ICO directory followed by PNG frames, supported by Windows 11.
                $writer.Write([uint16]0)
                $writer.Write([uint16]1)
                $writer.Write([uint16]$frames.Count)
                $offset = 6 + 16 * $frames.Count
                foreach ($frame in $frames) {
                    $dimension = if ($frame.Size -eq 256) { 0 } else { $frame.Size }
                    $writer.Write([byte]$dimension)
                    $writer.Write([byte]$dimension)
                    $writer.Write([byte]0)
                    $writer.Write([byte]0)
                    $writer.Write([uint16]1)
                    $writer.Write([uint16]32)
                    $writer.Write([uint32]$frame.Data.Length)
                    $writer.Write([uint32]$offset)
                    $offset += $frame.Data.Length
                }
                foreach ($frame in $frames) { $writer.Write([byte[]]$frame.Data) }
            } finally { $writer.Dispose() }
        } finally { $square.Dispose() }
    } finally { $image.Dispose() }
    & $compiler /nologo /optimize+ /target:winexe /platform:x64 /out:CFlow.exe /main:CFlow.Desktop /win32manifest:Windows\ui.manifest /win32icon:CFlow.ico @refs Windows\Engine.cs Windows\DisplayApi.cs Windows\Control.cs Windows\Desktop.cs
    if ($LASTEXITCODE -ne 0) { throw 'CFlow 构建失败；请先退出正在运行的 CFlow。' }
    & $compiler /nologo /optimize+ /target:winexe /platform:x64 /out:CFlow.Helper.exe /main:CFlow.HelperMain /win32manifest:Windows\helper.manifest @refs Windows\Engine.cs Windows\DisplayApi.cs Windows\Control.cs Windows\HelperMain.cs
    if ($LASTEXITCODE -ne 0) { throw '管理员助手构建失败。' }
} finally { Pop-Location }
