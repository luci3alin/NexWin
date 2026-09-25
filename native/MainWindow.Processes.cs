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
    private void ShowStartup()
    {
        PreparePage(NexLocale.T("startup_title"), NexLocale.T("startup_subtitle"));

        var startupApps = NativeTuning.GetStartupAppsNative();

        // Summary & Add App Toolbar
        var summaryBorder = new Border
        {
            Background = CardBackground(),
            BorderBrush = new SolidColorBrush(Color.FromRgb(24, 36, 52)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(16, 12, 16, 12),
            Margin = new Thickness(0, 0, 0, 16)
        };

        var sumGrid = new Grid();
        sumGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        sumGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var activeCount = startupApps.Count(a => a.Enabled);
        var summaryText = new TextBlock
        {
            Text = NexLocale.Format("startup_summary_format", startupApps.Count, activeCount, startupApps.Count - activeCount),
            FontSize = 12,
            Foreground = MutedBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(summaryText, 0);
        sumGrid.Children.Add(summaryText);

        var addStartupBtn = new Button
        {
            Content = NexLocale.T("startup_btn_add"),
            Style = (Style)FindResource("SecondaryButtonStyle"),
            Padding = new Thickness(14, 6, 14, 6),
            Cursor = Cursors.Hand
        };
        addStartupBtn.Click += (_, _) =>
        {
            var dlg = new OpenFileDialog
            {
                Filter = NexLocale.T("startup_filter_exe"),
                Title = NexLocale.T("startup_dlg_title")
            };
            if (dlg.ShowDialog() == true)
            {
                var name = System.IO.Path.GetFileNameWithoutExtension(dlg.FileName);
                bool ok = NativeTuning.AddStartupAppNative(name, dlg.FileName);
                if (ok)
                {
                    AppendLog(NexLocale.Format("proc_startup_log_added_format", name, dlg.FileName), false);
                    ShowToast(NexLocale.T("nav_startup"), NexLocale.Format("startup_toast_added", name), NexIcon.Check, GreenBrush);
                    ShowStartup();
                }
                else
                {
                    ShowToast(NexLocale.T("startup_toast_err_title"), NexLocale.T("startup_toast_err_msg"), NexIcon.Warning, RedBrush);
                }
            }
        };
        Grid.SetColumn(addStartupBtn, 1);
        sumGrid.Children.Add(addStartupBtn);

        summaryBorder.Child = sumGrid;
        PageRoot.Children.Insert(1, summaryBorder);

        if (startupApps.Count == 0)
        {
            AddActionRow(
                NexLocale.T("startup_none_detected"),
                NexLocale.T("startup_none_desc"),
                null, NexIcon.Rocket, NexLocale.T("status_clean"), GreenBrush, CyanBrush, NexLocale.T("startup_none_badge")
            );
            return;
        }

        foreach (var item in startupApps)
        {
            var cmdSnippet = item.Command.Length > 75 ? item.Command.Substring(0, 72) + "..." : item.Command;
            AddActionRow(
                item.Name,
                string.IsNullOrWhiteSpace(cmdSnippet) ? NexLocale.T("startup_cmd_default") : cmdSnippet,
                item.Icon,
                NexIcon.Rocket,
                item.Enabled ? NexLocale.T("status_active") : NexLocale.T("status_disabled"),
                item.Enabled ? GreenBrush : AmberBrush,
                CyanBrush,
                NexLocale.Format("startup_item_desc_format", item.Hive, item.Impact),
                item.Enabled
                    ? ActionBtn(NexLocale.T("startup_btn_disable"), NexIcon.Power, RedBrush, () => {
                        NativeTuning.ToggleStartupAppNative(item.Name, item.Hive, false);
                        AppendLog(NexLocale.Format("proc_startup_log_disabled_format", item.Name, item.Hive), false);
                        ShowToast(NexLocale.T("nav_startup"), NexLocale.Format("startup_toast_disabled", item.Name), NexIcon.Check, GreenBrush);
                        ShowStartup();
                        return Task.CompletedTask;
                    })
                    : ActionBtn(NexLocale.T("startup_btn_enable"), NexIcon.Power, GreenBrush, () => {
                        NativeTuning.ToggleStartupAppNative(item.Name, item.Hive, true);
                        AppendLog(NexLocale.Format("proc_startup_log_enabled_format", item.Name, item.Hive), false);
                        ShowToast(NexLocale.T("nav_startup"), NexLocale.Format("startup_toast_enabled", item.Name), NexIcon.Check, GreenBrush);
                        ShowStartup();
                        return Task.CompletedTask;
                    })
            );
        }
    }


    private string activeProcessCategory = "Toate procesele";
    private string processSearchQuery = "";
    private readonly HashSet<int> selectedProcessPids = new();

    private void ShowProcesses()
    {
        PageRoot.Children.Clear();

        // 1. Top Header (Matching media_1789749600039.png)
        var headGrid = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        headGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var leftHead = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var pulseBox = new Border
        {
            Width = 44,
            Height = 44,
            CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(Color.FromArgb(40, 56, 189, 248)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(80, 56, 189, 248)),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 14, 0),
            Child = CreateVectorIcon(NexIcon.Pulse, CyanBrush, 22)
        };
        leftHead.Children.Add(pulseBox);

        var titleTextStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        titleTextStack.Children.Add(new TextBlock
        {
            Text = NexLocale.T("processes_title"),
            FontSize = 19,
            FontWeight = FontWeights.Bold,
            Foreground = TextBrush
        });
        titleTextStack.Children.Add(new TextBlock
        {
            Text = NexLocale.T("processes_subtitle"),
            FontSize = 11.5,
            Foreground = MutedBrush,
            Margin = new Thickness(0, 2, 0, 0)
        });
        leftHead.Children.Add(titleTextStack);
        Grid.SetColumn(leftHead, 0);
        headGrid.Children.Add(leftHead);

        // Right side of Header: Search Box + 3 switch buttons ([::], [=], [Y])
        var rightHead = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        var searchBox = new Border
        {
            Background = CardBackground(),
            BorderBrush = new SolidColorBrush(Color.FromRgb(32, 44, 60)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(10, 5, 10, 5),
            Margin = new Thickness(0, 0, 8, 0),
            Width = 220
        };
        var sGrid = new Grid();
        sGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
        sGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var sIcon = CreateVectorIcon(NexIcon.Search, MutedBrush, 12);
        sIcon.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(sIcon, 0);
        sGrid.Children.Add(sIcon);

        Action? UpdateProcessTable = null;

        var sBox = new TextBox
        {
            Text = processSearchQuery,
            Background = Brushes.Transparent,
            Foreground = TextBrush,
            BorderThickness = new Thickness(0),
            FontSize = 11.5,
            VerticalAlignment = VerticalAlignment.Center
        };
        var placeholder = new TextBlock
        {
            Text = NexLocale.T("proc_search_placeholder"),
            Foreground = new SolidColorBrush(Color.FromRgb(90, 105, 125)),
            FontSize = 11.5,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
            Visibility = string.IsNullOrEmpty(processSearchQuery) ? Visibility.Visible : Visibility.Collapsed
        };
        sBox.TextChanged += (s, e) =>
        {
            processSearchQuery = sBox.Text;
            placeholder.Visibility = string.IsNullOrEmpty(sBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            UpdateProcessTable?.Invoke();
        };
        Grid.SetColumn(placeholder, 1);
        Grid.SetColumn(sBox, 1);
        sGrid.Children.Add(placeholder);
        sGrid.Children.Add(sBox);
        searchBox.Child = sGrid;
        rightHead.Children.Add(searchBox);

        Button MakeHeadIconBtn(NexIcon ico)
        {
            var b = new Button
            {
                Width = 32,
                Height = 32,
                Style = (Style)FindResource("SecondaryButtonStyle"),
                Background = CardBackground(),
                BorderBrush = new SolidColorBrush(Color.FromRgb(32, 44, 60)),
                Margin = new Thickness(0, 0, 6, 0),
                Padding = new Thickness(0),
                Cursor = Cursors.Hand,
                Content = CreateVectorIcon(ico, MutedBrush, 13)
            };
            return b;
        }

        rightHead.Children.Add(MakeHeadIconBtn(NexIcon.Window));
        rightHead.Children.Add(MakeHeadIconBtn(NexIcon.Layers));
        rightHead.Children.Add(MakeHeadIconBtn(NexIcon.Filter));

        Grid.SetColumn(rightHead, 1);
        headGrid.Children.Add(rightHead);
        PageRoot.Children.Add(headGrid);

        // 2. Toolbar Row: "Actualizează procese" (blue) & "Oprește selectat" (red)
        var actionToolRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14) };

        var refreshBtn = new Button
        {
            Style = (Style)FindResource("BlueGradientButtonStyle"),
            Padding = new Thickness(16, 7, 16, 7),
            Margin = new Thickness(0, 0, 10, 0),
            Cursor = Cursors.Hand
        };
        var rStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var rIco = CreateVectorIcon(NexIcon.Refresh, Brushes.White, 14);
        rIco.Margin = new Thickness(0, 0, 8, 0);
        rStack.Children.Add(rIco);
        rStack.Children.Add(new TextBlock { Text = NexLocale.T("proc_btn_refresh"), Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, FontSize = 12 });
        refreshBtn.Content = rStack;
        refreshBtn.Click += (_, _) => UpdateProcessTable?.Invoke();
        actionToolRow.Children.Add(refreshBtn);

        var stopBtn = new Button
        {
            Style = (Style)FindResource("DarkDangerOutlineButtonStyle"),
            Padding = new Thickness(16, 7, 16, 7),
            Cursor = Cursors.Hand
        };
        var stopStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var pwrIco = CreateVectorIcon(NexIcon.Power, RedBrush, 13);
        pwrIco.Margin = new Thickness(0, 0, 8, 0);
        stopStack.Children.Add(pwrIco);
        stopStack.Children.Add(new TextBlock { Text = NexLocale.T("proc_btn_stop_selected"), Foreground = RedBrush, FontWeight = FontWeights.SemiBold, FontSize = 12 });
        stopBtn.Content = stopStack;
        stopBtn.Click += async (_, _) =>
        {
            if (selectedProcessPids.Count > 0)
            {
                var conf = await ShowConfirmModalAsync(NexLocale.T("proc_modal_stop_title"), NexLocale.Format("proc_modal_stop_msg", selectedProcessPids.Count), RedBrush, NexLocale.T("proc_btn_stop_action"), NexLocale.T("btn_cancel"));
                if (conf)
                {
                    int stopped = 0;
                    foreach (var pid in selectedProcessPids.ToList())
                    {
                        try { Process.GetProcessById(pid).Kill(); stopped++; } catch { }
                    }
                    selectedProcessPids.Clear();
                    ShowToast(NexLocale.T("proc_toast_stopped_title"), NexLocale.Format("proc_toast_stopped_msg", stopped), NexIcon.Check, GreenBrush);
                    UpdateProcessTable?.Invoke();
                }
            }
            else
            {
                ShowToast(NexLocale.T("proc_toast_select_proc_title"), NexLocale.T("proc_toast_select_proc_msg"), NexIcon.Info, AmberBrush);
            }
        };
        actionToolRow.Children.Add(stopBtn);
        PageRoot.Children.Add(actionToolRow);

        // 3. RAM & System Donut Card
        var memStatus = new MEMORYSTATUSEX();
        GlobalMemoryStatusEx(memStatus);
        double totalRamGB = memStatus.ullTotalPhys / 1024d / 1024d / 1024d;
        double freeRamGB = memStatus.ullAvailPhys / 1024d / 1024d / 1024d;
        double usedRamGB = totalRamGB - freeRamGB;
        double ramPct = totalRamGB > 0 ? (usedRamGB / totalRamGB) * 100 : 33;

        var ramCard = new Border
        {
            Background = CardBackground(),
            BorderBrush = new SolidColorBrush(Color.FromRgb(24, 36, 52)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(18, 14, 18, 14),
            Margin = new Thickness(0, 0, 0, 14)
        };

        var ramCardGrid = new Grid();
        ramCardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ramCardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Left of RAM card: Donut + Stats
        var leftRamStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        leftRamStack.Children.Add(CreateDonutChart(ramPct, 72, CyanBrush));

        var ramStatsText = new StackPanel { Margin = new Thickness(16, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        ramStatsText.Children.Add(new TextBlock { Text = NexLocale.T("disk_used_label"), FontSize = 11, Foreground = MutedBrush });
        ramStatsText.Children.Add(new TextBlock { Text = $"{usedRamGB:0.0} GB", FontSize = 15, FontWeight = FontWeights.Bold, Foreground = TextBrush });
        ramStatsText.Children.Add(new TextBlock { Text = NexLocale.T("disk_free_label"), FontSize = 11, Foreground = MutedBrush, Margin = new Thickness(0, 4, 0, 0) });
        ramStatsText.Children.Add(new TextBlock { Text = $"{freeRamGB:0.0} GB", FontSize = 15, FontWeight = FontWeights.Bold, Foreground = TextBrush });
        leftRamStack.Children.Add(ramStatsText);
        Grid.SetColumn(leftRamStack, 0);
        ramCardGrid.Children.Add(leftRamStack);

        // Right of RAM card: Dedicated RAM optimization button & Refresh (No disk buttons)
        var rightRamBtns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        var cleanRamBtn = new Button
        {
            Style = (Style)FindResource("BlueGradientButtonStyle"),
            Padding = new Thickness(16, 8, 16, 8),
            Margin = new Thickness(0, 0, 10, 0),
            Cursor = Cursors.Hand
        };
        var crStack = new StackPanel { Orientation = Orientation.Horizontal };
        var crIco = CreateVectorIcon(NexIcon.Clean, Brushes.White, 13);
        crIco.Margin = new Thickness(0, 0, 8, 0);
        crStack.Children.Add(crIco);
        crStack.Children.Add(new TextBlock { Text = NexLocale.T("proc_btn_trim_all"), Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, FontSize = 12 });
        cleanRamBtn.Content = crStack;
        cleanRamBtn.Click += async (_, _) =>
        {
            long freed = NativeTuning.TrimAllWorkingSets();
            ShowToast(NexLocale.T("proc_toast_ram_trimmed_title"), NexLocale.Format("proc_toast_ram_trimmed_msg", freed), NexIcon.Check, GreenBrush);
            ShowProcesses();
        };
        rightRamBtns.Children.Add(cleanRamBtn);

        var refreshListBtn = new Button
        {
            Style = (Style)FindResource("SecondaryButtonStyle"),
            Padding = new Thickness(14, 8, 14, 8),
            Cursor = Cursors.Hand
        };
        var rlStack = new StackPanel { Orientation = Orientation.Horizontal };
        var rlIco = CreateVectorIcon(NexIcon.Refresh, MutedBrush, 13);
        rlIco.Margin = new Thickness(0, 0, 8, 0);
        rlStack.Children.Add(rlIco);
        rlStack.Children.Add(new TextBlock { Text = NexLocale.T("proc_btn_refresh"), Foreground = TextBrush, FontWeight = FontWeights.SemiBold, FontSize = 12 });
        refreshListBtn.Content = rlStack;
        refreshListBtn.Click += (_, _) => UpdateProcessTable?.Invoke();
        rightRamBtns.Children.Add(refreshListBtn);

        Grid.SetColumn(rightRamBtns, 1);
        ramCardGrid.Children.Add(rightRamBtns);
        ramCard.Child = ramCardGrid;
        PageRoot.Children.Add(ramCard);

        // 4. Category Tabs
        var pCats = new (string id, string localeKey, NexIcon icon)[]
        {
            ("Toate procesele", "proc_tab_all", NexIcon.Pulse),
            ("Aplicații", "proc_tab_apps", NexIcon.Window),
            ("Servicii", "proc_tab_services", NexIcon.Sliders),
            ("Background", "proc_tab_background", NexIcon.Layers),
            ("Sisteme", "proc_tab_system", NexIcon.Cpu),
            ("Duplicate", "proc_tab_duplicates", NexIcon.Layers)
        };

        var tabsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        foreach (var (cId, cKey, cIco) in pCats)
        {
            bool isCur = cId.Equals(activeProcessCategory, StringComparison.OrdinalIgnoreCase);
            var tabBorder = new Border
            {
                Tag = cId,
                Background = isCur ? new SolidColorBrush(Color.FromRgb(11, 27, 54)) : Brushes.Transparent,
                BorderBrush = isCur ? new SolidColorBrush(Color.FromRgb(37, 99, 235)) : Brushes.Transparent,
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = Cursors.Hand
            };
            var tStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var tabIcon = CreateVectorIcon(cIco, isCur ? CyanBrush : MutedBrush, 13);
            tabIcon.Margin = new Thickness(0, 0, 6, 0);
            tStack.Children.Add(tabIcon);
            tStack.Children.Add(new TextBlock
            {
                Text = NexLocale.T(cKey),
                FontSize = 11.5,
                FontWeight = isCur ? FontWeights.Bold : FontWeights.Normal,
                Foreground = isCur ? CyanBrush : MutedBrush
            });
            tabBorder.Child = tStack;
            tabBorder.MouseLeftButtonUp += (_, _) =>
            {
                activeProcessCategory = cId;
                foreach (Border b in tabsPanel.Children)
                {
                    if (b.Tag is string tg)
                    {
                        bool isSel = tg.Equals(activeProcessCategory, StringComparison.OrdinalIgnoreCase);
                        b.Background = isSel ? new SolidColorBrush(Color.FromRgb(11, 27, 54)) : Brushes.Transparent;
                        b.BorderBrush = isSel ? new SolidColorBrush(Color.FromRgb(37, 99, 235)) : Brushes.Transparent;
                        if (b.Child is StackPanel sp && sp.Children.Count > 1 && sp.Children[1] is TextBlock tb)
                        {
                            tb.Foreground = isSel ? CyanBrush : MutedBrush;
                            tb.FontWeight = isSel ? FontWeights.Bold : FontWeights.Normal;
                        }
                    }
                }
                UpdateProcessTable?.Invoke();
            };
            tabsPanel.Children.Add(tabBorder);
        }
        PageRoot.Children.Add(tabsPanel);

        // 5. Enclosed Table Card
        var tableCard = new Border
        {
            Background = CardBackground(),
            BorderBrush = new SolidColorBrush(Color.FromRgb(24, 36, 52)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 14, 16, 14),
            Margin = new Thickness(0, 0, 0, 14)
        };

        var mainStack = new StackPanel();

        // Table Header
        var tHead = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        tHead.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) }); // Checkbox
        tHead.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) }); // Proces
        tHead.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(85) }); // PID
        tHead.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(125) }); // Memorie (MB)
        tHead.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(85) }); // CPU (%)
        tHead.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(115) }); // Prioritate
        tHead.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) }); // Tip
        tHead.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) }); // Data pornirii
        tHead.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) }); // Action / Filter

        var chkAll = new CheckBox { VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(chkAll, 0); tHead.Children.Add(chkAll);

        void AddColHead(string txt, int col)
        {
            var tb = new TextBlock { Text = txt, Foreground = MutedBrush, FontSize = 11, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(tb, col);
            tHead.Children.Add(tb);
        }

        AddColHead(NexLocale.T("proc_col_process"), 1);
        AddColHead(NexLocale.T("proc_col_pid"), 2);
        AddColHead(NexLocale.T("proc_col_memory"), 3);
        AddColHead(NexLocale.T("proc_col_cpu"), 4);
        AddColHead(NexLocale.T("proc_col_priority"), 5);
        AddColHead(NexLocale.T("proc_col_type"), 6);
        AddColHead(NexLocale.T("proc_col_start_time"), 7);

        var filterIconBtn = CreateVectorIcon(NexIcon.Filter, MutedBrush, 13);
        filterIconBtn.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(filterIconBtn, 8);
        tHead.Children.Add(filterIconBtn);

        mainStack.Children.Add(tHead);

        // Table Rows Container
        var rowsStack = new StackPanel();
        var rowsScroll = new ScrollViewer
        {
            MaxHeight = 280,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = rowsStack
        };
        mainStack.Children.Add(rowsScroll);

        // Table Card Footer
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
            Text = NexLocale.T("proc_selected_empty"),
            FontSize = 12,
            Foreground = MutedBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(leftFooter, 0);
        footerGrid.Children.Add(leftFooter);

        var closeSelectedBtn = new Button
        {
            Style = (Style)FindResource("DarkTableFooterButtonStyle"),
            Padding = new Thickness(14, 6, 14, 6),
            Cursor = Cursors.Hand
        };
        var csStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var trIco = CreateVectorIcon(NexIcon.Trash, MutedBrush, 13);
        trIco.Margin = new Thickness(0, 0, 6, 0);
        csStack.Children.Add(trIco);
        csStack.Children.Add(new TextBlock { Text = NexLocale.T("proc_btn_close_selected"), Foreground = MutedBrush, FontWeight = FontWeights.SemiBold, FontSize = 11.5 });
        closeSelectedBtn.Content = csStack;
        closeSelectedBtn.Click += async (_, _) =>
        {
            if (selectedProcessPids.Count > 0)
            {
                var conf = await ShowConfirmModalAsync(NexLocale.T("proc_modal_close_title"), NexLocale.Format("proc_modal_close_msg", selectedProcessPids.Count), RedBrush, NexLocale.T("btn_close"), NexLocale.T("btn_cancel"));
                if (conf)
                {
                    int killed = 0;
                    foreach (var pid in selectedProcessPids.ToList())
                    {
                        try { Process.GetProcessById(pid).Kill(); killed++; } catch { }
                    }
                    selectedProcessPids.Clear();
                    ShowToast(NexLocale.T("proc_toast_closed_title"), NexLocale.Format("proc_toast_closed_msg", killed), NexIcon.Check, GreenBrush);
                    UpdateProcessTable?.Invoke();
                }
            }
            else
            {
                ShowToast(NexLocale.T("proc_toast_select_close_title"), NexLocale.T("proc_toast_select_close_msg"), NexIcon.Info, AmberBrush);
            }
        };
        Grid.SetColumn(closeSelectedBtn, 1);
        footerGrid.Children.Add(closeSelectedBtn);
        footerBorder.Child = footerGrid;
        mainStack.Children.Add(footerBorder);

        tableCard.Child = mainStack;
        PageRoot.Children.Add(tableCard);

        // Core Update Logic for Rows
        UpdateProcessTable = () =>
        {
            rowsStack.Children.Clear();
            var currentProcList = NativeTuning.GetProcessInspectorList(activeProcessCategory, processSearchQuery);

            chkAll.IsChecked = currentProcList.Count > 0 && currentProcList.All(p => selectedProcessPids.Contains(p.Pid));

            double totalSelectedMB = 0;
            foreach (var p in currentProcList.Take(150))
            {
                bool isChecked = selectedProcessPids.Contains(p.Pid);
                if (isChecked) totalSelectedMB += p.MemoryMB;

                var rowBorder = new Border
                {
                    BorderBrush = new SolidColorBrush(Color.FromRgb(18, 28, 40)),
                    BorderThickness = new Thickness(0, 1, 0, 0),
                    Background = isChecked ? new SolidColorBrush(Color.FromArgb(35, 56, 189, 248)) : Brushes.Transparent,
                    Padding = new Thickness(0, 7, 0, 7)
                };

                var rGrid = new Grid();
                rGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
                rGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
                rGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(85) });
                rGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(125) });
                rGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(85) });
                rGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(115) });
                rGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
                rGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
                rGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });

                // Row Checkbox
                var curPid = p.Pid;
                var curProc = p;
                var curBorder = rowBorder;

                var rowChk = new CheckBox { IsChecked = isChecked, VerticalAlignment = VerticalAlignment.Center };
                rowChk.Click += (_, _) =>
                {
                    if (rowChk.IsChecked == true)
                    {
                        selectedProcessPids.Add(curPid);
                        curBorder.Background = new SolidColorBrush(Color.FromArgb(35, 56, 189, 248));
                    }
                    else
                    {
                        selectedProcessPids.Remove(curPid);
                        curBorder.Background = Brushes.Transparent;
                    }
                    chkAll.IsChecked = currentProcList.Count > 0 && currentProcList.All(pr => selectedProcessPids.Contains(pr.Pid));
                    double selMB = currentProcList.Where(pr => selectedProcessPids.Contains(pr.Pid)).Sum(pr => pr.MemoryMB);
                    leftFooter.Text = selectedProcessPids.Count > 0
                        ? NexLocale.Format("proc_selected_format", selectedProcessPids.Count, selMB)
                        : NexLocale.T("proc_selected_empty");
                };
                Grid.SetColumn(rowChk, 0); rGrid.Children.Add(rowChk);

                // Process Icon + Name
                var pInfoStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                if (p.Icon != null)
                {
                    var img = new Image { Source = p.Icon, Width = 18, Height = 18, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
                    RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                    pInfoStack.Children.Add(img);
                }
                else
                {
                    var fIco = CreateVectorIcon(NexIcon.Window, CyanBrush, 14);
                    fIco.Margin = new Thickness(0, 0, 8, 0);
                    pInfoStack.Children.Add(fIco);
                }
                pInfoStack.Children.Add(new TextBlock { Text = p.Name, FontSize = 12.5, FontWeight = FontWeights.SemiBold, Foreground = TextBrush, VerticalAlignment = VerticalAlignment.Center });
                Grid.SetColumn(pInfoStack, 1); rGrid.Children.Add(pInfoStack);

                // PID
                var pidText = new TextBlock { Text = p.Pid.ToString(), FontSize = 11.5, Foreground = MutedBrush, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(pidText, 2); rGrid.Children.Add(pidText);

                // Memory (MB)
                var memText = new TextBlock { Text = $"{p.MemoryMB:0.0}", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = TextBrush, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(memText, 3); rGrid.Children.Add(memText);

                // CPU (%)
                var cpuText = new TextBlock { Text = $"{p.CpuPercent:0.0}%", FontSize = 11.5, Foreground = MutedBrush, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(cpuText, 4); rGrid.Children.Add(cpuText);

                // Interactive Priority Pill Badge
                Color bgPill, brdPill, fgPill;
                if (p.Priority == "Ridicată")
                {
                    bgPill = Color.FromRgb(45, 12, 22);
                    brdPill = Color.FromRgb(153, 27, 27);
                    fgPill = Color.FromRgb(248, 113, 113);
                }
                else if (p.Priority == "Scăzută")
                {
                    bgPill = Color.FromRgb(5, 46, 35);
                    brdPill = Color.FromRgb(6, 95, 70);
                    fgPill = Color.FromRgb(52, 211, 153);
                }
                else if (p.Priority == "Peste Normal")
                {
                    bgPill = Color.FromRgb(53, 29, 12);
                    brdPill = Color.FromRgb(180, 83, 9);
                    fgPill = Color.FromRgb(251, 191, 36);
                }
                else
                {
                    bgPill = Color.FromRgb(8, 28, 61);
                    brdPill = Color.FromRgb(30, 64, 175);
                    fgPill = Color.FromRgb(96, 165, 250);
                }

                var prioPill = new Border
                {
                    Background = new SolidColorBrush(bgPill),
                    BorderBrush = new SolidColorBrush(brdPill),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(5),
                    Padding = new Thickness(8, 2.5, 8, 2.5),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center,
                    Cursor = Cursors.Hand,
                    ToolTip = NexLocale.T("proc_prio_tooltip")
                };
                prioPill.Child = new TextBlock
                {
                    Text = p.Priority switch { "Ridicată" => NexLocale.T("proc_prio_high_short"), "Scăzută" => NexLocale.T("proc_prio_low_short"), "Peste Normal" => NexLocale.T("proc_prio_above_short"), "Sub Normal" => NexLocale.T("proc_prio_below_short"), "Timp Real" => NexLocale.T("proc_prio_realtime_short"), _ => NexLocale.T("proc_prio_normal_short") },
                    FontSize = 10.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(fgPill)
                };

                // Click opens Custom Dark Context Menu matching NexWin Theme
                prioPill.MouseLeftButtonUp += (s, e) =>
                {
                    e.Handled = true;
                    var cm = new ContextMenu
                    {
                        Style = (Style)FindResource("DarkContextMenuStyle"),
                        PlacementTarget = prioPill,
                        Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom
                    };
                    void AddPrioMenu(string title, ProcessPriorityClass pc, Color dotColor)
                    {
                        var mi = new MenuItem
                        {
                            Header = title,
                            Style = (Style)FindResource("DarkMenuItemStyle"),
                            Icon = new Ellipse
                            {
                                Width = 8,
                                Height = 8,
                                Fill = new SolidColorBrush(dotColor),
                                VerticalAlignment = VerticalAlignment.Center,
                                HorizontalAlignment = HorizontalAlignment.Center
                            }
                        };
                        mi.Click += (_, _) =>
                        {
                            if (NativeTuning.SetProcessPriorityNative(curPid, pc))
                            {
                                ShowToast(NexLocale.T("proc_toast_prio_updated_title"), NexLocale.Format("proc_toast_prio_updated_msg", curProc.Name, title), NexIcon.Check, GreenBrush);
                                UpdateProcessTable?.Invoke();
                            }
                            else
                            {
                                ShowToast(NexLocale.T("proc_toast_access_denied_title"), NexLocale.Format("proc_toast_access_denied_msg", curProc.Name), NexIcon.Warning, RedBrush);
                            }
                        };
                        cm.Items.Add(mi);
                    }

                    AddPrioMenu(NexLocale.T("proc_prio_high"), ProcessPriorityClass.High, Color.FromRgb(239, 68, 68));
                    AddPrioMenu(NexLocale.T("proc_prio_above_normal"), ProcessPriorityClass.AboveNormal, Color.FromRgb(245, 158, 11));
                    AddPrioMenu(NexLocale.T("proc_prio_normal"), ProcessPriorityClass.Normal, Color.FromRgb(56, 189, 248));
                    AddPrioMenu(NexLocale.T("proc_prio_below_normal"), ProcessPriorityClass.BelowNormal, Color.FromRgb(148, 163, 184));
                    AddPrioMenu(NexLocale.T("proc_prio_low"), ProcessPriorityClass.Idle, Color.FromRgb(16, 185, 129));
                    AddPrioMenu(NexLocale.T("proc_prio_realtime"), ProcessPriorityClass.RealTime, Color.FromRgb(168, 85, 247));

                    cm.IsOpen = true;
                };
                Grid.SetColumn(prioPill, 5); rGrid.Children.Add(prioPill);

                // Tip
                var catText = new TextBlock { Text = p.Category, FontSize = 11.5, Foreground = MutedBrush, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(catText, 6); rGrid.Children.Add(catText);

                // Data pornirii
                var dateText = new TextBlock { Text = p.StartTime, FontSize = 11, Foreground = MutedBrush, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(dateText, 7); rGrid.Children.Add(dateText);

                // 3-dots Context Menu (Custom Dark Style)
                var dotsBtn = new Border
                {
                    Width = 28,
                    Height = 24,
                    Background = Brushes.Transparent,
                    Cursor = Cursors.Hand,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock { Text = "•••", FontSize = 12, Foreground = MutedBrush, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                };
                dotsBtn.MouseLeftButtonUp += (s, e) =>
                {
                    e.Handled = true;
                    var cm = new ContextMenu
                    {
                        Style = (Style)FindResource("DarkContextMenuStyle"),
                        PlacementTarget = dotsBtn,
                        Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom
                    };

                    var miTrim = new MenuItem
                    {
                        Header = NexLocale.T("proc_ctx_trim_ram"),
                        Style = (Style)FindResource("DarkMenuItemStyle"),
                        Icon = CreateVectorIcon(NexIcon.Clean, CyanBrush, 12)
                    };
                    miTrim.Click += (_, _) =>
                    {
                        NativeTuning.TrimProcessMemory(curPid);
                        ShowToast(NexLocale.T("proc_toast_ram_freed_title"), NexLocale.Format("proc_toast_ram_freed_msg", curProc.Name), NexIcon.Check, GreenBrush);
                        UpdateProcessTable?.Invoke();
                    };
                    cm.Items.Add(miTrim);

                    var miKill = new MenuItem
                    {
                        Header = NexLocale.T("proc_ctx_kill"),
                        Style = (Style)FindResource("DarkMenuItemStyle"),
                        Icon = CreateVectorIcon(NexIcon.Power, RedBrush, 12)
                    };
                    miKill.Click += async (_, _) =>
                    {
                        var conf = await ShowConfirmModalAsync(NexLocale.T("proc_modal_kill_title"), NexLocale.Format("proc_modal_kill_msg", curProc.Name, curPid), RedBrush, NexLocale.T("proc_btn_kill_action"), NexLocale.T("btn_cancel"));
                        if (conf)
                        {
                            try { Process.GetProcessById(curPid).Kill(); UpdateProcessTable?.Invoke(); } catch { }
                        }
                    };
                    cm.Items.Add(miKill);

                    cm.IsOpen = true;
                };
                Grid.SetColumn(dotsBtn, 8); rGrid.Children.Add(dotsBtn);

                rowBorder.Child = rGrid;
                rowsStack.Children.Add(rowBorder);
            }

            leftFooter.Text = selectedProcessPids.Count > 0
                ? NexLocale.Format("proc_selected_format", selectedProcessPids.Count, totalSelectedMB)
                : NexLocale.T("proc_selected_empty");
        };

        // Check All click handler
        chkAll.Click += (_, _) =>
        {
            var currentProcList = NativeTuning.GetProcessInspectorList(activeProcessCategory, processSearchQuery);
            bool select = chkAll.IsChecked == true;
            if (select)
            {
                foreach (var pr in currentProcList) selectedProcessPids.Add(pr.Pid);
            }
            else
            {
                foreach (var pr in currentProcList) selectedProcessPids.Remove(pr.Pid);
            }
            UpdateProcessTable();
        };

        // Initial populate
        UpdateProcessTable();
    }

}