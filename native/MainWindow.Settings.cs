using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;

namespace NexWin.Native;

public partial class MainWindow : Window
{
    private void ShowSettings()
    {
        PreparePage(NexLocale.T("settings_title"), NexLocale.T("settings_subtitle"));

        // ── 1. Language Settings Section ─────────────────────────────────
        AddLanguageSettingsSection();

        // ── 2. Behavior & Startup Section ────────────────────────────────
        AddBehaviorSettingsSection();

        // ── 3. About & System Section ────────────────────────────────────
        AddAboutSettingsSection();
    }

    private void AddLanguageSettingsSection()
    {
        if (cardGrid == null) return;

        var sectionHeader = new TextBlock
        {
            Text = NexLocale.T("settings_lang_section"),
            FontSize = 13.5,
            FontWeight = FontWeights.Bold,
            Foreground = TextBrush,
            Margin = new Thickness(0, 4, 0, 4)
        };
        cardGrid.Children.Add(sectionHeader);

        var sectionSub = new TextBlock
        {
            Text = NexLocale.T("settings_lang_section_desc"),
            FontSize = 11.5,
            Foreground = MutedBrush,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10),
            LineHeight = 16
        };
        cardGrid.Children.Add(sectionSub);

        // Language Cards Container
        var langGrid = new Grid { Margin = new Thickness(0, 0, 0, 18) };
        langGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        langGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        langGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Romanian Card
        bool isRo = NexLocale.CurrentLanguage == AppLanguage.Ro;
        var roCard = CreateLanguageCard(
            "RO",
            NexLocale.T("settings_lang_ro"),
            NexLocale.T("settings_lang_ro_desc"),
            isRo,
            () =>
            {
                if (NexLocale.CurrentLanguage != AppLanguage.Ro)
                {
                    NexLocale.SetLanguage(AppLanguage.Ro);
                    ApplyLanguageToChrome();
                    ShowSettings();
                    ShowToast(NexLocale.T("settings_lang_changed_toast"), NexLocale.T("settings_lang_changed_desc"), NexIcon.Check, GreenBrush);
                }
            });
        Grid.SetColumn(roCard, 0);
        langGrid.Children.Add(roCard);

        // English Card
        bool isEn = NexLocale.CurrentLanguage == AppLanguage.En;
        var enCard = CreateLanguageCard(
            "EN",
            NexLocale.T("settings_lang_en"),
            NexLocale.T("settings_lang_en_desc"),
            isEn,
            () =>
            {
                if (NexLocale.CurrentLanguage != AppLanguage.En)
                {
                    NexLocale.SetLanguage(AppLanguage.En);
                    ApplyLanguageToChrome();
                    ShowSettings();
                    ShowToast(NexLocale.T("settings_lang_changed_toast"), NexLocale.T("settings_lang_changed_desc"), NexIcon.Check, GreenBrush);
                }
            });
        Grid.SetColumn(enCard, 2);
        langGrid.Children.Add(enCard);

        cardGrid.Children.Add(langGrid);
    }

    private Border CreateLanguageCard(string flagTag, string title, string description, bool isActive, Action onSelect)
    {
        var border = new Border
        {
            Background = isActive ? new SolidColorBrush(Color.FromArgb(28, 56, 189, 248)) : CardBackground(),
            BorderBrush = isActive ? CyanBrush : new SolidColorBrush(Color.FromRgb(24, 36, 52)),
            BorderThickness = new Thickness(isActive ? 1.8 : 1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 14, 16, 14),
            Cursor = Cursors.Hand
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Left Tag / Icon Badge
        var badgeBorder = new Border
        {
            Width = 36,
            Height = 36,
            CornerRadius = new CornerRadius(18),
            Background = isActive ? new SolidColorBrush(Color.FromArgb(40, 56, 189, 248)) : new SolidColorBrush(Color.FromArgb(25, 140, 160, 180)),
            BorderBrush = isActive ? CyanBrush : new SolidColorBrush(Color.FromArgb(60, 140, 160, 180)),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        var badgeText = new TextBlock
        {
            Text = flagTag,
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = isActive ? CyanBrush : MutedBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        badgeBorder.Child = badgeText;
        Grid.SetColumn(badgeBorder, 0);
        grid.Children.Add(badgeBorder);

        // Center Content
        var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 8, 0) };
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
        var titleTb = new TextBlock
        {
            Text = title,
            FontSize = 13.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = isActive ? CyanBrush : TextBrush
        };
        titleRow.Children.Add(titleTb);

        if (isActive)
        {
            var activeBadge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, 16, 185, 129)),
                BorderBrush = GreenBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 1.5, 6, 1.5),
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            activeBadge.Child = new TextBlock
            {
                Text = NexLocale.T("settings_active_badge"),
                FontSize = 9.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = GreenBrush
            };
            titleRow.Children.Add(activeBadge);
        }

        sp.Children.Add(titleRow);

        var descTb = new TextBlock
        {
            Text = description,
            FontSize = 11,
            Foreground = MutedBrush,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 3, 0, 0),
            LineHeight = 15
        };
        sp.Children.Add(descTb);

        Grid.SetColumn(sp, 1);
        grid.Children.Add(sp);

        // Right Radio Circle Indicator
        var radioCircle = new Ellipse
        {
            Width = 18,
            Height = 18,
            Stroke = isActive ? CyanBrush : MutedBrush,
            StrokeThickness = 2,
            Fill = isActive ? CyanBrush : Brushes.Transparent,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(6, 0, 0, 0)
        };
        Grid.SetColumn(radioCircle, 2);
        grid.Children.Add(radioCircle);

        border.Child = grid;

        border.MouseDown += (_, _) => onSelect();

        return border;
    }

    private void AddBehaviorSettingsSection()
    {
        if (cardGrid == null) return;

        var sectionHeader = new TextBlock
        {
            Text = NexLocale.T("settings_behavior_section"),
            FontSize = 13.5,
            FontWeight = FontWeights.Bold,
            Foreground = TextBrush,
            Margin = new Thickness(0, 6, 0, 10)
        };
        cardGrid.Children.Add(sectionHeader);

        // 1. Startup with Windows Row
        bool isAutostart = IsAutoStartEnabled();
        var autoStartStatusColor = isAutostart ? GreenBrush : MutedBrush;
        var autoStartStatusIcon = isAutostart ? NexIcon.Check : NexIcon.Info;
        string autoStartBadge = isAutostart ? NexLocale.T("status_enabled") : NexLocale.T("status_disabled");

        AddActionRow(
            NexLocale.T("settings_autostart_title"),
            NexLocale.Format("status_format", autoStartBadge),
            autoStartStatusIcon,
            autoStartBadge,
            autoStartStatusColor,
            autoStartStatusColor,
            NexLocale.T("settings_autostart_desc"),
            new RowAction(
                isAutostart ? NexLocale.T("btn_disable") : NexLocale.T("btn_enable"),
                isAutostart ? NexIcon.X : NexIcon.Check,
                isAutostart ? RedBrush : CyanBrush,
                async () =>
                {
                    ToggleAutoStart(!isAutostart);
                    await Task.Delay(150);
                    ShowSettings();
                },
                IsPrimary: true)
        );

        // 2. Automatic Updates Row (Setting that can be enabled/disabled)
        bool isAutoUpdates = NativeTuning.GetSoftwareUpdateNotificationSetting();
        var autoUpdatesStatusColor = isAutoUpdates ? GreenBrush : MutedBrush;
        var autoUpdatesStatusIcon = isAutoUpdates ? NexIcon.Check : NexIcon.Info;
        string autoUpdatesBadge = isAutoUpdates ? NexLocale.T("status_enabled") : NexLocale.T("status_disabled");

        AddActionRow(
            NexLocale.T("settings_updates_title"),
            NexLocale.Format("status_format", autoUpdatesBadge),
            autoUpdatesStatusIcon,
            autoUpdatesBadge,
            autoUpdatesStatusColor,
            autoUpdatesStatusColor,
            NexLocale.T("settings_updates_desc"),
            new RowAction(
                isAutoUpdates ? NexLocale.T("btn_disable") : NexLocale.T("btn_enable"),
                isAutoUpdates ? NexIcon.X : NexIcon.Check,
                isAutoUpdates ? RedBrush : CyanBrush,
                async () =>
                {
                    NativeTuning.SetSoftwareUpdateNotificationSetting(!isAutoUpdates);
                    await Task.Delay(150);
                    ShowSettings();
                },
                IsPrimary: true)
        );
    }

    private void AddAboutSettingsSection()
    {
        if (cardGrid == null) return;

        var sectionHeader = new TextBlock
        {
            Text = NexLocale.T("settings_about_section"),
            FontSize = 13.5,
            FontWeight = FontWeights.Bold,
            Foreground = TextBrush,
            Margin = new Thickness(0, 12, 0, 10)
        };
        cardGrid.Children.Add(sectionHeader);

        var aboutCard = new Border
        {
            Tag = "actionRow",
            Margin = new Thickness(0, 0, 0, 12),
            BorderBrush = new SolidColorBrush(Color.FromRgb(24, 36, 52)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Background = CardBackground(),
            Padding = new Thickness(20, 16, 20, 16)
        };

        var sp = new StackPanel();

        var titleBlock = new TextBlock
        {
            Text = NexLocale.T("settings_suite_brand", "NexWin Tuning Suite"),
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = TextBrush,
            Margin = new Thickness(0, 0, 0, 6)
        };
        sp.Children.Add(titleBlock);

        var descBlock = new TextBlock
        {
            Text = NexLocale.T("settings_about_desc"),
            FontSize = 11.5,
            Foreground = MutedBrush,
            Margin = new Thickness(0, 0, 0, 14),
            LineHeight = 16
        };
        sp.Children.Add(descBlock);

        // Version only
        var versionPanel = CreateInfoItem(NexLocale.T("settings_version"), "v1.0.87");
        sp.Children.Add(versionPanel);

        aboutCard.Child = sp;
        cardGrid.Children.Add(aboutCard);

        // Automated Community Support Goal ("Sustine Proiectul") Card
        var goalInfo = NativeTuning.GetCurrentCommunityGoal();
        int goalPct = goalInfo.Percentage;

        var supportCard = new Border
        {
            Tag = "actionRow",
            Margin = new Thickness(0, 0, 0, 12),
            BorderBrush = new SolidColorBrush(Color.FromRgb(22, 82, 74)),
            BorderThickness = new Thickness(1.2),
            CornerRadius = new CornerRadius(9),
            Background = new SolidColorBrush(Color.FromRgb(10, 22, 34)),
            Padding = new Thickness(20, 16, 20, 16)
        };

        var sGrid = new Grid();
        sGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
        sGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        sGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var sIconBox = new Border
        {
            Width = 36,
            Height = 36,
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(Color.FromArgb(40, 16, 185, 129)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(90, 16, 185, 129)),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = CreateVectorIcon(NexIcon.Rocket, GreenBrush, 18)
        };
        Grid.SetColumn(sIconBox, 0);
        sGrid.Children.Add(sIconBox);

        var sTextStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 18, 0) };
        var sTitleRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        sTitleRow.Children.Add(new TextBlock
        {
            Text = NexLocale.T("support_goal_settings_title", "Susține Proiectul NexWin"),
            FontSize = 13.5,
            FontWeight = FontWeights.Bold,
            Foreground = TextBrush,
            VerticalAlignment = VerticalAlignment.Center
        });
        sTitleRow.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(40, 16, 185, 129)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(90, 16, 185, 129)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(7, 1.5, 7, 1.5),
            Margin = new Thickness(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = $"{goalInfo.CurrentAmount:0} / {goalInfo.TargetAmount:0} {goalInfo.Currency} ({goalPct}%)",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = GreenBrush
            }
        });
        sTextStack.Children.Add(sTitleRow);

        sTextStack.Children.Add(new TextBlock
        {
            Text = NexLocale.T("support_goal_settings_desc"),
            FontSize = 11.5,
            Foreground = MutedBrush,
            Margin = new Thickness(0, 3, 0, 8),
            TextWrapping = TextWrapping.Wrap
        });

        var sBarOuter = new Border
        {
            Height = 6,
            Background = new SolidColorBrush(Color.FromRgb(18, 33, 52)),
            CornerRadius = new CornerRadius(3),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var sBarFill = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            Width = goalPct <= 0 ? 0 : Math.Clamp(goalPct * 3.8, 8, 380),
            CornerRadius = new CornerRadius(3),
            Background = new LinearGradientBrush(Color.FromRgb(16, 185, 129), Color.FromRgb(56, 189, 248), 0.0)
        };
        sBarOuter.Child = sBarFill;
        sTextStack.Children.Add(sBarOuter);

        Grid.SetColumn(sTextStack, 1);
        sGrid.Children.Add(sTextStack);

        var sBtn = MakeCardButton(
            NexLocale.T("support_goal_settings_btn", "Susține Proiectul"),
            NexIcon.Rocket,
            GreenBrush,
            () => { ShowSupportProjectModal(); return Task.CompletedTask; },
            true,
            155
        );
        sBtn.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(sBtn, 2);
        sGrid.Children.Add(sBtn);

        supportCard.Child = sGrid;
        cardGrid.Children.Add(supportCard);

        // Community & Discord Support Card
        var discordCard = new Border
        {
            Tag = "actionRow",
            Margin = new Thickness(0, 0, 0, 14),
            BorderBrush = new SolidColorBrush(Color.FromRgb(40, 54, 88)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Background = new SolidColorBrush(Color.FromRgb(12, 18, 30)),
            Padding = new Thickness(20, 14, 20, 14)
        };

        var dGrid = new Grid();
        dGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
        dGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        dGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Discord Icon Box
        var dIconBox = new Border
        {
            Width = 34,
            Height = 34,
            CornerRadius = new CornerRadius(7),
            Background = new SolidColorBrush(Color.FromRgb(88, 101, 242)),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        };
        var dIconPath = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M19.27 5.33C17.94 4.71 16.5 4.26 15 4a.09.09 0 0 0-.07.03c-.18.33-.39.76-.53 1.09a16.09 16.09 0 0 0-4.8 0c-.14-.34-.35-.76-.54-1.09-.01-.02-.04-.03-.07-.03-1.5.26-2.93.71-4.27 1.33-.01 0-.02.01-.03.02-2.72 4.07-3.47 8.03-3.1 11.95 0 .02.01.04.03.05 1.8 1.32 3.53 2.12 5.24 2.65.03.01.06 0 .07-.02.4-.55.76-1.13 1.07-1.74.02-.04 0-.08-.04-.09-.57-.22-1.11-.48-1.64-.78-.04-.02-.04-.08-.01-.11.11-.08.22-.17.33-.25.02-.02.05-.02.07-.01 3.44 1.57 7.15 1.57 10.55 0 .02-.01.05-.01.07.01.11.09.22.17.33.26.04.03.04.08-.01.11-.52.31-1.07.56-1.64.78-.04.01-.05.06-.04.09.32.61.68 1.19 1.07 1.74.02.02.05.03.08.02 1.72-.53 3.45-1.33 5.25-2.65.02-.01.03-.03.03-.05.44-4.53-.73-8.46-3.1-11.95-.01-.01-.02-.02-.04-.02zM8.52 14.91c-1.03 0-1.89-.95-1.89-2.12s.84-2.12 1.89-2.12c1.06 0 1.9.96 1.89 2.12 0 1.17-.84 2.12-1.89 2.12zm6.97 0c-1.03 0-1.89-.95-1.89-2.12s.84-2.12 1.89-2.12c1.06 0 1.9.96 1.89 2.12 0 1.17-.83 2.12-1.89 2.12z"),
            Fill = Brushes.White,
            Stretch = Stretch.Uniform,
            Width = 18,
            Height = 18,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        dIconBox.Child = dIconPath;
        Grid.SetColumn(dIconBox, 0);
        dGrid.Children.Add(dIconBox);

        // Discord Info
        var dInfo = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 12, 0) };
        var dTitleRow = new StackPanel { Orientation = Orientation.Horizontal };
        dTitleRow.Children.Add(new TextBlock
        {
            Text = NexLocale.T("settings_discord_title"),
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = TextBrush
        });
        var dBadge = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(40, 88, 101, 242)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(88, 101, 242)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 1.5, 6, 1.5),
            Margin = new Thickness(8, 0, 0, 0),
            Child = new TextBlock { Text = "luci3alin", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(165, 180, 252)) }
        };
        dTitleRow.Children.Add(dBadge);
        dInfo.Children.Add(dTitleRow);
        dInfo.Children.Add(new TextBlock
        {
            Text = NexLocale.T("settings_discord_desc"),
            FontSize = 11,
            Foreground = MutedBrush,
            Margin = new Thickness(0, 3, 0, 0)
        });
        Grid.SetColumn(dInfo, 1);
        dGrid.Children.Add(dInfo);

        // Action Buttons
        var dButtons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var copyBtn = new Button
        {
            Content = NexLocale.T("settings_discord_btn_copy"),
            Style = (Style)FindResource("SecondaryButtonStyle"),
            Padding = new Thickness(12, 6, 12, 6),
            Margin = new Thickness(0, 0, 8, 0),
            Cursor = Cursors.Hand
        };
        copyBtn.Click += (_, _) =>
        {
            try
            {
                Clipboard.SetDataObject("luci3alin", true);
                ShowToast(NexLocale.T("settings_discord_copied_title"), NexLocale.T("settings_discord_copied_msg"), NexIcon.Check, GreenBrush);
            }
            catch
            {
                ShowToast(NexLocale.T("settings_discord_title"), "luci3alin", NexIcon.Info, CyanBrush);
            }
        };
        dButtons.Children.Add(copyBtn);

        var openBtn = new Button
        {
            Content = NexLocale.T("settings_discord_btn_open"),
            Style = (Style)FindResource("BlueGradientButtonStyle"),
            Padding = new Thickness(14, 6, 14, 6),
            Cursor = Cursors.Hand
        };
        openBtn.Click += (_, _) =>
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://discord.com",
                    UseShellExecute = true
                });
            }
            catch { }
        };
        dButtons.Children.Add(openBtn);

        Grid.SetColumn(dButtons, 2);
        dGrid.Children.Add(dButtons);

        discordCard.Child = dGrid;
        cardGrid.Children.Add(discordCard);
    }

    private static StackPanel CreateInfoItem(string label, string value)
    {
        var sp = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
        sp.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139))
        });
        sp.Children.Add(new TextBlock
        {
            Text = value,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(215, 228, 242)),
            Margin = new Thickness(0, 2, 0, 0)
        });
        return sp;
    }

    // ── Autostart Helper (Zero-UAC via Task Scheduler /RL HIGHEST) ───────
    private const string AutoStartTaskName = "NexWinAutoStart";
    private static bool _migratedLegacyRunKeyOnce;

    private static bool IsAutoStartEnabled()
    {
        try
        {
            string taskFile = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "Tasks", AutoStartTaskName);
            if (File.Exists(taskFile))
            {
                return true;
            }

            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false);
            var existingVal = key?.GetValue("NexWin")?.ToString();
            if (!string.IsNullOrEmpty(existingVal))
            {
                if (!_migratedLegacyRunKeyOnce)
                {
                    _migratedLegacyRunKeyOnce = true;
                    _ = Task.Run(() => ToggleAutoStart(true));
                }
                return true;
            }
            return false;
        }
        catch { return false; }
    }

    private static void ToggleAutoStart(bool enable)
    {
        _ = Task.Run(() =>
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                string exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "";

                if (enable && !string.IsNullOrEmpty(exePath))
                {
                    // 1. Try creating an elevated Windows Scheduled Task (/RL HIGHEST) so ZERO UAC prompt appears at boot
                    bool taskCreated = RunSchtasks($"/Create /TN \"{AutoStartTaskName}\" /TR \"\\\"{exePath}\\\" --tray\" /SC ONLOGON /RL HIGHEST /F");
                    if (taskCreated)
                    {
                        // Remove legacy HKCU Run entry so UAC never pops up
                        key?.DeleteValue("NexWin", false);
                    }
                    else
                    {
                        // Fallback if not running as Admin: add --tray --no-elevate so UAC still never pops up
                        key?.SetValue("NexWin", $"\"{exePath}\" --tray --no-elevate");
                    }
                }
                else
                {
                    RunSchtasks($"/Delete /TN \"{AutoStartTaskName}\" /F");
                    key?.DeleteValue("NexWin", false);
                }
            }
            catch { }
        });
    }

    private static bool RunSchtasks(string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return false;
            proc.WaitForExit(4000);
            return proc.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
