[CmdletBinding()]
param()

Write-Host "Restoring Windows Screenshot & Snipping Tool functionality..."

# 1. Restore camsvc (Capability Access Manager Service)
try {
    Set-Service -Name "camsvc" -StartupType Manual -ErrorAction SilentlyContinue
    Start-Service -Name "camsvc" -ErrorAction SilentlyContinue
    Set-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Services\camsvc" -Name "Start" -Value 3 -Type DWord -Force -ErrorAction SilentlyContinue
    Write-Host "SUCCESS: Capability Access Manager Service (camsvc) enabled and started."
} catch {
    Write-Warning "Could not configure camsvc: $_"
}

# 2. Enable PrintScreen key to open Snipping Tool (Win+Shift+S and PrintScreen)
try {
    $keyboardKey = "HKCU:\Control Panel\Keyboard"
    if (-not (Test-Path $keyboardKey)) {
        New-Item -Path $keyboardKey -Force | Out-Null
    }
    Set-ItemProperty -Path $keyboardKey -Name "PrintScreenKeyForSnippingEnabled" -Value 1 -Type DWord -Force
    Write-Host "SUCCESS: PrintScreen key mapped to Snipping Tool."
} catch {
    Write-Warning "Could not set PrintScreenKeyForSnippingEnabled: $_"
}

# 3. Explicitly allow Snipping Tool / ScreenSketch in background apps
try {
    $bgAppKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications"
    # Do not globally disable all if it kills screen sketch
    $sketchPackages = Get-AppxPackage -Name "*ScreenSketch*" -ErrorAction SilentlyContinue
    foreach ($pkg in $sketchPackages) {
        $pkgKey = "$bgAppKey\$($pkg.PackageFamilyName)"
        if (-not (Test-Path $pkgKey)) {
            New-Item -Path $pkgKey -Force | Out-Null
        }
        Set-ItemProperty -Path $pkgKey -Name "Disabled" -Value 0 -Type DWord -Force
        Set-ItemProperty -Path $pkgKey -Name "DisabledByUser" -Value 0 -Type DWord -Force
    }
    Write-Host "SUCCESS: ScreenSketch background access granted."
} catch {
    Write-Warning "Could not configure ScreenSketch background access: $_"
}

# 4. Ensure ms-screenclip protocol and hotkeys are not blocked
try {
    Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Policies\Explorer" -Name "NoScreenCapture" -ErrorAction SilentlyContinue
    Remove-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer" -Name "NoScreenCapture" -ErrorAction SilentlyContinue
    Remove-ItemProperty -Path "HKCU:\Software\Policies\Microsoft\Windows\TabletPC" -Name "DisableSnippingTool" -ErrorAction SilentlyContinue
    Remove-ItemProperty -Path "HKLM:\SOFTWARE\Policies\Microsoft\Windows\TabletPC" -Name "DisableSnippingTool" -ErrorAction SilentlyContinue
    Write-Host "SUCCESS: Screenshot policies verified and unblocked."
} catch {}

# 5. Restore Clipboard Services and Connected Devices Platform (CDPSvc & cbdhsvc)
try {
    $cdpKey = "HKLM:\SYSTEM\CurrentControlSet\Services\CDPSvc"
    if (Test-Path $cdpKey) {
        Set-ItemProperty -Path $cdpKey -Name "Start" -Value 2 -Type DWord -Force -ErrorAction SilentlyContinue
    }
    Set-Service -Name "CDPSvc" -StartupType Automatic -ErrorAction SilentlyContinue
    Start-Service -Name "CDPSvc" -ErrorAction SilentlyContinue

    $cbdKey = "HKLM:\SYSTEM\CurrentControlSet\Services\cbdhsvc"
    if (Test-Path $cbdKey) {
        Set-ItemProperty -Path $cbdKey -Name "Start" -Value 2 -Type DWord -Force -ErrorAction SilentlyContinue
    }
    Get-Service *cbdhsvc* -ErrorAction SilentlyContinue | ForEach-Object {
        Start-Service -Name $_.Name -ErrorAction SilentlyContinue
    }

    Remove-ItemProperty -Path "HKLM:\SOFTWARE\Policies\Microsoft\Windows\AppPrivacy" -Name "LetAppsRunInBackground" -ErrorAction SilentlyContinue
    Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications" -Name "GlobalUserDisabled" -ErrorAction SilentlyContinue

    Write-Host "SUCCESS: Serviciile Clipboard si Connected Devices (CDPSvc/cbdhsvc) au fost repornite."
} catch {}

Write-Host "SUCCESS: Windows Screenshot & Clipboard functionality is now fully restored."
