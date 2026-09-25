namespace NexWin.Native;

internal static class Bootstrap
{
    [STAThread]
    private static void Main(string[] args)
    {
        bool isScreenshot = args.Any(a => a.Equals("--screenshot", StringComparison.OrdinalIgnoreCase) || a.Equals("--screenshot-page", StringComparison.OrdinalIgnoreCase));
        bool isTray = args.Any(a => a.Equals("--tray", StringComparison.OrdinalIgnoreCase));
        if (!isScreenshot && !isTray && !IsAdministrator() && !args.Any(a => a.Equals("--no-elevate", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = Environment.ProcessPath ?? "NexWin.exe",
                    UseShellExecute = true,
                    Verb = "runas",
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
                };
                foreach (var arg in args) psi.ArgumentList.Add(arg);
                System.Diagnostics.Process.Start(psi);
                return;
            }
            catch
            {
                // Daca utilizatorul a refuzat dialogul UAC, se continua rularea in mod standard
            }
        }

        if (!isScreenshot)
        {
            try
            {
                int currentPid = Environment.ProcessId;
                foreach (var proc in System.Diagnostics.Process.GetProcessesByName("NexWin"))
                {
                    if (proc.Id != currentPid)
                    {
                        try { proc.Kill(); proc.WaitForExit(1500); } catch { }
                    }
                }
            }
            catch { }
            LiveWallpaperWindow.RepairDesktopState(IntPtr.Zero);
        }

        var application = new System.Windows.Application();

        string? langArg = null;
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals("--lang", StringComparison.OrdinalIgnoreCase))
            {
                langArg = args[i + 1];
                break;
            }
        }
        if (!string.IsNullOrEmpty(langArg))
        {
            NexLocale.SetLanguage(langArg.Equals("en", StringComparison.OrdinalIgnoreCase) ? AppLanguage.En : AppLanguage.Ro);
        }

        if (isScreenshot)
        {
            bool isMaximized = args.Any(a => a.Equals("--maximized", StringComparison.OrdinalIgnoreCase));
            var win = new MainWindow();
            win.Width = 1360;
            win.Height = 860;
            if (isMaximized) win.WindowState = System.Windows.WindowState.Maximized;
            win.Show();

            int pageIdx = Array.FindIndex(args, a => a.Equals("--screenshot-page", StringComparison.OrdinalIgnoreCase));
            if (pageIdx >= 0 && pageIdx + 1 < args.Length)
            {
                string targetPage = args[pageIdx + 1];
                if (targetPage.Equals("ActivatorMethodModal", StringComparison.OrdinalIgnoreCase))
                {
                    win.NavigateTo("Activator");
                    _ = win.PromptActivationMethodAsync("windows");
                }
                else if (targetPage.Equals("Search", StringComparison.OrdinalIgnoreCase) || targetPage.Equals("GlobalSearch", StringComparison.OrdinalIgnoreCase))
                {
                    win.ShowGlobalSearchModal();
                }
                else if (targetPage.Equals("SupportModal", StringComparison.OrdinalIgnoreCase))
                {
                    win.ShowSupportProjectModal(1, false);
                }
                else if (targetPage.Equals("SupportModalDemo", StringComparison.OrdinalIgnoreCase))
                {
                    win.ShowSupportProjectModal(1, true);
                }
                else
                {
                    win.NavigateTo(targetPage);
                }
            }

            int delayMs = 600;
            if (args.Length > 3 && int.TryParse(args[3], out int customDelay))
            {
                delayMs = customDelay;
            }

            // Run dispatcher frame to allow Window_Loaded, background queries, and rendering to complete
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(delayMs) };
            var frame = new System.Windows.Threading.DispatcherFrame();
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                frame.Continue = false;
            };
            timer.Start();
            System.Windows.Threading.Dispatcher.PushFrame(frame);

            int renderW = isMaximized ? (int)Math.Max(1360, win.ActualWidth > 0 ? win.ActualWidth : 1920) : 1360;
            int renderH = isMaximized ? (int)Math.Max(860, win.ActualHeight > 0 ? win.ActualHeight : 1040) : 860;
            win.Measure(new System.Windows.Size(renderW, renderH));
            win.Arrange(new System.Windows.Rect(0, 0, renderW, renderH));
            win.UpdateLayout();

            var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(renderW, renderH, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            rtb.Render(win);

            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
            var outPath = "qa-exact-render.png";
            int outIdx = Array.FindIndex(args, a => a.Equals("--out", StringComparison.OrdinalIgnoreCase));
            if (outIdx >= 0 && outIdx + 1 < args.Length)
            {
                outPath = args[outIdx + 1];
            }
            else if (args.Length > 2 && !args[2].StartsWith("--"))
            {
                outPath = args[2];
            }
            else if (args.Length > 1 && args[0] == "--screenshot")
            {
                outPath = args[1];
            }
            using var stream = System.IO.File.Create(outPath);
            encoder.Save(stream);
            Console.WriteLine($"Screenshot saved to {outPath}");
            win.Close();
            return;
        }

        NativeTuning.RegisterProtocolHandler();
        EnsureRealtimeDonationServerStarted();

        var mainWindow = new MainWindow { StartInTray = isTray };
        if (args.Length > 0)
        {
            string rawArg = args[0];
            if (rawArg.StartsWith("nexwin://", StringComparison.OrdinalIgnoreCase) || rawArg.Equals("--updates", StringComparison.OrdinalIgnoreCase))
            {
                mainWindow.NavigateTo("AppsUpdates");
            }
            else if (rawArg.Equals("--page", StringComparison.OrdinalIgnoreCase) && args.Length > 1)
            {
                string p = args[1].Replace("nexwin://", "").Trim('/');
                if (p.Equals("updates", StringComparison.OrdinalIgnoreCase)) p = "AppsUpdates";
                mainWindow.NavigateTo(p);
            }
        }
        application.Run(mainWindow);
    }

    private static void EnsureRealtimeDonationServerStarted()
    {
        try
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] candidateScripts =
            {
                System.IO.Path.Combine(baseDir, "server", "index.js"),
                System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, "..", "..", "..", "..", "server", "index.js"))
            };
            string? scriptPath = candidateScripts.FirstOrDefault(System.IO.File.Exists);
            if (string.IsNullOrEmpty(scriptPath)) return;

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "node",
                Arguments = $"\"{scriptPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = System.IO.Path.GetDirectoryName(scriptPath) ?? baseDir
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch { }
    }

    private static bool IsAdministrator()
    {
        try
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }
}
