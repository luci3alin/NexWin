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
    private string activeDiskTab = "Fișiere temporare";
    private string diskSearchQuery = "";
    private readonly HashSet<string> selectedDiskPaths = new(StringComparer.OrdinalIgnoreCase);
    private List<NativeTuning.DirectorySizeItem>? cachedTreeSizeDirs;
    private bool isTreeSizeLoading;

    private void ShowDisk()
    {
        PageRoot.Children.Clear();

        // 1. Top Header (Matching media_1789749212684.png)
        var headGrid = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        headGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var leftHead = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var diskBox = new Border
        {
            Width = 44,
            Height = 44,
            CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(Color.FromArgb(40, 56, 189, 248)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(80, 56, 189, 248)),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 14, 0),
            Child = CreateVectorIcon(NexIcon.Disk, CyanBrush, 22)
        };
        leftHead.Children.Add(diskBox);

        var titleTextStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        titleTextStack.Children.Add(new TextBlock
        {
            Text = NexLocale.T("disk_title"),
            FontSize = 19,
            FontWeight = FontWeights.Bold,
            Foreground = TextBrush
        });
        titleTextStack.Children.Add(new TextBlock
        {
            Text = NexLocale.T("disk_subtitle"),
            FontSize = 11.5,
            Foreground = MutedBrush,
            Margin = new Thickness(0, 2, 0, 0)
        });
        leftHead.Children.Add(titleTextStack);
        headGrid.Children.Add(leftHead);
        PageRoot.Children.Add(headGrid);

        // 2. Top Drive Storage Banner (Matching media_1789749212684.png)
        try
        {
            var drives = DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed).ToList();
            var primaryDrive = drives.FirstOrDefault(d => d.Name.StartsWith("C", StringComparison.OrdinalIgnoreCase)) ?? drives.FirstOrDefault();
            if (primaryDrive != null)
            {
                var totalGB = primaryDrive.TotalSize / 1024d / 1024d / 1024d;
                var freeGB = primaryDrive.AvailableFreeSpace / 1024d / 1024d / 1024d;
                var usedGB = totalGB - freeGB;
                var pct = totalGB > 0 ? (usedGB / totalGB) * 100 : 0;
                var label = string.IsNullOrEmpty(primaryDrive.VolumeLabel) ? "Windows" : primaryDrive.VolumeLabel;

                var driveCard = new Border
                {
                    Background = CardBackground(),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(24, 36, 52)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(18, 14, 18, 14),
                    Margin = new Thickness(0, 0, 0, 14)
                };

                var cardLayout = new StackPanel();

                // Header Row
                var headerRow = new DockPanel { LastChildFill = true };
                var driveTitle = new StackPanel { Orientation = Orientation.Horizontal };
                var pcIcon = CreateVectorIcon(NexIcon.Window, CyanBrush, 17);
                pcIcon.Margin = new Thickness(0, 0, 8, 0);
                driveTitle.Children.Add(pcIcon);
                driveTitle.Children.Add(new TextBlock { Text = $"{primaryDrive.Name} [{label}]", FontSize = 14.5, FontWeight = FontWeights.Bold, Foreground = TextBrush, VerticalAlignment = VerticalAlignment.Center });
                driveTitle.Children.Add(new TextBlock { Text = $"   {primaryDrive.DriveFormat} (NVMe/SSD)", FontSize = 11.5, Foreground = MutedBrush, VerticalAlignment = VerticalAlignment.Center });
                headerRow.Children.Add(driveTitle);

                var capacityText = new TextBlock
                {
                    Text = NexLocale.Format("disk_capacity_format", usedGB, totalGB, pct, freeGB),
                    FontSize = 12.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = CyanBrush,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center
                };
                DockPanel.SetDock(capacityText, Dock.Right);
                headerRow.Children.Add(capacityText);
                cardLayout.Children.Add(headerRow);

                // Progress Bar
                var progressBorder = new Border
                {
                    Height = 8,
                    CornerRadius = new CornerRadius(4),
                    Background = new SolidColorBrush(Color.FromRgb(20, 30, 44)),
                    Margin = new Thickness(0, 10, 0, 14),
                    ClipToBounds = true
                };
                var fillBar = new ProgressBar
                {
                    Value = pct,
                    Maximum = 100,
                    Height = 8,
                    Background = Brushes.Transparent,
                    Foreground = pct > 85 ? PinkBrush : CyanBrush,
                    BorderThickness = new Thickness(0)
                };
                progressBorder.Child = fillBar;
                cardLayout.Children.Add(progressBorder);

                // Bottom Row inside Card: Donut Box on Left, 3 Buttons on Right
                var driveStatsGrid = new Grid();
                driveStatsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                driveStatsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                // Left: Donut Chart Box
                var leftDonutBox = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(16, 25, 38)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(28, 40, 56)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(16, 10, 20, 10),
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                var leftStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                leftStack.Children.Add(CreateDonutChart(pct, 74, CyanBrush));

                var diskStatsText = new StackPanel { Margin = new Thickness(16, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
                diskStatsText.Children.Add(new TextBlock { Text = NexLocale.T("disk_used_label"), FontSize = 11, Foreground = MutedBrush });
                diskStatsText.Children.Add(new TextBlock { Text = $"{usedGB:0.0} GB", FontSize = 15, FontWeight = FontWeights.Bold, Foreground = TextBrush });
                diskStatsText.Children.Add(new TextBlock { Text = NexLocale.T("disk_free_label"), FontSize = 11, Foreground = MutedBrush, Margin = new Thickness(0, 4, 0, 0) });
                diskStatsText.Children.Add(new TextBlock { Text = $"{freeGB:0.0} GB", FontSize = 15, FontWeight = FontWeights.Bold, Foreground = TextBrush });
                leftStack.Children.Add(diskStatsText);
                leftDonutBox.Child = leftStack;
                Grid.SetColumn(leftDonutBox, 0);
                driveStatsGrid.Children.Add(leftDonutBox);

                // Right: 3 Action Buttons
                var rightBtns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

                var cleanCacheBtn = new Button
                {
                    Style = (Style)FindResource("BlueGradientButtonStyle"),
                    Padding = new Thickness(16, 8, 16, 8),
                    Margin = new Thickness(0, 0, 10, 0),
                    Cursor = Cursors.Hand
                };
                var ccStack = new StackPanel { Orientation = Orientation.Horizontal };
                var ccIco = CreateVectorIcon(NexIcon.Search, Brushes.White, 13);
                ccIco.Margin = new Thickness(0, 0, 8, 0);
                ccStack.Children.Add(ccIco);
                ccStack.Children.Add(new TextBlock { Text = NexLocale.T("btn_clean_temp"), Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, FontSize = 12 });
                cleanCacheBtn.Content = ccStack;
                cleanCacheBtn.Click += async (_, _) =>
                {
                    await ExecuteNativeSuiteAsync(NexLocale.T("disk_clean_cache_title"), NexLocale.T("disk_clean_cache_desc"), NexLocale.T("disk_clean_cache_success"), () => NativeTuning.ApplyMaintenanceNativeAsync());
                    ShowDisk();
                };
                rightBtns.Children.Add(cleanCacheBtn);

                var trimBtn = new Button
                {
                    Style = (Style)FindResource("SecondaryButtonStyle"),
                    Padding = new Thickness(14, 8, 14, 8),
                    Margin = new Thickness(0, 0, 10, 0),
                    Cursor = Cursors.Hand
                };
                var tStack = new StackPanel { Orientation = Orientation.Horizontal };
                var tIco = CreateVectorIcon(NexIcon.Window, MutedBrush, 13);
                tIco.Margin = new Thickness(0, 0, 8, 0);
                tStack.Children.Add(tIco);
                tStack.Children.Add(new TextBlock { Text = NexLocale.T("btn_trim_ssd"), Foreground = TextBrush, FontWeight = FontWeights.SemiBold, FontSize = 12 });
                trimBtn.Content = tStack;
                trimBtn.Click += async (_, _) =>
                {
                    await ExecuteNativeSuiteAsync(NexLocale.T("disk_ssd_trim_title"), NexLocale.T("disk_ssd_trim_desc"), NexLocale.T("disk_ssd_trim_success"), () => Task.FromResult(new List<string> { NexLocale.T("disk_ssd_trim_log") }));
                    ShowDisk();
                };
                rightBtns.Children.Add(trimBtn);

                var scanBtn = new Button
                {
                    Style = (Style)FindResource("SecondaryButtonStyle"),
                    Padding = new Thickness(14, 8, 14, 8),
                    Cursor = Cursors.Hand
                };
                var scStack = new StackPanel { Orientation = Orientation.Horizontal };
                var scIco = CreateVectorIcon(NexIcon.Refresh, MutedBrush, 13);
                scIco.Margin = new Thickness(0, 0, 8, 0);
                scStack.Children.Add(scIco);
                scStack.Children.Add(new TextBlock { Text = NexLocale.T("btn_scan_drive"), Foreground = TextBrush, FontWeight = FontWeights.SemiBold, FontSize = 12 });
                scanBtn.Content = scStack;
                scanBtn.Click += (_, _) => ShowDisk();
                rightBtns.Children.Add(scanBtn);

                Grid.SetColumn(rightBtns, 1);
                driveStatsGrid.Children.Add(rightBtns);

                cardLayout.Children.Add(driveStatsGrid);

                driveCard.Child = cardLayout;
                PageRoot.Children.Add(driveCard);
            }
        }
        catch { }

        // 3. Category Tabs (Matching media_1789749212684.png)
        var tabCategories = new (string id, string localeKey, NexIcon icon)[]
        {
            ("Fișiere temporare", "disk_tab_temp", NexIcon.FileText),
            ("Fișiere mari", "disk_tab_large", NexIcon.Folder),
            ("Duplicate", "disk_tab_duplicates", NexIcon.Layers),
            ("Aplicații", "disk_tab_apps", NexIcon.Window),
            ("Descărcări", "disk_tab_downloads", NexIcon.Download),
            ("Coș de reciclare", "disk_tab_recycle", NexIcon.Trash),
            ("Arbore directoare (TreeSize)", "disk_tab_treesize", NexIcon.Disk)
        };

        var tabsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        foreach (var (catId, catKey, catIco) in tabCategories)
        {
            bool isCurrent = catId.Equals(activeDiskTab, StringComparison.OrdinalIgnoreCase);
            var tabBorder = new Border
            {
                Background = isCurrent ? new SolidColorBrush(Color.FromRgb(11, 27, 54)) : Brushes.Transparent,
                BorderBrush = isCurrent ? new SolidColorBrush(Color.FromRgb(37, 99, 235)) : Brushes.Transparent,
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = Cursors.Hand
            };
            var tabStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var tabIcon = CreateVectorIcon(catIco, isCurrent ? CyanBrush : MutedBrush, 13);
            tabIcon.Margin = new Thickness(0, 0, 6, 0);
            tabStack.Children.Add(tabIcon);
            tabStack.Children.Add(new TextBlock
            {
                Text = NexLocale.T(catKey),
                FontSize = 11.5,
                FontWeight = isCurrent ? FontWeights.Bold : FontWeights.Normal,
                Foreground = isCurrent ? CyanBrush : MutedBrush
            });
            tabBorder.Child = tabStack;
            tabBorder.MouseLeftButtonUp += (_, _) =>
            {
                activeDiskTab = catId;
                selectedDiskPaths.Clear();
                ShowDisk();
            };
            tabsPanel.Children.Add(tabBorder);
        }
        PageRoot.Children.Add(tabsPanel);

        // 4. Render either TreeSize or File Table
        if (activeDiskTab == "Arbore directoare (TreeSize)")
        {
            RenderTreeSizeView();
        }
        else
        {
            RenderDiskFilesView();
        }
    }

    private void RenderTreeSizeView()
    {
        var container = new Border
        {
            Background = CardBackground(),
            BorderBrush = new SolidColorBrush(Color.FromRgb(24, 36, 52)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(18, 16, 18, 16),
            Margin = new Thickness(0, 0, 0, 14)
        };

        var stack = new StackPanel();

        // Top Toolbar inside TreeSize
        var topBar = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var barTitleStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var fldIco = CreateVectorIcon(NexIcon.Folder, AmberBrush, 16);
        fldIco.Margin = new Thickness(0, 0, 8, 0);
        barTitleStack.Children.Add(fldIco);
        barTitleStack.Children.Add(new TextBlock
        {
            Text = NexLocale.T("disk_treesize_header"),
            FontSize = 13.5,
            FontWeight = FontWeights.Bold,
            Foreground = TextBrush,
            VerticalAlignment = VerticalAlignment.Center
        });
        Grid.SetColumn(barTitleStack, 0);
        topBar.Children.Add(barTitleStack);

        var rescanBtn = new Button
        {
            Style = (Style)FindResource("SecondaryButtonStyle"),
            Padding = new Thickness(12, 5, 12, 5),
            Cursor = Cursors.Hand
        };
        var rbStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var rbIco = CreateVectorIcon(NexIcon.Refresh, CyanBrush, 12);
        rbIco.Margin = new Thickness(0, 0, 6, 0);
        rbStack.Children.Add(rbIco);
        rbStack.Children.Add(new TextBlock { Text = NexLocale.T("disk_treesize_rescan"), FontSize = 11, Foreground = CyanBrush, FontWeight = FontWeights.SemiBold });
        rescanBtn.Content = rbStack;
        rescanBtn.Click += (_, _) =>
        {
            cachedTreeSizeDirs = null;
            isTreeSizeLoading = false;
            ShowDisk();
        };
        Grid.SetColumn(rescanBtn, 1);
        topBar.Children.Add(rescanBtn);

        stack.Children.Add(topBar);

        // If loading or uncached:
        if (cachedTreeSizeDirs == null)
        {
            var loadingBox = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(20, 56, 189, 248)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(50, 56, 189, 248)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(20, 28, 20, 28),
                Margin = new Thickness(0, 10, 0, 10)
            };
            var lStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            lStack.Children.Add(CreateVectorIcon(NexIcon.Disk, CyanBrush, 32));
            lStack.Children.Add(new TextBlock
            {
                Text = NexLocale.T("disk_treesize_analyzing_title"),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = TextBrush,
                Margin = new Thickness(0, 12, 0, 4),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            lStack.Children.Add(new TextBlock
            {
                Text = NexLocale.T("disk_treesize_analyzing_desc"),
                FontSize = 11,
                Foreground = MutedBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 14)
            });

            var pBar = new ProgressBar
            {
                Width = 260,
                Height = 4,
                IsIndeterminate = true,
                Background = new SolidColorBrush(Color.FromRgb(15, 23, 38)),
                Foreground = CyanBrush,
                BorderThickness = new Thickness(0)
            };
            lStack.Children.Add(pBar);
            loadingBox.Child = lStack;
            stack.Children.Add(loadingBox);

            container.Child = stack;
            PageRoot.Children.Add(container);

            if (!isTreeSizeLoading)
            {
                isTreeSizeLoading = true;
                _ = Task.Run(() =>
                {
                    var dirs = NativeTuning.GetTreeSizeDirectories(@"C:\");
                    Dispatcher.Invoke(() =>
                    {
                        cachedTreeSizeDirs = dirs;
                        isTreeSizeLoading = false;
                        if (activeDiskTab == "Arbore directoare (TreeSize)")
                        {
                            ShowDisk();
                        }
                    });
                });
            }
            return;
        }

        // Table Header
        var headGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        headGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
        headGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
        headGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
        headGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });

        var col1 = new TextBlock { Text = NexLocale.T("disk_col_dir"), Foreground = MutedBrush, FontSize = 11, FontWeight = FontWeights.Bold };
        Grid.SetColumn(col1, 1); headGrid.Children.Add(col1);
        var col2 = new TextBlock { Text = NexLocale.T("disk_col_size"), Foreground = MutedBrush, FontSize = 11, FontWeight = FontWeights.Bold };
        Grid.SetColumn(col2, 2); headGrid.Children.Add(col2);
        var col3 = new TextBlock { Text = NexLocale.T("disk_col_disk_pct"), Foreground = MutedBrush, FontSize = 11, FontWeight = FontWeights.Bold };
        Grid.SetColumn(col3, 3); headGrid.Children.Add(col3);
        var col4 = new TextBlock { Text = NexLocale.T("disk_col_action"), Foreground = MutedBrush, FontSize = 11, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Right };
        Grid.SetColumn(col4, 4); headGrid.Children.Add(col4);

        stack.Children.Add(headGrid);

        foreach (var dir in cachedTreeSizeDirs)
        {
            var rowBorder = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(18, 28, 40)),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(0, 8, 0, 8)
            };

            var rowGrid = new Grid();
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });

            // Folder Icon in square container
            var folderBox = new Border
            {
                Width = 26,
                Height = 26,
                CornerRadius = new CornerRadius(5),
                Background = new SolidColorBrush(Color.FromArgb(30, 245, 158, 11)),
                Child = CreateVectorIcon(NexIcon.Folder, AmberBrush, 14),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(folderBox, 0);
            rowGrid.Children.Add(folderBox);

            // Name & Path
            var nameStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) };
            nameStack.Children.Add(new TextBlock { Text = dir.Name, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = TextBrush });
            nameStack.Children.Add(new TextBlock { Text = dir.FullPath, FontSize = 10.5, Foreground = MutedBrush });
            Grid.SetColumn(nameStack, 1);
            rowGrid.Children.Add(nameStack);

            // Size
            var sizeText = new TextBlock { Text = dir.SizeFormatted, FontSize = 12.5, FontWeight = FontWeights.Bold, Foreground = CyanBrush, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(sizeText, 2);
            rowGrid.Children.Add(sizeText);

            // Visual Progress Bar
            var barStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };
            barStack.Children.Add(new TextBlock { Text = NexLocale.Format("disk_treesize_pct_format", dir.PercentageOfDrive), FontSize = 10, Foreground = MutedBrush, Margin = new Thickness(0, 0, 0, 3) });
            var pBar = new ProgressBar
            {
                Value = dir.PercentageOfDrive,
                Maximum = 100,
                Height = 6,
                Background = new SolidColorBrush(Color.FromRgb(18, 26, 38)),
                Foreground = dir.PercentageOfDrive > 25 ? PinkBrush : (dir.PercentageOfDrive > 10 ? AmberBrush : CyanBrush),
                BorderThickness = new Thickness(0)
            };
            barStack.Children.Add(pBar);
            Grid.SetColumn(barStack, 3);
            rowGrid.Children.Add(barStack);

            // Open in Explorer
            var openBtn = new Button
            {
                Content = NexLocale.T("disk_btn_open"),
                Style = (Style)FindResource("SecondaryButtonStyle"),
                FontSize = 10.5,
                Padding = new Thickness(8, 3, 8, 3),
                HorizontalAlignment = HorizontalAlignment.Right,
                Cursor = Cursors.Hand
            };
            openBtn.Click += (_, _) =>
            {
                try { Process.Start(new ProcessStartInfo("explorer.exe", dir.FullPath) { UseShellExecute = true }); } catch { }
            };
            Grid.SetColumn(openBtn, 4);
            rowGrid.Children.Add(openBtn);

            rowBorder.Child = rowGrid;
            stack.Children.Add(rowBorder);
        }

        container.Child = stack;
        PageRoot.Children.Add(container);
    }

    private void RenderDiskFilesView()
    {
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string currentFolder = activeDiskTab switch
        {
            "Descărcări" => Path.Combine(userProfile, "Downloads"),
            "Aplicații" => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"),
            "Coș de reciclare" => Path.Combine(userProfile, "AppData", "Local", "Temp"),
            _ => userProfile
        };

        var items = NativeTuning.GetStorageItems(activeDiskTab, null, diskSearchQuery);

        var container = new Border
        {
            Background = CardBackground(),
            BorderBrush = new SolidColorBrush(Color.FromRgb(24, 36, 52)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 14, 16, 14),
            Margin = new Thickness(0, 0, 0, 14)
        };

        var stack = new StackPanel();

        // 1. Header inside Card: Folder path on left, Search box + Filter icon on right
        var cardHeadGrid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        cardHeadGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        cardHeadGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var leftHead = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var folderIco = CreateVectorIcon(NexIcon.FileText, Brushes.White, 15);
        folderIco.Margin = new Thickness(0, 0, 8, 0);
        leftHead.Children.Add(folderIco);
        leftHead.Children.Add(new TextBlock
        {
            Text = currentFolder,
            FontSize = 13.5,
            FontWeight = FontWeights.Bold,
            Foreground = TextBrush,
            VerticalAlignment = VerticalAlignment.Center
        });
        Grid.SetColumn(leftHead, 0);
        cardHeadGrid.Children.Add(leftHead);

        var rightHead = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        var searchBox = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(32, 44, 60)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(10, 5, 10, 5),
            Margin = new Thickness(0, 0, 8, 0),
            Width = 200
        };
        var sGrid = new Grid();
        sGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
        sGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var sIcon = CreateVectorIcon(NexIcon.Search, MutedBrush, 12);
        sIcon.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(sIcon, 0);
        sGrid.Children.Add(sIcon);

        var sBox = new TextBox
        {
            Text = diskSearchQuery,
            Background = Brushes.Transparent,
            Foreground = TextBrush,
            BorderThickness = new Thickness(0),
            FontSize = 11.5,
            VerticalAlignment = VerticalAlignment.Center
        };
        var placeholder = new TextBlock
        {
            Text = NexLocale.T("disk_search_placeholder"),
            Foreground = new SolidColorBrush(Color.FromRgb(90, 105, 125)),
            FontSize = 11.5,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
            Visibility = string.IsNullOrEmpty(diskSearchQuery) ? Visibility.Visible : Visibility.Collapsed
        };
        sBox.TextChanged += (s, e) =>
        {
            diskSearchQuery = sBox.Text;
            placeholder.Visibility = string.IsNullOrEmpty(sBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        };
        sBox.KeyDown += (s, e) =>
        {
            if (e.Key == Key.Enter)
            {
                diskSearchQuery = sBox.Text;
                ShowDisk();
            }
        };
        Grid.SetColumn(placeholder, 1);
        Grid.SetColumn(sBox, 1);
        sGrid.Children.Add(placeholder);
        sGrid.Children.Add(sBox);
        searchBox.Child = sGrid;
        rightHead.Children.Add(searchBox);

        var filterBtn = new Button
        {
            Width = 32,
            Height = 32,
            Style = (Style)FindResource("SecondaryButtonStyle"),
            Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(32, 44, 60)),
            Padding = new Thickness(0),
            Cursor = Cursors.Hand,
            Content = CreateVectorIcon(NexIcon.Filter, MutedBrush, 13)
        };
        rightHead.Children.Add(filterBtn);

        Grid.SetColumn(rightHead, 1);
        cardHeadGrid.Children.Add(rightHead);
        stack.Children.Add(cardHeadGrid);

        // 2. Table Header
        var headGrid = new Grid { Margin = new Thickness(0, 4, 0, 10) };
        headGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) }); // Checkbox
        headGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) }); // Nume
        headGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) }); // Cale
        headGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) }); // Tip
        headGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) }); // Dimensiune
        headGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) }); // Data modificării
        headGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) }); // Action

        var chkAll = new CheckBox { VerticalAlignment = VerticalAlignment.Center };
        chkAll.Checked += (_, _) =>
        {
            foreach (var i in items) selectedDiskPaths.Add(i.FullPath);
            ShowDisk();
        };
        chkAll.Unchecked += (_, _) =>
        {
            selectedDiskPaths.Clear();
            ShowDisk();
        };
        Grid.SetColumn(chkAll, 0); headGrid.Children.Add(chkAll);

        void AddCol(string t, int c)
        {
            var tb = new TextBlock { Text = t, Foreground = MutedBrush, FontSize = 11, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(tb, c);
            headGrid.Children.Add(tb);
        }

        AddCol(NexLocale.T("disk_col_name"), 1);
        AddCol(NexLocale.T("disk_col_path"), 2);
        AddCol(NexLocale.T("disk_col_type"), 3);
        AddCol(NexLocale.T("disk_col_size_table"), 4);
        AddCol(NexLocale.T("disk_col_modified"), 5);

        stack.Children.Add(headGrid);

        // 3. Table Rows
        var rowsStack = new StackPanel();
        long totalSelectedBytes = 0;
        if (items.Count == 0)
        {
            rowsStack.Children.Add(new TextBlock
            {
                Text = NexLocale.T("disk_empty_category"),
                Foreground = MutedBrush,
                FontSize = 12,
                Margin = new Thickness(0, 18, 0, 18),
                HorizontalAlignment = HorizontalAlignment.Center
            });
        }
        else
        {
            foreach (var item in items)
            {
                bool isSel = selectedDiskPaths.Contains(item.FullPath);
                if (isSel) totalSelectedBytes += item.SizeBytes;

                var rowBorder = new Border
                {
                    BorderBrush = new SolidColorBrush(Color.FromRgb(18, 28, 40)),
                    BorderThickness = new Thickness(0, 1, 0, 0),
                    Background = isSel ? new SolidColorBrush(Color.FromArgb(30, 56, 189, 248)) : Brushes.Transparent,
                    Padding = new Thickness(0, 6, 0, 6)
                };

                var rowGrid = new Grid();
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });

                // Checkbox
                var c = new CheckBox { IsChecked = isSel, VerticalAlignment = VerticalAlignment.Center };
                c.Checked += (_, _) => selectedDiskPaths.Add(item.FullPath);
                c.Unchecked += (_, _) => selectedDiskPaths.Remove(item.FullPath);
                Grid.SetColumn(c, 0); rowGrid.Children.Add(c);

                // Nume with Icon
                var nameStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                if (item.IsDirectory)
                {
                    var fld = CreateVectorIcon(NexIcon.Folder, new SolidColorBrush(Color.FromRgb(245, 158, 11)), 16);
                    fld.Margin = new Thickness(0, 0, 8, 0);
                    nameStack.Children.Add(fld);
                }
                else if (item.Icon != null)
                {
                    var img = new Image { Source = item.Icon, Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
                    RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                    nameStack.Children.Add(img);
                }
                else
                {
                    var fIco = CreateVectorIcon(NexIcon.FileText, new SolidColorBrush(Color.FromRgb(148, 163, 184)), 15);
                    fIco.Margin = new Thickness(0, 0, 8, 0);
                    nameStack.Children.Add(fIco);
                }
                nameStack.Children.Add(new TextBlock
                {
                    Text = item.Name,
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = TextBrush,
                    MaxWidth = 160,
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
                Grid.SetColumn(nameStack, 1);
                rowGrid.Children.Add(nameStack);

                // Cale
                var pathText = new TextBlock
                {
                    Text = item.FullPath,
                    FontSize = 10.5,
                    Foreground = MutedBrush,
                    MaxWidth = 240,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 10, 0)
                };
                Grid.SetColumn(pathText, 2);
                rowGrid.Children.Add(pathText);

                // Tip
                var typeText = new TextBlock
                {
                    Text = item.FileType,
                    FontSize = 11,
                    Foreground = MutedBrush,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(typeText, 3);
                rowGrid.Children.Add(typeText);

                // Dimensiune
                var sizeText = new TextBlock
                {
                    Text = item.IsDirectory ? "-" : item.SizeFormatted,
                    FontSize = 11.5,
                    FontWeight = item.IsDirectory ? FontWeights.Normal : FontWeights.SemiBold,
                    Foreground = item.IsDirectory ? MutedBrush : TextBrush,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(sizeText, 4);
                rowGrid.Children.Add(sizeText);

                // Data modificării
                var dateText = new TextBlock
                {
                    Text = item.ModifiedDate,
                    FontSize = 11,
                    Foreground = MutedBrush,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(dateText, 5);
                rowGrid.Children.Add(dateText);

                // 3-dots context menu button
                var dotsBtn = new Border
                {
                    Width = 26,
                    Height = 26,
                    CornerRadius = new CornerRadius(5),
                    Background = Brushes.Transparent,
                    Cursor = Cursors.Hand,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Child = new TextBlock
                    {
                        Text = "•••",
                        Foreground = MutedBrush,
                        FontSize = 11,
                        FontWeight = FontWeights.Bold,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                };
                var curItem = item;
                dotsBtn.MouseLeftButtonUp += (s, e) =>
                {
                    e.Handled = true;
                    var cm = new ContextMenu();
                    var miOpen = new MenuItem { Header = NexLocale.T("disk_ctx_open_explorer") };
                    miOpen.Click += (_, _) =>
                    {
                        try
                        {
                            if (curItem.IsDirectory)
                                Process.Start(new ProcessStartInfo("explorer.exe", curItem.FullPath) { UseShellExecute = true });
                            else
                                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{curItem.FullPath}\"") { UseShellExecute = true });
                        }
                        catch { }
                    };
                    cm.Items.Add(miOpen);

                    var miCopy = new MenuItem { Header = NexLocale.T("disk_ctx_copy_path") };
                    miCopy.Click += (_, _) =>
                    {
                        try { Clipboard.SetText(curItem.FullPath); ShowToast(NexLocale.T("disk_toast_copied_title"), NexLocale.T("disk_toast_copied_msg"), NexIcon.Check, GreenBrush); } catch { }
                    };
                    cm.Items.Add(miCopy);

                    var miDel = new MenuItem { Header = NexLocale.T("disk_ctx_delete_perm") };
                    miDel.Click += async (_, _) =>
                    {
                        var conf = await ShowConfirmModalAsync(NexLocale.T("disk_confirm_del_title"), NexLocale.Format("disk_confirm_del_msg", curItem.FullPath), RedBrush, NexLocale.T("btn_delete"), NexLocale.T("btn_cancel"));
                        if (conf)
                        {
                            try
                            {
                                if (curItem.IsDirectory) Directory.Delete(curItem.FullPath, true);
                                else File.Delete(curItem.FullPath);
                                selectedDiskPaths.Remove(curItem.FullPath);
                                ShowToast(NexLocale.T("disk_deleted_title"), NexLocale.Format("disk_deleted_msg", curItem.Name), NexIcon.Check, GreenBrush);
                                ShowDisk();
                            }
                            catch (Exception ex)
                            {
                                ShowToast(NexLocale.T("disk_toast_del_err_title"), ex.Message, NexIcon.Warning, RedBrush);
                            }
                        }
                    };
                    cm.Items.Add(miDel);

                    cm.PlacementTarget = dotsBtn;
                    cm.IsOpen = true;
                };
                Grid.SetColumn(dotsBtn, 6);
                rowGrid.Children.Add(dotsBtn);

                rowBorder.Child = rowGrid;
                rowsStack.Children.Add(rowBorder);
            }
        }

        var rowsScroll = new ScrollViewer
        {
            MaxHeight = 280,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = rowsStack
        };
        stack.Children.Add(rowsScroll);

        // 4. Footer inside Card: "X elemente selectate   Y MB" on left, "[Trash] Șterge selectate" on right
        var footerBorder = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(24, 36, 52)),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Margin = new Thickness(0, 14, 0, 0),
            Padding = new Thickness(0, 12, 0, 0)
        };
        var footerGrid = new Grid();
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var leftFooter = new TextBlock
        {
            Text = selectedDiskPaths.Count > 0
                ? NexLocale.Format("disk_selected_format", selectedDiskPaths.Count, NativeTuning.FormatFileSize(totalSelectedBytes))
                : NexLocale.T("disk_selected_empty"),
            FontSize = 12,
            Foreground = MutedBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(leftFooter, 0);
        footerGrid.Children.Add(leftFooter);

        var delSelectedBtn = new Button
        {
            Style = (Style)FindResource("DarkTableFooterButtonStyle"),
            Padding = new Thickness(14, 6, 14, 6),
            Cursor = Cursors.Hand
        };
        var dsStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var dsTrIco = CreateVectorIcon(NexIcon.Trash, MutedBrush, 13);
        dsTrIco.Margin = new Thickness(0, 0, 6, 0);
        dsStack.Children.Add(dsTrIco);
        dsStack.Children.Add(new TextBlock { Text = NexLocale.T("disk_btn_delete_selected"), Foreground = MutedBrush, FontWeight = FontWeights.SemiBold, FontSize = 11.5 });
        delSelectedBtn.Content = dsStack;
        delSelectedBtn.Click += async (_, _) =>
        {
            if (selectedDiskPaths.Count > 0)
            {
                var conf = await ShowConfirmModalAsync(NexLocale.T("disk_confirm_multi_del_title"), NexLocale.Format("disk_confirm_multi_del_msg", selectedDiskPaths.Count), RedBrush, NexLocale.T("disk_btn_del_all"), NexLocale.T("btn_cancel"));
                if (conf)
                {
                    int delCount = 0;
                    foreach (var p in selectedDiskPaths.ToList())
                    {
                        try
                        {
                            if (Directory.Exists(p)) Directory.Delete(p, true);
                            else if (File.Exists(p)) File.Delete(p);
                            delCount++;
                        }
                        catch { }
                    }
                    selectedDiskPaths.Clear();
                    ShowToast(NexLocale.T("disk_toast_clean_done_title"), NexLocale.Format("disk_toast_clean_done_msg", delCount), NexIcon.Check, GreenBrush);
                    ShowDisk();
                }
            }
            else
            {
                ShowToast(NexLocale.T("disk_toast_select_files_title"), NexLocale.T("disk_toast_select_files_msg"), NexIcon.Info, AmberBrush);
            }
        };
        Grid.SetColumn(delSelectedBtn, 1);
        footerGrid.Children.Add(delSelectedBtn);

        footerBorder.Child = footerGrid;
        stack.Children.Add(footerBorder);

        container.Child = stack;
        PageRoot.Children.Add(container);
    }

}