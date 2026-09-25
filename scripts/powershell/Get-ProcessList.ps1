[CmdletBinding()]
param(
    [int]$Top = 40
)

$ErrorActionPreference = 'SilentlyContinue'

$procs = Get-Process | Group-Object ProcessName | ForEach-Object {
    $first = $_.Group[0]
    $totalMem = ($_.Group | Measure-Object -Property WorkingSet64 -Sum).Sum
    $memMB = [math]::Round($totalMem / 1MB, 1)
    
    [PSCustomObject]@{
        name    = $_.Name
        count   = $_.Count
        memMB   = $memMB
        pids    = ($_.Group | Select-Object -ExpandProperty Id)
    }
} | Sort-Object memMB -Descending | Select-Object -First $Top

$procs | ConvertTo-Json -Compress
