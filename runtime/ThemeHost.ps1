param([string]$BrowserProfile, [string]$StatusFile, [switch]$AttachOnly)
$ErrorActionPreference = 'Stop'
$taskLogPath = Join-Path $PSScriptRoot '启动日志.txt'
$taskMutexName = 'Local\CodexBackgroundOfficialHost'
if ($BrowserProfile) {
    $taskHasher = [Security.Cryptography.SHA256]::Create()
    try { $taskMutexName += '-' + [BitConverter]::ToString($taskHasher.ComputeHash([Text.Encoding]::UTF8.GetBytes([IO.Path]::GetFullPath($BrowserProfile)))).Replace('-','').Substring(0,16) } finally { $taskHasher.Dispose() }
}
$taskMutex = [Threading.Mutex]::new($false, $taskMutexName)
$taskLocked = $false
$taskLaunched = $false
$taskAppId = $null
try {
    try { $taskLocked = $taskMutex.WaitOne(0) } catch [Threading.AbandonedMutexException] { $taskLocked = $true }
    if (-not $taskLocked) { exit 0 }
    $taskPackage = Get-AppxPackage -Name OpenAI.Codex | Sort-Object Version -Descending | Select-Object -First 1
    if (-not $taskPackage) { throw '未找到官方安装版 Codex。' }
    $taskApplication = ($taskPackage | Get-AppxPackageManifest).Package.Applications.Application | Where-Object { $_.Id -eq 'App' } | Select-Object -First 1
    if (-not $taskApplication) { throw '官方程序的启动方式发生了变化。' }
    $taskAppId = '{0}!{1}' -f $taskPackage.PackageFamilyName, $taskApplication.Id
    Add-Type -Path (Join-Path $PSScriptRoot 'ThemeHost.cs') -ReferencedAssemblies 'System.Web.Extensions.dll'
    [CodexThemeNative]::ConfigureExecutable($taskApplication.Executable)
    if(Test-Path -LiteralPath (Join-Path $PSScriptRoot 'disabled.flag')) { exit 0 }
    if (-not $BrowserProfile) { $BrowserProfile = [CodexThemeNative]::DefaultBrowserProfile($taskPackage.PackageFamilyName) }
    if (-not $StatusFile) { $StatusFile = Join-Path $PSScriptRoot 'theme-host-state.json' }
    if (-not $AttachOnly) {
        $taskArguments = [CodexThemeNative]::ThemeArguments
        # An explicit profile is used by isolated integration tests only.
        if ($PSBoundParameters.ContainsKey('BrowserProfile')) { $taskArguments += ' --user-data-dir="{0}"' -f $BrowserProfile }
        $taskProcessId = [CodexThemeNative]::Launch($taskAppId, $taskArguments)
        $taskLaunched = $true
        if (-not [CodexThemeNative]::IsOfficialProcess($taskProcessId, $taskPackage.PackageFamilyName)) { throw '无法确认程序以官方安装身份运行。' }
    }
    [CodexThemeMonitor]::Run($BrowserProfile, $taskPackage.PackageFamilyName, (Join-Path $PSScriptRoot 'custom-background.js'), $StatusFile)
} catch {
    Add-Content -LiteralPath $taskLogPath -Value ('[{0}] 主题未能加载：{1}。官方程序可以继续使用。' -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $_.Exception.Message) -Encoding UTF8
    if (-not $taskLaunched -and $taskAppId) {
        try { Start-Process -FilePath "$env:SystemRoot\explorer.exe" -ArgumentList ('shell:AppsFolder\' + $taskAppId) -WindowStyle Hidden | Out-Null } catch {}
    }
    exit 1
} finally {
    if ($taskLocked) { $taskMutex.ReleaseMutex() }
    $taskMutex.Dispose()
}
