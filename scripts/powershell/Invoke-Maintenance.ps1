[CmdletBinding()]
param(
    [switch]$CleanTemp,
    [switch]$CleanShaderCache,
    [switch]$RunTrim
)

$ErrorActionPreference = 'SilentlyContinue'

if ($CleanTemp) {
    Write-Host "Cleaning temporary files and system cache..."
    $tempFolders = @(
        $env:TEMP,
        "$env:SystemRoot\Temp"
    )

    $freedBytes = 0
    foreach ($folder in $tempFolders) {
        if (Test-Path $folder) {
            Get-ChildItem -Path $folder -Recurse -Force -ErrorAction SilentlyContinue | ForEach-Object {
                try {
                    $freedBytes += $_.Length
                    Remove-Item $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
                } catch {}
            }
        }
    }
    $freedMB = [math]::Round($freedBytes / 1MB, 2)
    Write-Host "Temp cleanup completed. Freed ~$freedMB MB."
}

if ($CleanShaderCache) {
    Write-Host "Clearing DirectX and GPU Shader Cache..."
    $shaderFolders = @(
        "$env:LOCALAPPDATA\D3DSCache",
        "$env:LOCALAPPDATA\NVIDIA\DXCache",
        "$env:LOCALAPPDATA\AMD\DxCache"
    )
    foreach ($folder in $shaderFolders) {
        if (Test-Path $folder) {
            Remove-Item -Path "$folder\*" -Recurse -Force -ErrorAction SilentlyContinue
            Write-Host "Cleared shader cache at: $folder"
        }
    }
}

if ($RunTrim) {
    Write-Host "Running SSD TRIM optimization on system drive..."
    try {
        Optimize-Volume -DriveLetter C -ReTrim -Verbose
        Write-Host "TRIM executed successfully for drive C:."
    } catch {
        Write-Host "TRIM command not supported or not required for this storage type."
    }
}

Write-Host "SUCCESS: Maintenance routine finished."
