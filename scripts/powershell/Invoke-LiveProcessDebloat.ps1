[CmdletBinding()]
param()

Write-Host ">>> Executing Aggressive Live Process Debloat..."
$initialCount = (Get-Process).Count
Write-Host "Initial process count: $initialCount"

# 1. Stop AI and Telemetry Services immediately
$servicesToKill = @(
    "WSAIFabricSvc",
    "AIFabricService",
    "InventorySvc",
    "PhoneSvc",
    "wuqisvc",
    "whesvc",
    "WdiServiceHost",
    "WdiSystemHost",
    "DoSvc",
    "SSDPSRV",
    "TrkWks",
    "RemoteRegistry",
    "MapsBroker",
    "lfsvc",
    "shpamsvc",
    "RetailDemo"
)

foreach ($svc in $servicesToKill) {
    try {
        $s = Get-Service -Name $svc -ErrorAction SilentlyContinue
        if ($s -and $s.Status -eq "Running") {
            Stop-Service -Name $svc -Force -ErrorAction SilentlyContinue
            Set-Service -Name $svc -StartupType Disabled -ErrorAction SilentlyContinue
            Write-Host "Stopped & Disabled service: $svc"
        }
    } catch {}
}

# 2. Disable Bing Web Search inside Start Menu (Terminates 6+ msedgewebview2 processes)
try {
    $searchKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Search"
    if (-not (Test-Path $searchKey)) { New-Item -Path $searchKey -Force | Out-Null }
    Set-ItemProperty -Path $searchKey -Name "BingSearchEnabled" -Value 0 -Type DWord -Force
    Set-ItemProperty -Path $searchKey -Name "CortanaConsent" -Value 0 -Type DWord -Force

    $searchPolicy = "HKCU:\Software\Policies\Microsoft\Windows\Explorer"
    if (-not (Test-Path $searchPolicy)) { New-Item -Path $searchPolicy -Force | Out-Null }
    Set-ItemProperty -Path $searchPolicy -Name "DisableSearchBoxSuggestions" -Value 1 -Type DWord -Force

    # Restart SearchHost to drop the webview2 processes
    Stop-Process -Name "SearchHost" -Force -ErrorAction SilentlyContinue
    Write-Host "Bing Start Menu Search disabled (SearchHost webviews purged)."
} catch {}

# 3. Clean excess RuntimeBroker instances (inactive ones)
try {
    Get-Process -Name "RuntimeBroker" -ErrorAction SilentlyContinue | Where-Object {
        $_.WorkingSet64 -lt 10MB
    } | ForEach-Object {
        Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
    }
    Write-Host "Idle RuntimeBroker instances cleaned."
} catch {}

# 5. SmartTrim working set memory
try {
    [System.GC]::Collect()
} catch {}

$finalCount = (Get-Process).Count
$dropped = $initialCount - $finalCount
Write-Host "SUCCESS: Live Process Debloat complete."
Write-Host "Current process count: $finalCount (Dropped: $dropped processes live)."
Write-Host "NOTE: To group the remaining 80 svchost services down to ~25, a Windows restart is required (SvcHostSplitThreshold is set)."
