[CmdletBinding()]
param()

$apps = @()

# 1. HKCU Run
$hkcuRun = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
$hkcuApproved = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run"
if (Test-Path $hkcuRun) {
    $props = (Get-Item $hkcuRun).Property
    foreach ($p in $props) {
        $val = (Get-ItemProperty $hkcuRun).$p
        $enabled = $true
        if (Test-Path $hkcuApproved) {
            $bin = (Get-ItemProperty $hkcuApproved -ErrorAction SilentlyContinue).$p
            if ($bin -and $bin[0] -ne 2) {
                $enabled = $false
            }
        }
        $apps += [PSCustomObject]@{
            Name = $p
            Command = "$val"
            Hive = "HKCU"
            Enabled = $enabled
        }
    }
}

# 2. HKLM Run
$hklmRun = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run"
$hklmApproved = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run"
if (Test-Path $hklmRun) {
    $props = (Get-Item $hklmRun).Property
    foreach ($p in $props) {
        $val = (Get-ItemProperty $hklmRun).$p
        $enabled = $true
        if (Test-Path $hklmApproved) {
            $bin = (Get-ItemProperty $hklmApproved -ErrorAction SilentlyContinue).$p
            if ($bin -and $bin[0] -ne 2) {
                $enabled = $false
            }
        }
        $apps += [PSCustomObject]@{
            Name = $p
            Command = "$val"
            Hive = "HKLM"
            Enabled = $enabled
        }
    }
}

$json = $apps | ConvertTo-Json -Compress
Write-Output $json
