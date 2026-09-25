[CmdletBinding()]
param(
    [switch]$All,
    [switch]$DisableCopilot,
    [switch]$DisableRecall,
    [switch]$DisableEdgeAI,
    [switch]$DisablePaintAI,
    [switch]$DisableTasks,
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

function Remove-RegValue {
    param(
        [string]$Path,
        [string]$Name
    )
    try {
        if (Test-Path $Path -ErrorAction SilentlyContinue) {
            Remove-ItemProperty -Path $Path -Name $Name -Force -ErrorAction SilentlyContinue | Out-Null
            Write-Host "Removed $Path\$Name"
        }
    } catch {
        $cleanPath = $Path -replace '^HKLM:\\?', 'HKLM\' -replace '^HKCU:\\?', 'HKCU\'
        $null = & reg.exe delete $cleanPath /v $Name /f 2>&1
    }
}

if ($Revert) {
    Write-Host "REVERTING AI settings to Windows defaults..."

    # Revert Copilot
    Remove-RegValue "HKCU:\Software\Policies\Microsoft\Windows\WindowsCopilot" "TurnOffWindowsCopilot"
    Remove-RegValue "HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot" "TurnOffWindowsCopilot"
    Set-RegDword "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" "ShowCopilotButton" 1

    # Revert Recall
    Remove-RegValue "HKCU:\Software\Policies\Microsoft\Windows\WindowsAI" "DisableAIDataAnalysis"
    Remove-RegValue "HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsAI" "DisableAIDataAnalysis"

    # Revert Edge AI
    Remove-RegValue "HKLM:\SOFTWARE\Policies\Microsoft\Edge" "CopilotPageContext"
    Remove-RegValue "HKLM:\SOFTWARE\Policies\Microsoft\Edge" "HubsSidebarEnabled"

    # Revert Input Harvesting
    Remove-RegValue "HKCU:\Software\Microsoft\InputPersonalization" "RestrictImplicitInkCollection"
    Remove-RegValue "HKCU:\Software\Microsoft\InputPersonalization" "RestrictImplicitTextCollection"

    Write-Host "SUCCESS: AI settings reverted."
    exit 0
}

Write-Host "Starting AI removal and debloat..."

if ($All -or $DisableCopilot) {
    Write-Host "Disabling Windows Copilot and Shell Integration..."
    Set-RegDword "HKCU:\Software\Policies\Microsoft\Windows\WindowsCopilot" "TurnOffWindowsCopilot" 1
    Set-RegDword "HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot" "TurnOffWindowsCopilot" 1
    Set-RegDword "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" "ShowCopilotButton" 0
    Set-RegDword "HKLM:\SOFTWARE\Policies\Microsoft\Windows\Windows Search" "AllowCortana" 0
    Set-RegDword "HKCU:\Software\Microsoft\Windows\CurrentVersion\Search" "SearchboxTaskbarMode" 1
}

if ($All -or $DisableRecall) {
    Write-Host "Disabling Windows Recall and AI Data Analysis..."
    Set-RegDword "HKCU:\Software\Policies\Microsoft\Windows\WindowsAI" "DisableAIDataAnalysis" 1
    Set-RegDword "HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsAI" "DisableAIDataAnalysis" 1
    Set-RegDword "HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsAI" "AllowRecallEnablement" 0
    Set-RegDword "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" "RecallEnabled" 0
}

if ($All -or $DisableEdgeAI) {
    Write-Host "Disabling Microsoft Edge Copilot and AI Hubs..."
    Set-RegDword "HKLM:\SOFTWARE\Policies\Microsoft\Edge" "CopilotPageContext" 0
    Set-RegDword "HKLM:\SOFTWARE\Policies\Microsoft\Edge" "HubsSidebarEnabled" 0
    Set-RegDword "HKLM:\SOFTWARE\Policies\Microsoft\Edge" "StandaloneHubsSidebarEnabled" 0
}

if ($All -or $DisablePaintAI) {
    Write-Host "Disabling AI Experiments and Features in Paint and Photos..."
    Set-RegDword "HKCU:\Software\Microsoft\Windows\CurrentVersion\Paint" "DisableAIExperiments" 1
    Set-RegDword "HKLM:\SOFTWARE\Policies\Microsoft\Windows\AppPrivacy" "LetAppsAccessFaceTracker" 2
}

if ($All) {
    Write-Host "Disabling Typing Telemetry and Input Insights..."
    Set-RegDword "HKCU:\Software\Microsoft\InputPersonalization" "RestrictImplicitInkCollection" 1
    Set-RegDword "HKCU:\Software\Microsoft\InputPersonalization" "RestrictImplicitTextCollection" 1
    Set-RegDword "HKCU:\Software\Microsoft\InputPersonalization\TrainedDataStore" "HarvestContacts" 0

    # Stop and disable AI Fabric Service if present
    $fabric = Get-Service -Name "AIFabricService" -ErrorAction SilentlyContinue
    if ($fabric) {
        Stop-Service -Name "AIFabricService" -Force -ErrorAction SilentlyContinue
        Set-Service -Name "AIFabricService" -StartupType Disabled -ErrorAction SilentlyContinue
        Write-Host "Disabled AIFabricService"
    }
}

if ($All -or $DisableTasks) {
    Write-Host "Disabling Windows AI and telemetry scheduled tasks..."
    $tasksToDisable = @(
        "\Microsoft\Windows\WindowsAI\SnapshotTask",
        "\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser",
        "\Microsoft\Windows\Application Experience\ProgramDataUpdater",
        "\Microsoft\Windows\Customer Experience Improvement Program\Consolidator",
        "\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip",
        "\Microsoft\Windows\Autochk\Proxy"
    )

    foreach ($taskPath in $tasksToDisable) {
        try {
            Disable-ScheduledTask -TaskPath ($taskPath.Substring(0, $taskPath.LastIndexOf('\') + 1)) -TaskName ($taskPath.Substring($taskPath.LastIndexOf('\') + 1)) -ErrorAction SilentlyContinue | Out-Null
            Write-Host "Disabled task: $taskPath"
        }
        catch {}
    }
}

Write-Host "SUCCESS: Windows AI components successfully disabled."
