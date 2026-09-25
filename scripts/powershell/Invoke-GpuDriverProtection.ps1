[CmdletBinding()]
param(
    [switch]$Enable,
    [switch]$Revert
)

function Set-RegDword {
    param([string]$Path, [string]$Name, [int]$Value)
    try {
        if (-not (Test-Path $Path -ErrorAction SilentlyContinue)) {
            New-Item -Path $Path -Force -ErrorAction SilentlyContinue | Out-Null
        }
        Set-ItemProperty -Path $Path -Name $Name -Value $Value -Type DWord -Force -ErrorAction Stop | Out-Null
        Write-Host "Set $Path\$Name = $Value"
    } catch {
        $cleanPath = $Path -replace '^HKLM:\\?', 'HKLM\' -replace '^HKCU:\\?', 'HKCU\'
        $null = & reg.exe add $cleanPath /v $Name /t REG_DWORD /d $Value /f 2>&1
        Write-Host "Set $Path\$Name = $Value (via reg.exe)"
    }
}

$wuPolicy = "HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate"
$driverSearch = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\DriverSearching"

if ($Revert) {
    Write-Host "Revert: Permite descarcarea driverelor prin Windows Update..."
    Set-RegDword $wuPolicy "ExcludeWUDriversInQualityUpdate" 0
    Set-RegDword $driverSearch "SearchOrderConfig" 1
    Write-Host "SUCCESS: Windows Update poate include din nou actualizari de drivere."
    exit 0
}

Write-Host "Blocare suprascriere drivere GPU prin Windows Update..."
Set-RegDword $wuPolicy "ExcludeWUDriversInQualityUpdate" 1
Set-RegDword $driverSearch "SearchOrderConfig" 0

Write-Host "SUCCESS: Driverele video (NVIDIA/AMD) nu vor mai fi suprascrise de Windows Update."
