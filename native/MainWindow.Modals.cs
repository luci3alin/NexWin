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
    // ================= LIVE TOAST NOTIFICATION SYSTEM =================
    private DispatcherTimer? toastAutoDismissTimer;

    private Action? currentToastAction;

    private void ShowToast(string title, string message, NexIcon? icon = null, Brush? color = null)
    {
        currentToastAction = null;
        bool isSuccess = icon == NexIcon.Check;
        bool isProgress = icon == NexIcon.Bolt || icon == NexIcon.Clean;
        ShowNotification(title, message, isProgress, isSuccess, false);
    }

    private void ShowToastWithAction(string title, string message, NexIcon icon, Brush color, string actionText, Action onAction)
    {
        currentToastAction = onAction;
        Dispatcher.Invoke(() =>
        {
            toastAutoDismissTimer?.Stop();
            ToastTitle.Text = title;
            ToastMessage.Text = message;
            ToastContainer.Visibility = Visibility.Visible;
            ToastReportBtn.Visibility = Visibility.Visible;

            var txt = (ToastReportBtn.Content as StackPanel)?.Children.OfType<TextBlock>().FirstOrDefault();
            if (txt != null) txt.Text = actionText;

            ToastIconBox.Background = new SolidColorBrush(Color.FromArgb(45, 245, 158, 11));
            ToastIconHost.Content = CreateVectorIcon(icon, color, 14);
            ToastProgressBar.Visibility = Visibility.Collapsed;

            toastAutoDismissTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(12) };
            toastAutoDismissTimer.Tick += (_, _) =>
            {
                toastAutoDismissTimer.Stop();
                ToastContainer.Visibility = Visibility.Collapsed;
            };
            toastAutoDismissTimer.Start();
        });
    }

    private void LogNav(string text) => AppendLog(text, false);

    private void ShowNotification(string title, string message, bool isProgress = false, bool isSuccess = false, bool hasReportAction = false)
    {
        Dispatcher.Invoke(() =>
        {
            toastAutoDismissTimer?.Stop();
            ToastTitle.Text = title;
            ToastMessage.Text = message;
            ToastContainer.Visibility = Visibility.Visible;
            ToastReportBtn.Visibility = hasReportAction ? Visibility.Visible : Visibility.Collapsed;

            var txt = (ToastReportBtn.Content as StackPanel)?.Children.OfType<TextBlock>().FirstOrDefault();
            if (txt != null && hasReportAction) txt.Text = NexLocale.T("modal_toast_view_steps", "Vezi pașii aplicați");

            if (isSuccess)
            {
                ToastIconBox.Background = new SolidColorBrush(Color.FromArgb(45, 16, 185, 129));
                ToastIconHost.Content = CreateVectorIcon(NexIcon.Check, GreenBrush, 14);
                ToastProgressBar.Visibility = Visibility.Collapsed;
            }
            else if (isProgress)
            {
                ToastIconBox.Background = new SolidColorBrush(Color.FromArgb(45, 255, 42, 133));
                ToastIconHost.Content = CreateVectorIcon(NexIcon.Bolt, PinkBrush, 14);
                ToastProgressBar.Visibility = Visibility.Visible;
                ToastProgressBar.IsIndeterminate = true;
            }
            else
            {
                ToastIconBox.Background = new SolidColorBrush(Color.FromArgb(45, 56, 189, 248));
                ToastIconHost.Content = CreateVectorIcon(NexIcon.Info, CyanBrush, 14);
                ToastProgressBar.Visibility = Visibility.Collapsed;
            }

            if (!isProgress)
            {
                toastAutoDismissTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(hasReportAction ? 10 : 6) };
                toastAutoDismissTimer.Tick += (_, _) =>
                {
                    toastAutoDismissTimer.Stop();
                    ToastContainer.Visibility = Visibility.Collapsed;
                };
                toastAutoDismissTimer.Start();
            }
        });
    }

    private void UpdateNotificationProgress(string message)
    {
        Dispatcher.Invoke(() =>
        {
            ToastMessage.Text = message;
        });
    }

    private void HideNotification()
    {
        Dispatcher.Invoke(() =>
        {
            toastAutoDismissTimer?.Stop();
            ToastContainer.Visibility = Visibility.Collapsed;
        });
    }

    private void ToastDismiss_Click(object sender, RoutedEventArgs e)
    {
        HideNotification();
    }

    private void ToastReportBtn_Click(object sender, RoutedEventArgs e)
    {
        HideNotification();
        if (currentToastAction != null)
        {
            var act = currentToastAction;
            currentToastAction = null;
            act();
        }
        else
        {
            ShowOptimizationReportModal(lastReport);
        }
    }

    private void ReportNavButton_Click(object sender, RoutedEventArgs e)
    {
        ShowOptimizationReportModal(lastReport);
    }

    private void ShowOptimizationReportModal(OptimizationReport? report)
    {
        if (report == null)
        {
            ShowModal(NexLocale.T("modal_report_title"), body =>
            {
                body.Children.Add(new TextBlock
                {
                    Text = NexLocale.T("modal_report_empty"),
                    FontSize = 12.5,
                    Foreground = MutedBrush,
                    Margin = new Thickness(0, 0, 0, 16),
                    TextWrapping = TextWrapping.Wrap
                });
                var close = new Button { Content = NexLocale.T("btn_close"), Style = (Style)FindResource("SecondaryButtonStyle"), HorizontalAlignment = HorizontalAlignment.Right, MinWidth = 90 };
                close.Click += (_, _) => HideModal();
                body.Children.Add(close);
            });
            return;
        }

        ShowModal(string.Format(NexLocale.T("modal_report_title_format", "Raport: {0}"), report.SuiteName), body =>
        {
            // Status Header Banner
            var banner = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(30, 16, 185, 129)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(70, 16, 185, 129)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 0, 14)
            };
            var bannerGrid = new DockPanel { LastChildFill = true };
            var statusText = new StackPanel();
            statusText.Children.Add(new TextBlock
            {
                Text = NexLocale.T("modal_report_all_success"),
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = GreenBrush
            });
            statusText.Children.Add(new TextBlock
            {
                Text = $"{report.Timestamp:HH:mm:ss}  ·  " + string.Format(NexLocale.T("modal_report_duration_format", "Durată totală: {0} ms  ·  {1} operațiuni aplicate"), report.TotalDurationMs, report.Steps.Count),
                FontSize = 11,
                Foreground = MutedBrush,
                Margin = new Thickness(0, 2, 0, 0)
            });
            bannerGrid.Children.Add(statusText);
            banner.Child = bannerGrid;
            body.Children.Add(banner);

            // Steps scroll viewer
            var scroll = new ScrollViewer
            {
                MaxHeight = 360,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(0, 0, 0, 16)
            };
            var stepsList = new StackPanel();

            foreach (var step in report.Steps)
            {
                var stepBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(15, 23, 34)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(28, 42, 58)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(12, 9, 12, 9),
                    Margin = new Thickness(0, 0, 0, 6)
                };

                var stepGrid = new Grid();
                stepGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
                stepGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                stepGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                // Checkmark icon
                var chk = CreateVectorIcon(NexIcon.Check, GreenBrush, 14);
                chk.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(chk, 0);
                stepGrid.Children.Add(chk);

                // Title & detail
                var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                texts.Children.Add(new TextBlock
                {
                    Text = step.Title,
                    FontSize = 12.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = TextBrush
                });
                if (!string.IsNullOrEmpty(step.Details))
                {
                    texts.Children.Add(new TextBlock
                    {
                        Text = step.Details,
                        FontSize = 11,
                        Foreground = MutedBrush,
                        Margin = new Thickness(0, 2, 0, 0)
                    });
                }
                Grid.SetColumn(texts, 1);
                stepGrid.Children.Add(texts);

                // Status chip
                var statusChip = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(30, 16, 185, 129)),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 2, 6, 2),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock { Text = NexLocale.T("common_success", "Reușit"), FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = GreenBrush }
                };
                Grid.SetColumn(statusChip, 2);
                stepGrid.Children.Add(statusChip);

                stepBorder.Child = stepGrid;
                stepsList.Children.Add(stepBorder);
            }

            scroll.Content = stepsList;
            body.Children.Add(scroll);

            // Post-Optimization Appreciation & Community Goal CTA Banner
            var goalInfo = NativeTuning.GetCurrentCommunityGoal();
            var supportBanner = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(11, 24, 38)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(22, 82, 74)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 0, 12)
            };
            var sbGrid = new Grid();
            sbGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            sbGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var sbTextStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
            sbTextStack.Children.Add(new TextBlock
            {
                Text = NexLocale.T("support_goal_report_title", "Ți-a fost utilă această optimizare? NexWin este 100% gratuit."),
                FontSize = 11.5,
                FontWeight = FontWeights.Bold,
                Foreground = TextBrush
            });
            sbTextStack.Children.Add(new TextBlock
            {
                Text = NexLocale.Format("support_goal_report_desc", goalInfo.Percentage),
                FontSize = 10.5,
                Foreground = MutedBrush,
                Margin = new Thickness(0, 2, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });
            Grid.SetColumn(sbTextStack, 0);
            sbGrid.Children.Add(sbTextStack);

            var sbBtn = new Button
            {
                Content = NexLocale.T("support_goal_report_btn", "Susține Proiectul"),
                Style = (Style)FindResource("SecondaryButtonStyle"),
                Foreground = GreenBrush,
                BorderBrush = new SolidColorBrush(Color.FromArgb(110, 16, 185, 129)),
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Height = 30,
                Padding = new Thickness(12, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand
            };
            sbBtn.Click += (_, _) =>
            {
                HideModal();
                ShowSupportProjectModal();
            };
            Grid.SetColumn(sbBtn, 1);
            sbGrid.Children.Add(sbBtn);
            supportBanner.Child = sbGrid;
            body.Children.Add(supportBanner);

            // Bottom Actions
            var bottom = new DockPanel { LastChildFill = false };

            var viewLogBtn = new Button
            {
                Content = NexLocale.T("modal_report_view_detailed_log", "Deschide Jurnal Tehnic Detaliat"),
                Style = (Style)FindResource("SecondaryButtonStyle"),
                FontSize = 11,
                Height = 32,
                Padding = new Thickness(12, 0, 12, 0),
                Cursor = Cursors.Hand
            };
            viewLogBtn.Click += (_, _) =>
            {
                HideModal();
                SelectNav(null);
                ShowLogs();
            };
            DockPanel.SetDock(viewLogBtn, Dock.Left);
            bottom.Children.Add(viewLogBtn);

            var closeBtn = new Button
            {
                Content = NexLocale.T("btn_close", "Închide"),
                Style = (Style)FindResource("PrimaryGradientButtonStyle"),
                FontSize = 11.5,
                Height = 32,
                Padding = new Thickness(18, 0, 18, 0),
                MinWidth = 90,
                Cursor = Cursors.Hand
            };
            closeBtn.Click += (_, _) => HideModal();
            DockPanel.SetDock(closeBtn, Dock.Right);
            bottom.Children.Add(closeBtn);

            body.Children.Add(bottom);
        });
    }

    private async Task RunDnsBenchmarkAndShowModalAsync()
    {
        ShowNotification(NexLocale.T("modal_dns_notif_bench_title", "Benchmark DNS în curs..."), NexLocale.T("modal_dns_notif_bench_msg", "Măsurare timp de răspuns către Cloudflare, Google și Quad9..."), isProgress: true);
        var results = await NativeTuning.RunDnsBenchmarkAsync();
        HideNotification();
        ShowDnsBenchmarkResultsModal(results);
    }

    private void ShowDnsBenchmarkResultsModal(List<NativeTuning.DnsBenchmarkItem> results)
    {
        ShowModal(NexLocale.T("modal_dns_title", "Rezultate Benchmark DNS"), body =>
        {
            body.Children.Add(new TextBlock
            {
                Text = NexLocale.T("modal_dns_desc", "Latențele au fost măsurate în timp real. Poți activa cel mai rapid server DNS cu un singur clic:"),
                FontSize = 12.5,
                Foreground = MutedBrush,
                Margin = new Thickness(0, 0, 0, 16),
                TextWrapping = TextWrapping.Wrap
            });

            foreach (var item in results)
            {
                var border = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(15, 23, 34)),
                    BorderBrush = item.IsFastest ? AmberBrush : new SolidColorBrush(Color.FromRgb(28, 42, 58)),
                    BorderThickness = new Thickness(item.IsFastest ? 1.5 : 1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(14, 12, 14, 12),
                    Margin = new Thickness(0, 0, 0, 10)
                };

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                // Provider Info
                var infoStack = new StackPanel();
                var titleStack = new StackPanel { Orientation = Orientation.Horizontal };
                titleStack.Children.Add(new TextBlock
                {
                    Text = item.Name,
                    FontSize = 13.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = TextBrush,
                    VerticalAlignment = VerticalAlignment.Center
                });

                if (item.IsFastest)
                {
                    var fastBadge = new Border
                    {
                        Background = new SolidColorBrush(Color.FromArgb(40, 245, 158, 11)),
                        BorderBrush = AmberBrush,
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(6, 2, 6, 2),
                        Margin = new Thickness(10, 0, 0, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    var badgeStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                    var bolt = CreateVectorIcon(NexIcon.Bolt, AmberBrush, 10);
                    bolt.Margin = new Thickness(0, 0, 4, 0);
                    badgeStack.Children.Add(bolt);
                    badgeStack.Children.Add(new TextBlock
                    {
                        Text = NexLocale.T("modal_dns_fastest", "Cel mai rapid"),
                        FontSize = 10,
                        FontWeight = FontWeights.Bold,
                        Foreground = AmberBrush,
                        VerticalAlignment = VerticalAlignment.Center
                    });
                    fastBadge.Child = badgeStack;
                    titleStack.Children.Add(fastBadge);
                }
                infoStack.Children.Add(titleStack);

                infoStack.Children.Add(new TextBlock
                {
                    Text = NexLocale.Format("modal_dns_bench_ip_format", item.Primary, item.Secondary, item.Tag),
                    FontSize = 11,
                    Foreground = MutedBrush,
                    Margin = new Thickness(0, 3, 0, 0)
                });
                Grid.SetColumn(infoStack, 0);
                grid.Children.Add(infoStack);

                // Latency Badge
                var latColor = item.LatencyMs < 25 ? GreenBrush : (item.LatencyMs < 60 ? CyanBrush : AmberBrush);
                var latencyBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(25, 255, 255, 255)),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(10, 4, 10, 4),
                    Margin = new Thickness(12, 0, 12, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock
                    {
                        Text = item.LatencyMs >= 900 ? "Timeout" : $"{item.LatencyMs:0} ms",
                        FontSize = 13,
                        FontWeight = FontWeights.Bold,
                        Foreground = latColor
                    }
                };
                Grid.SetColumn(latencyBorder, 1);
                grid.Children.Add(latencyBorder);

                // Apply Button
                var applyBtn = new Button
                {
                    Content = NexLocale.T("modal_dns_btn_apply", "Aplică acest DNS"),
                    Style = item.IsFastest ? (Style)FindResource("PrimaryGradientButtonStyle") : (Style)FindResource("SecondaryButtonStyle"),
                    Height = 32,
                    Width = 140,
                    Padding = new Thickness(12, 0, 12, 0),
                    FontSize = 11.5,
                    Cursor = Cursors.Hand,
                    VerticalAlignment = VerticalAlignment.Center
                };
                applyBtn.Click += async (_, _) =>
                {
                    HideModal();
                    ShowNotification(NexLocale.T("modal_dns_notif_setting_title", "Configurare DNS..."), string.Format(NexLocale.T("modal_dns_notif_setting_msg", "Setare {0} ({1})..."), item.Name, item.Primary), isProgress: true);
                    var ok = await NativeTuning.SetDnsProviderAsync(item.Primary, item.Secondary);
                    if (ok)
                    {
                        ShowNotification(NexLocale.T("modal_dns_notif_done_title", "DNS Configurat!"), string.Format(NexLocale.T("modal_dns_notif_done_msg", "Sistemul folosește acum {0} ({1})."), item.Name, item.Primary), isSuccess: true);
                    }
                    else
                    {
                        ShowNotification(NexLocale.T("modal_dns_notif_warn_title", "Avertisment DNS"), NexLocale.T("modal_dns_notif_warn_msg", "Nu s-a putut actualiza adaptorul de rețea."), isProgress: false);
                    }
                };
                Grid.SetColumn(applyBtn, 2);
                grid.Children.Add(applyBtn);

                border.Child = grid;
                body.Children.Add(border);
            }

            // Bottom Actions
            var bottomBar = new DockPanel { Margin = new Thickness(0, 12, 0, 0), LastChildFill = false };

            var resetDhcpBtn = new Button
            {
                Style = (Style)FindResource("SecondaryButtonStyle"),
                FontSize = 11,
                Height = 32,
                Padding = new Thickness(12, 0, 12, 0),
                Cursor = Cursors.Hand
            };
            var resetStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var revIcon = CreateVectorIcon(NexIcon.Revert, MutedBrush, 12);
            revIcon.Margin = new Thickness(0, 0, 6, 0);
            resetStack.Children.Add(revIcon);
            resetStack.Children.Add(new TextBlock { Text = NexLocale.T("modal_dns_btn_dhcp", "Resetează la DHCP (Implicit)"), VerticalAlignment = VerticalAlignment.Center });
            resetDhcpBtn.Content = resetStack;
            resetDhcpBtn.Click += async (_, _) =>
            {
                HideModal();
                ShowNotification(NexLocale.T("modal_dns_notif_reset_title", "Resetare DNS..."), NexLocale.T("modal_dns_notif_reset_msg", "Revenire la atribuire automată DHCP..."), isProgress: true);
                var ok = await NativeTuning.ResetDnsToDhcpAsync();
                ShowNotification(NexLocale.T("modal_dns_notif_reset_done_title", "DNS Resetat!"), NexLocale.T("modal_dns_notif_reset_done_msg", "Adaptorul de rețea utilizează acum DHCP-ul automat al furnizorului."), isSuccess: true);
            };
            DockPanel.SetDock(resetDhcpBtn, Dock.Left);
            bottomBar.Children.Add(resetDhcpBtn);

            var closeBtn = new Button
            {
                Content = NexLocale.T("btn_close", "Închide"),
                Style = (Style)FindResource("SecondaryButtonStyle"),
                FontSize = 11.5,
                Height = 32,
                Padding = new Thickness(16, 0, 16, 0),
                MinWidth = 90,
                Cursor = Cursors.Hand
            };
            closeBtn.Click += (_, _) => HideModal();
            DockPanel.SetDock(closeBtn, Dock.Right);
            bottomBar.Children.Add(closeBtn);

            body.Children.Add(bottomBar);
        });
    }

    // ================= MODALS & CONFIGURATION DRAWERS =================
    private TaskCompletionSource<bool>? activeModalTcs;

    private void ShowModal(string title, Action<StackPanel> buildContent)
    {
        Dispatcher.Invoke(() =>
        {
            ModalContent.Children.Clear();

            var header = new Grid { Margin = new Thickness(0, 0, 0, 18) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });

            var titleBlock = new TextBlock
            {
                Text = title,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = TextBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(titleBlock, 0);
            header.Children.Add(titleBlock);

            var closeBtn = new Button
            {
                Content = "×",
                Style = (Style)FindResource("ChromeCloseButtonStyle"),
                Width = 32,
                Height = 28,
                FontSize = 18,
                FontWeight = FontWeights.Bold
            };
            closeBtn.Click += (_, _) => HideModal();
            Grid.SetColumn(closeBtn, 1);
            header.Children.Add(closeBtn);

            ModalContent.Children.Add(header);
            buildContent(ModalContent);
            ModalOverlay.Visibility = Visibility.Visible;
        });
    }

    private void HideModal()
    {
        Dispatcher.Invoke(() =>
        {
            ModalOverlay.Visibility = Visibility.Collapsed;
            ModalContent.Children.Clear();
            var pending = activeModalTcs;
            activeModalTcs = null;
            pending?.TrySetResult(false);
        });
    }

    private Task<bool> ShowConfirmModalAsync(string title, string message, Brush? accent = null, string? confirmText = null, string? cancelText = null)
    {
        var tcs = new TaskCompletionSource<bool>();
        activeModalTcs = tcs;
        string actualConfirm = confirmText ?? NexLocale.T("btn_continue", "Continuă");
        string actualCancel = cancelText ?? NexLocale.T("btn_cancel", "Renunță");

        ShowModal(title, body =>
        {
            var iconCircle = new Border
            {
                Width = 48,
                Height = 48,
                CornerRadius = new CornerRadius(24),
                Background = new SolidColorBrush(Color.FromArgb(35, 245, 158, 11)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 16),
                Child = CreateVectorIcon(NexIcon.Warning, accent ?? AmberBrush, 22)
            };
            body.Children.Add(iconCircle);

            body.Children.Add(new TextBlock
            {
                Text = message,
                FontSize = 13.5,
                Foreground = new SolidColorBrush(Color.FromRgb(215, 228, 242)),
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(12, 0, 12, 24),
                LineHeight = 22
            });

            var btnRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var cancelBtn = new Button
            {
                Content = actualCancel,
                Style = (Style)FindResource("SecondaryButtonStyle"),
                MinWidth = 120,
                Margin = new Thickness(0, 0, 12, 0),
                Cursor = Cursors.Hand
            };
            cancelBtn.Click += (_, _) =>
            {
                activeModalTcs = null;
                tcs.TrySetResult(false);
                HideModal();
            };

            var confirmBtn = new Button
            {
                Content = actualConfirm,
                Style = (Style)FindResource("PrimaryGradientButtonStyle"),
                MinWidth = 140,
                Cursor = Cursors.Hand
            };
            confirmBtn.Click += (_, _) =>
            {
                activeModalTcs = null;
                tcs.TrySetResult(true);
                HideModal();
            };

            btnRow.Children.Add(cancelBtn);
            btnRow.Children.Add(confirmBtn);
            body.Children.Add(btnRow);
        });

        return tcs.Task;
    }

    private Task ShowAlertModalAsync(string title, string message, Brush? accent = null, string? okText = null)
    {
        var tcs = new TaskCompletionSource<bool>();
        activeModalTcs = tcs;
        string actualOk = okText ?? NexLocale.T("btn_understood", "Am înțeles");

        ShowModal(title, body =>
        {
            var iconCircle = new Border
            {
                Width = 48,
                Height = 48,
                CornerRadius = new CornerRadius(24),
                Background = new SolidColorBrush(Color.FromArgb(35, 56, 189, 248)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 16),
                Child = CreateVectorIcon(NexIcon.Info, accent ?? CyanBrush, 22)
            };
            body.Children.Add(iconCircle);

            body.Children.Add(new TextBlock
            {
                Text = message,
                FontSize = 13.5,
                Foreground = new SolidColorBrush(Color.FromRgb(215, 228, 242)),
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(12, 0, 12, 24),
                LineHeight = 22
            });

            var okBtn = new Button
            {
                Content = actualOk,
                Style = (Style)FindResource("PrimaryGradientButtonStyle"),
                MinWidth = 130,
                HorizontalAlignment = HorizontalAlignment.Center,
                Cursor = Cursors.Hand
            };
            okBtn.Click += (_, _) =>
            {
                activeModalTcs = null;
                tcs.TrySetResult(true);
                HideModal();
            };
            body.Children.Add(okBtn);
        });

        return tcs.Task;
    }

    private void ModalOverlay_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource == ModalOverlay) HideModal();
    }

    private void ModalBox_MouseDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }

    private (Border Container, CheckBox CheckBox) MakeCheckbox(string title, string subtitle, bool isChecked)
    {
        var cb = new CheckBox
        {
            IsChecked = isChecked,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 2, 12, 0)
        };

        var textPanel = new StackPanel();
        textPanel.Children.Add(new TextBlock { Text = title, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = TextBrush });
        textPanel.Children.Add(new TextBlock { Text = subtitle, FontSize = 11, Foreground = MutedBrush, Margin = new Thickness(0, 2, 0, 0) });

        var dock = new DockPanel();
        dock.Children.Add(cb);
        dock.Children.Add(textPanel);

        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(15, 23, 34)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(28, 42, 58)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 0, 8),
            Child = dock
        };
        return (border, cb);
    }

    private void ShowOneClickBoostModal()
    {
        ShowModal(NexLocale.T("modal_boost_title"), body =>
        {
            body.Children.Add(new TextBlock
            {
                Text = NexLocale.T("modal_boost_desc"),
                FontSize = 12.5,
                Foreground = MutedBrush,
                Margin = new Thickness(0, 0, 0, 16),
                TextWrapping = TextWrapping.Wrap
            });

            var cbRestore = MakeCheckbox(NexLocale.T("modal_boost_cb_restore"), NexLocale.T("modal_boost_cb_restore_desc"), true);
            var cbAi = MakeCheckbox(NexLocale.T("modal_boost_cb_ai"), NexLocale.T("modal_boost_cb_ai_desc"), true);
            var cbGaming = MakeCheckbox(NexLocale.T("modal_boost_cb_gaming"), NexLocale.T("modal_boost_cb_gaming_desc"), true);
            var cbDebloat = MakeCheckbox(NexLocale.T("modal_boost_cb_debloat"), NexLocale.T("modal_boost_cb_debloat_desc"), true);
            var cbMaintenance = MakeCheckbox(NexLocale.T("modal_boost_cb_maint"), NexLocale.T("modal_boost_cb_maint_desc"), true);

            body.Children.Add(cbRestore.Container);
            body.Children.Add(cbAi.Container);
            body.Children.Add(cbGaming.Container);
            body.Children.Add(cbDebloat.Container);
            body.Children.Add(cbMaintenance.Container);

            var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
            var cancelBtn = new Button { Content = NexLocale.T("btn_cancel"), Style = (Style)FindResource("SecondaryButtonStyle"), Margin = new Thickness(0, 0, 10, 0), MinWidth = 90, Height = 34 };
            cancelBtn.Click += (_, _) => HideModal();
            actions.Children.Add(cancelBtn);

            var applyBtn = new Button { Style = (Style)FindResource("PrimaryGradientButtonStyle"), Width = 160, Height = 34, Cursor = Cursors.Hand };
            var applyStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var boltIcon = CreateVectorIcon(NexIcon.Bolt, Brushes.White, 13);
            boltIcon.Margin = new Thickness(0, 0, 6, 0);
            applyStack.Children.Add(boltIcon);
            applyStack.Children.Add(new TextBlock { Text = NexLocale.T("modal_boost_btn_apply"), FontWeight = FontWeights.SemiBold, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
            applyBtn.Content = applyStack;
            applyBtn.Click += async (_, _) =>
            {
                HideModal();
                await RunOneClickBoostAsync(
                    cbRestore.CheckBox.IsChecked == true,
                    cbAi.CheckBox.IsChecked == true,
                    cbGaming.CheckBox.IsChecked == true,
                    cbDebloat.CheckBox.IsChecked == true,
                    cbMaintenance.CheckBox.IsChecked == true);
            };
            actions.Children.Add(applyBtn);
            body.Children.Add(actions);
        });
    }

    private void ShowGamingModal()
    {
        ShowModal(NexLocale.T("modal_gaming_title", "Configurare Profil Gaming"), body =>
        {
            body.Children.Add(new TextBlock
            {
                Text = NexLocale.T("modal_gaming_desc", "Optimizări pentru reducerea latenței, stabilizarea framerate-ului și prioritate GPU."),
                FontSize = 12.5,
                Foreground = MutedBrush,
                Margin = new Thickness(0, 0, 0, 16),
                TextWrapping = TextWrapping.Wrap
            });

            var cbPower = MakeCheckbox(NexLocale.T("modal_gaming_cb_power", "Plan de alimentare High Performance"), NexLocale.T("modal_gaming_cb_power_desc", "Previne intrarea nucleelor în stări inactive de economisire energie."), true);
            var cbGpu = MakeCheckbox(NexLocale.T("modal_gaming_cb_gpu", "Optimizare GPU & Protecție drivere"), NexLocale.T("modal_gaming_cb_gpu_desc", "Previne suprascrierea driverului video NVIDIA prin Windows Update."), true);
            var cbNetwork = MakeCheckbox(NexLocale.T("modal_gaming_cb_nagle", "Dezactivare algoritm Nagle (TCP NoDelay)"), NexLocale.T("modal_gaming_cb_nagle_desc", "Trimite pachetele de rețea imediat, reducând ping-ul în jocuri multiplayer."), true);
            var cbGameMode = MakeCheckbox(NexLocale.T("modal_gaming_cb_gamemode", "Activare Game Mode Windows 11"), NexLocale.T("modal_gaming_cb_gamemode_desc", "Prioritizează resursele CPU/GPU către procesul jocului activ."), true);
            var cbVbs = MakeCheckbox(NexLocale.T("modal_gaming_cb_vbs", "Dezactivare VBS / Core Isolation (Opțional)"), NexLocale.T("modal_gaming_cb_vbs_desc", "Crește performanța în jocuri CPU-heavy. Necesită restart."), false);

            body.Children.Add(cbPower.Container);
            body.Children.Add(cbGpu.Container);
            body.Children.Add(cbNetwork.Container);
            body.Children.Add(cbGameMode.Container);
            body.Children.Add(cbVbs.Container);

            var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
            var cancelBtn = new Button { Content = NexLocale.T("btn_cancel", "Anulează"), Style = (Style)FindResource("SecondaryButtonStyle"), Margin = new Thickness(0, 0, 10, 0), MinWidth = 90, Height = 34 };
            cancelBtn.Click += (_, _) => HideModal();
            actions.Children.Add(cancelBtn);

            var applyBtn = new Button { Style = (Style)FindResource("PrimaryGradientButtonStyle"), Width = 180, Height = 34, Cursor = Cursors.Hand };
            var gameStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var gameIcon = CreateVectorIcon(NexIcon.Gamepad, Brushes.White, 13);
            gameIcon.Margin = new Thickness(0, 0, 6, 0);
            gameStack.Children.Add(gameIcon);
            gameStack.Children.Add(new TextBlock { Text = NexLocale.T("modal_gaming_btn_apply", "Aplică profil gaming"), FontWeight = FontWeights.SemiBold, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
            applyBtn.Content = gameStack;
            applyBtn.Click += async (_, _) =>
            {
                HideModal();
                await ExecuteNativeSuiteAsync(
                    NexLocale.T("modal_gaming_custom_title", "Profil Gaming Personalizat"),
                    NexLocale.T("modal_gaming_custom_applying", "Aplicare opțiuni gaming selectate..."),
                    NexLocale.T("modal_gaming_custom_success", "Profilul gaming a fost aplicat cu succes."),
                    async () =>
                    {
                        var logs = new List<string>();
                        if (cbGameMode.CheckBox.IsChecked == true)
                        {
                            NativeTuning.SetRegistryDword(RegistryHive.CurrentUser, @"Software\Microsoft\GameBar", "AutoGameModeEnabled", 1);
                            NativeTuning.SetRegistryDword(RegistryHive.CurrentUser, @"Software\Microsoft\GameBar", "AllowAutoGameMode", 1);
                            logs.Add(NexLocale.T("modal_gaming_log_gamemode", "Windows 11 Game Mode activat."));
                        }
                        if (cbGpu.CheckBox.IsChecked == true)
                        {
                            NativeTuning.SetRegistryDword(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 2);
                            logs.Add(NexLocale.T("modal_gaming_log_gpu", "GPU HAGS activat."));
                        }
                        if (cbNetwork.CheckBox.IsChecked == true)
                        {
                            var count = NativeTuning.ConfigureTcpNoDelay(true);
                            logs.Add(string.Format(NexLocale.T("modal_gaming_log_nodelay_format", "TCP NoDelay configurat pe {0} adaptoare rețea."), count));
                        }
                        if (cbPower.CheckBox.IsChecked == true)
                        {
                            await NativeTuning.SetPowerSchemeAsync(true);
                            logs.Add(NexLocale.T("modal_gaming_log_power", "Planul de alimentare High Performance activat."));
                        }
                        if (cbVbs.CheckBox.IsChecked == true)
                        {
                            NativeTuning.SetRegistryDword(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\DeviceGuard", "EnableVirtualizationBasedSecurity", 0);
                            NativeTuning.SetRegistryDword(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity", "Enabled", 0);
                            logs.Add(NexLocale.T("modal_gaming_log_vbs", "VBS & HVCI dezactivate. Necesită restart."));
                        }
                        return logs;
                    });
            };
            actions.Children.Add(applyBtn);
            body.Children.Add(actions);
        });
    }

    private void ShowSnapshotModal()
    {
        ShowModal(NexLocale.T("modal_snap_title", "Manager Snapshot-uri & Rollback"), body =>
        {
            body.Children.Add(new TextBlock
            {
                Text = NexLocale.T("modal_snap_desc", "Salvează starea curentă a registrilor și serviciilor pentru a putea reveni oricând în siguranță."),
                FontSize = 12.5,
                Foreground = MutedBrush,
                Margin = new Thickness(0, 0, 0, 16),
                TextWrapping = TextWrapping.Wrap
            });

            var cardSnap = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(15, 23, 34)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(28, 42, 58)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 14),
                Child = new StackPanel
                {
                    Children =
                    {
                        new TextBlock { Text = NexLocale.T("modal_snap_active", "Snapshot curent activ"), FontSize = 13.5, FontWeight = FontWeights.SemiBold, Foreground = TextBrush },
                        new TextBlock { Text = NexLocale.T("modal_snap_active_desc", "Stochează setările Windows, serviciile optimizate și starea registrelor."), FontSize = 11.5, Foreground = MutedBrush, Margin = new Thickness(0, 4, 0, 0) }
                    }
                }
            };
            body.Children.Add(cardSnap);

            var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            var createBtn = new Button { Style = (Style)FindResource("SecondaryButtonStyle"), Margin = new Thickness(0, 0, 10, 0), Width = 180, Height = 34, Cursor = Cursors.Hand };
            var createStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var snapIcon = CreateVectorIcon(NexIcon.Layers, CyanBrush, 13);
            snapIcon.Margin = new Thickness(0, 0, 6, 0);
            createStack.Children.Add(snapIcon);
            createStack.Children.Add(new TextBlock { Text = NexLocale.T("modal_snap_btn_save", "Salvează snapshot nou"), FontSize = 12, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
            createBtn.Content = createStack;
            createBtn.Click += async (_, _) =>
            {
                HideModal();
                await RunSafeAsync("Save-NexWinSnapshot.ps1");
            };
            actions.Children.Add(createBtn);

            var restoreBtn = new Button { Style = (Style)FindResource("PrimaryGradientButtonStyle"), Width = 180, Height = 34, Cursor = Cursors.Hand };
            var restoreStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var restIcon = CreateVectorIcon(NexIcon.Revert, Brushes.White, 13);
            restIcon.Margin = new Thickness(0, 0, 6, 0);
            restoreStack.Children.Add(restIcon);
            restoreStack.Children.Add(new TextBlock { Text = NexLocale.T("modal_snap_btn_restore", "Restaurează snapshot"), FontSize = 12, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
            restoreBtn.Content = restoreStack;
            restoreBtn.Click += async (_, _) =>
            {
                HideModal();
                await ConfirmAndRunAsync(NexLocale.T("modal_snap_confirm_restore_title", "Restaurare snapshot"), NexLocale.T("modal_snap_confirm_restore_msg", "Vor fi restaurate setările salvate în ultimul snapshot."), "Restore-NexWinSnapshot.ps1");
            };
            actions.Children.Add(restoreBtn);

            body.Children.Add(actions);
        });
    }

    private void Notifications_Click(object sender, RoutedEventArgs e)
    {
        ShowNotificationsModal();
    }

    public void ShowNotificationsModal()
    {
        ShowModal(NexLocale.T("notif_center_title", "Centru de Notificări & Actualizări"), body =>
        {
            body.Children.Add(new TextBlock
            {
                Text = NexLocale.T("notif_center_sub", "Notificări privind actualizările NexWin și versiunile noi ale programelor instalate pe sistem."),
                FontSize = 11.5,
                Foreground = MutedBrush,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, -8, 0, 14)
            });

            // 1. NexWin Official In-Place Self-Updater Card
            bool hasNexWinUpdate = _nexwinSelfUpdateInfo?.IsUpdateAvailable == true;
            string latestNexWinVer = _nexwinSelfUpdateInfo?.LatestVersion ?? "1.0.85";

            var nexwinCard = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(12, 21, 34)),
                BorderBrush = hasNexWinUpdate ? CyanBrush : new SolidColorBrush(Color.FromRgb(28, 48, 74)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(14, 12, 14, 12),
                Margin = new Thickness(0, 0, 0, 16)
            };

            var nexwinOuterStack = new StackPanel();
            var nexwinGrid = new Grid();
            nexwinGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
            nexwinGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            nexwinGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var nexwinIconContainer = new Grid { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left };
            var nexwinIconBox = new Border
            {
                Width = 36,
                Height = 36,
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromArgb(28, 255, 42, 133)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(75, 56, 189, 248)),
                BorderThickness = new Thickness(1)
            };
            try
            {
                var logoImg = new Image
                {
                    Source = new BitmapImage(new Uri("pack://application:,,,/assets/logo_ribbon.png")),
                    Width = 24,
                    Height = 25,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                RenderOptions.SetBitmapScalingMode(logoImg, BitmapScalingMode.HighQuality);
                nexwinIconBox.Child = logoImg;
            }
            catch
            {
                nexwinIconBox.Child = CreateVectorIcon(NexIcon.Bolt, CyanBrush, 16);
            }
            nexwinIconContainer.Children.Add(nexwinIconBox);
            if (hasNexWinUpdate)
            {
                nexwinIconContainer.Children.Add(new Border
                {
                    Width = 8,
                    Height = 8,
                    CornerRadius = new CornerRadius(4),
                    Background = new SolidColorBrush(Color.FromRgb(239, 68, 68)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(12, 21, 34)),
                    BorderThickness = new Thickness(1),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, -2, -2, 0)
                });
            }
            Grid.SetColumn(nexwinIconContainer, 0);
            nexwinGrid.Children.Add(nexwinIconContainer);

            var nexwinTextStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 12, 0) };
            var nexwinTitleRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            nexwinTitleRow.Children.Add(new TextBlock
            {
                Text = "NexWin",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = TextBrush,
                VerticalAlignment = VerticalAlignment.Center
            });
            var nexwinVerBadge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(35, 16, 185, 129)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(85, 16, 185, 129)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 1, 6, 1),
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = hasNexWinUpdate ? $"v1.0.15 -> v{latestNexWinVer}" : "v1.0.15",
                    FontSize = 9.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = GreenBrush
                }
            };
            nexwinTitleRow.Children.Add(nexwinVerBadge);
            nexwinTextStack.Children.Add(nexwinTitleRow);

            var txtNexWinStatus = new TextBlock
            {
                Text = hasNexWinUpdate
                    ? $"Versiune nouă disponibilă: NexWin v{latestNexWinVer}"
                    : NexLocale.T("notif_remote_uptodate", "NexWin v1.0.15 este la zi"),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = hasNexWinUpdate ? CyanBrush : GreenBrush,
                Margin = new Thickness(0, 2, 0, 2)
            };
            nexwinTextStack.Children.Add(txtNexWinStatus);

            var txtNexWinDesc = new TextBlock
            {
                Text = hasNexWinUpdate
                    ? "Actualizare automată în-place: se descarcă și se aplică direct cu repornire automată, fără instalator manual."
                    : NexLocale.T("notif_remote_desc", "Actualizare automată integrată: când apare o versiune nouă NexWin, se instalează direct de aici cu un singur click."),
                FontSize = 10,
                Foreground = MutedBrush,
                TextWrapping = TextWrapping.Wrap
            };
            nexwinTextStack.Children.Add(txtNexWinDesc);
            Grid.SetColumn(nexwinTextStack, 1);
            nexwinGrid.Children.Add(nexwinTextStack);

            // Progress bar row for zero-touch self-update
            var selfUpgProgressPanel = new StackPanel { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 10, 0, 0) };
            var selfUpgProgressBar = new ProgressBar
            {
                Height = 6,
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Foreground = CyanBrush,
                Background = new SolidColorBrush(Color.FromRgb(18, 30, 46)),
                BorderThickness = new Thickness(0)
            };
            var selfUpgProgressLabel = new TextBlock
            {
                Text = "0%",
                FontSize = 10,
                Foreground = CyanBrush,
                Margin = new Thickness(0, 4, 0, 0)
            };
            selfUpgProgressPanel.Children.Add(selfUpgProgressBar);
            selfUpgProgressPanel.Children.Add(selfUpgProgressLabel);

            var checkNexWinBtn = new Button
            {
                Content = hasNexWinUpdate ? "Actualizează NexWin" : NexLocale.T("notif_remote_btn_check", "Verifică actualizări"),
                Style = (Style)FindResource(hasNexWinUpdate ? "PrimaryGradientButtonStyle" : "SecondaryButtonStyle"),
                Padding = new Thickness(12, 5, 12, 5),
                FontSize = 11,
                FontWeight = hasNexWinUpdate ? FontWeights.Bold : FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand
            };
            checkNexWinBtn.Click += async (_, _) =>
            {
                if (_nexwinSelfUpdateInfo?.IsUpdateAvailable == true && !string.IsNullOrWhiteSpace(_nexwinSelfUpdateInfo.DownloadUrl))
                {
                    checkNexWinBtn.IsEnabled = false;
                    selfUpgProgressPanel.Visibility = Visibility.Visible;
                    selfUpgProgressLabel.Text = "Descărcare pachet actualizare NexWin...";

                    bool ok = await NativeTuning.ExecuteNexWinSelfUpdateAsync(_nexwinSelfUpdateInfo.DownloadUrl, (pct, msg) =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            selfUpgProgressBar.Value = pct;
                            selfUpgProgressLabel.Text = msg;
                        });
                    });

                    if (ok)
                    {
                        await Task.Delay(500);
                        Application.Current.Shutdown();
                        return;
                    }
                }

                checkNexWinBtn.IsEnabled = false;
                var info = await NativeTuning.CheckNexWinSelfUpdateAsync();
                _nexwinSelfUpdateInfo = info;
                checkNexWinBtn.IsEnabled = true;

                if (info.IsUpdateAvailable)
                {
                    ShowNotificationsModal();
                }
                else
                {
                    ShowToast(
                        NexLocale.T("notif_remote_toast_title", "Actualizare NexWin"),
                        NexLocale.T("notif_remote_toast_msg", "Rulezi deja cea mai recentă versiune NexWin v1.0.15."),
                        NexIcon.Check,
                        GreenBrush);
                }
            };
            Grid.SetColumn(checkNexWinBtn, 2);
            nexwinGrid.Children.Add(checkNexWinBtn);

            nexwinOuterStack.Children.Add(nexwinGrid);
            nexwinOuterStack.Children.Add(selfUpgProgressPanel);
            nexwinCard.Child = nexwinOuterStack;
            body.Children.Add(nexwinCard);

            // 2. Updates Notifications Header
            var secHeaderGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            secHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            secHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var secTitleRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            if (cachedUpgradesList != null && cachedUpgradesList.Count > 0)
            {
                secTitleRow.Children.Add(new Border
                {
                    Width = 8,
                    Height = 8,
                    CornerRadius = new CornerRadius(4),
                    Background = new SolidColorBrush(Color.FromRgb(239, 68, 68)),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0)
                });
            }
            secTitleRow.Children.Add(new TextBlock
            {
                Text = NexLocale.T("notif_apps_section_title", "Actualizări Disponibile"),
                FontSize = 12.5,
                FontWeight = FontWeights.Bold,
                Foreground = TextBrush,
                VerticalAlignment = VerticalAlignment.Center
            });
            if (cachedUpgradesList != null && cachedUpgradesList.Count > 0)
            {
                secTitleRow.Children.Add(new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(35, 56, 189, 248)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(80, 56, 189, 248)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(7, 1, 7, 1),
                    Margin = new Thickness(8, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock
                    {
                        Text = $"{cachedUpgradesList.Count} programe",
                        FontSize = 9.5,
                        FontWeight = FontWeights.Bold,
                        Foreground = CyanBrush
                    }
                });
            }
            Grid.SetColumn(secTitleRow, 0);
            secHeaderGrid.Children.Add(secTitleRow);

            var secActions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var rescanBtn = new Button
            {
                Content = NexLocale.T("notif_apps_btn_rescan", "Scanează acum"),
                Style = (Style)FindResource("SecondaryButtonStyle"),
                Padding = new Thickness(10, 4, 10, 4),
                FontSize = 10.5,
                Cursor = Cursors.Hand
            };
            rescanBtn.Click += async (_, _) =>
            {
                if (isScanningUpgrades) return;
                isScanningUpgrades = true;
                ShowNotificationsModal();
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
                UpdateTopNotificationsBadge(cachedUpgradesList.Count);
                ShowNotificationsModal();
            };
            secActions.Children.Add(rescanBtn);

            if (cachedUpgradesList != null && cachedUpgradesList.Count > 1)
            {
                var updateAllBtn = new Button
                {
                    Content = NexLocale.T("notif_apps_btn_update_all", "Actualizează tot"),
                    Style = (Style)FindResource("PrimaryGradientButtonStyle"),
                    Padding = new Thickness(12, 4, 12, 4),
                    Margin = new Thickness(8, 0, 0, 0),
                    FontSize = 10.5,
                    Cursor = Cursors.Hand
                };
                updateAllBtn.Click += async (_, _) =>
                {
                    var allItems = cachedUpgradesList.Select(u => new NativeTuning.SoftwareAppItem
                    {
                        Id = u.Id,
                        WingetId = u.Id,
                        Name = u.Name,
                        Category = NexLocale.T("apps_cat_upgrade"),
                        Description = NexLocale.Format("apps_desc_upgrade_to", u.AvailableVersion)
                    }).ToList();
                    HideModal();
                    await ExecuteAppInstallQueueAsync(allItems);
                    cachedUpgradesList?.Clear();
                    UpdateTopNotificationsBadge(0);
                    if (activeNav == NavApps) ShowApps();
                };
                secActions.Children.Add(updateAllBtn);
            }

            Grid.SetColumn(secActions, 1);
            secHeaderGrid.Children.Add(secActions);
            body.Children.Add(secHeaderGrid);

            // 3. Notifications List Content
            if (isScanningUpgrades || cachedUpgradesList == null)
            {
                if (!isScanningUpgrades && cachedUpgradesList == null)
                {
                    isScanningUpgrades = true;
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var list = await NativeTuning.CheckForAppUpgradesDetailedAsync();
                            Dispatcher.Invoke(() =>
                            {
                                cachedUpgradesList = list;
                                isScanningUpgrades = false;
                                UpdateTopNotificationsBadge(list.Count);
                                if (ModalOverlay.Visibility == Visibility.Visible)
                                    ShowNotificationsModal();
                            });
                        }
                        catch
                        {
                            Dispatcher.Invoke(() =>
                            {
                                cachedUpgradesList = new List<NativeTuning.AppUpgradeDetail>();
                                isScanningUpgrades = false;
                                UpdateTopNotificationsBadge(0);
                                if (ModalOverlay.Visibility == Visibility.Visible)
                                    ShowNotificationsModal();
                            });
                        }
                    });
                }

                var scanCard = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(13, 21, 33)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(24, 38, 56)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(20, 22, 20, 22),
                    Margin = new Thickness(0, 0, 0, 12)
                };
                var scanStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                scanStack.Children.Add(CreateVectorIcon(NexIcon.Pulse, CyanBrush, 24));
                scanStack.Children.Add(new TextBlock
                {
                    Text = NexLocale.T("apps_scan_searching_title", "Se caută actualizări disponibile..."),
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = TextBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 10, 0, 4)
                });
                scanStack.Children.Add(new TextBlock
                {
                    Text = NexLocale.T("apps_scan_searching_desc", "Interogare pachete instalate prin winget..."),
                    FontSize = 10.5,
                    Foreground = MutedBrush,
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                scanCard.Child = scanStack;
                body.Children.Add(scanCard);
            }
            else if (cachedUpgradesList.Count == 0)
            {
                var emptyCard = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(13, 21, 33)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(24, 38, 56)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(20, 20, 20, 20),
                    Margin = new Thickness(0, 0, 0, 12)
                };
                var emptyStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                emptyStack.Children.Add(CreateVectorIcon(NexIcon.Check, GreenBrush, 24));
                emptyStack.Children.Add(new TextBlock
                {
                    Text = NexLocale.T("notif_apps_empty_title", "Nu există notificări de actualizare"),
                    FontSize = 12.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = GreenBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 8, 0, 3)
                });
                emptyStack.Children.Add(new TextBlock
                {
                    Text = NexLocale.T("notif_apps_empty_desc", "Toate programele monitorizate pe acest PC sunt actualizate la zi."),
                    FontSize = 10.5,
                    Foreground = MutedBrush,
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                emptyCard.Child = emptyStack;
                body.Children.Add(emptyCard);
            }
            else
            {
                var listStack = new StackPanel();
                foreach (var app in cachedUpgradesList)
                {
                    var itemBorder = new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(14, 23, 36)),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(30, 48, 72)),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(8),
                        Padding = new Thickness(12, 10, 12, 10),
                        Margin = new Thickness(0, 0, 0, 8),
                        Cursor = Cursors.Hand
                    };

                    itemBorder.MouseEnter += (_, _) =>
                    {
                        itemBorder.Background = new SolidColorBrush(Color.FromRgb(20, 34, 54));
                        itemBorder.BorderBrush = CyanBrush;
                    };
                    itemBorder.MouseLeave += (_, _) =>
                    {
                        itemBorder.Background = new SolidColorBrush(Color.FromRgb(14, 23, 36));
                        itemBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(30, 48, 72));
                    };

                    var itemGrid = new Grid();
                    itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
                    itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    // App Icon with red notification dot
                    var iconContainer = new Grid { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left };
                    var iconBox = new Border
                    {
                        Width = 30,
                        Height = 30,
                        CornerRadius = new CornerRadius(7),
                        Background = new SolidColorBrush(Color.FromArgb(32, 56, 189, 248)),
                        BorderBrush = new SolidColorBrush(Color.FromArgb(75, 56, 189, 248)),
                        BorderThickness = new Thickness(1)
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
                    iconContainer.Children.Add(iconBox);
                    iconContainer.Children.Add(new Border
                    {
                        Width = 7,
                        Height = 7,
                        CornerRadius = new CornerRadius(3.5),
                        Background = new SolidColorBrush(Color.FromRgb(239, 68, 68)),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(14, 23, 36)),
                        BorderThickness = new Thickness(1),
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Top,
                        Margin = new Thickness(0, -2, -2, 0)
                    });
                    Grid.SetColumn(iconContainer, 0);
                    itemGrid.Children.Add(iconContainer);

                    // App Name, Version Transition & Click Hint
                    var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 12, 0) };
                    var nameVerRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                    nameVerRow.Children.Add(new TextBlock
                    {
                        Text = app.Name,
                        FontSize = 12,
                        FontWeight = FontWeights.Bold,
                        Foreground = TextBrush,
                        VerticalAlignment = VerticalAlignment.Center
                    });

                    var verPill = new Border
                    {
                        Background = new SolidColorBrush(Color.FromArgb(32, 16, 185, 129)),
                        BorderBrush = new SolidColorBrush(Color.FromArgb(80, 16, 185, 129)),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(6, 1, 6, 1),
                        Margin = new Thickness(8, 0, 0, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        Child = new TextBlock
                        {
                            Text = $"{app.InstalledVersion} -> {app.AvailableVersion}",
                            FontSize = 9.5,
                            FontWeight = FontWeights.Bold,
                            Foreground = GreenBrush
                        }
                    };
                    nameVerRow.Children.Add(verPill);
                    infoStack.Children.Add(nameVerRow);

                    infoStack.Children.Add(new TextBlock
                    {
                        Text = NexLocale.T("notif_apps_item_hint", "Apasă pe notificare pentru a o deschide în Software Installer"),
                        FontSize = 10,
                        Foreground = CyanBrush,
                        Margin = new Thickness(0, 3, 0, 0)
                    });
                    Grid.SetColumn(infoStack, 1);
                    itemGrid.Children.Add(infoStack);

                    // Direct "Actualizează" button inside notification
                    var directUpgBtn = new Button
                    {
                        Style = (Style)FindResource("PrimaryGradientButtonStyle"),
                        Padding = new Thickness(12, 5, 12, 5),
                        VerticalAlignment = VerticalAlignment.Center,
                        Cursor = Cursors.Hand
                    };
                    var btnContent = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                    btnContent.Children.Add(CreateVectorIcon(NexIcon.Clean, Brushes.White, 12));
                    btnContent.Children.Add(new TextBlock
                    {
                        Text = NexLocale.T("notif_apps_btn_update", "Actualizează"),
                        FontSize = 11,
                        FontWeight = FontWeights.Bold,
                        Foreground = Brushes.White,
                        Margin = new Thickness(5, 0, 0, 0)
                    });
                    directUpgBtn.Content = btnContent;

                    directUpgBtn.Click += async (_, ev) =>
                    {
                        ev.Handled = true;
                        HideModal();
                        var singleItem = new NativeTuning.SoftwareAppItem
                        {
                            Id = app.Id,
                            WingetId = app.Id,
                            Name = app.Name,
                            Category = NexLocale.T("apps_cat_upgrade"),
                            Description = NexLocale.Format("apps_desc_upgrade_to", app.AvailableVersion)
                        };
                        await ExecuteAppInstallQueueAsync(new List<NativeTuning.SoftwareAppItem> { singleItem });
                        cachedUpgradesList?.Remove(app);
                        UpdateTopNotificationsBadge(cachedUpgradesList?.Count ?? 0);
                        if (activeNav == NavApps) ShowApps();
                    };
                    Grid.SetColumn(directUpgBtn, 2);
                    itemGrid.Children.Add(directUpgBtn);

                    // Clicking the notification card opens Software Installer -> Actualizări Disponibile with this app checked!
                    itemBorder.MouseLeftButtonUp += (_, ev) =>
                    {
                        if (ev.OriginalSource is DependencyObject dep && FindParent<Button>(dep) != null) return;
                        HideModal();
                        if (cachedUpgradesList != null)
                        {
                            foreach (var u in cachedUpgradesList)
                            {
                                u.IsSelected = string.Equals(u.Id, app.Id, StringComparison.OrdinalIgnoreCase);
                            }
                        }
                        NavigateTo("AppsUpdates");
                    };

                    itemBorder.Child = itemGrid;
                    listStack.Children.Add(itemBorder);
                }

                if (cachedUpgradesList.Count > 3)
                {
                    listStack.Margin = new Thickness(0, 0, 6, 0);
                }

                var scroll = new ScrollViewer
                {
                    MaxHeight = 310,
                    VerticalScrollBarVisibility = cachedUpgradesList.Count > 3 ? ScrollBarVisibility.Visible : ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    Content = listStack,
                    Margin = new Thickness(0, 0, 0, 12)
                };
                body.Children.Add(scroll);
            }

            var closeBtn = new Button
            {
                Content = NexLocale.T("btn_close", "Închide"),
                Style = (Style)FindResource("SecondaryButtonStyle"),
                HorizontalAlignment = HorizontalAlignment.Right,
                MinWidth = 95
            };
            closeBtn.Click += (_, _) => HideModal();
            body.Children.Add(closeBtn);
        });
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo("Settings");
    }


    private void UserProfile_Click(object sender, MouseButtonEventArgs e)
    {
        ShowModal(NexLocale.T("modal_user_info_title", "Informații Utilizator & Sistem"), body =>
        {
            var info = new[]
            {
                (NexLocale.T("modal_user_info_user", "Utilizator Windows"), Environment.UserName),
                (NexLocale.T("modal_user_info_pc", "Nume Computer"), Environment.MachineName),
                (NexLocale.T("modal_user_info_priv", "Statut Privilegii"), NexLocale.T("modal_user_info_priv_val", "Administrator / Rulare Elevată")),
                (NexLocale.T("modal_user_info_os", "Sistem de Operare"), "Microsoft Windows 11 Pro (Build 26200)"),
                (NexLocale.T("modal_user_info_app_ver", "Versiune Aplicație"), "NexWin v1.0.15 (Windows 11)")
            };

            foreach (var (title, desc) in info)
            {
                var card = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(15, 23, 34)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(28, 42, 58)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(12, 9, 12, 9),
                    Margin = new Thickness(0, 0, 0, 8),
                    Child = new DockPanel
                    {
                        Children =
                        {
                            new TextBlock { Text = title, FontSize = 12, Foreground = MutedBrush },
                            new TextBlock { Text = desc, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = TextBrush, HorizontalAlignment = HorizontalAlignment.Right }
                        }
                    }
                };
                body.Children.Add(card);
            }

            var closeBtn = new Button { Content = NexLocale.T("btn_close", "Închide"), Style = (Style)FindResource("SecondaryButtonStyle"), HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0), MinWidth = 90 };
            closeBtn.Click += (_, _) => HideModal();
            body.Children.Add(closeBtn);
        });
    }


    public void ShowGlobalSearchModal()
    {
        ShowModal(NexLocale.T("modal_search_title"), body =>
        {
            var searchBoxContainer = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(11, 22, 38)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(37, 70, 110)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 4, 10, 4),
                Margin = new Thickness(0, 0, 0, 14)
            };

            var sGrid = new Grid();
            sGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            sGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var sIcon = CreateVectorIcon(NexIcon.Search, CyanBrush, 14);
            sIcon.Margin = new Thickness(0, 0, 8, 0);
            sIcon.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(sIcon, 0);
            sGrid.Children.Add(sIcon);

            var searchInput = new TextBox
            {
                Background = Brushes.Transparent,
                Foreground = TextBrush,
                CaretBrush = CyanBrush,
                BorderThickness = new Thickness(0),
                FontSize = 13.5,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(2, 4, 2, 4)
            };
            Grid.SetColumn(searchInput, 1);
            sGrid.Children.Add(searchInput);
            searchBoxContainer.Child = sGrid;
            body.Children.Add(searchBoxContainer);

            var resultsStack = new StackPanel();
            var scrollViewer = new ScrollViewer
            {
                MaxHeight = 360,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = resultsStack
            };
            body.Children.Add(scrollViewer);

            void UpdateList(string query)
            {
                resultsStack.Children.Clear();
                var list = GlobalSearchService.Search(query, 14);

                if (list.Count == 0)
                {
                    resultsStack.Children.Add(new TextBlock
                    {
                        Text = NexLocale.T("modal_search_no_results"),
                        FontSize = 12,
                        Foreground = MutedBrush,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 24, 0, 24)
                    });
                    return;
                }

                foreach (var item in list)
                {
                    var card = new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(14, 23, 36)),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(24, 38, 56)),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(7),
                        Padding = new Thickness(12, 9, 12, 9),
                        Margin = new Thickness(0, 0, 0, 6),
                        Cursor = Cursors.Hand
                    };

                    card.MouseEnter += (_, _) =>
                    {
                        card.Background = new SolidColorBrush(Color.FromRgb(20, 36, 60));
                        card.BorderBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248));
                    };
                    card.MouseLeave += (_, _) =>
                    {
                        card.Background = new SolidColorBrush(Color.FromRgb(14, 23, 36));
                        card.BorderBrush = new SolidColorBrush(Color.FromRgb(24, 38, 56));
                    };

                    var row = new Grid();
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    var ico = CreateVectorIcon(item.Icon, CyanBrush, 15);
                    ico.Margin = new Thickness(0, 0, 10, 0);
                    ico.VerticalAlignment = VerticalAlignment.Center;
                    Grid.SetColumn(ico, 0);
                    row.Children.Add(ico);

                    var tStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                    tStack.Children.Add(new TextBlock
                    {
                        Text = item.Title,
                        FontSize = 12.5,
                        FontWeight = FontWeights.Bold,
                        Foreground = TextBrush
                    });
                    tStack.Children.Add(new TextBlock
                    {
                        Text = item.Subtitle,
                        FontSize = 10.5,
                        Foreground = MutedBrush,
                        Margin = new Thickness(0, 2, 0, 0)
                    });
                    Grid.SetColumn(tStack, 1);
                    row.Children.Add(tStack);

                    var badge = new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(10, 20, 34)),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(30, 58, 95)),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(6, 2, 6, 2),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(8, 0, 0, 0)
                    };
                    badge.Child = new TextBlock
                    {
                        Text = item.Category,
                        FontSize = 9.5,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = CyanBrush
                    };
                    Grid.SetColumn(badge, 2);
                    row.Children.Add(badge);

                    card.Child = row;

                    var captured = item;
                    card.MouseLeftButtonUp += (_, _) =>
                    {
                        HideModal();
                        NavigateToSearchResult(captured);
                    };

                    resultsStack.Children.Add(card);
                }
            }

            searchInput.TextChanged += (_, _) => UpdateList(searchInput.Text);
            searchInput.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    var first = GlobalSearchService.Search(searchInput.Text, 1).FirstOrDefault();
                    if (first != null)
                    {
                        HideModal();
                        NavigateToSearchResult(first);
                    }
                }
                else if (e.Key == Key.Escape)
                {
                    HideModal();
                }
            };

            UpdateList("");

            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                searchInput.Focus();
            }));
        });
    }

    private void NavigateToSearchResult(GlobalSearchResult item)
    {
        switch (item.TargetPage)
        {
            case "Gaming":
                NavigateTo("Gaming");
                break;
            case "Customizer":
                NavigateTo("CustomizerWallpaper");
                break;
            case "Debloat":
                NavigateTo("Apps");
                break;
            case "Hardware":
                NavigateTo("Performance");
                break;
            case "Tuning":
            default:
                NavigateTo("Performance");
                break;
        }

        ShowToast(item.Title, NexLocale.Format("modal_search_toast_section_format", item.Category), NexIcon.Check, CyanBrush);
    }

    private string _selectedPresetAmount = "10";
    private string _draftCustomAmount = "";
    private string _draftDonorName = "";
    private string _draftDonorMessage = "";

    public void ShowSupportProjectModal(int selectedTier = 1, bool useDemoPreview = false)
    {
        var goal = NativeTuning.GetCurrentCommunityGoal();
        if (useDemoPreview)
        {
            goal = new NativeTuning.CommunityGoalInfo
            {
                CurrentAmount = 30,
                TargetAmount = 100,
                Currency = "EUR",
                Supporters = new List<NativeTuning.SupporterItem>
                {
                    new() { Name = "AlexG", Amount = "15 EUR", Message = "Super aplicatie, mi-a crescut FPS-ul in CS2!" },
                    new() { Name = "Vlad", Amount = "10 EUR", Message = "Cel mai curat utilitar de Windows 11, respect!" },
                    new() { Name = "Mihai", Amount = "5 EUR", Message = "Mult succes cu proiectul!" }
                }
            };
        }
        bool isEn = NexLocale.CurrentLanguage == AppLanguage.En;

        if (selectedTier == 0) _selectedPresetAmount = "5";
        else if (selectedTier == 1) _selectedPresetAmount = "10";
        else if (selectedTier == 2) _selectedPresetAmount = "25";

        ShowModal(NexLocale.T("support_goal_modal_title", "Susține Proiectul"), body =>
        {
            // 1. Header Card + Compact Progress Bar (without "100% GRATUIT" badge)
            var topCard = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(11, 20, 32)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(26, 48, 72)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(16, 12, 16, 12),
                Margin = new Thickness(0, 0, 0, 12)
            };

            var topRow = new Grid();
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(235) });

            var logoBox = new Border
            {
                Width = 36,
                Height = 36,
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromRgb(15, 30, 48)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(36, 76, 110)),
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            try
            {
                var logoImg = new Image
                {
                    Source = new BitmapImage(new Uri("pack://application:,,,/assets/logo_ribbon.png", UriKind.RelativeOrAbsolute)),
                    Width = 24,
                    Height = 24,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                RenderOptions.SetBitmapScalingMode(logoImg, BitmapScalingMode.HighQuality);
                logoBox.Child = logoImg;
            }
            catch
            {
                logoBox.Child = CreateVectorIcon(NexIcon.Rocket, CyanBrush, 18);
            }
            Grid.SetColumn(logoBox, 0);
            topRow.Children.Add(logoBox);

            var introStack = new StackPanel { Margin = new Thickness(6, 0, 14, 0), VerticalAlignment = VerticalAlignment.Center };
            introStack.Children.Add(new TextBlock
            {
                Text = isEn ? goal.TitleEn : goal.TitleRo,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = TextBrush,
                VerticalAlignment = VerticalAlignment.Center
            });

            introStack.Children.Add(new TextBlock
            {
                Text = NexLocale.T("support_goal_modal_desc"),
                FontSize = 11,
                Foreground = MutedBrush,
                Margin = new Thickness(0, 3, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 15
            });
            Grid.SetColumn(introStack, 1);
            topRow.Children.Add(introStack);

            // Compact Goal Pill on the right (208px wide bar)
            var compactGoalBox = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(13, 27, 42)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(24, 88, 78)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 8, 12, 9),
                VerticalAlignment = VerticalAlignment.Center
            };
            var cgStack = new StackPanel();
            var cgHeader = new DockPanel { Margin = new Thickness(0, 0, 0, 5) };
            var cgTitle = new TextBlock
            {
                Text = NexLocale.T("support_goal_sidebar_title", "Susține Proiectul"),
                FontSize = 10.5,
                FontWeight = FontWeights.Bold,
                Foreground = GreenBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(cgTitle, Dock.Left);
            cgHeader.Children.Add(cgTitle);

            var liveAmountLabel = new TextBlock
            {
                Text = $"{goal.CurrentAmount:0.##} / {goal.TargetAmount:0} {goal.Currency} ({goal.Percentage}%)",
                FontSize = 10.5,
                FontWeight = FontWeights.Bold,
                Foreground = CyanBrush,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(liveAmountLabel, Dock.Right);
            cgHeader.Children.Add(liveAmountLabel);
            cgStack.Children.Add(cgHeader);

            var compactBarOuter = new Border
            {
                Width = 208,
                Height = 7,
                Background = new SolidColorBrush(Color.FromRgb(18, 36, 56)),
                CornerRadius = new CornerRadius(3.5),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            var compactBarFill = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = goal.Percentage <= 0 ? 0.0 : Math.Clamp((goal.Percentage / 100.0) * 208.0, 6.0, 208.0),
                CornerRadius = new CornerRadius(3.5),
                Background = new LinearGradientBrush(Color.FromRgb(16, 185, 129), Color.FromRgb(56, 189, 248), 0.0)
            };
            compactBarOuter.Child = compactBarFill;
            cgStack.Children.Add(compactBarOuter);

            compactGoalBox.Child = cgStack;
            Grid.SetColumn(compactGoalBox, 2);
            topRow.Children.Add(compactGoalBox);

            topCard.Child = topRow;
            body.Children.Add(topCard);

            // 2. Amount Selector (Clean 5 EUR / 10 EUR / 25 EUR + Empty Custom Box with Background Watermark)
            body.Children.Add(new TextBlock
            {
                Text = NexLocale.T("support_goal_tier_section", "ALEGE SAU INTRODU SUMA DORITĂ"),
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = MutedBrush,
                Margin = new Thickness(2, 0, 0, 5)
            });

            var amountRow = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            amountRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            amountRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            amountRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            amountRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            amountRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            amountRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            amountRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.3, GridUnitType.Star) });

            Button? primaryDonateBtn = null;
            Button? revolutBtn = null;
            Button? paypalBtn = null;
            TextBox? customAmountBox = null;
            Border? customBoxBorder = null;

            double GetCurrentSelectedAmount()
            {
                string rawCustom = (customAmountBox?.Text ?? _draftCustomAmount).Replace(",", ".").Trim();
                if (!string.IsNullOrEmpty(rawCustom) && double.TryParse(rawCustom, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val) && val >= 1)
                {
                    return Math.Round(val, 2);
                }
                if (double.TryParse(_selectedPresetAmount, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double presetVal) && presetVal >= 1)
                {
                    return presetVal;
                }
                return 10.0;
            }

            void UpdateCtaButtonLabels()
            {
                double amt = GetCurrentSelectedAmount();
                string amtStr = amt.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                if (primaryDonateBtn != null)
                    primaryDonateBtn.Content = NexLocale.Format("support_goal_btn_primary", amtStr);
                if (revolutBtn != null)
                    revolutBtn.Content = NexLocale.Format("support_goal_btn_revolut", amtStr);
                if (paypalBtn != null)
                    paypalBtn.Content = NexLocale.Format("support_goal_btn_paypal", amtStr);
            }

            var presetBorders = new List<(Border Border, TextBlock Label, string Val)>();
            Border CreatePresetPill(string amountVal, string title)
            {
                bool isSel = string.IsNullOrWhiteSpace(_draftCustomAmount) && (_selectedPresetAmount == amountVal);
                var b = new Border
                {
                    Background = isSel ? new SolidColorBrush(Color.FromRgb(14, 34, 54)) : new SolidColorBrush(Color.FromRgb(11, 18, 28)),
                    BorderBrush = isSel ? CyanBrush : new SolidColorBrush(Color.FromRgb(26, 40, 58)),
                    BorderThickness = new Thickness(isSel ? 1.5 : 1),
                    CornerRadius = new CornerRadius(8),
                    Height = 44,
                    Padding = new Thickness(10, 0, 10, 0),
                    Cursor = Cursors.Hand
                };
                var lbl = new TextBlock
                {
                    Text = title,
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    Foreground = isSel ? CyanBrush : TextBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                b.Child = lbl;
                b.MouseLeftButtonUp += (_, _) =>
                {
                    _selectedPresetAmount = amountVal;
                    _draftCustomAmount = "";
                    if (customAmountBox != null) customAmountBox.Text = "";
                    foreach (var pb in presetBorders)
                    {
                        pb.Border.Background = new SolidColorBrush(Color.FromRgb(11, 18, 28));
                        pb.Border.BorderBrush = new SolidColorBrush(Color.FromRgb(26, 40, 58));
                        pb.Border.BorderThickness = new Thickness(1);
                        pb.Label.Foreground = TextBrush;
                    }
                    b.Background = new SolidColorBrush(Color.FromRgb(14, 34, 54));
                    b.BorderBrush = CyanBrush;
                    b.BorderThickness = new Thickness(1.5);
                    lbl.Foreground = CyanBrush;
                    if (customBoxBorder != null)
                    {
                        customBoxBorder.Background = new SolidColorBrush(Color.FromRgb(11, 18, 28));
                        customBoxBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(28, 44, 64));
                        customBoxBorder.BorderThickness = new Thickness(1);
                    }
                    UpdateCtaButtonLabels();
                };
                presetBorders.Add((b, lbl, amountVal));
                return b;
            }

            var p5 = CreatePresetPill("5", NexLocale.T("support_goal_tier_1_title", "5 EUR"));
            var p10 = CreatePresetPill("10", NexLocale.T("support_goal_tier_2_title", "10 EUR"));
            var p25 = CreatePresetPill("25", NexLocale.T("support_goal_tier_3_title", "25 EUR"));

            Grid.SetColumn(p5, 0);
            Grid.SetColumn(p10, 2);
            Grid.SetColumn(p25, 4);
            amountRow.Children.Add(p5);
            amountRow.Children.Add(p10);
            amountRow.Children.Add(p25);

            // Custom Amount Box (Empty by default with background watermark placeholder)
            bool hasCustomInit = !string.IsNullOrWhiteSpace(_draftCustomAmount);
            customBoxBorder = new Border
            {
                Background = hasCustomInit ? new SolidColorBrush(Color.FromRgb(14, 34, 54)) : new SolidColorBrush(Color.FromRgb(11, 18, 28)),
                BorderBrush = hasCustomInit ? CyanBrush : new SolidColorBrush(Color.FromRgb(28, 44, 64)),
                BorderThickness = new Thickness(hasCustomInit ? 1.5 : 1),
                CornerRadius = new CornerRadius(8),
                Height = 44,
                Padding = new Thickness(10, 4, 10, 4)
            };
            var customBoxStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            customBoxStack.Children.Add(new TextBlock
            {
                Text = NexLocale.T("support_goal_custom_amount_label", "Custom"),
                FontSize = 9.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = CyanBrush
            });
            var customInputGrid = new Grid { Margin = new Thickness(0, 1, 0, 0) };
            customInputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            customInputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var customWatermark = new TextBlock
            {
                Text = NexLocale.T("support_goal_custom_placeholder", "Introdu suma..."),
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(90, 110, 135)),
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false,
                Visibility = string.IsNullOrEmpty(_draftCustomAmount) ? Visibility.Visible : Visibility.Collapsed
            };
            Grid.SetColumn(customWatermark, 0);
            customInputGrid.Children.Add(customWatermark);

            customAmountBox = new TextBox
            {
                Text = _draftCustomAmount,
                MaxLength = 6,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = TextBrush,
                CaretBrush = CyanBrush,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            customAmountBox.GotFocus += (_, _) =>
            {
                customWatermark.Visibility = Visibility.Collapsed;
                customBoxBorder.Background = new SolidColorBrush(Color.FromRgb(14, 34, 54));
                customBoxBorder.BorderBrush = CyanBrush;
                customBoxBorder.BorderThickness = new Thickness(1.5);
            };
            customAmountBox.LostFocus += (_, _) =>
            {
                customWatermark.Visibility = string.IsNullOrEmpty(customAmountBox.Text) ? Visibility.Visible : Visibility.Collapsed;
                if (string.IsNullOrWhiteSpace(customAmountBox.Text))
                {
                    customBoxBorder.Background = new SolidColorBrush(Color.FromRgb(11, 18, 28));
                    customBoxBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(28, 44, 64));
                    customBoxBorder.BorderThickness = new Thickness(1);
                }
            };
            customAmountBox.TextChanged += (_, _) =>
            {
                _draftCustomAmount = customAmountBox.Text;
                customWatermark.Visibility = string.IsNullOrEmpty(_draftCustomAmount) ? Visibility.Visible : Visibility.Collapsed;
                if (!string.IsNullOrWhiteSpace(_draftCustomAmount))
                {
                    foreach (var pb in presetBorders)
                    {
                        pb.Border.Background = new SolidColorBrush(Color.FromRgb(11, 18, 28));
                        pb.Border.BorderBrush = new SolidColorBrush(Color.FromRgb(26, 40, 58));
                        pb.Border.BorderThickness = new Thickness(1);
                        pb.Label.Foreground = TextBrush;
                    }
                    customBoxBorder.Background = new SolidColorBrush(Color.FromRgb(14, 34, 54));
                    customBoxBorder.BorderBrush = CyanBrush;
                    customBoxBorder.BorderThickness = new Thickness(1.5);
                }
                UpdateCtaButtonLabels();
            };
            Grid.SetColumn(customAmountBox, 0);
            customInputGrid.Children.Add(customAmountBox);

            var eurSuffix = new TextBlock
            {
                Text = goal.Currency,
                FontSize = 10.5,
                FontWeight = FontWeights.Bold,
                Foreground = MutedBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0)
            };
            Grid.SetColumn(eurSuffix, 1);
            customInputGrid.Children.Add(eurSuffix);

            customBoxStack.Children.Add(customInputGrid);
            customBoxBorder.Child = customBoxStack;
            Grid.SetColumn(customBoxBorder, 6);
            amountRow.Children.Add(customBoxBorder);

            body.Children.Add(amountRow);

            // 3. Donor Name ("Numele tău") & Message Input with Background Watermarks
            var formGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            formGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(185) });
            formGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            formGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Name Field
            var nameBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(11, 18, 28)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(28, 44, 64)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 6, 10, 6)
            };
            var nameStack = new StackPanel();
            nameStack.Children.Add(new TextBlock
            {
                Text = NexLocale.T("support_goal_name_label", "Numele tău"),
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = MutedBrush,
                Margin = new Thickness(0, 0, 0, 3)
            });
            var nameInputGrid = new Grid();
            var nameWatermark = new TextBlock
            {
                Text = NexLocale.T("support_goal_name_placeholder", "Introdu numele tău..."),
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(85, 105, 130)),
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false,
                Visibility = string.IsNullOrEmpty(_draftDonorName) ? Visibility.Visible : Visibility.Collapsed
            };
            nameInputGrid.Children.Add(nameWatermark);

            var nameInput = new TextBox
            {
                Text = _draftDonorName,
                MaxLength = 24,
                FontSize = 11.5,
                Foreground = TextBrush,
                CaretBrush = CyanBrush,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0)
            };
            nameInput.GotFocus += (_, _) => nameWatermark.Visibility = Visibility.Collapsed;
            nameInput.LostFocus += (_, _) => nameWatermark.Visibility = string.IsNullOrEmpty(nameInput.Text) ? Visibility.Visible : Visibility.Collapsed;
            nameInput.TextChanged += (_, _) =>
            {
                _draftDonorName = nameInput.Text;
                nameWatermark.Visibility = string.IsNullOrEmpty(_draftDonorName) ? Visibility.Visible : Visibility.Collapsed;
            };
            nameInputGrid.Children.Add(nameInput);
            nameStack.Children.Add(nameInputGrid);
            nameBorder.Child = nameStack;
            Grid.SetColumn(nameBorder, 0);
            formGrid.Children.Add(nameBorder);

            // Message Field with Live Character Counter (0 / 120)
            var msgBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(11, 18, 28)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(28, 44, 64)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 6, 10, 6)
            };
            var msgStack = new StackPanel();
            var msgHeader = new DockPanel { Margin = new Thickness(0, 0, 0, 3) };
            var msgLabel = new TextBlock
            {
                Text = NexLocale.T("support_goal_msg_label", "Mesajul tău (opțional)"),
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = MutedBrush
            };
            DockPanel.SetDock(msgLabel, Dock.Left);
            msgHeader.Children.Add(msgLabel);

            var charCounterLabel = new TextBlock
            {
                Text = NexLocale.Format("support_goal_msg_counter", _draftDonorMessage.Length),
                FontSize = 9.5,
                FontWeight = FontWeights.Bold,
                Foreground = CyanBrush,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            DockPanel.SetDock(charCounterLabel, Dock.Right);
            msgHeader.Children.Add(charCounterLabel);
            msgStack.Children.Add(msgHeader);

            var msgInputGrid = new Grid();
            var msgWatermark = new TextBlock
            {
                Text = NexLocale.T("support_goal_msg_placeholder", "Scrie un mesaj scurt..."),
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(85, 105, 130)),
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false,
                Visibility = string.IsNullOrEmpty(_draftDonorMessage) ? Visibility.Visible : Visibility.Collapsed
            };
            msgInputGrid.Children.Add(msgWatermark);

            var msgInput = new TextBox
            {
                Text = _draftDonorMessage,
                MaxLength = 120,
                FontSize = 11.5,
                Foreground = TextBrush,
                CaretBrush = CyanBrush,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0)
            };
            msgInput.GotFocus += (_, _) => msgWatermark.Visibility = Visibility.Collapsed;
            msgInput.LostFocus += (_, _) => msgWatermark.Visibility = string.IsNullOrEmpty(msgInput.Text) ? Visibility.Visible : Visibility.Collapsed;
            msgInput.TextChanged += (_, _) =>
            {
                _draftDonorMessage = msgInput.Text;
                int len = _draftDonorMessage.Length;
                msgWatermark.Visibility = len == 0 ? Visibility.Visible : Visibility.Collapsed;
                charCounterLabel.Text = NexLocale.Format("support_goal_msg_counter", len);
                charCounterLabel.Foreground = len >= 110 ? AmberBrush : CyanBrush;
            };
            msgInputGrid.Children.Add(msgInput);
            msgStack.Children.Add(msgInputGrid);
            msgBorder.Child = msgStack;
            Grid.SetColumn(msgBorder, 2);
            formGrid.Children.Add(msgBorder);

            body.Children.Add(formGrid);

            // 4. Donors Feed Container ("Cine a donat")
            var supportersFeedPanel = new StackPanel();
            void RenderSupportersList(NativeTuning.CommunityGoalInfo currentGoal)
            {
                liveAmountLabel.Text = $"{currentGoal.CurrentAmount:0.##} / {currentGoal.TargetAmount:0} {currentGoal.Currency} ({currentGoal.Percentage}%)";
                compactBarFill.Width = currentGoal.Percentage <= 0 ? 0.0 : Math.Clamp((currentGoal.Percentage / 100.0) * 208.0, 6.0, 208.0);
                UpdateSidebarSupportGoalUI(currentGoal);

                supportersFeedPanel.Children.Clear();
                if (currentGoal.Supporters.Count == 0)
                {
                    supportersFeedPanel.Children.Add(new TextBlock
                    {
                        Text = NexLocale.T("support_goal_supporters_empty", "Nicio donație înregistrată încă. Fii primul care susține proiectul!"),
                        FontSize = 11,
                        Foreground = MutedBrush,
                        Margin = new Thickness(2, 6, 2, 6)
                    });
                    return;
                }

                foreach (var sup in currentGoal.Supporters.Take(12))
                {
                    var rowCard = new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(13, 22, 34)),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(25, 40, 58)),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(6),
                        Padding = new Thickness(10, 6, 10, 6),
                        Margin = new Thickness(0, 0, 0, 5)
                    };
                    var rStack = new StackPanel();
                    var rTop = new StackPanel { Orientation = Orientation.Horizontal };
                    rTop.Children.Add(new TextBlock
                    {
                        Text = sup.Name,
                        FontSize = 11,
                        FontWeight = FontWeights.Bold,
                        Foreground = TextBrush
                    });
                    if (!string.IsNullOrWhiteSpace(sup.Amount))
                    {
                        rTop.Children.Add(new Border
                        {
                            Background = new SolidColorBrush(Color.FromArgb(35, 16, 185, 129)),
                            CornerRadius = new CornerRadius(3),
                            Padding = new Thickness(5, 0.5, 5, 0.5),
                            Margin = new Thickness(7, 0, 0, 0),
                            Child = new TextBlock
                            {
                                Text = sup.Amount,
                                FontSize = 9.5,
                                FontWeight = FontWeights.Bold,
                                Foreground = GreenBrush
                            }
                        });
                    }
                    rStack.Children.Add(rTop);

                    if (!string.IsNullOrWhiteSpace(sup.Message))
                    {
                        rStack.Children.Add(new TextBlock
                        {
                            Text = sup.Message,
                            FontSize = 10.5,
                            Foreground = new SolidColorBrush(Color.FromRgb(190, 205, 222)),
                            Margin = new Thickness(0, 2, 0, 0),
                            TextWrapping = TextWrapping.Wrap
                        });
                    }
                    rowCard.Child = rStack;
                    supportersFeedPanel.Children.Add(rowCard);
                }
            }

            // 5. Primary & Secondary Direct Call-to-Action Buttons
            var ctaRow = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            ctaRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.65, GridUnitType.Star) });
            ctaRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            ctaRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ctaRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            ctaRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            async Task ExecuteDonationAndBroadcastAsync(string targetBaseUrl)
            {
                double amt = GetCurrentSelectedAmount();
                string donor = nameInput.Text.Trim();
                string msg = msgInput.Text.Trim();

                if (!string.IsNullOrWhiteSpace(msg))
                {
                    try { Clipboard.SetText(msg); } catch { }
                }

                // Store pending donor name & message draft for webhook verification (does NOT add to Cine a donat before payment!)
                _ = NativeTuning.SavePendingDonationDraftAsync(donor, amt, msg);

                try
                {
                    Process.Start(new ProcessStartInfo(targetBaseUrl) { UseShellExecute = true });
                }
                catch { }

                bool isEn = NexLocale.CurrentLanguage == AppLanguage.En;
                ShowToast(
                    isEn ? "Payment Page Opened" : "Pagină de Plată Deschisă",
                    isEn
                        ? $"Complete the {amt:0.##} EUR donation in your browser. It will appear automatically after payment confirmation."
                        : $"Finalizează donația ({amt:0.##} EUR) în browser. Va apărea automat în listă imediat după confirmarea plății.",
                    NexIcon.Check,
                    CyanBrush
                );
                await Task.CompletedTask;
            }

            primaryDonateBtn = new Button
            {
                Style = (Style)FindResource("PrimaryGradientButtonStyle"),
                Height = 35,
                FontSize = 11.5,
                FontWeight = FontWeights.Bold,
                Cursor = Cursors.Hand
            };
            primaryDonateBtn.Click += async (_, _) => await ExecuteDonationAndBroadcastAsync(goal.DonateUrl);
            Grid.SetColumn(primaryDonateBtn, 0);
            ctaRow.Children.Add(primaryDonateBtn);

            revolutBtn = new Button
            {
                Style = (Style)FindResource("SecondaryButtonStyle"),
                Height = 35,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = CyanBrush,
                BorderBrush = new SolidColorBrush(Color.FromArgb(90, 56, 189, 248)),
                Cursor = Cursors.Hand
            };
            revolutBtn.Click += async (_, _) => await ExecuteDonationAndBroadcastAsync(goal.RevolutUrl);
            Grid.SetColumn(revolutBtn, 2);
            ctaRow.Children.Add(revolutBtn);

            paypalBtn = new Button
            {
                Style = (Style)FindResource("SecondaryButtonStyle"),
                Height = 35,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand
            };
            paypalBtn.Click += async (_, _) => await ExecuteDonationAndBroadcastAsync(goal.PaypalUrl);
            Grid.SetColumn(paypalBtn, 4);
            ctaRow.Children.Add(paypalBtn);

            UpdateCtaButtonLabels();
            body.Children.Add(ctaRow);

            // 6. "Cine a donat" Card
            var supportersBox = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(9, 15, 24)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(22, 34, 50)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 9, 12, 9),
                Margin = new Thickness(0, 0, 0, 10)
            };
            var supStack = new StackPanel();
            supStack.Children.Add(new TextBlock
            {
                Text = NexLocale.T("support_goal_supporters_title", "Cine a donat"),
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = TextBrush,
                Margin = new Thickness(0, 0, 0, 6)
            });

            var feedScroll = new ScrollViewer
            {
                MaxHeight = 135,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            feedScroll.Content = supportersFeedPanel;
            supStack.Children.Add(feedScroll);

            supportersBox.Child = supStack;
            body.Children.Add(supportersBox);

            RenderSupportersList(goal);

            // Silent Background Polling while modal is open (every 8s)
            var liveModalTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
            liveModalTimer.Tick += async (_, _) =>
            {
                if (ModalOverlay.Visibility != Visibility.Visible)
                {
                    liveModalTimer.Stop();
                    return;
                }
                if (!useDemoPreview)
                {
                    var latest = await NativeTuning.FetchCommunityGoalAsync();
                    RenderSupportersList(latest);
                }
            };
            liveModalTimer.Start();

            // Bottom Footer Bar
            var footerBar = new DockPanel { LastChildFill = false };
            var closeModalBtn = new Button
            {
                Content = NexLocale.T("btn_close", "Închide"),
                Style = (Style)FindResource("SecondaryButtonStyle"),
                Height = 28,
                FontSize = 10.5,
                MinWidth = 85,
                Padding = new Thickness(14, 0, 14, 0),
                Cursor = Cursors.Hand
            };
            closeModalBtn.Click += (_, _) =>
            {
                liveModalTimer.Stop();
                HideModal();
            };
            DockPanel.SetDock(closeModalBtn, Dock.Right);
            footerBar.Children.Add(closeModalBtn);

            body.Children.Add(footerBar);
        });
    }
}
