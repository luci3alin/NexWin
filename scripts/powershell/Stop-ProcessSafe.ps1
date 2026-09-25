[CmdletBinding(DefaultParameterSetName = 'ByPid')]
param(
    [Parameter(ParameterSetName = 'ByPid', Mandatory = $true)]
    [ValidateRange(5, 2147483647)]
    [int]$Pid,

    [Parameter(ParameterSetName = 'ByName', Mandatory = $true)]
    [ValidatePattern('^[a-zA-Z0-9_.-]{1,128}$')]
    [string]$Name
)

$protected = @('System', 'Idle', 'Registry', 'smss', 'csrss', 'wininit', 'services', 'lsass', 'winlogon', 'explorer')
if ($Name -and $protected -contains $Name.TrimEnd('.exe')) {
    throw "Proces protejat: $Name"
}

if ($PSCmdlet.ParameterSetName -eq 'ByPid') {
    $process = Get-Process -Id $Pid -ErrorAction Stop
    if ($protected -contains $process.ProcessName) { throw "Proces protejat: $($process.ProcessName)" }
    Stop-Process -Id $Pid -Force -ErrorAction Stop
} else {
    Get-Process -Name $Name.TrimEnd('.exe') -ErrorAction Stop |
        Where-Object { $protected -notcontains $_.ProcessName } |
        Stop-Process -Force -ErrorAction Stop
}

Write-Output "Proces oprit în siguranță."
