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
    private string _activeGamingView = "Games"; // "Games" or "NvidiaInspector"
    private NativeTuning.NvidiaProfileSettings activeNvidiaSettings = NativeTuning.GetNvidiaPreset("Esports");
    private NativeTuning.NvidiaGpuInfo? activeNvidiaGpuInfo;

    private void ShowGaming()
    {
        activeNvidiaGpuInfo ??= NativeTuning.GetActiveNvidiaGpuInfo();
        bool isEn = NexLocale.CurrentLanguage == AppLanguage.En;
        PreparePage(NexLocale.T("gaming_title"), NexLocale.T("gaming_subtitle"));

        var topTabs = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14) };

        Border MakeGamingTopTab(string key, string label, NexIcon icon, string badge = "")
        {
            bool isCurr = _activeGamingView.Equals(key, StringComparison.OrdinalIgnoreCase);
            var b = new Border
            {
                Background = isCurr ? new SolidColorBrush(Color.FromRgb(11, 27, 54)) : new SolidColorBrush(Color.FromRgb(10, 17, 28)),
                BorderBrush = isCurr
                    ? (key == "NvidiaInspector" ? new SolidColorBrush(Color.FromRgb(16, 185, 129)) : new SolidColorBrush(Color.FromRgb(37, 99, 235)))
                    : new SolidColorBrush(Color.FromRgb(26, 40, 58)),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(14, 7, 14, 7),
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = Cursors.Hand
            };
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(CreateVectorIcon(icon, isCurr ? (key == "NvidiaInspector" ? GreenBrush : CyanBrush) : MutedBrush, 14));
            sp.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 12,
                FontWeight = isCurr ? FontWeights.Bold : FontWeights.SemiBold,
                Foreground = isCurr ? TextBrush : MutedBrush,
                Margin = new Thickness(7, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            if (!string.IsNullOrEmpty(badge))
            {
                sp.Children.Add(new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(42, 16, 185, 129)),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 1, 6, 1),
                    Margin = new Thickness(8, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock
                    {
                        Text = badge,
                        FontSize = 9.5,
                        FontWeight = FontWeights.Bold,
                        Foreground = GreenBrush
                    }
                });
            }
            b.Child = sp;
            b.MouseLeftButtonDown += (_, _) =>
            {
                _activeGamingView = key;
                ShowGaming();
            };
            return b;
        }

        topTabs.Children.Add(MakeGamingTopTab(
            "Games",
            isEn ? "Gaming Optimizations & Games" : "Optimizări Gaming & Jocuri",
            NexIcon.Gamepad));

        topTabs.Children.Add(MakeGamingTopTab(
            "NvidiaInspector",
            NexLocale.T("gaming_tab_nvidia", "NVIDIA Profile Inspector Suite"),
            NexIcon.Gpu,
            activeNvidiaGpuInfo.IsNvidiaDetected ? NexLocale.T("gaming_rtx_active", "RTX ACTIV") : "GPU"));

        if (cardGrid != null && PageRoot.Children.Contains(cardGrid))
        {
            PageRoot.Children.Insert(PageRoot.Children.IndexOf(cardGrid), topTabs);
        }
        else
        {
            PageRoot.Children.Add(topTabs);
        }

        if (_activeGamingView.Equals("NvidiaInspector", StringComparison.OrdinalIgnoreCase))
        {
            ShowNvidiaProfileInspectorSuite();
        }
        else
        {
            ShowWindowsGamingOptimizations();
        }
    }

    private void ShowWindowsGamingOptimizations()
    {
        var state = NativeTuning.DetectLiveTuningState();

        // 1. HAGS (Hardware-Accelerated GPU Scheduling)
        AddActionRow(
            NexLocale.T("gaming_hags_title"),
            NexLocale.T("gaming_hags_desc"),
            NexIcon.Cpu, state.HagsActive ? NexLocale.T("status_active") : NexLocale.T("status_inactive"), state.HagsActive ? GreenBrush : MutedBrush, CyanBrush, NexLocale.T("gaming_hags_badge"),
            state.HagsActive
                ? ActionBtn(NexLocale.T("gaming_hags_btn_off"), NexIcon.Cpu, CyanBrush, async () => {
                    await ExecuteNativeSuiteAsync(NexLocale.T("gaming_hags_title"), NexLocale.T("gaming_hags_working_off", "Dezactivare HAGS..."), NexLocale.T("gaming_hags_done_off", "HAGS a fost dezactivat."), () => Task.FromResult(new List<string> {
                        NativeTuning.SetRegistryDword(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 1) ? NexLocale.T("gaming_hags_log_default", "HAGS resetat la implicit.") : ""
                    }));
                    ShowGaming();
                })
                : ActionBtn(NexLocale.T("gaming_hags_btn_on"), NexIcon.Cpu, GreenBrush, async () => {
                    await ExecuteNativeSuiteAsync(NexLocale.T("gaming_hags_title"), NexLocale.T("gaming_hags_working_on", "Activare HAGS..."), NexLocale.T("gaming_hags_done_on", "HAGS a fost activat. Necesită repornire Windows."), () => Task.FromResult(new List<string> {
                        NativeTuning.SetRegistryDword(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 2) ? NexLocale.T("gaming_hags_log_enabled", "HAGS activat (HwSchMode = 2).") : ""
                    }));
                    ShowGaming();
                })
        );

        // 2. TCP NoDelay (Low Ping / Zero Network Buffering)
        AddActionRow(
            NexLocale.T("gaming_tcp_title"),
            NexLocale.T("gaming_tcp_desc"),
            NexIcon.Network, state.TcpNoDelayActive ? NexLocale.T("status_active") : NexLocale.T("status_not_configured"), state.TcpNoDelayActive ? GreenBrush : MutedBrush, PurpleBrush, NexLocale.T("gaming_tcp_badge"),
            state.TcpNoDelayActive
                ? ActionBtn(NexLocale.T("gaming_tcp_btn_off"), NexIcon.Network, PurpleBrush, async () => {
                    await ExecuteNativeSuiteAsync(NexLocale.T("gaming_tcp_title"), NexLocale.T("gaming_tcp_working_off", "Resetare setări rețea..."), NexLocale.T("gaming_tcp_done_off", "Setările de rețea au fost resetate."), () => Task.FromResult(new List<string> {
                        NexLocale.Format("gaming_tcp_log_off_format", NativeTuning.ConfigureTcpNoDelay(false))
                    }));
                    ShowGaming();
                })
                : ActionBtn(NexLocale.T("gaming_tcp_btn_on"), NexIcon.Network, PurpleBrush, async () => {
                    await ExecuteNativeSuiteAsync(NexLocale.T("gaming_tcp_title"), NexLocale.T("gaming_tcp_working_on", "Configurare latență rețea..."), NexLocale.T("gaming_tcp_done_on", "Optimizarea TCP NoDelay a fost aplicată."), () => Task.FromResult(new List<string> {
                        NexLocale.Format("gaming_tcp_log_on_format", NativeTuning.ConfigureTcpNoDelay(true))
                    }));
                    ShowGaming();
                }),
            RevertBtn(NexLocale.T("gaming_tcp_netlab"), () => { NavigateTo("Network"); return Task.CompletedTask; })
        );

        // 3. Plan de alimentare High Performance
        var isHighPerf = currentStatus?.PowerPlan?.Contains("High", StringComparison.OrdinalIgnoreCase) == true;
        AddActionRow(
            NexLocale.T("gaming_power_title"),
            NexLocale.T("gaming_power_desc"),
            NexIcon.Power, isHighPerf ? NexLocale.T("status_active") : NexLocale.T("status_balanced"), isHighPerf ? GreenBrush : MutedBrush, AmberBrush, NexLocale.T("gaming_power_badge"),
            isHighPerf
                ? ActionBtn(NexLocale.T("gaming_power_btn_off"), NexIcon.Power, CyanBrush, async () => {
                    await ExecuteNativeSuiteAsync(NexLocale.T("gaming_power_title"), NexLocale.T("gaming_power_working_off", "Setare Balanced..."), NexLocale.T("gaming_power_done_off", "Planul Balanced a fost restaurat."), async () => {
                        await NativeTuning.SetPowerSchemeAsync(false);
                        return new List<string> { NexLocale.T("gaming_power_log_balanced", "Planul Balanced este acum activ.") };
                    });
                    await RefreshStatusAsync();
                    ShowGaming();
                })
                : ActionBtn(NexLocale.T("gaming_power_btn_on"), NexIcon.Power, GreenBrush, async () => {
                    await ExecuteNativeSuiteAsync(NexLocale.T("gaming_power_title"), NexLocale.T("gaming_power_working_on", "Setare High Performance..."), NexLocale.T("gaming_power_done_on", "Planul High Performance a fost activat."), async () => {
                        await NativeTuning.SetPowerSchemeAsync(true);
                        return new List<string> { NexLocale.T("gaming_power_log_high", "Planul High Performance este acum activ.") };
                    });
                    await RefreshStatusAsync();
                    ShowGaming();
                })
        );

        // 4. Game Priority, Dedicated GPU & Steam Gaming Lite Configurator
        ShowGamePrioritySection();
    }

    private void ShowNvidiaProfileInspectorSuite()
    {
        var gpu = activeNvidiaGpuInfo ?? NativeTuning.GetActiveNvidiaGpuInfo();

        // 1. Hardware & Driver Telemetry Banner
        var teleCard = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(10, 18, 30)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(18, 14, 18, 14),
            Margin = new Thickness(0, 0, 0, 14)
        };

        var teleGrid = new Grid();
        teleGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        teleGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var topRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
        var gpuIcon = CreateVectorIcon(NexIcon.Cpu, GreenBrush, 20);
        gpuIcon.Margin = new Thickness(0, 0, 10, 0);
        topRow.Children.Add(gpuIcon);

        var titleStack = new StackPanel();
        var hTitle = new StackPanel { Orientation = Orientation.Horizontal };
        hTitle.Children.Add(new TextBlock
        {
            Text = gpu.GpuName,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = TextBrush
        });

        var rtxBadge = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(45, 16, 185, 129)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(120, 16, 185, 129)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 1, 6, 1),
            Margin = new Thickness(10, 0, 0, 0)
        };
        rtxBadge.Child = new TextBlock
        {
            Text = NexLocale.T("gaming_nvidia_banner_tag"),
            FontSize = 9.5,
            FontWeight = FontWeights.Bold,
            Foreground = GreenBrush
        };
        hTitle.Children.Add(rtxBadge);
        titleStack.Children.Add(hTitle);

        titleStack.Children.Add(new TextBlock
        {
            Text = NexLocale.Format("gaming_nvidia_arch_driver_format", gpu.Architecture, gpu.DriverVersion, gpu.DrsStatus),
            FontSize = 11,
            Foreground = MutedBrush,
            Margin = new Thickness(0, 2, 0, 0)
        });
        topRow.Children.Add(titleStack);
        Grid.SetRow(topRow, 0);
        teleGrid.Children.Add(topRow);

        // Telemetry Chips
        var chipsRow = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
        Border MakeChip(string label, string val, Brush col)
        {
            var b = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(14, 25, 42)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(28, 48, 78)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 5, 10, 5),
                Margin = new Thickness(0, 0, 8, 6)
            };
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(new TextBlock { Text = label + ": ", FontSize = 10.5, Foreground = MutedBrush });
            sp.Children.Add(new TextBlock { Text = val, FontSize = 10.5, FontWeight = FontWeights.SemiBold, Foreground = col });
            b.Child = sp;
            return b;
        }

        chipsRow.Children.Add(MakeChip(NexLocale.T("gaming_chip_vram"), gpu.VramTotal, CyanBrush));
        chipsRow.Children.Add(MakeChip(NexLocale.T("gaming_chip_bus"), gpu.BusInterface, GreenBrush));
        chipsRow.Children.Add(MakeChip(NexLocale.T("gaming_chip_drs"), NexLocale.T("gaming_chip_synced"), PurpleBrush));
        chipsRow.Children.Add(MakeChip(NexLocale.T("gaming_chip_reflex"), NexLocale.T("gaming_chip_active_support"), AmberBrush));
        chipsRow.Children.Add(MakeChip(NexLocale.T("gaming_chip_rbar"), NexLocale.T("gaming_chip_hw_compat"), GreenBrush));

        Grid.SetRow(chipsRow, 1);
        teleGrid.Children.Add(chipsRow);
        teleCard.Child = teleGrid;
        PageRoot.Children.Add(teleCard);

        // 2. Presets Toolbar
        var presetCard = new Border
        {
            Background = CardBackground(),
            BorderBrush = new SolidColorBrush(Color.FromRgb(24, 38, 56)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(16, 12, 16, 12),
            Margin = new Thickness(0, 0, 0, 14)
        };
        var presetStack = new StackPanel();
        var presetHeader = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
        presetHeader.Children.Add(CreateVectorIcon(NexIcon.Bolt, AmberBrush, 15));
        var pTitle = new TextBlock
        {
            Text = NexLocale.T("gaming_nvidia_presets_title"),
            FontSize = 12.5,
            FontWeight = FontWeights.Bold,
            Foreground = TextBrush,
            Margin = new Thickness(8, 0, 0, 0)
        };
        presetHeader.Children.Add(pTitle);
        presetStack.Children.Add(presetHeader);

        var presetBtns = new WrapPanel();
        Button MakePresetBtn(string title, string presetKey, Brush accent)
        {
            var btn = new Button
            {
                Content = title,
                Style = (Style)FindResource("DarkTableFooterButtonStyle"),
                Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(0, 0, 8, 6),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand
            };
            btn.Click += (_, _) =>
            {
                activeNvidiaSettings = NativeTuning.GetNvidiaPreset(presetKey);
                ShowToast("Preset", $"{title} -> {presetKey}", NexIcon.Check, accent);
                if (activeNav?.Tag?.ToString() == "Drivers") ShowDrivers(); else ShowGaming();
            };
            return btn;
        }

        presetBtns.Children.Add(MakePresetBtn(NexLocale.T("gaming_nvidia_preset_esports"), "Esports", CyanBrush));
        presetBtns.Children.Add(MakePresetBtn(NexLocale.T("gaming_nvidia_preset_smooth"), "Smooth", PurpleBrush));
        presetBtns.Children.Add(MakePresetBtn(NexLocale.T("gaming_nvidia_preset_visual"), "Visual", AmberBrush));
        presetBtns.Children.Add(MakePresetBtn(NexLocale.T("gaming_nvidia_preset_default"), "Default", CyanBrush));
        presetStack.Children.Add(presetBtns);

        presetCard.Child = presetStack;
        PageRoot.Children.Add(presetCard);

        // Helper for setting rows
        void AddSettingPillRow(StackPanel parent, string title, string desc, string[] options, string selectedVal, Action<string> onSelect)
        {
            var rowCard = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(12, 20, 32)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(22, 34, 50)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 0, 8)
            };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            info.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = TextBrush
            });
            info.Children.Add(new TextBlock
            {
                Text = desc,
                FontSize = 10.5,
                Foreground = MutedBrush,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0)
            });
            Grid.SetColumn(info, 0);
            grid.Children.Add(info);

            var pills = new WrapPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
            foreach (var opt in options)
            {
                bool isSel = string.Equals(opt, selectedVal, StringComparison.OrdinalIgnoreCase);
                var pill = new Border
                {
                    Background = isSel ? new SolidColorBrush(Color.FromRgb(2, 132, 199)) : new SolidColorBrush(Color.FromRgb(18, 28, 44)),
                    BorderBrush = isSel ? new SolidColorBrush(Color.FromRgb(56, 189, 248)) : new SolidColorBrush(Color.FromRgb(34, 52, 78)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(5),
                    Padding = new Thickness(10, 4, 10, 4),
                    Margin = new Thickness(0, 0, 6, 4),
                    Cursor = Cursors.Hand
                };
                var tb = new TextBlock
                {
                    Text = opt,
                    FontSize = 10.5,
                    FontWeight = isSel ? FontWeights.Bold : FontWeights.Normal,
                    Foreground = isSel ? Brushes.White : new SolidColorBrush(Color.FromRgb(148, 163, 184))
                };
                pill.Child = tb;
                string captureOpt = opt;
                pill.MouseLeftButtonDown += (_, _) =>
                {
                    onSelect(captureOpt);
                    if (activeNav?.Tag?.ToString() == "Drivers") ShowDrivers(); else ShowGaming();
                };
                pills.Children.Add(pill);
            }
            Grid.SetColumn(pills, 1);
            grid.Children.Add(pills);

            rowCard.Child = grid;
            parent.Children.Add(rowCard);
        }

        Border CreateCategoryCard(string catTitle, string catDesc, NexIcon icon, Brush accent)
        {
            var b = new Border
            {
                Background = CardBackground(),
                BorderBrush = new SolidColorBrush(Color.FromRgb(24, 38, 56)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(16, 14, 16, 14),
                Margin = new Thickness(0, 0, 0, 14)
            };
            var st = new StackPanel();
            var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
            header.Children.Add(CreateVectorIcon(icon, accent, 16));
            var hInfo = new StackPanel { Margin = new Thickness(8, 0, 0, 0) };
            hInfo.Children.Add(new TextBlock { Text = catTitle, FontSize = 13, FontWeight = FontWeights.Bold, Foreground = TextBrush });
            hInfo.Children.Add(new TextBlock { Text = catDesc, FontSize = 10.5, Foreground = MutedBrush, Margin = new Thickness(0, 2, 0, 0) });
            header.Children.Add(hInfo);
            st.Children.Add(header);
            b.Child = st;
            return b;
        }

        // ================= CATEGORIA 1: Sincronizare & Latenta =================
        var cat1 = CreateCategoryCard(NexLocale.T("gaming_nvidia_cat1_title"), NexLocale.T("gaming_nvidia_cat1_desc"), NexIcon.Pulse, CyanBrush);
        var st1 = (StackPanel)cat1.Child;
        AddSettingPillRow(st1, NexLocale.T("gaming_opt_low_latency_title"), NexLocale.T("gaming_opt_low_latency_desc"), new[] { "Off", "On", "Ultra" }, activeNvidiaSettings.LowLatencyMode, val => activeNvidiaSettings.LowLatencyMode = val);
        AddSettingPillRow(st1, NexLocale.T("gaming_opt_pre_rendered_title"), NexLocale.T("gaming_opt_pre_rendered_desc"), new[] { "Driver Default", "1", "2", "3", "4" }, activeNvidiaSettings.MaxPreRenderedFrames, val => activeNvidiaSettings.MaxPreRenderedFrames = val);
        AddSettingPillRow(st1, NexLocale.T("gaming_opt_reflex_override_title"), NexLocale.T("gaming_opt_reflex_override_desc"), new[] { "Default", "On", "On + Boost" }, activeNvidiaSettings.ReflexOverride, val => activeNvidiaSettings.ReflexOverride = val);
        AddSettingPillRow(st1, NexLocale.T("gaming_opt_fps_limiter_title"), NexLocale.T("gaming_opt_fps_limiter_desc"), new[] { "Off", "60 FPS", "120 FPS", "141 FPS (144Hz)", "162 FPS (165Hz)", "237 FPS (240Hz)" }, activeNvidiaSettings.FrameRateLimiter, val => activeNvidiaSettings.FrameRateLimiter = val);
        AddSettingPillRow(st1, NexLocale.T("gaming_opt_monitor_tech_title"), NexLocale.T("gaming_opt_monitor_tech_desc"), new[] { "G-Sync Compatible", "Fixed Refresh Rate" }, activeNvidiaSettings.MonitorTechnology, val => activeNvidiaSettings.MonitorTechnology = val);
        PageRoot.Children.Add(cat1);

        // ================= CATEGORIA 2: Texturi & Filtrare =================
        var cat2 = CreateCategoryCard(NexLocale.T("gaming_nvidia_cat2_title"), NexLocale.T("gaming_nvidia_cat2_desc"), NexIcon.Layers, GreenBrush);
        var st2 = (StackPanel)cat2.Child;
        AddSettingPillRow(st2, NexLocale.T("gaming_opt_tex_quality_title"), NexLocale.T("gaming_opt_tex_quality_desc"), new[] { "High Performance", "Performance", "Quality", "High Quality" }, activeNvidiaSettings.TextureQuality, val => activeNvidiaSettings.TextureQuality = val);
        AddSettingPillRow(st2, NexLocale.T("gaming_opt_lod_bias_title"), NexLocale.T("gaming_opt_lod_bias_desc"), new[] { "Clamp", "Allow" }, activeNvidiaSettings.NegativeLodBias, val => activeNvidiaSettings.NegativeLodBias = val);
        AddSettingPillRow(st2, NexLocale.T("gaming_opt_af_title"), NexLocale.T("gaming_opt_af_desc"), new[] { "App-controlled", "Off", "2x", "4x", "8x", "16x" }, activeNvidiaSettings.AnisotropicFiltering, val => activeNvidiaSettings.AnisotropicFiltering = val);
        AddSettingPillRow(st2, NexLocale.T("gaming_opt_af_sample_title"), NexLocale.T("gaming_opt_af_sample_desc"), new[] { "On", "Off" }, activeNvidiaSettings.AnisotropicSampleOptimization ? "On" : "Off", val => activeNvidiaSettings.AnisotropicSampleOptimization = val == "On");
        AddSettingPillRow(st2, NexLocale.T("gaming_opt_trilinear_title"), NexLocale.T("gaming_opt_trilinear_desc"), new[] { "On", "Off" }, activeNvidiaSettings.TrilinearOptimization ? "On" : "Off", val => activeNvidiaSettings.TrilinearOptimization = val == "On");
        PageRoot.Children.Add(cat2);

        // ================= CATEGORIA 3: Alimentare & Hardware =================
        var cat3 = CreateCategoryCard(NexLocale.T("gaming_nvidia_cat3_title"), NexLocale.T("gaming_nvidia_cat3_desc"), NexIcon.Power, PurpleBrush);
        var st3 = (StackPanel)cat3.Child;
        AddSettingPillRow(st3, NexLocale.T("gaming_opt_power_mode_title"), NexLocale.T("gaming_opt_power_mode_desc"), new[] { "Prefer Maximum Performance", "Optimal Power / Adaptive" }, activeNvidiaSettings.PowerManagementMode, val => activeNvidiaSettings.PowerManagementMode = val);
        AddSettingPillRow(st3, NexLocale.T("gaming_opt_shader_cache_title"), NexLocale.T("gaming_opt_shader_cache_desc"), new[] { "Disabled", "10 GB", "100 GB", "Unlimited" }, activeNvidiaSettings.ShaderCacheSize, val => activeNvidiaSettings.ShaderCacheSize = val);
        AddSettingPillRow(st3, NexLocale.T("gaming_opt_p2_state_title"), NexLocale.T("gaming_opt_p2_state_desc"), new[] { "Disabled (Max VRAM Clock)", "Enabled (Standard)" }, activeNvidiaSettings.DisableCudaP2State ? "Disabled (Max VRAM Clock)" : "Enabled (Standard)", val => activeNvidiaSettings.DisableCudaP2State = val.StartsWith("Disabled"));
        AddSettingPillRow(st3, NexLocale.T("gaming_opt_threaded_title"), NexLocale.T("gaming_opt_threaded_desc"), new[] { "Auto", "On", "Off" }, activeNvidiaSettings.ThreadedOptimization, val => activeNvidiaSettings.ThreadedOptimization = val);
        PageRoot.Children.Add(cat3);

        // ================= CATEGORIA 4: Resizable BAR Deep Tuning =================
        var cat4 = CreateCategoryCard(NexLocale.T("gaming_nvidia_cat4_title"), NexLocale.T("gaming_nvidia_cat4_desc"), NexIcon.Bolt, AmberBrush);
        var st4 = (StackPanel)cat4.Child;
        AddSettingPillRow(st4, NexLocale.T("gaming_opt_rbar_feat_title"), NexLocale.T("gaming_opt_rbar_feat_desc"), new[] { "Enabled", "Disabled" }, activeNvidiaSettings.ResizableBarFeature ? "Enabled" : "Disabled", val => activeNvidiaSettings.ResizableBarFeature = val == "Enabled");
        AddSettingPillRow(st4, NexLocale.T("gaming_opt_rbar_force_title"), NexLocale.T("gaming_opt_rbar_force_desc"), new[] { "Enabled (0x00000001)", "Disabled" }, activeNvidiaSettings.ResizableBarForceAllGames ? "Enabled (0x00000001)" : "Disabled", val => activeNvidiaSettings.ResizableBarForceAllGames = val.StartsWith("Enabled"));
        AddSettingPillRow(st4, NexLocale.T("gaming_opt_rbar_limit_title"), NexLocale.T("gaming_opt_rbar_limit_desc"), new[] { "No Limit (0x00000000)", "Standard Limit" }, activeNvidiaSettings.ResizableBarNoSizeLimit ? "No Limit (0x00000000)" : "Standard Limit", val => activeNvidiaSettings.ResizableBarNoSizeLimit = val.StartsWith("No Limit"));
        PageRoot.Children.Add(cat4);

        // ================= ACTION FOOTER =================
        var actionCard = new Border
        {
            Background = CardBackground(),
            BorderBrush = new SolidColorBrush(Color.FromRgb(24, 38, 56)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(18, 14, 18, 14),
            Margin = new Thickness(0, 4, 0, 20)
        };
        var actionGrid = new Grid();
        actionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        actionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var actLeft = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        actLeft.Children.Add(new TextBlock
        {
            Text = NexLocale.T("gaming_nvidia_card_title"),
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = TextBrush
        });
        actLeft.Children.Add(new TextBlock
        {
            Text = NexLocale.T("gaming_nvidia_card_desc"),
            FontSize = 10.5,
            Foreground = MutedBrush,
            Margin = new Thickness(0, 2, 0, 0)
        });
        Grid.SetColumn(actLeft, 0);
        actionGrid.Children.Add(actLeft);

        var actBtns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        // Buton Aplicare
        var applyBtn = new Button
        {
            Content = NexLocale.T("gaming_nvidia_apply_btn"),
            Style = (Style)FindResource("DarkTableFooterButtonStyle"),
            Background = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
            Foreground = Brushes.White,
            Padding = new Thickness(16, 8, 16, 8),
            FontSize = 11.5,
            FontWeight = FontWeights.Bold,
            Cursor = Cursors.Hand
        };
        applyBtn.Click += async (_, _) =>
        {
            ShowToast(NexLocale.T("gaming_nvidia_toast_title", "NVIDIA Profile Inspector"), NexLocale.T("gaming_nvidia_toast_applying", "Se aplică setările în registrul driverului GPU..."), NexIcon.Pulse, CyanBrush);
            bool ok = await Task.Run(() => NativeTuning.ApplyNvidiaSettings(activeNvidiaSettings));
            if (ok)
            {
                ShowToast(NexLocale.T("gaming_nvidia_toast_applied_title", "Setări NVIDIA Aplicate"), NexLocale.T("gaming_nvidia_toast_applied_msg", "Setările de latență, randare și rBAR au fost actualizate."), NexIcon.Check, GreenBrush);
                NativeTuning.SendWindowsNativeToast(NexLocale.T("gaming_nvidia_toast_app_title", "NexWin NVIDIA Optimizer"), NexLocale.T("gaming_nvidia_toast_applied_msg", "Setările de profil NVIDIA au fost aplicate cu succes."));
            }
            else
            {
                ShowToast(NexLocale.T("common_warning", "Avertisment"), NexLocale.T("gaming_nvidia_toast_admin_req", "Unele chei de registry necesită drepturi de administrator elevat."), NexIcon.Warning, AmberBrush);
            }
        };
        actBtns.Children.Add(applyBtn);

        // Buton Export .nip
        var exportBtn = new Button
        {
            Content = NexLocale.T("gaming_nvidia_export"),
            Style = (Style)FindResource("DarkTableFooterButtonStyle"),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(8, 0, 0, 0),
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Cursor = Cursors.Hand
        };
        exportBtn.Click += async (_, _) =>
        {
            try
            {
                string sfd = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "NexWin_NvidiaProfile.nip");
                var nipXml = NativeTuning.GenerateNipXml(activeNvidiaSettings);
                await File.WriteAllTextAsync(sfd, nipXml, Encoding.Unicode);
                ShowToast(NexLocale.T("gaming_nvidia_export_title", "Export"), NexLocale.T("gaming_nvidia_export_msg", "Salvat pe Desktop: NexWin_NvidiaProfile.nip"), NexIcon.Check, CyanBrush);
            }
            catch (Exception ex)
            {
                ShowToast(NexLocale.T("gaming_nvidia_export_err", "Eroare Export"), ex.Message, NexIcon.Warning, AmberBrush);
            }
        };
        actBtns.Children.Add(exportBtn);

        // Buton Import .nip
        var importBtn = new Button
        {
            Content = NexLocale.T("gaming_nvidia_import"),
            Style = (Style)FindResource("DarkTableFooterButtonStyle"),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(8, 0, 0, 0),
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Cursor = Cursors.Hand
        };
        importBtn.Click += async (_, _) =>
        {
            try
            {
                var ofd = new OpenFileDialog
                {
                    Filter = NexLocale.T("gaming_nvidia_file_filter", "NVIDIA Profile Inspector (*.nip)|*.nip|Toate fișierele (*.*)|*.*"),
                    Title = NexLocale.T("gaming_nvidia_file_title", "Selectează fișierul de profil NVIDIA (.nip)")
                };
                if (ofd.ShowDialog() == true)
                {
                    string xml = await File.ReadAllTextAsync(ofd.FileName);
                    activeNvidiaSettings = NativeTuning.ParseNipXml(xml);
                    ShowToast(NexLocale.T("gaming_nvidia_import_title", "Import"), NexLocale.Format("gaming_nvidia_import_msg", Path.GetFileName(ofd.FileName)), NexIcon.Check, PurpleBrush);
                    if (activeNav?.Tag?.ToString() == "Drivers") ShowDrivers(); else ShowGaming();
                }
            }
            catch (Exception ex)
            {
                ShowToast(NexLocale.T("gaming_nvidia_import_err", "Eroare Import"), ex.Message, NexIcon.Warning, AmberBrush);
            }
        };
        actBtns.Children.Add(importBtn);

        Grid.SetColumn(actBtns, 1);
        actionGrid.Children.Add(actBtns);
        actionCard.Child = actionGrid;
        PageRoot.Children.Add(actionCard);
    }

    private string selectedGameExe = "steam.exe";
    private string gameSearchFilter = "";

    private FrameworkElement GetGameBadgeElement(NativeTuning.GamePriorityItem game, double size = 36)
    {
        var name = (game.DisplayName ?? "").ToUpperInvariant();
        var exe = (game.ExeName ?? "").ToUpperInvariant();

        // Official Steam vector icon badge for Steam Lite
        if (name.Contains("STEAM") || exe.Contains("STEAM"))
        {
            var steamBox = new Border
            {
                Width = size,
                Height = size,
                CornerRadius = new CornerRadius(Math.Max(6, size * 0.24)),
                Background = new LinearGradientBrush(Color.FromRgb(17, 41, 77), Color.FromRgb(10, 24, 48), new Point(0, 0), new Point(1, 1)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                BorderThickness = new Thickness(1.5),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            var steamPath = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M11.979 0C5.678 0 .511 4.86.022 11.037l6.432 2.658c.545-.371 1.203-.59 1.912-.59.063 0 .125.004.188.006l2.861-4.142V8.91c0-2.495 2.028-4.524 4.524-4.524 2.494 0 4.524 2.031 4.524 4.527s-2.03 4.525-4.524 4.525h-.105l-4.076 2.911c0 .052.004.105.004.159 0 1.875-1.515 3.396-3.39 3.396-1.635 0-3.016-1.173-3.331-2.727L.436 15.27C1.862 20.307 6.486 24 11.979 24c6.627 0 11.999-5.373 11.999-12S18.605 0 11.979 0zM7.54 18.21l-1.473-.61c.262.543.714.999 1.314 1.25 1.297.539 2.793-.076 3.332-1.375.263-.63.264-1.319.005-1.949s-.75-1.121-1.377-1.383c-.624-.26-1.29-.249-1.878-.03l1.523.63c.956.4 1.409 1.5 1.009 2.455-.397.957-1.497 1.41-2.454 1.012H7.54zm11.415-9.303c0-1.662-1.353-3.015-3.015-3.015-1.665 0-3.015 1.353-3.015 3.015 0 1.665 1.35 3.015 3.015 3.015 1.663 0 3.015-1.35 3.015-3.015zm-5.273-.005c0-1.252 1.013-2.266 2.265-2.266 1.249 0 2.266 1.014 2.266 2.266 0 1.251-1.017 2.265-2.266 2.265-1.253 0-2.265-1.014-2.265-2.265z"),
                Fill = new SolidColorBrush(Color.FromRgb(125, 211, 252)),
                Width = size * 0.62,
                Height = size * 0.62,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            steamBox.Child = steamPath;
            return steamBox;
        }

        // Official NVIDIA GPU vector badge for NVIDIA Profile Inspector Suite
        if (name.Contains("NVIDIA") || exe.Contains("NVIDIA"))
        {
            var nvBox = new Border
            {
                Width = size,
                Height = size,
                CornerRadius = new CornerRadius(Math.Max(6, size * 0.24)),
                Background = new LinearGradientBrush(Color.FromRgb(8, 42, 30), Color.FromRgb(6, 26, 20), new Point(0, 0), new Point(1, 1)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                BorderThickness = new Thickness(1.5),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Child = CreateVectorIcon(NexIcon.Gpu, new SolidColorBrush(Color.FromRgb(52, 211, 153)), size * 0.56)
            };
            return nvBox;
        }

        if (game.Icon != null)
        {
            var img = new Image
            {
                Source = game.Icon,
                Width = size,
                Height = size,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            return img;
        }

        var container = new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(Math.Max(4, size * 0.2)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (name.Contains("GTA") || name.Contains("GRAND THEFT AUTO"))
        {
            container.Background = new SolidColorBrush(Color.FromRgb(15, 35, 20));
            container.BorderBrush = new SolidColorBrush(Color.FromRgb(34, 197, 94));
            container.BorderThickness = new Thickness(1.5);
            container.Child = new TextBlock
            {
                Text = "V",
                FontSize = size * 0.55,
                FontWeight = FontWeights.ExtraBold,
                Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }
        else if (name.Contains("CS2") || name.Contains("COUNTER-STRIKE") || name.Contains("CS:GO"))
        {
            container.Background = new SolidColorBrush(Color.FromRgb(40, 25, 10));
            container.BorderBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11));
            container.BorderThickness = new Thickness(1.5);
            container.Child = new TextBlock
            {
                Text = "$",
                FontSize = size * 0.55,
                FontWeight = FontWeights.ExtraBold,
                Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }
        else if (name.Contains("FIVEM"))
        {
            container.Background = new SolidColorBrush(Color.FromRgb(45, 20, 10));
            container.BorderBrush = new SolidColorBrush(Color.FromRgb(249, 115, 22));
            container.BorderThickness = new Thickness(1.5);
            container.Child = new TextBlock
            {
                Text = "?",
                FontSize = size * 0.6,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(249, 115, 22)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }
        else if (name.Contains("RDR") || name.Contains("RED DEAD"))
        {
            container.Background = new SolidColorBrush(Color.FromRgb(45, 10, 10));
            container.BorderBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            container.BorderThickness = new Thickness(1.5);
            container.Child = new TextBlock
            {
                Text = "II",
                FontSize = size * 0.5,
                FontWeight = FontWeights.ExtraBold,
                Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }
        else if (name.Contains("VALORANT"))
        {
            container.Background = new SolidColorBrush(Color.FromRgb(40, 10, 20));
            container.BorderBrush = new SolidColorBrush(Color.FromRgb(244, 63, 94));
            container.BorderThickness = new Thickness(1.5);
            container.Child = new TextBlock
            {
                Text = "V",
                FontSize = size * 0.55,
                FontWeight = FontWeights.ExtraBold,
                Foreground = new SolidColorBrush(Color.FromRgb(244, 63, 94)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }
        else if (name.Contains("FORTNITE"))
        {
            container.Background = new SolidColorBrush(Color.FromRgb(10, 30, 50));
            container.BorderBrush = new SolidColorBrush(Color.FromRgb(14, 165, 233));
            container.BorderThickness = new Thickness(1.5);
            container.Child = new TextBlock
            {
                Text = "F",
                FontSize = size * 0.55,
                FontWeight = FontWeights.ExtraBold,
                Foreground = new SolidColorBrush(Color.FromRgb(14, 165, 233)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }
        else if (name.Contains("MINECRAFT"))
        {
            container.Background = new SolidColorBrush(Color.FromRgb(20, 35, 15));
            container.BorderBrush = new SolidColorBrush(Color.FromRgb(34, 197, 94));
            container.BorderThickness = new Thickness(1.5);
            container.Child = new TextBlock
            {
                Text = "¦",
                FontSize = size * 0.5,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }
        else if (name.Contains("CALL OF DUTY") || name.Contains("WARZONE") || name.Contains("COD"))
        {
            container.Background = new SolidColorBrush(Color.FromRgb(25, 30, 40));
            container.BorderBrush = new SolidColorBrush(Color.FromRgb(148, 163, 184));
            container.BorderThickness = new Thickness(1.5);
            container.Child = new TextBlock
            {
                Text = "COD",
                FontSize = size * 0.35,
                FontWeight = FontWeights.ExtraBold,
                Foreground = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }
        else if (name.Contains("APEX"))
        {
            container.Background = new SolidColorBrush(Color.FromRgb(40, 15, 15));
            container.BorderBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            container.BorderThickness = new Thickness(1.5);
            container.Child = new TextBlock
            {
                Text = "A",
                FontSize = size * 0.55,
                FontWeight = FontWeights.ExtraBold,
                Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }
        else if (name.Contains("ROBLOX"))
        {
            container.Background = new SolidColorBrush(Color.FromRgb(30, 20, 25));
            container.BorderBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            container.BorderThickness = new Thickness(1.5);
            container.Child = new TextBlock
            {
                Text = "R",
                FontSize = size * 0.55,
                FontWeight = FontWeights.ExtraBold,
                Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }
        else
        {
            container.Background = new SolidColorBrush(Color.FromRgb(15, 23, 42));
            container.BorderBrush = CyanBrush;
            container.BorderThickness = new Thickness(1);
            var ico = CreateVectorIcon(NexIcon.Gamepad, CyanBrush, size * 0.6);
            container.Child = ico;
        }

        return container;
    }

    private void ShowGamePrioritySection()
    {
        var defaultGames = NativeTuning.GetDefaultGamesList();
        if (!defaultGames.Any(g => g.ExeName.Equals("steam.exe", StringComparison.OrdinalIgnoreCase)))
        {
            defaultGames.Insert(0, new NativeTuning.GamePriorityItem
            {
                DisplayName = "Steam Lite",
                ExeName = "steam.exe",
                FullPath = @"C:\Program Files (x86)\Steam\steam.exe",
                IsHighPriority = true
            });
        }
        if (selectedGameExe.Equals("nvidia-inspector.exe", StringComparison.OrdinalIgnoreCase))
        {
            selectedGameExe = "steam.exe";
        }
        var selectedGame = defaultGames.FirstOrDefault(g => g.ExeName.Equals(selectedGameExe, StringComparison.OrdinalIgnoreCase)) ?? defaultGames.First();

        var container = new Border
        {
            Background = CardBackground(),
            BorderBrush = new SolidColorBrush(Color.FromRgb(24, 36, 52)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(18, 16, 18, 16),
            Margin = new Thickness(0, 16, 0, 14)
        };

        var rootStack = new StackPanel();

        // 1. Header (Matching media_1789755330928.png)
        var headGrid = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        headGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        textStack.Children.Add(new TextBlock
        {
            Text = NexLocale.T("gaming_prio_title"),
            FontSize = 15,
            FontWeight = FontWeights.Bold,
            Foreground = TextBrush
        });
        textStack.Children.Add(new TextBlock
        {
            Text = NexLocale.T("gaming_prio_sub"),
            FontSize = 11.5,
            Foreground = MutedBrush,
            Margin = new Thickness(0, 3, 0, 0)
        });
        Grid.SetColumn(textStack, 0);
        headGrid.Children.Add(textStack);

        var rightBar = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        // Search Box (media_1789755330928.png)
        var sBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(10, 19, 34)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(31, 46, 69)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 4, 10, 4),
            Margin = new Thickness(0, 0, 10, 0),
            Height = 32
        };
        var sGrid = new Grid();
        sGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        sGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var sIco = CreateVectorIcon(NexIcon.Search, new SolidColorBrush(Color.FromRgb(140, 160, 185)), 12);
        sIco.Margin = new Thickness(0, 0, 8, 0);
        sIco.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(sIco, 0);
        sGrid.Children.Add(sIco);

        var sInput = new TextBox
        {
            Text = gameSearchFilter,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = TextBrush,
            CaretBrush = TextBrush,
            FontSize = 11.5,
            Width = 140,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(sInput, 1);
        sGrid.Children.Add(sInput);
        sBorder.Child = sGrid;

        // Custom game button (media_1789755330928.png)
        var addBtn = new Button
        {
            Content = NexLocale.T("gaming_prio_btn_add_custom"),
            Style = (Style)FindResource("SecondaryButtonStyle"),
            Padding = new Thickness(12, 6, 12, 6),
            Cursor = Cursors.Hand,
            Height = 32
        };
        addBtn.Click += (_, _) =>
        {
            var dlg = new OpenFileDialog
            {
                Filter = NexLocale.T("gaming_exe_filter", "Executabil joc (*.exe)|*.exe"),
                Title = NexLocale.T("gaming_prio_dlg_title")
            };
            if (dlg.ShowDialog() == true)
            {
                var exeName = Path.GetFileName(dlg.FileName);
                NativeTuning.SaveCustomGame(dlg.FileName, Path.GetFileNameWithoutExtension(dlg.FileName));
                NativeTuning.SetGamePriorityNative(exeName, dlg.FileName, true);
                selectedGameExe = exeName;
                AppendLog(NexLocale.Format("gaming_log_game_added_format", exeName, dlg.FileName), false);
                ShowToast(NexLocale.T("gaming_toast_prio_set_title", "Prioritate Joc Setată"), NexLocale.Format("gaming_toast_prio_set_msg_format", exeName), NexIcon.Check, GreenBrush);
                ShowGaming();
            }
        };

        // Scan Games button
        var scanBtn = new Button
        {
            Content = NexLocale.T("gaming_prio_btn_scan"),
            Style = (Style)FindResource("SecondaryButtonStyle"),
            Padding = new Thickness(12, 6, 12, 6),
            Margin = new Thickness(0, 0, 8, 0),
            Cursor = Cursors.Hand,
            Height = 32
        };
        scanBtn.Click += async (_, _) =>
        {
            ShowNotification(NexLocale.T("gaming_scan_notif_title", "Scanare Jocuri"), NexLocale.T("gaming_scan_notif_working", "Scanez librăriile de jocuri instalate pe PC..."), true);
            var scanned = await Task.Run(() => NativeTuning.ScanInstalledGamesNative(true));
            ShowNotification(NexLocale.T("gaming_scan_done_title", "Scanare Finalizată"), NexLocale.Format("gaming_scan_done_msg_format", scanned.Count), false, true);
            ShowGaming();
        };

        rightBar.Children.Add(sBorder);
        rightBar.Children.Add(scanBtn);
        rightBar.Children.Add(addBtn);
        Grid.SetColumn(rightBar, 1);
        headGrid.Children.Add(rightBar);
        rootStack.Children.Add(headGrid);

        // 2. Horizontal Game Tiles Carousel (Matching media_1789755330928.png)
        var tilesViewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Margin = new Thickness(0, 4, 0, 16)
        };
        var tilesStack = new StackPanel { Orientation = Orientation.Horizontal };

        void RenderTiles()
        {
            tilesStack.Children.Clear();
            var filtered = string.IsNullOrWhiteSpace(gameSearchFilter)
                ? defaultGames
                : defaultGames.Where(g => g.DisplayName.Contains(gameSearchFilter, StringComparison.OrdinalIgnoreCase) ||
                                          g.ExeName.Contains(gameSearchFilter, StringComparison.OrdinalIgnoreCase)).ToList();

            foreach (var g in filtered)
            {
                bool isSel = g.ExeName.Equals(selectedGame.ExeName, StringComparison.OrdinalIgnoreCase);

                var tile = new Border
                {
                    Width = 84,
                    Height = 84,
                    CornerRadius = new CornerRadius(10),
                    Margin = new Thickness(0, 0, 10, 0),
                    Cursor = Cursors.Hand,
                    Background = isSel ? new SolidColorBrush(Color.FromRgb(18, 18, 40)) : new SolidColorBrush(Color.FromRgb(10, 17, 30)),
                    BorderBrush = isSel ? new SolidColorBrush(Color.FromRgb(168, 85, 247)) : new SolidColorBrush(Color.FromRgb(26, 40, 60)),
                    BorderThickness = new Thickness(isSel ? 2 : 1)
                };

                if (isSel)
                {
                    tile.Effect = new System.Windows.Media.Effects.DropShadowEffect
                    {
                        Color = Color.FromRgb(168, 85, 247),
                        BlurRadius = 12,
                        ShadowDepth = 0,
                        Opacity = 0.65
                    };
                }

                var tileContent = new StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                // Centered game icon
                var badge = GetGameBadgeElement(g, 36);
                tileContent.Children.Add(badge);

                // Game Name
                tileContent.Children.Add(new TextBlock
                {
                    Text = g.DisplayName,
                    FontSize = 11,
                    FontWeight = isSel ? FontWeights.Bold : FontWeights.SemiBold,
                    Foreground = isSel ? TextBrush : new SolidColorBrush(Color.FromRgb(200, 210, 225)),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(2, 6, 2, 0),
                    MaxWidth = 76
                });

                tile.Child = tileContent;

                var clickedExe = g.ExeName;
                tile.MouseLeftButtonUp += (_, _) =>
                {
                    selectedGameExe = clickedExe;
                    ShowGaming();
                };

                tilesStack.Children.Add(tile);
            }

            // "Toate jocurile" Tile (Last tile in media_1789755330928.png)
            var allTile = new Border
            {
                Width = 84,
                Height = 84,
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = Cursors.Hand,
                Background = new SolidColorBrush(Color.FromRgb(10, 17, 30)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(26, 40, 60)),
                BorderThickness = new Thickness(1)
            };
            var allContent = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            // 2x2 Grid Icon
            var gridIco = new Grid { Width = 22, Height = 22, Margin = new Thickness(0, 4, 0, 4) };
            gridIco.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            gridIco.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2) });
            gridIco.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            gridIco.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            gridIco.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2) });
            gridIco.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var sq1 = new Border { Background = new SolidColorBrush(Color.FromRgb(148, 163, 184)), CornerRadius = new CornerRadius(1.5) };
            var sq2 = new Border { Background = new SolidColorBrush(Color.FromRgb(148, 163, 184)), CornerRadius = new CornerRadius(1.5) };
            var sq3 = new Border { Background = new SolidColorBrush(Color.FromRgb(148, 163, 184)), CornerRadius = new CornerRadius(1.5) };
            var sq4 = new Border { Background = new SolidColorBrush(Color.FromRgb(148, 163, 184)), CornerRadius = new CornerRadius(1.5) };
            Grid.SetColumn(sq1, 0); Grid.SetRow(sq1, 0); gridIco.Children.Add(sq1);
            Grid.SetColumn(sq2, 2); Grid.SetRow(sq2, 0); gridIco.Children.Add(sq2);
            Grid.SetColumn(sq3, 0); Grid.SetRow(sq3, 2); gridIco.Children.Add(sq3);
            Grid.SetColumn(sq4, 2); Grid.SetRow(sq4, 2); gridIco.Children.Add(sq4);
            allContent.Children.Add(gridIco);

            allContent.Children.Add(new TextBlock
            {
                Text = NexLocale.T("gaming_prio_all_games"),
                FontSize = 10.5,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            });
            allTile.Child = allContent;

            allTile.MouseLeftButtonUp += async (_, _) =>
            {
                ShowNotification(NexLocale.T("gaming_scan_notif_title", "Scanare Jocuri"), NexLocale.T("gaming_scan_notif_working", "Scanez librăriile de jocuri instalate..."), true);
                var scanned = await Task.Run(() => NativeTuning.ScanInstalledGamesNative(true));
                ShowNotification(NexLocale.T("gaming_scan_done_title", "Scanare Finalizată"), NexLocale.Format("gaming_scan_done_msg_format", scanned.Count), false, true);
                ShowGaming();
            };

            tilesStack.Children.Add(allTile);
        }

        sInput.TextChanged += (_, _) =>
        {
            gameSearchFilter = sInput.Text;
            RenderTiles();
        };

        RenderTiles();
        tilesViewer.Content = tilesStack;
        rootStack.Children.Add(tilesViewer);

        // 3. Selected Game Detail Card (Matching media_1789755330928.png)
        var detailCard = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(7, 14, 26)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(22, 36, 56)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 14, 16, 14),
            Margin = new Thickness(0, 0, 0, 14)
        };

        var dGrid = new Grid();
        dGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        dGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        dGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Left Icon in rounded box
        var iconBox = new Border
        {
            Width = 52,
            Height = 52,
            CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(Color.FromRgb(13, 25, 43)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(31, 51, 80)),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 14, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = GetGameBadgeElement(selectedGame, 38)
        };
        Grid.SetColumn(iconBox, 0);
        dGrid.Children.Add(iconBox);

        // Center Stack (Name, Status Pill, Feature Badges)
        var midStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

        // Row 1: Game Title + Status Pill
        var nameRow = new StackPanel { Orientation = Orientation.Horizontal };
        nameRow.Children.Add(new TextBlock
        {
            Text = selectedGame.DisplayName,
            FontSize = 16.5,
            FontWeight = FontWeights.Bold,
            Foreground = TextBrush,
            VerticalAlignment = VerticalAlignment.Center
        });

        // Status Pill ("Profil activ" / "Profil inactiv")
        var statPill = new Border
        {
            Background = selectedGame.IsHighPriority ? new SolidColorBrush(Color.FromRgb(5, 46, 22)) : new SolidColorBrush(Color.FromRgb(30, 41, 59)),
            BorderBrush = selectedGame.IsHighPriority ? new SolidColorBrush(Color.FromRgb(22, 101, 52)) : new SolidColorBrush(Color.FromRgb(51, 65, 85)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8, 2, 8, 2),
            Margin = new Thickness(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = selectedGame.IsHighPriority ? NexLocale.T("gaming_prio_stat_active") : NexLocale.T("gaming_prio_stat_inactive"),
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = selectedGame.IsHighPriority ? new SolidColorBrush(Color.FromRgb(74, 222, 128)) : new SolidColorBrush(Color.FromRgb(148, 163, 184))
            }
        };
        nameRow.Children.Add(statPill);
        midStack.Children.Add(nameRow);

        // Row 2: Real Game Details & Hardware Binding
        var featsRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };

        Border MakeInfoBadge(NexIcon ico, string label, string val, Brush valBrush)
        {
            var p = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(12, 24, 42)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(22, 44, 75)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 8, 0)
            };
            var st = new StackPanel { Orientation = Orientation.Horizontal };
            st.Children.Add(CreateVectorIcon(ico, CyanBrush, 11));
            st.Children.Add(new TextBlock { Text = " " + label + ": ", FontSize = 10.5, Foreground = MutedBrush, VerticalAlignment = VerticalAlignment.Center });
            st.Children.Add(new TextBlock { Text = val, FontSize = 10.5, FontWeight = FontWeights.SemiBold, Foreground = valBrush, VerticalAlignment = VerticalAlignment.Center });
            p.Child = st;
            return p;
        }

        bool isSteamSelected = selectedGame.ExeName.Equals("steam.exe", StringComparison.OrdinalIgnoreCase);
        if (isSteamSelected)
        {
            featsRow.Children.Add(MakeInfoBadge(NexIcon.Rocket, "Mod UI", NexLocale.T("gaming_tag_safe_small_ui", "Small UI (-nochatui)"), CyanBrush));
            featsRow.Children.Add(MakeInfoBadge(NexIcon.Clean, "Memorie", NexLocale.T("gaming_tag_working_set", "WorkingSet Trim"), GreenBrush));
            featsRow.Children.Add(MakeInfoBadge(NexIcon.Bolt, "Randare", NexLocale.T("gaming_tag_no_stutter", "Zero CEF Stutter"), AmberBrush));
        }
        else
        {
            featsRow.Children.Add(MakeInfoBadge(NexIcon.FileText, NexLocale.T("gaming_prio_badge_exe"), selectedGame.ExeName, TextBrush));
            featsRow.Children.Add(MakeInfoBadge(NexIcon.Cpu, NexLocale.T("gaming_prio_badge_core_prio"), selectedGame.IsHighPriority ? "High (P-Cores)" : "Normal", selectedGame.IsHighPriority ? GreenBrush : MutedBrush));
            featsRow.Children.Add(MakeInfoBadge(NexIcon.Gpu, NexLocale.T("gaming_prio_badge_gpu_alloc"), activeNvidiaGpuInfo?.GpuName ?? "GPU Dedicat", CyanBrush));
        }
        midStack.Children.Add(featsRow);

        Grid.SetColumn(midStack, 1);
        dGrid.Children.Add(midStack);

        var rightStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        if (isSteamSelected)
        {
            var launchSteamBtn = new Button
            {
                Cursor = Cursors.Hand,
                Height = 36,
                Padding = new Thickness(15, 0, 15, 0),
                Margin = new Thickness(0, 0, 10, 0),
                BorderThickness = new Thickness(0),
                Background = new LinearGradientBrush(Color.FromRgb(14, 165, 233), Color.FromRgb(37, 99, 235), new Point(0, 0), new Point(1, 0))
            };
            var lsStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            lsStack.Children.Add(CreateVectorIcon(NexIcon.Rocket, Brushes.White, 13));
            lsStack.Children.Add(new TextBlock
            {
                Text = "  " + NexLocale.T("gaming_steam_btn_launch", "Pornește Steam Lite"),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            });
            launchSteamBtn.Content = lsStack;
            launchSteamBtn.Click += async (_, _) =>
            {
                ShowNotification(NexLocale.T("gaming_steam_toast_title", "Steam Lite"), NexLocale.T("gaming_steam_launching", "Pornire Steam în mod Small UI optimizat..."), true);
                bool ok = await NativeTuning.LaunchSteamLiteAsync();
                if (ok)
                {
                    ShowNotification(NexLocale.T("gaming_steam_launched_title", "Steam Lite Pornit"), NexLocale.T("gaming_steam_launched_msg", "Steam a fost lansat cu succes în mod ultra-ușor."), false, true);
                    ShowToast(NexLocale.T("gaming_steam_launched_title", "Steam Lite Lansat"), NexLocale.T("gaming_steam_running_msg", "Steam rulează acum în mod Small UI cu consum minim de RAM."), NexIcon.Check, GreenBrush);
                }
                else
                {
                    ShowToast(NexLocale.T("gaming_steam_not_found_title", "Steam Nedetectat"), NexLocale.T("gaming_steam_not_found_msg", "Nu s-a putut localiza steam.exe în căile standard."), NexIcon.Warning, AmberBrush);
                }
            };
            rightStack.Children.Add(launchSteamBtn);

            var trimSteamBtn = new Button
            {
                Cursor = Cursors.Hand,
                Height = 36,
                Padding = new Thickness(15, 0, 15, 0),
                BorderThickness = new Thickness(0),
                Background = new LinearGradientBrush(Color.FromRgb(16, 185, 129), Color.FromRgb(5, 150, 105), new Point(0, 0), new Point(1, 0))
            };
            var tsStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            tsStack.Children.Add(CreateVectorIcon(NexIcon.Clean, Brushes.White, 13));
            tsStack.Children.Add(new TextBlock
            {
                Text = "  " + NexLocale.T("gaming_steam_btn_trim", "Eliberează RAM Steam"),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            });
            trimSteamBtn.Content = tsStack;
            trimSteamBtn.Click += (_, _) =>
            {
                int c = NativeTuning.TrimSteamWorkingSet();
                ShowToast(NexLocale.T("gaming_steam_trimmed_title", "RAM Steam Eliberat"), NexLocale.Format("gaming_steam_trimmed_msg_format", c), NexIcon.Check, GreenBrush);
                ShowGaming();
            };
            rightStack.Children.Add(trimSteamBtn);
        }
        else
        {
            var launchGameBtn = new Button
            {
                Cursor = Cursors.Hand,
                Height = 36,
                Padding = new Thickness(15, 0, 15, 0),
                Margin = new Thickness(0, 0, 10, 0),
                BorderThickness = new Thickness(0),
                Background = new LinearGradientBrush(Color.FromRgb(16, 185, 129), Color.FromRgb(5, 150, 105), new Point(0, 0), new Point(1, 0))
            };
            var lgStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            lgStack.Children.Add(CreateVectorIcon(NexIcon.Rocket, Brushes.White, 13));
            lgStack.Children.Add(new TextBlock
            {
                Text = "  " + NexLocale.T("gaming_prio_btn_launch", "Lansează Jocul"),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            });
            launchGameBtn.Content = lgStack;
            launchGameBtn.Click += (_, _) =>
            {
                NativeTuning.SetGamePriorityNative(selectedGame.ExeName, selectedGame.FullPath, true);
                if (!string.IsNullOrWhiteSpace(selectedGame.FullPath) && File.Exists(selectedGame.FullPath))
                {
                    try { Process.Start(new ProcessStartInfo { FileName = selectedGame.FullPath, UseShellExecute = true }); } catch { }
                }
                ShowToast(selectedGame.DisplayName, "Profilul High Priority + GPU Dedicat a fost activat pentru joc!", NexIcon.Check, GreenBrush);
                ShowGaming();
            };
            rightStack.Children.Add(launchGameBtn);

            var applyPrioBtn = new Button
            {
                Cursor = Cursors.Hand,
                Height = 36,
                Padding = new Thickness(15, 0, 15, 0),
                BorderThickness = new Thickness(0),
                Background = new LinearGradientBrush(Color.FromRgb(139, 92, 246), Color.FromRgb(109, 40, 217), new Point(0, 0), new Point(1, 0))
            };
            var apStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            apStack.Children.Add(CreateVectorIcon(NexIcon.Bolt, Brushes.White, 13));
            apStack.Children.Add(new TextBlock
            {
                Text = "  " + NexLocale.T("gaming_prio_btn_apply_high", "Aplică Profilul High"),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            });
            applyPrioBtn.Content = apStack;
            applyPrioBtn.Click += (_, _) =>
            {
                NativeTuning.SetGamePriorityNative(selectedGame.ExeName, selectedGame.FullPath, true);
                ShowToast(NexLocale.T("gaming_toast_prio_set_title", "Prioritate Joc Setată"), NexLocale.Format("gaming_toast_prio_set_msg_format", selectedGame.DisplayName), NexIcon.Check, GreenBrush);
                ShowGaming();
            };
            rightStack.Children.Add(applyPrioBtn);
        }

        Grid.SetColumn(rightStack, 2);
        dGrid.Children.Add(rightStack);
        detailCard.Child = dGrid;
        rootStack.Children.Add(detailCard);

        container.Child = rootStack;
        PageRoot.Children.Add(container);
    }

}
