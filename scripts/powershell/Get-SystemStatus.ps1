[CmdletBinding()]
param()

$ErrorActionPreference = 'SilentlyContinue'

# 1. Active Process Count
$processes = Get-Process
$procCount = $processes.Count

# 2. Memory Stats
$os = Get-CimInstance -ClassName Win32_OperatingSystem
$totalRam = [math]::Round($os.TotalVisibleMemorySize / 1MB, 2)
$freeRam = [math]::Round($os.FreePhysicalMemory / 1MB, 2)
$usedRam = [math]::Round($totalRam - $freeRam, 2)
$ramPercent = [math]::Round(($usedRam / $totalRam) * 100, 1)

# 3. CPU Load
$cpu = (Get-CimInstance -ClassName Win32_Processor | Measure-Object -Property LoadPercentage -Average).Average
if ($null -eq $cpu) { $cpu = 0 }
$cpu = [math]::Round($cpu, 1)

# 4. OS Version
$osName = $os.Caption
$osBuild = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion' -Name CurrentBuild -ErrorAction SilentlyContinue).CurrentBuild

# 5. VBS and Memory Integrity (HVCI)
$vbsVal = (Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\DeviceGuard' -Name 'EnableVirtualizationBasedSecurity' -ErrorAction SilentlyContinue).EnableVirtualizationBasedSecurity
$vbsEnabled = ($vbsVal -eq 1)

$hvciVal = (Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity' -Name 'Enabled' -ErrorAction SilentlyContinue).Enabled
$hvciEnabled = ($hvciVal -eq 1)

# 6. Game Mode
$gameModeVal = (Get-ItemProperty 'HKCU:\Software\Microsoft\GameBar' -Name 'AutoGameModeEnabled' -ErrorAction SilentlyContinue).AutoGameModeEnabled
$gameModeEnabled = ($gameModeVal -ne 0)

# 7. HAGS (Hardware Accelerated GPU Scheduling)
$hagsVal = (Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers' -Name 'HwSchMode' -ErrorAction SilentlyContinue).HwSchMode
$hagsEnabled = ($hagsVal -eq 2)

# 8. Power Plan
$activePlanOutput = (powercfg /getactivescheme) 2>$null
$powerPlanName = 'Balanced'
if ($activePlanOutput -match '\((.*?)\)') {
    $powerPlanName = $matches[1]
}

# 9. AI and Copilot Status
$copilotVal = (Get-ItemProperty 'HKCU:\Software\Policies\Microsoft\Windows\WindowsCopilot' -Name 'TurnOffWindowsCopilot' -ErrorAction SilentlyContinue).TurnOffWindowsCopilot
if ($null -eq $copilotVal) {
    $copilotVal = (Get-ItemProperty 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot' -Name 'TurnOffWindowsCopilot' -ErrorAction SilentlyContinue).TurnOffWindowsCopilot
}
$copilotDisabled = ($copilotVal -eq 1)

$recallVal = (Get-ItemProperty 'HKCU:\Software\Policies\Microsoft\Windows\WindowsAI' -Name 'DisableAIDataAnalysis' -ErrorAction SilentlyContinue).DisableAIDataAnalysis
if ($null -eq $recallVal) {
    $recallVal = (Get-ItemProperty 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsAI' -Name 'DisableAIDataAnalysis' -ErrorAction SilentlyContinue).DisableAIDataAnalysis
}
$recallDisabled = ($recallVal -eq 1)

# 10. Telemetry Status
$telemetryVal = (Get-ItemProperty 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection' -Name 'AllowTelemetry' -ErrorAction SilentlyContinue).AllowTelemetry
$telemetryDisabled = ($telemetryVal -eq 0)

# 11. DiagTrack Service Status
$diagTrackService = Get-Service -Name 'DiagTrack' -ErrorAction SilentlyContinue
$diagTrackRunning = ($diagTrackService -and $diagTrackService.Status -eq 'Running')

# 12. Active Network Adapter and Nagle status
$activeAdapter = Get-NetAdapter | Where-Object { $_.Status -eq 'Up' -and $_.InterfaceDescription -notmatch 'Virtual|Loopback|TAP|VPN' } | Select-Object -First 1
$nagleDisabled = $false
$dnsList = @()
if ($activeAdapter) {
    $interfaceGuid = $activeAdapter.InterfaceGuid
    $tcpPath = "HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\$interfaceGuid"
    $tcpAck = (Get-ItemProperty -Path $tcpPath -Name 'TcpAckFrequency' -ErrorAction SilentlyContinue).TcpAckFrequency
    $tcpNoDelay = (Get-ItemProperty -Path $tcpPath -Name 'TCPNoDelay' -ErrorAction SilentlyContinue).TCPNoDelay
    if ($tcpAck -eq 1 -and $tcpNoDelay -eq 1) {
        $nagleDisabled = $true
    }
    $dnsAddresses = (Get-DnsClientServerAddress -InterfaceIndex $activeAdapter.ifIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue).ServerAddresses
    if ($dnsAddresses) {
        $dnsList = $dnsAddresses
    }
}

# 13. Visual Effects Best Performance check
$visualVal = (Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects' -Name 'VisualFXSetting' -ErrorAction SilentlyContinue).VisualFXSetting
$visualOptimized = ($visualVal -eq 2 -or $visualVal -eq 3)

# 14. Screenshot / camsvc Status
$camsvc = Get-Service -Name 'camsvc' -ErrorAction SilentlyContinue
$screenshotEnabled = ($camsvc -and $camsvc.Status -eq 'Running')

# 15. Detect Steam Installation Path
$steamPath = (Get-ItemProperty "HKCU:\Software\Valve\Steam" -Name "SteamPath" -ErrorAction SilentlyContinue).SteamPath
if (-not $steamPath) {
    $steamPath = (Get-ItemProperty "HKLM:\SOFTWARE\WOW6432Node\Valve\Steam" -Name "InstallPath" -ErrorAction SilentlyContinue).InstallPath
}
if (-not $steamPath) {
    $steamPath = (Get-ItemProperty "HKLM:\SOFTWARE\Valve\Steam" -Name "InstallPath" -ErrorAction SilentlyContinue).InstallPath
}
if ($steamPath) {
    $steamPath = $steamPath.Replace('/', '\')
    $commonPath = Join-Path $steamPath "steamapps\common"
    if (Test-Path $commonPath) {
        $steamPath = $commonPath
    }
} else {
    $steamPath = "C:\Program Files (x86)\Steam\steamapps\common"
}

# 16. GPU Driver Protection Status
$gpuDriverExclude = (Get-ItemProperty 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate' -Name 'ExcludeWUDriversInQualityUpdate' -ErrorAction SilentlyContinue).ExcludeWUDriversInQualityUpdate
$gpuProtectionEnabled = ($gpuDriverExclude -eq 1)

# 17. USB Selective Suspend Status (0 = Disabled, which is optimized)
$usbSuspendVal = (powercfg /QUERY SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba4d5a0 48e6b7a6-50f5-4760-a579-e4ffdd470206) 2>$null
$usbOptimized = $false
if ($usbSuspendVal -match 'Current AC Power Setting Index: 0x00000000') {
    $usbOptimized = $true
}

$status = [PSCustomObject]@{
    processCount           = $procCount
    totalMemoryGB          = $totalRam
    freeMemoryGB           = $freeRam
    usedMemoryGB           = $usedRam
    memoryPercent          = $ramPercent
    cpuLoad                = $cpu
    osName                 = $osName
    osBuild                = $osBuild
    vbsEnabled             = $vbsEnabled
    memoryIntegrityEnabled = $hvciEnabled
    gameModeEnabled        = $gameModeEnabled
    hagsEnabled            = $hagsEnabled
    powerPlan              = $powerPlanName
    copilotDisabled        = $copilotDisabled
    recallDisabled         = $recallDisabled
    telemetryDisabled      = $telemetryDisabled
    diagTrackRunning       = $diagTrackRunning
    nagleDisabled          = $nagleDisabled
    dnsServers             = ($dnsList -join ', ')
    visualOptimized        = $visualOptimized
    screenshotEnabled      = $screenshotEnabled
    steamPath              = $steamPath
    usbOptimized           = $usbOptimized
    gpuProtectionEnabled   = $gpuProtectionEnabled
    timestamp              = (Get-Date).ToString('s')
}

$status | ConvertTo-Json -Compress
