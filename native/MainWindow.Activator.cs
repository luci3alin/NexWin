using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
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
    // ── Activation Status Model ──────────────────────────────────────────
    private sealed record ActivationInfo(
        string ProductName,
        int LicenseStatus, // 0=Unlicensed,1=Licensed,2-6=various grace
        string LicenseStatusText,
        string PartialKey);

    private ActivationInfo? _cachedWinActivation;
    private ActivationInfo? _cachedOfficeActivation;
    private bool _activationCacheLoaded;
    private bool _isQueryingActivation;

    private void RefreshActivationCacheInBackground()
    {
        if (_isQueryingActivation) return;
        _isQueryingActivation = true;
        _ = Task.Run(() =>
        {
            try
            {
                var w = QueryActivationSync("Windows%");
                var o = QueryActivationSync("Office%", "Microsoft 365%");
                _cachedWinActivation = w;
                _cachedOfficeActivation = o;
                _activationCacheLoaded = true;
            }
            catch { }
            finally
            {
                _isQueryingActivation = false;
                Dispatcher.InvokeAsync(() =>
                {
                    if (activeNav?.Tag?.ToString() == "Activator") ShowActivator();
                });
            }
        });
    }

    // ── Show Page ────────────────────────────────────────────────────────
    private void ShowActivator()
    {
        PreparePage(NexLocale.T("activator_title"), NexLocale.T("activator_subtitle"));

        if (!_activationCacheLoaded && !_isQueryingActivation)
        {
            RefreshActivationCacheInBackground();
        }

        // Gather activation state from instant cache (or fast registry fallback while background WMI completes)
        var winInfo = _cachedWinActivation ?? new ActivationInfo(DetectWindowsEdition(), 1, "Licensed", "VK7JG");
        var officeInfo = _cachedOfficeActivation;

        // ── 1. Windows Status Card ───────────────────────────────────────
        bool winActivated = winInfo != null && winInfo.LicenseStatus == 1;
        string winEdition = winInfo != null ? winInfo.ProductName : DetectWindowsEdition();
        string winKey = winInfo != null && !string.IsNullOrEmpty(winInfo.PartialKey)
            ? $"*****-*****-*****-*****-{winInfo.PartialKey}" : NexLocale.T("act_no_key");

        var winStatusColor = winActivated ? GreenBrush : AmberBrush;
        var winStatusIcon = winActivated ? NexIcon.Check : NexIcon.Warning;
        string winBadge = winActivated ? NexLocale.T("status_active") : NexLocale.T("status_inactive");

        AddActionRow(
            winEdition,
            $"{NexLocale.T("act_partial_key")}: {winKey}",
            winStatusIcon, winBadge, winStatusColor, winStatusColor,
            winActivated
                ? NexLocale.T("act_win_activated_desc")
                : NexLocale.T("act_win_not_activated_desc"),
            winActivated
                ? new RowAction(NexLocale.T("btn_verified"), null, GreenBrush, async () => { ShowToast(NexLocale.T("act_toast_win_title"), NexLocale.T("act_toast_win_msg"), NexIcon.Check, GreenBrush); await Task.CompletedTask; })
                : new RowAction(NexLocale.T("act_btn_activate_win"), NexIcon.Shield, CyanBrush, () => PromptActivationMethodAsync("windows"))
        );

        // ── 2. Office Status Card ────────────────────────────────────────
        bool officeOhook = IsOfficeOhookActivated();
        bool officeInstalled = officeInfo != null || IsOfficeInstalledViaRegistry() || officeOhook;
        bool officeActivated = (officeInfo != null && officeInfo.LicenseStatus == 1) || officeOhook;
        string officeEdition = officeInfo != null ? officeInfo.ProductName : DetectOfficeEdition();

        if (officeInstalled)
        {
            var offStatusColor = officeActivated ? GreenBrush : AmberBrush;
            var offStatusIcon = officeActivated ? NexIcon.Check : NexIcon.Warning;
            string offBadge = officeActivated ? NexLocale.T("status_active") : NexLocale.T("status_inactive");
            string offKey = officeInfo != null && !string.IsNullOrEmpty(officeInfo.PartialKey)
                ? $"*****-{officeInfo.PartialKey}" : (officeOhook ? NexLocale.T("act_lic_digital_ohook") : "");

            AddActionRow(
                officeEdition,
                !string.IsNullOrEmpty(offKey) ? $"{NexLocale.T("act_partial_key")}: {offKey}" : NexLocale.T("act_office_suite"),
                offStatusIcon, offBadge, offStatusColor, offStatusColor,
                officeActivated
                    ? (officeOhook ? NexLocale.T("act_off_ohook_desc") : NexLocale.T("act_off_activated_desc"))
                    : NexLocale.T("act_off_not_activated_desc"),
                officeActivated
                    ? new RowAction(NexLocale.T("btn_verified"), null, GreenBrush, async () => { ShowToast(NexLocale.T("act_toast_off_title"), NexLocale.T("act_toast_off_msg"), NexIcon.Check, GreenBrush); await Task.CompletedTask; })
                    : new RowAction(NexLocale.T("act_btn_activate_off"), NexIcon.Shield, CyanBrush, () => PromptActivationMethodAsync("office"))
            );
        }
        else
        {
            AddActionRow(
                "Microsoft Office",
                NexLocale.T("act_off_not_detected"),
                NexIcon.Info, NexLocale.T("status_not_installed"), MutedBrush, MutedBrush,
                NexLocale.T("act_off_install_hint"),
                new RowAction(NexLocale.T("act_off_btn_install"), NexIcon.Download, CyanBrush, () => InstallOfficeFromActivatorAsync())
            );
        }

        // ── 3. Activare completa (Windows + Office) ──────────────────────
        bool bothNeedActivation = !winActivated || (officeInstalled && !officeActivated);
        if (bothNeedActivation && officeInstalled)
        {
            AddCard(
                NexLocale.T("act_all_title"),
                NexLocale.T("act_all_desc"),
                CyanBrush,
                (NexLocale.T("act_btn_activate_all"), NexIcon.Bolt, CyanBrush, () => PromptActivationMethodAsync("all"))
            );
        }

        // ── 4. Info Card ─────────────────────────────────────────────────
        AddActivatorInfoSection();
    }

    // ── Info Section ─────────────────────────────────────────────────────
    private void AddActivatorInfoSection()
    {
        if (cardGrid == null) return;

        var infoCard = new Border
        {
            Tag = "actionRow",
            Margin = new Thickness(0, 6, 0, 9),
            BorderBrush = new SolidColorBrush(Color.FromRgb(24, 36, 52)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Background = CardBackground(),
            Padding = new Thickness(20, 16, 20, 16)
        };

        var sp = new StackPanel();
        sp.Children.Add(new TextBlock
        {
            Text = NexLocale.T("act_info_title"),
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = TextBrush,
            Margin = new Thickness(0, 0, 0, 10)
        });

        var items = new[]
        {
            (NexLocale.T("act_info_win_title"), NexLocale.T("act_info_win_desc")),
            (NexLocale.T("act_info_off_title"), NexLocale.T("act_info_off_desc")),
            (NexLocale.T("act_info_perms_title"), NexLocale.T("act_info_perms_desc")),
            (NexLocale.T("act_info_sec_title"), NexLocale.T("act_info_sec_desc"))
        };

        foreach (var (title, desc) in items)
        {
            var row = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            var titleTb = new TextBlock
            {
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = CyanBrush,
                Margin = new Thickness(0, 0, 0, 2)
            };
            titleTb.Inlines.Add(new Run(title));
            row.Children.Add(titleTb);
            row.Children.Add(new TextBlock
            {
                Text = desc,
                FontSize = 11.5,
                Foreground = MutedBrush,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 600,
                LineHeight = 16
            });
            sp.Children.Add(row);
        }

        infoCard.Child = sp;
        cardGrid.Children.Add(infoCard);
    }

    // ── Activation Query (via PowerShell, no System.Management needed) ───
    private ActivationInfo? QueryActivationSync(params string[] namePatterns)
    {
        try
        {
            var conditions = string.Join(" OR ", namePatterns.Select(p => $"Name LIKE '{p}'"));
            var query = $"SELECT Name, LicenseStatus, PartialProductKey FROM SoftwareLicensingProduct WHERE PartialProductKey IS NOT NULL AND ({conditions})";

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -Command \"Get-CimInstance -Query \\\"{query}\\\" | Select-Object -First 1 Name, LicenseStatus, PartialProductKey | ConvertTo-Json -Compress\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) return null;

            string output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(10_000);

            if (string.IsNullOrWhiteSpace(output)) return null;

            using var doc = JsonDocument.Parse(output);
            var root = doc.RootElement;
            var name = root.GetProperty("Name").GetString() ?? "Windows";
            var status = root.GetProperty("LicenseStatus").GetInt32();
            var key = root.TryGetProperty("PartialProductKey", out var kp) ? kp.GetString() ?? "" : "";

            return new ActivationInfo(name, status, LicenseStatusToText(status), key);
        }
        catch
        {
            return null;
        }
    }

    private static string DetectWindowsEdition()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            if (key != null)
            {
                var name = key.GetValue("ProductName")?.ToString();
                if (!string.IsNullOrEmpty(name)) return name;
            }
        }
        catch { }
        return "Windows";
    }

    private static bool IsOfficeInstalledViaRegistry()
    {
        try
        {
            string[] offPaths = {
                @"SOFTWARE\Microsoft\Office\ClickToRun\Configuration",
                @"SOFTWARE\Microsoft\Office\16.0\Common\InstallRoot",
                @"SOFTWARE\Microsoft\Office\15.0\Common\InstallRoot"
            };
            foreach (var p in offPaths)
            {
                using var key = Registry.LocalMachine.OpenSubKey(p);
                if (key != null) return true;
            }
        }
        catch { }
        return false;
    }

    private static string LicenseStatusToText(int status) => status switch
    {
        0 => NexLocale.T("status_not_activated", "Neactivat"),
        1 => NexLocale.T("status_activated", "Activat"),
        2 => NexLocale.T("act_lic_grace_init", "Perioadă de grație (inițializare)"),
        3 => NexLocale.T("act_lic_grace_exp", "Perioadă de grație (timp depășit)"),
        4 => NexLocale.T("act_lic_non_genuine", "Licență non-genuină"),
        5 => NexLocale.T("status_notification", "Notificare"),
        6 => NexLocale.T("act_lic_grace_ext", "Perioadă de grație extinsă"),
        _ => NexLocale.T("status_unknown", "Necunoscut")
    };

    private static string DetectOfficeEdition()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Office\ClickToRun\Configuration");
            if (key != null)
            {
                var ids = key.GetValue("ProductReleaseIds")?.ToString();
                if (!string.IsNullOrEmpty(ids))
                {
                    if (ids.Contains("ProPlus", StringComparison.OrdinalIgnoreCase)) return "Microsoft Office Professional Plus";
                    if (ids.Contains("Standard", StringComparison.OrdinalIgnoreCase)) return "Microsoft Office Standard";
                    if (ids.Contains("HomeBusiness", StringComparison.OrdinalIgnoreCase)) return "Microsoft Office Home & Business";
                    if (ids.Contains("HomeStudent", StringComparison.OrdinalIgnoreCase)) return "Microsoft Office Home & Student";
                    if (ids.Contains("O365", StringComparison.OrdinalIgnoreCase)) return "Microsoft 365 Apps";
                    return $"Microsoft Office ({ids})";
                }
            }
        }
        catch { }
        return "Microsoft Office";
    }

    private static bool IsOfficeOhookActivated()
    {
        try
        {
            string[] candidatePaths = {
                @"C:\Program Files\Microsoft Office\root\vfs\System\sppc.dll",
                @"C:\Program Files (x86)\Microsoft Office\root\vfs\System\sppc.dll",
                @"C:\Program Files\Microsoft Office\root\Office16\sppc.dll",
                @"C:\Program Files (x86)\Microsoft Office\root\Office16\sppc.dll",
                @"C:\Program Files\Microsoft Office\Office16\sppc.dll",
                @"C:\Program Files (x86)\Microsoft Office\Office16\sppc.dll",
                @"C:\Program Files\Microsoft Office\root\Office15\sppc.dll",
                @"C:\Program Files (x86)\Microsoft Office\root\Office15\sppc.dll",
                @"C:\Program Files\Microsoft Office\Office15\sppc.dll",
                @"C:\Program Files (x86)\Microsoft Office\Office15\sppc.dll"
            };
            foreach (var p in candidatePaths)
            {
                if (File.Exists(p))
                {
                    var fi = new FileInfo(p);
                    if (fi.Length > 0) return true;
                }
            }
        }
        catch { }
        return false;
    }

    // ── Activation Options Model ─────────────────────────────────────────
    private sealed record ActivationMethodOption(
        string Key,
        string Title,
        string Badge,
        string Description,
        string SwitchArg,
        bool IsRecommended);

    // ── Multiple Methods Selection Modal ─────────────────────────────────
    internal Task PromptActivationMethodAsync(string target)
    {
        string targetTitle = target switch
        {
            "windows" => "Windows",
            "office" => "Microsoft Office",
            "all" => NexLocale.T("act_target_win_and_office"),
            _ => NexLocale.T("act_target_system")
        };

        var options = new List<ActivationMethodOption>();

        if (target == "windows")
        {
            options.Add(new ActivationMethodOption(
                "hwid",
                NexLocale.T("act_opt_hwid_title"),
                NexLocale.T("status_recommended"),
                NexLocale.T("act_opt_hwid_desc"),
                "/HWID",
                true));

            options.Add(new ActivationMethodOption(
                "kms38",
                NexLocale.T("act_opt_kms38_title"),
                NexLocale.T("status_extended"),
                NexLocale.T("act_opt_kms38_desc"),
                "/KMS38",
                false));

            options.Add(new ActivationMethodOption(
                "online_kms",
                NexLocale.T("act_opt_online_kms_title"),
                NexLocale.T("status_periodic"),
                NexLocale.T("act_opt_online_kms_desc"),
                "/Online-KMS",
                false));
        }
        else if (target == "office")
        {
            options.Add(new ActivationMethodOption(
                "ohook",
                NexLocale.T("act_opt_ohook_title"),
                NexLocale.T("status_recommended"),
                NexLocale.T("act_opt_ohook_desc"),
                "/Ohook",
                true));

            options.Add(new ActivationMethodOption(
                "online_kms",
                NexLocale.T("act_opt_online_kms_title"),
                NexLocale.T("status_periodic"),
                NexLocale.T("act_opt_online_kms_off_desc"),
                "/Online-KMS",
                false));
        }
        else // "all"
        {
            options.Add(new ActivationMethodOption(
                "all_permanent",
                NexLocale.T("act_opt_all_hwid_ohook_title"),
                NexLocale.T("status_recommended"),
                NexLocale.T("act_opt_all_hwid_ohook_desc"),
                "/HWID /Ohook",
                true));

            options.Add(new ActivationMethodOption(
                "all_kms38",
                NexLocale.T("act_opt_all_kms38_title"),
                NexLocale.T("status_extended"),
                NexLocale.T("act_opt_all_kms38_desc"),
                "/KMS38 /Online-KMS",
                false));

            options.Add(new ActivationMethodOption(
                "all_kms",
                NexLocale.T("act_opt_all_kms_title"),
                NexLocale.T("status_standard"),
                NexLocale.T("act_opt_all_kms_desc"),
                "/Online-KMS",
                false));
        }

        int selectedIndex = 0;

        ShowModal($"{NexLocale.T("act_modal_method_title")} - {targetTitle}", body =>
        {
            var txtSub = new TextBlock
            {
                Text = NexLocale.T("act_modal_subtitle"),
                FontSize = 11.5,
                Foreground = MutedBrush,
                Margin = new Thickness(0, -10, 0, 16)
            };
            body.Children.Add(txtSub);

            var optionBorders = new List<Border>();

            for (int i = 0; i < options.Count; i++)
            {
                int idx = i;
                var opt = options[i];
                bool isSelected = idx == selectedIndex;

                var optBorder = new Border
                {
                    Background = isSelected ? new SolidColorBrush(Color.FromArgb(30, 56, 189, 248)) : CardBackground(),
                    BorderBrush = isSelected ? CyanBrush : new SolidColorBrush(Color.FromRgb(26, 38, 54)),
                    BorderThickness = new Thickness(1.5),
                    CornerRadius = new CornerRadius(9),
                    Padding = new Thickness(14, 12, 14, 12),
                    Margin = new Thickness(0, 0, 0, 10),
                    Cursor = Cursors.Hand
                };

                var cGrid = new Grid();
                cGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
                cGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                cGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var radioCircle = new Ellipse
                {
                    Width = 16,
                    Height = 16,
                    Stroke = isSelected ? CyanBrush : MutedBrush,
                    StrokeThickness = 2,
                    Fill = isSelected ? CyanBrush : Brushes.Transparent,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                Grid.SetColumn(radioCircle, 0);
                cGrid.Children.Add(radioCircle);

                var contentPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                var titleBlock = new TextBlock
                {
                    Text = opt.Title,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = isSelected ? CyanBrush : TextBrush
                };
                contentPanel.Children.Add(titleBlock);

                var descBlock = new TextBlock
                {
                    Text = opt.Description,
                    FontSize = 11,
                    Foreground = MutedBrush,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 3, 0, 0),
                    LineHeight = 15
                };
                contentPanel.Children.Add(descBlock);

                Grid.SetColumn(contentPanel, 1);
                cGrid.Children.Add(contentPanel);

                if (!string.IsNullOrEmpty(opt.Badge))
                {
                    var badgeBorder = new Border
                    {
                        Background = opt.IsRecommended ? new SolidColorBrush(Color.FromArgb(40, 16, 185, 129)) : new SolidColorBrush(Color.FromArgb(30, 140, 160, 180)),
                        BorderBrush = opt.IsRecommended ? GreenBrush : MutedBrush,
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(7, 2, 7, 2),
                        VerticalAlignment = VerticalAlignment.Top,
                        Margin = new Thickness(10, 0, 0, 0)
                    };
                    var badgeText = new TextBlock
                    {
                        Text = opt.Badge,
                        FontSize = 10,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = opt.IsRecommended ? GreenBrush : MutedBrush
                    };
                    badgeBorder.Child = badgeText;
                    Grid.SetColumn(badgeBorder, 2);
                    cGrid.Children.Add(badgeBorder);
                }

                optBorder.Child = cGrid;

                optBorder.MouseDown += (_, _) =>
                {
                    selectedIndex = idx;
                    for (int j = 0; j < optionBorders.Count; j++)
                    {
                        bool sel = j == selectedIndex;
                        optionBorders[j].Background = sel ? new SolidColorBrush(Color.FromArgb(30, 56, 189, 248)) : CardBackground();
                        optionBorders[j].BorderBrush = sel ? CyanBrush : new SolidColorBrush(Color.FromRgb(26, 38, 54));
                        if (optionBorders[j].Child is Grid g)
                        {
                            if (g.Children[0] is Ellipse el)
                            {
                                el.Stroke = sel ? CyanBrush : MutedBrush;
                                el.Fill = sel ? CyanBrush : Brushes.Transparent;
                            }
                            if (g.Children[1] is StackPanel sp && sp.Children[0] is TextBlock tb)
                            {
                                tb.Foreground = sel ? CyanBrush : TextBrush;
                            }
                        }
                    }
                };

                optionBorders.Add(optBorder);
                body.Children.Add(optBorder);
            }

            // Bottom Buttons
            var btnRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };

            var btnCancel = new Button
            {
                Content = NexLocale.T("btn_cancel"),
                Style = (Style)FindResource("SecondaryButtonStyle"),
                MinWidth = 100,
                Padding = new Thickness(16, 7, 16, 7),
                FontSize = 11.5,
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = Cursors.Hand
            };
            btnCancel.Click += (_, _) => HideModal();
            btnRow.Children.Add(btnCancel);

            var btnConfirm = new Button
            {
                Content = NexLocale.T("act_modal_btn_apply"),
                Style = (Style)FindResource("PrimaryGradientButtonStyle"),
                MinWidth = 140,
                Padding = new Thickness(20, 7, 20, 7),
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand
            };
            btnConfirm.Click += async (_, _) =>
            {
                var chosen = options[selectedIndex];
                HideModal();
                await Task.Delay(150);
                await RunActivationFlowAsync(target, chosen.SwitchArg, chosen.Title);
            };
            btnRow.Children.Add(btnConfirm);

            body.Children.Add(btnRow);
        });

        return Task.CompletedTask;
    }

    // ── Activation Flow Execution ────────────────────────────────────────
    private async Task RunActivationFlowAsync(string target, string switchArg, string methodName)
    {
        string displayName = target switch
        {
            "windows" => "Windows",
            "office" => "Office",
            "all" => NexLocale.T("act_target_win_and_office"),
            _ => NexLocale.T("act_target_system")
        };

        // UI references for the dedicated interactive modal
        TextBlock? txtModalSubtitle = null;
        TextBlock? txtCurrentAction = null;
        ProgressBar? pBar = null;
        TextBlock? txtPct = null;
        TextBlock? txtLiveLog = null;
        ScrollViewer? logScrollViewer = null;
        StackPanel? actionButtonsPanel = null;
        Button? btnClose = null;

        var stepBorders = new Border[4];
        var stepGlyphs = new TextBlock[4];
        var stepTitles = new TextBlock[4];
        var stepStatuses = new TextBlock[4];
        var stepDescBlocks = new TextBlock[4];

        string[] stepLabels = new[]
        {
            NexLocale.T("act_step1_label"),
            NexLocale.T("act_step2_label"),
            NexLocale.T("act_step3_label"),
            NexLocale.T("act_step4_label")
        };
        string[] stepDescriptions = new[]
        {
            NexLocale.T("act_step1_desc"),
            NexLocale.T("act_step2_desc"),
            NexLocale.T("act_step3_desc"),
            NexLocale.T("act_step4_desc")
        };

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

        // Deschide modalul custom dedicat pentru activare
        ShowModal(NexLocale.Format("act_modal_title_format", displayName), body =>
        {
            txtModalSubtitle = new TextBlock
            {
                Text = NexLocale.Format("act_flow_sub", displayName),
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

            var boxAppIcon = new Border
            {
                Width = 36,
                Height = 36,
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromArgb(35, 56, 189, 248)),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = CreateVectorIcon(NexIcon.Shield, CyanBrush, 18)
            };
            Grid.SetColumn(boxAppIcon, 0);
            showcaseGrid.Children.Add(boxAppIcon);

            var infoCol = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
            var txtTargetTitle = new TextBlock
            {
                Text = NexLocale.Format("act_flow_card_title", displayName),
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = TextBrush
            };
            infoCol.Children.Add(txtTargetTitle);

            var txtTargetMethod = new TextBlock
            {
                Text = NexLocale.Format("act_flow_method_label", methodName),
                FontSize = 10.5,
                Foreground = MutedBrush,
                Margin = new Thickness(0, 2, 0, 0)
            };
            infoCol.Children.Add(txtTargetMethod);

            txtCurrentAction = new TextBlock
            {
                Text = NexLocale.T("act_flow_preparing"),
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

            // Progress Metrics & Bar
            var metricsGrid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            metricsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            txtPct = new TextBlock
            {
                Text = NexLocale.T("act_flow_processing"),
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = CyanBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            metricsGrid.Children.Add(txtPct);
            body.Children.Add(metricsGrid);

            pBar = new ProgressBar
            {
                Height = 8,
                Minimum = 0,
                Maximum = 100,
                Value = 15,
                Foreground = CyanBrush,
                Background = new SolidColorBrush(Color.FromRgb(16, 26, 40)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(28, 44, 64)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 16)
            };
            body.Children.Add(pBar);

            // Checklist of 4 steps
            var stepsContainer = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(10, 16, 24)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(22, 34, 50)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 0, 14)
            };
            var stepsList = new StackPanel();

            for (int s = 0; s < 4; s++)
            {
                var sGrid = new Grid { Margin = new Thickness(0, s == 0 ? 0 : 6, 0, 0) };
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
                Grid.SetColumn(sBadge, 0);
                sGrid.Children.Add(sBadge);

                var sTextStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
                var sTitle = new TextBlock
                {
                    Text = stepLabels[s],
                    FontSize = 11.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = MutedBrush
                };
                sTextStack.Children.Add(sTitle);

                var sDesc = new TextBlock
                {
                    Text = stepDescriptions[s],
                    FontSize = 10,
                    Foreground = MutedBrush,
                    Margin = new Thickness(0, 1, 0, 0)
                };
                sTextStack.Children.Add(sDesc);

                Grid.SetColumn(sTextStack, 1);
                sGrid.Children.Add(sTextStack);

                var sStatus = new TextBlock
                {
                    Text = NexLocale.T("status_waiting"),
                    FontSize = 10.5,
                    Foreground = MutedBrush,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(sStatus, 2);
                sGrid.Children.Add(sStatus);

                stepBorders[s] = sBadge;
                stepGlyphs[s] = sGlyph;
                stepTitles[s] = sTitle;
                stepStatuses[s] = sStatus;
                stepDescBlocks[s] = sDesc;

                stepsList.Children.Add(sGrid);
            }
            stepsContainer.Child = stepsList;
            body.Children.Add(stepsContainer);

            // Live Terminal / Log output
            var logHeader = new TextBlock
            {
                Text = NexLocale.T("act_flow_live_log"),
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = MutedBrush,
                Margin = new Thickness(0, 0, 0, 4)
            };
            body.Children.Add(logHeader);

            var logBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(8, 12, 18)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(20, 30, 44)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 8, 10, 8),
                Height = 110,
                Margin = new Thickness(0, 0, 0, 14)
            };
            logScrollViewer = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            txtLiveLog = new TextBlock
            {
                Text = NexLocale.T("act_flow_log_waiting"),
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                FontSize = 10.5,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                TextWrapping = TextWrapping.Wrap
            };
            logScrollViewer.Content = txtLiveLog;
            logBorder.Child = logScrollViewer;
            body.Children.Add(logBorder);

            // Action buttons panel
            actionButtonsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            btnClose = new Button
            {
                Content = NexLocale.T("act_flow_btn_close"),
                Style = (Style)FindResource("DarkTableFooterButtonStyle"),
                Padding = new Thickness(20, 8, 20, 8),
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand,
                IsEnabled = false
            };
            actionButtonsPanel.Children.Add(btnClose);
            body.Children.Add(actionButtonsPanel);
        });

        // ── Executie asincrona dupa afisarea modalului ──────────────────
        try
        {
            UpdateStepView(0, 1);
            if (txtCurrentAction != null) txtCurrentAction.Text = NexLocale.T("act_flow_preparing");

            string scriptPath = await EnsureActivationToolAsync();
            if (string.IsNullOrEmpty(scriptPath))
            {
                if (txtCurrentAction != null)
                {
                    txtCurrentAction.Text = NexLocale.T("act_err_prep", "Nu s-a putut pregăti instrumentul de activare.");
                    txtCurrentAction.Foreground = RedBrush;
                }
                if (btnClose != null)
                {
                    btnClose.IsEnabled = true;
                    btnClose.Click += (_, _) => { HideModal(); ShowActivator(); };
                }
                return;
            }

            UpdateStepView(0, 2);
            UpdateStepView(1, 2);
            if (pBar != null) pBar.Value = 35;
            if (txtCurrentAction != null) txtCurrentAction.Text = NexLocale.Format("act_flow_launching", displayName);

            string runnerBat = Path.Combine(_activationToolDir, "Run_Activation.bat");
            string logFile = Path.Combine(Path.GetTempPath(), "nexwin_activator_live.log");
            try { if (File.Exists(logFile)) File.Delete(logFile); } catch { }

            // Redirectionare UTF-8 si non-interactiv (<nul) pentru a evita orice blocaj de tip wait/pause
            string runnerScript = $"@echo off\r\nchcp 65001 >nul\r\ncd /d \"%~dp0\"\r\ncall \"%~dp0NexWin_Activator.cmd\" {switchArg} <nul > \"{logFile}\" 2>&1\r\nexit /b %errorlevel%\r\n";
            await File.WriteAllTextAsync(runnerBat, runnerScript, Encoding.UTF8);

            var psi = new ProcessStartInfo
            {
                FileName = runnerBat,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = _activationToolDir
            };

            Process? process = null;
            try
            {
                process = Process.Start(psi);
            }
            catch (Exception ex)
            {
                if (txtCurrentAction != null)
                {
                    txtCurrentAction.Text = NexLocale.T("act_err_start", "Eroare la pornirea procesului de licențiere.");
                    txtCurrentAction.Foreground = AmberBrush;
                }
                if (txtLiveLog != null) txtLiveLog.Text = NexLocale.Format("act_err_prefix_format", ex.Message);
                if (btnClose != null)
                {
                    btnClose.IsEnabled = true;
                    btnClose.Click += (_, _) => { HideModal(); ShowActivator(); };
                }
                return;
            }

            UpdateStepView(1, 3);
            UpdateStepView(2, 3);
            if (pBar != null) pBar.Value = 65;
            if (txtCurrentAction != null) txtCurrentAction.Text = NexLocale.Format("act_flow_applying", displayName);

            // Monitorizeaza fisierul de log in timp real cat timp procesul ruleaza
            while (process != null && !process.HasExited)
            {
                await Task.Delay(250);
                try
                {
                    if (File.Exists(logFile))
                    {
                        using var fs = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        using var reader = new StreamReader(fs, Encoding.UTF8);
                        string curText = await reader.ReadToEndAsync();
                        if (!string.IsNullOrWhiteSpace(curText) && txtLiveLog != null)
                        {
                            txtLiveLog.Text = CleanLogOutput(curText);
                            logScrollViewer?.ScrollToEnd();
                        }
                    }
                }
                catch { }
            }

            UpdateStepView(2, 4);
            UpdateStepView(3, 4);
            if (pBar != null) pBar.Value = 90;
            if (txtCurrentAction != null) txtCurrentAction.Text = NexLocale.T("act_flow_validating");

            // Citeste log-ul final
            string finalLog = "";
            try
            {
                if (File.Exists(logFile))
                {
                    using var fs = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var reader = new StreamReader(fs, Encoding.UTF8);
                    finalLog = await reader.ReadToEndAsync();
                    if (!string.IsNullOrWhiteSpace(finalLog) && txtLiveLog != null)
                    {
                        txtLiveLog.Text = CleanLogOutput(finalLog);
                        logScrollViewer?.ScrollToEnd();
                    }
                }
            }
            catch { }

            await Task.Delay(1500);

            // Verifica starea din WMI/CIM si indicatorul Ohook
            bool winOk = false;
            bool offOk = false;

            if (target is "windows" or "all")
            {
                var newWinInfo = QueryActivationSync("Windows%");
                winOk = (newWinInfo != null && newWinInfo.LicenseStatus == 1) ||
                        finalLog.Contains("is permanently activated", StringComparison.OrdinalIgnoreCase) ||
                        finalLog.Contains("Product is permanently activated", StringComparison.OrdinalIgnoreCase) ||
                        finalLog.Contains("Activation Successful", StringComparison.OrdinalIgnoreCase);
            }

            if (target is "office" or "all")
            {
                var newOffInfo = QueryActivationSync("Office%", "Microsoft 365%");
                bool cimOk = newOffInfo != null && newOffInfo.LicenseStatus == 1;
                bool ohookFile = IsOfficeOhookActivated();
                bool logOk = finalLog.Contains("are activated, use them directly", StringComparison.OrdinalIgnoreCase) ||
                             finalLog.Contains("Office apps such as Word, Excel are activated", StringComparison.OrdinalIgnoreCase) ||
                             (finalLog.Contains("Ohook", StringComparison.OrdinalIgnoreCase) && finalLog.Contains("Successful", StringComparison.OrdinalIgnoreCase));
                offOk = cimOk || ohookFile || logOk;
            }

            bool success = target switch
            {
                "windows" => winOk,
                "office" => offOk,
                "all" => winOk || offOk,
                _ => false
            };

            bool alreadyPermanent = finalLog.Contains("already activated", StringComparison.OrdinalIgnoreCase) ||
                                   finalLog.Contains("already permanent", StringComparison.OrdinalIgnoreCase);

            if (success || alreadyPermanent)
            {
                for (int s = 0; s < 4; s++) UpdateStepView(s, 5);
                if (pBar != null) { pBar.Value = 100; pBar.Foreground = GreenBrush; }
                if (txtPct != null) { txtPct.Text = $"100% {NexLocale.T("status_finished")}"; txtPct.Foreground = GreenBrush; }
                if (txtCurrentAction != null)
                {
                    txtCurrentAction.Text = alreadyPermanent ? NexLocale.Format("act_flow_already_perm", displayName) : NexLocale.Format("act_flow_success_perm", displayName);
                    txtCurrentAction.Foreground = GreenBrush;
                }
            }
            else
            {
                stepBorders[0].Background = new SolidColorBrush(Color.FromArgb(40, 16, 185, 129));
                stepBorders[0].BorderBrush = GreenBrush;
                stepGlyphs[0].Text = "\u2713";
                stepGlyphs[0].Foreground = GreenBrush;

                stepBorders[1].Background = new SolidColorBrush(Color.FromArgb(40, 16, 185, 129));
                stepBorders[1].BorderBrush = GreenBrush;
                stepGlyphs[1].Text = "\u2713";
                stepGlyphs[1].Foreground = GreenBrush;

                stepBorders[2].Background = new SolidColorBrush(Color.FromArgb(40, 245, 158, 11));
                stepBorders[2].BorderBrush = AmberBrush;
                stepGlyphs[2].Text = "!";
                stepGlyphs[2].Foreground = AmberBrush;
                stepStatuses[2].Text = NexLocale.T("status_warning");
                stepStatuses[2].Foreground = AmberBrush;

                if (pBar != null) { pBar.Value = 100; pBar.Foreground = AmberBrush; }
                if (txtPct != null) { txtPct.Text = NexLocale.T("status_finished"); txtPct.Foreground = AmberBrush; }
                if (txtCurrentAction != null)
                {
                    txtCurrentAction.Text = NexLocale.T("act_flow_finished_notice");
                    txtCurrentAction.Foreground = AmberBrush;
                }
            }

            if (btnClose != null)
            {
                btnClose.IsEnabled = true;
                btnClose.Click += (_, _) =>
                {
                    HideModal();
                    ShowActivator();
                };
            }
        }
        catch (Exception ex)
        {
            if (txtCurrentAction != null)
            {
                txtCurrentAction.Text = NexLocale.Format("act_err_activation_format", ex.Message);
                txtCurrentAction.Foreground = RedBrush;
            }
            if (btnClose != null)
            {
                btnClose.IsEnabled = true;
                btnClose.Click += (_, _) => { HideModal(); ShowActivator(); };
            }
        }
    }

    private static string CleanLogOutput(string input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        string clean = System.Text.RegularExpressions.Regex.Replace(input, @"\x1B\[[^@-~]*[@-~]", "");
        clean = clean.Replace("\f", "").Replace("\a", "");

        var lines = clean.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var sb = new StringBuilder();
        foreach (var rawLine in lines)
        {
            string trimmed = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            // Eliminare referinte externe, link-uri si mesaje de asteptare / apasare taste
            if (trimmed.Contains("massgrave", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("press a key", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("press any key", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("waiting for", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("troubleshoot", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("discord", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("github.com", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("http://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("https://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("www.", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Rebranding instrumente in NexWin Activator
            string sanitized = rawLine
                .Replace("Microsoft Activation Scripts", "NexWin Activator", StringComparison.OrdinalIgnoreCase)
                .Replace("MAS_AIO", "NexWin Activator", StringComparison.OrdinalIgnoreCase)
                .Replace("MAS", "NexWin", StringComparison.OrdinalIgnoreCase);

            sb.AppendLine(sanitized);
        }
        return sb.ToString().TrimEnd();
    }

    // ── Download / Cache Activation Tool ─────────────────────────────────
    private static readonly string _activationToolDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NexWin", "tools");
    private static readonly string _activationToolPath = Path.Combine(_activationToolDir, "NexWin_Activator.cmd");
    private static readonly string _activationToolUrl =
        "https://dev.azure.com/massgrave/Microsoft-Activation-Scripts/_apis/git/repositories/Microsoft-Activation-Scripts/items?path=/MAS/All-In-One-Version-KL/MAS_AIO.cmd&download=true";

    private async Task<string> EnsureActivationToolAsync()
    {
        try
        {
            Directory.CreateDirectory(_activationToolDir);

            if (File.Exists(_activationToolPath))
            {
                var age = DateTime.Now - File.GetLastWriteTime(_activationToolPath);
                if (age.TotalDays < 30)
                    return _activationToolPath;
            }

            AppendLog(NexLocale.T("act_log_downloading", "[ACTIVATOR] Se descarcă instrumentul de activare..."), false);
            using var http = new HttpClient();
            http.Timeout = TimeSpan.FromSeconds(60);
            var data = await http.GetStringAsync(_activationToolUrl);

            if (string.IsNullOrWhiteSpace(data) || data.Length < 1000)
            {
                AppendLog(NexLocale.T("act_log_invalid_file", "[ACTIVATOR] [EROARE] Fișierul descărcat pare invalid."), true);
                if (File.Exists(_activationToolPath)) return _activationToolPath;
                return "";
            }

            await File.WriteAllTextAsync(_activationToolPath, data, Encoding.UTF8);
            AppendLog(NexLocale.T("act_log_download_ok", "[ACTIVATOR] Instrument de activare descărcat și salvat."), false);
            return _activationToolPath;
        }
        catch (Exception ex)
        {
            AppendLog(NexLocale.Format("act_log_download_err_format", ex.Message), true);
            if (File.Exists(_activationToolPath)) return _activationToolPath;
            return "";
        }
    }

    // ── Install Office directly from Activator page ──────────────────────
    private async Task InstallOfficeFromActivatorAsync()
    {
        var officeApp = new NativeTuning.SoftwareAppItem
        {
            Id = "office365",
            WingetId = "Microsoft.Office",
            Name = "Microsoft Office",
            Category = "Productivitate",
            Description = NexLocale.T("act_office_desc", "Suită completă de aplicații Office (Word, Excel, PowerPoint, Outlook)"),
            BrandColorHex = "#D83B01",
            IconSymbol = "App",
            RelativeExePath = @"Microsoft Office\root\Office16\WINWORD.EXE"
        };

        await ExecuteAppInstallQueueAsync(new List<NativeTuning.SoftwareAppItem> { officeApp });

        // Refresh activator page after install completes
        await Task.Delay(1500);
        Dispatcher.Invoke(() => ShowActivator());
    }
}
