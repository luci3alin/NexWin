[CmdletBinding()]
param(
    [switch]$Optimize,
    [switch]$RestoreFonts,
    [switch]$DisableAnimations,
    [switch]$EnableAnimations,
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
    } catch {
        $cleanPath = $Path -replace '^HKLM:\\?', 'HKLM\' -replace '^HKCU:\\?', 'HKCU\'
        $null = & reg.exe add $cleanPath /v $Name /t REG_DWORD /d $Value /f 2>&1
    }
}

function Restore-ClearTypeFonts {
    $desktopKey = "HKCU:\Control Panel\Desktop"
    Set-ItemProperty -Path $desktopKey -Name "FontSmoothing" -Value "2" -Type String -Force -ErrorAction SilentlyContinue
    Set-ItemProperty -Path $desktopKey -Name "FontSmoothingType" -Value 2 -Type DWord -Force -ErrorAction SilentlyContinue
    Set-ItemProperty -Path $desktopKey -Name "FontSmoothingGamma" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue
    Set-ItemProperty -Path $desktopKey -Name "FontSmoothingOrientation" -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue
    
    # Preserve 8-byte binary mask with font smoothing bit 1 set (0x9E)
    $defaultMask = [byte[]](0x9E, 0x3E, 0x07, 0x80, 0x12, 0x00, 0x00, 0x00)
    Set-ItemProperty -Path $desktopKey -Name "UserPreferencesMask" -Value $defaultMask -Type Binary -Force -ErrorAction SilentlyContinue

    Write-Host "Netezire fonturi (ClearType) restaurata complet la calitatea nativa."
}

$desktopKey = "HKCU:\Control Panel\Desktop"
$windowMetrics = "HKCU:\Control Panel\Desktop\WindowMetrics"
$explorerAdvanced = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced"

if ($RestoreFonts) {
    Restore-ClearTypeFonts
    exit 0
}

if ($Revert -or $EnableAnimations) {
    Write-Host "Restoring default Windows visual effects & animations..."
    Restore-ClearTypeFonts
    Set-RegDword $explorerAdvanced "TaskbarAnimations" 1
    Set-RegDword $windowMetrics "MinAnimate" 1
    Write-Host "Efecte vizuale si animatii resetate la valorile implicite."
    exit 0
}

# Optimize or DisableAnimations
Write-Host "Aplicare optimizari efecte vizuale (viteza maxima + fonturi clare)..."

# Ensure fonts are ALWAYS kept crisp and smoothed
Restore-ClearTypeFonts

# Disable heavy window animations
Set-RegDword $windowMetrics "MinAnimate" 0
Set-RegDword $explorerAdvanced "TaskbarAnimations" 0
Set-RegDword $explorerAdvanced "IconsOnly" 0

$visualKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects"
Set-RegDword $visualKey "VisualFXSetting" 3

Write-Host "SUCCESS: Efecte vizuale optimizate cu pastrarea integrala a claritatii fonturilor."

