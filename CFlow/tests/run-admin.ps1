$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe /platform:x64 ("/out:" + (Join-Path $root 'AdminPipeTests.exe')) ("/reference:" + (Join-Path $root 'CFlow.exe')) /reference:System.Core.dll (Join-Path $PSScriptRoot 'AdminPipeTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Admin pipe test compilation failed' }
& (Join-Path $root 'AdminPipeTests.exe') | Tee-Object -FilePath (Join-Path $PSScriptRoot 'admin-results.txt')
if ($LASTEXITCODE -ne 0) { throw 'Admin pipe tests failed' }
