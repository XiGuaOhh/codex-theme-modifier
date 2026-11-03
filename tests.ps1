param([string]$Executable)
$ErrorActionPreference='Stop'
if(-not $Executable){$Executable=Join-Path $PSScriptRoot 'dist\CodexThemeModifier.exe'}
$Executable=[IO.Path]::GetFullPath($Executable)
$build=Join-Path $PSScriptRoot 'build'
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$refs=@('/r:System.dll','/r:System.Core.dll','/r:System.Drawing.dll','/r:System.Windows.Forms.dll','/r:System.Web.Extensions.dll','/r:System.Management.dll')
$resources=@()
foreach($file in Get-ChildItem -LiteralPath (Join-Path $build 'runtime') -File){$resources+=('/resource:'+$file.FullName+',payload.'+$file.Name)}
foreach($name in @('MaintenanceWorker.exe','ChatGPTTheme.exe','modifier.ico')){$resources+=('/resource:'+(Join-Path $build $name)+',payload.'+$name)}
$sources=@((Join-Path $build 'BuildInfo.cs'),(Join-Path $build 'runtime\ThemeHost.cs'))
foreach($name in @('Models.cs','Platform.cs','RuntimeClient.cs','ManagerEngine.cs','UpdateService.cs','MaintenanceWorker.cs')){$sources+=Join-Path $PSScriptRoot ('src\'+$name)}
$sources+=Join-Path $PSScriptRoot 'tests\SmokeTests.cs'
$testExe=Join-Path $build 'SmokeTests.exe'
& $csc /nologo /target:exe /main:CodexThemeModifier.TestProgram @refs @resources ('/out:'+$testExe) @sources
if($LASTEXITCODE){throw 'Test harness compilation failed.'}
$root=Join-Path $build ('test-'+[Guid]::NewGuid().ToString('N'))
& $testExe $root $Executable
if($LASTEXITCODE){throw 'Modifier tests failed.'}
