[CmdletBinding()]
param(
    [switch]$SkipRestorePoint,
    [switch]$SkipAiRemoval,
    [switch]$SkipGamingTweaks,
    [switch]$SkipDebloat,
    [switch]$SkipVisualEffects,
    [switch]$SkipMaintenance
)

$baseDir = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host "=========================================================="
Write-Host "NexWin Optimization Pipeline"
Write-Host "=========================================================="

# 1. Restore Point
if (-not $SkipRestorePoint) {
    Write-Host "`n[1/7] Creare punct de restaurare..."
    & "$baseDir\Invoke-RestorePoint.ps1" -Description "NexWin Boost"
}

# 2. AI Removal
if (-not $SkipAiRemoval) {
    Write-Host "`n[2/7] Eliminare componente AI (Copilot, Recall)..."
    & "$baseDir\Invoke-AiRemoval.ps1" -All
}

# 3. Gaming Tweaks
if (-not $SkipGamingTweaks) {
    Write-Host "`n[3/7] Configurare profil gaming (VBS, HAGS, Power Plan)..."
    & "$baseDir\Invoke-GamingTweaks.ps1" -All
}

# 4. Service Debloat & SvcHost
if (-not $SkipDebloat) {
    Write-Host "`n[4/7] Oprire servicii telemetrie si consolidare SvcHost..."
    & "$baseDir\Invoke-DebloatServices.ps1" -All
}

# 5. Visual Effects
if (-not $SkipVisualEffects) {
    Write-Host "`n[5/7] Ajustare efecte vizuale (pastrare fonturi clare)..."
    & "$baseDir\Invoke-VisualEffects.ps1" -Optimize
}

# 6. Maintenance
if (-not $SkipMaintenance) {
    Write-Host "`n[6/7] Curatare fisiere temporare si TRIM..."
    & "$baseDir\Invoke-Maintenance.ps1" -CleanTemp -CleanShaderCache -RunTrim
}

# 7. Final Status Report
Write-Host "`n[STEP 7/7] Fetching updated system status..."
$statusJson = & "$baseDir\Get-SystemStatus.ps1"
Write-Host "STATUS REPORT: $statusJson"

Write-Host "`n=========================================================="
Write-Host "SUCCESS: NexWin One-Click Optimization Completed!"
Write-Host "NOTE: To finalize VBS, HAGS, and SvcHost consolidation,"
Write-Host "please restart your computer."
Write-Host "=========================================================="
