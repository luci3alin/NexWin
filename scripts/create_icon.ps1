Add-Type -AssemblyName System.Drawing

$sourcePath = "C:\Users\luci3\Documents\ChatGPT\NexWin\native\assets\logo_ribbon.png"
if (-not (Test-Path $sourcePath)) {
    Write-Error "Source file not found: $sourcePath"
    exit 1
}

$sourceImg = [System.Drawing.Bitmap]::FromFile($sourcePath)
$sizes = @(256, 128, 64, 48, 32, 16)
$entries = [System.Collections.Generic.List[psobject]]::new()

foreach ($sz in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $sz, $sz, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.DrawImage($sourceImg, 0, 0, $sz, $sz)
    $g.Dispose()

    if ($sz -eq 256) {
        # 256x256 is stored as PNG
        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $pngData = $ms.ToArray()
        $bmp.Dispose()
        $entries.Add([PSCustomObject]@{
            Size = 256
            Data = $pngData
            IsPng = $true
        })
    } else {
        # DIB format for 128, 64, 48, 32, 16
        # BITMAPINFOHEADER (40 bytes)
        $ms = New-Object System.IO.MemoryStream
        $bw = New-Object System.IO.BinaryWriter $ms
        $bw.Write([uint32]40) # biSize
        $bw.Write([int32]$sz) # biWidth
        $bw.Write([int32]($sz * 2)) # biHeight (doubled for XOR + AND)
        $bw.Write([uint16]1) # biPlanes
        $bw.Write([uint16]32) # biBitCount
        $bw.Write([uint32]0) # biCompression (BI_RGB)
        $bw.Write([uint32]($sz * $sz * 4)) # biSizeImage
        $bw.Write([int32]0) # biXPelsPerMeter
        $bw.Write([int32]0) # biYPelsPerMeter
        $bw.Write([uint32]0) # biClrUsed
        $bw.Write([uint32]0) # biClrImportant

        # Pixel data: bottom-to-top BGRA
        for ($y = $sz - 1; $y -ge 0; $y--) {
            for ($x = 0; $x -lt $sz; $x++) {
                $c = $bmp.GetPixel($x, $y)
                $bw.Write([byte]$c.B)
                $bw.Write([byte]$c.G)
                $bw.Write([byte]$c.R)
                $bw.Write([byte]$c.A)
            }
        }

        # AND mask: 1 bit per pixel, row padded to 32 bits (4 bytes)
        $rowBytes = [int][Math]::Ceiling($sz / 32.0) * 4
        $andMask = New-Object byte[] ($rowBytes * $sz) # all 0 for 32-bit with alpha
        $bw.Write($andMask)

        $bw.Flush()
        $dibData = $ms.ToArray()
        $bmp.Dispose()
        $entries.Add([PSCustomObject]@{
            Size = $sz
            Data = $dibData
            IsPng = $false
        })
    }
}
$sourceImg.Dispose()

$outFile = "C:\Users\luci3\Documents\ChatGPT\NexWin\native\assets\logo.ico"
$fs = [System.IO.File]::Create($outFile)
$bw = New-Object System.IO.BinaryWriter $fs

# ICO Header
$bw.Write([uint16]0) # Reserved
$bw.Write([uint16]1) # Type: 1 = Icon
$bw.Write([uint16]$entries.Count) # Image count

$headerSize = 6 + (16 * $entries.Count)
$currentOffset = $headerSize

# Directory entries
foreach ($item in $entries) {
    $w = if ($item.Size -ge 256) { 0 } else { [byte]$item.Size }
    $h = if ($item.Size -ge 256) { 0 } else { [byte]$item.Size }
    $bw.Write([byte]$w)
    $bw.Write([byte]$h)
    $bw.Write([byte]0) # Color count
    $bw.Write([byte]0) # Reserved
    $bw.Write([uint16]1) # Planes
    $bw.Write([uint16]32) # Bit count
    $bw.Write([uint32]$item.Data.Length) # Bytes in res
    $bw.Write([uint32]$currentOffset) # Image offset
    $currentOffset += $item.Data.Length
}

# Write data
foreach ($item in $entries) {
    $bw.Write($item.Data)
}

$bw.Close()
$fs.Close()

Copy-Item $outFile "C:\Users\luci3\Documents\ChatGPT\NexWin\public\logo.ico" -Force -ErrorAction SilentlyContinue
Copy-Item $outFile "C:\Users\luci3\Documents\ChatGPT\NexWin\dist\logo.ico" -Force -ErrorAction SilentlyContinue

Write-Host "Created native/assets/logo.ico successfully with DIB format: $((Get-Item $outFile).Length) bytes."
