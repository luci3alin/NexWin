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
    private void ShowNetwork()
    {
        PreparePage(NexLocale.T("network_title"), NexLocale.T("network_subtitle"));

        var netStat = NativeTuning.GetNetworkLabStatus();

        // 1. Adapter Status Card
        var statCard = new Border
        {
            Background = CardBackground(),
            BorderBrush = new SolidColorBrush(Color.FromRgb(24, 38, 56)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 14, 16, 14),
            Margin = new Thickness(0, 0, 0, 14)
        };
        var sStack = new StackPanel();
        var sHead = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
        sHead.Children.Add(CreateVectorIcon(NexIcon.Network, CyanBrush, 18));
        var sTitles = new StackPanel { Margin = new Thickness(10, 0, 0, 0) };
        sTitles.Children.Add(new TextBlock { Text = NexLocale.T("net_adapter_header", "ADAPTOR DE REȚEA ACTIV"), FontSize = 13, FontWeight = FontWeights.Bold, Foreground = TextBrush });
        sTitles.Children.Add(new TextBlock { Text = $"{netStat.AdapterName}  ·  {netStat.LinkSpeed}", FontSize = 11, Foreground = CyanBrush });
        sHead.Children.Add(sTitles);
        sStack.Children.Add(sHead);

        var netWrap = new WrapPanel();
        netWrap.Children.Add(BuildInfoTile(NexLocale.T("net_tile_ipv4", "Adresă IPv4 Locală"), netStat.LocalIpv4, CyanBrush));
        netWrap.Children.Add(BuildInfoTile(NexLocale.T("net_tile_gateway", "Gateway (Router)"), $"{netStat.GatewayIp} ({netStat.GatewayPingMs:0.0} ms)", GreenBrush));
        netWrap.Children.Add(BuildInfoTile(NexLocale.T("net_tile_dns", "Server DNS Principal"), $"{netStat.DnsServer} ({netStat.DnsPingMs:0.0} ms)", PurpleBrush));
        string connType = netStat.IsWifi ? string.Format(NexLocale.T("net_tile_wifi_format", "Wi-Fi ({0}% Semnal)"), netStat.WifiSignalStrength) : NexLocale.T("net_tile_ethernet", "Ethernet Gigabit");
        netWrap.Children.Add(BuildInfoTile(NexLocale.T("net_tile_conn_type", "Tip Conexiune"), connType, AmberBrush));
        sStack.Children.Add(netWrap);
        statCard.Child = sStack;
        PageRoot.Children.Add(statCard);

        // 2. Gaming Server Latency Live Card
        var pingCard = new Border
        {
            Background = CardBackground(),
            BorderBrush = new SolidColorBrush(Color.FromRgb(24, 38, 56)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 14, 16, 14),
            Margin = new Thickness(0, 0, 0, 14)
        };
        var pStack = new StackPanel();

        var pHead = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        pHead.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        pHead.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var pTitleStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        pTitleStack.Children.Add(CreateVectorIcon(NexIcon.Gamepad, PurpleBrush, 16));
        pTitleStack.Children.Add(new TextBlock
        {
            Text = "  " + NexLocale.T("net_ping_header", "LATENȚĂ SERVERE GAMING COMPETITIVE (PING LIVE)"),
            FontSize = 12.5,
            FontWeight = FontWeights.Bold,
            Foreground = PurpleBrush,
            VerticalAlignment = VerticalAlignment.Center
        });
        Grid.SetColumn(pTitleStack, 0);
        pHead.Children.Add(pTitleStack);

        var pingRefreshBtn = MakeCardButton(NexLocale.T("net_btn_retest_ping", "Re-testează Ping"), NexIcon.Refresh, PurpleBrush, async () =>
        {
            ShowNotification(NexLocale.T("net_notif_ping_title", "Test Pinging"), NexLocale.T("net_notif_ping_msg", "Măsurare latență către servere gaming..."), true);
            await NativeTuning.PingGamingServersAsync();
            ShowNotification(NexLocale.T("net_notif_ping_done_title", "Ping Finalizat"), NexLocale.T("net_notif_ping_done_msg", "Latențele au fost actualizate."), false, true);
            Dispatcher.Invoke(() => ShowNetwork());
        }, false, 140);
        Grid.SetColumn(pingRefreshBtn, 1);
        pHead.Children.Add(pingRefreshBtn);
        pStack.Children.Add(pHead);

        var pings = NativeTuning.GetDefaultGamingServers();
        foreach (var srv in pings)
        {
            var rowB = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(9, 15, 24)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(20, 32, 46)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(14, 8, 14, 8),
                Margin = new Thickness(0, 0, 0, 6)
            };
            var rG = new Grid();
            rG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) });
            rG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) });
            rG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var nameTxt = new TextBlock { Text = srv.Title, FontSize = 12.5, FontWeight = FontWeights.SemiBold, Foreground = TextBrush, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(nameTxt, 0);
            rG.Children.Add(nameTxt);

            var regTxt = new TextBlock { Text = srv.Region, FontSize = 11, Foreground = MutedBrush, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(regTxt, 1);
            rG.Children.Add(regTxt);

            var ipTxt = new TextBlock { Text = srv.HostIp, FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(100, 120, 145)), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(ipTxt, 2);
            rG.Children.Add(ipTxt);

            Brush pBrush = srv.LatencyMs < 30 ? GreenBrush : (srv.LatencyMs < 60 ? AmberBrush : RedBrush);
            var latBadge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(35, ((SolidColorBrush)pBrush).Color.R, ((SolidColorBrush)pBrush).Color.G, ((SolidColorBrush)pBrush).Color.B)),
                BorderBrush = pBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(10, 3, 10, 3),
                VerticalAlignment = VerticalAlignment.Center
            };
            string qualityLabel = srv.LatencyMs < 25 ? NexLocale.T("net_quality_excellent") : (srv.LatencyMs < 55 ? NexLocale.T("net_quality_good") : NexLocale.T("net_quality_medium"));
            latBadge.Child = new TextBlock
            {
                Text = $"{srv.LatencyMs:0.0} ms ({qualityLabel})",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = pBrush
            };
            Grid.SetColumn(latBadge, 3);
            rG.Children.Add(latBadge);

            rowB.Child = rG;
            pStack.Children.Add(rowB);
        }
        pingCard.Child = pStack;
        PageRoot.Children.Add(pingCard);

        // 3. Official Ookla Speedtest CLI Card
        var speedCard = new Border
        {
            Background = CardBackground(),
            BorderBrush = new SolidColorBrush(Color.FromRgb(24, 38, 56)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 14, 16, 14),
            Margin = new Thickness(0, 0, 0, 14)
        };
        var spStack = new StackPanel();

        var spHead = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        spHead.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        spHead.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var spTitleStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        spTitleStack.Children.Add(CreateVectorIcon(NexIcon.Bolt, CyanBrush, 17));
        var spTitles = new StackPanel { Margin = new Thickness(10, 0, 0, 0) };
        spTitles.Children.Add(new TextBlock
        {
            Text = NexLocale.T("net_speedtest_header", "ETHERNET & INTERNET SPEEDTEST"),
            FontSize = 12.5,
            FontWeight = FontWeights.Bold,
            Foreground = CyanBrush
        });
        spTitles.Children.Add(new TextBlock
        {
            Text = NexLocale.T("net_speedtest_subtitle", "Măsurare lățime de bandă Download, Upload, Latență Ping și Jitter pentru conexiunea ta de rețea."),
            FontSize = 10.5,
            Foreground = MutedBrush,
            Margin = new Thickness(0, 2, 0, 0)
        });
        spTitleStack.Children.Add(spTitles);
        Grid.SetColumn(spTitleStack, 0);
        spHead.Children.Add(spTitleStack);

        void CancelSpeedtest()
        {
            if (speedtestCts != null && !speedtestCts.IsCancellationRequested)
            {
                try { speedtestCts.Cancel(); } catch { }
            }
            isSpeedtestRunning = false;
            AppendLog(NexLocale.T("net_speedtest_log_stopped", "[SPEEDTEST] Măsurarea vitezei a fost oprită de utilizator."), false);
            ShowToast(NexLocale.T("net_toast_stopped_title", "Speedtest Oprit"), NexLocale.T("net_toast_stopped_msg", "Măsurarea vitezei a fost anulată."), NexIcon.Info, AmberBrush);
            ShowNetwork();
        }

        FrameworkElement spActionElement;

        if (isSpeedtestRunning)
        {
            var runningBox = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            // Status Pill (Informational only - no button hover!)
            var statusPill = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(10, 22, 36)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(20, 48, 76)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            var sContent = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            sContent.Children.Add(CreateVectorIcon(NexIcon.Pulse, CyanBrush, 12));
            sContent.Children.Add(new TextBlock
            {
                Text = "  " + NexLocale.T("net_measuring_speed", "Se măsoară viteza..."),
                Foreground = CyanBrush,
                FontWeight = FontWeights.SemiBold,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            });
            statusPill.Child = sContent;
            runningBox.Children.Add(statusPill);

            // Top-right Cancel Button
            var cancelBtn = new Border
            {
                Cursor = Cursors.Hand,
                Height = 32,
                Padding = new Thickness(12, 0, 12, 0),
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromRgb(40, 16, 22)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(120, 28, 38)),
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Center
            };
            var cbContent = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            cbContent.Children.Add(CreateVectorIcon(NexIcon.X, new SolidColorBrush(Color.FromRgb(248, 113, 113)), 11));
            cbContent.Children.Add(new TextBlock
            {
                Text = "  " + NexLocale.T("btn_cancel", "Anulează"),
                Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113)),
                FontWeight = FontWeights.Bold,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            });
            cancelBtn.Child = cbContent;

            cancelBtn.MouseEnter += (_, _) =>
            {
                cancelBtn.Background = new SolidColorBrush(Color.FromRgb(65, 20, 28));
                cancelBtn.BorderBrush = new SolidColorBrush(Color.FromRgb(185, 28, 28));
            };
            cancelBtn.MouseLeave += (_, _) =>
            {
                cancelBtn.Background = new SolidColorBrush(Color.FromRgb(40, 16, 22));
                cancelBtn.BorderBrush = new SolidColorBrush(Color.FromRgb(120, 28, 38));
            };
            cancelBtn.MouseLeftButtonUp += (_, _) => CancelSpeedtest();

            runningBox.Children.Add(cancelBtn);
            spActionElement = runningBox;
        }
        else
        {
            var runSpBtn = new Border
            {
                Cursor = Cursors.Hand,
                Height = 34,
                Padding = new Thickness(16, 0, 16, 0),
                CornerRadius = new CornerRadius(6),
                Background = new LinearGradientBrush(Color.FromRgb(14, 165, 233), Color.FromRgb(59, 130, 246), new Point(0, 0), new Point(1, 0)),
                VerticalAlignment = VerticalAlignment.Center
            };

            var spBtnContent = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            spBtnContent.Children.Add(CreateVectorIcon(NexIcon.Pulse, Brushes.White, 13));
            spBtnContent.Children.Add(new TextBlock
            {
                Text = "  " + NexLocale.T("net_btn_start_speedtest", "Pornește Speedtest Rețea"),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 11.5,
                VerticalAlignment = VerticalAlignment.Center
            });
            runSpBtn.Child = spBtnContent;

            runSpBtn.MouseEnter += (_, _) =>
            {
                runSpBtn.Background = new LinearGradientBrush(Color.FromRgb(2, 132, 199), Color.FromRgb(37, 99, 235), new Point(0, 0), new Point(1, 0));
            };
            runSpBtn.MouseLeave += (_, _) =>
            {
                runSpBtn.Background = new LinearGradientBrush(Color.FromRgb(14, 165, 233), Color.FromRgb(59, 130, 246), new Point(0, 0), new Point(1, 0));
            };

            runSpBtn.MouseLeftButtonUp += async (_, _) =>
            {
                if (isSpeedtestRunning) return;
                isSpeedtestRunning = true;
                speedtestCts = new CancellationTokenSource();
                var token = speedtestCts.Token;
                HideNotification();
                ShowNetwork();

                var result = await NativeTuning.RunSpeedtestAsync(progress =>
                {
                    Dispatcher.Invoke(() => AppendLog($"[SPEEDTEST] {progress}", false));
                }, token);

                isSpeedtestRunning = false;
                speedtestCts = null;

                if (token.IsCancellationRequested)
                {
                    AppendLog(NexLocale.T("net_speedtest_log_stopped_test", "[SPEEDTEST] Testul a fost oprit de utilizator."), false);
                    ShowToast(NexLocale.T("net_toast_stopped_title", "Speedtest Oprit"), NexLocale.T("net_toast_stopped_msg", "Măsurarea vitezei a fost anulată."), NexIcon.Info, AmberBrush);
                }
                else if (result.IsSuccess)
                {
                    lastSpeedtestResult = result;
                    AppendLog(NexLocale.Format("net_speedtest_log_success_format", result.DownloadMbps, result.UploadMbps, result.PingLatencyMs, result.Isp), false);
                }
                else
                {
                    ShowToast(NexLocale.T("net_toast_err_title", "Eroare Speedtest"), result.ErrorMessage, NexIcon.Warning, AmberBrush);
                }
                ShowNetwork();
            };

            spActionElement = runSpBtn;
        }

        Grid.SetColumn(spActionElement, 1);
        spHead.Children.Add(spActionElement);
        spStack.Children.Add(spHead);

        // Results Container
        var spResGrid = new Grid { Margin = new Thickness(0, 4, 0, 0) };
        spResGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        spResGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        spResGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        spResGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        Border BuildSpeedTile(string title, string val, string unit, Brush col)
        {
            var b = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(10, 18, 30)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(22, 36, 54)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 8, 0)
            };
            var st = new StackPanel();
            st.Children.Add(new TextBlock { Text = title, FontSize = 10, Foreground = MutedBrush });
            var vRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
            vRow.Children.Add(new TextBlock { Text = val, FontSize = 18, FontWeight = FontWeights.Bold, Foreground = col });
            vRow.Children.Add(new TextBlock { Text = " " + unit, FontSize = 11, Foreground = MutedBrush, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(2, 0, 0, 2) });
            st.Children.Add(vRow);
            b.Child = st;
            return b;
        }

        if (isSpeedtestRunning)
        {
            var loadingBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(10, 18, 30)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(22, 36, 54)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(20, 16, 20, 16)
            };
            var lSt = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            var loadHeader = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            loadHeader.Children.Add(CreateVectorIcon(NexIcon.Pulse, CyanBrush, 14));
            loadHeader.Children.Add(new TextBlock { Text = "  " + NexLocale.T("net_speedtest_running_header", "Măsurare viteză conexiune rețea în desfășurare..."), FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = CyanBrush, VerticalAlignment = VerticalAlignment.Center });
            lSt.Children.Add(loadHeader);
            
            var pBar = new ProgressBar
            {
                IsIndeterminate = true,
                Width = 320,
                Height = 5,
                Margin = new Thickness(0, 12, 0, 10),
                Foreground = CyanBrush,
                Background = new SolidColorBrush(Color.FromRgb(22, 36, 54)),
                BorderThickness = new Thickness(0)
            };
            lSt.Children.Add(pBar);

            var stepsRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2, 0, 4) };
            stepsRow.Children.Add(new TextBlock { Text = NexLocale.T("net_speedtest_step1", "1. Conectare & Ping (~15s)  •  "), FontSize = 10.5, Foreground = CyanBrush });
            stepsRow.Children.Add(new TextBlock { Text = NexLocale.T("net_speedtest_step2", "2. Test Download (~10s)  •  "), FontSize = 10.5, Foreground = PurpleBrush });
            stepsRow.Children.Add(new TextBlock { Text = NexLocale.T("net_speedtest_step3", "3. Test Upload (~5s)"), FontSize = 10.5, Foreground = GreenBrush });
            lSt.Children.Add(stepsRow);

            lSt.Children.Add(new TextBlock { Text = NexLocale.T("net_speedtest_step_desc", "Măsurare precisă debite de transfer și calitatea conexiunii active."), FontSize = 11, Foreground = MutedBrush, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0) });

            // Center Cancel Button
            var cancelCardBtn = new Border
            {
                Cursor = Cursors.Hand,
                Height = 30,
                Padding = new Thickness(14, 0, 14, 0),
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromRgb(35, 15, 20)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(90, 24, 32)),
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 12, 0, 0)
            };
            var ccContent = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            ccContent.Children.Add(CreateVectorIcon(NexIcon.X, new SolidColorBrush(Color.FromRgb(248, 113, 113)), 10));
            ccContent.Children.Add(new TextBlock
            {
                Text = "  " + NexLocale.T("net_btn_cancel_measure", "Anulează măsurarea"),
                Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113)),
                FontWeight = FontWeights.SemiBold,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            });
            cancelCardBtn.Child = ccContent;
            cancelCardBtn.MouseEnter += (_, _) => cancelCardBtn.Background = new SolidColorBrush(Color.FromRgb(55, 20, 28));
            cancelCardBtn.MouseLeave += (_, _) => cancelCardBtn.Background = new SolidColorBrush(Color.FromRgb(35, 15, 20));
            cancelCardBtn.MouseLeftButtonUp += (_, _) => CancelSpeedtest();
            lSt.Children.Add(cancelCardBtn);

            loadingBorder.Child = lSt;
            spStack.Children.Add(loadingBorder);
        }
        else if (lastSpeedtestResult != null && lastSpeedtestResult.IsSuccess)
        {
            var tDl = BuildSpeedTile(NexLocale.T("net_speedtest_download", "DOWNLOAD"), $"{lastSpeedtestResult.DownloadMbps:0.00}", "Mbps", CyanBrush);
            Grid.SetColumn(tDl, 0); spResGrid.Children.Add(tDl);

            var tUl = BuildSpeedTile(NexLocale.T("net_speedtest_upload", "UPLOAD"), $"{lastSpeedtestResult.UploadMbps:0.00}", "Mbps", PurpleBrush);
            Grid.SetColumn(tUl, 1); spResGrid.Children.Add(tUl);

            var tPing = BuildSpeedTile(NexLocale.T("net_speedtest_ping", "LATENȚĂ PING"), $"{lastSpeedtestResult.PingLatencyMs:0.0}", "ms", GreenBrush);
            Grid.SetColumn(tPing, 2); spResGrid.Children.Add(tPing);

            var tJit = BuildSpeedTile(NexLocale.T("net_speedtest_jitter", "JITTER"), $"{lastSpeedtestResult.JitterMs:0.0}", "ms", AmberBrush);
            Grid.SetColumn(tJit, 3); spResGrid.Children.Add(tJit);

            spStack.Children.Add(spResGrid);

            // Server & ISP row
            var srvRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
            srvRow.Children.Add(new TextBlock { Text = NexLocale.T("net_speedtest_isp", "Furnizor (ISP): "), FontSize = 11, Foreground = MutedBrush });
            srvRow.Children.Add(new TextBlock { Text = string.IsNullOrEmpty(lastSpeedtestResult.Isp) ? NexLocale.T("net_speedtest_auto_detected", "Detectat automat") : lastSpeedtestResult.Isp, FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = TextBrush, Margin = new Thickness(0, 0, 16, 0) });

            srvRow.Children.Add(new TextBlock { Text = NexLocale.T("net_speedtest_server", "Server: "), FontSize = 11, Foreground = MutedBrush });
            srvRow.Children.Add(new TextBlock { Text = $"{lastSpeedtestResult.ServerName} ({lastSpeedtestResult.ServerLocation})", FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = CyanBrush, Margin = new Thickness(0, 0, 16, 0) });
            spStack.Children.Add(srvRow);
        }
        else
        {
            var tDl = BuildSpeedTile(NexLocale.T("net_speedtest_download", "DOWNLOAD"), "--", "Mbps", CyanBrush);
            Grid.SetColumn(tDl, 0); spResGrid.Children.Add(tDl);

            var tUl = BuildSpeedTile(NexLocale.T("net_speedtest_upload", "UPLOAD"), "--", "Mbps", PurpleBrush);
            Grid.SetColumn(tUl, 1); spResGrid.Children.Add(tUl);

            var tPing = BuildSpeedTile(NexLocale.T("net_speedtest_ping", "LATENȚĂ PING"), "--", "ms", GreenBrush);
            Grid.SetColumn(tPing, 2); spResGrid.Children.Add(tPing);

            var tJit = BuildSpeedTile(NexLocale.T("net_speedtest_jitter", "JITTER"), "--", "ms", AmberBrush);
            Grid.SetColumn(tJit, 3); spResGrid.Children.Add(tJit);

            spStack.Children.Add(spResGrid);
        }

        speedCard.Child = spStack;
        PageRoot.Children.Add(speedCard);

        // 4. 1-Click Network Repair & Reset Tools Card
        var repCard = new Border
        {
            Background = CardBackground(),
            BorderBrush = new SolidColorBrush(Color.FromRgb(24, 38, 56)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 14, 16, 14)
        };
        var repStack = new StackPanel();
        var repHead = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        repHead.Children.Add(CreateVectorIcon(NexIcon.Clean, GreenBrush, 16));
        repHead.Children.Add(new TextBlock
        {
            Text = "  " + NexLocale.T("net_tools_header", "UNELTE REPARARE & RESETARE STIVĂ DE REȚEA (1-CLICK)"),
            FontSize = 12.5,
            FontWeight = FontWeights.Bold,
            Foreground = GreenBrush,
            VerticalAlignment = VerticalAlignment.Center
        });
        repStack.Children.Add(repHead);

        var toolWrap = new WrapPanel();

        toolWrap.Children.Add(MakeCardButton(NexLocale.T("net_btn_flush_dns", "Flush DNS Cache"), NexIcon.Clean, CyanBrush, async () =>
        {
            await NativeTuning.FlushDnsAsync();
            ShowToast(NexLocale.T("net_toast_flush_title", "DNS Cache Curățat"), NexLocale.T("net_toast_flush_msg", "Cache-ul resolverului DNS a fost golit."), NexIcon.Check, GreenBrush);
        }, false, 150));

        toolWrap.Children.Add(MakeCardButton(NexLocale.T("net_btn_reset_winsock", "Reset Winsock"), NexIcon.Revert, AmberBrush, async () =>
        {
            await NativeTuning.ResetWinsockAsync();
            ShowToast(NexLocale.T("net_toast_winsock_title", "Winsock Resetat"), NexLocale.T("net_toast_winsock_msg", "Catalogul Winsock a fost resetat la configurarea implicită."), NexIcon.Check, GreenBrush);
        }, false, 140));

        toolWrap.Children.Add(MakeCardButton(NexLocale.T("net_btn_reset_tcp", "Reset TCP/IP"), NexIcon.Refresh, PurpleBrush, async () =>
        {
            await NativeTuning.ResetTcpIpAsync();
            ShowToast(NexLocale.T("net_toast_tcp_title", "TCP/IP Resetat"), NexLocale.T("net_toast_tcp_msg", "Stiva TCP/IP a fost reconfigurată."), NexIcon.Check, GreenBrush);
        }, false, 140));

        toolWrap.Children.Add(MakeCardButton(NexLocale.T("net_btn_renew_ip", "Reînnoiește IP"), NexIcon.Pulse, GreenBrush, async () =>
        {
            await NativeTuning.RenewIpAsync();
            ShowToast(NexLocale.T("net_toast_ip_title", "Adresă IP Reînnoită"), NexLocale.T("net_toast_ip_msg", "Lease-ul DHCP a fost reînnoit."), NexIcon.Check, GreenBrush);
            ShowNetwork();
        }, false, 140));

        toolWrap.Children.Add(MakeCardButton(NexLocale.T("net_btn_dns_bench", "DNS Benchmark"), NexIcon.Network, PinkBrush, async () =>
        {
            await RunDnsBenchmarkAndShowModalAsync();
        }, true, 145));

        repStack.Children.Add(toolWrap);
        repCard.Child = repStack;
        PageRoot.Children.Add(repCard);

        Border BuildInfoTile(string label, string val, Brush valBrush)
        {
            var b = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(9, 15, 24)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(22, 34, 48)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 0, 10, 8)
            };
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock { Text = label, FontSize = 10, Foreground = MutedBrush });
            sp.Children.Add(new TextBlock { Text = val, FontSize = 13.5, FontWeight = FontWeights.Bold, Foreground = valBrush, Margin = new Thickness(0, 2, 0, 0) });
            b.Child = sp;
            return b;
        }
    }

}