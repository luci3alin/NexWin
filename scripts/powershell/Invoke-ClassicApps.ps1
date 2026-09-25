[CmdletBinding()]
param(
    [switch]$EnablePhotoViewer,
    [switch]$EnableClassicNotepad,
    [switch]$EnableClassicPaint
)

if ($EnablePhotoViewer) {
    Write-Host "Enabling Classic Windows Photo Viewer..."
    $pvPath = "HKLM:\SOFTWARE\Microsoft\Windows Photo Viewer\Capabilities\FileAssociations"
    if (-not (Test-Path $pvPath)) {
        New-Item -Path $pvPath -Force | Out-Null
    }
    $extensions = @(".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".tif")
    foreach ($ext in $extensions) {
        Set-ItemProperty -Path $pvPath -Name $ext -Value "PhotoViewer.FileAssoc.Tiff" -Force | Out-Null
    }
    Write-Host "Windows Photo Viewer file associations enabled."
}

if ($EnableClassicNotepad) {
    Write-Host "Configuring Classic Notepad..."
    # Set execution redirect for notepad
    $appPath = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\notepad.exe"
    if (Test-Path $appPath) {
        Remove-Item -Path $appPath -Recurse -Force -ErrorAction SilentlyContinue
    }
    Write-Host "Classic Notepad restored."
}

if ($EnableClassicPaint) {
    Write-Host "Configuring Classic MS Paint..."
    $appPath = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\mspaint.exe"
    if (Test-Path $appPath) {
        Remove-Item -Path $appPath -Recurse -Force -ErrorAction SilentlyContinue
    }
    Write-Host "Classic MS Paint restored."
}

Write-Host "SUCCESS: Classic application settings configured."
