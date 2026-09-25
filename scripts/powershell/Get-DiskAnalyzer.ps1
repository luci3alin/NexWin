[CmdletBinding()]
param(
    [ValidateSet("Drives", "Scan", "ScanLargest", "EstimateJunk")]
    [string]$Action = "Drives",
    [string]$Path = "",
    [string]$Drive = "C:"
)

$ErrorActionPreference = "SilentlyContinue"

# Fast directory scanner helper in C#
if (-not ([System.Management.Automation.PSTypeName]'FastDiskScanner').Type) {
    $csharpCode = @"
using System;
using System.IO;
using System.Collections.Generic;

public class FastDiskScanner {
    public static long GetDirectorySize(string path) {
        long totalSize = 0;
        var stack = new Stack<string>();
        stack.Push(path);

        while (stack.Count > 0) {
            string current = stack.Pop();
            try {
                var dirInfo = new DirectoryInfo(current);
                if (dirInfo.Attributes.HasFlag(FileAttributes.ReparsePoint)) {
                    continue;
                }

                foreach (var file in dirInfo.EnumerateFiles()) {
                    try { totalSize += file.Length; } catch {}
                }

                foreach (var dir in dirInfo.EnumerateDirectories()) {
                    try {
                        if (!dir.Attributes.HasFlag(FileAttributes.ReparsePoint)) {
                            stack.Push(dir.FullName);
                        }
                    } catch {}
                }
            } catch {}
        }
        return totalSize;
    }
}
"@
    Add-Type -TypeDefinition $csharpCode -Language CSharp -ErrorAction SilentlyContinue
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

switch ($Action) {
    "Drives" {
        $disks = Get-CimInstance Win32_LogicalDisk -Filter "DriveType=3" -ErrorAction SilentlyContinue
        $result = @()
        foreach ($d in $disks) {
            $total = [long]$d.Size
            $free = [long]$d.FreeSpace
            $used = $total - $free
            $pct = if ($total -gt 0) { [math]::Round(($used / $total) * 100, 1) } else { 0 }

            $result += [PSCustomObject]@{
                DeviceID = $d.DeviceID
                VolumeName = if ($d.VolumeName) { $d.VolumeName } else { "Disc Local" }
                FileSystem = $d.FileSystem
                TotalBytes = $total
                TotalGB = [math]::Round($total / 1GB, 2)
                FreeBytes = $free
                FreeGB = [math]::Round($free / 1GB, 2)
                UsedBytes = $used
                UsedGB = [math]::Round($used / 1GB, 2)
                PercentUsed = $pct
            }
        }
        $json = $result | ConvertTo-Json -Compress
        Write-Output $json
    }

    "Scan" {
        if (-not $Path -or -not (Test-Path -LiteralPath $Path)) {
            $Path = if ($Drive) { "$Drive\" } else { "C:\" }
        }

        $dirInfo = Get-Item -LiteralPath $Path -Force -ErrorAction SilentlyContinue
        if (-not $dirInfo) {
            Write-Output "[]"
            exit 0
        }

        $subDirs = Get-ChildItem -LiteralPath $Path -Directory -Force -ErrorAction SilentlyContinue |
            Where-Object {
                $_.Name -notmatch '^\$|System Volume Information|Recovery'
            }

        $totalCount = $subDirs.Count
        $folders = @()
        $idx = 0
        $sw = [System.Diagnostics.Stopwatch]::StartNew()

        foreach ($sub in $subDirs) {
            $idx++
            $elapsed = [math]::Max(0.1, $sw.Elapsed.TotalSeconds)
            $avgPerItem = $elapsed / $idx
            $remaining = [math]::Max(0, $totalCount - $idx)
            $etaSec = [math]::Round($avgPerItem * $remaining)
            $pct = [math]::Round(($idx / [math]::Max(1, $totalCount)) * 100)

            $progObj = [PSCustomObject]@{
                percent = $pct
                current = $sub.Name
                index = $idx
                total = $totalCount
                eta = $etaSec
            }
            $progJson = $progObj | ConvertTo-Json -Compress
            Write-Output "DISK_PROGRESS:$progJson"

            $size = [FastDiskScanner]::GetDirectorySize($sub.FullName)

            $folders += [PSCustomObject]@{
                Name = $sub.Name
                FullPath = $sub.FullName
                SizeBytes = $size
                SizeFormatted = Format-Size $size
                Type = "folder"
            }
        }

        $directFiles = Get-ChildItem -LiteralPath $Path -File -Force -ErrorAction SilentlyContinue
        foreach ($f in $directFiles) {
            $folders += [PSCustomObject]@{
                Name = $f.Name
                FullPath = $f.FullName
                SizeBytes = [long]$f.Length
                SizeFormatted = Format-Size [long]$f.Length
                Type = "file"
            }
        }

        $doneObj = [PSCustomObject]@{
            percent = 100
            current = "Complet"
            index = $totalCount
            total = $totalCount
            eta = 0
        }
        Write-Output "DISK_PROGRESS:$($doneObj | ConvertTo-Json -Compress)"

        $sorted = $folders | Sort-Object -Property SizeBytes -Descending
        $json = $sorted | ConvertTo-Json -Compress
        Write-Output "FINAL_RESULT:$json"
    }

    "ScanLargest" {
        $targetRoot = if ($Path -and (Test-Path -LiteralPath $Path)) { $Path } else { "$Drive\" }
        
        $largest = Get-ChildItem -LiteralPath $targetRoot -Recurse -File -Force -ErrorAction SilentlyContinue |
            Sort-Object -Property Length -Descending |
            Select-Object -First 40 |
            ForEach-Object {
                [PSCustomObject]@{
                    Name = $_.Name
                    FullPath = $_.FullName
                    SizeBytes = [long]$_.Length
                    SizeFormatted = Format-Size [long]$_.Length
                    Extension = $_.Extension.ToLower()
                    LastWriteTime = $_.LastWriteTime.ToString("yyyy-MM-dd HH:mm")
                }
            }

        $json = $largest | ConvertTo-Json -Compress
        Write-Output $json
    }

    "EstimateJunk" {
        $categories = @()
        $totalBytes = 0

        $junkTargets = @(
            @{ Name = "Fisiere Temporare Utilizator"; Path = $env:TEMP },
            @{ Name = "Fisiere Temporare Windows"; Path = "$env:SystemRoot\Temp" },
            @{ Name = "Cache Descarcari Windows Update"; Path = "$env:SystemRoot\SoftwareDistribution\Download" },
            @{ Name = "Rapoarte Erori & Crash Dumps"; Path = "$env:ProgramData\Microsoft\Windows\WER" },
            @{ Name = "Minidump Crash Windows"; Path = "$env:SystemRoot\Minidump" },
            @{ Name = "Cache Optimizare Livrare"; Path = "$env:SystemRoot\ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization" }
        )

        foreach ($t in $junkTargets) {
            $targetPath = $t.Path
            $catBytes = 0
            if (Test-Path -LiteralPath $targetPath) {
                $catBytes = [FastDiskScanner]::GetDirectorySize($targetPath)
            }
            $totalBytes += $catBytes
            $categories += [PSCustomObject]@{
                Name = $t.Name
                Path = $targetPath
                SizeBytes = $catBytes
                SizeFormatted = Format-Size $catBytes
            }
        }

        $rbBytes = 0
        $drives = Get-CimInstance Win32_LogicalDisk -Filter "DriveType=3" -ErrorAction SilentlyContinue
        foreach ($d in $drives) {
            $rbPath = "$($d.DeviceID)\$Recycle.Bin"
            if (Test-Path -LiteralPath $rbPath) {
                $rbBytes += [FastDiskScanner]::GetDirectorySize($rbPath)
            }
        }
        $totalBytes += $rbBytes
        $categories += [PSCustomObject]@{
            Name = "Cosul de Reciclare (Recycle Bin)"
            Path = "Recycle.Bin"
            SizeBytes = $rbBytes
            SizeFormatted = Format-Size $rbBytes
        }

        $result = [PSCustomObject]@{
            Categories = $categories
            TotalBytes = $totalBytes
            TotalFormatted = Format-Size $totalBytes
            TotalMB = [math]::Round($totalBytes / 1MB, 2)
            TotalGB = [math]::Round($totalBytes / 1GB, 2)
        }

        $json = $result | ConvertTo-Json -Compress
        Write-Output $json
    }
}
