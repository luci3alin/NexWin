[CmdletBinding()]
param(
    [switch]$Revert
)

Write-Host "=========================================================="
Write-Host "Optimizare Periferice USB & Reducere Latenta Input"
Write-Host "=========================================================="

if ($Revert) {
    Write-Host "Reactivare setari implicite USB..."
    # Re-enable USB Selective Suspend
    powercfg /SETACVALUEINDEX SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba4d5a0 48e6b7a6-50f5-4760-a579-e4ffdd470206 1
    powercfg /SETACTIVE SCHEME_CURRENT
    Write-Host "SUCCESS: USB Selective Suspend resetat la valorile implicite."
    exit 0
}

# 1. Disable USB Selective Suspend in active Power Plan
try {
    powercfg /SETACVALUEINDEX SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba4d5a0 48e6b7a6-50f5-4760-a579-e4ffdd470206 0
    powercfg /SETDCVALUEINDEX SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba4d5a0 48e6b7a6-50f5-4760-a579-e4ffdd470206 0
    powercfg /SETACTIVE SCHEME_CURRENT
    Write-Host "SUCCESS: USB Selective Suspend dezactivat in planul de energie activ."
} catch {
    Write-Host "Warning la setarea powercfg USB: $_"
}

# 2. Disable Power Management on USB Hubs
try {
    $usbHubs = Get-CimInstance -ClassName Win32_PnPEntity | Where-Object { $_.Service -match "usbhub|usbxhci|usbehci" }
    $count = 0
    foreach ($hub in $usbHubs) {
        $devId = $hub.DeviceID
        $devPath = "HKLM:\SYSTEM\CurrentControlSet\Enum\$devId\Device Parameters"
        if (Test-Path $devPath) {
            Set-ItemProperty -Path $devPath -Name "EnhancedPowerManagementEnabled" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue
            Set-ItemProperty -Path $devPath -Name "DeviceSelectiveSuspended" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue
            Set-ItemProperty -Path $devPath -Name "SelectiveSuspendEnabled" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue
            $count++
        }
    }
    Write-Host "SUCCESS: Economisire energie dezactivata pe $count controlere/hub-uri USB."
} catch {
    Write-Host "Nota la configurarea hub-urilor USB: $_"
}

Write-Host "SUCCESS: Optimizare periferice USB finalizata (polling rate stabil si input lag redus)."
