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
using System.Net.NetworkInformation;
using Microsoft.Win32;
using Path = System.IO.Path;

namespace NexWin.Native;

public partial class MainWindow : Window
{
    private List<NativeTuning.SoftwareAppItem>? currentCatalog;
    private string activeAppsTab = "Catalog"; // "Catalog", "Updates", "Installed"
    private List<NativeTuning.AppUpgradeDetail>? cachedUpgradesList;
    private bool isScanningUpgrades = false;

    private static string GetLocalizedCategory(string category) => category switch
    {
        "Navigatoare Web" => NexLocale.T("apps_cat_browsers"),
        "Gaming & Mesagerie" => NexLocale.T("apps_cat_gaming"),
        "Media & Sunet" => NexLocale.T("apps_cat_media"),
        "Utilitare & Sistem" => NexLocale.T("apps_cat_utilities"),
        "Dezvoltare & Programare" => NexLocale.T("apps_cat_development"),
        "Grafică & Birou" => NexLocale.T("apps_cat_graphics"),
        "Securitate & Parole" => NexLocale.T("apps_cat_security"),
        _ => category
    };

    private async void TriggerUpgradeScan(bool showStartToast = true)
    {
        if (isScanningUpgrades) return;
        isScanningUpgrades = true;
        if (showStartToast)
        {
            ShowToast(NexLocale.T("apps_toast_scan_title"), NexLocale.T("apps_toast_scan_msg"), NexIcon.Pulse, CyanBrush);
        }
        if (activeAppsTab == "Updates") ShowApps();

        try
        {
            cachedUpgradesList = await NativeTuning.CheckForAppUpgradesDetailedAsync();
        }
        catch
        {
            cachedUpgradesList = new List<NativeTuning.AppUpgradeDetail>();
        }
        finally
        {
            isScanningUpgrades = false;
        }

        UpdateTopNotificationsBadge(cachedUpgradesList?.Count ?? 0);

        if (activeAppsTab == "Updates")
        {
            ShowApps();
        }

        if (cachedUpgradesList != null && cachedUpgradesList.Count > 0)
        {
            ShowToastWithAction(
                NexLocale.T("apps_toast_updates_avail_title"),
                NexLocale.Format("apps_toast_updates_avail_msg", cachedUpgradesList.Count),
                NexIcon.Bell, AmberBrush, NexLocale.T("apps_toast_btn_view_updates"),
                () => ShowNotificationsModal());
        }
        else
        {
            ShowToast(NexLocale.T("apps_toast_up_to_date_title"), NexLocale.T("apps_toast_up_to_date_msg"), NexIcon.Check, GreenBrush);
        }
    }

    private async Task RunBatchUpgradeAsync(List<NativeTuning.AppUpgradeDetail> toUpgrade)
    {
        if (toUpgrade.Count == 0)
        {
            ShowToast(NexLocale.T("apps_toast_upgrade_title"), NexLocale.T("apps_toast_select_upgrade_msg"), NexIcon.Info, AmberBrush);
            return;
        }

        var confirmed = await ShowConfirmModalAsync(
            NexLocale.T("apps_confirm_upgrade_title"),
            NexLocale.Format("apps_confirm_upgrade_msg", toUpgrade.Count, string.Join(", ", toUpgrade.Select(u => u.Name))),
            CyanBrush, NexLocale.T("apps_btn_upgrade_now"), NexLocale.T("btn_cancel"));

        if (!confirmed) return;

        ShowToast(NexLocale.T("apps_toast_upgrading_title"), NexLocale.Format("apps_toast_upgrading_start_msg", toUpgrade.Count), NexIcon.Clean, CyanBrush);
        int successCount = 0;

        foreach (var app in toUpgrade)
        {
            ShowToast(NexLocale.T("apps_toast_upgrading_title"), NexLocale.Format("apps_toast_upgrading_item_msg", app.Name, app.AvailableVersion), NexIcon.Clean, CyanBrush);
            LogNav($"[WINGET UPGRADE] Actualizare: {app.Name} ({app.Id})...");
            bool ok = await NativeTuning.UpgradeAppNativeAsync(app.Id);
            if (ok)
            {
                successCount++;
                cachedUpgradesList?.Remove(app);
                LogNav($"[OK] Actualizat {app.Name}.");
            }
            else
            {
                LogNav($"[ERR] Eșec actualizare {app.Name}.");
            }
        }

        UpdateTopNotificationsBadge(cachedUpgradesList?.Count ?? 0);
        ShowToast(NexLocale.T("apps_toast_upgrade_done_title"), NexLocale.Format("apps_toast_upgrade_done_msg", successCount, toUpgrade.Count), NexIcon.Check, GreenBrush);
        ShowApps();
    }

    private Border BuildAppUpgradeCard(NativeTuning.AppUpgradeDetail app)
    {
        var card = new Border
        {
            Background = CardBackground(),
            BorderBrush = new SolidColorBrush(app.IsSelected ? Color.FromRgb(255, 42, 133) : Color.FromRgb(24, 36, 52)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 0, 0, 8),
            Cursor = Cursors.Hand
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var chk = new CheckBox
        {
            IsChecked = app.IsSelected,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Cursor = Cursors.Hand
        };
        chk.Checked += (_, _) => { app.IsSelected = true; card.BorderBrush = PinkBrush; ShowApps(); };
        chk.Unchecked += (_, _) => { app.IsSelected = false; card.BorderBrush = new SolidColorBrush(Color.FromRgb(24, 36, 52)); ShowApps(); };
        Grid.SetColumn(chk, 0);
        grid.Children.Add(chk);

        // Icon Box
        var iconBox = new Border
        {
            Width = 28,
            Height = 28,
            CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(Color.FromArgb(32, 56, 189, 248)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(70, 56, 189, 248)),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        var realIcon = NativeTuning.GetOfficialOrCachedAppIcon(app.Id, "", app.Name, null);
        if (realIcon != null)
        {
            var img = new Image { Source = realIcon, Width = 20, Height = 20, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            iconBox.Child = img;
        }
        else
        {
            iconBox.Child = CreateVectorIcon(NexIcon.Clean, CyanBrush, 14);
        }
        Grid.SetColumn(iconBox, 1);
        grid.Children.Add(iconBox);

        // Name & ID
        var textPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 12, 0) };
        textPanel.Children.Add(new TextBlock
        {
            Text = app.Name,
            FontSize = 12.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = TextBrush
        });
        textPanel.Children.Add(new TextBlock
        {
            Text = app.Id,
            FontSize = 10,
            Foreground = MutedBrush,
            Margin = new Thickness(0, 1, 0, 0)
        });
        Grid.SetColumn(textPanel, 2);
        grid.Children.Add(textPanel);

        // Version Badge
        var verBadge = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(35, 16, 185, 129)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(80, 16, 185, 129)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(8, 3, 8, 3),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        };
        var verText = new StackPanel { Orientation = Orientation.Horizontal };
        verText.Children.Add(new TextBlock { Text = app.InstalledVersion, FontSize = 10.5, Foreground = MutedBrush });
        verText.Children.Add(new TextBlock { Text = " -> ", FontSize = 10.5, FontWeight = FontWeights.Bold, Foreground = GreenBrush });
        verText.Children.Add(new TextBlock { Text = app.AvailableVersion, FontSize = 10.5, FontWeight = FontWeights.Bold, Foreground = GreenBrush });
        verBadge.Child = verText;
        Grid.SetColumn(verBadge, 3);
        grid.Children.Add(verBadge);

        // Action Buttons: Upgrade & Uninstall
        var actionRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        var upgBtn = new Button
        {
            Content = NexLocale.T("apps_btn_upgrade"),
            Style = (Style)FindResource("DarkTableFooterButtonStyle"),
            Padding = new Thickness(11, 4, 11, 4),
            FontSize = 11,
            Cursor = Cursors.Hand
        };
        upgBtn.Click += async (_, _) =>
        {
            var fakeItem = new NativeTuning.SoftwareAppItem
            {
                Id = app.Id,
                WingetId = app.Id,
                Name = app.Name,
                Category = NexLocale.T("apps_cat_upgrade"),
                Description = NexLocale.Format("apps_desc_upgrade_to", app.AvailableVersion)
            };
            await ExecuteAppInstallQueueAsync(new List<NativeTuning.SoftwareAppItem> { fakeItem });
            cachedUpgradesList?.Remove(app);
            UpdateTopNotificationsBadge(cachedUpgradesList?.Count ?? 0);
            ShowApps();
        };
        actionRow.Children.Add(upgBtn);

        var uninstBtn = new Button
        {
            Content = NexLocale.T("btn_uninstall"),
            Style = (Style)FindResource("DarkDangerOutlineButtonStyle"),
            Padding = new Thickness(9, 4, 9, 4),
            Margin = new Thickness(8, 0, 0, 0),
            FontSize = 10.5,
            Cursor = Cursors.Hand
        };
        uninstBtn.Click += async (_, _) =>
        {
            var conf = await ShowConfirmModalAsync(
                NexLocale.T("apps_confirm_uninstall_title"),
                NexLocale.Format("apps_confirm_uninstall_msg", app.Name, app.Id),
                PinkBrush, NexLocale.T("btn_uninstall"), NexLocale.T("btn_cancel"));
            if (conf)
            {
                ShowToast(NexLocale.T("apps_toast_uninstalling_title"), NexLocale.Format("apps_toast_uninstalling_msg", app.Name), NexIcon.Trash, AmberBrush);
                bool ok = await NativeTuning.UninstallAppNativeAsync(app.Id);
                if (ok)
                {
                    ShowToast(NexLocale.T("apps_toast_uninstall_done_title"), NexLocale.Format("apps_toast_uninstall_done_msg", app.Name), NexIcon.Check, GreenBrush);
                    cachedUpgradesList?.Remove(app);
                    if (currentCatalog != null)
                    {
                        foreach (var a in currentCatalog)
                        {
                            a.IsInstalled = NativeTuning.CheckAppInstalled(a.RelativeExePath);
                        }
                    }
                    ShowApps();
                }
                else
                {
                    ShowToast(NexLocale.T("apps_toast_uninstall_err_title"), NexLocale.Format("apps_toast_uninstall_err_msg", app.Name), NexIcon.Warning, AmberBrush);
                }
            }
        };
        actionRow.Children.Add(uninstBtn);

        Grid.SetColumn(actionRow, 4);
        grid.Children.Add(actionRow);

        card.Child = grid;
        return card;
    }

    private Border BuildInstalledAppCard(NativeTuning.SoftwareAppItem app)
    {
        var card = new Border
        {
            Background = CardBackground(),
            BorderBrush = new SolidColorBrush(Color.FromRgb(24, 36, 52)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 0, 0, 8)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Icon
        var iconBox = new Border
        {
            Width = 28,
            Height = 28,
            CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(Color.FromArgb(32, 56, 189, 248)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(70, 56, 189, 248)),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        if (app.RealIcon != null)
        {
            var img = new Image { Source = app.RealIcon, Width = 20, Height = 20, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            iconBox.Child = img;
        }
        else
        {
            iconBox.Child = CreateVectorIcon(NexIcon.Layers, CyanBrush, 14);
        }
        Grid.SetColumn(iconBox, 0);
        grid.Children.Add(iconBox);

        // Name & Category
        var textPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 12, 0) };
        textPanel.Children.Add(new TextBlock
        {
            Text = app.Name,
            FontSize = 12.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = TextBrush
        });
        textPanel.Children.Add(new TextBlock
        {
            Text = $"{GetLocalizedCategory(app.Category)}  ·  {app.WingetId}",
            FontSize = 10,
            Foreground = MutedBrush,
            Margin = new Thickness(0, 1, 0, 0)
        });
        Grid.SetColumn(textPanel, 1);
        grid.Children.Add(textPanel);

        // Status badge
        var badge = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(32, 16, 185, 129)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(70, 16, 185, 129)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2, 6, 2),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0),
            Child = new TextBlock { Text = NexLocale.T("status_installed"), FontSize = 9.5, FontWeight = FontWeights.SemiBold, Foreground = GreenBrush }
        };
        Grid.SetColumn(badge, 2);
        grid.Children.Add(badge);

        // Actions
        var actionRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        var reinBtn = new Button
        {
            Content = NexLocale.T("apps_btn_reinstall"),
            Style = (Style)FindResource("DarkTableFooterButtonStyle"),
            Padding = new Thickness(10, 4, 10, 4),
            FontSize = 10.5,
            Cursor = Cursors.Hand
        };
        reinBtn.Click += async (_, _) =>
        {
            await ExecuteAppInstallQueueAsync(new List<NativeTuning.SoftwareAppItem> { app });
        };
        actionRow.Children.Add(reinBtn);

        var uninstBtn = new Button
        {
            Content = NexLocale.T("btn_uninstall"),
            Style = (Style)FindResource("DarkDangerOutlineButtonStyle"),
            Padding = new Thickness(9, 4, 9, 4),
            Margin = new Thickness(8, 0, 0, 0),
            FontSize = 10.5,
            Cursor = Cursors.Hand
        };
        uninstBtn.Click += async (_, _) =>
        {
            var conf = await ShowConfirmModalAsync(
                NexLocale.T("apps_confirm_uninstall_title"),
                NexLocale.Format("apps_confirm_uninstall_cat_msg", app.Name, app.WingetId),
                PinkBrush, NexLocale.T("btn_uninstall"), NexLocale.T("btn_cancel"));
            if (conf)
            {
                ShowToast(NexLocale.T("apps_toast_uninstall_title"), NexLocale.Format("apps_toast_removing_msg", app.Name), NexIcon.Trash, AmberBrush);
                bool ok = await NativeTuning.UninstallAppNativeAsync(app.WingetId);
                if (ok)
                {
                    app.IsInstalled = false;
                    ShowToast(NexLocale.T("apps_toast_uninstall_done_title"), NexLocale.Format("apps_toast_uninstall_done_msg", app.Name), NexIcon.Check, GreenBrush);
                    if (currentCatalog != null)
                    {
                        foreach (var a in currentCatalog)
                        {
                            a.IsInstalled = NativeTuning.CheckAppInstalled(a.RelativeExePath);
                        }
                    }
                    ShowApps();
                }
                else
                {
                    ShowToast(NexLocale.T("apps_toast_uninstall_fail_title"), NexLocale.Format("apps_toast_uninstall_fail_msg", app.Name), NexIcon.Warning, AmberBrush);
                }
            }
        };
        actionRow.Children.Add(uninstBtn);

        Grid.SetColumn(actionRow, 3);
        grid.Children.Add(actionRow);

        card.Child = grid;
        return card;
    }

    private void ShowApps()
    {
        PageRoot.Children.Clear();
        AddPageHeading(NexLocale.T("apps_title"), NexLocale.T("apps_subtitle"));

        currentCatalog ??= NativeTuning.GetDefaultSoftwareCatalog();

        foreach (var app in currentCatalog)
        {
            app.IsInstalled = NativeTuning.CheckAppInstalled(app.RelativeExePath);
        }

        // Hero Segmented Navigation Bar: [ Catalog Esențial ] [ Actualizări Disponibile ] [ Aplicații Instalate ]
        var heroNavBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(10, 16, 26)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(22, 34, 52)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(6),
            Margin = new Thickness(0, 0, 0, 14)
        };

        var heroNavGrid = new Grid();
        heroNavGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        heroNavGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        heroNavGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        heroNavGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        heroNavGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        Border CreateHeroTab(string key, string title, string subtitle, NexIcon icon, Brush activeBrush, int? badgeCount, string? badgeLabel = null)
        {
            bool isCurrent = activeAppsTab.Equals(key, StringComparison.OrdinalIgnoreCase);

            var tabBorder = new Border
            {
                Background = isCurrent ? new SolidColorBrush(Color.FromRgb(17, 28, 46)) : new SolidColorBrush(Color.FromRgb(13, 20, 32)),
                BorderBrush = isCurrent ? new SolidColorBrush(Color.FromRgb(37, 99, 235)) : new SolidColorBrush(Color.FromRgb(20, 32, 48)),
                BorderThickness = new Thickness(isCurrent ? 1.5 : 1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 10, 14, 10),
                Cursor = Cursors.Hand
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Icon box
            var iconBox = new Border
            {
                Width = 32,
                Height = 32,
                CornerRadius = new CornerRadius(6),
                Background = isCurrent
                    ? new SolidColorBrush(Color.FromArgb(45, 56, 189, 248))
                    : new SolidColorBrush(Color.FromArgb(20, 148, 163, 184)),
                Child = CreateVectorIcon(icon, isCurrent ? activeBrush : MutedBrush, 15),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(iconBox, 0);
            grid.Children.Add(iconBox);

            // Title & Subtitle Stack
            var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 8, 0) };
            textStack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 12.5,
                FontWeight = isCurrent ? FontWeights.Bold : FontWeights.SemiBold,
                Foreground = isCurrent ? Brushes.White : new SolidColorBrush(Color.FromRgb(203, 213, 225))
            });
            textStack.Children.Add(new TextBlock
            {
                Text = subtitle,
                FontSize = 10.5,
                Foreground = isCurrent ? activeBrush : MutedBrush,
                Margin = new Thickness(0, 2, 0, 0)
            });
            Grid.SetColumn(textStack, 1);
            grid.Children.Add(textStack);

            // Badge / Count on right
            if (badgeCount.HasValue && badgeCount.Value > 0)
            {
                var badge = new Border
                {
                    Background = key == "Updates"
                        ? new SolidColorBrush(Color.FromArgb(40, 245, 158, 11))
                        : new SolidColorBrush(Color.FromArgb(35, 16, 185, 129)),
                    BorderBrush = key == "Updates"
                        ? new SolidColorBrush(Color.FromArgb(100, 245, 158, 11))
                        : new SolidColorBrush(Color.FromArgb(80, 16, 185, 129)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(8, 2, 8, 2),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock
                    {
                        Text = badgeLabel != null ? $"{badgeCount.Value} {badgeLabel}" : badgeCount.Value.ToString(),
                        FontSize = 10,
                        FontWeight = FontWeights.Bold,
                        Foreground = key == "Updates" ? AmberBrush : GreenBrush
                    }
                };
                Grid.SetColumn(badge, 2);
                grid.Children.Add(badge);
            }
            else if (key == "Catalog")
            {
                var badge = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(30, 56, 189, 248)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(60, 56, 189, 248)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(8, 2, 8, 2),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock
                    {
                        Text = currentCatalog.Count.ToString(),
                        FontSize = 10,
                        FontWeight = FontWeights.Bold,
                        Foreground = CyanBrush
                    }
                };
                Grid.SetColumn(badge, 2);
                grid.Children.Add(badge);
            }

            tabBorder.Child = grid;
            tabBorder.MouseLeftButtonUp += (_, _) =>
            {
                activeAppsTab = key;
                ShowApps();
                if (key == "Updates" && cachedUpgradesList == null && !isScanningUpgrades)
                {
                    TriggerUpgradeScan(false);
                }
            };
            return tabBorder;
        }

        int installedCatalogCount = currentCatalog.Count(a => a.IsInstalled);
        int? upgradesCount = cachedUpgradesList?.Count;

        var tabCatalog = CreateHeroTab("Catalog", NexLocale.T("apps_tab_catalog", "Catalog Esențial"), NexLocale.Format("apps_hero_catalog_sub", currentCatalog.Count), NexIcon.Layers, CyanBrush, null);
        Grid.SetColumn(tabCatalog, 0);
        heroNavGrid.Children.Add(tabCatalog);

        string updatesSub = (upgradesCount.HasValue && upgradesCount.Value > 0)
            ? NexLocale.Format("apps_hero_updates_sub_active", upgradesCount.Value)
            : NexLocale.T("apps_hero_updates_sub_clean");
        var tabUpdates = CreateHeroTab("Updates", NexLocale.T("apps_tab_updates", "Actualizări Disponibile"), updatesSub, NexIcon.Bell, AmberBrush, upgradesCount, NexLocale.T("apps_hero_badge_new"));
        Grid.SetColumn(tabUpdates, 2);
        heroNavGrid.Children.Add(tabUpdates);

        string installedSub = NexLocale.Format("apps_hero_installed_sub", installedCatalogCount);
        var tabInstalled = CreateHeroTab("Installed", NexLocale.T("apps_tab_installed", "Aplicații Instalate"), installedSub, NexIcon.Check, GreenBrush, installedCatalogCount);
        Grid.SetColumn(tabInstalled, 4);
        heroNavGrid.Children.Add(tabInstalled);

        heroNavBorder.Child = heroNavGrid;
        PageRoot.Children.Add(heroNavBorder);

        // ================= TAB 1: CATALOG ESENTIAL =================
        if (activeAppsTab == "Catalog")
        {
            // Action Toolbar Card
            var toolbarCard = new Border
            {
                Background = CardBackground(),
                BorderBrush = new SolidColorBrush(Color.FromRgb(24, 36, 52)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(16, 12, 16, 12),
                Margin = new Thickness(0, 0, 0, 16)
            };

            var tbGrid = new Grid();
            tbGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            tbGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var tbLeft = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var tbStatusText = new TextBlock
            {
                Text = NexLocale.Format("apps_tb_catalog_status_format", installedCatalogCount, currentCatalog.Count),
                FontSize = 12,
                Foreground = MutedBrush
            };
            tbLeft.Children.Add(tbStatusText);
            Grid.SetColumn(tbLeft, 0);
            tbGrid.Children.Add(tbLeft);

            var tbButtons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            var selectAllBtn = new Button
            {
                Content = NexLocale.T("apps_btn_select_avail"),
                Style = (Style)FindResource("SecondaryButtonStyle"),
                Margin = new Thickness(0, 0, 8, 0),
                Padding = new Thickness(12, 6, 12, 6)
            };
            selectAllBtn.Click += (_, _) =>
            {
                foreach (var app in currentCatalog.Where(a => !a.IsInstalled)) app.IsSelected = true;
                ShowApps();
            };
            tbButtons.Children.Add(selectAllBtn);

            var clearBtn = new Button
            {
                Content = NexLocale.T("btn_deselect_all"),
                Style = (Style)FindResource("SecondaryButtonStyle"),
                Margin = new Thickness(0, 0, 12, 0),
                Padding = new Thickness(12, 6, 12, 6)
            };
            clearBtn.Click += (_, _) =>
            {
                foreach (var app in currentCatalog) app.IsSelected = false;
                ShowApps();
            };
            tbButtons.Children.Add(clearBtn);

            var installBtn = new Button
            {
                Style = (Style)FindResource("PrimaryGradientButtonStyle"),
                Padding = new Thickness(16, 7, 16, 7),
                Cursor = Cursors.Hand
            };
            var btnContent = new StackPanel { Orientation = Orientation.Horizontal };
            var dlIcon = CreateVectorIcon(NexIcon.Clean, Brushes.White, 14);
            dlIcon.Margin = new Thickness(0, 0, 7, 0);
            btnContent.Children.Add(dlIcon);
            btnContent.Children.Add(new TextBlock
            {
                Text = NexLocale.T("apps_btn_install_selected"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            });
            installBtn.Content = btnContent;
            installBtn.Click += async (_, _) => await RunAppBatchInstallAsync();
            tbButtons.Children.Add(installBtn);

            Grid.SetColumn(tbButtons, 1);
            tbGrid.Children.Add(tbButtons);

            toolbarCard.Child = tbGrid;
            PageRoot.Children.Add(toolbarCard);

            // Group by category
            var categories = currentCatalog.GroupBy(a => a.Category).ToList();
            foreach (var cat in categories)
            {
                var catHeader = new TextBlock
                {
                    Text = GetLocalizedCategory(cat.Key).ToUpperInvariant(),
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 120, 145)),
                    Margin = new Thickness(4, 12, 0, 8)
                };
                PageRoot.Children.Add(catHeader);

                var catGrid = new Grid { Margin = new Thickness(0, 0, 0, 14) };
                catGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                catGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
                catGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                catGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
                catGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var items = cat.ToList();
                int rowCount = (int)Math.Ceiling(items.Count / 3.0);
                for (int r = 0; r < rowCount; r++)
                {
                    catGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                }

                for (int i = 0; i < items.Count; i++)
                {
                    var app = items[i];
                    var card = BuildSoftwareAppCard(app);
                    var col = (i % 3) * 2;
                    var row = i / 3;
                    card.Margin = new Thickness(0, 0, 0, 8);
                    Grid.SetColumn(card, col);
                    Grid.SetRow(card, row);
                    catGrid.Children.Add(card);
                }
                PageRoot.Children.Add(catGrid);
            }
        }
        // ================= TAB 2: ACTUALIZARI DISPONIBILE (DEDICATED PAGE) =================
        else if (activeAppsTab == "Updates")
        {
            if (isScanningUpgrades)
            {
                var scanCard = new Border
                {
                    Background = CardBackground(),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(24, 38, 56)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(9),
                    Padding = new Thickness(24, 28, 24, 28),
                    Margin = new Thickness(0, 0, 0, 16)
                };
                var st = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                st.Children.Add(CreateVectorIcon(NexIcon.Pulse, CyanBrush, 32));
                st.Children.Add(new TextBlock
                {
                    Text = NexLocale.T("apps_scan_searching_title"),
                    FontSize = 13.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = TextBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 14, 0, 6)
                });
                st.Children.Add(new TextBlock
                {
                    Text = NexLocale.T("apps_scan_searching_desc"),
                    FontSize = 11,
                    Foreground = MutedBrush,
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                scanCard.Child = st;
                PageRoot.Children.Add(scanCard);
                return;
            }

            if (cachedUpgradesList == null)
            {
                if (!isScanningUpgrades)
                {
                    Dispatcher.BeginInvoke(new Action(() => TriggerUpgradeScan(false)));
                }

                var scanCard = new Border
                {
                    Background = CardBackground(),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(24, 38, 56)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(9),
                    Padding = new Thickness(24, 28, 24, 28),
                    Margin = new Thickness(0, 0, 0, 16)
                };
                var st = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                st.Children.Add(CreateVectorIcon(NexIcon.Pulse, CyanBrush, 32));
                st.Children.Add(new TextBlock
                {
                    Text = NexLocale.T("apps_scan_searching_title"),
                    FontSize = 13.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = TextBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 14, 0, 6)
                });
                st.Children.Add(new TextBlock
                {
                    Text = NexLocale.T("apps_scan_searching_desc"),
                    FontSize = 11,
                    Foreground = MutedBrush,
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                scanCard.Child = st;
                PageRoot.Children.Add(scanCard);
                return;
            }

            if (cachedUpgradesList.Count == 0)
            {
                var upToDateCard = new Border
                {
                    Background = CardBackground(),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(24, 42, 60)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(9),
                    Padding = new Thickness(24, 28, 24, 28),
                    Margin = new Thickness(0, 0, 0, 16)
                };
                var st = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                st.Children.Add(CreateVectorIcon(NexIcon.Check, GreenBrush, 32));
                st.Children.Add(new TextBlock
                {
                    Text = NexLocale.T("apps_scan_uptodate_title"),
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Foreground = GreenBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 12, 0, 4)
                });
                st.Children.Add(new TextBlock
                {
                    Text = NexLocale.T("apps_scan_uptodate_desc"),
                    FontSize = 11,
                    Foreground = MutedBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 16)
                });
                var refBtn = new Button
                {
                    Content = NexLocale.T("apps_scan_btn_rescan"),
                    Style = (Style)FindResource("SecondaryButtonStyle"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Padding = new Thickness(14, 6, 14, 6)
                };
                refBtn.Click += (_, _) => TriggerUpgradeScan(true);
                st.Children.Add(refBtn);
                upToDateCard.Child = st;
                PageRoot.Children.Add(upToDateCard);
                return;
            }

            // Toolbar for Upgrades
            int selectedUpgradesCount = cachedUpgradesList.Count(u => u.IsSelected);
            var upgToolbar = new Border
            {
                Background = CardBackground(),
                BorderBrush = new SolidColorBrush(Color.FromRgb(24, 38, 56)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(16, 12, 16, 12),
                Margin = new Thickness(0, 0, 0, 14)
            };
            var utbGrid = new Grid();
            utbGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            utbGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var utbLeft = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            utbLeft.Children.Add(new TextBlock
            {
                Text = NexLocale.Format("apps_upg_tb_status_format", cachedUpgradesList.Count, selectedUpgradesCount),
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = TextBrush
            });
            utbLeft.Children.Add(new TextBlock
            {
                Text = NexLocale.T("apps_upg_tb_desc"),
                FontSize = 10.5,
                Foreground = MutedBrush,
                Margin = new Thickness(0, 2, 0, 0)
            });
            Grid.SetColumn(utbLeft, 0);
            utbGrid.Children.Add(utbLeft);

            var utbRight = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            var selAllBtn = new Button
            {
                Content = NexLocale.T("btn_select_all"),
                Style = (Style)FindResource("SecondaryButtonStyle"),
                Margin = new Thickness(0, 0, 8, 0),
                Padding = new Thickness(11, 5, 11, 5)
            };
            selAllBtn.Click += (_, _) =>
            {
                foreach (var u in cachedUpgradesList) u.IsSelected = true;
                ShowApps();
            };
            utbRight.Children.Add(selAllBtn);

            var deselBtn = new Button
            {
                Content = NexLocale.T("btn_deselect_all"),
                Style = (Style)FindResource("SecondaryButtonStyle"),
                Margin = new Thickness(0, 0, 8, 0),
                Padding = new Thickness(11, 5, 11, 5)
            };
            deselBtn.Click += (_, _) =>
            {
                foreach (var u in cachedUpgradesList) u.IsSelected = false;
                ShowApps();
            };
            utbRight.Children.Add(deselBtn);

            var rescanBtn = new Button
            {
                Content = NexLocale.T("btn_refresh"),
                Style = (Style)FindResource("SecondaryButtonStyle"),
                Margin = new Thickness(0, 0, 10, 0),
                Padding = new Thickness(11, 5, 11, 5)
            };
            rescanBtn.Click += (_, _) => TriggerUpgradeScan(true);
            utbRight.Children.Add(rescanBtn);

            var batchUpgBtn = new Button
            {
                Style = (Style)FindResource("PrimaryGradientButtonStyle"),
                Padding = new Thickness(14, 6, 14, 6),
                Cursor = Cursors.Hand
            };
            var bUpgContent = new StackPanel { Orientation = Orientation.Horizontal };
            bUpgContent.Children.Add(CreateVectorIcon(NexIcon.Clean, Brushes.White, 13));
            bUpgContent.Children.Add(new TextBlock
            {
                Text = NexLocale.Format("apps_btn_batch_upgrade_format", selectedUpgradesCount),
                FontSize = 11.5,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Margin = new Thickness(4, 0, 0, 0)
            });
            batchUpgBtn.Content = bUpgContent;
            batchUpgBtn.Click += async (_, _) => await RunBatchUpgradeAsync(cachedUpgradesList.Where(u => u.IsSelected).ToList());
            utbRight.Children.Add(batchUpgBtn);

            Grid.SetColumn(utbRight, 1);
            utbGrid.Children.Add(utbRight);
            upgToolbar.Child = utbGrid;
            PageRoot.Children.Add(upgToolbar);

            // List of upgrade cards
            foreach (var u in cachedUpgradesList)
            {
                PageRoot.Children.Add(BuildAppUpgradeCard(u));
            }
        }
        // ================= TAB 3: APLICATII INSTALATE =================
        else if (activeAppsTab == "Installed")
        {
            var installedApps = currentCatalog.Where(a => a.IsInstalled).ToList();
            var instToolbar = new Border
            {
                Background = CardBackground(),
                BorderBrush = new SolidColorBrush(Color.FromRgb(24, 38, 56)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(16, 12, 16, 12),
                Margin = new Thickness(0, 0, 0, 14)
            };
            var itbGrid = new Grid();
            itbGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            itbGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var itbLeft = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            itbLeft.Children.Add(new TextBlock
            {
                Text = NexLocale.Format("apps_inst_tb_status_format", installedApps.Count),
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = TextBrush
            });
            itbLeft.Children.Add(new TextBlock
            {
                Text = NexLocale.T("apps_inst_tb_desc"),
                FontSize = 10.5,
                Foreground = MutedBrush,
                Margin = new Thickness(0, 2, 0, 0)
            });
            Grid.SetColumn(itbLeft, 0);
            itbGrid.Children.Add(itbLeft);

            var refInstBtn = new Button
            {
                Content = NexLocale.T("apps_inst_btn_refresh_list"),
                Style = (Style)FindResource("SecondaryButtonStyle"),
                Padding = new Thickness(11, 5, 11, 5)
            };
            refInstBtn.Click += (_, _) =>
            {
                if (currentCatalog != null)
                {
                    foreach (var a in currentCatalog)
                    {
                        a.IsInstalled = NativeTuning.CheckAppInstalled(a.RelativeExePath);
                    }
                }
                ShowToast(NexLocale.T("apps_inst_toast_updated_title"), NexLocale.T("apps_inst_toast_updated_msg"), NexIcon.Check, CyanBrush);
                ShowApps();
            };
            Grid.SetColumn(refInstBtn, 1);
            itbGrid.Children.Add(refInstBtn);

            instToolbar.Child = itbGrid;
            PageRoot.Children.Add(instToolbar);

            if (installedApps.Count == 0)
            {
                var emptyBorder = new Border
                {
                    Background = CardBackground(),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(24, 36, 52)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(24, 18, 24, 18)
                };
                emptyBorder.Child = new TextBlock
                {
                    Text = NexLocale.T("apps_inst_empty_msg"),
                    FontSize = 11.5,
                    Foreground = MutedBrush,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                PageRoot.Children.Add(emptyBorder);
            }
            else
            {
                foreach (var app in installedApps)
                {
                    PageRoot.Children.Add(BuildInstalledAppCard(app));
                }
            }
        }
    }

    private Border BuildSoftwareAppCard(NativeTuning.SoftwareAppItem app)
    {
        var card = new Border
        {
            Background = CardBackground(),
            BorderBrush = new SolidColorBrush(app.IsSelected ? Color.FromRgb(255, 42, 133) : Color.FromRgb(24, 36, 52)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 9, 10, 9),
            Cursor = Cursors.Hand
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var chk = new CheckBox
        {
            IsChecked = app.IsSelected,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Cursor = Cursors.Hand
        };
        chk.Checked += (_, _) => { app.IsSelected = true; card.BorderBrush = PinkBrush; };
        chk.Unchecked += (_, _) => { app.IsSelected = false; card.BorderBrush = new SolidColorBrush(Color.FromRgb(24, 36, 52)); };
        Grid.SetColumn(chk, 0);
        grid.Children.Add(chk);

        // Brand Icon / Badge
        Color brandColor = Color.FromRgb(56, 189, 248);
        try
        {
            var hex = app.BrandColorHex.TrimStart('#');
            if (hex.Length == 6)
            {
                byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                brandColor = Color.FromRgb(r, g, b);
            }
        }
        catch { }

        var iconBox = new Border
        {
            Width = 26,
            Height = 26,
            CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(Color.FromArgb(38, brandColor.R, brandColor.G, brandColor.B)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(85, brandColor.R, brandColor.G, brandColor.B)),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        if (app.RealIcon != null)
        {
            var rImg = new Image
            {
                Source = app.RealIcon,
                Width = 20,
                Height = 20,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            RenderOptions.SetBitmapScalingMode(rImg, BitmapScalingMode.HighQuality);
            iconBox.Child = rImg;
        }
        else
        {
            var iconBrush = new SolidColorBrush(brandColor);
            var iconSymbol = app.Category switch
            {
                "Navigatoare Web" => CreateVectorIcon(NexIcon.Window, iconBrush, 13),
                "Gaming & Mesagerie" => CreateVectorIcon(NexIcon.Gamepad, iconBrush, 13),
                "Media & Sunet" => CreateVectorIcon(NexIcon.Audio, iconBrush, 13),
                "Utilitare & Sistem" => CreateVectorIcon(NexIcon.Clean, iconBrush, 13),
                "Dezvoltare & Programare" => CreateVectorIcon(NexIcon.Cpu, iconBrush, 13),
                _ => CreateVectorIcon(NexIcon.Layers, iconBrush, 13)
            };
            iconBox.Child = iconSymbol;
        }
        Grid.SetColumn(iconBox, 1);
        grid.Children.Add(iconBox);

        var textPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 8, 0) };
        textPanel.Children.Add(new TextBlock
        {
            Text = app.Name,
            FontSize = 12.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = TextBrush
        });
        textPanel.Children.Add(new TextBlock
        {
            Text = NexLocale.T($"app_desc_{app.Id}", app.Description),
            FontSize = 10,
            Foreground = MutedBrush,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 0, 0),
            MaxHeight = 26
        });
        Grid.SetColumn(textPanel, 2);
        grid.Children.Add(textPanel);

        // Status badge
        var badge = new Border
        {
            Background = app.IsInstalled ? new SolidColorBrush(Color.FromArgb(32, 16, 185, 129)) : new SolidColorBrush(Color.FromArgb(20, 140, 160, 180)),
            BorderBrush = app.IsInstalled ? new SolidColorBrush(Color.FromArgb(70, 16, 185, 129)) : new SolidColorBrush(Color.FromArgb(40, 140, 160, 180)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(5, 2, 5, 2),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = app.IsInstalled ? NexLocale.T("status_installed") : NexLocale.T("apps_status_available"),
                FontSize = 9.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = app.IsInstalled ? GreenBrush : MutedBrush
            }
        };
        Grid.SetColumn(badge, 3);
        grid.Children.Add(badge);

        card.MouseLeftButtonUp += (s, e) =>
        {
            if (e.OriginalSource is DependencyObject dep && FindParent<CheckBox>(dep) != null) return;
            chk.IsChecked = !chk.IsChecked;
        };

        card.Child = grid;
        return card;
    }

        private async Task ExecuteAppInstallQueueAsync(List<NativeTuning.SoftwareAppItem> selected)
    {
        if (selected == null || selected.Count == 0) return;

        var cts = new CancellationTokenSource();
        bool isBackgrounded = false;
        var sw = Stopwatch.StartNew();

        int totalApps = selected.Count;
        int currentAppIndex = 0;
        var upToDateApps = new List<string>();
        var installedOrUpgradedApps = new List<string>();
        var failedApps = new List<string>();
        var failedReasons = new List<string>();

        // UI element references
        TextBlock? txtModalSubtitle = null;
        TextBlock? txtAppName = null;
        TextBlock? txtAppDesc = null;
        Border? boxAppIcon = null;
        TextBlock? txtCurrentAction = null;
        Border? boxStatusNotice = null;
        TextBlock? txtStatusNotice = null;
        ProgressBar? pBar = null;
        TextBlock? txtPct = null;
        TextBlock? txtSpeed = null;
        TextBlock? txtEta = null;
        DispatcherTimer? speedTimer = null;

        var stepBorders = new Border[4];
        var stepGlyphs = new TextBlock[4];
        var stepTitles = new TextBlock[4];
        var stepStatuses = new TextBlock[4];
        var stepDescBlocks = new TextBlock[4];

        StackPanel? actionButtonsPanel = null;
        Button? btnBg = null;
        Button? btnCancel = null;

        string[] stepLabels = new[]
        {
            NexLocale.T("apps_step1_label"),
            NexLocale.T("apps_step2_label"),
            NexLocale.T("apps_step3_label"),
            NexLocale.T("apps_step4_label")
        };
        string[] stepDescriptions = new[]
        {
            NexLocale.T("apps_step1_desc"),
            NexLocale.T("apps_step2_desc"),
            NexLocale.T("apps_step3_desc"),
            NexLocale.T("apps_step4_desc")
        };

        string FormatEta(double remainingSeconds)
        {
            if (remainingSeconds < 4.0) return NexLocale.T("apps_eta_seconds_short");
            if (remainingSeconds < 60.0) return NexLocale.Format("apps_eta_seconds_format", (int)Math.Round(remainingSeconds));
            int m = (int)(remainingSeconds / 60.0);
            int s = (int)(remainingSeconds % 60.0);
            return NexLocale.Format("apps_eta_minutes_format", m, s);
        }

        void SetSpotlightIcon(NativeTuning.SoftwareAppItem itm)
        {
            if (boxAppIcon == null) return;
            boxAppIcon.Child = null;

            Color brandColor = Color.FromRgb(56, 189, 248);
            try
            {
                if (!string.IsNullOrEmpty(itm.BrandColorHex))
                {
                    var hex = itm.BrandColorHex.TrimStart('#');
                    if (hex.Length == 6)
                    {
                        byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                        byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                        byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                        brandColor = Color.FromRgb(r, g, b);
                    }
                }
            }
            catch { }

            boxAppIcon.Background = new SolidColorBrush(Color.FromArgb(38, brandColor.R, brandColor.G, brandColor.B));
            boxAppIcon.BorderBrush = new SolidColorBrush(Color.FromArgb(90, brandColor.R, brandColor.G, brandColor.B));

            if (itm.RealIcon != null)
            {
                var img = new Image
                {
                    Source = itm.RealIcon,
                    Width = 24,
                    Height = 24,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                boxAppIcon.Child = img;
            }
            else
            {
                var iconBrush = new SolidColorBrush(brandColor);
                var iconSymbol = itm.Category switch
                {
                    "Navigatoare Web" => CreateVectorIcon(NexIcon.Window, iconBrush, 15),
                    "Gaming & Mesagerie" => CreateVectorIcon(NexIcon.Gamepad, iconBrush, 15),
                    "Media & Sunet" => CreateVectorIcon(NexIcon.Audio, iconBrush, 15),
                    "Utilitare & Sistem" => CreateVectorIcon(NexIcon.Clean, iconBrush, 15),
                    "Dezvoltare & Programare" => CreateVectorIcon(NexIcon.Cpu, iconBrush, 15),
                    _ => CreateVectorIcon(NexIcon.Layers, iconBrush, 15)
                };
                boxAppIcon.Child = iconSymbol;
            }
        }

        void UpdateStepView(int stepIndex, int activeStep)
        {
            int stepNum = stepIndex + 1;
            if (stepNum < activeStep)
            {
                stepBorders[stepIndex].Background = new SolidColorBrush(Color.FromArgb(40, 16, 185, 129));
                stepBorders[stepIndex].BorderBrush = GreenBrush;
                stepGlyphs[stepIndex].Text = "\u2713";
                stepGlyphs[stepIndex].Foreground = GreenBrush;
                stepTitles[stepIndex].Foreground = TextBrush;
                stepStatuses[stepIndex].Text = NexLocale.T("status_finished");
                stepStatuses[stepIndex].Foreground = GreenBrush;
            }
            else if (stepNum == activeStep)
            {
                stepBorders[stepIndex].Background = new SolidColorBrush(Color.FromArgb(40, 56, 189, 248));
                stepBorders[stepIndex].BorderBrush = CyanBrush;
                stepGlyphs[stepIndex].Text = "\u2022";
                stepGlyphs[stepIndex].Foreground = CyanBrush;
                stepTitles[stepIndex].Foreground = TextBrush;
                stepStatuses[stepIndex].Text = NexLocale.T("status_in_progress");
                stepStatuses[stepIndex].Foreground = CyanBrush;
            }
            else
            {
                stepBorders[stepIndex].Background = new SolidColorBrush(Color.FromArgb(20, 140, 160, 180));
                stepBorders[stepIndex].BorderBrush = new SolidColorBrush(Color.FromArgb(50, 140, 160, 180));
                stepGlyphs[stepIndex].Text = "-";
                stepGlyphs[stepIndex].Foreground = MutedBrush;
                stepTitles[stepIndex].Foreground = MutedBrush;
                stepStatuses[stepIndex].Text = NexLocale.T("status_waiting");
                stepStatuses[stepIndex].Foreground = MutedBrush;
            }
        }

        // 1. Build and show the interactive modal
        ShowModal(totalApps == 1 ? NexLocale.T("apps_modal_title_single") : NexLocale.T("apps_modal_title_multi"), body =>
        {
            txtModalSubtitle = new TextBlock
            {
                Text = NexLocale.Format("apps_modal_sub_format", 1, totalApps),
                FontSize = 11.5,
                Foreground = MutedBrush,
                Margin = new Thickness(0, -10, 0, 16)
            };
            body.Children.Add(txtModalSubtitle);

            // Active App Card Showcase
            var appShowcase = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(14, 22, 34)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(30, 46, 68)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(16, 14, 16, 14),
                Margin = new Thickness(0, 0, 0, 16)
            };
            var showcaseGrid = new Grid();
            showcaseGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });
            showcaseGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            boxAppIcon = new Border
            {
                Width = 36,
                Height = 36,
                CornerRadius = new CornerRadius(8),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            Grid.SetColumn(boxAppIcon, 0);
            showcaseGrid.Children.Add(boxAppIcon);

            var infoCol = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
            txtAppName = new TextBlock
            {
                Text = selected[0].Name,
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = TextBrush
            };
            infoCol.Children.Add(txtAppName);

            txtAppDesc = new TextBlock
            {
                Text = $"{GetLocalizedCategory(selected[0].Category)} - {selected[0].WingetId}",
                FontSize = 10.5,
                Foreground = MutedBrush,
                Margin = new Thickness(0, 2, 0, 0)
            };
            infoCol.Children.Add(txtAppDesc);

            txtCurrentAction = new TextBlock
            {
                Text = NexLocale.T("apps_modal_prep_env"),
                FontSize = 11,
                Foreground = CyanBrush,
                FontWeight = FontWeights.Medium,
                Margin = new Thickness(0, 5, 0, 0)
            };
            infoCol.Children.Add(txtCurrentAction);

            Grid.SetColumn(infoCol, 1);
            showcaseGrid.Children.Add(infoCol);

            appShowcase.Child = showcaseGrid;
            body.Children.Add(appShowcase);

            // Set initial icon
            SetSpotlightIcon(selected[0]);

            // Dedicated Status Notice Banner for already-up-to-date notifications
            boxStatusNotice = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(28, 16, 185, 129)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(90, 16, 185, 129)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 0, 0, 14),
                Visibility = Visibility.Collapsed
            };
            var noticeGrid = new Grid();
            noticeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
            noticeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var noticeIcon = CreateVectorIcon(NexIcon.Check, GreenBrush, 13);
            noticeIcon.VerticalAlignment = VerticalAlignment.Center;
            noticeIcon.HorizontalAlignment = HorizontalAlignment.Left;
            Grid.SetColumn(noticeIcon, 0);
            noticeGrid.Children.Add(noticeIcon);

            txtStatusNotice = new TextBlock
            {
                Text = "",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = GreenBrush,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(txtStatusNotice, 1);
            noticeGrid.Children.Add(txtStatusNotice);
            boxStatusNotice.Child = noticeGrid;
            body.Children.Add(boxStatusNotice);

            // Progress Metrics & Bar
            var metricsGrid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            metricsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            metricsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            metricsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            txtPct = new TextBlock
            {
                Text = NexLocale.Format("apps_modal_pct_format", 0),
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = CyanBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(txtPct, 0);
            metricsGrid.Children.Add(txtPct);

            txtSpeed = new TextBlock
            {
                Text = "",
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = CyanBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(txtSpeed, 1);
            metricsGrid.Children.Add(txtSpeed);

            txtEta = new TextBlock
            {
                Text = FormatEta(totalApps * 25.0),
                FontSize = 11.5,
                Foreground = MutedBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(txtEta, 2);
            metricsGrid.Children.Add(txtEta);

            body.Children.Add(metricsGrid);

            pBar = new ProgressBar
            {
                Height = 8,
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Foreground = CyanBrush,
                Background = new SolidColorBrush(Color.FromRgb(16, 26, 40)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(28, 44, 64)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 18)
            };
            body.Children.Add(pBar);

            // Checklist of 4 steps
            var stepsContainer = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(10, 16, 24)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(22, 34, 50)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 12, 14, 12),
                Margin = new Thickness(0, 0, 0, 20)
            };
            var stepsList = new StackPanel();

            for (int s = 0; s < 4; s++)
            {
                var sGrid = new Grid { Margin = new Thickness(0, s == 0 ? 0 : 8, 0, 0) };
                sGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
                sGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                sGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var sBadge = new Border
                {
                    Width = 18,
                    Height = 18,
                    CornerRadius = new CornerRadius(9),
                    Background = new SolidColorBrush(Color.FromArgb(20, 140, 160, 180)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(50, 140, 160, 180)),
                    BorderThickness = new Thickness(1),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center
                };
                var sGlyph = new TextBlock
                {
                    Text = "-",
                    FontSize = 9.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = MutedBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                sBadge.Child = sGlyph;
                stepBorders[s] = sBadge;
                stepGlyphs[s] = sGlyph;
                Grid.SetColumn(sBadge, 0);
                sGrid.Children.Add(sBadge);

                var sTextStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 8, 0) };
                var sTitle = new TextBlock
                {
                    Text = stepLabels[s],
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = MutedBrush
                };
                stepTitles[s] = sTitle;
                sTextStack.Children.Add(sTitle);

                var sDesc = new TextBlock
                {
                    Text = stepDescriptions[s],
                    FontSize = 9.5,
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 120, 145)),
                    Margin = new Thickness(0, 1, 0, 0)
                };
                sTextStack.Children.Add(sDesc);
                stepDescBlocks[s] = sDesc;

                Grid.SetColumn(sTextStack, 1);
                sGrid.Children.Add(sTextStack);

                var sStatus = new TextBlock
                {
                    Text = NexLocale.T("status_waiting"),
                    FontSize = 10,
                    FontWeight = FontWeights.Medium,
                    Foreground = MutedBrush,
                    VerticalAlignment = VerticalAlignment.Center
                };
                stepStatuses[s] = sStatus;
                Grid.SetColumn(sStatus, 2);
                sGrid.Children.Add(sStatus);

                stepsList.Children.Add(sGrid);
            }

            stepsContainer.Child = stepsList;
            body.Children.Add(stepsContainer);

            // Bottom action buttons
            actionButtonsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            btnBg = new Button
            {
                Content = NexLocale.T("apps_modal_btn_bg"),
                Style = (Style)FindResource("DarkTableFooterButtonStyle"),
                Padding = new Thickness(16, 7, 16, 7),
                FontSize = 11,
                Cursor = Cursors.Hand
            };
            btnBg.Click += (_, _) =>
            {
                isBackgrounded = true;
                HideModal();
                ShowToast(NexLocale.T("nav_apps"), NexLocale.Format("apps_modal_toast_bg_msg", totalApps), NexIcon.Info, CyanBrush);
            };
            actionButtonsPanel.Children.Add(btnBg);

            btnCancel = new Button
            {
                Content = NexLocale.T("btn_cancel"),
                Style = (Style)FindResource("DarkDangerOutlineButtonStyle"),
                Padding = new Thickness(16, 7, 16, 7),
                Margin = new Thickness(10, 0, 0, 0),
                FontSize = 11,
                Cursor = Cursors.Hand
            };
            btnCancel.Click += (_, _) =>
            {
                speedTimer?.Stop();
                cts.Cancel();
                HideModal();
                ShowToast(NexLocale.T("apps_modal_toast_cancel_title"), NexLocale.T("apps_modal_toast_cancel_msg"), NexIcon.Warning, AmberBrush);
            };
            actionButtonsPanel.Children.Add(btnCancel);

            body.Children.Add(actionButtonsPanel);
        });

        // Real-time network download speed monitor
        speedTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        long lastBytes = 0;
        var lastTime = DateTime.UtcNow;

        try
        {
            var nics = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback);
            lastBytes = nics.Sum(n => n.GetIPv4Statistics().BytesReceived);
        }
        catch { }

        speedTimer.Tick += (_, _) =>
        {
            try
            {
                var nics = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback);
                long curBytes = nics.Sum(n => n.GetIPv4Statistics().BytesReceived);
                var now = DateTime.UtcNow;
                double secs = (now - lastTime).TotalSeconds;
                long diff = curBytes - lastBytes;

                if (secs > 0 && diff > 0 && pBar != null && pBar.Value < 99 && !isBackgrounded)
                {
                    double speedMBs = (diff / secs) / (1024.0 * 1024.0);
                    if (speedMBs >= 0.1 && txtSpeed != null)
                    {
                        txtSpeed.Text = $"{speedMBs:F1} MB/s";
                    }
                    else if (txtSpeed != null)
                    {
                        txtSpeed.Text = "";
                    }
                }
                else if (txtSpeed != null)
                {
                    txtSpeed.Text = "";
                }

                lastBytes = curBytes;
                lastTime = now;
            }
            catch
            {
                if (txtSpeed != null) txtSpeed.Text = "";
            }
        };
        speedTimer.Start();

        // 2. Execute installation queue asynchronously
        for (int i = 0; i < totalApps; i++)
        {
            if (cts.IsCancellationRequested) break;
            currentAppIndex = i;
            var app = selected[i];

            Dispatcher.Invoke(() =>
            {
                if (ModalOverlay.Visibility != Visibility.Visible)
                {
                    isBackgrounded = true;
                }

                if (!isBackgrounded)
                {
                    if (txtModalSubtitle != null)
                        txtModalSubtitle.Text = NexLocale.Format("apps_modal_sub_format", i + 1, totalApps);

                    if (txtAppName != null) txtAppName.Text = app.Name;
                    if (txtAppDesc != null) txtAppDesc.Text = $"{GetLocalizedCategory(app.Category)} - {app.WingetId}";
                    SetSpotlightIcon(app);

                    for (int s = 0; s < 4; s++) UpdateStepView(s, 1);
                    if (txtCurrentAction != null) txtCurrentAction.Text = NexLocale.Format("apps_modal_init_format", app.Name);
                }
                else
                {
                    ShowToast(NexLocale.T("nav_apps"), NexLocale.Format("apps_modal_toast_bg_item_format", i + 1, totalApps, app.Name), NexIcon.Clean, CyanBrush);
                }
            });

            LogNav($"[WINGET] Start procesare {app.Name} ({app.WingetId})...");

            var (ok, output, wasUpgrade) = await NativeTuning.InstallOrUpgradeAppAsync(
                app,
                onStepUpdate: (stepNum, stepMessage) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (ModalOverlay.Visibility != Visibility.Visible) isBackgrounded = true;

                        if (!isBackgrounded)
                        {
                            if (txtCurrentAction != null) txtCurrentAction.Text = stepMessage;
                            for (int s = 0; s < 4; s++) UpdateStepView(s, stepNum);

                            double baseAppPct = (double)currentAppIndex / totalApps * 100.0;
                            double stepPct = (stepNum switch { 1 => 0.08, 2 => 0.40, 3 => 0.75, 4 => 0.95, _ => 0.05 }) * (100.0 / totalApps);
                            double overallPct = Math.Min(99.0, baseAppPct + stepPct);

                            if (pBar != null) pBar.Value = overallPct;
                            if (txtPct != null) txtPct.Text = NexLocale.Format("apps_modal_pct_format", (int)overallPct);

                            double elapsed = sw.Elapsed.TotalSeconds;
                            if (overallPct > 3.0)
                            {
                                double estTotal = elapsed / (overallPct / 100.0);
                                double rem = Math.Max(2.0, estTotal - elapsed);
                                if (txtEta != null) txtEta.Text = FormatEta(rem);
                            }
                            else
                            {
                                if (txtEta != null) txtEta.Text = FormatEta((totalApps - currentAppIndex) * 25.0);
                            }
                        }
                    });
                },
                ct: cts.Token
            );

            if (ok)
            {
                app.IsInstalled = true;
                app.IsSelected = false;
                LogNav($"[OK] {output}");

                if (output.Contains("deja la cea mai recenta versiune", StringComparison.OrdinalIgnoreCase) ||
                    output.Contains("deja la zi", StringComparison.OrdinalIgnoreCase) ||
                    output.Contains("deja actualizat", StringComparison.OrdinalIgnoreCase) ||
                    output.Contains("nu necesita nicio modificare", StringComparison.OrdinalIgnoreCase))
                {
                    upToDateApps.Add(app.Name);
                    Dispatcher.Invoke(() =>
                    {
                        string exactMsg = NexLocale.Format("apps_modal_already_uptodate_format", app.Name);
                        if (txtCurrentAction != null)
                        {
                            txtCurrentAction.Text = exactMsg;
                            txtCurrentAction.Foreground = GreenBrush;
                        }
                        if (boxStatusNotice != null && txtStatusNotice != null)
                        {
                            txtStatusNotice.Text = exactMsg;
                            boxStatusNotice.Visibility = Visibility.Visible;
                        }
                        if (txtModalSubtitle != null && totalApps == 1)
                        {
                            txtModalSubtitle.Text = NexLocale.Format("apps_modal_already_uptodate_sub", app.Name);
                        }
                        stepStatuses[1].Text = NexLocale.T("apps_step_status_skipped_uptodate");
                        stepStatuses[1].Foreground = GreenBrush;
                        if (stepDescBlocks[1] != null) stepDescBlocks[1].Text = NexLocale.T("apps_modal_step2_uptodate_desc");

                        stepStatuses[2].Text = NexLocale.T("apps_step_status_skipped");
                        stepStatuses[2].Foreground = GreenBrush;
                        if (stepDescBlocks[2] != null) stepDescBlocks[2].Text = NexLocale.T("apps_modal_step3_skipped_desc");

                        stepStatuses[3].Text = NexLocale.T("apps_step_status_verified");
                        stepStatuses[3].Foreground = GreenBrush;
                        if (stepDescBlocks[3] != null) stepDescBlocks[3].Text = NexLocale.Format("apps_modal_step4_uptodate_desc", app.Name);
                    });
                }
                else
                {
                    installedOrUpgradedApps.Add(app.Name);
                }

                if (isBackgrounded)
                {
                    ShowToast(NexLocale.T("apps_modal_toast_done_title"), NexLocale.Format("apps_modal_toast_done_item_msg", app.Name), NexIcon.Check, GreenBrush);
                }
            }
            else
            {
                failedApps.Add(app.Name);
                failedReasons.Add(output);
                LogNav($"[ERR] Operatiune {app.Name} esuata: {output}");

                if (isBackgrounded)
                {
                    ShowToast(NexLocale.T("apps_modal_toast_err_title"), NexLocale.Format("apps_modal_toast_err_item_msg", app.Name), NexIcon.Warning, AmberBrush);
                }
            }
        }

        speedTimer.Stop();
        Dispatcher.Invoke(() => { if (txtSpeed != null) txtSpeed.Text = ""; });

        // 3. Queue completion
        if (!isBackgrounded && !cts.IsCancellationRequested)
        {
            Dispatcher.Invoke(() =>
            {
                if (upToDateApps.Count > 0 && installedOrUpgradedApps.Count == 0 && failedApps.Count == 0)
                {
                    string upMsg = upToDateApps.Count == 1
                        ? NexLocale.Format("apps_modal_already_uptodate_format", upToDateApps[0])
                        : NexLocale.Format("apps_modal_all_uptodate_msg", string.Join(", ", upToDateApps));

                    for (int s = 0; s < 4; s++) UpdateStepView(s, 5);

                    stepStatuses[1].Text = NexLocale.T("apps_step_status_skipped_uptodate");
                    stepStatuses[1].Foreground = GreenBrush;
                    if (stepDescBlocks[1] != null) stepDescBlocks[1].Text = NexLocale.T("apps_modal_step2_uptodate_desc");

                    stepStatuses[2].Text = NexLocale.T("apps_step_status_skipped");
                    stepStatuses[2].Foreground = GreenBrush;
                    if (stepDescBlocks[2] != null) stepDescBlocks[2].Text = NexLocale.T("apps_modal_step3_skipped_desc");

                    stepStatuses[3].Text = NexLocale.T("apps_step_status_verified");
                    stepStatuses[3].Foreground = GreenBrush;
                    if (stepDescBlocks[3] != null) stepDescBlocks[3].Text = upToDateApps.Count == 1
                        ? NexLocale.Format("apps_modal_already_uptodate_sub", upToDateApps[0])
                        : NexLocale.T("apps_modal_all_uptodate_short");

                    if (txtModalSubtitle != null)
                        txtModalSubtitle.Text = upToDateApps.Count == 1
                            ? NexLocale.Format("apps_modal_already_uptodate_sub", upToDateApps[0])
                            : NexLocale.T("apps_modal_all_selected_uptodate");

                    if (txtCurrentAction != null)
                    {
                        txtCurrentAction.Text = upMsg;
                        txtCurrentAction.Foreground = GreenBrush;
                    }

                    if (boxStatusNotice != null && txtStatusNotice != null)
                    {
                        txtStatusNotice.Text = upMsg;
                        boxStatusNotice.Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    if (failedApps.Count > 0)
                    {
                        stepBorders[0].Background = new SolidColorBrush(Color.FromArgb(40, 16, 185, 129));
                        stepBorders[0].BorderBrush = GreenBrush;
                        stepGlyphs[0].Text = "\u2713";
                        stepGlyphs[0].Foreground = GreenBrush;
                        stepStatuses[0].Text = NexLocale.T("status_finished");
                        stepStatuses[0].Foreground = GreenBrush;

                        stepBorders[1].Background = new SolidColorBrush(Color.FromArgb(40, 245, 158, 11));
                        stepBorders[1].BorderBrush = AmberBrush;
                        stepGlyphs[1].Text = "!";
                        stepGlyphs[1].Foreground = AmberBrush;
                        stepStatuses[1].Text = NexLocale.T("status_warning");
                        stepStatuses[1].Foreground = AmberBrush;
                        if (stepDescBlocks[1] != null && failedReasons.Count > 0)
                            stepDescBlocks[1].Text = failedReasons[0];

                        stepBorders[2].Background = new SolidColorBrush(Color.FromArgb(20, 140, 160, 180));
                        stepBorders[2].BorderBrush = new SolidColorBrush(Color.FromArgb(50, 140, 160, 180));
                        stepGlyphs[2].Text = "-";
                        stepGlyphs[2].Foreground = MutedBrush;
                        stepStatuses[2].Text = NexLocale.T("apps_step_status_interrupted");
                        stepStatuses[2].Foreground = MutedBrush;

                        stepBorders[3].Background = new SolidColorBrush(Color.FromArgb(20, 140, 160, 180));
                        stepBorders[3].BorderBrush = new SolidColorBrush(Color.FromArgb(50, 140, 160, 180));
                        stepGlyphs[3].Text = "-";
                        stepGlyphs[3].Foreground = MutedBrush;
                        stepStatuses[3].Text = NexLocale.T("apps_step_status_unvalidated");
                        stepStatuses[3].Foreground = MutedBrush;

                        if (txtModalSubtitle != null)
                            txtModalSubtitle.Text = totalApps == 1
                                ? NexLocale.Format("apps_modal_warn_single_sub", failedApps[0])
                                : NexLocale.Format("apps_modal_warn_multi_sub", failedApps.Count, totalApps);

                        if (txtCurrentAction != null)
                        {
                            txtCurrentAction.Text = failedReasons.Count > 0 ? failedReasons[0] : NexLocale.Format("apps_modal_warn_action_format", failedApps.Count);
                            txtCurrentAction.Foreground = AmberBrush;
                        }

                        if (boxStatusNotice != null && txtStatusNotice != null)
                        {
                            boxStatusNotice.Background = new SolidColorBrush(Color.FromArgb(28, 245, 158, 11));
                            boxStatusNotice.BorderBrush = new SolidColorBrush(Color.FromArgb(90, 245, 158, 11));
                            txtStatusNotice.Foreground = AmberBrush;
                            txtStatusNotice.Text = string.Join("\n", failedReasons);
                            boxStatusNotice.Visibility = Visibility.Visible;
                        }
                    }
                    else
                    {
                        for (int s = 0; s < 4; s++) UpdateStepView(s, 5);

                        if (txtModalSubtitle != null)
                            txtModalSubtitle.Text = NexLocale.Format("apps_modal_all_processed_sub", totalApps);

                        if (txtCurrentAction != null)
                        {
                            txtCurrentAction.Text = upToDateApps.Count > 0
                                ? NexLocale.Format("apps_modal_stat_line_format", installedOrUpgradedApps.Count, string.Join(", ", upToDateApps))
                                : NexLocale.T("apps_modal_action_success");
                            txtCurrentAction.Foreground = GreenBrush;
                        }

                        if (upToDateApps.Count > 0 && boxStatusNotice != null && txtStatusNotice != null)
                        {
                            boxStatusNotice.Background = new SolidColorBrush(Color.FromArgb(28, 16, 185, 129));
                            boxStatusNotice.BorderBrush = new SolidColorBrush(Color.FromArgb(90, 16, 185, 129));
                            txtStatusNotice.Foreground = GreenBrush;
                            txtStatusNotice.Text = NexLocale.Format("apps_modal_notice_uptodate_format", string.Join(", ", upToDateApps));
                            boxStatusNotice.Visibility = Visibility.Visible;
                        }
                    }
                }

                if (pBar != null) pBar.Value = 100;
                if (txtPct != null)
                {
                    txtPct.Text = NexLocale.T("apps_modal_pct_100");
                    txtPct.Foreground = GreenBrush;
                }
                if (txtEta != null)
                {
                    txtEta.Text = NexLocale.Format("apps_modal_total_time_format", Math.Round(sw.Elapsed.TotalSeconds));
                    txtEta.Foreground = GreenBrush;
                }

                if (actionButtonsPanel != null)
                {
                    actionButtonsPanel.Children.Clear();
                    var btnClose = new Button
                    {
                        Content = NexLocale.T("btn_close_window"),
                        Style = (Style)FindResource("DarkTableFooterButtonStyle"),
                        Padding = new Thickness(20, 8, 20, 8),
                        FontSize = 11.5,
                        FontWeight = FontWeights.SemiBold,
                        Cursor = Cursors.Hand
                    };
                    btnClose.Click += (_, _) =>
                    {
                        HideModal();
                        if (activeNav == NavActivator)
                            ShowActivator();
                        else
                            ShowApps();
                    };
                    actionButtonsPanel.Children.Add(btnClose);
                }
            });
        }
        else if (isBackgrounded)
        {
            Dispatcher.Invoke(() =>
            {
                int totalDone = installedOrUpgradedApps.Count + upToDateApps.Count;
                ShowToast(NexLocale.T("apps_modal_toast_done_title"), NexLocale.Format("apps_modal_toast_bg_done_msg", totalDone, totalApps), NexIcon.Check, GreenBrush);
                NativeTuning.SendWindowsNativeToast("NexWin " + NexLocale.T("nav_apps"), NexLocale.Format("apps_modal_win_toast_format", totalDone, totalApps));
                if (activeNav == NavActivator)
                    ShowActivator();
                else
                    ShowApps();
            });
        }
    }

    private async Task RunAppBatchInstallAsync()
    {
        if (currentCatalog == null) return;
        var selected = currentCatalog.Where(a => a.IsSelected).ToList();
        if (selected.Count == 0)
        {
            ShowToast(NexLocale.T("nav_apps"), NexLocale.T("apps_batch_toast_select_msg"), NexIcon.Info, AmberBrush);
            return;
        }

        await ExecuteAppInstallQueueAsync(selected);
    }
}
