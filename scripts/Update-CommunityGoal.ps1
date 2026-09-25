param(
    [Parameter(Mandatory=$false)][double]$CurrentAmount = -1,
    [Parameter(Mandatory=$false)][double]$TargetAmount = -1,
    [Parameter(Mandatory=$false)][string]$NewSupporterName = "",
    [Parameter(Mandatory=$false)][string]$NewSupporterAmount = "10 EUR",
    [Parameter(Mandatory=$false)][string]$NewSupporterBadge = "Pro Supporter",
    [Parameter(Mandatory=$false)][switch]$PushToGitHub
)

$jsonPath = Join-Path $PSScriptRoot "..\community_goal.json"
if (-not (Test-Path $jsonPath)) {
    Write-Error "File not found: $jsonPath"
    exit 1
}

$data = Get-Content $jsonPath -Raw -Encoding UTF8 | ConvertFrom-Json

if ($CurrentAmount -ge 0) {
    $data.currentAmount = $CurrentAmount
}
if ($TargetAmount -gt 0) {
    $data.targetAmount = $TargetAmount
}
if (-not [string]::IsNullOrWhiteSpace($NewSupporterName)) {
    $newEntry = [PSCustomObject]@{
        name   = $NewSupporterName
        amount = $NewSupporterAmount
        badge  = $NewSupporterBadge
    }
    $existing = @($data.supporters)
    $data.supporters = @($newEntry) + $existing
}

$data | ConvertTo-Json -Depth 6 | Set-Content -Path $jsonPath -Encoding UTF8
Write-Host "Updated community_goal.json -> $($data.currentAmount) / $($data.targetAmount) $($data.currency)"

if ($PushToGitHub) {
    Push-Location (Join-Path $PSScriptRoot "..")
    git add community_goal.json
    git commit -m "chore(goal): update community support goal progress ($($data.currentAmount)/$($data.targetAmount) $($data.currency))"
    git push
    Pop-Location
    Write-Host "Successfully pushed live goal update to GitHub!"
}
