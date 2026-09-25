[CmdletBinding()]
param([string]$Path = "$env:LOCALAPPDATA\NexWin\system-snapshot.json")

$registryPaths = @(
  'HKCU:\Software\Policies\Microsoft\Windows\WindowsCopilot',
  'HKCU:\Software\Policies\Microsoft\Windows\WindowsAI',
  'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced',
  'HKCU:\Control Panel\Desktop',
  'HKCU:\Control Panel\Desktop\WindowMetrics',
  'HKCU:\Software\Microsoft\GameBar',
  'HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot',
  'HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsAI',
  'HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection',
  'HKLM:\SYSTEM\CurrentControlSet\Control\DeviceGuard',
  'HKLM:\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity',
  'HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers',
  'HKLM:\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling',
  'HKLM:\SYSTEM\CurrentControlSet\Control'
)
$registry = @()
foreach ($key in $registryPaths) {
  if (Test-Path $key) {
    $props = Get-ItemProperty -Path $key
    $values = [ordered]@{}
    foreach ($property in $props.PSObject.Properties) {
      if ($property.Name -notmatch '^PS') { $values[$property.Name] = $property.Value }
    }
    $registry += [PSCustomObject]@{ Path = $key; Values = $values }
  }
}
$services = @('DiagTrack','dmwappushservice','DPS','WdiServiceHost','WdiSystemHost','WerSvc','AIFabricService','MapsBroker','lfsvc','TrkWks','DoSvc','SysMain','WSearch') | ForEach-Object {
  $service = Get-CimInstance Win32_Service -Filter "Name='$($_)'" -ErrorAction SilentlyContinue
  if ($service) { [PSCustomObject]@{ Name=$service.Name; State=$service.State; StartMode=$service.StartMode } }
}
$dns = @(Get-DnsClientServerAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue | Select-Object InterfaceAlias,InterfaceIndex,ServerAddresses)
$power = (powercfg /getactivescheme 2>$null | Out-String).Trim()
$defender = @()
try { $defender = @((Get-MpPreference -ErrorAction Stop).ExclusionPath) } catch {}
$snapshot = [PSCustomObject]@{ CreatedAt=(Get-Date).ToUniversalTime().ToString('o'); Registry=$registry; Services=@($services); Dns=$dns; PowerPlan=$power; DefenderExclusions=$defender }
$parent = Split-Path -Parent $Path
New-Item -ItemType Directory -Path $parent -Force | Out-Null
$snapshot | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $Path -Encoding UTF8
Write-Output "SNAPSHOT_SAVED:$Path"
