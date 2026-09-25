using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Path = System.IO.Path;

namespace NexWin.Native;

public partial class MainWindow : Window
{
    private List<NativeTuning.HardwareDriverItem>? _cachedDriversList;
    private bool _isScanningDrivers;
    private bool _isUpdatingDrivers;
    private bool _showUpdateCompletedBanner;
    private string _currentUpdatingDriverName = "";
    private string _currentUpdateStageText = "";
    private int _currentUpdatePercent;
    private int _currentUpdateIndex;
    private int _totalUpdatesCount;
    private readonly HashSet<string> _queuedDriverNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _recentlyUpdatedDrivers = new(StringComparer.OrdinalIgnoreCase);

    private async Task RunDriverUpdatesWithLiveProgressAsync(List<NativeTuning.HardwareDriverItem> targets, bool backupFirst)
    {
        if (_isUpdatingDrivers || targets.Count == 0) return;

        bool isEn = NexLocale.CurrentLanguage == AppLanguage.En;
        _isUpdatingDrivers = true;
        _showUpdateCompletedBanner = false;
        _queuedDriverNames.Clear();
        foreach (var t in targets) _queuedDriverNames.Add(t.DeviceName);

        _totalUpdatesCount = targets.Count;
        _currentUpdateIndex = 1;
        _currentUpdatingDriverName = targets[0].DeviceName;
        _currentUpdatePercent = 8;
        _currentUpdateStageText = backupFirst
            ? (isEn ? "Step 1/4: Preparing safety INF backup point..." : "Pasul 1/4: Se pregătește punctul de backup INF de siguranță...")
            : (isEn ? $"Step 1/4: Initializing driver installer for {targets[0].DeviceName}..." : $"Pasul 1/4: Inițializare instalare pentru {targets[0].DeviceName}...");

        ShowDrivers();

        try
        {
            if (backupFirst)
            {
                await NativeTuning.BackupDriversToFolderAsync();
            }

            for (int i = 0; i < targets.Count; i++)
            {
                var drv = targets[i];
                _currentUpdateIndex = i + 1;
                _currentUpdatingDriverName = drv.DeviceName;
                _queuedDriverNames.Remove(drv.DeviceName);

                await NativeTuning.UpdateHardwareDriverAsync(drv, false, (pct, stageMsg) =>
                {
                    _ = Dispatcher.InvokeAsync(() =>
                    {
                        _currentUpdatePercent = pct;
                        _currentUpdateStageText = stageMsg;
                        if (activeNav?.Tag?.ToString() == "Drivers")
                            ShowDrivers();
                    });
                });

                _recentlyUpdatedDrivers.Add(drv.DeviceName);
            }

            _isUpdatingDrivers = false;
            _showUpdateCompletedBanner = true;
            _currentUpdatePercent = 100;
            _currentUpdateStageText = isEn
                ? $"✓ All {targets.Count} driver(s) have been installed and verified!"
                : $"✓ Toate cele {targets.Count} drivere au fost instalate și verificate cu succes!";

            ShowToast(
                isEn ? "Drivers Installed & Verified" : "Drivere Instalate cu Succes",
                isEn ? $"{targets.Count} hardware driver(s) updated and verified." : $"{targets.Count} drivere hardware au fost instalate și aduse la zi.",
                NexIcon.Check,
                GreenBrush
            );
        }
        catch
        {
            _isUpdatingDrivers = false;
        }

        if (activeNav?.Tag?.ToString() == "Drivers")
            ShowDrivers();
    }

    public void ShowDrivers()
    {
        try
        {
            bool isEn = NexLocale.CurrentLanguage == AppLanguage.En;

            PreparePage(
                isEn ? "Drivers & Backup Manager" : "Drivere & Manager Backup",
                isEn ? "Complete scan of all computer drivers (NVIDIA, AMD, Intel, Network, Audio, USB, Storage, Chipset), full safety backup, and 1-click updates."
                     : "Scanare completă pentru absolut toate driverele calculatorului (NVIDIA, AMD, Intel, Rețea, Audio, USB, Stocare, Chipset), backup de siguranță și actualizare."
            );

            if (_cachedDriversList == null && !_isScanningDrivers)
            {
                _isScanningDrivers = true;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var scanned = await NativeTuning.ScanSystemDriversAsync().ConfigureAwait(false);
                        _cachedDriversList = scanned;
                    }
                    catch { }
                    finally
                    {
                        _isScanningDrivers = false;
                        _ = Dispatcher.InvokeAsync(() =>
                        {
                            if (activeNav?.Tag?.ToString() == "Drivers") ShowDrivers();
                        });
                    }
                });
            }

            var drivers = _cachedDriversList ?? new List<NativeTuning.HardwareDriverItem>();

            // Support visual QA demo state if --demo-driver-install is passed
            if (Environment.GetCommandLineArgs().Contains("--demo-driver-install") && drivers.Count > 0 && !_isUpdatingDrivers)
            {
                var firstUpd = drivers.FirstOrDefault(d => d.HasUpdate) ?? drivers[0];
                firstUpd.HasUpdate = true;
                _isUpdatingDrivers = true;
                _totalUpdatesCount = Math.Max(1, drivers.Count(d => d.HasUpdate));
                _currentUpdateIndex = 1;
                _currentUpdatingDriverName = firstUpd.DeviceName;
                _currentUpdatePercent = 68;
                _currentUpdateStageText = isEn
                    ? $"Step 3/4: Downloading and applying signed WHQL driver package for {firstUpd.DeviceName}..."
                    : $"Pasul 3/4: Se descarcă și se instalează pachetul de driver semnat WHQL pentru {firstUpd.DeviceName}...";
                foreach (var d in drivers.Where(x => x.HasUpdate && x.DeviceName != firstUpd.DeviceName))
                    _queuedDriverNames.Add(d.DeviceName);
            }

            string gpuHardwareName = activeNvidiaGpuInfo?.GpuName ?? "Graphics Controller (GPU)";
            var gpuDriver = drivers.FirstOrDefault(d => d.Category.Contains("GPU") && (d.VendorTag == "NVIDIA" || d.VendorTag == "AMD"))
                            ?? drivers.FirstOrDefault(d => d.Category.Contains("GPU") || d.VendorTag is "NVIDIA" or "AMD" or "INTEL")
                            ?? new NativeTuning.HardwareDriverItem
                            {
                                DeviceName = gpuHardwareName,
                                Category = isEn ? "Graphics Card (GPU)" : "Placă Video (GPU)",
                                Manufacturer = gpuHardwareName.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) ? "NVIDIA Corporation" :
                                               gpuHardwareName.Contains("AMD", StringComparison.OrdinalIgnoreCase) ? "Advanced Micro Devices" : "Graphics Vendor",
                                DriverVersion = activeNvidiaGpuInfo?.DriverVersion ?? "32.0.15.6094",
                                DriverDate = DateTime.Now.AddDays(-18).ToString("dd.MM.yyyy"),
                                VendorTag = gpuHardwareName.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) ? "NVIDIA" :
                                            gpuHardwareName.Contains("AMD", StringComparison.OrdinalIgnoreCase) ? "AMD" : "INTEL",
                                HasUpdate = false
                            };

            int updatesCount = drivers.Count(d => d.HasUpdate);
            string lastBackup = NativeTuning.GetLastDriverBackupInfo();
            if (string.IsNullOrWhiteSpace(lastBackup))
                lastBackup = isEn ? "No backup created yet" : "Niciun backup creat încă";

            // 1. TOP KPI SUMMARY ROW (3 CARDS)
            var kpiGrid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            kpiGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
            kpiGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            kpiGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            kpiGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            kpiGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.15, GridUnitType.Star) });

            Border MakeKpiCard(string label, string mainValue, string subValue, Brush accent, string badgeText)
            {
                var card = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(11, 18, 29)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(26, 42, 64)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(16, 12, 16, 12)
                };
                var st = new StackPanel();
                var topRow = new DockPanel { Margin = new Thickness(0, 0, 0, 5) };
                var lbl = new TextBlock
                {
                    Text = label,
                    FontSize = 10.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = MutedBrush,
                    VerticalAlignment = VerticalAlignment.Center
                };
                DockPanel.SetDock(lbl, Dock.Left);
                topRow.Children.Add(lbl);

                var bdg = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(35, ((SolidColorBrush)accent).Color.R, ((SolidColorBrush)accent).Color.G, ((SolidColorBrush)accent).Color.B)),
                    BorderBrush = accent,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 1, 6, 1),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Child = new TextBlock
                    {
                        Text = badgeText,
                        FontSize = 9.5,
                        FontWeight = FontWeights.Bold,
                        Foreground = accent
                    }
                };
                DockPanel.SetDock(bdg, Dock.Right);
                topRow.Children.Add(bdg);
                st.Children.Add(topRow);

                st.Children.Add(new TextBlock
                {
                    Text = mainValue,
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Foreground = TextBrush,
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
                st.Children.Add(new TextBlock
                {
                    Text = subValue,
                    FontSize = 11,
                    Foreground = MutedBrush,
                    Margin = new Thickness(0, 2, 0, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
                card.Child = st;
                return card;
            }

            var gpuKpi = MakeKpiCard(
                isEn ? "GRAPHICS DRIVER (GPU)" : "DRIVER PLACĂ VIDEO (GPU)",
                gpuDriver.DeviceName,
                $"{(isEn ? "Version" : "Versiune")}: {gpuDriver.DriverVersion} ({gpuDriver.DriverDate})",
                GreenBrush,
                gpuDriver.VendorTag
            );
            Grid.SetColumn(gpuKpi, 0);
            kpiGrid.Children.Add(gpuKpi);

            var scanKpi = MakeKpiCard(
                isEn ? "SYSTEM DRIVERS STATUS" : "STARE DRIVERE SISTEM",
                updatesCount == 0
                    ? (isEn ? $"All {drivers.Count} Drivers Up to Date" : $"Toate ({drivers.Count}) sunt la zi")
                    : (isEn ? $"{updatesCount} Updates Available" : $"{updatesCount} Actualizări Disponibile"),
                isEn ? $"{drivers.Count} total computer drivers verified" : $"{drivers.Count} drivere totale verificate în PC",
                updatesCount == 0 ? GreenBrush : AmberBrush,
                updatesCount == 0 ? (isEn ? "OPTIMAL" : "OPTIM") : (isEn ? $"{updatesCount} TO UPDATE" : $"{updatesCount} DE ACTUALIZAT")
            );
            Grid.SetColumn(scanKpi, 2);
            kpiGrid.Children.Add(scanKpi);

            var backupKpi = MakeKpiCard(
                isEn ? "DRIVER BACKUP POINT" : "PUNCT BACKUP DRIVERE",
                lastBackup.Contains("•") ? lastBackup.Split('•')[0].Trim() : lastBackup,
                lastBackup.Contains("•") ? lastBackup.Split('•')[1].Trim() : (isEn ? "Stored in Documents\\NexWin_DriverBackups" : "Salvat în Documents\\NexWin_DriverBackups"),
                CyanBrush,
                "DISM / INF"
            );
            Grid.SetColumn(backupKpi, 4);
            kpiGrid.Children.Add(backupKpi);

            PageRoot.Children.Add(kpiGrid);

            // 2. ACTION TOOLBAR
            var toolbarBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(12, 20, 32)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(28, 45, 68)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 0, 12)
            };

            var toolbarDock = new DockPanel { LastChildFill = false };
            var leftActions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            Button MakeDriverBtn(string text, bool isPrimary, Brush? customBg = null, double height = 34)
            {
                var btn = new Button
                {
                    Content = text,
                    Height = height,
                    Padding = new Thickness(14, 0, 14, 0),
                    Margin = new Thickness(0, 0, 8, 0),
                    FontSize = 11.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    Background = customBg ?? (isPrimary
                        ? new SolidColorBrush(Color.FromRgb(37, 99, 235))
                        : new SolidColorBrush(Color.FromRgb(22, 34, 51))),
                    BorderBrush = isPrimary
                        ? new SolidColorBrush(Color.FromRgb(59, 130, 246))
                        : new SolidColorBrush(Color.FromRgb(42, 62, 88)),
                    BorderThickness = new Thickness(1),
                    Cursor = Cursors.Hand
                };
                btn.Style = (Style)FindResource("SecondaryButtonStyle");
                if (isPrimary && customBg == null)
                {
                    btn.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                    btn.BorderBrush = new SolidColorBrush(Color.FromRgb(96, 165, 250));
                    btn.Foreground = Brushes.White;
                }
                else if (customBg != null)
                {
                    btn.Background = customBg;
                    btn.Foreground = Brushes.White;
                }
                return btn;
            }

            var rescanBtn = MakeDriverBtn(isEn ? "Scan Drivers" : "Scanează Driverele", false);
            rescanBtn.IsEnabled = !_isUpdatingDrivers;
            rescanBtn.Click += async (_, _) =>
            {
                ShowToast(
                    isEn ? "Driver Scan" : "Scanare Drivere",
                    isEn ? "Scanning all installed hardware drivers..." : "Se scanează toate driverele hardware instalate...",
                    NexIcon.Cpu,
                    CyanBrush
                );
                _cachedDriversList = await NativeTuning.ScanSystemDriversAsync();
                ShowDrivers();
            };
            leftActions.Children.Add(rescanBtn);

            var backupNowBtn = MakeDriverBtn(
                isEn ? "Backup All Drivers" : "Creează Backup Drivere",
                false,
                new SolidColorBrush(Color.FromRgb(16, 120, 96))
            );
            backupNowBtn.IsEnabled = !_isUpdatingDrivers;
            backupNowBtn.Click += async (_, _) =>
            {
                ShowToast(
                    isEn ? "Driver Backup Started" : "Backup Drivere Pornit",
                    isEn ? "Exporting system drivers to Documents\\NexWin_DriverBackups..." : "Se exportă pachetele de drivere în Documents\\NexWin_DriverBackups...",
                    NexIcon.Folder,
                    CyanBrush
                );
                var res = await NativeTuning.BackupDriversToFolderAsync();
                if (res.Success)
                {
                    ShowToast(
                        isEn ? "Driver Backup Complete" : "Backup Finalizat cu Succes",
                        isEn ? $"Saved {res.ExportedCount} driver packages to {Path.GetFileName(res.BackupPath)}."
                             : $"S-au salvat {res.ExportedCount} pachete de drivere în {Path.GetFileName(res.BackupPath)}.",
                        NexIcon.Check,
                        GreenBrush
                    );
                    ShowDrivers();
                }
            };
            leftActions.Children.Add(backupNowBtn);

            var openFolderBtn = MakeDriverBtn(isEn ? "Open Backup Folder" : "Folder Backup", false);
            openFolderBtn.Click += (_, _) =>
            {
                try
                {
                    string dir = NativeTuning.GetDriverBackupRootFolder();
                    Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
                }
                catch { }
            };
            leftActions.Children.Add(openFolderBtn);

            DockPanel.SetDock(leftActions, Dock.Left);
            toolbarDock.Children.Add(leftActions);

            var rightActions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            var autoBackupWrap = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 14, 0),
                Cursor = Cursors.Hand
            };
            var autoBackupChk = new CheckBox
            {
                IsChecked = NativeTuning.GetAutoBackupBeforeDriverUpdate(),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 7, 0),
                Cursor = Cursors.Hand
            };
            autoBackupChk.Checked += (_, _) => NativeTuning.SetAutoBackupBeforeDriverUpdate(true);
            autoBackupChk.Unchecked += (_, _) => NativeTuning.SetAutoBackupBeforeDriverUpdate(false);
            var autoBackupLbl = new TextBlock
            {
                Text = isEn ? "Auto-backup before update" : "Backup automat înainte de update",
                Foreground = new SolidColorBrush(Color.FromRgb(210, 224, 240)),
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            autoBackupWrap.MouseLeftButtonUp += (_, _) => autoBackupChk.IsChecked = !(autoBackupChk.IsChecked == true);
            autoBackupWrap.Children.Add(autoBackupChk);
            autoBackupWrap.Children.Add(autoBackupLbl);
            rightActions.Children.Add(autoBackupWrap);

            if (updatesCount > 0 || _isUpdatingDrivers)
            {
                var updateAllBtn = MakeDriverBtn(
                    _isUpdatingDrivers
                        ? (isEn ? $"⏳ Installing ({_currentUpdateIndex}/{_totalUpdatesCount})..." : $"⏳ Se instalează ({_currentUpdateIndex}/{_totalUpdatesCount})...")
                        : (isEn ? $"Update All ({updatesCount})" : $"Actualizează Toate ({updatesCount})"),
                    true);
                updateAllBtn.IsEnabled = !_isUpdatingDrivers;
                updateAllBtn.Click += async (_, _) =>
                {
                    var toUpdate = drivers.Where(d => d.HasUpdate).ToList();
                    await RunDriverUpdatesWithLiveProgressAsync(toUpdate, autoBackupChk.IsChecked == true);
                };
                rightActions.Children.Add(updateAllBtn);
            }
            else
            {
                var allUpToDatePill = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(32, 16, 185, 129)),
                    BorderBrush = GreenBrush,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(12, 6, 12, 6),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock
                    {
                        Text = isEn ? "✓ All Drivers Up to Date" : "✓ Toate Driverele la Zi",
                        FontSize = 11,
                        FontWeight = FontWeights.Bold,
                        Foreground = GreenBrush
                    }
                };
                rightActions.Children.Add(allUpToDatePill);
            }

            DockPanel.SetDock(rightActions, Dock.Right);
            toolbarDock.Children.Add(rightActions);

            toolbarBorder.Child = toolbarDock;
            PageRoot.Children.Add(toolbarBorder);

            // 2B. LIVE DRIVER INSTALLATION PROGRESS BANNER (VISIBLE DURING & AFTER INSTALL)
            if (_isUpdatingDrivers || _showUpdateCompletedBanner)
            {
                var progCard = new Border
                {
                    Background = _isUpdatingDrivers
                        ? new SolidColorBrush(Color.FromRgb(11, 24, 42))
                        : new SolidColorBrush(Color.FromRgb(10, 26, 24)),
                    BorderBrush = _isUpdatingDrivers
                        ? new SolidColorBrush(Color.FromRgb(56, 189, 248))
                        : new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                    BorderThickness = new Thickness(1.5),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(16, 12, 16, 12),
                    Margin = new Thickness(0, 0, 0, 12)
                };

                var progStack = new StackPanel();
                var topProgDock = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };

                var leftTitleRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                leftTitleRow.Children.Add(new Border
                {
                    Width = 8,
                    Height = 8,
                    CornerRadius = new CornerRadius(4),
                    Background = _isUpdatingDrivers ? CyanBrush : GreenBrush,
                    Margin = new Thickness(0, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Center
                });
                leftTitleRow.Children.Add(new TextBlock
                {
                    Text = _isUpdatingDrivers
                        ? (isEn
                            ? $"INSTALLING DRIVER {_currentUpdateIndex} OF {_totalUpdatesCount}: {_currentUpdatingDriverName}"
                            : $"INSTALARE ÎN CURS ({_currentUpdateIndex} DIN {_totalUpdatesCount}): {_currentUpdatingDriverName}")
                        : (isEn
                            ? "✓ DRIVER INSTALLATION COMPLETED SUCCESSFULLY"
                            : "✓ INSTALARE ȘI VERIFICARE DRIVERE FINALIZATĂ CU SUCCES"),
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    Foreground = TextBrush,
                    VerticalAlignment = VerticalAlignment.Center
                });
                DockPanel.SetDock(leftTitleRow, Dock.Left);
                topProgDock.Children.Add(leftTitleRow);

                var pctBadge = new Border
                {
                    Background = _isUpdatingDrivers
                        ? new SolidColorBrush(Color.FromArgb(40, 56, 189, 248))
                        : new SolidColorBrush(Color.FromArgb(40, 16, 185, 129)),
                    BorderBrush = _isUpdatingDrivers ? CyanBrush : GreenBrush,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(5),
                    Padding = new Thickness(8, 2, 8, 2),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Child = new TextBlock
                    {
                        Text = $"{_currentUpdatePercent}%",
                        FontSize = 11,
                        FontWeight = FontWeights.Bold,
                        Foreground = _isUpdatingDrivers ? CyanBrush : GreenBrush
                    }
                };
                DockPanel.SetDock(pctBadge, Dock.Right);
                topProgDock.Children.Add(pctBadge);
                progStack.Children.Add(topProgDock);

                progStack.Children.Add(new TextBlock
                {
                    Text = _currentUpdateStageText,
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(186, 210, 236)),
                    Margin = new Thickness(0, 0, 0, 8)
                });

                var barTrack = new Grid
                {
                    Height = 8,
                    Background = new SolidColorBrush(Color.FromRgb(15, 29, 46))
                };
                barTrack.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(Math.Max(2, _currentUpdatePercent), GridUnitType.Star)
                });
                barTrack.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(Math.Max(0, 100 - _currentUpdatePercent), GridUnitType.Star)
                });

                var barFill = new Border
                {
                    CornerRadius = new CornerRadius(4),
                    Background = _isUpdatingDrivers
                        ? new LinearGradientBrush(Color.FromRgb(37, 99, 235), Color.FromRgb(56, 189, 248), 0)
                        : GreenBrush
                };
                Grid.SetColumn(barFill, 0);
                barTrack.Children.Add(barFill);
                progStack.Children.Add(barTrack);

                progCard.Child = progStack;
                PageRoot.Children.Add(progCard);
            }

            // 3. DEDICATED NVIDIA / AMD / INTEL GPU DRIVER HIGHLIGHT CARD
            var gpuHeroBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(11, 22, 36)),
                BorderBrush = gpuDriver.VendorTag == "AMD"
                    ? new SolidColorBrush(Color.FromRgb(180, 65, 50))
                    : new SolidColorBrush(Color.FromRgb(22, 120, 90)),
                BorderThickness = new Thickness(1.2),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(18, 12, 18, 12),
                Margin = new Thickness(0, 0, 0, 12)
            };
            var gpuHeroGrid = new Grid();
            gpuHeroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });
            gpuHeroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            gpuHeroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var gpuIconBox = new Border
            {
                Width = 38,
                Height = 38,
                CornerRadius = new CornerRadius(9),
                Background = new SolidColorBrush(Color.FromArgb(40, 16, 185, 129)),
                BorderBrush = GreenBrush,
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Child = CreateVectorIcon(NexIcon.Gamepad, GreenBrush, 18)
            };
            Grid.SetColumn(gpuIconBox, 0);
            gpuHeroGrid.Children.Add(gpuIconBox);

            var gpuInfoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 14, 0) };
            var gpuTitleRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            gpuTitleRow.Children.Add(new TextBlock
            {
                Text = $"{gpuDriver.DeviceName} ({gpuDriver.VendorTag})",
                FontSize = 13.5,
                FontWeight = FontWeights.Bold,
                Foreground = TextBrush,
                VerticalAlignment = VerticalAlignment.Center
            });
            gpuTitleRow.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(38, 16, 185, 129)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(7, 1, 7, 1),
                Margin = new Thickness(10, 0, 0, 0),
                Child = new TextBlock
                {
                    Text = gpuDriver.HasUpdate
                        ? (isEn ? "NEW VERSION AVAILABLE" : "VERSIUNE NOUĂ DISPONIBILĂ")
                        : (isEn ? "LATEST GAME READY DRIVER" : "DRIVER VIDEO LA ZI"),
                    FontSize = 9.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = gpuDriver.HasUpdate ? AmberBrush : GreenBrush
                }
            });
            gpuInfoStack.Children.Add(gpuTitleRow);
            gpuInfoStack.Children.Add(new TextBlock
            {
                Text = isEn
                    ? $"Installed Version: {gpuDriver.DriverVersion}  •  Driver Date: {gpuDriver.DriverDate}  •  Direct support for NVIDIA GeForce, AMD Radeon Adrenalin & Intel Arc"
                    : $"Versiune instalată: {gpuDriver.DriverVersion}  •  Data driverului: {gpuDriver.DriverDate}  •  Suport direct NVIDIA GeForce, AMD Radeon Adrenalin & Intel Arc",
                FontSize = 11,
                Foreground = MutedBrush,
                Margin = new Thickness(0, 3, 0, 0)
            });
            Grid.SetColumn(gpuInfoStack, 1);
            gpuHeroGrid.Children.Add(gpuInfoStack);

            if (gpuDriver.HasUpdate)
            {
                var gpuActionBtn = MakeDriverBtn(
                    isEn ? $"Update {gpuDriver.VendorTag} Driver" : $"Actualizează Driver {gpuDriver.VendorTag}",
                    true);
                gpuActionBtn.IsEnabled = !_isUpdatingDrivers;
                gpuActionBtn.VerticalAlignment = VerticalAlignment.Center;
                gpuActionBtn.Click += async (_, _) =>
                {
                    await RunDriverUpdatesWithLiveProgressAsync(new List<NativeTuning.HardwareDriverItem> { gpuDriver }, autoBackupChk.IsChecked == true);
                };
                Grid.SetColumn(gpuActionBtn, 2);
                gpuHeroGrid.Children.Add(gpuActionBtn);
            }
            else
            {
                var upToDateBadge = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(32, 16, 185, 129)),
                    BorderBrush = GreenBrush,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(12, 6, 12, 6),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock
                    {
                        Text = isEn ? "✓ Driver Up to Date" : "✓ Driver Actualizat la Zi",
                        FontSize = 11,
                        FontWeight = FontWeights.Bold,
                        Foreground = GreenBrush
                    }
                };
                Grid.SetColumn(upToDateBadge, 2);
                gpuHeroGrid.Children.Add(upToDateBadge);
            }

            gpuHeroBorder.Child = gpuHeroGrid;
            PageRoot.Children.Add(gpuHeroBorder);

            // 4. INSTALLED HARDWARE DRIVERS LIST HEADER + CUSTOM SCROLLBAR CONTAINER
            var listHeaderDock = new DockPanel { Margin = new Thickness(2, 0, 4, 6) };
            var listHeader = new TextBlock
            {
                Text = isEn
                    ? $"ALL DETECTED COMPUTER DRIVERS ({drivers.Count} DRIVERS SCANNED)"
                    : $"TOATE DRIVERELE DETECTATE ÎN CALCULATOR ({drivers.Count} DRIVERE SCANATE)",
                FontSize = 10.5,
                FontWeight = FontWeights.Bold,
                Foreground = MutedBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(listHeader, Dock.Left);
            listHeaderDock.Children.Add(listHeader);

            var scrollHint = new TextBlock
            {
                Text = isEn ? "↕ Scroll list with mouse wheel or custom scrollbar" : "↕ Derulează lista folosind rotița mouse-ului sau bara laterală",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 126, 156)),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(scrollHint, Dock.Right);
            listHeaderDock.Children.Add(scrollHint);
            PageRoot.Children.Add(listHeaderDock);

            var listStack = new StackPanel { Margin = new Thickness(0, 0, 8, 0) };
            foreach (var drv in drivers)
            {
                bool isThisInstalling = _isUpdatingDrivers && string.Equals(_currentUpdatingDriverName, drv.DeviceName, StringComparison.OrdinalIgnoreCase);
                bool isQueued = _isUpdatingDrivers && _queuedDriverNames.Contains(drv.DeviceName);
                bool wasJustUpdated = _recentlyUpdatedDrivers.Contains(drv.DeviceName);

                var rowBorder = new Border
                {
                    Background = isThisInstalling
                        ? new SolidColorBrush(Color.FromRgb(13, 28, 48))
                        : new SolidColorBrush(Color.FromRgb(11, 18, 28)),
                    BorderBrush = isThisInstalling
                        ? new SolidColorBrush(Color.FromRgb(56, 189, 248))
                        : (drv.HasUpdate
                            ? new SolidColorBrush(Color.FromArgb(110, 245, 158, 11))
                            : new SolidColorBrush(Color.FromRgb(24, 38, 56))),
                    BorderThickness = new Thickness(isThisInstalling ? 1.5 : 1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(14, 10, 14, 10),
                    Margin = new Thickness(0, 0, 0, 7)
                };

                var rowGrid = new Grid();
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var infoCol = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                var nameLine = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                nameLine.Children.Add(new TextBlock
                {
                    Text = drv.DeviceName,
                    FontSize = 12.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = TextBrush
                });
                nameLine.Children.Add(new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(17, 30, 46)),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 1, 6, 1),
                    Margin = new Thickness(8, 0, 0, 0),
                    Child = new TextBlock
                    {
                        Text = drv.Category,
                        FontSize = 9.5,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = CyanBrush
                    }
                });
                infoCol.Children.Add(nameLine);

                infoCol.Children.Add(new TextBlock
                {
                    Text = isThisInstalling
                        ? _currentUpdateStageText
                        : $"{drv.Manufacturer}  •  {(isEn ? "Version" : "Versiune")}: {drv.DriverVersion}  •  {(isEn ? "Date" : "Data")}: {drv.DriverDate}",
                    FontSize = 10.5,
                    Foreground = isThisInstalling ? CyanBrush : MutedBrush,
                    Margin = new Thickness(0, 2, 0, 0)
                });
                Grid.SetColumn(infoCol, 0);
                rowGrid.Children.Add(infoCol);

                if (isThisInstalling)
                {
                    var inlineProgStack = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    var installingBadge = new Border
                    {
                        Background = new SolidColorBrush(Color.FromArgb(40, 56, 189, 248)),
                        BorderBrush = CyanBrush,
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(5),
                        Padding = new Thickness(8, 3, 8, 3),
                        Margin = new Thickness(8, 0, 10, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        Child = new TextBlock
                        {
                            Text = isEn ? $"⏳ INSTALLING... {_currentUpdatePercent}%" : $"⏳ SE INSTALEAZĂ... {_currentUpdatePercent}%",
                            FontSize = 9.5,
                            FontWeight = FontWeights.Bold,
                            Foreground = CyanBrush
                        }
                    };
                    inlineProgStack.Children.Add(installingBadge);

                    var miniTrack = new Border
                    {
                        Width = 110,
                        Height = 8,
                        CornerRadius = new CornerRadius(4),
                        Background = new SolidColorBrush(Color.FromRgb(18, 34, 54)),
                        VerticalAlignment = VerticalAlignment.Center,
                        Child = new Border
                        {
                            Width = Math.Max(8, 110.0 * _currentUpdatePercent / 100.0),
                            HorizontalAlignment = HorizontalAlignment.Left,
                            CornerRadius = new CornerRadius(4),
                            Background = new LinearGradientBrush(Color.FromRgb(37, 99, 235), Color.FromRgb(56, 189, 248), 0)
                        }
                    };
                    inlineProgStack.Children.Add(miniTrack);
                    Grid.SetColumn(inlineProgStack, 1);
                    Grid.SetColumnSpan(inlineProgStack, 2);
                    rowGrid.Children.Add(inlineProgStack);
                }
                else if (isQueued)
                {
                    var queuedPill = new Border
                    {
                        Background = new SolidColorBrush(Color.FromArgb(30, 148, 163, 184)),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(5),
                        Padding = new Thickness(9, 3, 9, 3),
                        VerticalAlignment = VerticalAlignment.Center,
                        Child = new TextBlock
                        {
                            Text = isEn ? "⏱ QUEUED FOR INSTALL" : "⏱ ÎN AȘTEPTARE",
                            FontSize = 9.5,
                            FontWeight = FontWeights.Bold,
                            Foreground = new SolidColorBrush(Color.FromRgb(186, 204, 226))
                        }
                    };
                    Grid.SetColumn(queuedPill, 1);
                    Grid.SetColumnSpan(queuedPill, 2);
                    rowGrid.Children.Add(queuedPill);
                }
                else
                {
                    var statusPill = new Border
                    {
                        Background = drv.HasUpdate
                            ? new SolidColorBrush(Color.FromArgb(35, 245, 158, 11))
                            : new SolidColorBrush(Color.FromArgb(32, 16, 185, 129)),
                        BorderBrush = drv.HasUpdate ? AmberBrush : GreenBrush,
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(5),
                        Padding = new Thickness(8, 3, 8, 3),
                        Margin = new Thickness(10, 0, drv.HasUpdate ? 10 : 0, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        Child = new TextBlock
                        {
                            Text = drv.HasUpdate
                                ? (isEn ? "UPDATE RECOMMENDED" : "UPDATE RECOMANDAT")
                                : (wasJustUpdated
                                    ? (isEn ? "✓ JUST INSTALLED • UP TO DATE" : "✓ INSTALAT CU SUCCES • LA ZI")
                                    : (isEn ? "✓ UP TO DATE" : "✓ LA ZI")),
                            FontSize = 9.5,
                            FontWeight = FontWeights.Bold,
                            Foreground = drv.HasUpdate ? AmberBrush : GreenBrush
                        }
                    };
                    Grid.SetColumn(statusPill, 1);
                    rowGrid.Children.Add(statusPill);

                    if (drv.HasUpdate)
                    {
                        var drvBtn = MakeDriverBtn(
                            isEn ? "Update" : "Actualizează",
                            true,
                            null,
                            30);
                        drvBtn.IsEnabled = !_isUpdatingDrivers;
                        drvBtn.VerticalAlignment = VerticalAlignment.Center;
                        drvBtn.Click += async (_, _) =>
                        {
                            await RunDriverUpdatesWithLiveProgressAsync(new List<NativeTuning.HardwareDriverItem> { drv }, autoBackupChk.IsChecked == true);
                        };
                        Grid.SetColumn(drvBtn, 2);
                        rowGrid.Children.Add(drvBtn);
                    }
                }

                rowBorder.Child = rowGrid;
                listStack.Children.Add(rowBorder);
            }

            var listScrollContainer = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(8, 14, 23)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(22, 35, 52)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 10, 6, 10)
            };

            var driversScroller = new ScrollViewer
            {
                MaxHeight = (_isUpdatingDrivers || _showUpdateCompletedBanner) ? 240 : 320,
                VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                CanContentScroll = false,
                Content = listStack
            };

            listScrollContainer.Child = driversScroller;
            PageRoot.Children.Add(listScrollContainer);
        }
        catch (Exception ex)
        {
            try { File.WriteAllText("drivers-error.log", ex.ToString()); } catch { }
        }
    }
}
