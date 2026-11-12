$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$private = Join-Path $PSScriptRoot 'private'
New-Item -ItemType Directory -Path $private -Force | Out-Null
$openssl = 'C:\Program Files\Git\usr\bin\openssl.exe'
if (-not (Test-Path -LiteralPath $openssl)) { throw 'Local TLS fixture needs the existing Git OpenSSL tool.' }
$key = Join-Path $private 'test-key.pem'
$cert = Join-Path $private 'test-cert.pem'
$pfx = Join-Path $private 'test.pfx'
& $openssl req -x509 -newkey rsa:2048 -nodes -days 1 -subj '/CN=CFlow-Test' -keyout $key -out $cert 2>$null
if ($LASTEXITCODE -ne 0) { throw 'Test certificate creation failed' }
& $openssl pkcs12 -export -inkey $key -in $cert -out $pfx -passout 'pass:cflow-tests-only'
if ($LASTEXITCODE -ne 0) { throw 'Test fixture creation failed' }
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe /platform:x64 ("/out:" + (Join-Path $root 'ProtocolTests.exe')) ("/reference:" + (Join-Path $root 'CFlow.exe')) /reference:System.Core.dll /reference:System.Security.dll /reference:System.Xml.dll /reference:System.Xml.Linq.dll (Join-Path $PSScriptRoot 'ProtocolTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed' }
& (Join-Path $root 'ProtocolTests.exe') $pfx | Tee-Object -FilePath (Join-Path $PSScriptRoot 'results.txt')
if ($LASTEXITCODE -ne 0) { throw 'Protocol tests failed' }
