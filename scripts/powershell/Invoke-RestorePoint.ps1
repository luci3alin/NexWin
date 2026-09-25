[CmdletBinding()]
param(
    [string]$Description = "NexWin Optimization Point"
)

Write-Host "Creating System Restore Point: $Description..."

try {
    # Ensure System Restore service is enabled
    Set-Service -Name srservice -StartupType Automatic -ErrorAction SilentlyContinue
    Start-Service -Name srservice -ErrorAction SilentlyContinue

    # Enable restore on System Drive
    $sysDrive = $env:SystemDrive
    Enable-ComputerRestore -Drive "$sysDrive\" -ErrorAction SilentlyContinue

    # Set frequency registry key to allow multiple restore points
    $srKey = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore"
    if (Test-Path $srKey) {
        Set-ItemProperty -Path $srKey -Name "SystemRestorePointCreationFrequency" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue
    }

    Checkpoint-Computer -Description $Description -RestorePointType "MODIFY_SETTINGS" -ErrorAction Stop
    Write-Host "SUCCESS: System Restore Point created successfully."
    exit 0
}
catch {
    Write-Warning "Failed to create Restore Point via Checkpoint-Computer: $_"
    # Fallback via WMI
    try {
        $wmi = [wmiclass]"\\localhost\root\default:systemrestore"
        $result = $wmi.CreateRestorePoint($Description, 0, 100)
        if ($result.ReturnValue -eq 0) {
            Write-Host "SUCCESS: Restore Point created via WMI fallback."
            exit 0
        } else {
            Write-Host "WARNING: System Restore is disabled by Group Policy or disk space. Continuing safely."
            exit 0
        }
    }
    catch {
        Write-Host "WARNING: Could not create restore point (likely disabled in Windows settings). Proceeding."
        exit 0
    }
}
