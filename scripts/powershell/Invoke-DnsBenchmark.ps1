[CmdletBinding()]
param(
    [switch]$Benchmark,
    [string]$SetProvider,
    [string]$Primary,
    [string]$Secondary,
    [switch]$ResetDns
)

function Get-ActiveAdapter {
    Get-NetAdapter | Where-Object { 
        $_.Status -eq 'Up' -and 
        $_.InterfaceDescription -notmatch 'Virtual|Loopback|TAP|VPN|Hyper-V|VMware' 
    } | Select-Object -First 1
}

$adapter = Get-ActiveAdapter

if ($ResetDns) {
    if ($adapter) {
        Set-DnsClientServerAddress -InterfaceIndex $adapter.ifIndex -ResetServerAddresses -ErrorAction SilentlyContinue
        Clear-DnsClientCache -ErrorAction SilentlyContinue
        Write-Host "SUCCESS: DNS resetat la configuratia implicita a routerului (DHCP)."
    } else {
        Write-Host "Warning: Nu s-a gasit o placa de retea activa."
    }
    exit 0
}

if ($SetProvider) {
    $dnsMap = @{
        "Cloudflare" = @{ Primary = "1.1.1.1"; Secondary = "1.0.0.1" }
        "Google"     = @{ Primary = "8.8.8.8"; Secondary = "8.8.4.4" }
        "Quad9"      = @{ Primary = "9.9.9.9"; Secondary = "149.112.112.112" }
    }
    if ($dnsMap.ContainsKey($SetProvider)) {
        $p = $dnsMap[$SetProvider].Primary
        $s = $dnsMap[$SetProvider].Secondary
        if ($adapter) {
            Set-DnsClientServerAddress -InterfaceIndex $adapter.ifIndex -ServerAddresses @($p, $s) -ErrorAction SilentlyContinue
            Clear-DnsClientCache -ErrorAction SilentlyContinue
            Write-Host "SUCCESS: DNS setat pe $SetProvider ($p, $s) pentru interfata $($adapter.Name)."
        } else {
            Write-Host "Warning: Nu s-a gasit o placa de retea activa."
        }
    }
    exit 0
}

if ($Primary -and $Secondary) {
    if ($adapter) {
        Set-DnsClientServerAddress -InterfaceIndex $adapter.ifIndex -ServerAddresses @($Primary, $Secondary) -ErrorAction SilentlyContinue
        Clear-DnsClientCache -ErrorAction SilentlyContinue
        Write-Host "SUCCESS: DNS personalizat ($Primary, $Secondary) aplicat pe $($adapter.Name)."
    }
    exit 0
}

# Run Benchmark. ICMP ping is not a DNS measurement, so query the selected
# resolver directly and retain packet ping only as supplementary information.
Write-Host "Rulare test de latenta DNS (query real)..."
$providers = @(
    @{ Name = "Cloudflare"; Primary = "1.1.1.1"; Secondary = "1.0.0.1" },
    @{ Name = "Google"; Primary = "8.8.8.8"; Secondary = "8.8.4.4" },
    @{ Name = "Quad9"; Primary = "9.9.9.9"; Secondary = "149.112.112.112" }
)

$results = @()
foreach ($prov in $providers) {
    $samples = @()
    1..4 | ForEach-Object {
        $watch = [System.Diagnostics.Stopwatch]::StartNew()
        try {
            Resolve-DnsName -Name 'example.com' -Server $prov.Primary -Type A -DnsOnly -ErrorAction Stop | Out-Null
            $watch.Stop()
            $samples += $watch.Elapsed.TotalMilliseconds
        } catch { $watch.Stop() }
    }
    $avg = if ($samples.Count) { [math]::Round(($samples | Measure-Object -Average).Average, 1) } else { 999 }
    $pingObj = Test-Connection -ComputerName $prov.Primary -Count 3 -ErrorAction SilentlyContinue
    $ping = if ($pingObj) { [math]::Round(($pingObj | Measure-Object -Property ResponseTime -Average).Average, 1) } else { 999 }
    $results += [PSCustomObject]@{
        Name = $prov.Name
        Primary = $prov.Primary
        Secondary = $prov.Secondary
        QueryMs = $avg
        Ping = $ping
        Samples = $samples.Count
    }
    Write-Host "$($prov.Name) ($($prov.Primary)): $avg ms"
}

$json = $results | ConvertTo-Json -Compress
Write-Host "DNS_BENCHMARK_DATA:$json"
