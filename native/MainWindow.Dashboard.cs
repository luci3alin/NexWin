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
    private void ShowDashboard()
    {
        PageRoot.Children.Clear();

        // 1. Gather Real Hardware Telemetry
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

        var (tempBytes, tempFiles) = NativeTuning.GetTempAndCacheSize();
        double tempGb = tempBytes / (1024.0 * 1024.0 * 1024.0);
        var startupApps = NativeTuning.GetStartupAppsNative();
        int startupCount = startupApps.Count;

        // ================= 5-FACTOR DYNAMIC HEALTH SCORE ALGORITHM (REAL MEASUREMENTS) =================
        // Factor 1: Stocare Reziduala & Junk Temp (max 20 pts)
        int storageScore = tempGb switch
        {
            < 0.5 => 20,
            < 2.0 => 17,
            < 5.0 => 14,
            < 15.0 => 10,
            < 30.0 => 6,
            _ => 3
        };

        // Factor 2: Aplicatii de Pornire la Conectare (max 20 pts)
        int startupScore = startupCount switch
        {
            <= 3 => 20,
            <= 6 => 16,
            <= 10 => 12,
            <= 15 => 8,
            _ => 4
        };

        // Factor 3: Sanatate Disc SMART & Spatiu Liber (max 20 pts)
        double diskFreePercent = tele.DiskTotalGb > 0 ? (tele.DiskFreeGb / tele.DiskTotalGb * 100.0) : 50.0;
        int diskSmartScore = (tele.DiskHealthPercent, diskFreePercent) switch
        {
            ( >= 95, >= 20.0) => 20,
            ( >= 90, >= 15.0) => 17,
            ( >= 80, >= 10.0) => 13,
            _ => 9
        };

        // Factor 4: Temperaturi & Termice Hardware (max 20 pts)
        double maxTemp = Math.Max(tele.CpuTempC, tele.GpuTempC);
        int thermalScore = maxTemp switch
        {
            < 55.0 => 20,
            < 68.0 => 17,
            < 78.0 => 13,
            < 85.0 => 8,
            _ => 4
        };

        // Factor 5: Configuratie Windows & Securitate (max 20 pts)
        int configScore = 20;
        bool gameModeOn = (NativeTuning.GetRegistryDword(RegistryHive.CurrentUser, @"Software\Microsoft\GameBar", "AutoGameModeEnabled") ?? 1) == 1;
        if (!gameModeOn) configScore -= 3;
        bool uacOn = (NativeTuning.GetRegistryDword(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "EnableLUA") ?? 1) == 1;
        if (!uacOn) configScore -= 4;

        int healthScore = Math.Clamp(storageScore + startupScore + diskSmartScore + thermalScore + configScore, 10, 100);
        Brush healthColor = healthScore >= 85 ? GreenBrush : (healthScore >= 70 ? AmberBrush : PinkBrush);
        string healthStatusText = healthScore >= 88 ? NexLocale.T("dash_health_opt_stable") :
                                 (healthScore >= 72 ? NexLocale.T("dash_health_good") : NexLocale.T("dash_health_rec"));

        // ================= ZONE 1. TOP HERO BANNER: IDENTITY + HARDWARE MINI-METERS + HEALTH SCORE =================
        var heroBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(9, 16, 27)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(24, 40, 62)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(18, 14, 18, 14),
            Margin = new Thickness(0, 0, 0, 14)
        };

        var heroGrid = new Grid();
        heroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.05, GridUnitType.Star) }); // Left: Identity
        heroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) }); // Spacer
        heroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.3, GridUnitType.Star) });  // Center: Mini-meters
        heroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) }); // Spacer
        heroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });                           // Right: Health Donut

        // --- Column 0: Left Welcome Identity ---
        var heroLeft = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var heroBadgeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };
        var heroBadge = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(32, 56, 189, 248)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(65, 56, 189, 248)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(7, 2, 7, 2),
            Margin = new Thickness(0, 0, 8, 0),
            Child = new TextBlock { Text = NexLocale.T("dash_hero_badge"), FontSize = 9, FontWeight = FontWeights.Bold, Foreground = CyanBrush }
        };
        heroBadgeRow.Children.Add(heroBadge);
        heroLeft.Children.Add(heroBadgeRow);

        heroLeft.Children.Add(new TextBlock
        {
            Text = NexLocale.T("dash_welcome_title"),
            FontSize = 21,
            FontWeight = FontWeights.Bold,
            Foreground = TextBrush
        });
        heroLeft.Children.Add(new TextBlock
        {
            Text = NexLocale.T("dash_welcome_sub"),
            FontSize = 11,
            Foreground = MutedBrush,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 0, 8)
        });

        var heroStatusRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var livePulseDot = new Ellipse { Width = 7, Height = 7, Fill = GreenBrush, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
        heroStatusRow.Children.Add(livePulseDot);
        heroStatusRow.Children.Add(new TextBlock { Text = NexLocale.T("dash_monitoring_status"), FontSize = 10, Foreground = GreenBrush, VerticalAlignment = VerticalAlignment.Center });
        heroLeft.Children.Add(heroStatusRow);

        Grid.SetColumn(heroLeft, 0);
        heroGrid.Children.Add(heroLeft);

        // --- Column 2: Center Compact Hardware Mini-Meters (2x2 Grid) ---
        (Border card, TextBlock valText, TextBlock subText, ProgressBar bar) BuildCompactMiniMeter(
            NexIcon icon, Brush iconBrush, string title,
            string initialVal, Brush valBrush,
            double initialPct, string initialSub)
        {
            var b = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(12, 21, 34)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(24, 40, 62)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(9, 7, 9, 7)
            };
            var sp = new StackPanel();

            var topGrid = new Grid();
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var ico = CreateVectorIcon(icon, iconBrush, 12);
            ico.VerticalAlignment = VerticalAlignment.Center;
            ico.Margin = new Thickness(0, 0, 5, 0);
            Grid.SetColumn(ico, 0); topGrid.Children.Add(ico);

            var lbl = new TextBlock
            {
                Text = title,
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = TextBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(lbl, 1); topGrid.Children.Add(lbl);

            var val = new TextBlock
            {
                Text = initialVal,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = valBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(val, 2); topGrid.Children.Add(val);
            sp.Children.Add(topGrid);

            var pb = new ProgressBar
            {
                Value = Math.Clamp(initialPct, 0, 100),
                Maximum = 100,
                Height = 3.5,
                Background = new SolidColorBrush(Color.FromRgb(19, 31, 48)),
                Foreground = valBrush,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 5, 0, 4)
            };
            sp.Children.Add(pb);

            var sub = new TextBlock
            {
                Text = initialSub,
                FontSize = 9.5,
                Foreground = MutedBrush,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            sp.Children.Add(sub);

            b.Child = sp;
            return (b, val, sub, pb);
        }

        var miniMetersBox = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(10, 18, 29)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(22, 38, 58)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8, 7, 8, 7),
            VerticalAlignment = VerticalAlignment.Center
        };

        var miniGrid = new Grid();
        miniGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        miniGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(6) });
        miniGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        miniGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        miniGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        miniGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Meter 1: CPU
        var (cpuMiniCard, cpuMiniVal, cpuMiniSub, cpuMiniBar) = BuildCompactMiniMeter(
            NexIcon.Cpu, CyanBrush, NexLocale.T("dash_cpu"),
            $"{tele.CpuUsagePercent:0}%", CyanBrush,
            tele.CpuUsagePercent, $"{tele.CpuClockMhz:0} MHz - {tele.CpuTempC:0.0} °C");
        Grid.SetRow(cpuMiniCard, 0); Grid.SetColumn(cpuMiniCard, 0); miniGrid.Children.Add(cpuMiniCard);

        // Meter 2: GPU
        var (gpuMiniCard, gpuMiniVal, gpuMiniSub, gpuMiniBar) = BuildCompactMiniMeter(
            NexIcon.Gpu, GreenBrush, NexLocale.T("dash_gpu"),
            $"{tele.GpuUsagePercent:0}%", GreenBrush,
            tele.GpuUsagePercent, $"VRAM {tele.GpuVramUsedGb:0.0}/{tele.GpuVramTotalGb:0.0} GB - {tele.GpuTempC:0.0} °C");
        Grid.SetRow(gpuMiniCard, 0); Grid.SetColumn(gpuMiniCard, 2); miniGrid.Children.Add(gpuMiniCard);

        // Meter 3: RAM
        var (ramMiniCard, ramMiniVal, ramMiniSub, ramMiniBar) = BuildCompactMiniMeter(
            NexIcon.Memory, PurpleBrush, NexLocale.T("dash_ram"),
            $"{ramPct:0}%", PurpleBrush,
            ramPct, $"{tele.RamUsedGb:0.0} / {tele.RamTotalGb:0.0} GB ({tele.RamSpeedMhz} MT/s)");
        Grid.SetRow(ramMiniCard, 2); Grid.SetColumn(ramMiniCard, 0); miniGrid.Children.Add(ramMiniCard);

        // Meter 4: SSD
        var (diskMiniCard, diskMiniVal, diskMiniSub, diskMiniBar) = BuildCompactMiniMeter(
            NexIcon.Disk, AmberBrush, NexLocale.T("dash_storage"),
            $"{tele.DiskUsagePercent:0}%", AmberBrush,
            tele.DiskUsagePercent, $"{tele.DiskFreeGb:0} GB {NexLocale.T("dash_free_label")} ({diskFreePercent:0}%)");
        Grid.SetRow(diskMiniCard, 2); Grid.SetColumn(diskMiniCard, 2); miniGrid.Children.Add(diskMiniCard);

        miniMetersBox.Child = miniGrid;
        Grid.SetColumn(miniMetersBox, 2);
        heroGrid.Children.Add(miniMetersBox);

        // --- Column 4: Right Health Score Circular Donut + Details ---
        var heroRight = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(12, 22, 36)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(26, 46, 72)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 8, 14, 8),
            VerticalAlignment = VerticalAlignment.Center
        };
        var scoreRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        scoreRow.Children.Add(CreateMetricDonut(healthScore, NexLocale.T("dash_health_donut_label"), 72, healthColor));

        var scoreTextStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
        scoreTextStack.Children.Add(new TextBlock { Text = NexLocale.T("dash_health_score"), FontSize = 12.5, FontWeight = FontWeights.Bold, Foreground = TextBrush });
        scoreTextStack.Children.Add(new TextBlock { Text = healthStatusText, FontSize = 10, Foreground = healthColor, Margin = new Thickness(0, 1, 0, 4) });

        var reportBtn = new Button
        {
            Content = NexLocale.T("dash_detailed_report"),
            Style = (Style)FindResource("DarkTableFooterButtonStyle"),
            Padding = new Thickness(8, 3, 8, 3),
            FontSize = 10.5,
            Cursor = Cursors.Hand
        };
        reportBtn.Click += (_, _) =>
        {
            ShowOptimizationReportModal(new OptimizationReport(NexLocale.T("dash_diag_report_title"), DateTime.Now, 120, true, new List<OptimizationStep>
            {
                new(NexLocale.T("dash_diag_step1_title"), NexLocale.Format("dash_diag_step1_desc", storageScore, tempGb, tempFiles), storageScore >= 14),
                new(NexLocale.T("dash_diag_step2_title"), NexLocale.Format("dash_diag_step2_desc", startupScore, startupCount), startupScore >= 14),
                new(NexLocale.T("dash_diag_step3_title"), NexLocale.Format("dash_diag_step3_desc", diskSmartScore, tele.DiskHealthPercent, tele.DiskFreeGb, diskFreePercent), diskSmartScore >= 14),
                new(NexLocale.T("dash_diag_step4_title"), NexLocale.Format("dash_diag_step4_desc", thermalScore, maxTemp, tele.CpuTempC, tele.GpuTempC), thermalScore >= 14),
                new(NexLocale.T("dash_diag_step5_title"), NexLocale.Format("dash_diag_step5_desc", configScore, (gameModeOn ? NexLocale.T("status_enabled") : NexLocale.T("status_disabled")), (uacOn ? NexLocale.T("status_enabled") : NexLocale.T("status_disabled"))), configScore >= 14)
            }));
        };
        scoreTextStack.Children.Add(reportBtn);

        scoreRow.Children.Add(scoreTextStack);
        heroRight.Child = scoreRow;
        Grid.SetColumn(heroRight, 4);
        heroGrid.Children.Add(heroRight);

        heroBorder.Child = heroGrid;
        PageRoot.Children.Add(heroBorder);

        // ================= ZONE 2. ACTION CENTER: ONE-CLICK BOOST + REAL RECOMMENDATIONS =================
        var midGrid = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        midGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.05, GridUnitType.Star) });
        midGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        midGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });

        // Left Card: One-Click Boost Card
        var boostCard = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(9, 17, 30)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(26, 48, 76)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 14, 16, 14)
        };
        var boostStack = new StackPanel();

        var boostHead = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        boostHead.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
        boostHead.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        boostHead.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var boostIcoBox = CreateHardwareBadge(NexIcon.Bolt, Color.FromRgb(255, 42, 133), Color.FromRgb(251, 113, 133), 36, 18);
        Grid.SetColumn(boostIcoBox, 0);
        boostHead.Children.Add(boostIcoBox);

        var boostTitles = new StackPanel { Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        boostTitles.Children.Add(new TextBlock { Text = NexLocale.T("dash_boost_title"), FontSize = 14, FontWeight = FontWeights.Bold, Foreground = TextBrush });
        boostTitles.Children.Add(new TextBlock { Text = NexLocale.T("dash_boost_sub"), FontSize = 10.5, Foreground = MutedBrush });
        Grid.SetColumn(boostTitles, 1);
        boostHead.Children.Add(boostTitles);

        var boostRecPill = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(32, 255, 42, 133)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(70, 255, 42, 133)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(7, 2, 7, 2),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock { Text = NexLocale.T("status_recommended").ToUpperInvariant(), FontSize = 9, FontWeight = FontWeights.Bold, Foreground = PinkBrush }
        };
        Grid.SetColumn(boostRecPill, 2);
        boostHead.Children.Add(boostRecPill);
        boostStack.Children.Add(boostHead);

        boostStack.Children.Add(new TextBlock
        {
            Text = NexLocale.T("dash_boost_desc"),
            FontSize = 11,
            Foreground = MutedBrush,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        });

        // Checklist Items
        UIElement BuildCheckItem(string text)
        {
            var p = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 7) };
            p.Children.Add(CreateVectorIcon(NexIcon.Check, GreenBrush, 13));
            p.Children.Add(new TextBlock { Text = " " + text, FontSize = 11, Foreground = TextBrush, Margin = new Thickness(4, 0, 0, 0) });
            return p;
        }

        boostStack.Children.Add(BuildCheckItem(NexLocale.T("dash_boost_feat_restore")));
        boostStack.Children.Add(BuildCheckItem(NexLocale.T("dash_boost_feat_cache")));
        boostStack.Children.Add(BuildCheckItem(NexLocale.T("dash_boost_feat_reg")));
        boostStack.Children.Add(BuildCheckItem(NexLocale.T("dash_boost_feat_tele")));

        var boostBtnRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };

        var optNowBtn = new Button
        {
            Style = (Style)FindResource("PrimaryGradientButtonStyle"),
            Padding = new Thickness(16, 7, 16, 7),
            Margin = new Thickness(0, 0, 8, 0),
            Cursor = Cursors.Hand
        };
        var optBtnContent = new StackPanel { Orientation = Orientation.Horizontal };
        var boltIco = CreateVectorIcon(NexIcon.Bolt, Brushes.White, 13);
        boltIco.Margin = new Thickness(0, 0, 6, 0);
        optBtnContent.Children.Add(boltIco);
        optBtnContent.Children.Add(new TextBlock { Text = NexLocale.T("dash_boost_button"), FontSize = 12, FontWeight = FontWeights.Bold, Foreground = Brushes.White });
        optNowBtn.Content = optBtnContent;
        optNowBtn.Click += async (_, _) =>
        {
            var confirmed = await ShowConfirmModalAsync(
                NexLocale.T("dash_confirm_boost_title"),
                NexLocale.T("dash_confirm_boost_msg"),
                PinkBrush, NexLocale.T("btn_continue"), NexLocale.T("btn_cancel"));
            if (confirmed)
            {
                await RunOneClickBoostAsync(true, true, true, true, true);
            }
        };
        boostBtnRow.Children.Add(optNowBtn);

        var planBtn = new Button
        {
            Content = NexLocale.T("dash_view_plan"),
            Style = (Style)FindResource("SecondaryButtonStyle"),
            Padding = new Thickness(12, 7, 12, 7),
            Cursor = Cursors.Hand
        };
        planBtn.Click += (_, _) => ShowOneClickBoostModal();
        boostBtnRow.Children.Add(planBtn);

        boostStack.Children.Add(boostBtnRow);
        boostCard.Child = boostStack;
        Grid.SetColumn(boostCard, 0);
        midGrid.Children.Add(boostCard);

        // Right Card: Quick Real Recommendations
        var recCol = new StackPanel();

        var recTitleStack = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
        recTitleStack.Children.Add(new TextBlock { Text = NexLocale.T("dash_recs_title"), FontSize = 13.5, FontWeight = FontWeights.Bold, Foreground = TextBrush });
        recTitleStack.Children.Add(new TextBlock { Text = NexLocale.T("dash_recs_sub"), FontSize = 10.5, Foreground = MutedBrush });
        recCol.Children.Add(recTitleStack);

        Border BuildRecCard(NexIcon ico, Brush icoColor, string title, string detail, string btnText, Action onAction)
        {
            var b = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(9, 16, 27)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(22, 38, 60)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 9, 12, 9),
                Margin = new Thickness(0, 0, 0, 7)
            };
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var icBox = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromArgb(28, 56, 189, 248)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(60, 56, 189, 248)),
                BorderThickness = new Thickness(1),
                Child = CreateVectorIcon(ico, icoColor, 14),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(icBox, 0); g.Children.Add(icBox);

            var st = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 10, 0) };
            st.Children.Add(new TextBlock { Text = title, FontSize = 11.5, FontWeight = FontWeights.SemiBold, Foreground = TextBrush });
            st.Children.Add(new TextBlock { Text = detail, FontSize = 10, Foreground = MutedBrush, TextWrapping = TextWrapping.Wrap });
            Grid.SetColumn(st, 1); g.Children.Add(st);

            var btn = new Button
            {
                Content = btnText,
                Style = (Style)FindResource("DarkTableFooterButtonStyle"),
                Padding = new Thickness(10, 4, 10, 4),
                FontSize = 10.5,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand
            };
            btn.Click += (_, _) => onAction();
            Grid.SetColumn(btn, 2); g.Children.Add(btn);

            b.Child = g;
            return b;
        }

        recCol.Children.Add(BuildRecCard(
            NexIcon.Clean, CyanBrush,
            NexLocale.T("dash_clean_temp"),
            NexLocale.Format("dash_clean_temp_desc", $"{tempGb:F1} GB", $"{tempFiles:N0}"),
            NexLocale.T("dash_clean_button"),
            async () =>
            {
                await ExecuteNativeSuiteAsync(NexLocale.T("dash_clean_action_title", "Curățare Fișiere Temporare"), NexLocale.T("dash_clean_action_working", "Ștergere cache shadere și directoare temporare..."), NexLocale.T("dash_clean_action_done", "Curățare efectuată cu succes."), () => NativeTuning.ApplyMaintenanceNativeAsync());
                ShowDashboard();
            }));

        recCol.Children.Add(BuildRecCard(
            NexIcon.Rocket, AmberBrush,
            NexLocale.T("dash_opt_startup"),
            NexLocale.Format("dash_opt_startup_desc", startupCount),
            NexLocale.T("dash_manage_button"),
            () => NavigateTo("Startup")));

        recCol.Children.Add(BuildRecCard(
            NexIcon.Gamepad, GreenBrush,
            NexLocale.T("dash_gaming_profile"),
            NexLocale.T("dash_gaming_desc"),
            NexLocale.T("dash_open_button"),
            () => NavigateTo("Gaming")));

        Grid.SetColumn(recCol, 2);
        midGrid.Children.Add(recCol);

        PageRoot.Children.Add(midGrid);

        // ================= ZONE 3. BOTTOM SYSTEM INTELLIGENCE STRIP =================
        var botStrip = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(8, 14, 24)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(18, 30, 48)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 8, 14, 8),
            Margin = new Thickness(0, 0, 0, 4)
        };
        var botGrid = new Grid();
        botGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var botLeft = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        botLeft.Children.Add(CreateVectorIcon(NexIcon.Shield, GreenBrush, 13));

        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        var uptimeStr = NexLocale.Format("dash_uptime_format", uptime.Days, uptime.Hours, uptime.Minutes);
        var sysInfo = $"Windows 11 {(Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit")} (Build {Environment.OSVersion.Version.Build}) | Uptime: {uptimeStr} | {NexLocale.T("dash_device_label")}: {Environment.MachineName} | {NexLocale.T("dash_banner_footer")}";
        botLeft.Children.Add(new TextBlock { Text = " " + sysInfo, FontSize = 10, Foreground = MutedBrush, VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(botLeft, 0); botGrid.Children.Add(botLeft);

        botStrip.Child = botGrid;
        PageRoot.Children.Add(botStrip);

        // ================= 4. WIRE REAL-TIME LIVE UPDATE TICK =================
        activePageTick = () =>
        {
            var live = NativeTuning.GetHardwareTelemetry(lastCpuUsage);
            if (!string.IsNullOrEmpty(detectedCpuName) && detectedCpuName != "Necunoscut") live.CpuName = detectedCpuName;
            if (!string.IsNullOrEmpty(detectedGpuName) && detectedGpuName != "Necunoscut") live.GpuName = detectedGpuName;

            double liveRamPct = live.RamTotalGb > 0 ? (live.RamUsedGb / live.RamTotalGb * 100.0) : 44.0;
            double liveDiskFreePct = live.DiskTotalGb > 0 ? (live.DiskFreeGb / live.DiskTotalGb * 100.0) : 50.0;

            cpuMiniVal.Text = $"{live.CpuUsagePercent:0}%";
            cpuMiniSub.Text = $"{live.CpuClockMhz:0} MHz - {live.CpuTempC:0.0} °C";
            cpuMiniBar.Value = Math.Clamp(live.CpuUsagePercent, 0, 100);

            gpuMiniVal.Text = $"{live.GpuUsagePercent:0}%";
            gpuMiniSub.Text = $"VRAM {live.GpuVramUsedGb:0.0}/{live.GpuVramTotalGb:0.0} GB - {live.GpuTempC:0.0} °C";
            gpuMiniBar.Value = Math.Clamp(live.GpuUsagePercent, 0, 100);

            ramMiniVal.Text = $"{liveRamPct:0}%";
            ramMiniSub.Text = $"{live.RamUsedGb:0.0} / {live.RamTotalGb:0.0} GB ({live.RamSpeedMhz} MT/s)";
            ramMiniBar.Value = Math.Clamp(liveRamPct, 0, 100);

            diskMiniVal.Text = $"{live.DiskUsagePercent:0}%";
            diskMiniSub.Text = $"{live.DiskFreeGb:0} GB {NexLocale.T("dash_free_label")} ({liveDiskFreePct:0}%)";
            diskMiniBar.Value = Math.Clamp(live.DiskUsagePercent, 0, 100);
        };
    }
}