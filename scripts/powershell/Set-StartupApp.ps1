[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string]$Name,
    [Parameter(Mandatory=$true)]
    [string]$Hive,
    [Parameter(Mandatory=$true)]
    [string]$Enabled
)

$isEnabled = ($Enabled -in @('1', 'true', 'True', '$true', 'True', 'true'))

$approvedKey = if ($Hive -eq "HKLM") {
    "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run"
} else {
    "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run"
}

try {
    if (-not (Test-Path $approvedKey)) {
        New-Item -Path $approvedKey -Force -ErrorAction SilentlyContinue | Out-Null
    }

    if ($isEnabled) {
        $val = [byte[]](2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)
        Set-ItemProperty -Path $approvedKey -Name $Name -Value $val -Type Binary -Force -ErrorAction SilentlyContinue
        Write-Host "SUCCESS: $Name a fost activat la pornire."
    } else {
        $val = [byte[]](3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)
        Set-ItemProperty -Path $approvedKey -Name $Name -Value $val -Type Binary -Force -ErrorAction SilentlyContinue
        Write-Host "SUCCESS: $Name a fost dezactivat la pornire."
    }
} catch {
    Write-Host "Eroare la modificarea starii programului de startup: $_"
}
