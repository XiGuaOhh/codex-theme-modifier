param([switch]$NoWait, [switch]$Original, [switch]$Quiet)
$ErrorActionPreference = 'Stop'
if (-not $Quiet) {
    try { [Console]::OutputEncoding = [Text.UTF8Encoding]::new(); $Host.UI.RawUI.WindowTitle = 'Codex 自定义背景' } catch {}
}
$taskLogPath = Join-Path $PSScriptRoot '启动日志.txt'
$taskStatusPath = Join-Path $PSScriptRoot 'theme-host-state.json'
$taskMutex = $null
$taskLockHeld = $false
$taskAppId = $null
function Write-StartupStatus([string]$Message) {
    if (-not $Quiet) { Write-Host $Message }
    try { Add-Content -LiteralPath $taskLogPath -Value ('[{0}] {1}' -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'),$Message) -Encoding UTF8 } catch {}
}
function Open-OfficialFallback {
    if ($taskAppId) { Start-Process -FilePath "$env:SystemRoot\explorer.exe" -ArgumentList ('shell:AppsFolder\' + $taskAppId) -WindowStyle Hidden | Out-Null }
}
function Get-OfficialMainProcesses([string]$Family) {
    # Same-name processes, renderers and exited process objects are not app windows.
    $taskProcessName=[CodexThemeNative]::ProcessName+'.exe'
    $taskFilter="Name = '"+$taskProcessName.Replace("'","''")+"'"
    foreach ($taskCandidate in @(Get-CimInstance Win32_Process -Filter $taskFilter -ErrorAction SilentlyContinue)) {
        if ($taskCandidate.CommandLine -like '*--type=*') { continue }
        if (-not [CodexThemeNative]::IsOfficialProcess($taskCandidate.ProcessId,$Family)) { continue }
        try {
            $taskProcess = Get-Process -Id $taskCandidate.ProcessId -ErrorAction Stop
            if (-not $taskProcess.HasExited -and $taskProcess.Threads.Count -gt 0) { $taskProcess }
        } catch {}
    }
}
try {
    $taskMutex = [Threading.Mutex]::new($false,'Local\CodexBackgroundLauncher')
    try { $taskLockHeld = $taskMutex.WaitOne(0) } catch [Threading.AbandonedMutexException] { $taskLockHeld = $true }
    if (-not $taskLockHeld -and -not $Original) {
        Write-StartupStatus '启动器正在打开官方窗口。'
        exit 0
    }
    $taskPackage = Get-AppxPackage -Name OpenAI.Codex | Sort-Object Version -Descending | Select-Object -First 1
    if (-not $taskPackage) { throw '未找到官方安装版 Codex。请先通过原有渠道安装官方程序。' }
    $taskApplication = ($taskPackage | Get-AppxPackageManifest).Package.Applications.Application | Where-Object { $_.Id -eq 'App' } | Select-Object -First 1
    if (-not $taskApplication) { throw '未能找到官方应用的启动入口。' }
    $taskAppId = '{0}!{1}' -f $taskPackage.PackageFamilyName,$taskApplication.Id
    if ($Original -or (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'disabled.flag')) -or ((Test-Path -LiteralPath (Join-Path $PSScriptRoot 'modifier-state.json')) -and -not(Test-Path -LiteralPath (Join-Path $PSScriptRoot 'enabled.flag')))) { Open-OfficialFallback; exit 0 }
    try { Add-Type -Path (Join-Path $PSScriptRoot 'ThemeHost.cs') -ReferencedAssemblies 'System.Web.Extensions.dll' }
    catch { Open-OfficialFallback; Write-StartupStatus '主题加载器暂不可用，已打开官方程序。'; exit 0 }
    [CodexThemeNative]::ConfigureExecutable($taskApplication.Executable)
    $taskBrowserProfile = [CodexThemeNative]::DefaultBrowserProfile($taskPackage.PackageFamilyName)
    $taskAttachExisting = [CodexThemeMonitor]::ActivePort($taskBrowserProfile,$taskPackage.PackageFamilyName) -gt 0
    $taskCurrentMain = @(Get-OfficialMainProcesses $taskPackage.PackageFamilyName)
    if ($taskCurrentMain.Count -gt 0 -and -not $taskAttachExisting) {
        # An existing app without a local theme channel remains fully usable.
        # Never wait on all ChatGPT processes or force the current work to close.
        [void][CodexThemeNative]::Launch($taskAppId,'')
        Write-StartupStatus '已打开当前官方窗口。当前实例由原版入口启动；下次完全退出后从开始菜单的主题入口打开，即会加载背景。'
        exit 0
    }
    $taskHostActive = $false
    try {
        $taskHostMutex = [Threading.Mutex]::OpenExisting('Local\CodexBackgroundOfficialHost')
        try {
            $taskHostLock = $false
            try { $taskHostLock = $taskHostMutex.WaitOne(0) } catch [Threading.AbandonedMutexException] { $taskHostLock = $true }
            if ($taskHostLock) { $taskHostMutex.ReleaseMutex() } else { $taskHostActive = $true }
        } finally { $taskHostMutex.Dispose() }
    } catch [Threading.WaitHandleCannotBeOpenedException] {} catch [UnauthorizedAccessException] {}
    if ($taskHostActive) {
        $taskActivationArguments = if ($taskCurrentMain.Count -eq 0) { [CodexThemeNative]::ThemeArguments } else { '' }
        [void][CodexThemeNative]::Launch($taskAppId,$taskActivationArguments)
        Write-StartupStatus '已打开官方窗口，主题加载器正在处理窗口。'
        exit 0
    }
    if (-not (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'ThemeHost.ps1')) -or -not (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'custom-background.js'))) {
        Open-OfficialFallback; Write-StartupStatus '主题文件不完整，已打开官方程序。'; exit 0
    }
    if ($taskAttachExisting) { Write-StartupStatus '正在为当前窗口恢复主题…' }
    else { Write-StartupStatus ('正在打开官方安装版 {0} 并加载主题…' -f $taskPackage.Version) }
    if (Test-Path -LiteralPath $taskStatusPath) { Remove-Item -LiteralPath $taskStatusPath }
    $taskHostPath = Join-Path $PSScriptRoot 'ThemeHost.ps1'
    $taskHostArguments = @('-NoLogo','-NoProfile','-ExecutionPolicy','Bypass','-File',('"{0}"' -f $taskHostPath))
    if ($taskAttachExisting) { $taskHostArguments += '-AttachOnly' }
    $taskStartedHost = Start-Process -FilePath "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" -ArgumentList $taskHostArguments -WindowStyle Hidden -PassThru
    $taskDeadline = (Get-Date).AddSeconds(27)
    do {
        Start-Sleep -Milliseconds 250
        $taskState = $null
        if (Test-Path -LiteralPath $taskStatusPath) { try { $taskState = Get-Content -LiteralPath $taskStatusPath -Raw -Encoding UTF8 | ConvertFrom-Json } catch {} }
        if ($taskState -and $taskState.status -eq 'ready') { Write-StartupStatus '官方程序已打开，背景主题已加载。'; exit 0 }
        if ($taskStartedHost.HasExited) {
            if (-not [CodexThemeNative]::HasOfficialProcess($taskPackage.PackageFamilyName)) { Open-OfficialFallback }
            Write-StartupStatus '官方程序可以正常使用；本次主题未加载，请查看启动日志。'; exit 0
        }
    } while ((Get-Date) -lt $taskDeadline)
    Write-StartupStatus '官方程序已打开；主题正在等待界面完成初始化。'
    exit 0
} catch {
    Write-StartupStatus ('启动提示：' + $_.Exception.Message)
    try { Open-OfficialFallback } catch {}
    exit 1
} finally {
    if ($taskLockHeld -and $taskMutex) { $taskMutex.ReleaseMutex() }
    if ($taskMutex) { $taskMutex.Dispose() }
}
