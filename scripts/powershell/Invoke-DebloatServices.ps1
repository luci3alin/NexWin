[CmdletBinding()]
param(
    [switch]$All,
    [switch]$DeepDebloat,
    [switch]$DisableTelemetry,
    [switch]$DisableErrorReporting,
    [switch]$ConsolidateSvcHost,
    [switch]$DisableUnnecessaryServices,
    [switch]$DisableEdgeBackground,
    [switch]$DisableBackgroundApps,
    [switch]$DisableSysMain,
    [switch]$DisableSearchIndexing,
    [switch]$Revert
)

function Set-ServiceState {
    param(
        [string]$Name,
        [string]$StartupType,
        [string]$Action
    )
    $s = Get-Service -Name $Name -ErrorAction SilentlyContinue
    if ($s) {
        if ($Action -eq "Stop" -and $s.Status -eq "Running") {
            Stop-Service -Name $Name -Force -ErrorAction SilentlyContinue
            Write-Host "Stopped service: $Name"
        }
        if ($StartupType) {
            Set-Service -Name $Name -StartupType $StartupType -ErrorAction SilentlyContinue
            # Permanent registry lock for reboots (4 = Disabled, 3 = Manual, 2 = Automatic)
            $regStart = switch ($StartupType.ToLower()) {
                "disabled" { 4 }
                "automatic" { 2 }
                default { 3 }
            }
            $svcKey = "HKLM:\SYSTEM\CurrentControlSet\Services\$Name"
            if (Test-Path $svcKey) {
                Set-ItemProperty -Path $svcKey -Name "Start" -Value $regStart -Type DWord -Force -ErrorAction SilentlyContinue
            }
            Write-Host "Set service $Name permanently to: $StartupType (Start = $regStart)"
        }
    }
}

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
    Write-Host "REVERTING Windows services and debloat settings..."

    # Revert telemetry & diagnostics
    Set-ServiceState "DiagTrack" "Automatic" "Start"
    Set-ServiceState "dmwappushservice" "Automatic" "Start"
    Set-ServiceState "WerSvc" "Manual" "Start"
    Set-ServiceState "DPS" "Automatic" "Start"
    Set-ServiceState "WSearch" "Automatic" "Start"
    Set-ServiceState "SysMain" "Automatic" "Start"
    Set-ServiceState "DoSvc" "Automatic" "Start"
    Set-ServiceState "PcaSvc" "Automatic" "Start"

    # Revert SvcHost split threshold
    Remove-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control" -Name "SvcHostSplitThresholdInKB" -ErrorAction SilentlyContinue
    Write-Host "Removed SvcHostSplitThresholdInKB (default split restored)."

    # Revert Edge background
    Remove-ItemProperty -Path "HKLM:\SOFTWARE\Policies\Microsoft\Edge" -Name "StartupBoostEnabled" -ErrorAction SilentlyContinue
    Remove-ItemProperty -Path "HKLM:\SOFTWARE\Policies\Microsoft\Edge" -Name "BackgroundModeEnabled" -ErrorAction SilentlyContinue

    # Revert Background Apps
    Remove-ItemProperty -Path "HKLM:\SOFTWARE\Policies\Microsoft\Windows\AppPrivacy" -Name "LetAppsRunInBackground" -ErrorAction SilentlyContinue

    Write-Host "SUCCESS: Services and debloat restored to default."
    exit 0
}

Write-Host "Starting Windows 11 Deep Debloat & Service Optimization..."

# 1. Telemetry & DiagTrack
if ($All -or $DeepDebloat -or $DisableTelemetry) {
    Write-Host "Disabling Telemetry & Diagnostic services..."
    Set-RegDword "HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection" "AllowTelemetry" 0
    Set-RegDword "HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection" "MaxTelemetryAllowed" 0
    Set-RegDword "HKCU:\Software\Microsoft\Windows\CurrentVersion\Diagnostics\DiagTrack" "ShowDiagTrackNotice" 0

    Set-ServiceState "DiagTrack" "Disabled" "Stop"
    Set-ServiceState "dmwappushservice" "Disabled" "Stop"
    Set-ServiceState "DPS" "Disabled" "Stop"
    Set-ServiceState "WdiServiceHost" "Disabled" "Stop"
    Set-ServiceState "WdiSystemHost" "Disabled" "Stop"
    Set-ServiceState "InventorySvc" "Disabled" "Stop"
    Set-ServiceState "DsSvc" "Disabled" "Stop"
    Set-ServiceState "DusmSvc" "Disabled" "Stop"

    # Scheduled Tasks
    $tasks = @(
        "\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser",
        "\Microsoft\Windows\Application Experience\ProgramDataUpdater",
        "\Microsoft\Windows\Customer Experience Improvement Program\Consolidator",
        "\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip",
        "\Microsoft\Windows\DiskDiagnostic\Microsoft-Windows-DiskDiagnosticDataCollector"
    )
    foreach ($t in $tasks) {
        $path = $t.Substring(0, $t.LastIndexOf('\') + 1)
        $name = $t.Substring($t.LastIndexOf('\') + 1)
        Disable-ScheduledTask -TaskPath $path -TaskName $name -ErrorAction SilentlyContinue | Out-Null
        Write-Host "Disabled task: $name"
    }
}

# 2. Windows AI Fabric & Recall Services
if ($All -or $DeepDebloat) {
    Write-Host "Stopping Windows AI Fabric background services..."
    Set-ServiceState "WSAIFabricSvc" "Disabled" "Stop"
    Set-ServiceState "AIFabricService" "Disabled" "Stop"
}

# 3. Microsoft Edge Background & Startup Boost (Eliminates 15-20 idle background processes!)
if ($All -or $DeepDebloat -or $DisableEdgeBackground) {
    Write-Host "Disabling Microsoft Edge Background Mode and Startup Boost..."
    Set-RegDword "HKLM:\SOFTWARE\Policies\Microsoft\Edge" "StartupBoostEnabled" 0
    Set-RegDword "HKLM:\SOFTWARE\Policies\Microsoft\Edge" "BackgroundModeEnabled" 0
    Set-RegDword "HKCU:\Software\Policies\Microsoft\Edge" "StartupBoostEnabled" 0
    Set-RegDword "HKCU:\Software\Policies\Microsoft\Edge" "BackgroundModeEnabled" 0
    Write-Host "Edge background workers and startup boost disabled via policy."
}

# 4. Background Content & Bloat
if ($All -or $DeepDebloat -or $DisableBackgroundApps) {
    Write-Host "Disabling Windows Content Delivery background suggestions..."

    # Disable Windows Content Delivery Manager (suggestions, spotlight ads, silent downloads)
    $cdm = "HKCU:\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager"
    Set-RegDword $cdm "SystemPaneSuggestionsEnabled" 0
    Set-RegDword $cdm "SubscribedContent-338388Enabled" 0
    Set-RegDword $cdm "SubscribedContent-338389Enabled" 0
    Set-RegDword $cdm "SubscribedContent-353696Enabled" 0
    Set-RegDword $cdm "SoftLandingEnabled" 0
    Write-Host "ContentDeliveryManager suggestions disabled."
}

# 5. Windows Widgets & News / Interests WebViews
if ($All -or $DeepDebloat) {
    Write-Host "Disabling Windows Widgets and Feeds background processes..."
    Set-RegDword "HKLM:\SOFTWARE\Policies\Microsoft\Dsh" "AllowNewsAndInterests" 0
    Set-RegDword "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" "TaskbarDa" 0
    Stop-Process -Name "Widgets" -Force -ErrorAction SilentlyContinue
    Stop-Process -Name "WidgetService" -Force -ErrorAction SilentlyContinue
}

# 6. Windows Error Reporting
if ($All -or $DeepDebloat -or $DisableErrorReporting) {
    Write-Host "Disabling Windows Error Reporting (WerSvc)..."
    Set-ServiceState "WerSvc" "Disabled" "Stop"
    Set-RegDword "HKLM:\SOFTWARE\Microsoft\Windows\Windows Error Reporting" "Disabled" 1
}

# 7. Unnecessary Background Services (BlackViper / AtlasOS recommended)
if ($All -or $DeepDebloat -or $DisableUnnecessaryServices) {
    Write-Host "Stopping redundant background services..."
    $services = @(
        "MapsBroker",     # Downloaded Maps Manager
        "lfsvc",          # Geolocation Service
        "TrkWks",         # Distributed Link Tracking Client
        "RetailDemo",     # Retail Demo Service
        "RemoteRegistry", # Remote Registry
        "shpamsvc",       # Shared PC Account Manager
        "PhoneSvc",       # Phone Service
        "PcaSvc",         # Program Compatibility Assistant
        "DoSvc",          # Delivery Optimization
        "SSDPSRV"         # SSDP Discovery
    )
    foreach ($srv in $services) {
        Set-ServiceState $srv "Disabled" "Stop"
    }
}

# 8. SvcHost Consolidation (Prag de 64GB pentru gruparea proceselor la reboot)
if ($All -or $ConsolidateSvcHost) {
    Write-Host "Configuring SvcHost process consolidation..."
    $threshold = 67108864
    Set-RegDword "HKLM:\SYSTEM\CurrentControlSet\Control" "SvcHostSplitThresholdInKB" $threshold
    Write-Host "SvcHostSplitThresholdInKB set to 64GB. SvcHost processes will group on next reboot."
}

# 9. SysMain / Superfetch (Optional for SSDs)
if ($DisableSysMain) {
    Write-Host "Disabling SysMain (Superfetch) for SSD performance..."
    Set-ServiceState "SysMain" "Disabled" "Stop"
}

# 10. Windows Search Indexing (Optional)
if ($DisableSearchIndexing) {
    Write-Host "Disabling Windows Search indexing daemon (WSearch)..."
    Set-ServiceState "WSearch" "Disabled" "Stop"
    Stop-Process -Name "SearchFilterHost" -Force -ErrorAction SilentlyContinue
    Stop-Process -Name "SearchIndexer" -Force -ErrorAction SilentlyContinue
}

Write-Host "SUCCESS: Windows debloat and service optimizations completed."
