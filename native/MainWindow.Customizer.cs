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
    private string activeCustomizerTab = "Wallpaper"; // "Wallpaper", "Taskbar", "Start", "Explorer", "Theme"

    // Engine Mode: "Live" (Live Wallpapers), "Static" (Wallpapers), or "Library" (Biblioteca mea)
    private string _wallpaperEngineMode = "Live";
    private string _libraryCategory = "Live"; // "Live" or "Static"

    // Live Wallpapers State
    private string _wallpaperSearchQuery = "";
    private string _selectedWallpaperCategory = "";
    private string _selectedResolutionFilter = "Toate"; // "Toate", "MyScreen", "4K UHD", "1440p QHD", "1080p FHD"
    private List<PixabayVideoItem>? _cachedPixabayVideos = null;
    private bool _isPixabayLoading = false;
    private string? _downloadingVideoId = null;
    private double _downloadProgressPercent = 0;

    // Static Wallpapers State
    private string _staticSearchQuery = "";
    private string _staticCategory = "111"; // "111"=all, "100"=general, "010"=anime, "001"=people
    private string _staticPurity = "100";   // "100"=SFW, "110"=SFW+Sketchy, "111"=All/NSFW, "001"=Only NSFW
    private string _staticSorting = "toplist"; // toplist, hot, date_added, random, views, favorites
    private string _staticTopRange = "1M";
    private string _staticResolution = "Toate"; // "Toate", "MyScreen", "4K UHD", "1440p QHD", "1080p FHD"
    private string _staticRatio = "Toate";      // "Toate", "16x9", "21x9", "16x10"
    private List<WallpaperPhotoItem>? _cachedStaticWallpapers = null;
    private bool _isStaticLoading = false;
    private string? _downloadingPhotoId = null;
    private double _staticProgressPercent = 0;

    private void ShowCustomizer()
    {
        PreparePage(NexLocale.T("customizer_title"), NexLocale.T("customizer_subtitle"));

        // Top Action Bar
        var topBar = new Border
        {
            Background = CardBackground(),
            BorderBrush = new SolidColorBrush(Color.FromRgb(24, 38, 56)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 12, 16, 12),
            Margin = new Thickness(0, 0, 0, 14)
        };
        var topGrid = new Grid();
        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var topInfo = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var headRow = new StackPanel { Orientation = Orientation.Horizontal };
        headRow.Children.Add(CreateVectorIcon(NexIcon.Window, CyanBrush, 16));
        headRow.Children.Add(new TextBlock
        {
            Text = "  " + NexLocale.T("customizer_bar_title"),
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = TextBrush,
            VerticalAlignment = VerticalAlignment.Center
        });
        topInfo.Children.Add(headRow);
        topInfo.Children.Add(new TextBlock
        {
            Text = NexLocale.T("customizer_bar_desc"),
            FontSize = 11.5,
            Foreground = MutedBrush,
            Margin = new Thickness(0, 3, 0, 0)
        });
        Grid.SetColumn(topInfo, 0);
        topGrid.Children.Add(topInfo);

        var restartBtn = MakeCardButton(NexLocale.T("btn_restart_shell"), NexIcon.Refresh, CyanBrush, async () =>
        {
            ShowNotification("Windows Explorer", NexLocale.T("cust_notif_restart_msg"), true);
            await NativeTuning.RestartExplorerAsync();
            ShowNotification(NexLocale.T("cust_notif_restarted_title"), NexLocale.T("cust_notif_restarted_msg"), false, true);
            ShowToast(NexLocale.T("cust_toast_updated_title"), NexLocale.T("cust_toast_updated_msg"), NexIcon.Check, GreenBrush);
        }, true, 165);
        Grid.SetColumn(restartBtn, 1);
        topGrid.Children.Add(restartBtn);

        topBar.Child = topGrid;
        PageRoot.Children.Add(topBar);

        // Sub-Navigation Tabs - "Wallpaper & Live" placed FIRST
        var tabsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14) };

        Border CreateCustomizerSubTab(string key, string title, NexIcon ico)
        {
            bool isCurrent = activeCustomizerTab.Equals(key, StringComparison.OrdinalIgnoreCase);
            var tabBorder = new Border
            {
                Background = isCurrent ? new SolidColorBrush(Color.FromRgb(11, 27, 54)) : Brushes.Transparent,
                BorderBrush = isCurrent ? new SolidColorBrush(Color.FromRgb(37, 99, 235)) : Brushes.Transparent,
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(14, 7, 14, 7),
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = Cursors.Hand
            };
            var tabStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var tabIcon = CreateVectorIcon(ico, isCurrent ? CyanBrush : MutedBrush, 13);
            tabIcon.Margin = new Thickness(0, 0, 7, 0);
            tabStack.Children.Add(tabIcon);

            tabStack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 12,
                FontWeight = isCurrent ? FontWeights.Bold : FontWeights.Normal,
                Foreground = isCurrent ? CyanBrush : MutedBrush,
                VerticalAlignment = VerticalAlignment.Center
            });

            tabBorder.Child = tabStack;
            tabBorder.MouseLeftButtonUp += (_, _) =>
            {
                activeCustomizerTab = key;
                NativeTuning.TrimWorkingSet();
                ShowCustomizer();
            };
            return tabBorder;
        }

        tabsPanel.Children.Add(CreateCustomizerSubTab("Wallpaper", NexLocale.T("customizer_tab_wallpaper"), NexIcon.Layers));
        tabsPanel.Children.Add(CreateCustomizerSubTab("Taskbar", NexLocale.T("customizer_tab_taskbar"), NexIcon.Window));
        tabsPanel.Children.Add(CreateCustomizerSubTab("Start", NexLocale.T("customizer_tab_start"), NexIcon.Sliders));
        tabsPanel.Children.Add(CreateCustomizerSubTab("Explorer", NexLocale.T("customizer_tab_explorer"), NexIcon.Folder));
        tabsPanel.Children.Add(CreateCustomizerSubTab("Theme", NexLocale.T("customizer_tab_theme"), NexIcon.Paint));
        PageRoot.Children.Add(tabsPanel);

        // ================= TAB: WALLPAPERS & LIVE WALLPAPER =================
        if (activeCustomizerTab.Equals("Wallpaper", StringComparison.OrdinalIgnoreCase))
        {
            NativeTuning.EnsureOriginalWallpaperSaved();
            string origPath = NativeTuning.GetSavedOriginalWallpaper();
            string currentPath = NativeTuning.GetCurrentWallpaperPath();
            bool isOrigActive = string.Equals(origPath, currentPath, StringComparison.OrdinalIgnoreCase);
            bool isLiveRunning = LiveWallpaperWindow.IsRunning;
            string activeName = LiveWallpaperWindow.CurrentPresetOrFile;
            if (!string.IsNullOrEmpty(activeName) && File.Exists(activeName))
            {
                activeName = System.IO.Path.GetFileName(activeName);
            }

            int screenW = (int)SystemParameters.PrimaryScreenWidth;
            int screenH = (int)SystemParameters.PrimaryScreenHeight;
            string userResName = (screenW >= 3840 || screenH >= 2160) ? "4K UHD" :
                                 (screenW >= 2560 || screenH >= 1440) ? "1440p QHD" : "1080p FHD";

            void TriggerVideoSearch(string q, string cat)
            {
                _wallpaperSearchQuery = q;
                _selectedWallpaperCategory = cat;
                _cachedPixabayVideos = null;
                _isPixabayLoading = true;
                NativeTuning.TrimWorkingSet();
                ShowCustomizer();

                Task.Run(async () =>
                {
                    string finalQ = string.IsNullOrWhiteSpace(q) ? "background" : q;
                    int minW = 0;
                    int minH = 0;
                    if (_selectedResolutionFilter == "4K UHD") { minW = 3840; minH = 2160; }
                    else if (_selectedResolutionFilter == "1440p QHD") { minW = 2560; minH = 1440; }
                    else if (_selectedResolutionFilter == "1080p FHD") { minW = 1920; minH = 1080; }
                    else if (_selectedResolutionFilter == "MyScreen") { minW = Math.Min(screenW, 3840); minH = Math.Min(screenH, 2160); }

                    var vids = await PixabayVideoService.SearchVideosAsync(finalQ, cat, 18, minW, minH);
                    await Dispatcher.InvokeAsync(() =>
                    {
                        _cachedPixabayVideos = vids;
                        _isPixabayLoading = false;
                        NativeTuning.TrimWorkingSet();
                        ShowCustomizer();
                    });
                });
            }

            void TriggerStaticSearch(string q)
            {
                _staticSearchQuery = q;
                _cachedStaticWallpapers = null;
                _isStaticLoading = true;
                NativeTuning.TrimWorkingSet();
                ShowCustomizer();

                Task.Run(async () =>
                {
                    string atLeastRes = _staticResolution switch
                    {
                        "4K UHD" => "3840x2160",
                        "1440p QHD" => "2560x1440",
                        "1080p FHD" => "1920x1080",
                        "MyScreen" => $"{screenW}x{screenH}",
                        _ => ""
                    };

                    string ratioParam = _staticRatio switch
                    {
                        "16:9" => "16x9",
                        "21:9" => "21x9",
                        "16:10" => "16x10",
                        _ => ""
                    };

                    var items = await WallhavenService.SearchWallpapersAsync(
                        query: q,
                        categories: _staticCategory,
                        purity: _staticPurity,
                        sorting: _staticSorting,
                        topRange: _staticTopRange,
                        atleast: atLeastRes,
                        ratios: ratioParam,
                        page: 1
                    );

                    await Dispatcher.InvokeAsync(() =>
                    {
                        _cachedStaticWallpapers = items;
                        _isStaticLoading = false;
                        NativeTuning.TrimWorkingSet();
                        ShowCustomizer();
                    });
                });
            }

            // Auto-trigger video search on first load of Live mode
            if (_wallpaperEngineMode == "Live" && _cachedPixabayVideos == null && !_isPixabayLoading)
            {
                _isPixabayLoading = true;
                Task.Run(async () =>
                {
                    string initialQuery = string.IsNullOrWhiteSpace(_wallpaperSearchQuery) ? "wallpaper" : _wallpaperSearchQuery;
                    var vids = await PixabayVideoService.SearchVideosAsync(initialQuery, _selectedWallpaperCategory, 18);
                    await Dispatcher.InvokeAsync(() =>
                    {
                        _cachedPixabayVideos = vids;
                        _isPixabayLoading = false;
                        NativeTuning.TrimWorkingSet();
                        ShowCustomizer();
                    });
                });
            }

            // Auto-trigger photo search on first load of Static mode
            if (_wallpaperEngineMode == "Static" && _cachedStaticWallpapers == null && !_isStaticLoading)
            {
                _isStaticLoading = true;
                Task.Run(async () =>
                {
                    var items = await WallhavenService.SearchWallpapersAsync(
                        query: _staticSearchQuery,
                        categories: _staticCategory,
                        purity: _staticPurity,
                        sorting: _staticSorting,
                        topRange: _staticTopRange,
                        atleast: "1920x1080",
                        ratios: "16x9",
                        page: 1
                    );
                    await Dispatcher.InvokeAsync(() =>
                    {
                        _cachedStaticWallpapers = items;
                        _isStaticLoading = false;
                        NativeTuning.TrimWorkingSet();
                        ShowCustomizer();
                    });
                });
            }

            // Card 1: Live Wallpaper Engine & Quick Controls
            var engineCard = new Border
            {
                Background = CardBackground(),
                BorderBrush = new SolidColorBrush(Color.FromRgb(24, 38, 56)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(18, 16, 18, 16),
                Margin = new Thickness(0, 0, 0, 14)
            };
            var engGrid = new Grid();
            engGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            engGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var engInfo = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 0) };
            var engTitleRow = new StackPanel { Orientation = Orientation.Horizontal };
            engTitleRow.Children.Add(new TextBlock
            {
                Text = NexLocale.T("cust_live_title"),
                FontSize = 13.5,
                FontWeight = FontWeights.Bold,
                Foreground = TextBrush
            });
            engTitleRow.Children.Add(new Border
            {
                Background = isLiveRunning ? new SolidColorBrush(Color.FromArgb(35, 16, 185, 129)) : new SolidColorBrush(Color.FromArgb(30, 71, 85, 105)),
                BorderBrush = isLiveRunning ? new SolidColorBrush(Color.FromArgb(80, 16, 185, 129)) : new SolidColorBrush(Color.FromArgb(60, 71, 85, 105)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 1.5, 6, 1.5),
                Margin = new Thickness(10, 0, 0, 0),
                Child = new TextBlock
                {
                    Text = isLiveRunning ? NexLocale.T("cust_lib_badge_active") : NexLocale.T("status_inactive").ToUpperInvariant(),
                    FontSize = 9.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = isLiveRunning ? GreenBrush : MutedBrush
                }
            });
            engInfo.Children.Add(engTitleRow);
            if (isLiveRunning)
            {
                engInfo.Children.Add(new TextBlock
                {
                    Text = $"{NexLocale.T("cust_active_source")}: {activeName}",
                    FontSize = 10.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = CyanBrush,
                    Margin = new Thickness(0, 3, 0, 0)
                });
            }

            // Game Auto-Pause Checkbox with explicit TextBlock for guaranteed dark-theme contrast
            var pauseRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 9, 0, 0),
                Cursor = Cursors.Hand
            };
            var pauseBox = new CheckBox
            {
                IsChecked = LiveWallpaperWindow.IsAutoPauseEnabled,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            pauseBox.Checked += (_, _) => LiveWallpaperWindow.SetAutoPauseEnabled(true);
            pauseBox.Unchecked += (_, _) => LiveWallpaperWindow.SetAutoPauseEnabled(false);
            pauseRow.Children.Add(pauseBox);

            var pauseLabel = new TextBlock
            {
                Text = NexLocale.T("cust_live_pause_desc"),
                FontSize = 11,
                Foreground = MutedBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            pauseLabel.MouseLeftButtonUp += (_, _) => pauseBox.IsChecked = !pauseBox.IsChecked;
            pauseRow.Children.Add(pauseLabel);

            engInfo.Children.Add(pauseRow);

            Grid.SetColumn(engInfo, 0);
            engGrid.Children.Add(engInfo);

            var engActions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            var customVideoBtn = new Button
            {
                Content = NexLocale.T("cust_btn_choose_local"),
                Style = (Style)FindResource("SecondaryButtonStyle"),
                Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = Cursors.Hand
            };
            customVideoBtn.Click += (_, _) =>
            {
                var ofd = new OpenFileDialog
                {
                    Title = NexLocale.T("cust_ofd_title"),
                    Filter = NexLocale.T("cust_ofd_filter")
                };
                if (ofd.ShowDialog() == true)
                {
                    string target = ofd.FileName;
                    bool ok = LiveWallpaperWindow.StartLive(target);
                    if (ok)
                    {
                        ShowToast(NexLocale.T("cust_live_title"), NexLocale.Format("cust_toast_live_activated", System.IO.Path.GetFileName(target)), NexIcon.Check, GreenBrush);
                    }
                    else
                    {
                        ShowToast(NexLocale.T("status_error"), NexLocale.T("cust_toast_err_load_file"), NexIcon.Warning, RedBrush);
                    }
                    ShowCustomizer();
                }
            };
            engActions.Children.Add(customVideoBtn);

            if (isLiveRunning)
            {
                var stopLiveBtn = new Button
                {
                    Content = NexLocale.T("cust_btn_stop_live"),
                    Style = (Style)FindResource("SecondaryButtonStyle"),
                    Padding = new Thickness(12, 6, 12, 6),
                    Margin = new Thickness(0, 0, 8, 0),
                    Foreground = RedBrush,
                    BorderBrush = new SolidColorBrush(Color.FromArgb(80, 239, 68, 68)),
                    Cursor = Cursors.Hand
                };
                stopLiveBtn.Click += (_, _) =>
                {
                    LiveWallpaperWindow.StopLive();
                    ShowToast(NexLocale.T("cust_toast_live_stopped_title"), NexLocale.T("cust_toast_live_stopped_msg"), NexIcon.Info, CyanBrush);
                    ShowCustomizer();
                };
                engActions.Children.Add(stopLiveBtn);
            }

            var restoreBtn = new Button
            {
                Content = NexLocale.T("cust_btn_restore_orig"),
                Style = (Style)FindResource("SecondaryButtonStyle"),
                Padding = new Thickness(12, 6, 12, 6),
                Cursor = Cursors.Hand
            };
            restoreBtn.Click += (_, _) =>
            {
                LiveWallpaperWindow.StopLive();
                NativeTuning.RestoreOriginalWallpaper();
                InitAutoWallpaperTimer();
                ShowToast(NexLocale.T("cust_toast_orig_restored_title"), NexLocale.T("cust_toast_orig_restored_msg"), NexIcon.Check, GreenBrush);
                ShowCustomizer();
            };
            engActions.Children.Add(restoreBtn);

            Grid.SetColumn(engActions, 1);
            engGrid.Children.Add(engActions);
            engineCard.Child = engGrid;
            PageRoot.Children.Add(engineCard);

            // ================= MODE SWITCHER: LIVE WALLPAPERS vs WALLPAPERS =================
            var modeSwitchRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 2, 0, 14)
            };

            Border MakeModeSwitchButton(string modeKey, string label, NexIcon icon)
            {
                bool isSel = _wallpaperEngineMode.Equals(modeKey, StringComparison.OrdinalIgnoreCase);
                var b = new Border
                {
                    Background = isSel ? new SolidColorBrush(Color.FromRgb(15, 38, 74)) : new SolidColorBrush(Color.FromRgb(13, 20, 32)),
                    BorderBrush = isSel ? new SolidColorBrush(Color.FromRgb(37, 99, 235)) : new SolidColorBrush(Color.FromRgb(24, 38, 56)),
                    BorderThickness = new Thickness(1.5),
                    CornerRadius = new CornerRadius(7),
                    Padding = new Thickness(18, 8, 18, 8),
                    Margin = new Thickness(0, 0, 10, 0),
                    Cursor = Cursors.Hand
                };
                var sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                var ico = CreateVectorIcon(icon, isSel ? CyanBrush : MutedBrush, 13);
                ico.Margin = new Thickness(0, 0, 7, 0);
                sp.Children.Add(ico);
                sp.Children.Add(new TextBlock
                {
                    Text = label,
                    FontSize = 12.5,
                    FontWeight = isSel ? FontWeights.Bold : FontWeights.Normal,
                    Foreground = isSel ? CyanBrush : TextBrush
                });
                b.Child = sp;
                b.MouseLeftButtonUp += (_, _) =>
                {
                    _wallpaperEngineMode = modeKey;
                    NativeTuning.TrimWorkingSet();
                    ShowCustomizer();
                };
                return b;
            }

            modeSwitchRow.Children.Add(MakeModeSwitchButton("Live", NexLocale.T("cust_mode_live"), NexIcon.Play));
            modeSwitchRow.Children.Add(MakeModeSwitchButton("Static", NexLocale.T("cust_mode_static"), NexIcon.Photo));
            modeSwitchRow.Children.Add(MakeModeSwitchButton("Library", NexLocale.T("cust_mode_library"), NexIcon.Folder));
            PageRoot.Children.Add(modeSwitchRow);

            // ================= MODE A: LIVE WALLPAPERS =================
            if (_wallpaperEngineMode == "Live")
            {
                var hubHeader = new Border
                {
                    Background = CardBackground(),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(24, 38, 56)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(16, 14, 16, 14),
                    Margin = new Thickness(0, 0, 0, 14)
                };
                var hubStack = new StackPanel();

                var hubTitleText = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
                hubTitleText.Children.Add(new TextBlock
                {
                    Text = NexLocale.T("cust_live_header"),
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    Foreground = TextBrush
                });
                hubTitleText.Children.Add(new TextBlock
                {
                    Text = NexLocale.T("cust_live_header_desc"),
                    FontSize = 11,
                    Foreground = MutedBrush,
                    Margin = new Thickness(0, 2, 0, 0)
                });
                hubStack.Children.Add(hubTitleText);

                // Search Bar & Resolution Dropdown
                var searchRow = new Grid { Margin = new Thickness(0, 0, 0, 12) };
                searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(320) });
                searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
                searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var searchBoxContainer = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(11, 22, 38)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(37, 70, 110)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(7),
                    Padding = new Thickness(8, 2, 6, 2)
                };
                var sGrid = new Grid();
                sGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var searchInput = new TextBox
                {
                    Text = _wallpaperSearchQuery,
                    Background = Brushes.Transparent,
                    Foreground = TextBrush,
                    CaretBrush = CyanBrush,
                    BorderThickness = new Thickness(0),
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center,
                    Padding = new Thickness(4, 4, 4, 4)
                };
                sGrid.Children.Add(searchInput);
                searchBoxContainer.Child = sGrid;
                Grid.SetColumn(searchBoxContainer, 0);
                searchRow.Children.Add(searchBoxContainer);

                var sBtn = new Button
                {
                    Content = NexLocale.T("cust_btn_search"),
                    Style = (Style)FindResource("DarkTableFooterButtonStyle"),
                    Padding = new Thickness(16, 6, 16, 6),
                    Cursor = Cursors.Hand,
                    VerticalAlignment = VerticalAlignment.Center
                };
                sBtn.Click += (_, _) => TriggerVideoSearch(searchInput.Text.Trim(), _selectedWallpaperCategory);
                searchInput.KeyDown += (_, e) =>
                {
                    if (e.Key == Key.Enter) TriggerVideoSearch(searchInput.Text.Trim(), _selectedWallpaperCategory);
                };
                Grid.SetColumn(sBtn, 2);
                searchRow.Children.Add(sBtn);

                // Resolution Selector Dropdown
                string activeResText = _selectedResolutionFilter switch
                {
                    "MyScreen" => NexLocale.Format("cust_res_my_screen_format", screenW, screenH),
                    "4K UHD" => "4K UHD",
                    "1440p QHD" => "1440p QHD",
                    "1080p FHD" => "1080p FHD",
                    _ => NexLocale.T("cust_res_all_label", "Toate Rezoluțiile")
                };
                bool isFilterActive = !_selectedResolutionFilter.Equals("Toate", StringComparison.OrdinalIgnoreCase);

                var resBtn = new Button
                {
                    Content = NexLocale.Format("cust_res_btn_format", activeResText),
                    Style = (Style)FindResource("DarkTableFooterButtonStyle"),
                    Foreground = isFilterActive ? CyanBrush : TextBrush,
                    Padding = new Thickness(14, 6, 14, 6),
                    Cursor = Cursors.Hand,
                    VerticalAlignment = VerticalAlignment.Center
                };

                resBtn.Click += (_, _) =>
                {
                    var menu = new ContextMenu
                    {
                        Style = (Style)FindResource("DarkContextMenuStyle"),
                        PlacementTarget = resBtn,
                        Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom
                    };

                    var resOptions = new (string Key, string Label)[]
                    {
                        ("Toate", NexLocale.T("cust_res_all_label", "Toate Rezoluțiile")),
                        ("MyScreen", NexLocale.Format("cust_res_my_screen_format", screenW, screenH)),
                        ("4K UHD", "4K UHD (3840x2160+)"),
                        ("1440p QHD", "1440p QHD (2560x1440)"),
                        ("1080p FHD", "1080p FHD (1920x1080)")
                    };

                    foreach (var (rKey, rLabel) in resOptions)
                    {
                        bool isSel = _selectedResolutionFilter.Equals(rKey, StringComparison.OrdinalIgnoreCase);
                        var mi = new MenuItem
                        {
                            Header = rLabel,
                            Style = (Style)FindResource("DarkMenuItemStyle"),
                            Icon = isSel ? CreateVectorIcon(NexIcon.Check, CyanBrush, 12) : null,
                            Foreground = isSel ? CyanBrush : (Brush)FindResource("TextPrimary"),
                            FontWeight = isSel ? FontWeights.Bold : FontWeights.Normal
                        };
                        var capturedKey = rKey;
                        mi.Click += (_, _) =>
                        {
                            _selectedResolutionFilter = capturedKey;
                            TriggerVideoSearch(_wallpaperSearchQuery, _selectedWallpaperCategory);
                        };
                        menu.Items.Add(mi);
                    }
                    menu.IsOpen = true;
                };

                Grid.SetColumn(resBtn, 4);
                searchRow.Children.Add(resBtn);
                hubStack.Children.Add(searchRow);

                // Category Filter Pills
                var catWrap = new WrapPanel { Margin = new Thickness(0, 2, 0, 0) };
                var categories = new (string Name, string Label, string Query)[]
                {
                    ("Cyberpunk", NexLocale.T("cust_cat_cyberpunk", "Cyberpunk"), "cyberpunk neon"),
                    ("Spațiu", NexLocale.T("cust_cat_space", "Spațiu"), "space nebula galaxy"),
                    ("Natură", NexLocale.T("cust_cat_nature", "Natură"), "nature landscape waterfall"),
                    ("Abstract", NexLocale.T("cust_cat_abstract", "Abstract"), "abstract particles motion"),
                    ("Anime", NexLocale.T("cust_cat_anime", "Anime"), "anime aesthetic scenery"),
                    ("Gaming", NexLocale.T("cust_cat_gaming", "Gaming"), "gaming futuristic dark"),
                    ("Mașini", NexLocale.T("cust_cat_cars", "Mașini"), "cars synthwave drive"),
                    ("Relaxare", NexLocale.T("cust_cat_relax", "Relaxare"), "relaxing water rain nature")
                };

                foreach (var (catName, catLabel, catQuery) in categories)
                {
                    bool isCatActive = !string.IsNullOrEmpty(_selectedWallpaperCategory) &&
                                       _selectedWallpaperCategory.Equals(catName, StringComparison.OrdinalIgnoreCase);
                    var catPill = new Border
                    {
                        Background = isCatActive ? new SolidColorBrush(Color.FromRgb(15, 38, 74)) : new SolidColorBrush(Color.FromRgb(13, 20, 32)),
                        BorderBrush = isCatActive ? new SolidColorBrush(Color.FromRgb(37, 99, 235)) : new SolidColorBrush(Color.FromRgb(24, 38, 56)),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(5),
                        Padding = new Thickness(10, 4, 10, 4),
                        Margin = new Thickness(0, 0, 6, 6),
                        Cursor = Cursors.Hand
                    };
                    var catText = new TextBlock
                    {
                        Text = catLabel,
                        FontSize = 11,
                        Foreground = isCatActive ? CyanBrush : MutedBrush,
                        FontWeight = isCatActive ? FontWeights.Bold : FontWeights.Normal
                    };
                    catPill.Child = catText;
                    catPill.MouseLeftButtonUp += (_, _) =>
                    {
                        if (isCatActive)
                        {
                            TriggerVideoSearch(_wallpaperSearchQuery, "");
                        }
                        else
                        {
                            TriggerVideoSearch(catQuery, catName);
                        }
                    };
                    catWrap.Children.Add(catPill);
                }
                hubStack.Children.Add(catWrap);

                hubHeader.Child = hubStack;
                PageRoot.Children.Add(hubHeader);

                // Results Grid for Live Wallpapers
                if (_isPixabayLoading)
                {
                    var loadingCard = new Border
                    {
                        Background = CardBackground(),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(24, 38, 56)),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(10),
                        Padding = new Thickness(30),
                        Margin = new Thickness(0, 0, 0, 16),
                        HorizontalAlignment = HorizontalAlignment.Stretch
                    };
                    var loadStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                    loadStack.Children.Add(CreateVectorIcon(NexIcon.Refresh, CyanBrush, 24));
                    loadStack.Children.Add(new TextBlock
                    {
                        Text = NexLocale.T("cust_live_searching"),
                        FontSize = 13,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = TextBrush,
                        Margin = new Thickness(0, 10, 0, 0)
                    });
                    loadingCard.Child = loadStack;
                    PageRoot.Children.Add(loadingCard);
                }
                else if (_cachedPixabayVideos == null || _cachedPixabayVideos.Count == 0)
                {
                    var emptyCard = new Border
                    {
                        Background = CardBackground(),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(24, 38, 56)),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(10),
                        Padding = new Thickness(30),
                        Margin = new Thickness(0, 0, 0, 16),
                        HorizontalAlignment = HorizontalAlignment.Stretch
                    };
                    var emptyStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                    emptyStack.Children.Add(CreateVectorIcon(NexIcon.Layers, MutedBrush, 24));
                    emptyStack.Children.Add(new TextBlock
                    {
                        Text = NexLocale.T("cust_live_not_found"),
                        FontSize = 13,
                        Foreground = TextBrush,
                        Margin = new Thickness(0, 10, 0, 8)
                    });
                    var retryBtn = new Button
                    {
                        Content = NexLocale.T("cust_btn_reload_all"),
                        Style = (Style)FindResource("DarkTableFooterButtonStyle"),
                        Padding = new Thickness(12, 6, 12, 6),
                        Cursor = Cursors.Hand
                    };
                    retryBtn.Click += (_, _) => TriggerVideoSearch("wallpaper", "");
                    emptyStack.Children.Add(retryBtn);
                    emptyCard.Child = emptyStack;
                    PageRoot.Children.Add(emptyCard);
                }
                else
                {
                    var displayVideos = _cachedPixabayVideos.ToList();
                    var vGrid = new Grid { Margin = new Thickness(0, 0, 0, 16) };
                    vGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    vGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
                    vGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    vGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
                    vGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    int rowCount = (int)Math.Ceiling(displayVideos.Count / 3.0);
                    for (int r = 0; r < rowCount; r++)
                    {
                        vGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    }

                    for (int i = 0; i < displayVideos.Count; i++)
                    {
                        var vid = displayVideos[i];
                        bool isThisActive = isLiveRunning && !string.IsNullOrEmpty(activeName) && activeName.Contains($"pixabay_{vid.Id}");
                        bool isDownloading = _downloadingVideoId == vid.Id.ToString();

                        var vCard = new Border
                        {
                            Background = CardBackground(),
                            BorderBrush = isThisActive ? CyanBrush : new SolidColorBrush(Color.FromRgb(24, 38, 56)),
                            BorderThickness = new Thickness(isThisActive ? 1.5 : 1),
                            CornerRadius = new CornerRadius(9),
                            Padding = new Thickness(10),
                            Margin = new Thickness(0, 0, 0, 14)
                        };

                        var cardStack = new StackPanel();

                        // Video Preview Thumbnail Container
                        var thumbBox = new Border
                        {
                            Height = 160,
                            CornerRadius = new CornerRadius(6),
                            ClipToBounds = true,
                            Background = new SolidColorBrush(Color.FromRgb(10, 16, 26))
                        };

                        var imgGrid = new Grid();
                        if (!string.IsNullOrEmpty(vid.LocalThumbnailPath) && File.Exists(vid.LocalThumbnailPath))
                        {
                            try
                            {
                                var bmp = new BitmapImage();
                                bmp.BeginInit();
                                bmp.CacheOption = BitmapCacheOption.OnLoad;
                                bmp.UriSource = new Uri(vid.LocalThumbnailPath);
                                bmp.EndInit();
                                imgGrid.Children.Add(new Image { Source = bmp, Stretch = Stretch.UniformToFill });
                            }
                            catch { }
                        }
                        else if (!string.IsNullOrEmpty(vid.ThumbnailUrl))
                        {
                            try
                            {
                                var bmp = new BitmapImage();
                                bmp.BeginInit();
                                bmp.CacheOption = BitmapCacheOption.OnLoad;
                                bmp.UriSource = new Uri(vid.ThumbnailUrl);
                                bmp.EndInit();
                                imgGrid.Children.Add(new Image { Source = bmp, Stretch = Stretch.UniformToFill });
                            }
                            catch { }
                        }

                        // Resolution Badge
                        var resBadge = new Border
                        {
                            Background = new SolidColorBrush(Color.FromArgb(200, 10, 16, 26)),
                            CornerRadius = new CornerRadius(4),
                            Padding = new Thickness(6, 2, 6, 2),
                            HorizontalAlignment = HorizontalAlignment.Left,
                            VerticalAlignment = VerticalAlignment.Top,
                            Margin = new Thickness(8)
                        };
                        resBadge.Child = new TextBlock
                        {
                            Text = $"{vid.Resolution} ({vid.Width}x{vid.Height})",
                            FontSize = 9.5,
                            FontWeight = FontWeights.Bold,
                            Foreground = CyanBrush
                        };
                        imgGrid.Children.Add(resBadge);

                        // Duration Badge
                        if (vid.Duration > 0)
                        {
                            var durBadge = new Border
                            {
                                Background = new SolidColorBrush(Color.FromArgb(190, 0, 0, 0)),
                                CornerRadius = new CornerRadius(4),
                                Padding = new Thickness(5, 1.5, 5, 1.5),
                                HorizontalAlignment = HorizontalAlignment.Right,
                                VerticalAlignment = VerticalAlignment.Bottom,
                                Margin = new Thickness(8)
                            };
                            durBadge.Child = new TextBlock
                            {
                                Text = $"{vid.Duration}s",
                                FontSize = 9.5,
                                FontWeight = FontWeights.SemiBold,
                                Foreground = TextBrush
                            };
                            imgGrid.Children.Add(durBadge);
                        }

                        thumbBox.Child = imgGrid;
                        cardStack.Children.Add(thumbBox);

                        // Card Info Row
                        var infoRow = new Grid { Margin = new Thickness(0, 8, 0, 6) };
                        infoRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        infoRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                        string tagSummary = string.IsNullOrWhiteSpace(vid.Tags) ? NexLocale.T("cust_video_bg_default", "Fundal Video") : vid.Tags.Split(',')[0].Trim();
                        var tagText = new TextBlock
                        {
                            Text = char.ToUpperInvariant(tagSummary[0]) + tagSummary.Substring(1),
                            FontSize = 11.5,
                            FontWeight = FontWeights.SemiBold,
                            Foreground = TextBrush,
                            TextTrimming = TextTrimming.CharacterEllipsis
                        };
                        Grid.SetColumn(tagText, 0);
                        infoRow.Children.Add(tagText);

                        double mb = (double)vid.FileSizeBytes / (1024 * 1024);
                        if (mb > 0)
                        {
                            var sizeText = new TextBlock
                            {
                                Text = $"{mb:F1} MB",
                                FontSize = 10.5,
                                Foreground = MutedBrush,
                                VerticalAlignment = VerticalAlignment.Center
                            };
                            Grid.SetColumn(sizeText, 1);
                            infoRow.Children.Add(sizeText);
                        }
                        cardStack.Children.Add(infoRow);

                        // Actions / Download State
                        if (isDownloading)
                        {
                            var progStack = new StackPanel { Margin = new Thickness(0, 2, 0, 0) };
                            progStack.Children.Add(new ProgressBar
                            {
                                Height = 4,
                                Maximum = 1.0,
                                Value = _downloadProgressPercent,
                                Background = new SolidColorBrush(Color.FromRgb(24, 38, 56)),
                                Foreground = CyanBrush,
                                BorderThickness = new Thickness(0)
                            });
                            progStack.Children.Add(new TextBlock
                            {
                                Text = NexLocale.Format("cust_downloading_progress_format", _downloadProgressPercent * 100),
                                FontSize = 10,
                                Foreground = CyanBrush,
                                HorizontalAlignment = HorizontalAlignment.Center,
                                Margin = new Thickness(0, 3, 0, 0)
                            });
                            cardStack.Children.Add(progStack);
                        }
                        else if (isThisActive)
                        {
                            var activeBtn = new Button
                            {
                                Content = NexLocale.T("cust_btn_run_desktop"),
                                Style = (Style)FindResource("DarkTableFooterButtonStyle"),
                                Background = new SolidColorBrush(Color.FromArgb(40, 16, 185, 129)),
                                BorderBrush = GreenBrush,
                                Foreground = GreenBrush,
                                FontWeight = FontWeights.Bold,
                                Padding = new Thickness(8, 5, 8, 5),
                                HorizontalAlignment = HorizontalAlignment.Stretch,
                                IsEnabled = false
                            };
                            cardStack.Children.Add(activeBtn);
                        }
                        else
                        {
                            var dlBtn = new Button
                            {
                                Content = vid.IsDownloaded ? NexLocale.T("cust_btn_apply_desktop") : NexLocale.T("cust_btn_download_apply"),
                                Style = (Style)FindResource("DarkTableFooterButtonStyle"),
                                Padding = new Thickness(8, 5, 8, 5),
                                HorizontalAlignment = HorizontalAlignment.Stretch,
                                Cursor = Cursors.Hand
                            };
                            var capturedVid = vid;
                            dlBtn.Click += (_, _) =>
                            {
                                _downloadingVideoId = capturedVid.Id.ToString();
                                _downloadProgressPercent = 0.05;
                                ShowCustomizer();

                                Task.Run(async () =>
                                {
                                    var prog = new Progress<double>(p =>
                                    {
                                        Dispatcher.Invoke(() => { _downloadProgressPercent = p; });
                                    });
                                    string downloaded = await PixabayVideoService.DownloadVideoAsync(capturedVid, prog);
                                    await Dispatcher.InvokeAsync(() =>
                                    {
                                        _downloadingVideoId = null;
                                        if (File.Exists(downloaded))
                                        {
                                            LiveWallpaperWindow.StartLive(downloaded);
                                            ShowToast(NexLocale.T("cust_toast_live_active_title"), NexLocale.T("cust_toast_live_active_msg"), NexIcon.Check, GreenBrush);
                                        }
                                        else
                                        {
                                            ShowToast(NexLocale.T("cust_toast_err_download"), NexLocale.T("cust_toast_err_download_msg"), NexIcon.Warning, RedBrush);
                                        }
                                        NativeTuning.TrimWorkingSet();
                                        ShowCustomizer();
                                    });
                                });
                            };
                            cardStack.Children.Add(dlBtn);
                        }

                        vCard.Child = cardStack;
                        int row = i / 3;
                        int col = (i % 3) * 2;
                        Grid.SetRow(vCard, row);
                        Grid.SetColumn(vCard, col);
                        vGrid.Children.Add(vCard);
                    }
                    PageRoot.Children.Add(vGrid);
                }

            }
            // ================= MODE B: WALLPAPERS (STATIC PHOTOS) =================
            else if (_wallpaperEngineMode == "Static")
            {
                var staticHeader = new Border
                {
                    Background = CardBackground(),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(24, 38, 56)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(16, 14, 16, 14),
                    Margin = new Thickness(0, 0, 0, 14)
                };
                var staticStack = new StackPanel();

                var staticTitleText = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
                staticTitleText.Children.Add(new TextBlock
                {
                    Text = NexLocale.T("cust_static_header"),
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    Foreground = TextBrush
                });
                staticTitleText.Children.Add(new TextBlock
                {
                    Text = NexLocale.T("cust_static_header_desc"),
                    FontSize = 11,
                    Foreground = MutedBrush,
                    Margin = new Thickness(0, 2, 0, 0)
                });
                staticStack.Children.Add(staticTitleText);

                // Row 1: Search Bar & Action Buttons
                var searchRow = new Grid { Margin = new Thickness(0, 0, 0, 10) };
                searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
                searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
                searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var searchBoxContainer = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(11, 22, 38)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(37, 70, 110)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(7),
                    Padding = new Thickness(8, 2, 6, 2)
                };
                var sGrid = new Grid();
                var searchInput = new TextBox
                {
                    Text = _staticSearchQuery,
                    Background = Brushes.Transparent,
                    Foreground = TextBrush,
                    CaretBrush = CyanBrush,
                    BorderThickness = new Thickness(0),
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center,
                    Padding = new Thickness(4, 4, 4, 4)
                };
                sGrid.Children.Add(searchInput);
                searchBoxContainer.Child = sGrid;
                Grid.SetColumn(searchBoxContainer, 0);
                searchRow.Children.Add(searchBoxContainer);

                var sBtn = new Button
                {
                    Content = NexLocale.T("cust_btn_search"),
                    Style = (Style)FindResource("DarkTableFooterButtonStyle"),
                    Padding = new Thickness(16, 6, 16, 6),
                    Cursor = Cursors.Hand,
                    VerticalAlignment = VerticalAlignment.Center
                };
                sBtn.Click += (_, _) => TriggerStaticSearch(searchInput.Text.Trim());
                searchInput.KeyDown += (_, e) =>
                {
                    if (e.Key == Key.Enter) TriggerStaticSearch(searchInput.Text.Trim());
                };
                Grid.SetColumn(sBtn, 2);
                searchRow.Children.Add(sBtn);

                staticStack.Children.Add(searchRow);

                // Row 2: Advanced Filter Controls (Purity / NSFW, Rezoluție, Sortare, Format)
                var filtersRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };

                // Dropdown 1: Rezoluție
                string staticResLabel = _staticResolution switch
                {
                    "MyScreen" => NexLocale.Format("cust_res_my_screen_short_format", screenW, screenH),
                    "4K UHD" => "4K UHD (3840x2160+)",
                    "1440p QHD" => "1440p QHD",
                    "1080p FHD" => "1080p FHD",
                    _ => NexLocale.T("cust_res_all_label", "Toate Rezoluțiile")
                };
                var resDropBtn = new Button
                {
                    Content = NexLocale.Format("cust_res_btn_format", staticResLabel),
                    Style = (Style)FindResource("DarkTableFooterButtonStyle"),
                    Padding = new Thickness(10, 5, 10, 5),
                    Margin = new Thickness(0, 0, 8, 6),
                    Cursor = Cursors.Hand
                };
                resDropBtn.Click += (_, _) =>
                {
                    var m = new ContextMenu { Style = (Style)FindResource("DarkContextMenuStyle"), PlacementTarget = resDropBtn, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
                    var opts = new (string Key, string Label)[]
                    {
                        ("Toate", NexLocale.T("cust_res_all_label", "Toate Rezoluțiile")),
                        ("MyScreen", NexLocale.Format("cust_res_my_screen_format", screenW, screenH)),
                        ("4K UHD", "4K UHD (3840x2160+)"),
                        ("1440p QHD", "1440p QHD (2560x1440)"),
                        ("1080p FHD", "1080p FHD (1920x1080)")
                    };
                    foreach (var (k, l) in opts)
                    {
                        bool isSel = _staticResolution.Equals(k, StringComparison.OrdinalIgnoreCase);
                        var mi = new MenuItem
                        {
                            Header = l,
                            Style = (Style)FindResource("DarkMenuItemStyle"),
                            Icon = isSel ? CreateVectorIcon(NexIcon.Check, CyanBrush, 12) : null,
                            Foreground = isSel ? CyanBrush : (Brush)FindResource("TextPrimary"),
                            FontWeight = isSel ? FontWeights.Bold : FontWeights.Normal
                        };
                        var capK = k;
                        mi.Click += (_, _) => { _staticResolution = capK; TriggerStaticSearch(_staticSearchQuery); };
                        m.Items.Add(mi);
                    }
                    m.IsOpen = true;
                };
                filtersRow.Children.Add(resDropBtn);

                // Dropdown 2: Filtru Conținut / Purity (SFW, Sketchy, NSFW)
                string purityLabel = _staticPurity switch
                {
                    "110" => NexLocale.T("cust_purity_sketchy", "SFW + Sketchy"),
                    "111" => NexLocale.T("cust_purity_all", "Toate (inclusiv NSFW 18+)"),
                    "001" => NexLocale.T("cust_purity_nsfw", "Doar NSFW (18+)"),
                    _ => NexLocale.T("cust_purity_sfw", "SFW (Sigur)")
                };
                bool isNsfwActive = _staticPurity == "111" || _staticPurity == "001";
                var purityDropBtn = new Button
                {
                    Content = NexLocale.Format("cust_purity_btn_format", purityLabel),
                    Style = (Style)FindResource("DarkTableFooterButtonStyle"),
                    Foreground = isNsfwActive ? RedBrush : (purityLabel.Contains("Sketchy") ? AmberBrush : TextBrush),
                    Padding = new Thickness(10, 5, 10, 5),
                    Margin = new Thickness(0, 0, 8, 6),
                    Cursor = Cursors.Hand
                };
                purityDropBtn.Click += (_, _) =>
                {
                    var m = new ContextMenu { Style = (Style)FindResource("DarkContextMenuStyle"), PlacementTarget = purityDropBtn, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
                    var opts = new (string Key, string Label)[]
                    {
                        ("100", NexLocale.T("cust_purity_opt_100", "SFW (Sigur / Implicit)")),
                        ("110", NexLocale.T("cust_purity_opt_110", "SFW + Sketchy (Artistic)")),
                        ("111", NexLocale.T("cust_purity_opt_111", "Toate (SFW + Sketchy + NSFW 18+)")),
                        ("001", NexLocale.T("cust_purity_opt_001", "Doar NSFW (18+)"))
                    };
                    foreach (var (k, l) in opts)
                    {
                        bool isSel = _staticPurity.Equals(k, StringComparison.OrdinalIgnoreCase);
                        var mi = new MenuItem
                        {
                            Header = l,
                            Style = (Style)FindResource("DarkMenuItemStyle"),
                            Icon = isSel ? CreateVectorIcon(NexIcon.Check, CyanBrush, 12) : null,
                            Foreground = isSel ? CyanBrush : (Brush)FindResource("TextPrimary"),
                            FontWeight = isSel ? FontWeights.Bold : FontWeights.Normal
                        };
                        var capK = k;
                        mi.Click += (_, _) => { _staticPurity = capK; TriggerStaticSearch(_staticSearchQuery); };
                        m.Items.Add(mi);
                    }
                    m.IsOpen = true;
                };
                filtersRow.Children.Add(purityDropBtn);

                // Dropdown 3: Sortare
                string sortLabel = _staticSorting switch
                {
                    "hot" => NexLocale.T("cust_sort_hot", "Hot (În tendințe)"),
                    "date_added" => NexLocale.T("cust_sort_recent", "Recente"),
                    "random" => NexLocale.T("cust_sort_random", "Aleatoriu"),
                    "views" => NexLocale.T("cust_sort_views", "Vizualizări"),
                    "favorites" => NexLocale.T("cust_sort_favorites", "Favorite"),
                    _ => NexLocale.T("cust_sort_top", "Toplist (Cele mai votate)")
                };
                var sortDropBtn = new Button
                {
                    Content = NexLocale.Format("cust_sort_btn_format", sortLabel),
                    Style = (Style)FindResource("DarkTableFooterButtonStyle"),
                    Padding = new Thickness(10, 5, 10, 5),
                    Margin = new Thickness(0, 0, 8, 6),
                    Cursor = Cursors.Hand
                };
                sortDropBtn.Click += (_, _) =>
                {
                    var m = new ContextMenu { Style = (Style)FindResource("DarkContextMenuStyle"), PlacementTarget = sortDropBtn, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
                    var opts = new (string Key, string Label)[]
                    {
                        ("toplist", NexLocale.T("cust_sort_top", "Toplist (Cele mai votate)")),
                        ("hot", NexLocale.T("cust_sort_hot", "Hot (În tendințe)")),
                        ("date_added", NexLocale.T("cust_sort_recent_opt", "Recente (Ultimele adăugate)")),
                        ("random", NexLocale.T("cust_sort_random_opt", "Aleatoriu (Random)")),
                        ("views", NexLocale.T("cust_sort_views_opt", "Cele mai vizualizate")),
                        ("favorites", NexLocale.T("cust_sort_favorites_opt", "Cele mai adăugate la favorite"))
                    };
                    foreach (var (k, l) in opts)
                    {
                        bool isSel = _staticSorting.Equals(k, StringComparison.OrdinalIgnoreCase);
                        var mi = new MenuItem
                        {
                            Header = l,
                            Style = (Style)FindResource("DarkMenuItemStyle"),
                            Icon = isSel ? CreateVectorIcon(NexIcon.Check, CyanBrush, 12) : null,
                            Foreground = isSel ? CyanBrush : (Brush)FindResource("TextPrimary"),
                            FontWeight = isSel ? FontWeights.Bold : FontWeights.Normal
                        };
                        var capK = k;
                        mi.Click += (_, _) => { _staticSorting = capK; TriggerStaticSearch(_staticSearchQuery); };
                        m.Items.Add(mi);
                    }
                    m.IsOpen = true;
                };
                filtersRow.Children.Add(sortDropBtn);

                // Dropdown 4: Format / Raport Aspect
                string ratioLabel = _staticRatio switch
                {
                    "16:9" => NexLocale.T("cust_ratio_16_9", "16:9 (Standard)"),
                    "21:9" => NexLocale.T("cust_ratio_21_9", "21:9 (Ultrawide)"),
                    "16:10" => NexLocale.T("cust_ratio_16_10", "16:10"),
                    _ => NexLocale.T("cust_ratio_all", "Toate Formatele")
                };
                var ratioDropBtn = new Button
                {
                    Content = NexLocale.Format("cust_ratio_btn_format", ratioLabel),
                    Style = (Style)FindResource("DarkTableFooterButtonStyle"),
                    Padding = new Thickness(10, 5, 10, 5),
                    Margin = new Thickness(0, 0, 8, 6),
                    Cursor = Cursors.Hand
                };
                ratioDropBtn.Click += (_, _) =>
                {
                    var m = new ContextMenu { Style = (Style)FindResource("DarkContextMenuStyle"), PlacementTarget = ratioDropBtn, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
                    var opts = new (string Key, string Label)[]
                    {
                        ("Toate", NexLocale.T("cust_ratio_all", "Toate Formatele")),
                        ("16:9", NexLocale.T("cust_ratio_opt_16_9", "16:9 (Standard Monitor)")),
                        ("21:9", NexLocale.T("cust_ratio_opt_21_9", "21:9 (Ultrawide Monitor)")),
                        ("16:10", NexLocale.T("cust_ratio_opt_16_10", "16:10 (Productivitate)"))
                    };
                    foreach (var (k, l) in opts)
                    {
                        bool isSel = _staticRatio.Equals(k, StringComparison.OrdinalIgnoreCase);
                        var mi = new MenuItem
                        {
                            Header = l,
                            Style = (Style)FindResource("DarkMenuItemStyle"),
                            Icon = isSel ? CreateVectorIcon(NexIcon.Check, CyanBrush, 12) : null,
                            Foreground = isSel ? CyanBrush : (Brush)FindResource("TextPrimary"),
                            FontWeight = isSel ? FontWeights.Bold : FontWeights.Normal
                        };
                        var capK = k;
                        mi.Click += (_, _) => { _staticRatio = capK; TriggerStaticSearch(_staticSearchQuery); };
                        m.Items.Add(mi);
                    }
                    m.IsOpen = true;
                };
                filtersRow.Children.Add(ratioDropBtn);

                staticStack.Children.Add(filtersRow);

                // Category & Theme Pills
                var staticPills = new WrapPanel { Margin = new Thickness(0, 0, 0, 2) };
                var staticPresets = new (string Label, string Query, string Cat)[]
                {
                    (NexLocale.T("cust_cat_all", "Toate"), "", "111"),
                    (NexLocale.T("cust_cat_general", "General"), "", "100"),
                    (NexLocale.T("cust_cat_anime", "Anime"), "", "010"),
                    (NexLocale.T("cust_cat_people", "Oameni"), "", "001"),
                    (NexLocale.T("cust_cat_cyberpunk", "Cyberpunk"), "cyberpunk", "111"),
                    (NexLocale.T("cust_cat_space", "Spațiu"), "space", "111"),
                    (NexLocale.T("cust_cat_nature", "Natură"), "nature", "111"),
                    (NexLocale.T("cust_cat_minimalist", "Minimalist"), "minimalism", "111"),
                    (NexLocale.T("cust_cat_gaming", "Gaming"), "gaming", "111"),
                    (NexLocale.T("cust_cat_cars", "Mașini"), "cars", "111"),
                    (NexLocale.T("cust_cat_fantasy", "Fantasy"), "fantasy", "111")
                };

                foreach (var (pLabel, pQuery, pCat) in staticPresets)
                {
                    bool isActivePill = (_staticCategory == pCat && string.IsNullOrEmpty(pQuery) && string.IsNullOrEmpty(_staticSearchQuery)) ||
                                        (!string.IsNullOrEmpty(pQuery) && _staticSearchQuery.Equals(pQuery, StringComparison.OrdinalIgnoreCase));
                    var pill = new Border
                    {
                        Background = isActivePill ? new SolidColorBrush(Color.FromRgb(15, 38, 74)) : new SolidColorBrush(Color.FromRgb(13, 20, 32)),
                        BorderBrush = isActivePill ? new SolidColorBrush(Color.FromRgb(37, 99, 235)) : new SolidColorBrush(Color.FromRgb(24, 38, 56)),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(5),
                        Padding = new Thickness(10, 4, 10, 4),
                        Margin = new Thickness(0, 0, 6, 6),
                        Cursor = Cursors.Hand
                    };
                    pill.Child = new TextBlock
                    {
                        Text = pLabel,
                        FontSize = 11,
                        Foreground = isActivePill ? CyanBrush : MutedBrush,
                        FontWeight = isActivePill ? FontWeights.Bold : FontWeights.Normal
                    };
                    var capCat = pCat;
                    var capQ = pQuery;
                    pill.MouseLeftButtonUp += (_, _) =>
                    {
                        _staticCategory = capCat;
                        _staticSearchQuery = capQ;
                        TriggerStaticSearch(capQ);
                    };
                    staticPills.Children.Add(pill);
                }
                staticStack.Children.Add(staticPills);

                staticHeader.Child = staticStack;
                PageRoot.Children.Add(staticHeader);

                // Results Grid for Wallpapers (Static Photos)
                if (_isStaticLoading)
                {
                    var loadingCard = new Border
                    {
                        Background = CardBackground(),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(24, 38, 56)),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(10),
                        Padding = new Thickness(30),
                        Margin = new Thickness(0, 0, 0, 16),
                        HorizontalAlignment = HorizontalAlignment.Stretch
                    };
                    var loadStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                    loadStack.Children.Add(CreateVectorIcon(NexIcon.Refresh, CyanBrush, 24));
                    loadStack.Children.Add(new TextBlock
                    {
                        Text = NexLocale.T("cust_static_searching"),
                        FontSize = 13,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = TextBrush,
                        Margin = new Thickness(0, 10, 0, 0)
                    });
                    loadingCard.Child = loadStack;
                    PageRoot.Children.Add(loadingCard);
                }
                else if (_cachedStaticWallpapers == null || _cachedStaticWallpapers.Count == 0)
                {
                    var emptyCard = new Border
                    {
                        Background = CardBackground(),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(24, 38, 56)),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(10),
                        Padding = new Thickness(30),
                        Margin = new Thickness(0, 0, 0, 16),
                        HorizontalAlignment = HorizontalAlignment.Stretch
                    };
                    var emptyStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                    emptyStack.Children.Add(CreateVectorIcon(NexIcon.Photo, MutedBrush, 24));
                    emptyStack.Children.Add(new TextBlock
                    {
                        Text = NexLocale.T("cust_static_not_found"),
                        FontSize = 13,
                        Foreground = TextBrush,
                        Margin = new Thickness(0, 10, 0, 8)
                    });
                    var retryBtn = new Button
                    {
                        Content = NexLocale.T("cust_btn_reset_filters"),
                        Style = (Style)FindResource("DarkTableFooterButtonStyle"),
                        Padding = new Thickness(12, 6, 12, 6),
                        Cursor = Cursors.Hand
                    };
                    retryBtn.Click += (_, _) =>
                    {
                        _staticSearchQuery = "";
                        _staticCategory = "111";
                        _staticPurity = "100";
                        _staticResolution = "Toate";
                        TriggerStaticSearch("");
                    };
                    emptyStack.Children.Add(retryBtn);
                    emptyCard.Child = emptyStack;
                    PageRoot.Children.Add(emptyCard);
                }
                else
                {
                    var displayPhotos = _cachedStaticWallpapers.ToList();
                    var pGrid = new Grid { Margin = new Thickness(0, 0, 0, 16) };
                    pGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    pGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
                    pGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    pGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
                    pGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    int pRowCount = (int)Math.Ceiling(displayPhotos.Count / 3.0);
                    for (int r = 0; r < pRowCount; r++)
                    {
                        pGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    }

                    for (int i = 0; i < displayPhotos.Count; i++)
                    {
                        var photo = displayPhotos[i];
                        bool isDownloading = _downloadingPhotoId == photo.Id;

                        var pCard = new Border
                        {
                            Background = CardBackground(),
                            BorderBrush = new SolidColorBrush(Color.FromRgb(24, 38, 56)),
                            BorderThickness = new Thickness(1),
                            CornerRadius = new CornerRadius(9),
                            Padding = new Thickness(10),
                            Margin = new Thickness(0, 0, 0, 14)
                        };

                        var cStack = new StackPanel();

                        // Image Preview Container
                        var imgBox = new Border
                        {
                            Height = 160,
                            CornerRadius = new CornerRadius(6),
                            ClipToBounds = true,
                            Background = new SolidColorBrush(Color.FromRgb(10, 16, 26))
                        };

                        var iGrid = new Grid();
                        string thumbPath = !string.IsNullOrEmpty(photo.LocalThumbnailPath) && File.Exists(photo.LocalThumbnailPath)
                            ? photo.LocalThumbnailPath
                            : photo.ThumbnailUrl;

                        if (!string.IsNullOrEmpty(thumbPath))
                        {
                            try
                            {
                                var bmp = new BitmapImage();
                                bmp.BeginInit();
                                bmp.CacheOption = BitmapCacheOption.OnLoad;
                                bmp.UriSource = new Uri(thumbPath);
                                bmp.EndInit();
                                iGrid.Children.Add(new Image { Source = bmp, Stretch = Stretch.UniformToFill });
                            }
                            catch { }
                        }

                        // Resolution Badge
                        var rBadge = new Border
                        {
                            Background = new SolidColorBrush(Color.FromArgb(200, 10, 16, 26)),
                            CornerRadius = new CornerRadius(4),
                            Padding = new Thickness(6, 2, 6, 2),
                            HorizontalAlignment = HorizontalAlignment.Left,
                            VerticalAlignment = VerticalAlignment.Top,
                            Margin = new Thickness(8)
                        };
                        rBadge.Child = new TextBlock
                        {
                            Text = photo.Resolution,
                            FontSize = 9.5,
                            FontWeight = FontWeights.Bold,
                            Foreground = CyanBrush
                        };
                        iGrid.Children.Add(rBadge);

                        // Purity Badge (NSFW / Sketchy)
                        if (photo.Purity.Equals("nsfw", StringComparison.OrdinalIgnoreCase))
                        {
                            var nsfwBadge = new Border
                            {
                                Background = new SolidColorBrush(Color.FromArgb(220, 185, 28, 28)),
                                CornerRadius = new CornerRadius(4),
                                Padding = new Thickness(6, 2, 6, 2),
                                HorizontalAlignment = HorizontalAlignment.Right,
                                VerticalAlignment = VerticalAlignment.Top,
                                Margin = new Thickness(8)
                            };
                            nsfwBadge.Child = new TextBlock
                            {
                                Text = "18+ NSFW",
                                FontSize = 9,
                                FontWeight = FontWeights.Bold,
                                Foreground = Brushes.White
                            };
                            iGrid.Children.Add(nsfwBadge);
                        }
                        else if (photo.Purity.Equals("sketchy", StringComparison.OrdinalIgnoreCase))
                        {
                            var skBadge = new Border
                            {
                                Background = new SolidColorBrush(Color.FromArgb(220, 180, 83, 9)),
                                CornerRadius = new CornerRadius(4),
                                Padding = new Thickness(6, 2, 6, 2),
                                HorizontalAlignment = HorizontalAlignment.Right,
                                VerticalAlignment = VerticalAlignment.Top,
                                Margin = new Thickness(8)
                            };
                            skBadge.Child = new TextBlock
                            {
                                Text = "Sketchy",
                                FontSize = 9,
                                FontWeight = FontWeights.Bold,
                                Foreground = Brushes.White
                            };
                            iGrid.Children.Add(skBadge);
                        }

                        imgBox.Child = iGrid;
                        cStack.Children.Add(imgBox);

                        // Info row (Category, File Size)
                        var pInfoRow = new Grid { Margin = new Thickness(0, 8, 0, 6) };
                        pInfoRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        pInfoRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                        string catDisplay = char.ToUpperInvariant(photo.Category[0]) + photo.Category.Substring(1);
                        var cText = new TextBlock
                        {
                            Text = catDisplay,
                            FontSize = 11.5,
                            FontWeight = FontWeights.SemiBold,
                            Foreground = TextBrush
                        };
                        Grid.SetColumn(cText, 0);
                        pInfoRow.Children.Add(cText);

                        double mbSize = (double)photo.FileSizeBytes / (1024 * 1024);
                        if (mbSize > 0)
                        {
                            var sText = new TextBlock
                            {
                                Text = $"{mbSize:F1} MB",
                                FontSize = 10.5,
                                Foreground = MutedBrush,
                                VerticalAlignment = VerticalAlignment.Center
                            };
                            Grid.SetColumn(sText, 1);
                            pInfoRow.Children.Add(sText);
                        }
                        cStack.Children.Add(pInfoRow);

                        // Actions
                        if (isDownloading)
                        {
                            var pStack = new StackPanel { Margin = new Thickness(0, 2, 0, 0) };
                            pStack.Children.Add(new ProgressBar
                            {
                                Height = 4,
                                Maximum = 1.0,
                                Value = _staticProgressPercent,
                                Background = new SolidColorBrush(Color.FromRgb(24, 38, 56)),
                                Foreground = CyanBrush,
                                BorderThickness = new Thickness(0)
                            });
                            pStack.Children.Add(new TextBlock
                            {
                                Text = NexLocale.Format("cust_downloading_progress_format", _staticProgressPercent * 100),
                                FontSize = 10,
                                Foreground = CyanBrush,
                                HorizontalAlignment = HorizontalAlignment.Center,
                                Margin = new Thickness(0, 3, 0, 0)
                            });
                            cStack.Children.Add(pStack);
                        }
                        else
                        {
                            var applyBtn = new Button
                            {
                                Content = NexLocale.T("cust_btn_apply_desktop"),
                                Style = (Style)FindResource("DarkTableFooterButtonStyle"),
                                Padding = new Thickness(8, 5, 8, 5),
                                HorizontalAlignment = HorizontalAlignment.Stretch,
                                Cursor = Cursors.Hand
                            };
                            var capPhoto = photo;
                            applyBtn.Click += (_, _) =>
                            {
                                _downloadingPhotoId = capPhoto.Id;
                                _staticProgressPercent = 0.05;
                                ShowCustomizer();

                                Task.Run(async () =>
                                {
                                    var prog = new Progress<double>(p =>
                                    {
                                        Dispatcher.Invoke(() => { _staticProgressPercent = p; });
                                    });

                                    string downloaded = await WallhavenService.DownloadWallpaperAsync(capPhoto, prog);
                                    await Dispatcher.InvokeAsync(() =>
                                    {
                                        _downloadingPhotoId = null;
                                        if (File.Exists(downloaded))
                                        {
                                            if (LiveWallpaperWindow.IsRunning)
                                            {
                                                LiveWallpaperWindow.StopLive();
                                            }
                                            bool applied = WallhavenService.ApplyAsDesktopWallpaper(downloaded);
                                            if (applied)
                                            {
                                                ShowToast(NexLocale.T("cust_toast_wp_applied_title"), NexLocale.Format("cust_toast_wp_applied_msg", capPhoto.Resolution), NexIcon.Check, GreenBrush);
                                            }
                                            else
                                            {
                                                ShowToast(NexLocale.T("status_error"), NexLocale.T("cust_toast_err_set_wp"), NexIcon.Warning, AmberBrush);
                                            }
                                        }
                                        else
                                        {
                                            ShowToast(NexLocale.T("cust_toast_err_download"), NexLocale.T("cust_toast_err_dl_img"), NexIcon.Warning, RedBrush);
                                        }
                                        NativeTuning.TrimWorkingSet();
                                        ShowCustomizer();
                                    });
                                });
                            };
                            cStack.Children.Add(applyBtn);
                        }

                        pCard.Child = cStack;
                        int row = i / 3;
                        int col = (i % 3) * 2;
                        Grid.SetRow(pCard, row);
                        Grid.SetColumn(pCard, col);
                        pGrid.Children.Add(pCard);
                    }
                }
            }
            // ================= MODE C: BIBLIOTECA MEA (LOCAL DOWNLOADED MEDIA) =================
            else if (_wallpaperEngineMode == "Library")
            {
                var localVideos = PixabayVideoService.GetLocalWallpapers();
                var localPhotos = WallhavenService.GetLocalWallpapers();

                var libHeader = new Border
                {
                    Background = CardBackground(),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(24, 38, 56)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(16, 14, 16, 14),
                    Margin = new Thickness(0, 0, 0, 14)
                };
                var libStack = new StackPanel();

                // Top row: Title and "Deschide Dosar"
                var libTopRow = new Grid { Margin = new Thickness(0, 0, 0, 12) };
                libTopRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                libTopRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var libTitleStack = new StackPanel();
                libTitleStack.Children.Add(new TextBlock
                {
                    Text = NexLocale.T("cust_lib_header"),
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    Foreground = TextBrush
                });
                libTitleStack.Children.Add(new TextBlock
                {
                    Text = NexLocale.T("cust_lib_header_desc"),
                    FontSize = 11,
                    Foreground = MutedBrush,
                    Margin = new Thickness(0, 2, 0, 0)
                });
                Grid.SetColumn(libTitleStack, 0);
                libTopRow.Children.Add(libTitleStack);

                var openFolderBtn = new Button
                {
                    Style = (Style)FindResource("DarkTableFooterButtonStyle"),
                    Padding = new Thickness(12, 6, 12, 6),
                    Cursor = Cursors.Hand
                };
                var openSp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                var openIco = CreateVectorIcon(NexIcon.Folder, CyanBrush, 13);
                openIco.Margin = new Thickness(0, 0, 6, 0);
                openSp.Children.Add(openIco);
                openSp.Children.Add(new TextBlock { Text = NexLocale.T("cust_btn_open_folder"), FontSize = 11.5, Foreground = TextBrush, VerticalAlignment = VerticalAlignment.Center });
                openFolderBtn.Content = openSp;
                openFolderBtn.Click += (_, _) =>
                {
                    try
                    {
                        string dir = _libraryCategory == "Live"
                            ? PixabayVideoService.GetWallpapersDirectory()
                            : WallhavenService.GetWallpapersDirectory();
                        if (Directory.Exists(dir))
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = dir,
                                UseShellExecute = true
                            });
                        }
                    }
                    catch { }
                };
                Grid.SetColumn(openFolderBtn, 1);
                libTopRow.Children.Add(openFolderBtn);
                libStack.Children.Add(libTopRow);

                // Category Switcher Row: [ Live Wallpapers (N) ] and [ Wallpapers (M) ]
                var catRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 2) };

                Border MakeLibCategoryPill(string catKey, string label, NexIcon icon, int count)
                {
                    bool isSel = _libraryCategory.Equals(catKey, StringComparison.OrdinalIgnoreCase);
                    var b = new Border
                    {
                        Background = isSel ? new SolidColorBrush(Color.FromRgb(15, 38, 74)) : new SolidColorBrush(Color.FromRgb(13, 20, 32)),
                        BorderBrush = isSel ? new SolidColorBrush(Color.FromRgb(37, 99, 235)) : new SolidColorBrush(Color.FromRgb(24, 38, 56)),
                        BorderThickness = new Thickness(1.5),
                        CornerRadius = new CornerRadius(7),
                        Padding = new Thickness(14, 6, 14, 6),
                        Margin = new Thickness(0, 0, 10, 0),
                        Cursor = Cursors.Hand
                    };
                    var sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                    var ico = CreateVectorIcon(icon, isSel ? CyanBrush : MutedBrush, 12);
                    ico.Margin = new Thickness(0, 0, 7, 0);
                    sp.Children.Add(ico);
                    sp.Children.Add(new TextBlock
                    {
                        Text = $"{label} ({count})",
                        FontSize = 11.5,
                        FontWeight = isSel ? FontWeights.Bold : FontWeights.Normal,
                        Foreground = isSel ? CyanBrush : TextBrush
                    });
                    b.Child = sp;
                    b.MouseLeftButtonUp += (_, _) =>
                    {
                        _libraryCategory = catKey;
                        NativeTuning.TrimWorkingSet();
                        ShowCustomizer();
                    };
                    return b;
                }

                catRow.Children.Add(MakeLibCategoryPill("Live", NexLocale.T("cust_mode_live"), NexIcon.Play, localVideos.Count));
                catRow.Children.Add(MakeLibCategoryPill("Static", NexLocale.T("cust_mode_static"), NexIcon.Photo, localPhotos.Count));
                libStack.Children.Add(catRow);

                libHeader.Child = libStack;
                PageRoot.Children.Add(libHeader);
                PageRoot.Children.Add(BuildAutoWallpaperCard(localPhotos.Count + localVideos.Count));

                // ================= LIBRARY SUB-CATEGORY A: LIVE WALLPAPERS =================
                if (_libraryCategory == "Live")
                {
                    if (localVideos.Count == 0)
                    {
                        var emptyCard = new Border
                        {
                            Background = CardBackground(),
                            BorderBrush = new SolidColorBrush(Color.FromRgb(24, 38, 56)),
                            BorderThickness = new Thickness(1),
                            CornerRadius = new CornerRadius(10),
                            Padding = new Thickness(24, 36, 24, 36),
                            Margin = new Thickness(0, 10, 0, 16)
                        };
                        var emptyStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                        var emptyIcoBox = new Border
                        {
                            Width = 44,
                            Height = 44,
                            CornerRadius = new CornerRadius(22),
                            Background = new SolidColorBrush(Color.FromArgb(25, 56, 189, 248)),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin = new Thickness(0, 0, 0, 12)
                        };
                        emptyIcoBox.Child = CreateVectorIcon(NexIcon.Folder, CyanBrush, 20);
                        emptyStack.Children.Add(emptyIcoBox);

                        emptyStack.Children.Add(new TextBlock
                        {
                            Text = NexLocale.T("cust_lib_empty_live_title"),
                            FontSize = 13,
                            FontWeight = FontWeights.SemiBold,
                            Foreground = TextBrush,
                            HorizontalAlignment = HorizontalAlignment.Center
                        });
                        emptyStack.Children.Add(new TextBlock
                        {
                            Text = NexLocale.T("cust_lib_empty_live_desc"),
                            FontSize = 11.5,
                            Foreground = MutedBrush,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin = new Thickness(0, 4, 0, 16)
                        });

                        var exploreBtn = new Button
                        {
                            Content = NexLocale.T("cust_btn_explore_live"),
                            Style = (Style)FindResource("DarkTableFooterButtonStyle"),
                            Foreground = CyanBrush,
                            Padding = new Thickness(16, 7, 16, 7),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Cursor = Cursors.Hand
                        };
                        exploreBtn.Click += (_, _) =>
                        {
                            _wallpaperEngineMode = "Live";
                            NativeTuning.TrimWorkingSet();
                            ShowCustomizer();
                        };
                        emptyStack.Children.Add(exploreBtn);

                        emptyCard.Child = emptyStack;
                        PageRoot.Children.Add(emptyCard);
                    }
                    else
                    {
                        var locGrid = new Grid { Margin = new Thickness(0, 0, 0, 16) };
                        locGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        locGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
                        locGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        locGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
                        locGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                        int locRowCount = (int)Math.Ceiling(localVideos.Count / 3.0);
                        for (int r = 0; r < locRowCount; r++)
                        {
                            locGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                        }

                        for (int i = 0; i < localVideos.Count; i++)
                        {
                            var lItem = localVideos[i];
                            bool isLocRunning = isLiveRunning && !string.IsNullOrEmpty(activeName) && activeName.Equals(lItem.FileName, StringComparison.OrdinalIgnoreCase);

                            var lCard = new Border
                            {
                                Background = CardBackground(),
                                BorderBrush = isLocRunning ? GreenBrush : new SolidColorBrush(Color.FromRgb(24, 38, 56)),
                                BorderThickness = new Thickness(isLocRunning ? 1.5 : 1),
                                CornerRadius = new CornerRadius(9),
                                Padding = new Thickness(10),
                                Margin = new Thickness(0, 0, 0, 14)
                            };

                            var lStack = new StackPanel();

                            // Thumbnail Box
                            var lThumbBox = new Border
                            {
                                Height = 140,
                                CornerRadius = new CornerRadius(6),
                                ClipToBounds = true,
                                Background = new SolidColorBrush(Color.FromRgb(10, 16, 26))
                            };

                            var lThumbGrid = new Grid();

                            if (!string.IsNullOrEmpty(lItem.ThumbnailPath) && File.Exists(lItem.ThumbnailPath))
                            {
                                try
                                {
                                    var bmp = new BitmapImage();
                                    bmp.BeginInit();
                                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                                    bmp.DecodePixelWidth = 400;
                                    bmp.UriSource = new Uri(lItem.ThumbnailPath);
                                    bmp.EndInit();
                                    bmp.Freeze();
                                    lThumbGrid.Children.Add(new Image { Source = bmp, Stretch = Stretch.UniformToFill });
                                }
                                catch { }
                            }
                            else
                            {
                                var ph = new Grid { Background = new SolidColorBrush(Color.FromRgb(14, 22, 34)) };
                                ph.Children.Add(CreateVectorIcon(NexIcon.Play, CyanBrush, 28));
                                lThumbGrid.Children.Add(ph);
                            }

                            // Dark overlay gradient
                            var gradOverlay = new Border
                            {
                                Background = new LinearGradientBrush(
                                    Color.FromArgb(0, 0, 0, 0),
                                    Color.FromArgb(160, 0, 0, 0),
                                    90
                                )
                            };
                            lThumbGrid.Children.Add(gradOverlay);

                            // Top Badges
                            var topBadges = new StackPanel
                            {
                                Orientation = Orientation.Horizontal,
                                HorizontalAlignment = HorizontalAlignment.Left,
                                VerticalAlignment = VerticalAlignment.Top,
                                Margin = new Thickness(8, 8, 0, 0)
                            };

                            if (isLocRunning)
                            {
                                var actBadge = new Border
                                {
                                    Background = new SolidColorBrush(Color.FromArgb(210, 16, 185, 129)),
                                    CornerRadius = new CornerRadius(4),
                                    Padding = new Thickness(6, 2, 6, 2),
                                    Margin = new Thickness(0, 0, 6, 0)
                                };
                                actBadge.Child = new TextBlock
                                {
                                    Text = NexLocale.T("cust_lib_badge_active", "ACTIV PE DESKTOP"),
                                    FontSize = 9.5,
                                    FontWeight = FontWeights.Bold,
                                    Foreground = new SolidColorBrush(Colors.White)
                                };
                                topBadges.Children.Add(actBadge);
                            }

                            var formatBadge = new Border
                            {
                                Background = new SolidColorBrush(Color.FromArgb(190, 8, 14, 24)),
                                CornerRadius = new CornerRadius(4),
                                Padding = new Thickness(6, 2, 6, 2)
                            };
                            formatBadge.Child = new TextBlock
                            {
                                Text = "VIDEO MP4",
                                FontSize = 9.5,
                                FontWeight = FontWeights.Bold,
                                Foreground = CyanBrush
                            };
                            topBadges.Children.Add(formatBadge);
                            lThumbGrid.Children.Add(topBadges);

                            // Bottom Size Badge
                            var sizeBadge = new Border
                            {
                                Background = new SolidColorBrush(Color.FromArgb(190, 8, 14, 24)),
                                CornerRadius = new CornerRadius(4),
                                Padding = new Thickness(6, 2, 6, 2),
                                HorizontalAlignment = HorizontalAlignment.Right,
                                VerticalAlignment = VerticalAlignment.Bottom,
                                Margin = new Thickness(0, 0, 8, 8)
                            };
                            double sizeMb = lItem.FileSizeBytes / (1024.0 * 1024.0);
                            sizeBadge.Child = new TextBlock
                            {
                                Text = $"{sizeMb:F1} MB",
                                FontSize = 9.5,
                                FontWeight = FontWeights.SemiBold,
                                Foreground = new SolidColorBrush(Colors.White)
                            };
                            lThumbGrid.Children.Add(sizeBadge);

                            lThumbBox.Child = lThumbGrid;
                            lStack.Children.Add(lThumbBox);

                            // Details Row
                            var infoRow = new Grid { Margin = new Thickness(2, 8, 2, 8) };
                            infoRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                            infoRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                            string cleanTitle = System.IO.Path.GetFileNameWithoutExtension(lItem.FileName);
                            if (cleanTitle.StartsWith("pixabay_", StringComparison.OrdinalIgnoreCase))
                            {
                                cleanTitle = NexLocale.T("cust_clean_title_video", "Wallpaper Video #") + cleanTitle.Substring(8);
                            }

                            var nameText = new TextBlock
                            {
                                Text = cleanTitle,
                                FontSize = 12,
                                FontWeight = FontWeights.SemiBold,
                                Foreground = TextBrush,
                                TextTrimming = TextTrimming.CharacterEllipsis
                            };
                            Grid.SetColumn(nameText, 0);
                            infoRow.Children.Add(nameText);

                            var dateText = new TextBlock
                            {
                                Text = lItem.DateAdded.ToString("dd.MM.yyyy"),
                                FontSize = 10.5,
                                Foreground = MutedBrush,
                                VerticalAlignment = VerticalAlignment.Center
                            };
                            Grid.SetColumn(dateText, 1);
                            infoRow.Children.Add(dateText);
                            lStack.Children.Add(infoRow);

                            // Actions Row
                            var btnRow = new Grid();
                            btnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                            btnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
                            btnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                            var applyBtn = new Button
                            {
                                Content = isLocRunning ? NexLocale.T("cust_lib_status_playing", "În Redare") : NexLocale.T("cust_btn_apply_desktop", "Aplică pe Desktop"),
                                Style = (Style)FindResource("DarkTableFooterButtonStyle"),
                                Foreground = isLocRunning ? GreenBrush : CyanBrush,
                                Padding = new Thickness(8, 6, 8, 6),
                                Cursor = Cursors.Hand
                            };
                            var capturedLocPath = lItem.FilePath;
                            applyBtn.Click += (_, _) =>
                            {
                                LiveWallpaperWindow.StartLive(capturedLocPath);
                                ShowToast(NexLocale.T("cust_lib_toast_activated_title", "Live Wallpaper Activat"), NexLocale.T("cust_lib_toast_activated_msg", "Fundalul animat rulează pe desktop."), NexIcon.Check, GreenBrush);
                                ShowCustomizer();
                            };
                            Grid.SetColumn(applyBtn, 0);
                            btnRow.Children.Add(applyBtn);

                            var delBtn = new Button
                            {
                                Content = NexLocale.T("btn_delete", "Șterge"),
                                Style = (Style)FindResource("DarkTableFooterButtonStyle"),
                                Foreground = RedBrush,
                                Padding = new Thickness(8, 6, 8, 6),
                                Cursor = Cursors.Hand
                            };
                            delBtn.Click += (_, _) =>
                            {
                                if (isLocRunning)
                                {
                                    LiveWallpaperWindow.StopLive();
                                }
                                PixabayVideoService.DeleteLocalWallpaper(capturedLocPath);
                                ShowToast(NexLocale.T("cust_lib_toast_deleted_title", "Șters"), NexLocale.T("cust_lib_toast_deleted_msg", "Fișierul a fost eliminat din bibliotecă."), NexIcon.Info, CyanBrush);
                                NativeTuning.TrimWorkingSet();
                                ShowCustomizer();
                            };
                            Grid.SetColumn(delBtn, 2);
                            btnRow.Children.Add(delBtn);

                            lStack.Children.Add(btnRow);

                            lCard.Child = lStack;
                            int r = i / 3;
                            int c = (i % 3) * 2;
                            Grid.SetRow(lCard, r);
                            Grid.SetColumn(lCard, c);
                            locGrid.Children.Add(lCard);
                        }
                        PageRoot.Children.Add(locGrid);
                    }
                }
                // ================= LIBRARY SUB-CATEGORY B: STATIC WALLPAPERS =================
                else if (_libraryCategory == "Static")
                {
                    if (localPhotos.Count == 0)
                    {
                        var emptyCard = new Border
                        {
                            Background = CardBackground(),
                            BorderBrush = new SolidColorBrush(Color.FromRgb(24, 38, 56)),
                            BorderThickness = new Thickness(1),
                            CornerRadius = new CornerRadius(10),
                            Padding = new Thickness(24, 36, 24, 36),
                            Margin = new Thickness(0, 10, 0, 16)
                        };
                        var emptyStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                        var emptyIcoBox = new Border
                        {
                            Width = 44,
                            Height = 44,
                            CornerRadius = new CornerRadius(22),
                            Background = new SolidColorBrush(Color.FromArgb(25, 56, 189, 248)),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin = new Thickness(0, 0, 0, 12)
                        };
                        emptyIcoBox.Child = CreateVectorIcon(NexIcon.Photo, CyanBrush, 20);
                        emptyStack.Children.Add(emptyIcoBox);

                        emptyStack.Children.Add(new TextBlock
                        {
                            Text = NexLocale.T("cust_lib_empty_static_title", "Nu ai niciun wallpaper static descărcat încă."),
                            FontSize = 13,
                            FontWeight = FontWeights.SemiBold,
                            Foreground = TextBrush,
                            HorizontalAlignment = HorizontalAlignment.Center
                        });
                        emptyStack.Children.Add(new TextBlock
                        {
                            Text = NexLocale.T("cust_lib_empty_static_desc", "Explorează secțiunea Wallpapers pentru a descoperi și descărca fotografii de înaltă rezoluție 4K și 8K."),
                            FontSize = 11.5,
                            Foreground = MutedBrush,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin = new Thickness(0, 4, 0, 16)
                        });

                        var exploreBtn = new Button
                        {
                            Content = NexLocale.T("cust_btn_explore_static", "Explorează Wallpapers"),
                            Style = (Style)FindResource("DarkTableFooterButtonStyle"),
                            Foreground = CyanBrush,
                            Padding = new Thickness(16, 7, 16, 7),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Cursor = Cursors.Hand
                        };
                        exploreBtn.Click += (_, _) =>
                        {
                            _wallpaperEngineMode = "Static";
                            NativeTuning.TrimWorkingSet();
                            ShowCustomizer();
                        };
                        emptyStack.Children.Add(exploreBtn);

                        emptyCard.Child = emptyStack;
                        PageRoot.Children.Add(emptyCard);
                    }
                    else
                    {
                        var statGrid = new Grid { Margin = new Thickness(0, 0, 0, 16) };
                        statGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        statGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
                        statGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        statGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
                        statGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                        int statRowCount = (int)Math.Ceiling(localPhotos.Count / 3.0);
                        for (int r = 0; r < statRowCount; r++)
                        {
                            statGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                        }

                        for (int i = 0; i < localPhotos.Count; i++)
                        {
                            var sItem = localPhotos[i];

                            var sCard = new Border
                            {
                                Background = CardBackground(),
                                BorderBrush = new SolidColorBrush(Color.FromRgb(24, 38, 56)),
                                BorderThickness = new Thickness(1),
                                CornerRadius = new CornerRadius(9),
                                Padding = new Thickness(10),
                                Margin = new Thickness(0, 0, 0, 14)
                            };

                            var sStack = new StackPanel();

                            // Thumbnail Box
                            var sThumbBox = new Border
                            {
                                Height = 140,
                                CornerRadius = new CornerRadius(6),
                                ClipToBounds = true,
                                Background = new SolidColorBrush(Color.FromRgb(10, 16, 26))
                            };

                            var sThumbGrid = new Grid();

                            if (File.Exists(sItem.FilePath))
                            {
                                try
                                {
                                    var bmp = new BitmapImage();
                                    bmp.BeginInit();
                                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                                    bmp.DecodePixelWidth = 400;
                                    bmp.UriSource = new Uri(sItem.FilePath);
                                    bmp.EndInit();
                                    bmp.Freeze();
                                    sThumbGrid.Children.Add(new Image { Source = bmp, Stretch = Stretch.UniformToFill });
                                }
                                catch { }
                            }
                            else
                            {
                                var ph = new Grid { Background = new SolidColorBrush(Color.FromRgb(14, 22, 34)) };
                                ph.Children.Add(CreateVectorIcon(NexIcon.Photo, CyanBrush, 28));
                                sThumbGrid.Children.Add(ph);
                            }

                            // Dark overlay gradient
                            var gradOverlay = new Border
                            {
                                Background = new LinearGradientBrush(
                                    Color.FromArgb(0, 0, 0, 0),
                                    Color.FromArgb(160, 0, 0, 0),
                                    90
                                )
                            };
                            sThumbGrid.Children.Add(gradOverlay);

                            // Top Badges
                            var topBadges = new StackPanel
                            {
                                Orientation = Orientation.Horizontal,
                                HorizontalAlignment = HorizontalAlignment.Left,
                                VerticalAlignment = VerticalAlignment.Top,
                                Margin = new Thickness(8, 8, 0, 0)
                            };

                            if (!string.IsNullOrEmpty(sItem.Resolution))
                            {
                                var resBadge = new Border
                                {
                                    Background = new SolidColorBrush(Color.FromArgb(190, 8, 14, 24)),
                                    CornerRadius = new CornerRadius(4),
                                    Padding = new Thickness(6, 2, 6, 2)
                                };
                                resBadge.Child = new TextBlock
                                {
                                    Text = sItem.Resolution,
                                    FontSize = 9.5,
                                    FontWeight = FontWeights.Bold,
                                    Foreground = CyanBrush
                                };
                                topBadges.Children.Add(resBadge);
                            }
                            sThumbGrid.Children.Add(topBadges);

                            // Bottom Size Badge
                            var sizeBadge = new Border
                            {
                                Background = new SolidColorBrush(Color.FromArgb(190, 8, 14, 24)),
                                CornerRadius = new CornerRadius(4),
                                Padding = new Thickness(6, 2, 6, 2),
                                HorizontalAlignment = HorizontalAlignment.Right,
                                VerticalAlignment = VerticalAlignment.Bottom,
                                Margin = new Thickness(0, 0, 8, 8)
                            };
                            double sizeMb = sItem.FileSizeBytes / (1024.0 * 1024.0);
                            sizeBadge.Child = new TextBlock
                            {
                                Text = $"{sizeMb:F1} MB",
                                FontSize = 9.5,
                                FontWeight = FontWeights.SemiBold,
                                Foreground = new SolidColorBrush(Colors.White)
                            };
                            sThumbGrid.Children.Add(sizeBadge);

                            sThumbBox.Child = sThumbGrid;
                            sStack.Children.Add(sThumbBox);

                            // Details Row
                            var infoRow = new Grid { Margin = new Thickness(2, 8, 2, 8) };
                            infoRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                            infoRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                            string cleanTitle = System.IO.Path.GetFileNameWithoutExtension(sItem.FileName);
                            if (cleanTitle.StartsWith("wallhaven_", StringComparison.OrdinalIgnoreCase))
                            {
                                cleanTitle = NexLocale.T("cust_clean_title_wall", "Wallpaper #") + cleanTitle.Substring(10);
                            }

                            var nameText = new TextBlock
                            {
                                Text = cleanTitle,
                                FontSize = 12,
                                FontWeight = FontWeights.SemiBold,
                                Foreground = TextBrush,
                                TextTrimming = TextTrimming.CharacterEllipsis
                            };
                            Grid.SetColumn(nameText, 0);
                            infoRow.Children.Add(nameText);

                            var dateText = new TextBlock
                            {
                                Text = sItem.DateAdded.ToString("dd.MM.yyyy"),
                                FontSize = 10.5,
                                Foreground = MutedBrush,
                                VerticalAlignment = VerticalAlignment.Center
                            };
                            Grid.SetColumn(dateText, 1);
                            infoRow.Children.Add(dateText);
                            sStack.Children.Add(infoRow);

                            // Actions Row
                            var btnRow = new Grid();
                            btnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                            btnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
                            btnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                            var applyBtn = new Button
                            {
                                Content = NexLocale.T("cust_btn_apply_desktop", "Aplică pe Desktop"),
                                Style = (Style)FindResource("DarkTableFooterButtonStyle"),
                                Foreground = CyanBrush,
                                Padding = new Thickness(8, 6, 8, 6),
                                Cursor = Cursors.Hand
                            };
                            var capturedLocPath = sItem.FilePath;
                            applyBtn.Click += (_, _) =>
                            {
                                if (LiveWallpaperWindow.IsRunning)
                                {
                                    LiveWallpaperWindow.StopLive();
                                }
                                bool applied = WallhavenService.ApplyAsDesktopWallpaper(capturedLocPath);
                                if (applied)
                                {
                                    ShowToast(NexLocale.T("cust_toast_wp_applied_title", "Wallpaper Aplicat"), NexLocale.T("cust_lib_toast_wp_applied_msg", "Imaginea a fost setată pe desktop!"), NexIcon.Check, GreenBrush);
                                }
                                else
                                {
                                    ShowToast(NexLocale.T("common_error", "Eroare"), NexLocale.T("cust_toast_err_set_wp", "Nu s-a putut seta fundalul pe ecran."), NexIcon.Warning, AmberBrush);
                                }
                                ShowCustomizer();
                            };
                            Grid.SetColumn(applyBtn, 0);
                            btnRow.Children.Add(applyBtn);

                            var delBtn = new Button
                            {
                                Content = NexLocale.T("btn_delete", "Șterge"),
                                Style = (Style)FindResource("DarkTableFooterButtonStyle"),
                                Foreground = RedBrush,
                                Padding = new Thickness(8, 6, 8, 6),
                                Cursor = Cursors.Hand
                            };
                            delBtn.Click += (_, _) =>
                            {
                                WallhavenService.DeleteLocalWallpaper(capturedLocPath);
                                ShowToast(NexLocale.T("cust_lib_toast_deleted_title", "Șters"), NexLocale.T("cust_lib_toast_wp_deleted_msg", "Imaginea a fost eliminată din bibliotecă."), NexIcon.Info, CyanBrush);
                                NativeTuning.TrimWorkingSet();
                                ShowCustomizer();
                            };
                            Grid.SetColumn(delBtn, 2);
                            btnRow.Children.Add(delBtn);

                            sStack.Children.Add(btnRow);

                            sCard.Child = sStack;
                            int r = i / 3;
                            int c = (i % 3) * 2;
                            Grid.SetRow(sCard, r);
                            Grid.SetColumn(sCard, c);
                            statGrid.Children.Add(sCard);
                        }
                        PageRoot.Children.Add(statGrid);
                    }
                }
            }
        }
        // ================= TABS 1-4: TASKBAR, START, EXPLORER, THEME =================
        else
        {
            string categoryName = activeCustomizerTab switch
            {
                "Start" => "Meniu Start Curat",
                "Explorer" => "File Explorer",
                "Theme" => "Efecte Vizuale & Teme",
                _ => "Taskbar & Sistem"
            };

            var items = NativeTuning.GetCustomizerItems().Where(i => i.Category == categoryName).ToList();

            var listGrid = new Grid();
            listGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            listGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            listGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            int rowCount = (int)Math.Ceiling(items.Count / 2.0);
            for (int r = 0; r < rowCount; r++) listGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            for (int i = 0; i < items.Count; i++)
            {
                var itm = items[i];
                var rowBorder = new Border
                {
                    Background = CardBackground(),
                    BorderBrush = new SolidColorBrush(itm.IsEnabled ? Color.FromRgb(28, 54, 48) : Color.FromRgb(24, 38, 56)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(9),
                    Padding = new Thickness(14, 12, 14, 12),
                    Margin = new Thickness(0, 0, 0, 10)
                };

                var rGrid = new Grid();
                rGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
                rGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                rGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var icoBox = new Border
                {
                    Width = 28,
                    Height = 28,
                    CornerRadius = new CornerRadius(6),
                    Background = new SolidColorBrush(Color.FromArgb(30, 56, 189, 248)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(60, 56, 189, 248)),
                    BorderThickness = new Thickness(1),
                    Child = CreateVectorIcon(NexIcon.Sliders, CyanBrush, 13),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                Grid.SetColumn(icoBox, 0);
                rGrid.Children.Add(icoBox);

                var tStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 10, 0) };
                tStack.Children.Add(new TextBlock
                {
                    Text = NexLocale.T($"cust_item_{itm.Id}_title", itm.Title),
                    FontSize = 12.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = TextBrush
                });
                tStack.Children.Add(new TextBlock
                {
                    Text = NexLocale.T($"cust_item_{itm.Id}_desc", itm.Description),
                    FontSize = 10.5,
                    Foreground = MutedBrush,
                    Margin = new Thickness(0, 2, 0, 0),
                    TextWrapping = TextWrapping.Wrap
                });
                Grid.SetColumn(tStack, 1);
                rGrid.Children.Add(tStack);

                var tglBtn = new Button
                {
                    Style = (Style)FindResource("SecondaryButtonStyle"),
                    Padding = new Thickness(14, 6, 14, 6),
                    Cursor = Cursors.Hand,
                    Background = itm.IsEnabled ? new SolidColorBrush(Color.FromArgb(40, 16, 185, 129)) : new SolidColorBrush(Color.FromArgb(40, 71, 85, 105)),
                    BorderBrush = itm.IsEnabled ? GreenBrush : new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                    BorderThickness = new Thickness(1),
                    VerticalAlignment = VerticalAlignment.Center
                };
                var tglText = new TextBlock
                {
                    Text = itm.IsEnabled ? NexLocale.T("status_enabled", "ACTIVAT").ToUpper() : NexLocale.T("status_disabled", "DEZACTIVAT").ToUpper(),
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = itm.IsEnabled ? GreenBrush : MutedBrush
                };
                tglBtn.Content = tglText;

                var curItem = itm;
                tglBtn.Click += (_, _) =>
                {
                    bool newVal = !curItem.IsEnabled;
                    curItem.ApplyAction(newVal);
                    string statusLower = (newVal ? NexLocale.T("status_enabled", "activat") : NexLocale.T("status_disabled", "dezactivat")).ToLower();
                    string localizedTitle = NexLocale.T($"cust_item_{curItem.Id}_title", curItem.Title);
                    AppendLog($"[CUSTOMIZER] {localizedTitle} -> {statusLower}", false);
                    string toastMsg = string.Format(NexLocale.T("cust_toast_modified_msg", "{0} este acum {1}."), localizedTitle, statusLower);
                    ShowToastWithAction(NexLocale.T("cust_toast_modified_title", "Setare Modificată"), toastMsg, NexIcon.Check, GreenBrush, NexLocale.T("cust_btn_restart_explorer", "Repornește Explorer"), async () => await NativeTuning.RestartExplorerAsync());
                    ShowCustomizer();
                };

                Grid.SetColumn(tglBtn, 2);
                rGrid.Children.Add(tglBtn);

                rowBorder.Child = rGrid;

                int col = (i % 2) * 2;
                int row = i / 2;
                Grid.SetColumn(rowBorder, col);
                Grid.SetRow(rowBorder, row);
                listGrid.Children.Add(rowBorder);
            }

            PageRoot.Children.Add(listGrid);
        }
    }

    private Border BuildAutoWallpaperCard(int totalLocalMedia)
    {
        bool isAutoEnabled = NativeTuning.GetAutoWallpaperEnabled();
        int intervalMins = NativeTuning.GetAutoWallpaperIntervalMinutes();
        bool isRandom = NativeTuning.GetAutoWallpaperRandom();
        string mediaType = NativeTuning.GetAutoWallpaperMediaType();

        var card = new Border
        {
            Background = CardBackground(),
            BorderBrush = new SolidColorBrush(isAutoEnabled ? Color.FromRgb(37, 99, 235) : Color.FromRgb(24, 38, 56)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 14, 16, 14),
            Margin = new Thickness(0, 0, 0, 14)
        };

        var mainStack = new StackPanel();

        // Header Row: Icon + Title/Desc + Status Badge + Toggle Checkbox
        var topGrid = new Grid { Margin = new Thickness(0, 0, 0, isAutoEnabled ? 10 : 0) };
        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Icon Box
        var icoBox = new Border
        {
            Width = 32,
            Height = 32,
            CornerRadius = new CornerRadius(6),
            Background = isAutoEnabled
                ? new SolidColorBrush(Color.FromArgb(40, 56, 189, 248))
                : new SolidColorBrush(Color.FromArgb(20, 148, 163, 184)),
            Child = CreateVectorIcon(NexIcon.Refresh, isAutoEnabled ? CyanBrush : MutedBrush, 15),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(icoBox, 0);
        topGrid.Children.Add(icoBox);

        // Titles
        var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 10, 0) };
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
        titleRow.Children.Add(new TextBlock
        {
            Text = NexLocale.T("cust_auto_wp_title"),
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = TextBrush
        });

        string intervalText = intervalMins < 60 ? $"{intervalMins}m" : $"{intervalMins / 60}h";
        string statusText = isAutoEnabled
            ? NexLocale.Format("cust_auto_wp_status_on", intervalText)
            : NexLocale.T("cust_auto_wp_status_off");

        var statusBadge = new Border
        {
            Background = isAutoEnabled ? new SolidColorBrush(Color.FromArgb(30, 16, 185, 129)) : new SolidColorBrush(Color.FromArgb(30, 148, 163, 184)),
            BorderBrush = isAutoEnabled ? new SolidColorBrush(Color.FromArgb(70, 16, 185, 129)) : new SolidColorBrush(Color.FromArgb(50, 148, 163, 184)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 1.5, 6, 1.5),
            Margin = new Thickness(8, 0, 0, 0),
            Child = new TextBlock
            {
                Text = statusText,
                FontSize = 9.5,
                FontWeight = FontWeights.Bold,
                Foreground = isAutoEnabled ? GreenBrush : MutedBrush
            }
        };
        titleRow.Children.Add(statusBadge);
        titleStack.Children.Add(titleRow);

        titleStack.Children.Add(new TextBlock
        {
            Text = NexLocale.T("cust_auto_wp_desc"),
            FontSize = 11,
            Foreground = MutedBrush,
            Margin = new Thickness(0, 2, 0, 0)
        });
        Grid.SetColumn(titleStack, 1);
        topGrid.Children.Add(titleStack);

        // Toggle Switch Checkbox
        var toggleCb = new CheckBox
        {
            IsChecked = isAutoEnabled,
            Content = new TextBlock
            {
                Text = NexLocale.T("cust_auto_wp_enable"),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = isAutoEnabled ? CyanBrush : TextBrush,
                Margin = new Thickness(6, 0, 0, 0)
            },
            VerticalAlignment = VerticalAlignment.Center
        };
        toggleCb.Checked += (_, _) =>
        {
            NativeTuning.SetAutoWallpaperEnabled(true);
            InitAutoWallpaperTimer();
            ShowToast(NexLocale.T("cust_auto_wp_title"), NexLocale.T("status_enabled"), NexIcon.Check, GreenBrush);
            ShowCustomizer();
        };
        toggleCb.Unchecked += (_, _) =>
        {
            NativeTuning.SetAutoWallpaperEnabled(false);
            InitAutoWallpaperTimer();
            ShowToast(NexLocale.T("cust_auto_wp_title"), NexLocale.T("status_disabled"), NexIcon.Info, AmberBrush);
            ShowCustomizer();
        };
        Grid.SetColumn(toggleCb, 2);
        topGrid.Children.Add(toggleCb);
        mainStack.Children.Add(topGrid);

        // Controls Row: Media Selector + Interval Pills + Order Selector + "Schimbă acum" button
        if (isAutoEnabled)
        {
            var mediaRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 8), VerticalAlignment = VerticalAlignment.Center };
            mediaRow.Children.Add(new TextBlock
            {
                Text = NexLocale.T("cust_auto_wp_media_type"),
                FontSize = 11,
                Foreground = MutedBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            });

            var mediaOptions = new (string TypeKey, string Label)[]
            {
                ("all", NexLocale.T("cust_auto_wp_media_all")),
                ("video", NexLocale.T("cust_auto_wp_media_video")),
                ("static", NexLocale.T("cust_auto_wp_media_static"))
            };

            foreach (var (mType, mLbl) in mediaOptions)
            {
                bool isSel = mediaType == mType;
                var pill = new Border
                {
                    Background = isSel ? new SolidColorBrush(Color.FromRgb(15, 38, 74)) : new SolidColorBrush(Color.FromRgb(13, 20, 32)),
                    BorderBrush = isSel ? new SolidColorBrush(Color.FromRgb(37, 99, 235)) : new SolidColorBrush(Color.FromRgb(24, 38, 56)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(5),
                    Padding = new Thickness(9, 4, 9, 4),
                    Margin = new Thickness(0, 0, 6, 0),
                    Cursor = Cursors.Hand,
                    Child = new TextBlock
                    {
                        Text = mLbl,
                        FontSize = 10.5,
                        FontWeight = isSel ? FontWeights.Bold : FontWeights.Normal,
                        Foreground = isSel ? CyanBrush : TextBrush
                    }
                };
                string capturedType = mType;
                pill.MouseLeftButtonUp += (_, _) =>
                {
                    NativeTuning.SetAutoWallpaperMediaType(capturedType);
                    InitAutoWallpaperTimer();
                    ShowCustomizer();
                };
                mediaRow.Children.Add(pill);
            }
            mainStack.Children.Add(mediaRow);

            var controlsRow = new Grid { Margin = new Thickness(0, 2, 0, 0) };
            controlsRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            controlsRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var optStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            // Label interval
            optStack.Children.Add(new TextBlock
            {
                Text = NexLocale.T("cust_auto_wp_interval"),
                FontSize = 11,
                Foreground = MutedBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            });

            // Interval options: 5m, 15m, 30m, 1h, 3h, 24h
            var intervals = new (int Minutes, string Label)[]
            {
                (5, NexLocale.T("cust_auto_wp_5m")),
                (15, NexLocale.T("cust_auto_wp_15m")),
                (30, NexLocale.T("cust_auto_wp_30m")),
                (60, NexLocale.T("cust_auto_wp_1h")),
                (180, NexLocale.T("cust_auto_wp_3h")),
                (1440, NexLocale.T("cust_auto_wp_24h"))
            };

            foreach (var (mins, lbl) in intervals)
            {
                bool isSel = intervalMins == mins;
                var pill = new Border
                {
                    Background = isSel ? new SolidColorBrush(Color.FromRgb(15, 38, 74)) : new SolidColorBrush(Color.FromRgb(13, 20, 32)),
                    BorderBrush = isSel ? new SolidColorBrush(Color.FromRgb(37, 99, 235)) : new SolidColorBrush(Color.FromRgb(24, 38, 56)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(5),
                    Padding = new Thickness(9, 4, 9, 4),
                    Margin = new Thickness(0, 0, 6, 0),
                    Cursor = Cursors.Hand,
                    Child = new TextBlock
                    {
                        Text = lbl,
                        FontSize = 10.5,
                        FontWeight = isSel ? FontWeights.Bold : FontWeights.Normal,
                        Foreground = isSel ? CyanBrush : TextBrush
                    }
                };
                int capturedMins = mins;
                pill.MouseLeftButtonUp += (_, _) =>
                {
                    NativeTuning.SetAutoWallpaperIntervalMinutes(capturedMins);
                    InitAutoWallpaperTimer();
                    ShowCustomizer();
                };
                optStack.Children.Add(pill);
            }

            // Divider
            optStack.Children.Add(new Border
            {
                Width = 1,
                Height = 16,
                Background = new SolidColorBrush(Color.FromRgb(24, 38, 56)),
                Margin = new Thickness(6, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            });

            // Order Label
            optStack.Children.Add(new TextBlock
            {
                Text = NexLocale.T("cust_auto_wp_order"),
                FontSize = 11,
                Foreground = MutedBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            });

            // Order options: Sequential vs Shuffle
            var orders = new (bool Random, string Label)[]
            {
                (false, NexLocale.T("cust_auto_wp_seq")),
                (true, NexLocale.T("cust_auto_wp_random"))
            };
            foreach (var (rnd, lbl) in orders)
            {
                bool isSel = isRandom == rnd;
                var pill = new Border
                {
                    Background = isSel ? new SolidColorBrush(Color.FromRgb(15, 38, 74)) : new SolidColorBrush(Color.FromRgb(13, 20, 32)),
                    BorderBrush = isSel ? new SolidColorBrush(Color.FromRgb(37, 99, 235)) : new SolidColorBrush(Color.FromRgb(24, 38, 56)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(5),
                    Padding = new Thickness(9, 4, 9, 4),
                    Margin = new Thickness(0, 0, 6, 0),
                    Cursor = Cursors.Hand,
                    Child = new TextBlock
                    {
                        Text = lbl,
                        FontSize = 10.5,
                        FontWeight = isSel ? FontWeights.Bold : FontWeights.Normal,
                        Foreground = isSel ? CyanBrush : TextBrush
                    }
                };
                bool capturedRnd = rnd;
                pill.MouseLeftButtonUp += (_, _) =>
                {
                    NativeTuning.SetAutoWallpaperRandom(capturedRnd);
                    InitAutoWallpaperTimer();
                    ShowCustomizer();
                };
                optStack.Children.Add(pill);
            }

            Grid.SetColumn(optStack, 0);
            controlsRow.Children.Add(optStack);

            // "Schimbă acum" button
            var nextBtn = new Button
            {
                Style = (Style)FindResource("SecondaryButtonStyle"),
                Padding = new Thickness(12, 5, 12, 5),
                Cursor = Cursors.Hand
            };
            var nextSp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var nextIco = CreateVectorIcon(NexIcon.Revert, CyanBrush, 12);
            nextIco.Margin = new Thickness(0, 0, 5, 0);
            nextSp.Children.Add(nextIco);
            nextSp.Children.Add(new TextBlock
            {
                Text = NexLocale.T("cust_auto_wp_btn_next"),
                FontSize = 11,
                Foreground = TextBrush,
                VerticalAlignment = VerticalAlignment.Center
            });
            nextBtn.Content = nextSp;
            nextBtn.Click += (_, _) =>
            {
                bool changed = ApplyNextAutoWallpaper(notify: true);
                if (!changed)
                {
                    ShowToast(NexLocale.T("common_error"), NexLocale.T("cust_auto_wp_empty_hint"), NexIcon.Warning, AmberBrush);
                }
            };
            Grid.SetColumn(nextBtn, 1);
            controlsRow.Children.Add(nextBtn);

            mainStack.Children.Add(controlsRow);
        }

        if (totalLocalMedia == 0)
        {
            var hintText = new TextBlock
            {
                Text = NexLocale.T("cust_auto_wp_empty_hint"),
                FontSize = 11,
                Foreground = AmberBrush,
                Margin = new Thickness(0, 8, 0, 0)
            };
            mainStack.Children.Add(hintText);
        }

        card.Child = mainStack;
        return card;
    }
}