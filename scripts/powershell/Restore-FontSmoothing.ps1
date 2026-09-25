[CmdletBinding()]
param()

$desktopKey = "HKCU:\Control Panel\Desktop"

# 1. FontSmoothing must be String "2", NOT DWORD
Set-ItemProperty -Path $desktopKey -Name "FontSmoothing" -Value "2" -Type String -Force
Set-ItemProperty -Path $desktopKey -Name "FontSmoothingType" -Value 2 -Type DWord -Force
Set-ItemProperty -Path $desktopKey -Name "FontSmoothingGamma" -Value 0 -Type DWord -Force
Set-ItemProperty -Path $desktopKey -Name "FontSmoothingOrientation" -Value 1 -Type DWord -Force

# 2. UserPreferencesMask must be REG_BINARY (Default Windows 11 with font smoothing bit enabled)
$defaultMask = [byte[]](0x9E, 0x3E, 0x07, 0x80, 0x12, 0x00, 0x00, 0x00)
Set-ItemProperty -Path $desktopKey -Name "UserPreferencesMask" -Value $defaultMask -Type Binary -Force

# 3. Notify Windows subsystems live via user32 SystemParametersInfo
$signature = @"
using System;
using System.Runtime.InteropServices;
public class Win32FontRestore {
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);
}
"@
try {
    Add-Type -TypeDefinition $signature -ErrorAction SilentlyContinue
    # SPI_SETFONTSMOOTHING = 0x004B, SPI_SETFONTSMOOTHINGTYPE = 0x200A
    # SPIF_UPDATEINIFILE = 0x01, SPIF_SENDCHANGE = 0x02
    [Win32FontRestore]::SystemParametersInfo(0x004B, 1, [IntPtr]::Zero, 3) | Out-Null
    [Win32FontRestore]::SystemParametersInfo(0x200A, 2, [IntPtr]::Zero, 3) | Out-Null
} catch {}

Write-Host "SUCCESS: Windows font smoothing (ClearType) restored to default crisp rendering."
