$ErrorActionPreference='Stop'
[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
$p=Get-AppxPackage -Name OpenAI.Codex | Sort-Object Version -Descending | Select-Object -First 1
if(-not $p){[pscustomobject]@{Found=$false}|ConvertTo-Json -Compress;exit 0}
$apps=@(($p|Get-AppxPackageManifest).Package.Applications.Application)
$a=$apps | Where-Object {$_.Id -eq 'App'} | Select-Object -First 1
if(-not $a){throw 'Unsupported official application manifest.'}
$exe=[IO.Path]::GetFullPath((Join-Path $p.InstallLocation ($a.Executable -replace '/','\')))
$prefix=$p.InstallLocation.TrimEnd('\')+'\'
if(-not $exe.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)-or -not(Test-Path -LiteralPath $exe)){throw 'Invalid registered executable.'}
[pscustomobject]@{Found=$true;PackageFamily=$p.PackageFamilyName;FullName=$p.PackageFullName;Root=$p.InstallLocation;Executable=$exe;AppId=($p.PackageFamilyName+'!'+$a.Id);Version=$p.Version.ToString();DisplayName='Codex'} | ConvertTo-Json -Compress
