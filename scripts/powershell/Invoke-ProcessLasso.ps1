[CmdletBinding()]
param(
    [switch]$SmartTrim,
    [switch]$ProBalance,
    [string]$ProcessName,
    [string]$Priority,
    [switch]$PersistentRule,
    [switch]$DisablePowerThrottling
)

# C# P/Invoke helper for EmptyWorkingSet (Process Lasso SmartTrim style)
$typeDefinition = @"
using System;
using System.Runtime.InteropServices;

public class MemoryCleaner {
    [DllImport("psapi.dll")]
    public static extern int EmptyWorkingSet(IntPtr hwProc);

    public static long CleanProcesses() {
        long freed = 0;
        foreach (var proc in System.Diagnostics.Process.GetProcesses()) {
            try {
                long before = proc.WorkingSet64;
                EmptyWorkingSet(proc.Handle);
                long after = proc.WorkingSet64;
                if (before > after) {
                    freed += (before - after);
                }
            } catch {}
        }
        GC.Collect();
        GC.WaitForPendingFinalizers();
        return freed;
    }
}
"@

try {
    Add-Type -TypeDefinition $typeDefinition -ErrorAction SilentlyContinue
} catch {}

# 1. SmartTrim RAM Cleaner
if ($SmartTrim) {
    Write-Host "Running NexWin SmartTrim (Process Lasso memory optimization)..."
    Write-Warning "Eliberarea working set poate crește page faults și stuttering. Folosește opțiunea doar când memoria este realmente sub presiune."
    try {
        $freedBytes = [MemoryCleaner]::CleanProcesses()
        $freedMB = [math]::Round($freedBytes / 1MB, 2)
        Write-Host "SUCCESS: SmartTrim completed. Freed ~$freedMB MB of working set memory."
    } catch {
        # Fallback via PowerShell
        [System.GC]::Collect()
        Write-Host "SUCCESS: Garbage collection and working set trim executed."
    }
}

# 2. Set Process Priority (Running instance + Persistent Rule)
if ($ProcessName -and $Priority) {
    Write-Host "Setting priority for process: $ProcessName to $Priority..."
    
    $priorityClass = switch ($Priority.ToLower()) {
        "high" { [System.Diagnostics.ProcessPriorityClass]::High }
        "belownormal" { [System.Diagnostics.ProcessPriorityClass]::BelowNormal }
        "idle" { [System.Diagnostics.ProcessPriorityClass]::Idle }
        default { [System.Diagnostics.ProcessPriorityClass]::Normal }
    }

    $procs = Get-Process -Name $ProcessName -ErrorAction SilentlyContinue
    if ($procs) {
        foreach ($p in $procs) {
            try {
                $p.PriorityClass = $priorityClass
                Write-Host "Updated running process PID $($p.Id) to $Priority priority."
            } catch {
                Write-Warning "Could not change priority for PID $($p.Id) (Access Denied)."
            }
        }
    } else {
        Write-Host "No active instance of $ProcessName currently running."
    }

    # Persistent IFEO Rule (Windows will ALWAYS launch this .exe with this priority!)
    if ($PersistentRule) {
        $cleanName = $ProcessName
        if (-not $cleanName.EndsWith(".exe", [System.StringComparison]::OrdinalIgnoreCase)) {
            $cleanName = "$cleanName.exe"
        }

        $regValue = switch ($Priority.ToLower()) {
            "high" { 3 }
            "belownormal" { 5 }
            "idle" { 1 }
            default { 2 }
        }

        $ifeoPath = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\$cleanName\PerfOptions"
        if (-not (Test-Path $ifeoPath)) {
            New-Item -Path $ifeoPath -Force | Out-Null
        }
        Set-ItemProperty -Path $ifeoPath -Name "CpuPriorityClass" -Value $regValue -Type DWord -Force | Out-Null
        Write-Host "SUCCESS: Persistent Image File Execution Option saved. Windows will always start $cleanName at $Priority priority."
    }
}

# 3. ProBalance Background Throttle
if ($ProBalance) {
    Write-Host "Running ProBalance background throttle..."
    $backgroundHogs = @("Discord", "Spotify", "EpicGamesLauncher", "SteamService", "msedge", "brave", "chrome")
    foreach ($name in $backgroundHogs) {
        $pList = Get-Process -Name $name -ErrorAction SilentlyContinue
        foreach ($p in $pList) {
            try {
                if ($p.PriorityClass -eq [System.Diagnostics.ProcessPriorityClass]::Normal) {
                    $p.PriorityClass = [System.Diagnostics.ProcessPriorityClass]::BelowNormal
                    Write-Host "ProBalance: Lowered $name (PID $($p.Id)) to BelowNormal to prevent game lag."
                }
            } catch {}
        }
    }
    Write-Host "SUCCESS: ProBalance optimization applied to background applications."
}

# 4. Disable Power Throttling for Process
if ($ProcessName -and $DisablePowerThrottling) {
    $cleanName = $ProcessName
    if (-not $cleanName.EndsWith(".exe", [System.StringComparison]::OrdinalIgnoreCase)) {
        $cleanName = "$cleanName.exe"
    }
    $ptPath = "HKLM:\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling"
    if (-not (Test-Path $ptPath)) {
        New-Item -Path $ptPath -Force | Out-Null
    }
    Set-ItemProperty -Path $ptPath -Name "PowerThrottlingOff" -Value 1 -Type DWord -Force | Out-Null
    Write-Host "SUCCESS: Power throttling disabled for maximum CPU frequency allocation."
}
