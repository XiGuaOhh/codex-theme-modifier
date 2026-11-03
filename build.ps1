param([string]$Version='2.1.1',[string]$Output)
$ErrorActionPreference='Stop'
if($Version -notmatch '^\d+\.\d+\.\d+$'){throw 'Use a stable semantic version, for example 2.0.0.'}
if(-not $Output){$Output=Join-Path $PSScriptRoot 'dist'}
$Output=[IO.Path]::GetFullPath($Output)
$build=Join-Path $PSScriptRoot 'build'
New-Item -ItemType Directory -Path $Output -Force | Out-Null
New-Item -ItemType Directory -Path $build -Force | Out-Null
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if(-not(Test-Path -LiteralPath $csc)){throw 'Windows .NET Framework compiler not found.'}
Add-Type -AssemblyName System.Drawing
$iconPath=Join-Path $build 'modifier.ico'
$image=[Drawing.Bitmap]::new(64,64)
$g=[Drawing.Graphics]::FromImage($image)
$g.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([Drawing.Color]::FromArgb(17,25,43))
$brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(146,168,255));$g.FillEllipse($brush,10,10,43,43);$brush.Dispose()
$brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(17,25,43));$g.FillEllipse($brush,32,36,22,22);$brush.Dispose()
foreach($item in @(@(23,17,100,225,186),@(37,18,244,195,105),@(15,29,189,145,245))){$brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb($item[2],$item[3],$item[4]));$g.FillEllipse($brush,$item[0],$item[1],8,8);$brush.Dispose()}
$g.Dispose()
$png=[IO.MemoryStream]::new();$image.Save($png,[Drawing.Imaging.ImageFormat]::Png);$image.Dispose();$data=$png.ToArray();$png.Dispose()
$stream=[IO.File]::Create($iconPath);$writer=[IO.BinaryWriter]::new($stream)
try{$writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]1);$writer.Write([byte]64);$writer.Write([byte]64);$writer.Write([byte]0);$writer.Write([byte]0);$writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$data.Length);$writer.Write([uint32]22);$writer.Write($data)}finally{$writer.Dispose()}
$buildInfo=Join-Path $build 'BuildInfo.cs'
[IO.File]::WriteAllText($buildInfo,[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src\BuildInfo.cs')).Replace('2.0.0',$Version),[Text.UTF8Encoding]::new($true))
$runtime=Join-Path $build 'runtime';New-Item -ItemType Directory -Path $runtime -Force | Out-Null
foreach($name in @('Start.ps1','ThemeHost.ps1','ThemeHost.cs','PackageInfo.ps1','custom-background.js')){
    $text=[IO.File]::ReadAllText((Join-Path $PSScriptRoot ('runtime\'+$name)))
    if($name -eq 'custom-background.js'){$text=$text.Replace('2.0.0',$Version)}
    [IO.File]::WriteAllText((Join-Path $runtime $name),$text,[Text.UTF8Encoding]::new($true))
}
$refs=@('/r:System.dll','/r:System.Core.dll','/r:System.Drawing.dll','/r:System.Windows.Forms.dll','/r:System.Web.Extensions.dll','/r:System.Management.dll')
$common=@('/nologo','/target:winexe','/optimize+','/platform:anycpu',('/win32icon:'+$iconPath),('/win32manifest:'+(Join-Path $PSScriptRoot 'app.manifest')))
$worker=Join-Path $build 'MaintenanceWorker.exe'
& $csc @common @refs ('/out:'+$worker) $buildInfo (Join-Path $PSScriptRoot 'src\Models.cs') (Join-Path $PSScriptRoot 'src\UpdateService.cs') (Join-Path $PSScriptRoot 'src\MaintenanceWorker.cs')
if($LASTEXITCODE){throw 'Maintenance worker compilation failed.'}
$launcher=Join-Path $build 'ChatGPTTheme.exe'
& $csc @common @refs ('/out:'+$launcher) (Join-Path $PSScriptRoot 'src\Launcher.cs')
if($LASTEXITCODE){throw 'Theme launcher compilation failed.'}
$resources=@()
foreach($file in Get-ChildItem -LiteralPath $runtime -File){$resources+=('/resource:'+$file.FullName+',payload.'+$file.Name)}
$resources+=('/resource:'+$worker+',payload.MaintenanceWorker.exe')
$resources+=('/resource:'+$launcher+',payload.ChatGPTTheme.exe')
$resources+=('/resource:'+$iconPath+',payload.modifier.ico')
$resources+=('/resource:'+(Join-Path $PSScriptRoot 'assets\fonts\GroveRoundF-Regular.ttf')+',ui.rounded.ttf')
$resources+=('/resource:'+(Join-Path $PSScriptRoot 'assets\fonts\NOTICE.md')+',ui.font-notice')
$resources+=('/resource:'+(Join-Path $PSScriptRoot 'assets\fonts\OFL.txt')+',ui.font-license')
$sources=@($buildInfo)
foreach($name in @('Models.cs','Platform.cs','RuntimeClient.cs','ManagerEngine.cs','UpdateService.cs','UiControls.cs','ManagerForm.cs','Program.cs')){$sources+=Join-Path $PSScriptRoot ('src\'+$name)}
$sources+=Join-Path $runtime 'ThemeHost.cs'
$exe=Join-Path $Output 'CodexThemeModifier.exe'
& $csc @common @refs @resources ('/out:'+$exe) @sources
if($LASTEXITCODE){throw 'Modifier compilation failed.'}
$hash=(Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $Output 'CodexThemeModifier.exe.sha256'),($hash+'  CodexThemeModifier.exe'+[Environment]::NewLine),[Text.UTF8Encoding]::new($false))
[pscustomobject]@{Version=$Version;Executable=$exe;Bytes=(Get-Item -LiteralPath $exe).Length;Sha256=$hash} | ConvertTo-Json
