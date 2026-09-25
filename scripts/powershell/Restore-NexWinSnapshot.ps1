[CmdletBinding()]
param([string]$Path = "$env:LOCALAPPDATA\NexWin\system-snapshot.json")

if (-not (Test-Path $Path)) { throw "Nu există snapshot NexWin: $Path" }
$snapshot = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
foreach ($entry in @($snapshot.Registry)) {
  if (-not (Test-Path $entry.Path)) { New-Item -Path $entry.Path -Force | Out-Null }
  foreach ($property in $entry.Values.PSObject.Properties) {
    try { Set-ItemProperty -Path $entry.Path -Name $property.Name -Value $property.Value -Force -ErrorAction Stop } catch { Write-Warning "Registry restore eșuat: $($entry.Path)\$($property.Name)" }
  }
}
foreach ($item in @($snapshot.Services)) {
  try {
    Set-Service -Name $item.Name -StartupType $item.StartMode -ErrorAction Stop
    if ($item.State -eq 'Running') { Start-Service -Name $item.Name -ErrorAction SilentlyContinue } else { Stop-Service -Name $item.Name -Force -ErrorAction SilentlyContinue }
  } catch { Write-Warning "Service restore eșuat: $($item.Name)" }
}
foreach ($adapter in @($snapshot.Dns)) {
  try { Set-DnsClientServerAddress -InterfaceIndex $adapter.InterfaceIndex -ServerAddresses @($adapter.ServerAddresses) -ErrorAction Stop } catch {}
}
Write-Output "SNAPSHOT_RESTORED:$Path"
