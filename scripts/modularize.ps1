$lines = [System.IO.File]::ReadAllLines("C:\Users\luci3\Documents\ChatGPT\NexWin\native\MainWindow.xaml.cs.bak")

function ExtractRange($start, $end) {
    if ($end -gt $lines.Length) { $end = $lines.Length }
    $len = $end - $start + 1
    $subset = New-Object string[] $len
    [Array]::Copy($lines, $start - 1, $subset, 0, $len)
    return [string]::Join("`r`n", $subset)
}

function WritePartial($fileName, $codeBody) {
    $tpl = @"
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using Path = System.IO.Path;

namespace NexWin.Native;

public partial class MainWindow : Window
{
$codeBody
}
"@
    [System.IO.File]::WriteAllText("C:\Users\luci3\Documents\ChatGPT\NexWin\native\$fileName", $tpl, [System.Text.Encoding]::UTF8)
    Write-Host "Wrote $fileName ($($codeBody.Split("`n").Length) lines)"
}

# 1. Dashboard (1184 - 1786)
WritePartial "MainWindow.Dashboard.cs" (ExtractRange 1184 1786)

# 2. Profiles (1787 - 2019)
WritePartial "MainWindow.Profiles.cs" (ExtractRange 1787 2019)

# 3. Hardware (2083 - 2635 and 3780 - 3885)
$hwCode = (ExtractRange 2083 2635) + "`r`n`r`n" + (ExtractRange 3780 3885)
WritePartial "MainWindow.Hardware.cs" $hwCode

# 4. AiRecall (2636 - 2748)
WritePartial "MainWindow.AiRecall.cs" (ExtractRange 2636 2748)

# 5. Services (2749 - 2834)
WritePartial "MainWindow.Services.cs" (ExtractRange 2749 2834)

# 6. Gaming (2835 - 3670)
WritePartial "MainWindow.Gaming.cs" (ExtractRange 2835 3670)

# 7. Processes (Startup: 3671 - 3779, Processes: 6431 - 7155)
$procCode = (ExtractRange 3671 3779) + "`r`n`r`n" + (ExtractRange 6431 7155)
WritePartial "MainWindow.Processes.cs" $procCode

# 8. Apps (3886 - 5127)
WritePartial "MainWindow.Apps.cs" (ExtractRange 3886 5127)

# 9. Customizer (5128 - 5659)
WritePartial "MainWindow.Customizer.cs" (ExtractRange 5128 5659)

# 10. SystemSnapshot (5660 - 5890)
WritePartial "MainWindow.SystemSnapshot.cs" (ExtractRange 5660 5890)

# 11. Network (5891 - 6430)
WritePartial "MainWindow.Network.cs" (ExtractRange 5891 6430)

# 12. Disk (7156 - 8097)
WritePartial "MainWindow.Disk.cs" (ExtractRange 7156 8097)

# 13. Logs (8098 - 9047)
WritePartial "MainWindow.Logs.cs" (ExtractRange 8098 9047)

# 14. Modals (9048 - 10086)
WritePartial "MainWindow.Modals.cs" (ExtractRange 9048 10086)

# 15. Core MainWindow.xaml.cs (1 - 1183 + Sparkline 2020-2082 + Records 10087-10102)
$core1 = ExtractRange 1 1183
$spark = ExtractRange 2020 2082
$records = ExtractRange 10087 10102
$coreFull = $core1 + "`r`n`r`n" + $spark + "`r`n`r`n" + $records
[System.IO.File]::WriteAllText("C:\Users\luci3\Documents\ChatGPT\NexWin\native\MainWindow.xaml.cs", $coreFull, [System.Text.Encoding]::UTF8)
Write-Host "Wrote core MainWindow.xaml.cs ($($coreFull.Split("`n").Length) lines)"
