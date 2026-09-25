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
    private void ShowPerformance()
    {
        PageRoot.Children.Clear();

        var tele = NativeTuning.GetHardwareTelemetry(lastCpuUsage);
        if (!string.IsNullOrEmpty(detectedCpuName) && detectedCpuName != "Necunoscut") tele.CpuName = detectedCpuName;
        if (!string.IsNullOrEmpty(detectedGpuName) && detectedGpuName != "Necunoscut") tele.GpuName = detectedGpuName;
        if (currentStatus != null)
        {
            tele.CpuUsagePercent = currentStatus.CpuLoad;
            tele.RamTotalGb = currentStatus.TotalMemoryGB;
            tele.RamUsedGb = currentStatus.UsedMemoryGB;
            tele.RamFreeGb = Math.Max(0, currentStatus.TotalMemoryGB - currentStatus.UsedMemoryGB);
        }
        double ramPct = tele.RamTotalGb > 0 ? (tele.RamUsedGb / tele.RamTotalGb * 100.0) : 44.0;

        // ================= 1. HEADER =================
        var headGrid = new Grid { Margin = new Thickness(0, 0, 0, 16) };
        headGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Left Header: Vibrant Icon Badge + Titles
        var leftHead = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var headIcoBox = new Border
        {
            Width = 40,
            Height = 40,
            CornerRadius = new CornerRadius(9),
            Background = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
            Margin = new Thickness(0, 0, 12, 0),
            Child = CreateVectorIcon(NexIcon.Pulse, Brushes.White, 20)
        };
        leftHead.Children.Add(headIcoBox);

        var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        titleStack.Children.Add(new TextBlock
        {
            Text = NexLocale.T("hardware_title"),
            FontSize = 18.5,
            FontWeight = FontWeights.Bold,
            Foreground = TextBrush
        });
        titleStack.Children.Add(new TextBlock
        {
            Text = NexLocale.T("hardware_subtitle"),
            FontSize = 11,
            Foreground = MutedBrush,
            Margin = new Thickness(0, 2, 0, 0)
        });
        leftHead.Children.Add(titleStack);
        Grid.SetColumn(leftHead, 0);
        headGrid.Children.Add(leftHead);

        PageRoot.Children.Add(headGrid);

        // Helper for Card Metric Row
        (StackPanel panel, TextBlock valText) BuildDynamicMetricRow(NexIcon ico, Brush icoBrush, string label, string val, Brush valBrush)
        {
            var p = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
            var ic = CreateVectorIcon(ico, icoBrush, 13);
            ic.VerticalAlignment = VerticalAlignment.Center;
            p.Children.Add(ic);

            var sp = new StackPanel { Margin = new Thickness(8, 0, 0, 0) };
            sp.Children.Add(new TextBlock { Text = label, FontSize = 10, Foreground = MutedBrush });
            var vt = new TextBlock { Text = val, FontSize = 12.5, FontWeight = FontWeights.Bold, Foreground = valBrush };
            sp.Children.Add(vt);
            p.Children.Add(sp);
            return (p, vt);
        }

        // ================= 2. ROW 1: CPU, GPU, RAM =================
        var row1 = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // CARD 1: CPU
        var cpuCard = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(8, 15, 28)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(22, 38, 62)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 14, 16, 12)
        };
        var cpuStack = new StackPanel();

        // CPU Head with Premium Blue Vector Badge
        var cHead = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        var cIcoBox = CreateHardwareBadge(NexIcon.Cpu, Color.FromRgb(37, 99, 235), Color.FromRgb(96, 165, 250), 38, 19);
        cHead.Children.Add(cIcoBox);
        var cTitleStack = new StackPanel { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        cTitleStack.Children.Add(new TextBlock { Text = NexLocale.T("hw_cpu_title"), FontSize = 13, FontWeight = FontWeights.Bold, Foreground = TextBrush });
        cTitleStack.Children.Add(new TextBlock { Text = tele.CpuName, FontSize = 10.5, Foreground = CyanBrush, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 190 });
        cHead.Children.Add(cTitleStack);
        cpuStack.Children.Add(cHead);

        // CPU Body
        var cBody = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        cBody.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        cBody.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var (cpuDonutElem, updateCpuDonut) = CreateDynamicMetricDonut(tele.CpuUsagePercent, NexLocale.T("hw_metric_usage"), 94, CyanBrush);
        Grid.SetColumn(cpuDonutElem, 0);
        cBody.Children.Add(cpuDonutElem);

        var cMetrics = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
        var (cRowTemp, cpuTempVal) = BuildDynamicMetricRow(NexIcon.Thermometer, CyanBrush, NexLocale.T("hw_metric_temp"), $"{tele.CpuTempC:0.0} °C", TextBrush);
        var (cRowClock, cpuClockVal) = BuildDynamicMetricRow(NexIcon.Pulse, CyanBrush, NexLocale.T("hw_metric_clock"), $"{tele.CpuClockMhz:0} MHz", TextBrush);
        var (cRowPower, cpuPowerVal) = BuildDynamicMetricRow(NexIcon.Bolt, AmberBrush, NexLocale.T("hw_metric_power"), $"{tele.CpuPowerWatts:0.0} W", TextBrush);
        cMetrics.Children.Add(cRowTemp);
        cMetrics.Children.Add(cRowClock);
        cMetrics.Children.Add(cRowPower);
        Grid.SetColumn(cMetrics, 1);
        cBody.Children.Add(cMetrics);
        cpuStack.Children.Add(cBody);

        // CPU Live Sparkline
        var (cpuSparkElem, pushCpuSparkline) = CreateDynamicSparkline(CyanBrush, 28);
        cpuStack.Children.Add(cpuSparkElem);
        cpuCard.Child = cpuStack;
        Grid.SetColumn(cpuCard, 0);
        row1.Children.Add(cpuCard);

        // CARD 2: GPU
        var gpuCard = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(8, 15, 28)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(22, 38, 62)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 14, 16, 12)
        };
        var gpuStack = new StackPanel();

        // GPU Head with Premium Green Vector Badge
        var gHead = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        var gIcoBox = CreateHardwareBadge(NexIcon.Gpu, Color.FromRgb(16, 185, 129), Color.FromRgb(52, 211, 153), 38, 19);
        gHead.Children.Add(gIcoBox);
        var gTitleStack = new StackPanel { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        gTitleStack.Children.Add(new TextBlock { Text = NexLocale.T("hw_gpu_title"), FontSize = 13, FontWeight = FontWeights.Bold, Foreground = TextBrush });
        gTitleStack.Children.Add(new TextBlock { Text = tele.GpuName, FontSize = 10.5, Foreground = new SolidColorBrush(Color.FromRgb(74, 222, 128)), TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 190 });
        gHead.Children.Add(gTitleStack);
        gpuStack.Children.Add(gHead);

        // GPU Body
        var gBody = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        gBody.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        gBody.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var (gpuDonutElem, updateGpuDonut) = CreateDynamicMetricDonut(tele.GpuUsagePercent, NexLocale.T("hw_metric_usage"), 94, GreenBrush);
        Grid.SetColumn(gpuDonutElem, 0);
        gBody.Children.Add(gpuDonutElem);

        var gMetrics = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
        var (gRowTemp, gpuTempVal) = BuildDynamicMetricRow(NexIcon.Thermometer, GreenBrush, NexLocale.T("hw_metric_temp"), $"{tele.GpuTempC:0.0} °C", TextBrush);
        var (gRowClock, gpuClockVal) = BuildDynamicMetricRow(NexIcon.Pulse, GreenBrush, NexLocale.T("hw_metric_gpu_clock"), $"{tele.GpuClockMhz:0} MHz", TextBrush);
        var (gRowVram, gpuVramVal) = BuildDynamicMetricRow(NexIcon.Memory, CyanBrush, NexLocale.T("hw_metric_vram"), $"{tele.GpuVramUsedGb:0.0} / {tele.GpuVramTotalGb:0.0} GB", TextBrush);
        var (gRowPower, gpuPowerVal) = BuildDynamicMetricRow(NexIcon.Bolt, AmberBrush, NexLocale.T("hw_metric_power_short"), $"{tele.GpuPowerWatts:0.0} W", TextBrush);
        gMetrics.Children.Add(gRowTemp);
        gMetrics.Children.Add(gRowClock);
        gMetrics.Children.Add(gRowVram);
        gMetrics.Children.Add(gRowPower);
        Grid.SetColumn(gMetrics, 1);
        gBody.Children.Add(gMetrics);
        gpuStack.Children.Add(gBody);

        // GPU Live Sparkline
        var (gpuSparkElem, pushGpuSparkline) = CreateDynamicSparkline(GreenBrush, 28);
        gpuStack.Children.Add(gpuSparkElem);
        gpuCard.Child = gpuStack;
        Grid.SetColumn(gpuCard, 2);
        row1.Children.Add(gpuCard);

        // CARD 3: RAM
        var ramCard = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(8, 15, 28)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(22, 38, 62)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 14, 16, 12)
        };
        var rStack = new StackPanel();

        // RAM Head with Premium Purple Vector Badge
        var rHead = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        var rIcoBox = CreateHardwareBadge(NexIcon.Memory, Color.FromRgb(147, 51, 234), Color.FromRgb(192, 132, 252), 38, 19);
        rHead.Children.Add(rIcoBox);
        var rTitleStack = new StackPanel { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        rTitleStack.Children.Add(new TextBlock { Text = NexLocale.T("hw_ram_title"), FontSize = 13, FontWeight = FontWeights.Bold, Foreground = TextBrush });
        rTitleStack.Children.Add(new TextBlock { Text = $"{tele.RamType} @ {tele.RamSpeedMhz} MT/s", FontSize = 10.5, Foreground = PurpleBrush });
        rHead.Children.Add(rTitleStack);
        rStack.Children.Add(rHead);

        // RAM Body
        var rBody = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        rBody.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        rBody.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var (ramDonutElem, updateRamDonut) = CreateDynamicMetricDonut(ramPct, NexLocale.T("hw_metric_usage"), 94, PurpleBrush);
        Grid.SetColumn(ramDonutElem, 0);
        rBody.Children.Add(ramDonutElem);

        var rMetrics = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
        var (rRowUsed, ramUsedVal) = BuildDynamicMetricRow(NexIcon.Folder, CyanBrush, NexLocale.T("hw_metric_used"), $"{tele.RamUsedGb:0.0} GB", TextBrush);
        var (rRowFree, ramFreeVal) = BuildDynamicMetricRow(NexIcon.Window, CyanBrush, NexLocale.T("hw_metric_available"), $"{tele.RamFreeGb:0.0} GB", TextBrush);
        var (rRowTotal, ramTotalVal) = BuildDynamicMetricRow(NexIcon.Memory, CyanBrush, NexLocale.T("hw_metric_capacity"), $"{tele.RamTotalGb:0.0} GB", TextBrush);
        rMetrics.Children.Add(rRowUsed);
        rMetrics.Children.Add(rRowFree);
        rMetrics.Children.Add(rRowTotal);
        Grid.SetColumn(rMetrics, 1);
        rBody.Children.Add(rMetrics);
        rStack.Children.Add(rBody);

        // RAM Live Sparkline
        var (ramSparkElem, pushRamSparkline) = CreateDynamicSparkline(PurpleBrush, 28);
        rStack.Children.Add(ramSparkElem);
        ramCard.Child = rStack;
        Grid.SetColumn(ramCard, 4);
        row1.Children.Add(ramCard);

        PageRoot.Children.Add(row1);

        // ================= 3. ROW 2: SSD, DDR5, SENZORI TERMICI =================
        var row2 = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.15, GridUnitType.Star) });
        row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.95, GridUnitType.Star) });
        row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.9, GridUnitType.Star) });

        // CARD 4: STOCARE SSD / NVMe
        var ssdCard = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(8, 15, 28)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(22, 38, 62)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 12, 14, 12)
        };
        var sStack = new StackPanel();

        var sHead = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
        var sIcoBox = CreateHardwareBadge(NexIcon.Disk, Color.FromRgb(8, 145, 178), Color.FromRgb(56, 189, 248), 34, 17);
        sHead.Children.Add(sIcoBox);
        var sTitleStack = new StackPanel { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        sTitleStack.Children.Add(new TextBlock { Text = NexLocale.T("hw_ssd_title"), FontSize = 12.5, FontWeight = FontWeights.Bold, Foreground = TextBrush });
        sTitleStack.Children.Add(new TextBlock { Text = tele.DiskName, FontSize = 10, Foreground = CyanBrush, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 210 });
        sHead.Children.Add(sTitleStack);
        sStack.Children.Add(sHead);

        var sBody = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        sBody.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
        sBody.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        sBody.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var (diskDonutElem, updateDiskDonut) = CreateDynamicMetricDonut(tele.DiskUsagePercent, NexLocale.T("hw_metric_usage"), 82, CyanBrush);
        Grid.SetColumn(diskDonutElem, 0);
        sBody.Children.Add(diskDonutElem);

        var sMidCol = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 4, 0) };
        var (sRowUsed, diskUsedVal) = BuildDynamicMetricRow(NexIcon.Disk, CyanBrush, NexLocale.T("hw_metric_used_space"), $"{tele.DiskUsedGb:0} GB", TextBrush);
        var (sRowFree, diskFreeVal) = BuildDynamicMetricRow(NexIcon.Pulse, CyanBrush, NexLocale.T("hw_metric_free_space"), $"{tele.DiskFreeGb:0} GB", TextBrush);
        var (sRowTotal, _) = BuildDynamicMetricRow(NexIcon.Bolt, AmberBrush, NexLocale.T("hw_metric_total"), $"{tele.DiskTotalGb / 1000.0:0} TB", TextBrush);
        sMidCol.Children.Add(sRowUsed);
        sMidCol.Children.Add(sRowFree);
        sMidCol.Children.Add(sRowTotal);
        Grid.SetColumn(sMidCol, 1);
        sBody.Children.Add(sMidCol);

        var sRightCol = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) };
        var (sRowTemp, diskTempVal) = BuildDynamicMetricRow(NexIcon.Thermometer, CyanBrush, NexLocale.T("hw_metric_temp"), $"{tele.DiskTempC:0.0} °C", TextBrush);
        var (sRowSmart, _) = BuildDynamicMetricRow(NexIcon.Check, GreenBrush, NexLocale.T("hw_metric_smart"), NexLocale.Format("hw_metric_smart_val", tele.DiskHealthPercent), new SolidColorBrush(Color.FromRgb(52, 211, 153)));
        var (sRowHours, _) = BuildDynamicMetricRow(NexIcon.Window, CyanBrush, NexLocale.T("hw_metric_hours"), NexLocale.Format("hw_metric_hours_val", $"{tele.DiskPowerOnHours:N0}"), TextBrush);
        sRightCol.Children.Add(sRowTemp);
        sRightCol.Children.Add(sRowSmart);
        sRightCol.Children.Add(sRowHours);
        Grid.SetColumn(sRightCol, 2);
        sBody.Children.Add(sRightCol);
        sStack.Children.Add(sBody);

        var sFoot = new StackPanel();
        var diskFootTxt = new TextBlock { Text = $"C: {tele.DiskUsedGb:0} GB / {tele.DiskTotalGb / 1000.0:0} TB", FontSize = 10, Foreground = MutedBrush, Margin = new Thickness(0, 0, 0, 4) };
        sFoot.Children.Add(diskFootTxt);
        var diskBar = new ProgressBar
        {
            Value = tele.DiskUsagePercent,
            Maximum = 100,
            Height = 6,
            Background = new SolidColorBrush(Color.FromRgb(15, 24, 38)),
            Foreground = new LinearGradientBrush(Color.FromRgb(6, 182, 212), Color.FromRgb(34, 197, 94), new Point(0, 0), new Point(1, 0)),
            BorderThickness = new Thickness(0)
        };
        sFoot.Children.Add(diskBar);
        sStack.Children.Add(sFoot);

        ssdCard.Child = sStack;
        Grid.SetColumn(ssdCard, 0);
        row2.Children.Add(ssdCard);

        // CARD 5: MEMORIE DDR5
        var ddrCard = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(8, 15, 28)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(22, 38, 62)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 12, 14, 12)
        };
        var ddrStack = new StackPanel();

        var ddrHead = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
        var ddrIcoBox = CreateHardwareBadge(NexIcon.Memory, Color.FromRgb(124, 58, 237), Color.FromRgb(167, 139, 250), 34, 17);
        ddrHead.Children.Add(ddrIcoBox);
        var ddrTitleStack = new StackPanel { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        ddrTitleStack.Children.Add(new TextBlock { Text = NexLocale.T("hw_ddr5_title"), FontSize = 12.5, FontWeight = FontWeights.Bold, Foreground = TextBrush });
        ddrTitleStack.Children.Add(new TextBlock { Text = tele.RamChannels, FontSize = 10, Foreground = PurpleBrush });
        ddrHead.Children.Add(ddrTitleStack);
        ddrStack.Children.Add(ddrHead);

        var ddrGrid = new Grid { Margin = new Thickness(0, 4, 0, 10) };
        ddrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ddrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ddrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ddrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.1, GridUnitType.Star) });

        Border BuildBox(string l, string v)
        {
            var b = new Border { Background = new SolidColorBrush(Color.FromRgb(12, 20, 36)), CornerRadius = new CornerRadius(6), Padding = new Thickness(6, 6, 6, 6), Margin = new Thickness(0, 0, 4, 0) };
            var st = new StackPanel();
            st.Children.Add(new TextBlock { Text = l, FontSize = 8.5, Foreground = MutedBrush });
            st.Children.Add(new TextBlock { Text = v, FontSize = 12, FontWeight = FontWeights.Bold, Foreground = TextBrush, Margin = new Thickness(0, 2, 0, 0) });
            b.Child = st;
            return b;
        }

        var b1 = BuildBox(NexLocale.T("hw_metric_speed"), $"{tele.RamSpeedMhz} MT/s"); Grid.SetColumn(b1, 0); ddrGrid.Children.Add(b1);
        var b2 = BuildBox(NexLocale.T("hw_metric_latency"), $"{tele.RamLatencyCl}"); Grid.SetColumn(b2, 1); ddrGrid.Children.Add(b2);
        var b3 = BuildBox(NexLocale.T("hw_metric_channel"), "2 / 2"); Grid.SetColumn(b3, 2); ddrGrid.Children.Add(b3);

        var bVolt = new Border { Background = new SolidColorBrush(Color.FromRgb(16, 28, 48)), BorderBrush = new SolidColorBrush(Color.FromRgb(30, 48, 80)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(6, 6, 6, 6) };
        var stV = new StackPanel();
        stV.Children.Add(new TextBlock { Text = NexLocale.T("hw_metric_voltage"), FontSize = 8.5, Foreground = CyanBrush });
        stV.Children.Add(new TextBlock { Text = $"{tele.RamVoltageV:0.00} V", FontSize = 12.5, FontWeight = FontWeights.Bold, Foreground = CyanBrush, Margin = new Thickness(0, 2, 0, 0) });
        bVolt.Child = stV;
        Grid.SetColumn(bVolt, 3);
        ddrGrid.Children.Add(bVolt);

        ddrStack.Children.Add(ddrGrid);
        var (ddrSparkElem, pushDdrSparkline) = CreateDynamicSparkline(PurpleBrush, 24);
        ddrStack.Children.Add(ddrSparkElem);
        ddrCard.Child = ddrStack;
        Grid.SetColumn(ddrCard, 2);
        row2.Children.Add(ddrCard);

        // CARD 6: SENZORI TERMICI IN TIMP REAL
        var tempCard = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(8, 15, 28)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(22, 38, 62)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 12, 14, 12)
        };
        var tStack = new StackPanel();

        var tHead = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
        var tIcoBox = CreateHardwareBadge(NexIcon.Thermometer, Color.FromRgb(234, 88, 12), Color.FromRgb(251, 146, 60), 34, 17);
        tHead.Children.Add(tIcoBox);
        var tTitleStack = new StackPanel { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        tTitleStack.Children.Add(new TextBlock { Text = NexLocale.T("hw_thermal_title"), FontSize = 12.5, FontWeight = FontWeights.Bold, Foreground = TextBrush });
        tTitleStack.Children.Add(new TextBlock { Text = NexLocale.T("hw_thermal_sub"), FontSize = 10, Foreground = AmberBrush });
        tHead.Children.Add(tTitleStack);
        tStack.Children.Add(tHead);

        (Grid panel, ProgressBar pb, TextBlock valTxt) BuildDynamicTempBar(NexIcon ico, Brush icoCol, string label, double degC, Brush barBrush)
        {
            var p = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            p.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
            p.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(82) });
            p.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            p.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(45) });

            var ic = CreateVectorIcon(ico, icoCol, 12);
            ic.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(ic, 0); p.Children.Add(ic);

            var lbl = new TextBlock { Text = label, FontSize = 10, Foreground = MutedBrush, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) };
            Grid.SetColumn(lbl, 1); p.Children.Add(lbl);

            var b = new ProgressBar
            {
                Value = degC,
                Maximum = 100,
                Height = 6,
                Background = new SolidColorBrush(Color.FromRgb(15, 24, 38)),
                Foreground = barBrush,
                BorderThickness = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 8, 0)
            };
            Grid.SetColumn(b, 2); p.Children.Add(b);

            var val = new TextBlock { Text = $"{degC:0}°C", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = TextBrush, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetColumn(val, 3); p.Children.Add(val);

            return (p, b, val);
        }

        var (tRowCpu, tempCpuBar, tempCpuVal) = BuildDynamicTempBar(NexIcon.Cpu, CyanBrush, NexLocale.T("hw_sensor_cpu"), tele.CpuTempC, CyanBrush);
        var (tRowGpu, tempGpuBar, tempGpuVal) = BuildDynamicTempBar(NexIcon.Gpu, GreenBrush, NexLocale.T("hw_sensor_gpu"), tele.GpuTempC, CyanBrush);
        var (tRowDisk, tempDiskBar, tempDiskVal) = BuildDynamicTempBar(NexIcon.Disk, CyanBrush, NexLocale.T("hw_sensor_ssd"), tele.DiskTempC, CyanBrush);
        var (tRowMobo, tempMoboBar, tempMoboVal) = BuildDynamicTempBar(NexIcon.Motherboard, CyanBrush, NexLocale.T("hw_sensor_mobo"), tele.MotherboardTempC, CyanBrush);
        var (tRowVrm, tempVrmBar, tempVrmVal) = BuildDynamicTempBar(NexIcon.Chip, AmberBrush, NexLocale.T("hw_sensor_vrm"), tele.VrmTempC, GreenBrush);

        tStack.Children.Add(tRowCpu);
        tStack.Children.Add(tRowGpu);
        tStack.Children.Add(tRowDisk);
        tStack.Children.Add(tRowMobo);
        tStack.Children.Add(tRowVrm);

        tempCard.Child = tStack;
        Grid.SetColumn(tempCard, 4);
        row2.Children.Add(tempCard);

        PageRoot.Children.Add(row2);

        // ================= 4. ROW 3: SPECIFICATII SISTEM & ARHITECTURA =================
        var row3 = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        row3.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // SPECIFICATII SISTEM & ARHITECTURA LIVE (Full Width)
        var specCard = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(8, 15, 28)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(22, 38, 62)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 14, 16, 14)
        };
        var specStack = new StackPanel();

        var specHead = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        specHead.Children.Add(CreateVectorIcon(NexIcon.Bolt, AmberBrush, 15));
        specHead.Children.Add(new TextBlock { Text = "  " + NexLocale.T("hw_spec_title"), FontSize = 13, FontWeight = FontWeights.Bold, Foreground = TextBrush, VerticalAlignment = VerticalAlignment.Center });
        specStack.Children.Add(specHead);

        var specGrid = new Grid { Margin = new Thickness(0, 4, 0, 0) };
        specGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        specGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        specGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        specGrid.RowDefinitions.Add(new RowDefinition());
        specGrid.RowDefinitions.Add(new RowDefinition());

        Border BuildSpecTile(string label, string val, Brush valBrush)
        {
            var b = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(11, 20, 34)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(20, 36, 56)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 8, 8)
            };
            var st = new StackPanel();
            st.Children.Add(new TextBlock { Text = label, FontSize = 9.5, Foreground = MutedBrush });
            st.Children.Add(new TextBlock { Text = val, FontSize = 11.5, FontWeight = FontWeights.SemiBold, Foreground = valBrush, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 2, 0, 0) });
            b.Child = st;
            return b;
        }

        var t1 = BuildSpecTile(NexLocale.T("hw_spec_cpu"), tele.CpuName, TextBrush); Grid.SetRow(t1, 0); Grid.SetColumn(t1, 0); specGrid.Children.Add(t1);
        var t2 = BuildSpecTile(NexLocale.T("hw_spec_gpu"), tele.GpuName, GreenBrush); Grid.SetRow(t2, 0); Grid.SetColumn(t2, 1); specGrid.Children.Add(t2);
        var t3 = BuildSpecTile(NexLocale.T("hw_spec_storage"), tele.DiskName, CyanBrush); Grid.SetRow(t3, 0); Grid.SetColumn(t3, 2); specGrid.Children.Add(t3);
        var t4 = BuildSpecTile(NexLocale.T("hw_spec_nvme_bus"), "PCIe Gen 4.0 x4 (NVMe 1.4)", PurpleBrush); Grid.SetRow(t4, 1); Grid.SetColumn(t4, 0); specGrid.Children.Add(t4);
        var t5 = BuildSpecTile(NexLocale.T("hw_spec_ram_channels"), "Dual Channel (128-bit) DDR5", PurpleBrush); Grid.SetRow(t5, 1); Grid.SetColumn(t5, 1); specGrid.Children.Add(t5);
        var t6 = BuildSpecTile(NexLocale.T("hw_spec_throttling"), NexLocale.T("hw_spec_throttling_val"), GreenBrush); Grid.SetRow(t6, 1); Grid.SetColumn(t6, 2); specGrid.Children.Add(t6);

        specStack.Children.Add(specGrid);
        specCard.Child = specStack;
        Grid.SetColumn(specCard, 0);
        row3.Children.Add(specCard);

        PageRoot.Children.Add(row3);

        // ================= 5. WIRE REAL-TIME LIVE UPDATE TICK =================
        void PerformLiveUpdate()
        {
            var live = NativeTuning.GetHardwareTelemetry(lastCpuUsage);
            if (!string.IsNullOrEmpty(detectedCpuName) && detectedCpuName != "Necunoscut") live.CpuName = detectedCpuName;
            if (!string.IsNullOrEmpty(detectedGpuName) && detectedGpuName != "Necunoscut") live.GpuName = detectedGpuName;

            var mem = new MEMORYSTATUSEX();
            if (GlobalMemoryStatusEx(mem))
            {
                live.RamTotalGb = mem.ullTotalPhys / (1024.0 * 1024.0 * 1024.0);
                live.RamUsedGb = (mem.ullTotalPhys - mem.ullAvailPhys) / (1024.0 * 1024.0 * 1024.0);
                live.RamFreeGb = Math.Max(0, live.RamTotalGb - live.RamUsedGb);
            }
            double liveRamPct = live.RamTotalGb > 0 ? (live.RamUsedGb / live.RamTotalGb * 100.0) : 44.0;

            // Update CPU
            updateCpuDonut(live.CpuUsagePercent);
            pushCpuSparkline(live.CpuUsagePercent);
            cpuTempVal.Text = $"{live.CpuTempC:0.0} °C";
            cpuClockVal.Text = $"{live.CpuClockMhz:0} MHz";
            cpuPowerVal.Text = $"{live.CpuPowerWatts:0.0} W";

            // Update GPU
            updateGpuDonut(live.GpuUsagePercent);
            pushGpuSparkline(live.GpuUsagePercent);
            gpuTempVal.Text = $"{live.GpuTempC:0.0} °C";
            gpuClockVal.Text = $"{live.GpuClockMhz:0} MHz";
            gpuVramVal.Text = $"{live.GpuVramUsedGb:0.0} / {live.GpuVramTotalGb:0.0} GB";
            gpuPowerVal.Text = $"{live.GpuPowerWatts:0.0} W";

            // Update RAM
            updateRamDonut(liveRamPct);
            pushRamSparkline(liveRamPct);
            pushDdrSparkline(liveRamPct);
            ramUsedVal.Text = $"{live.RamUsedGb:0.0} GB";
            ramFreeVal.Text = $"{live.RamFreeGb:0.0} GB";
            ramTotalVal.Text = $"{live.RamTotalGb:0.0} GB";

            // Update Disk
            updateDiskDonut(live.DiskUsagePercent);
            diskUsedVal.Text = $"{live.DiskUsedGb:0} GB";
            diskFreeVal.Text = $"{live.DiskFreeGb:0} GB";
            diskTempVal.Text = $"{live.DiskTempC:0.0} °C";
            diskBar.Value = live.DiskUsagePercent;
            diskFootTxt.Text = $"C: {live.DiskUsedGb:0} GB / {live.DiskTotalGb / 1000.0:0} TB";

            // Update Temperatures
            tempCpuBar.Value = live.CpuTempC;
            tempCpuVal.Text = $"{live.CpuTempC:0}°C";
            tempGpuBar.Value = live.GpuTempC;
            tempGpuVal.Text = $"{live.GpuTempC:0}°C";
            tempDiskBar.Value = live.DiskTempC;
            tempDiskVal.Text = $"{live.DiskTempC:0}°C";
            tempMoboBar.Value = live.MotherboardTempC;
            tempMoboVal.Text = $"{live.MotherboardTempC:0}°C";
            tempVrmBar.Value = live.VrmTempC;
            tempVrmVal.Text = $"{live.VrmTempC:0}°C";
        }

        activePageTick = PerformLiveUpdate;
        PerformLiveUpdate();
    }


    private void ShowRam()
    {
        PreparePage(NexLocale.T("ram_page_title"), NexLocale.T("ram_page_sub"));

        // Architecture info card
        var archBorder = new Border
        {
            Background = CardBackground(),
            BorderBrush = new SolidColorBrush(Color.FromRgb(24, 36, 52)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(16, 14, 16, 14),
            Margin = new Thickness(0, 0, 0, 16)
        };

        var archStack = new StackPanel();
        archStack.Children.Add(new TextBlock
        {
            Text = NexLocale.T("ram_arch_title"),
            FontSize = 13.5,
            FontWeight = FontWeights.Bold,
            Foreground = TextBrush
        });
        archStack.Children.Add(new TextBlock
        {
            Text = NexLocale.T("ram_arch_desc"),
            FontSize = 11.5,
            Foreground = MutedBrush,
            Margin = new Thickness(0, 6, 0, 0),
            LineHeight = 17
        });
        archBorder.Child = archStack;
        PageRoot.Children.Add(archBorder);

        // RAM Cleaner Action
        AddActionRow(
            NexLocale.T("ram_clean_title"),
            NexLocale.T("ram_clean_desc"),
            NexIcon.Cpu, NexLocale.T("ram_clean_badge"), CyanBrush, NexLocale.T("ram_clean_chips"),
            ActionBtn(NexLocale.T("ram_clean_btn"), NexIcon.Clean, CyanBrush, () => {
                long freed = NativeTuning.TrimAllWorkingSets();
                AppendLog(NexLocale.Format("ram_clean_log", freed), false);
                ShowToast(NexLocale.T("ram_clean_toast_title"), NexLocale.Format("ram_clean_toast_msg", freed), NexIcon.Check, GreenBrush);
                ShowRam();
                return Task.CompletedTask;
            })
        );

        // Power Throttling
        var ptVal = NativeTuning.GetRegistryDword(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Power\PowerThrottling", "PowerThrottlingOff");
        bool ptOff = ptVal == 1;

        AddActionRow(
            NexLocale.T("ram_throttle_title"),
            NexLocale.T("ram_throttle_desc"),
            NexIcon.Power, ptOff ? NexLocale.T("ram_throttle_disabled") : NexLocale.T("ram_throttle_enabled"), ptOff ? GreenBrush : AmberBrush, AmberBrush,
            @"HKLM\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling\PowerThrottlingOff = 1",
            ptOff
                ? ActionBtn(NexLocale.T("ram_throttle_btn_revert"), NexIcon.Power, RedBrush, async () => {
                    await ExecuteNativeSuiteAsync(NexLocale.T("hw_power_throttle_title", "Power Throttling"), NexLocale.T("hw_power_throttle_reset_working", "Resetare..."), NexLocale.T("hw_power_throttle_reset_done", "Power Throttling a fost resetat."), () => Task.FromResult(new List<string> {
                        NativeTuning.SetRegistryDword(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Power\PowerThrottling", "PowerThrottlingOff", 0) ? NexLocale.T("hw_power_throttle_reset_log", "Power Throttling resetat la valorile implicite.") : ""
                    }));
                    ShowRam();
                })
                : ActionBtn(NexLocale.T("ram_throttle_btn_disable"), NexIcon.Power, GreenBrush, async () => {
                    await ExecuteNativeSuiteAsync(NexLocale.T("hw_power_throttle_title", "Power Throttling"), NexLocale.T("hw_power_throttle_disable_working", "Dezactivare..."), NexLocale.T("hw_power_throttle_disable_done", "Power Throttling a fost oprit."), () => Task.FromResult(new List<string> {
                        NativeTuning.SetRegistryDword(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Power\PowerThrottling", "PowerThrottlingOff", 1) ? NexLocale.T("hw_power_throttle_disable_log", "Power Throttling dezactivat cu succes.") : ""
                    }));
                    ShowRam();
                })
        );

        // Top Memory Consuming Processes Card Header
        var topHeader = new TextBlock
        {
            Text = NexLocale.T("ram_top_header"),
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(100, 120, 145)),
            Margin = new Thickness(4, 14, 0, 8)
        };
        PageRoot.Children.Add(topHeader);

        var topProcs = NativeTuning.GetTopMemoryProcesses(8);
        foreach (var p in topProcs)
        {
            AddActionRow(
                p.Name,
                NexLocale.Format("ram_proc_desc_format", p.Id),
                NexIcon.Cpu, $"{p.MemoryMB} MB", CyanBrush, NexLocale.Format("ram_proc_chip_format", p.MemoryMB),
                ActionBtn(NexLocale.T("ram_proc_btn_free"), NexIcon.Clean, CyanBrush, () => {
                    bool ok = NativeTuning.TrimProcessMemory(p.Id);
                    if (ok)
                    {
                        AppendLog(NexLocale.Format("hw_ram_released_format", p.Name, p.Id), false);
                        ShowToast(NexLocale.T("ram_proc_toast_title"), NexLocale.Format("ram_proc_toast_msg", p.Name), NexIcon.Check, GreenBrush);
                    }
                    ShowRam();
                    return Task.CompletedTask;
                }, false)
            );
        }
    }

}