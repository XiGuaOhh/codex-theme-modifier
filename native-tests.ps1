param()
$ErrorActionPreference='Stop'
$build=Join-Path $PSScriptRoot 'build'
if(-not(Test-Path -LiteralPath (Join-Path $build 'runtime\ThemeHost.cs'))){throw 'Run build.ps1 first.'}
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$refs=@('/r:System.dll','/r:System.Core.dll','/r:System.Drawing.dll','/r:System.Windows.Forms.dll','/r:System.Web.Extensions.dll','/r:System.Management.dll')
$sources=@((Join-Path $build 'BuildInfo.cs'),(Join-Path $build 'runtime\ThemeHost.cs'))
foreach($name in @('Models.cs','Platform.cs')){$sources+=Join-Path $PSScriptRoot ('src\'+$name)}
$sources+=Join-Path $PSScriptRoot 'tests\NativeSmoke.cs'
$exe=Join-Path $build 'NativeSmoke.exe'
& $csc /nologo /nowarn:0649 /target:exe /main:CodexThemeModifier.NativeSmoke @refs ('/resource:'+(Join-Path $build 'runtime\PackageInfo.ps1')+',payload.PackageInfo.ps1') ('/out:'+$exe) @sources
if($LASTEXITCODE){throw 'Native test compilation failed.'}
& $exe (Join-Path $build ('native-test-'+[Guid]::NewGuid().ToString('N'))) (Join-Path $build 'runtime')
if($LASTEXITCODE){throw 'Native integration tests failed.'}
