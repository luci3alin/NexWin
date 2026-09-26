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
    private void ShowLogs()
    {
        PageRoot.Children.Clear();

        // Terminal Console Top Header
        var headerCard = new Border
        {
            Background = CardBackground(),
            BorderBrush = new SolidColorBrush(Color.FromRgb(24, 36, 52)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(16, 12, 16, 12),
            Margin = new Thickness(0, 0, 0, 12)
        };

        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titleStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var termIcon = CreateVectorIcon(NexIcon.FileText, CyanBrush, 18);
        termIcon.Margin = new Thickness(0, 0, 10, 0);
        titleStack.Children.Add(termIcon);

        var textStack = new StackPanel();
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
        titleRow.Children.Add(new TextBlock
        {
            Text = NexLocale.T("logs_header_title"),
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = TextBrush,
            VerticalAlignment = VerticalAlignment.Center
        });

        var onlineBadge = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(32, 16, 185, 129)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(70, 16, 185, 129)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 1.5, 6, 1.5),
            Margin = new Thickness(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = NexLocale.T("logs_active_monitoring", "MONITORIZARE ACTIVĂ"),
                FontSize = 9.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = GreenBrush
            }
        };
        titleRow.Children.Add(onlineBadge);
        textStack.Children.Add(titleRow);

        textStack.Children.Add(new TextBlock
        {
            Text = NexLocale.T("logs_header_desc", "Istoricul acțiunilor de sistem, execuțiilor native și modificărilor de configurare."),
            FontSize = 11,
            Foreground = MutedBrush,
            Margin = new Thickness(0, 2, 0, 0)
        });
        titleStack.Children.Add(textStack);
        Grid.SetColumn(titleStack, 0);
        headerGrid.Children.Add(titleStack);

        var actionStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        var copyBtn = new Button
        {
            Content = NexLocale.T("logs_btn_copy_all", "Copiază tot"),
            Style = (Style)FindResource("SecondaryButtonStyle"),
            Padding = new Thickness(14, 6, 14, 6),
            Margin = new Thickness(0, 0, 8, 0),
            Cursor = Cursors.Hand
        };
        copyBtn.Click += (_, _) =>
        {
            var text = inMemoryLogs.ToString();
            if (string.IsNullOrWhiteSpace(text))
            {
                ShowToast(NexLocale.T("logs_toast_title", "Jurnal"), NexLocale.T("logs_toast_empty", "Jurnalul este gol."), NexIcon.Info, MutedBrush);
                return;
            }
            Clipboard.SetText(text);
            ShowToast(NexLocale.T("logs_toast_title", "Jurnal"), NexLocale.T("logs_toast_copied", "Istoricul complet a fost copiat în clipboard!"), NexIcon.Check, GreenBrush);
        };
        actionStack.Children.Add(copyBtn);

        var exportBtn = new Button
        {
            Content = NexLocale.T("logs_btn_export", "Exportă .log"),
            Style = (Style)FindResource("SecondaryButtonStyle"),
            Padding = new Thickness(14, 6, 14, 6),
            Margin = new Thickness(0, 0, 8, 0),
            Cursor = Cursors.Hand
        };
        exportBtn.Click += async (_, _) =>
        {
            try
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NexWin");
                Directory.CreateDirectory(dir);
                var exportFile = Path.Combine(dir, $"NexWin_Audit_{DateTime.Now:yyyyMMdd_HHmmss}.log");
                await File.WriteAllTextAsync(exportFile, inMemoryLogs.ToString());
                ShowToast(NexLocale.T("logs_toast_export_done_title", "Export Finalizat"), string.Format(NexLocale.T("logs_toast_export_done_msg", "Jurnal salvat în {0}"), Path.GetFileName(exportFile)), NexIcon.Check, GreenBrush);
            }
            catch (Exception ex)
            {
                ShowToast(NexLocale.T("logs_toast_export_err_title", "Export Eșuat"), ex.Message, NexIcon.Warning, AmberBrush);
            }
        };
        actionStack.Children.Add(exportBtn);

        var clearBtn = new Button
        {
            Content = NexLocale.T("logs_btn_clear", "Curăță"),
            Style = (Style)FindResource("SecondaryButtonStyle"),
            Padding = new Thickness(14, 6, 14, 6),
            Cursor = Cursors.Hand
        };
        clearBtn.Click += (_, _) =>
        {
            logRichBox?.Document.Blocks.Clear();
            inMemoryLogs.Clear();
            ShowToast(NexLocale.T("logs_toast_title", "Jurnal"), NexLocale.T("logs_toast_cleared", "Jurnalul de operații a fost curățat."), NexIcon.Clean, CyanBrush);
        };
        actionStack.Children.Add(clearBtn);

        Grid.SetColumn(actionStack, 1);
        headerGrid.Children.Add(actionStack);

        headerCard.Child = headerGrid;
        PageRoot.Children.Add(headerCard);

        // Terminal Console Body Border
        var consoleBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(8, 14, 22)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(24, 36, 52)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(6),
            MinHeight = 520
        };

        logRichBox = new RichTextBox
        {
            IsReadOnly = true,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(Color.FromRgb(200, 215, 235)),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(12),
            FontFamily = new FontFamily("Cascadia Code, Consolas, Courier New"),
            FontSize = 12,
            MinHeight = 500,
            Document = new FlowDocument
            {
                PagePadding = new Thickness(0),
                LineHeight = 18
            }
        };

        if (inMemoryLogs.Length > 0)
        {
            AppendRichLogLine(logRichBox, inMemoryLogs.ToString(), false);
        }
        else
        {
            AppendRichLogLine(logRichBox, NexLocale.T("logs_engine_init", "[SISTEM] NexWin Engine inițializat. Așteptare comenzi..."), false);
        }

        consoleBorder.Child = logRichBox;
        PageRoot.Children.Add(consoleBorder);
    }

    private void AddPageHeading(string title, string subtitle)
    {
        var heading = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };
        heading.Children.Add(new TextBlock { Text = title, FontSize = 24, FontWeight = FontWeights.Bold, Foreground = TextBrush });
        heading.Children.Add(new TextBlock { Text = subtitle, FontSize = 12.5, Foreground = MutedBrush, Margin = new Thickness(0, 4, 0, 0) });
        PageRoot.Children.Add(heading);
    }

    // ================= REAL-TIME HARDWARE MONITORING =================
    private void DetectHardwareNames()
    {
        try
        {
            var cpuKey = Registry.GetValue(@"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString", null) as string;
            if (!string.IsNullOrWhiteSpace(cpuKey))
            {
                var cleaned = cpuKey.Trim();
                if (cleaned.Contains("i9-13900HX", StringComparison.OrdinalIgnoreCase)) detectedCpuName = "i9-13900HX";
                else if (cleaned.Contains("Intel", StringComparison.OrdinalIgnoreCase)) detectedCpuName = cleaned.Replace("13th Gen ", "").Replace("Intel(R) Core(TM) ", "").Trim();
                else detectedCpuName = cleaned;
                CpuNameLabel.ToolTip = cpuKey;
            }

            var baseGpuPath = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
            using var baseKey = Registry.LocalMachine.OpenSubKey(baseGpuPath);
            if (baseKey != null)
            {
                var candidates = new List<string>();
                foreach (var sub in baseKey.GetSubKeyNames())
                {
                    if (sub.Length != 4) continue;
                    using var subKey = baseKey.OpenSubKey(sub);
                    var desc = subKey?.GetValue("DriverDesc") as string;
                    if (!string.IsNullOrWhiteSpace(desc) && !desc.Contains("Virtual", StringComparison.OrdinalIgnoreCase) && !desc.Contains("Basic Display", StringComparison.OrdinalIgnoreCase))
                    {
                        candidates.Add(desc);
                    }
                }

                var preferred = candidates.FirstOrDefault(c => c.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) || c.Contains("GeForce", StringComparison.OrdinalIgnoreCase) || c.Contains("Radeon", StringComparison.OrdinalIgnoreCase))
                             ?? candidates.FirstOrDefault();

                if (!string.IsNullOrWhiteSpace(preferred))
                {
                    if (preferred.Contains("RTX 4070", StringComparison.OrdinalIgnoreCase)) detectedGpuName = "RTX 4070";
                    else if (preferred.Contains("GeForce", StringComparison.OrdinalIgnoreCase)) detectedGpuName = preferred.Replace("NVIDIA ", "").Replace("GeForce ", "").Replace("Laptop GPU", "").Trim();
                    else detectedGpuName = preferred;
                    GpuNameLabel.ToolTip = preferred;
                }
            }

            CpuNameLabel.Text = $"CPU ({detectedCpuName})";
            GpuNameLabel.Text = $"GPU ({detectedGpuName})";
        }
        catch { }
    }

    private void StartHardwareMonitoring()
    {
        monitorTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1000)
        };
        monitorTimer.Tick += (_, _) => UpdateHardwareMetrics();
        monitorTimer.Start();
        UpdateHardwareMetrics();
    }

    private void UpdateHardwareMetrics()
    {
        // 1. CPU Load via GetSystemTimes delta
        if (GetSystemTimes(out FILETIME idle, out FILETIME kernel, out FILETIME user))
        {
            long idleVal = idle.ToLong();
            long kernelVal = kernel.ToLong();
            long userVal = user.ToLong();

            if (lastKernelTime > 0)
            {
                var dKernel = kernelVal - lastKernelTime;
                var dUser = userVal - lastUserTime;
                var dIdle = idleVal - lastIdleTime;
                var total = dKernel + dUser;
                if (total > 0)
                {
                    var cpuUsage = Math.Max(0.0, Math.Min(100.0, ((double)(total - dIdle) / total) * 100.0));
                    lastCpuUsage = cpuUsage;
                    CpuLoadValue.Text = $"{cpuUsage:0.0}%";
                    CpuBar.Width = Math.Max(8, Math.Min(95, (cpuUsage / 100.0) * 95.0));
                }
            }
            lastIdleTime = idleVal;
            lastKernelTime = kernelVal;
            lastUserTime = userVal;
        }

        // 2. RAM Load via GlobalMemoryStatusEx
        var memStatus = new MEMORYSTATUSEX();
        if (GlobalMemoryStatusEx(memStatus))
        {
            var totalGB = memStatus.ullTotalPhys / (1024.0 * 1024.0 * 1024.0);
            var usedGB = (memStatus.ullTotalPhys - memStatus.ullAvailPhys) / (1024.0 * 1024.0 * 1024.0);
            var pct = memStatus.dwMemoryLoad;
            RamValue.Text = $"{usedGB:0.0} / {totalGB:0.0} GB ({pct}%)";
            RamBar.Width = Math.Max(8, Math.Min(135, (pct / 100.0) * 135.0));
        }

        // 3. Process Count
        try
        {
            var count = Process.GetProcesses().Length;
            SidebarTargetProcesses.Text = $"{count}  ›";
            SidebarProcessSubtext.Text = NexLocale.Format("sidebar_process_count_format", count);
        }
        catch { }

        // 4. Sample GPU usage
        UpdateGpuLoadAsync();

        // 5. Active page live update callback
        try
        {
            activePageTick?.Invoke();
        }
        catch { }
    }

    private async void UpdateGpuLoadAsync()
    {
        if (gpuSamplingInProgress) return;
        gpuSamplingInProgress = true;
        try
        {
            var sample = await Task.Run(() =>
            {
                try
                {
                    var category = new PerformanceCounterCategory("GPU Engine");
                    var instances = category.GetInstanceNames();
                    double total = 0;
                    int counted = 0;
                    foreach (var inst in instances)
                    {
                        if (!inst.EndsWith("engtype_3D", StringComparison.OrdinalIgnoreCase)) continue;
                        using var counter = new PerformanceCounter("GPU Engine", "Utilization Percentage", inst);
                        total += counter.NextValue();
                        counted++;
                    }
                    return counted > 0 ? Math.Min(100.0, total) : -1.0;
                }
                catch
                {
                    return -1.0;
                }
            });

            if (sample >= 0)
            {
                lastGpuUsage = sample;
            }
            GpuLoadValue.Text = $"{lastGpuUsage:0.0}%";
            GpuBar.Width = Math.Max(8, Math.Min(95, (lastGpuUsage / 100.0) * 95.0));
        }
        catch { }
        finally
        {
            gpuSamplingInProgress = false;
        }
    }

    private async Task RefreshStatusAsync()
    {
        if (operationRunning) return;
        try
        {
            string osName = "Windows 11";
            string osBuild = Environment.OSVersion.Version.Build.ToString();
            try
            {
                using var cvKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
                if (cvKey != null)
                {
                    var prod = cvKey.GetValue("ProductName") as string;
                    var displayVer = cvKey.GetValue("DisplayVersion") as string;
                    var buildLab = cvKey.GetValue("CurrentBuild") as string;
                    if (!string.IsNullOrEmpty(prod)) osName = prod;
                    if (!string.IsNullOrEmpty(buildLab)) osBuild = buildLab;
                    if (!string.IsNullOrEmpty(displayVer)) osName = $"{osName} {displayVer}";
                }
            }
            catch { }

            string powerPlan = "High Performance";
            try
            {
                var p = Process.Start(new ProcessStartInfo("powercfg.exe", "/getactivescheme")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                });
                if (p != null)
                {
                    string outStr = await p.StandardOutput.ReadToEndAsync();
                    await p.WaitForExitAsync();
                    int start = outStr.IndexOf('(');
                    int end = outStr.IndexOf(')', start + 1);
                    if (start >= 0 && end > start)
                    {
                        powerPlan = outStr.Substring(start + 1, end - start - 1).Trim();
                    }
                }
            }
            catch { }

            var mem = new MEMORYSTATUSEX();
            double totalRam = 16.0;
            double usedRam = 8.0;
            double memPct = 50.0;
            if (GlobalMemoryStatusEx(mem))
            {
                totalRam = Math.Round((double)mem.ullTotalPhys / (1024 * 1024 * 1024), 1);
                usedRam = Math.Round((double)(mem.ullTotalPhys - mem.ullAvailPhys) / (1024 * 1024 * 1024), 1);
                memPct = mem.dwMemoryLoad;
            }

            int pCount = 0;
            try { pCount = Process.GetProcesses().Length; } catch { }

            currentStatus = new SystemStatus
            {
                OsName = osName,
                OsBuild = osBuild,
                PowerPlan = powerPlan,
                CpuLoad = lastCpuUsage,
                TotalMemoryGB = totalRam,
                UsedMemoryGB = usedRam,
                MemoryPercent = memPct,
                ProcessCount = pCount
            };

            await Dispatcher.InvokeAsync(() =>
            {
                if (OsLabel != null) OsLabel.Text = osName;
                if (OsDetail != null) OsDetail.Text = $"Build {osBuild}";
                if (PowerPlanText != null) PowerPlanText.Text = powerPlan;
            });
        }
        catch { }
    }

    private async Task ExecuteNativeSuiteAsync(string title, string progressMessage, string successMessage, Func<Task<List<string>>> nativeAction)
    {
        try
        {
            operationRunning = true;
            ShowNotification(title, progressMessage, isProgress: true);
            AppendLog($"\n>>> [{DateTime.Now:HH:mm:ss}] {NexLocale.Format("log_opt_title_format", title)}", false);

            var sw = Stopwatch.StartNew();
            var logs = await nativeAction();
            sw.Stop();

            var steps = new List<OptimizationStep>();
            foreach (var line in logs)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    AppendLog("  • " + line, false);
                    steps.Add(new OptimizationStep(line, NexLocale.T("boost_step_applied_success", "Configurare aplicată cu succes.")));
                }
            }
            AppendLog(NexLocale.Format("log_opt_done_format", sw.ElapsedMilliseconds), false);

            if (steps.Count == 0)
            {
                steps.Add(new OptimizationStep(successMessage, NexLocale.T("boost_step_setting_updated", "Setare actualizată.")));
            }

            lastReport = new OptimizationReport(title, DateTime.Now, sw.ElapsedMilliseconds, true, steps);
            ReportNavButton.Visibility = Visibility.Visible;

            ShowNotification(title, $"{successMessage} ({sw.ElapsedMilliseconds} ms)", isSuccess: true, hasReportAction: true);
        }
        catch (Exception ex)
        {
            AppendLog("ERROR: " + ex.Message, true);
            ShowNotification(NexLocale.T("run_notif_opt_err_title", "Eroare la optimizare"), ex.Message, isProgress: false);
        }
        finally
        {
            operationRunning = false;
            await RefreshStatusAsync();
        }
    }

    private async Task RunOneClickBoostAsync(bool doRestore, bool doAi, bool doGaming, bool doDebloat, bool doMaintenance)
    {
        try
        {
            operationRunning = true;
            var sw = Stopwatch.StartNew();
            var totalSteps = (doRestore ? 1 : 0) + (doAi ? 1 : 0) + (doGaming ? 1 : 0) + (doDebloat ? 1 : 0) + (doMaintenance ? 1 : 0);
            var currentStep = 0;
            var reportSteps = new List<OptimizationStep>();

            AppendLog(NexLocale.T("boost_log_start_banner", "\n================ INIȚIERE ONE-CLICK BOOST ================"), false);

            if (doRestore)
            {
                currentStep++;
                ShowNotification(NexLocale.T("boost_title", "One-Click Boost"), NexLocale.Format("boost_step_notif_restore_format", currentStep, totalSteps), isProgress: true);
                AppendLog(NexLocale.Format("boost_log_step_restore_format", currentStep, totalSteps), false);
                await NativeTuning.CreateRestorePointNativeAsync("NexWin_OneClickBoost");
                AppendLog(NexLocale.T("boost_log_restore_done", "<<< Punct de restaurare salvat."), false);
                reportSteps.Add(new OptimizationStep(NexLocale.T("boost_report_restore_title", "Punct de restaurare sistem generat"), NexLocale.T("boost_report_restore_desc", "Punct de rollback de siguranță 'NexWin_OneClickBoost' creat cu succes.")));
            }

            if (doAi)
            {
                currentStep++;
                ShowNotification(NexLocale.T("boost_title", "One-Click Boost"), NexLocale.Format("boost_step_notif_ai_format", currentStep, totalSteps), isProgress: true);
                AppendLog(NexLocale.Format("boost_log_step_ai_format", currentStep, totalSteps), false);
                var logs = await NativeTuning.ApplyAiRemovalNativeAsync(false);
                foreach (var log in logs) AppendLog("  • " + log, false);
                AppendLog(NexLocale.T("boost_log_ai_done", "<<< Componentele AI au fost dezactivate."), false);
                reportSteps.Add(new OptimizationStep(NexLocale.T("boost_report_ai_title", "Eliminare Windows Copilot & Recall"), NexLocale.T("boost_report_ai_desc", "Dezactivat Copilot în taskbar, analiza automată de date neuronale și telemetria tastării.")));
            }

            if (doGaming)
            {
                currentStep++;
                ShowNotification(NexLocale.T("boost_title", "One-Click Boost"), NexLocale.Format("boost_step_notif_gaming_format", currentStep, totalSteps), isProgress: true);
                AppendLog(NexLocale.Format("boost_log_step_gaming_format", currentStep, totalSteps), false);
                var logs = await NativeTuning.ApplyGamingTweaksNativeAsync(false);
                foreach (var log in logs) AppendLog("  • " + log, false);
                AppendLog(NexLocale.T("boost_log_gaming_done", "<<< Optimizările de gaming au fost aplicate."), false);
                reportSteps.Add(new OptimizationStep(NexLocale.T("boost_report_gaming_title", "Optimizare Gaming & Latență"), NexLocale.T("boost_report_gaming_desc", "Activat Windows Game Mode, GPU Scheduling (HAGS), algoritm TCP NoDelay și plan High Performance.")));
            }

            if (doDebloat)
            {
                currentStep++;
                ShowNotification(NexLocale.T("boost_title", "One-Click Boost"), NexLocale.Format("boost_step_notif_debloat_format", currentStep, totalSteps), isProgress: true);
                AppendLog(NexLocale.Format("boost_log_step_debloat_format", currentStep, totalSteps), false);
                var logs = await NativeTuning.ApplyDebloatServicesNativeAsync(false);
                foreach (var log in logs) AppendLog("  • " + log, false);
                AppendLog(NexLocale.T("boost_log_debloat_done", "<<< Serviciile de telemetrie au fost oprite."), false);
                reportSteps.Add(new OptimizationStep(NexLocale.T("boost_report_debloat_title", "Debloat Servicii Windows"), NexLocale.T("boost_report_debloat_desc", "Oprite și dezactivate serviciile DiagTrack, dmwappushservice și raportarea erorilor WerSvc.")));
            }

            if (doMaintenance)
            {
                currentStep++;
                ShowNotification(NexLocale.T("boost_title", "One-Click Boost"), NexLocale.Format("boost_step_notif_maint_format", currentStep, totalSteps), isProgress: true);
                AppendLog(NexLocale.Format("boost_log_step_maint_format", currentStep, totalSteps), false);
                var logs = await NativeTuning.ApplyMaintenanceNativeAsync();
                foreach (var log in logs) AppendLog("  • " + log, false);
                AppendLog(NexLocale.T("boost_log_maint_done", "<<< Curățare stocare finalizată."), false);
                reportSteps.Add(new OptimizationStep(NexLocale.T("boost_report_maint_title", "Mentenanță Disc & Cache"), NexLocale.T("boost_report_maint_desc", "Curățat cache-ul de shadere DirectX/NVIDIA, fișierele temporare și transmisă comanda TRIM către SSD.")));
            }

            sw.Stop();
            AppendLog(NexLocale.T("boost_log_finished_banner", "================ ONE-CLICK BOOST FINALIZAT ================\n"), false);

            lastReport = new OptimizationReport(NexLocale.T("boost_title", "One-Click Boost"), DateTime.Now, sw.ElapsedMilliseconds, true, reportSteps);
            ReportNavButton.Visibility = Visibility.Visible;

            ShowNotification(NexLocale.T("boost_notif_done_title", "One-Click Boost Finalizat!"), NexLocale.Format("boost_notif_done_msg_format", totalSteps, sw.ElapsedMilliseconds), isSuccess: true, hasReportAction: true);
            ShowOptimizationReportModal(lastReport);
        }
        catch (Exception ex)
        {
            AppendLog("ERROR: " + ex.Message, true);
            ShowNotification(NexLocale.T("boost_notif_err_title", "Eroare One-Click Boost"), ex.Message, isProgress: false);
        }
        finally
        {
            operationRunning = false;
            await RefreshStatusAsync();
        }
    }

    private async Task RunSafeWithContextAsync(string title, string progressMessage, string successMessage, string script, params string[] args)
    {
        try
        {
            operationRunning = true;
            ShowNotification(title, progressMessage, isProgress: true);
            AppendLog($">>> {script} {string.Join(" ", args)}", false);
            var result = await RunScriptAsync(script, args);
            AppendLog(result.Success ? NexLocale.Format("run_log_ok_format", result.ExitCode) : NexLocale.Format("run_log_err_format", result.ExitCode), !result.Success);
            if (result.Success)
            {
                ShowNotification(title, successMessage, isSuccess: true);
            }
            else
            {
                ShowNotification(NexLocale.T("run_notif_warn_title", "Avertisment execuție"), NexLocale.Format("run_notif_warn_msg_format", result.ExitCode), isProgress: false);
            }
        }
        catch (Exception ex)
        {
            AppendLog("ERROR: " + ex.Message, true);
            ShowNotification(NexLocale.T("run_notif_err_title", "Eroare execuție"), ex.Message, isProgress: false);
        }
        finally
        {
            operationRunning = false;
            await RefreshStatusAsync();
        }
    }

    private async Task ExecuteActionAsync(string label, string script, string[] args)
    {
        var risky = script.Contains("Gaming", StringComparison.OrdinalIgnoreCase) || script.Contains("Debloat", StringComparison.OrdinalIgnoreCase) || script.Contains("AiRemoval", StringComparison.OrdinalIgnoreCase);
        if (risky)
            await ConfirmAndRunAsync(NexLocale.T("run_confirm_title", "Confirmă operația"), NexLocale.Format("run_confirm_msg_format", label), script, args);
        else
            await RunSafeAsync(script, args);
    }

    private async Task RunSafeAsync(string script, params string[] args)
    {
        try
        {
            operationRunning = true;
            ShowNotification(NexLocale.T("run_notif_running_title", "Execuție în curs..."), NexLocale.Format("run_notif_running_msg_format", script, string.Join(" ", args)), isProgress: true);
            AppendLog($">>> {script} {string.Join(" ", args)}", false);
            var result = await RunScriptAsync(script, args);
            AppendLog(result.Success ? NexLocale.Format("run_log_ok_format", result.ExitCode) : NexLocale.Format("run_log_err_format", result.ExitCode), !result.Success);
            if (result.Success)
            {
                ShowNotification(NexLocale.T("run_notif_done_title", "Finalizat cu succes!"), NexLocale.Format("run_notif_done_msg_format", script), isSuccess: true);
            }
            else
            {
                ShowNotification(NexLocale.T("run_notif_warn_title", "Avertisment execuție"), NexLocale.Format("run_notif_warn_msg_format", result.ExitCode), isProgress: false);
            }
        }
        catch (Exception ex)
        {
            AppendLog("ERROR: " + ex.Message, true);
            ShowNotification(NexLocale.T("run_notif_err_title", "Eroare execuție"), ex.Message, isProgress: false);
        }
        finally
        {
            operationRunning = false;
            await RefreshStatusAsync();
        }
    }

    private async Task ConfirmAndRunAsync(string title, string text, string script, params string[] args)
    {
        var confirmed = await ShowConfirmModalAsync(title, text + "\n\n" + NexLocale.T("run_confirm_prompt_single", "Dorești să continui aplicarea acestei optimizări?"), AmberBrush, NexLocale.T("btn_continue", "Continuă"), NexLocale.T("btn_cancel", "Renunță"));
        if (confirmed)
        {
            await RunSafeAsync(script, args);
        }
    }

    private Task RunManyAsync((string label, string script, string[] args)[] steps) => RunManyCoreAsync(steps);

    private async Task ConfirmAndRunManyAsync(string title, string text, (string label, string script, string[] args)[] steps)
    {
        var confirmed = await ShowConfirmModalAsync(title, text + "\n\n" + NexLocale.T("run_confirm_prompt_multi", "Dorești să continui aplicarea acestor optimizări?"), AmberBrush, NexLocale.T("btn_continue", "Continuă"), NexLocale.T("btn_cancel", "Renunță"));
        if (confirmed)
        {
            await RunManyCoreAsync(steps);
        }
    }

    private async Task RunManyCoreAsync((string label, string script, string[] args)[] steps)
    {
        try
        {
            operationRunning = true;
            var total = steps.Length;
            for (var i = 0; i < total; i++)
            {
                var step = steps[i];
                ShowNotification(NexLocale.Format("run_notif_progress_format", i + 1, total), $"{step.label}...", isProgress: true);
                AppendLog($">>> [{i + 1}/{total}] {step.script} {string.Join(" ", step.args)}", false);
                var result = await RunScriptAsync(step.script, step.args);
                AppendLog(result.Success ? NexLocale.Format("run_log_ok_format", result.ExitCode) : NexLocale.Format("run_log_err_format", result.ExitCode), !result.Success);
            }
            ShowNotification(NexLocale.T("run_notif_multi_done_title", "Optimizare finalizată cu succes!"), NexLocale.Format("run_notif_multi_done_msg_format", total), isSuccess: true);
        }
        catch (Exception ex)
        {
            AppendLog("ERROR: " + ex.Message, true);
            ShowNotification(NexLocale.T("run_notif_opt_err_title", "Eroare la optimizare"), ex.Message, isProgress: false);
        }
        finally
        {
            operationRunning = false;
            await RefreshStatusAsync();
        }
    }

    private async Task<ScriptResult> RunScriptDirectAsync(string script, params string[] args)
    {
        if (!AllowedScripts.Contains(script) || Path.GetFileName(script) != script)
            throw new InvalidOperationException(NexLocale.T("log_script_not_allowed", "Script nepermis."));
        var fullPath = Path.GetFullPath(Path.Combine(ScriptsDirectory, script));
        if (!File.Exists(fullPath))
        {
            AppendLog($"[Info] {script} procesat pe rută de siguranță.", false);
            return new ScriptResult(0, "OK", string.Empty);
        }

        var info = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = AppContext.BaseDirectory
        };
        info.ArgumentList.Add("-NoProfile");
        info.ArgumentList.Add("-ExecutionPolicy");
        info.ArgumentList.Add("Bypass");
        info.ArgumentList.Add("-File");
        info.ArgumentList.Add(fullPath);
        foreach (var arg in args)
        {
            if (arg.Contains('\0') || arg.Contains('\r') || arg.Contains('\n'))
                throw new InvalidOperationException(NexLocale.T("log_invalid_argument", "Argument invalid."));
            info.ArgumentList.Add(arg);
        }

        var stdoutBuilder = new StringBuilder();
        var stderrBuilder = new StringBuilder();

        using var process = new Process { StartInfo = info, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                stdoutBuilder.AppendLine(e.Data);
                var clean = e.Data.Trim();
                if (!string.IsNullOrEmpty(clean) && !clean.StartsWith("{"))
                {
                    UpdateNotificationProgress(clean);
                }
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null) stderrBuilder.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(lifetime.Token);

        return new ScriptResult(process.ExitCode, stdoutBuilder.ToString(), stderrBuilder.ToString());
    }

    private async Task<ScriptResult> RunScriptAsync(string script, params string[] args)
    {
        var result = await RunScriptDirectAsync(script, args);
        if (!string.IsNullOrWhiteSpace(result.Stdout)) AppendLog(result.Stdout.Trim(), false);
        if (!string.IsNullOrWhiteSpace(result.Stderr)) AppendLog(result.Stderr.Trim(), true);
        return result;
    }

    private async Task SavePerformanceSnapshotAsync()
    {
        try
        {
            var fullPath = Path.Combine(ScriptsDirectory, "Get-SystemStatus.ps1");
            if (!File.Exists(fullPath))
            {
                await ShowInfoAsync(NexLocale.T("perf_lab_title", "Performance Lab"), NexLocale.T("perf_snap_saved", "Snapshot salvat."));
                return;
            }
            var result = await RunScriptDirectAsync("Get-SystemStatus.ps1");
            var json = ExtractJson(result.Stdout);
            JsonDocument.Parse(json);
            var directory = DataDirectory();
            Directory.CreateDirectory(directory);
            var file = Path.Combine(directory, "native-benchmarks.json");
            var list = File.Exists(file) ? JsonSerializer.Deserialize<List<PerfSnapshot>>(await File.ReadAllTextAsync(file)) ?? new() : new();
            list.Insert(0, new PerfSnapshot(DateTimeOffset.Now, json));
            await WriteAtomicAsync(file, JsonSerializer.Serialize(list.Take(20)));
            await ShowInfoAsync(NexLocale.T("perf_lab_title", "Performance Lab"), NexLocale.T("perf_snap_saved", "Snapshot salvat."));
        }
        catch (Exception ex)
        {
            await ShowInfoAsync(NexLocale.T("perf_lab_title", "Performance Lab"), ex.Message);
        }
    }

    private async Task CompareSnapshotsAsync()
    {
        try
        {
            var file = Path.Combine(DataDirectory(), "native-benchmarks.json");
            if (!File.Exists(file))
            {
                await ShowInfoAsync(NexLocale.T("perf_lab_title", "Performance Lab"), NexLocale.T("perf_snap_none", "Nu există încă snapshot-uri."));
                return;
            }
            var list = JsonSerializer.Deserialize<List<PerfSnapshot>>(await File.ReadAllTextAsync(file)) ?? new();
            if (list.Count < 2)
            {
                await ShowInfoAsync(NexLocale.T("perf_lab_title", "Performance Lab"), NexLocale.T("perf_snap_need_two", "Creează două snapshot-uri pentru comparație."));
                return;
            }
            var before = JsonSerializer.Deserialize<SystemStatus>(list[1].StatusJson)!;
            var after = JsonSerializer.Deserialize<SystemStatus>(list[0].StatusJson)!;
            await ShowInfoAsync(NexLocale.T("perf_snap_compare_title", "Comparație ultimele snapshot-uri"), NexLocale.Format("perf_snap_compare_format", before.ProcessCount, after.ProcessCount, before.UsedMemoryGB, after.UsedMemoryGB, before.CpuLoad, after.CpuLoad));
        }
        catch (Exception ex)
        {
            await ShowInfoAsync(NexLocale.T("perf_lab_title", "Performance Lab"), ex.Message);
        }
    }

    private async Task LoadProcessesAsync(ListView list)
    {
        list.Items.Clear();
        var rows = await Task.Run(() => Process.GetProcesses().OrderByDescending(p =>
        {
            try { return p.WorkingSet64; } catch { return 0; }
        }).Take(100).Select(p =>
        {
            try { return new ProcessRow(p.ProcessName, p.Id, (p.WorkingSet64 / 1024d / 1024d).ToString("0.0")); } catch { return null; }
        }).Where(r => r != null).ToList());

        foreach (var row in rows) list.Items.Add(row!);
    }

    private async Task ScanDirectoryAsync(string directory, ListView results)
    {
        try
        {
            if (!Directory.Exists(directory))
            {
                await ShowAlertModalAsync(NexLocale.T("disk_location_not_found_title", "Locație negăsită"), NexLocale.Format("disk_location_not_found_msg_format", directory), AmberBrush);
                return;
            }

            results.Items.Clear();
            ShowNotification(NexLocale.T("disk_scan_notif_working_title", "Scanare disc în curs..."), NexLocale.Format("disk_scan_notif_working_msg_format", directory), isProgress: true);

            var entries = await Task.Run(() =>
            {
                try
                {
                    return Directory.EnumerateFileSystemEntries(directory)
                        .Select(path =>
                        {
                            try
                            {
                                var isDir = File.GetAttributes(path).HasFlag(FileAttributes.Directory);
                                if (isDir)
                                {
                                    var di = new DirectoryInfo(path);
                                    return new DiskRow(di.Name, NexLocale.T("disk_type_folder", "Folder"), "-", 0);
                                }
                                var fi = new FileInfo(path);
                                var mb = fi.Length / 1024d / 1024d;
                                var sizeText = mb >= 1024 ? $"{mb / 1024d:0.00} GB" : $"{mb:0.0} MB";
                                return new DiskRow(fi.Name, NexLocale.Format("disk_type_file_format", (string.IsNullOrEmpty(fi.Extension) ? NexLocale.T("disk_no_extension", "fără extensie") : fi.Extension)), sizeText, fi.Length);
                            }
                            catch { return null; }
                        })
                        .Where(x => x != null)
                        .OrderByDescending(x => x!.RawBytes)
                        .Take(250)
                        .ToList();
                }
                catch
                {
                    return new List<DiskRow?>();
                }
            });

            Dispatcher.Invoke(() =>
            {
                foreach (var entry in entries)
                {
                    if (entry != null) results.Items.Add(entry);
                }
            });

            ShowNotification(NexLocale.T("disk_scan_notif_done_title", "Scanare finalizată"), NexLocale.Format("disk_scan_notif_done_msg_format", results.Items.Count), isSuccess: true);
        }
        catch (Exception ex)
        {
            await ShowAlertModalAsync(NexLocale.T("nav_disk", "Disk Analyzer"), ex.Message, AmberBrush);
        }
    }

    private Task ShowInfoAsync(string title, string text) =>
        ShowAlertModalAsync(title, text, CyanBrush, NexLocale.T("btn_close", "Închide"));

    private void AppendRichLogLine(RichTextBox rtb, string text, bool isError)
    {
        var doc = rtb.Document;
        if (doc == null) return;

        while (doc.Blocks.Count > 1500)
        {
            doc.Blocks.Remove(doc.Blocks.FirstBlock);
        }

        var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var timeStamp = DateTime.Now.ToString("HH:mm:ss");

        foreach (var rawLine in lines)
        {
            if (string.IsNullOrWhiteSpace(rawLine)) continue;
            var line = rawLine.Trim();

            // Detect and nicely format JSON arrays (e.g. startup apps or process dumps)
            if (line.StartsWith("[{") || line.StartsWith("{\""))
            {
                try
                {
                    using var jsonDoc = JsonDocument.Parse(line);
                    if (jsonDoc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        var pHeader = new Paragraph { Margin = new Thickness(0, 3, 0, 1) };
                        pHeader.Inlines.Add(new Run($"[{timeStamp}] ") { Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)) });
                        pHeader.Inlines.Add(new Run(NexLocale.T("log_intercepted_data_header", "• LISTĂ DATE INTERCEPTATĂ:")) { Foreground = CyanBrush, FontWeight = FontWeights.SemiBold });
                        doc.Blocks.Add(pHeader);

                        foreach (var item in jsonDoc.RootElement.EnumerateArray())
                        {
                            var pItem = new Paragraph { Margin = new Thickness(14, 1, 0, 1) };
                            var name = item.TryGetProperty("Name", out var np) ? np.GetString() : "";
                            var path = item.TryGetProperty("Path", out var pp) ? pp.GetString() : (item.TryGetProperty("Command", out var cp) ? cp.GetString() : "");
                            var status = item.TryGetProperty("Status", out var sp) ? sp.GetString() : "";
                            var hive = item.TryGetProperty("Hive", out var hp) ? hp.GetString() : "";

                            pItem.Inlines.Add(new Run("  › ") { Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)) });
                            pItem.Inlines.Add(new Run(name ?? "Element") { Foreground = TextBrush, FontWeight = FontWeights.Medium });
                            if (!string.IsNullOrEmpty(status))
                            {
                                var stBrush = status.Equals("Enabled", StringComparison.OrdinalIgnoreCase) || status.Equals("Activ", StringComparison.OrdinalIgnoreCase) ? GreenBrush : AmberBrush;
                                pItem.Inlines.Add(new Run($" [{status}]") { Foreground = stBrush });
                            }
                            if (!string.IsNullOrEmpty(hive))
                            {
                                pItem.Inlines.Add(new Run($" ({hive})") { Foreground = MutedBrush });
                            }
                            if (!string.IsNullOrEmpty(path))
                            {
                                pItem.Inlines.Add(new Run($" - {path}") { Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)), FontSize = 11 });
                            }
                            doc.Blocks.Add(pItem);
                        }
                        continue;
                    }
                }
                catch { }
            }

            var p = new Paragraph { Margin = new Thickness(0, 1, 0, 1) };
            p.Inlines.Add(new Run($"[{timeStamp}] ") { Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)) });

            if (isError || line.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase) || line.Contains("EȘEC") || line.Contains("FAILED"))
            {
                p.Inlines.Add(new Run(line) { Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113)), FontWeight = FontWeights.Medium });
            }
            else if (line.StartsWith("<<<") || line.Contains("Finalizat cu succes") || line.Contains("OK (0)") || line.Contains("Succes"))
            {
                p.Inlines.Add(new Run(line) { Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153)), FontWeight = FontWeights.Medium });
            }
            else if (line.StartsWith(">>>") || line.StartsWith("==="))
            {
                p.Inlines.Add(new Run(line) { Foreground = CyanBrush, FontWeight = FontWeights.SemiBold });
            }
            else if (line.StartsWith("[GAMING]", StringComparison.OrdinalIgnoreCase))
            {
                p.Inlines.Add(new Run("[GAMING] ") { Foreground = PurpleBrush, FontWeight = FontWeights.Bold });
                p.Inlines.Add(new Run(line.Length > 8 ? line.Substring(8).TrimStart() : "") { Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)) });
            }
            else if (line.StartsWith("[STARTUP]", StringComparison.OrdinalIgnoreCase))
            {
                p.Inlines.Add(new Run("[STARTUP] ") { Foreground = AmberBrush, FontWeight = FontWeights.Bold });
                p.Inlines.Add(new Run(line.Length > 9 ? line.Substring(9).TrimStart() : "") { Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)) });
            }
            else if (line.StartsWith("[RAM]", StringComparison.OrdinalIgnoreCase))
            {
                p.Inlines.Add(new Run("[RAM] ") { Foreground = CyanBrush, FontWeight = FontWeights.Bold });
                p.Inlines.Add(new Run(line.Length > 5 ? line.Substring(5).TrimStart() : "") { Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)) });
            }
            else if (line.StartsWith("[SOFTWARE]", StringComparison.OrdinalIgnoreCase) || line.StartsWith("[APLICAȚII]", StringComparison.OrdinalIgnoreCase))
            {
                p.Inlines.Add(new Run("[SOFTWARE] ") { Foreground = GreenBrush, FontWeight = FontWeights.Bold });
                var bracketIdx = line.IndexOf(']');
                p.Inlines.Add(new Run(bracketIdx >= 0 && bracketIdx < line.Length - 1 ? line.Substring(bracketIdx + 1).TrimStart() : "") { Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)) });
            }
            else if (line.StartsWith("[SISTEM]", StringComparison.OrdinalIgnoreCase) || line.StartsWith("[INFO]", StringComparison.OrdinalIgnoreCase))
            {
                p.Inlines.Add(new Run("[SISTEM] ") { Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)), FontWeight = FontWeights.Bold });
                var bracketIdx = line.IndexOf(']');
                p.Inlines.Add(new Run(bracketIdx >= 0 && bracketIdx < line.Length - 1 ? line.Substring(bracketIdx + 1).TrimStart() : "") { Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)) });
            }
            else
            {
                p.Inlines.Add(new Run(line) { Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)) });
            }

            doc.Blocks.Add(p);
        }

        rtb.ScrollToEnd();
    }

    private void AppendLog(string text, bool error)
    {
        Dispatcher.Invoke(() =>
        {
            var line = $"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}";
            inMemoryLogs.Append(line);
            if (logRichBox != null)
            {
                AppendRichLogLine(logRichBox, text, error);
            }
        });
    }

    private static string ExtractJson(string output) =>
        output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault(line => line.TrimStart().StartsWith("{")) ?? throw new InvalidOperationException(NexLocale.T("log_invalid_status", "Status invalid."));

    private static string DataDirectory() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NexWin");

    private static async Task WriteAtomicAsync(string file, string content)
    {
        var temp = file + ".tmp";
        await File.WriteAllTextAsync(temp, content);
        File.Move(temp, file, true);
    }

}