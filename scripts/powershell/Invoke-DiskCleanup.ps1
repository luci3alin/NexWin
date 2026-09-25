[CmdletBinding()]
param(
    [switch]$All,
    [switch]$Temp,
    [switch]$WindowsUpdate,
    [switch]$RecycleBin,
    [switch]$CrashDumps
)

$ErrorActionPreference = "SilentlyContinue"

function Get-DirSize {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return 0 }
    $m = Get-ChildItem -LiteralPath $Path -Recurse -File -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum
    if ($m.Sum) { return [long]$m.Sum }
    return 0
}

function Format-Size {
    param([long]$Bytes)
    if ($Bytes -ge 1TB) {
        return "$([math]::Round($Bytes / 1TB, 2)) TB"
    } elseif ($Bytes -ge 1GB) {
        return "$([math]::Round($Bytes / 1GB, 2)) GB"
    } elseif ($Bytes -ge 1MB) {
        return "$([math]::Round($Bytes / 1MB, 2)) MB"
    } elseif ($Bytes -ge 1KB) {
        return "$([math]::Round($Bytes / 1KB, 2)) KB"
    } else {
        return "$Bytes B"
    }
}

Write-Output "[*] Pornire curatare inteligenta spatiu disc..."

$bytesBefore = 0
$cleanedCount = 0

# 1. User Temp
if ($All -or $Temp) {
    Write-Output "[+] Curatare fisiere temporare utilizator ($env:TEMP)..."
    $userTemp = $env:TEMP
    if (Test-Path -LiteralPath $userTemp) {
        Get-ChildItem -LiteralPath $userTemp -Force -ErrorAction SilentlyContinue | ForEach-Object {
            try {
                Remove-Item -LiteralPath $_.FullName -Recurse -Force -ErrorAction Stop
                $cleanedCount++
            } catch {}
        }
    }
}

# 2. Windows Temp
if ($All -or $Temp) {
    Write-Output "[+] Curatare fisiere temporare sistem (C:\Windows\Temp)..."
    $winTemp = "$env:SystemRoot\Temp"
    if (Test-Path -LiteralPath $winTemp) {
        Get-ChildItem -LiteralPath $winTemp -Force -ErrorAction SilentlyContinue | ForEach-Object {
            try {
                Remove-Item -LiteralPath $_.FullName -Recurse -Force -ErrorAction Stop
                $cleanedCount++
            } catch {}
        }
    }
}

# 3. Windows Update Download Cache
if ($All -or $WindowsUpdate) {
    Write-Output "[+] Curatare cache descarcari Windows Update..."
    $wuPath = "$env:SystemRoot\SoftwareDistribution\Download"
    if (Test-Path -LiteralPath $wuPath) {
        Get-ChildItem -LiteralPath $wuPath -Force -ErrorAction SilentlyContinue | ForEach-Object {
            try {
                Remove-Item -LiteralPath $_.FullName -Recurse -Force -ErrorAction Stop
                $cleanedCount++
            } catch {}
        }
    }
}

# 4. Crash Dumps & WER
if ($All -or $CrashDumps) {
    Write-Output "[+] Curatare crash dumps si rapoarte erori..."
    $werPath = "$env:ProgramData\Microsoft\Windows\WER"
    if (Test-Path -LiteralPath $werPath) {
        Get-ChildItem -LiteralPath $werPath -Force -ErrorAction SilentlyContinue | ForEach-Object {
            try {
                Remove-Item -LiteralPath $_.FullName -Recurse -Force -ErrorAction Stop
                $cleanedCount++
            } catch {}
        }
    }
    $miniDump = "$env:SystemRoot\Minidump"
    if (Test-Path -LiteralPath $miniDump) {
        Get-ChildItem -LiteralPath $miniDump -Force -ErrorAction SilentlyContinue | ForEach-Object {
            try {
                Remove-Item -LiteralPath $_.FullName -Recurse -Force -ErrorAction Stop
                $cleanedCount++
            } catch {}
        }
    }
    $memoryDmp = "$env:SystemRoot\MEMORY.DMP"
    if (Test-Path -LiteralPath $memoryDmp) {
        try { Remove-Item -LiteralPath $memoryDmp -Force -ErrorAction Stop } catch {}
    }
}

# 5. Recycle Bin
if ($All -or $RecycleBin) {
    Write-Output "[+] Golire cos de reciclare (Recycle Bin)..."
    try {
        Clear-RecycleBin -Force -ErrorAction SilentlyContinue
    } catch {}
}

# 6. Delivery Optimization Cache
if ($All) {
    Write-Output "[+] Curatare cache Delivery Optimization..."
    $doPath = "$env:SystemRoot\ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization"
    if (Test-Path -LiteralPath $doPath) {
        Get-ChildItem -LiteralPath $doPath -Force -ErrorAction SilentlyContinue | ForEach-Object {
            try {
                Remove-Item -LiteralPath $_.FullName -Recurse -Force -ErrorAction Stop
                $cleanedCount++
            } catch {}
        }
    }
}

Write-Output "[OK] Curatare disc finalizata cu succes!"
