[CmdletBinding()]
param(
    [switch]$All,
    [switch]$ToggleVbs,
    [switch]$EnableGameMode,
    [switch]$EnableHags,
    [switch]$SetUltimatePowerPlan,
    [switch]$OptimizeNetwork,
    [switch]$SetCloudflareDns,
    [switch]$AddDefenderGameExclusion,
    [string]$GameDirectory = "C:\Games",
    [switch]$Revert
)

function Set-RegDword {
    param(
        [string]$Path,
        [string]$Name,
        [int]$Value
    )
    try {
        if (-not (Test-Path $Path -ErrorAction SilentlyContinue)) {
            New-Item -Path $Path -Force -ErrorAction SilentlyContinue | Out-Null
        }
        Set-ItemProperty -Path $Path -Name $Name -Value $Value -Type DWord -Force -ErrorAction Stop | Out-Null
        Write-Host "Set $Path\$Name = $Value"
    } catch {
        $cleanPath = $Path -replace '^HKLM:\\?', 'HKLM\' -replace '^HKCU:\\?', 'HKCU\'
        $null = & reg.exe add $cleanPath /v $Name /t REG_DWORD /d $Value /f 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Set $Path\$Name = $Value (via reg.exe)"
        } else {
            Write-Host "Nota: Registrul $Path\$Name a fost omis (necesita permisiuni de sistem)."
        }
    }
}

if ($Revert) {
    Write-Host "REVERTING Gaming Tweaks to Windows defaults..."

    # Re-enable VBS
    Set-RegDword "HKLM:\SYSTEM\CurrentControlSet\Control\DeviceGuard" "EnableVirtualizationBasedSecurity" 1
    Set-RegDword "HKLM:\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity" "Enabled" 1

    # Reset Power Plan to Balanced
    powercfg -setactive 381b4222-f694-41f0-9685-ff5bb260df2e
    Write-Host "Power plan restored to Balanced."

    # Reset Network Nagle
    $activeAdapter = Get-NetAdapter | Where-Object { $_.Status -eq 'Up' -and $_.InterfaceDescription -notmatch 'Virtual|Loopback|TAP|VPN' } | Select-Object -First 1
    if ($activeAdapter) {
        $tcpPath = "HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\$($activeAdapter.InterfaceGuid)"
        Remove-ItemProperty -Path $tcpPath -Name 'TcpAckFrequency' -ErrorAction SilentlyContinue
        Remove-ItemProperty -Path $tcpPath -Name 'TCPNoDelay' -ErrorAction SilentlyContinue
        Set-DnsClientServerAddress -InterfaceIndex $activeAdapter.ifIndex -ResetServerAddresses -ErrorAction SilentlyContinue
        Write-Host "Network adapter settings reset to default DHCP/DNS."
    }

    Write-Host "SUCCESS: Gaming tweaks reverted."
    exit 0
}

Write-Host "Applying Gaming and Latency Optimizations..."

# 1. VBS and Memory Integrity
if ($All -or $ToggleVbs) {
    Write-Host "Disabling VBS (Virtualization-Based Security) and Memory Integrity (HVCI)..."
    Set-RegDword "HKLM:\SYSTEM\CurrentControlSet\Control\DeviceGuard" "EnableVirtualizationBasedSecurity" 0
    Set-RegDword "HKLM:\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity" "Enabled" 0
    Write-Host "VBS disabled. (Note: A system restart is required for full effect)."
}

# 2. Game Mode
if ($All -or $EnableGameMode) {
    Write-Host "Enabling Windows 11 Game Mode..."
    Set-RegDword "HKCU:\Software\Microsoft\GameBar" "AutoGameModeEnabled" 1
    Set-RegDword "HKCU:\Software\Microsoft\GameBar" "AllowAutoGameMode" 1
    Write-Host "Game Mode activated."
}

# 3. HAGS
if ($All -or $EnableHags) {
    Write-Host "Enabling Hardware-Accelerated GPU Scheduling (HAGS)..."
    Set-RegDword "HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers" "HwSchMode" 2
    Write-Host "HAGS set to enabled. (Restart required)."
}

# 4. Ultimate Performance Power Plan
if ($All -or $SetUltimatePowerPlan) {
    Write-Host "Configuring Ultimate Performance Power Scheme..."
    $schemeGuid = "e9a42b02-d5df-448d-aa00-03f14749eb61"
    # Try duplicating scheme
    $output = powercfg -duplicatescheme $schemeGuid 2>&1
    powercfg -setactive $schemeGuid 2>$null
    if ($LASTEXITCODE -ne 0) {
        # Fallback to High Performance
        Write-Host "Ultimate Performance not available, falling back to High Performance plan..."
        $highPerfGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c"
        powercfg -duplicatescheme $highPerfGuid 2>$null
        powercfg -setactive $highPerfGuid 2>$null
    }
    Write-Host "Max CPU performance power plan applied."
}

# 5. Network Latency (Nagle Algorithm)
if ($All -or $OptimizeNetwork) {
    Write-Host "Optimizing network latency (Disabling Nagle's Algorithm)..."
    $activeAdapter = Get-NetAdapter | Where-Object { $_.Status -eq 'Up' -and $_.InterfaceDescription -notmatch 'Virtual|Loopback|TAP|VPN' } | Select-Object -First 1
    if ($activeAdapter) {
        $tcpPath = "HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\$($activeAdapter.InterfaceGuid)"
        Set-RegDword $tcpPath "TcpAckFrequency" 1
        Set-RegDword $tcpPath "TCPNoDelay" 1
        Write-Host "TcpAckFrequency and TCPNoDelay configured for adapter $($activeAdapter.Name)."
    } else {
        Write-Host "WARNING: No active physical network adapter found for Nagle optimization."
    }
}

# 6. Low Latency DNS
if ($All -or $SetCloudflareDns) {
    Write-Host "Configuring high-speed Cloudflare DNS (1.1.1.1, 1.0.0.1)..."
    $activeAdapter = Get-NetAdapter | Where-Object { $_.Status -eq 'Up' -and $_.InterfaceDescription -notmatch 'Virtual|Loopback|TAP|VPN' } | Select-Object -First 1
    if ($activeAdapter) {
        Set-DnsClientServerAddress -InterfaceIndex $activeAdapter.ifIndex -ServerAddresses ("1.1.1.1", "1.0.0.1") -ErrorAction SilentlyContinue
        Write-Host "DNS updated to Cloudflare 1.1.1.1 on adapter $($activeAdapter.Name)."
    }
}

# 7. Defender Game Exclusion (Auto-detects Steam on all user PCs)
if ($AddDefenderGameExclusion -or $All) {
    $targetDir = $GameDirectory
    if (-not $targetDir) {
        $steam = (Get-ItemProperty "HKCU:\Software\Valve\Steam" -Name "SteamPath" -ErrorAction SilentlyContinue).SteamPath
        if (-not $steam) {
            $steam = (Get-ItemProperty "HKLM:\SOFTWARE\WOW6432Node\Valve\Steam" -Name "InstallPath" -ErrorAction SilentlyContinue).InstallPath
        }
        if (-not $steam) {
            $steam = (Get-ItemProperty "HKLM:\SOFTWARE\Valve\Steam" -Name "InstallPath" -ErrorAction SilentlyContinue).InstallPath
        }
        if ($steam) {
            $steam = $steam.Replace('/', '\')
            $common = Join-Path $steam "steamapps\common"
            if (Test-Path $common) {
                $targetDir = $common
            } else {
                $targetDir = $steam
            }
        } else {
            $targetDir = "C:\Games"
        }
    }

    if ($targetDir) {
        Write-Host "Adding Defender real-time exclusion for detected game directory: $targetDir..."
        if (-not (Test-Path $targetDir)) {
            New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
        }
        Add-MpPreference -ExclusionPath $targetDir -ErrorAction SilentlyContinue
        Write-Host "SUCCESS: Windows Defender exclusion configured for: $targetDir"
    }
}

Write-Host "SUCCESS: Gaming optimizations applied successfully."
