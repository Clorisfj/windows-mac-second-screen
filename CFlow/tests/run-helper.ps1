$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$probe = Join-Path $PSScriptRoot 'private\helper-probe'
New-Item -ItemType Directory -Path $probe -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'CFlow.Helper.exe') -Destination $probe -Force
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$refs = @('/reference:System.dll','/reference:System.Core.dll','/reference:System.Drawing.dll','/reference:System.Windows.Forms.dll','/reference:System.ServiceProcess.dll','/reference:System.Security.dll','/reference:System.Xml.dll','/reference:System.Xml.Linq.dll','/reference:System.Web.Extensions.dll')
& $compiler /nologo /target:exe /platform:x64 ("/out:" + (Join-Path $probe 'CFlow.exe')) /main:HelperLaunchProbe @refs (Join-Path $root 'Windows\Control.cs') (Join-Path $root 'Windows\Engine.cs') (Join-Path $root 'Windows\DisplayApi.cs') (Join-Path $PSScriptRoot 'HelperLaunchProbe.cs')
if ($LASTEXITCODE -ne 0) { throw 'Helper launch probe compilation failed' }
& (Join-Path $probe 'CFlow.exe') | Tee-Object -FilePath (Join-Path $PSScriptRoot 'helper-results.txt')
if ($LASTEXITCODE -ne 0) { throw 'Helper launch probe failed' }
