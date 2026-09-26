using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace NexWin.Native;

public class LiveWallpaperWindow : Window
{
    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;
    private const int WS_CHILD = 0x40000000;
    private const int WS_POPUP = unchecked((int)0x80000000);
    private const int WS_VISIBLE = 0x10000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;

    private const int WM_NCHITTEST = 0x0084;
    private const int HTTRANSPARENT = -1;

    private static readonly IntPtr HWND_TOP = IntPtr.Zero;
    private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
    private const int WS_CLIPSIBLINGS = 0x04000000;
    private const int WS_CLIPCHILDREN = 0x02000000;
    private const uint WM_CLOSE = 0x0010;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_FRAMECHANGED = 0x0020;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const int SW_HIDE = 0;
    private const int SW_SHOW = 5;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [DllImport("user32.dll")]
    private static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string className, string? windowTitle);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, UIntPtr wParam, IntPtr lParam, uint fuFlags, uint uTimeout, out UIntPtr lpdwResult);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnableWindow(IntPtr hWnd, bool bEnable);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    // ── Multi-monitor enumeration ──────────────────────────────────────────
    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, EnumMonitorsDelegate lpfnEnum, IntPtr dwData);

    private delegate bool EnumMonitorsDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfoEx(IntPtr hMonitor, ref MONITORINFOEX lpmi);

    public class MonitorBounds
    {
        public int Left, Top, Right, Bottom;
        public string DeviceName = "";
        public bool IsPrimary;
        public int Width => Right - Left;
        public int Height => Bottom - Top;
        public override string ToString() => $"{DeviceName} {Width}×{Height} @({Left},{Top}){(IsPrimary ? " [Primary]" : "")}";
    }

    /// <summary>Enumerate all connected monitors with their physical pixel bounds.</summary>
    public static List<MonitorBounds> GetAllMonitors()
    {
        var monitors = new List<MonitorBounds>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData) =>
        {
            var info = new MONITORINFOEX();
            info.cbSize = Marshal.SizeOf<MONITORINFOEX>();
            if (GetMonitorInfoEx(hMonitor, ref info))
            {
                monitors.Add(new MonitorBounds
                {
                    Left = info.rcMonitor.Left,
                    Top = info.rcMonitor.Top,
                    Right = info.rcMonitor.Right,
                    Bottom = info.rcMonitor.Bottom,
                    DeviceName = info.szDevice ?? "",
                    IsPrimary = (info.dwFlags & 1) != 0
                });
            }
            return true;
        }, IntPtr.Zero);
        // Sort: primary first, then by Left position
        monitors.Sort((a, b) =>
        {
            if (a.IsPrimary != b.IsPrimary) return a.IsPrimary ? -1 : 1;
            return a.Left.CompareTo(b.Left);
        });
        return monitors;
    }

    // ── Monitor mode ───────────────────────────────────────────────────────
    public static LiveWallpaperWindow? Instance => _instance;
    public static string CurrentMode { get; private set; } = "individual";

    public static void SetDisplayMode(string mode)
    {
        CurrentMode = mode;
        NativeTuning.SetLiveWallpaperMonitorMode(mode);
        if (_instance != null && _instance.IsLoaded)
        {
            _instance.UpdateDisplayMode(mode);
        }
    }

    private static LiveWallpaperWindow? _instance;
    private static IntPtr _hiddenWorkerW = IntPtr.Zero;
    public static bool IsRunning => _instance != null && _instance.IsLoaded;
    public static IntPtr ActiveHandle => _instance != null ? new WindowInteropHelper(_instance).Handle : IntPtr.Zero;
    public static string CurrentPresetOrFile { get; private set; } = string.Empty;
    public static bool IsAutoPauseEnabled { get; set; } = true;
    public static bool IsPausedForGame { get; private set; } = false;

    private readonly Grid _rootGrid;
    private WebView2? _webView;
    private DispatcherTimer? _gifTimer;
    private DispatcherTimer? _gameWatcherTimer;
    private readonly List<BitmapSource> _gifFrames = new();
    private readonly List<int> _gifDelays = new();
    private readonly List<Image> _gifImages = new();
    private int _currentGifIndex = 0;
    private bool _isAttached = false;

    private static void Log(string message)
    {
        try
        {
            string dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NexWin");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            string file = System.IO.Path.Combine(dir, "live_wallpaper.log");
            System.IO.File.AppendAllText(file, $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch { }
    }

    public LiveWallpaperWindow(string filePath)
    {
        CurrentPresetOrFile = filePath;
        CurrentMode = NativeTuning.GetLiveWallpaperMonitorMode();
        Log($"Initializing LiveWallpaperWindow for: {filePath} [Mode: {CurrentMode}]");

        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        AllowsTransparency = false;
        Background = Brushes.Black;
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        _rootGrid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Background = Brushes.Black
        };
        Content = _rootGrid;

        if (File.Exists(filePath))
        {
            string ext = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
            if (ext == ".gif")
            {
                SetupNativeGifPlayback(filePath);
            }
            else
            {
                SetupChromiumVideoPlayback(filePath);
            }
        }

        Loaded += (_, _) =>
        {
            if (!_isAttached)
            {
                AttachBehindDesktopIcons();
            }
            if (_gameWatcherTimer == null)
            {
                _gameWatcherTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
                _gameWatcherTimer.Tick += (_, _) => CheckForegroundForGamePause();
                _gameWatcherTimer.Start();
            }
        };

        Closed += (_, _) =>
        {
            Log("LiveWallpaperWindow closed.");
            DisposeCurrentMedia();
            _instance = null;
            NativeTuning.TrimWorkingSet();
        };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        try
        {
            var helper = new WindowInteropHelper(this);
            var source = HwndSource.FromHwnd(helper.Handle);
            source?.AddHook(WndProc);

            int exStyle = GetWindowLong(helper.Handle, GWL_EXSTYLE);
            SetWindowLong(helper.Handle, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
        }
        catch (Exception ex)
        {
            Log($"OnSourceInitialized error: {ex.Message}");
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_NCHITTEST)
        {
            handled = true;
            return new IntPtr(HTTRANSPARENT);
        }
        return IntPtr.Zero;
    }

    private void DisposeCurrentMedia()
    {
        try
        {
            if (_webView != null)
            {
                _webView.Stop();
                _webView.Dispose();
                _webView = null;
            }

            if (_gifTimer != null)
            {
                _gifTimer.Stop();
                _gifTimer = null;
            }

            if (_gameWatcherTimer != null)
            {
                _gameWatcherTimer.Stop();
                _gameWatcherTimer = null;
            }

            _gifFrames.Clear();
            _gifDelays.Clear();
            _gifImages.Clear();
            _rootGrid.Children.Clear();
        }
        catch { }
    }

    public static void SetAutoPauseEnabled(bool enable)
    {
        IsAutoPauseEnabled = enable;
        if (!enable && _instance != null && IsPausedForGame)
        {
            _instance.ResumePlayback();
        }
    }

    public void PausePlayback()
    {
        try
        {
            if (IsPausedForGame) return;
            IsPausedForGame = true;
            Log("Game or FullScreen window detected: pausing LiveWallpaper (0% CPU & GPU).");

            if (_webView?.CoreWebView2 != null)
            {
                _webView.CoreWebView2.ExecuteScriptAsync("if (window.pauseVideo) { window.pauseVideo(); }");
            }

            if (_gifTimer != null)
            {
                _gifTimer.Stop();
            }

            NativeTuning.TrimWorkingSet();
        }
        catch (Exception ex)
        {
            Log($"PausePlayback error: {ex.Message}");
        }
    }

    public void ResumePlayback()
    {
        try
        {
            if (!IsPausedForGame) return;
            IsPausedForGame = false;
            Log("Desktop foreground resumed: unpausing LiveWallpaper.");

            if (_webView?.CoreWebView2 != null)
            {
                _webView.CoreWebView2.ExecuteScriptAsync("if (window.resumeVideo) { window.resumeVideo(); }");
            }

            if (_gifTimer != null)
            {
                _gifTimer.Start();
            }
        }
        catch (Exception ex)
        {
            Log($"ResumePlayback error: {ex.Message}");
        }
    }

    private void CheckForegroundForGamePause()
    {
        if (!IsAutoPauseEnabled)
        {
            if (IsPausedForGame) ResumePlayback();
            return;
        }

        try
        {
            IntPtr fg = GetForegroundWindow();
            if (fg == IntPtr.Zero) return;

            var sb = new StringBuilder(256);
            GetClassName(fg, sb, sb.Capacity);
            string className = sb.ToString();

            if (className == "Progman" || className == "WorkerW" || className == "Shell_TrayWnd" ||
                className == "Windows.UI.Core.CoreWindow" || className.Contains("Shell"))
            {
                if (IsPausedForGame) ResumePlayback();
                return;
            }

            GetWindowThreadProcessId(fg, out uint pid);
            if (pid == (uint)Environment.ProcessId)
            {
                if (IsPausedForGame) ResumePlayback();
                return;
            }

            if (GetWindowRect(fg, out RECT rect))
            {
                int fgW = rect.Right - rect.Left;
                int fgH = rect.Bottom - rect.Top;
                int screenW = (int)SystemParameters.PrimaryScreenWidth;
                int screenH = (int)SystemParameters.PrimaryScreenHeight;

                bool isFullScreen = (fgW >= (screenW - 5) && fgH >= (screenH - 5));

                if (isFullScreen)
                {
                    if (!IsPausedForGame) PausePlayback();
                    return;
                }
            }

            if (IsPausedForGame)
            {
                ResumePlayback();
            }
        }
        catch { }
    }

    public void SwitchVideo(string newFilePath)
    {
        try
        {
            CurrentPresetOrFile = newFilePath;
            Log($"SwitchVideo called in-place for: {newFilePath}");

            if (_webView?.CoreWebView2 != null)
            {
                string fileUri = new Uri(newFilePath).AbsoluteUri;
                string script = $"if (window.changeVideo) {{ window.changeVideo('{fileUri.Replace("'", "\\'")}'); }}";
                _webView.CoreWebView2.ExecuteScriptAsync(script);
                DisableChildInput();
                NativeTuning.TrimWorkingSet();
            }
            else
            {
                SetupChromiumVideoPlayback(newFilePath);
            }
        }
        catch (Exception ex)
        {
            Log($"SwitchVideo exception: {ex.Message}");
        }
    }

    private async void SetupChromiumVideoPlayback(string filePath)
    {
        try
        {
            Log($"Setting up Chromium WebView2 video playback for: {filePath}");
            DisposeCurrentMedia();

            _webView = new WebView2
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                DefaultBackgroundColor = System.Drawing.Color.Black
            };
            _rootGrid.Children.Add(_webView);

            string dataDir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NexWin",
                "WebView2_Wallpaper"
            );
            if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);

            // Lean Chromium arguments: strictly 1 renderer process, no telemetry or background network tasks
            string browserArgs = "--allow-file-access-from-files --disable-web-security --autoplay-policy=no-user-gesture-required " +
                                 "--disable-features=Translate,OptimizationHints,MediaRouter,DialMediaRouteProvider " +
                                 "--disable-background-networking --disable-sync --disable-extensions " +
                                 "--disable-component-update --disable-gpu-shader-disk-cache " +
                                 "--disable-background-timer-throttling --renderer-process-limit=1 " +
                                 "--enable-gpu-rasterization --enable-zero-copy";

            CoreWebView2Environment env;
            try
            {
                env = await CoreWebView2Environment.CreateAsync(
                    browserExecutableFolder: null,
                    userDataFolder: dataDir,
                    options: new CoreWebView2EnvironmentOptions(browserArgs)
                );
                await _webView.EnsureCoreWebView2Async(env);
            }
            catch (Exception lockEx)
            {
                Log($"Primary WebView2 folder locked ({lockEx.Message}), falling back to process-isolated profile.");
                dataDir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "NexWin",
                    $"WebView2_Wallpaper_{Environment.ProcessId}"
                );
                if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);

                env = await CoreWebView2Environment.CreateAsync(
                    browserExecutableFolder: null,
                    userDataFolder: dataDir,
                    options: new CoreWebView2EnvironmentOptions(browserArgs)
                );
                await _webView.EnsureCoreWebView2Async(env);
            }

            _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            _webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;

            string fileUri = new Uri(filePath).AbsoluteUri;

            string html = $@"<!DOCTYPE html>
<html>
<head>
<meta charset=""utf-8"">
<style>
  * {{ margin: 0; padding: 0; box-sizing: border-box; overflow: hidden; }}
  html, body {{ width: 100%; height: 100%; background: #000; overflow: hidden; position: relative; }}
  .video-layer {{ position: absolute; overflow: hidden; background: #000; }}
  video {{ width: 100%; height: 100%; object-fit: cover; pointer-events: none; display: block; }}
</style>
</head>
<body>
  <div id=""video-root"">
{BuildVideoContainersHtml(fileUri, CurrentMode)}
  </div>
  <script>
    function getAllVideos() {{
      return Array.from(document.querySelectorAll('video'));
    }}
    window.changeVideo = function(newSrc) {{
      getAllVideos().forEach(v => {{
        v.src = newSrc;
        v.currentTime = 0;
        v.play().catch(e => {{}});
      }});
    }};
    window.pauseVideo = function() {{
      getAllVideos().forEach(v => v.pause());
    }};
    window.resumeVideo = function() {{
      getAllVideos().forEach(v => v.play().catch(e => {{}}));
    }};
    window.setMode = function(htmlContent) {{
      const root = document.getElementById('video-root');
      if (root) {{
        root.innerHTML = htmlContent;
        getAllVideos().forEach(v => v.play().catch(e => {{}}));
      }}
    }};
  </script>
</body>
</html>";

            string htmlFile = System.IO.Path.Combine(dataDir, "video_player.html");
            await File.WriteAllTextAsync(htmlFile, html, Encoding.UTF8);

            _webView.CoreWebView2.NavigationCompleted += (s, e) =>
            {
                Log($"WebView2 NavigationCompleted: Success={e.IsSuccess}, Error={e.WebErrorStatus}");
                if (!_isAttached)
                {
                    AttachBehindDesktopIcons();
                }
                else
                {
                    EnsureDesktopIconsOnTop();
                }
                DisableChildInput();
            };

            var attachTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.0) };
            attachTimer.Tick += (s, e) =>
            {
                attachTimer.Stop();
                if (!_isAttached)
                {
                    Log("WebView2 safety timer fired, attaching behind desktop icons.");
                    AttachBehindDesktopIcons();
                }
                else
                {
                    EnsureDesktopIconsOnTop();
                }
                DisableChildInput();
            };
            attachTimer.Start();

            _webView.CoreWebView2.Navigate(new Uri(htmlFile).AbsoluteUri);
            Log("WebView2 navigated to video player.");
        }
        catch (Exception ex)
        {
            Log($"SetupChromiumVideoPlayback exception: {ex.Message} - stopping LiveWallpaper to prevent blank overlay.");
            StopLive();
        }
    }

    public static string BuildVideoContainersHtml(string fileUri, string mode)
    {
        var monitors = GetAllMonitors();
        if (monitors.Count <= 1 || mode.Equals("stretch", StringComparison.OrdinalIgnoreCase))
        {
            return $"    <div class=\"video-layer\" style=\"left:0;top:0;width:100%;height:100%;\"><video src=\"{fileUri}\" autoplay loop muted playsinline></video></div>";
        }

        int virtLeft = (int)SystemParameters.VirtualScreenLeft;
        int virtTop = (int)SystemParameters.VirtualScreenTop;

        if (int.TryParse(mode, out int specificIndex))
        {
            if (specificIndex >= 0 && specificIndex < monitors.Count)
            {
                var m = monitors[specificIndex];
                int relX = m.Left - virtLeft;
                int relY = m.Top - virtTop;
                return $"    <div class=\"video-layer\" style=\"left:{relX}px;top:{relY}px;width:{m.Width}px;height:{m.Height}px;\"><video src=\"{fileUri}\" autoplay loop muted playsinline></video></div>";
            }
        }

        var sb = new StringBuilder();
        foreach (var m in monitors)
        {
            int relX = m.Left - virtLeft;
            int relY = m.Top - virtTop;
            sb.AppendLine($"    <div class=\"video-layer\" style=\"left:{relX}px;top:{relY}px;width:{m.Width}px;height:{m.Height}px;\"><video src=\"{fileUri}\" autoplay loop muted playsinline></video></div>");
        }
        return sb.ToString();
    }

    public void UpdateDisplayMode(string newMode)
    {
        CurrentMode = newMode;
        NativeTuning.SetLiveWallpaperMonitorMode(newMode);
        Log($"UpdateDisplayMode called: {newMode}");

        if (_webView?.CoreWebView2 != null && !string.IsNullOrEmpty(CurrentPresetOrFile))
        {
            string fileUri = new Uri(CurrentPresetOrFile).AbsoluteUri;
            string containers = BuildVideoContainersHtml(fileUri, newMode);
            string escaped = containers.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\r", "").Replace("\n", "");
            _webView.CoreWebView2.ExecuteScriptAsync($"if (window.setMode) {{ window.setMode('{escaped}'); }}");
        }
        else if (_gifFrames.Count > 0)
        {
            SetupNativeGifLayout(newMode);
        }
    }

    private void SetupNativeGifPlayback(string filePath)
    {
        try
        {
            Log($"Setting up native GIF playback for: {filePath}");
            DisposeCurrentMedia();

            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var decoder = new GifBitmapDecoder(fs, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                foreach (var frame in decoder.Frames)
                {
                    int delayMs = 40;
                    if (frame.Metadata is BitmapMetadata meta && meta.ContainsQuery("/grctlext/Delay"))
                    {
                        var delayVal = meta.GetQuery("/grctlext/Delay");
                        if (delayVal is ushort us && us > 0)
                        {
                            delayMs = us * 10;
                        }
                    }
                    if (delayMs < 20) delayMs = 40;

                    var frozen = (BitmapSource)frame.CloneCurrentValue();
                    if (frozen.CanFreeze) frozen.Freeze();

                    _gifFrames.Add(frozen);
                    _gifDelays.Add(delayMs);
                }
            }

            if (_gifFrames.Count == 0) return;

            SetupNativeGifLayout(CurrentMode);

            _gifTimer = new DispatcherTimer(DispatcherPriority.Render);
            _gifTimer.Interval = TimeSpan.FromMilliseconds(_gifDelays[0]);
            _gifTimer.Tick += (s, e) =>
            {
                if (_gifFrames.Count == 0 || _gifImages.Count == 0) return;
                _currentGifIndex = (_currentGifIndex + 1) % _gifFrames.Count;
                var currentFrame = _gifFrames[_currentGifIndex];
                for (int i = 0; i < _gifImages.Count; i++)
                {
                    _gifImages[i].Source = currentFrame;
                }
                _gifTimer.Interval = TimeSpan.FromMilliseconds(_gifDelays[_currentGifIndex]);
            };
            _gifTimer.Start();
            Log("Native GIF render loop started.");
        }
        catch (Exception ex)
        {
            Log($"SetupNativeGifPlayback exception: {ex.Message}");
            StopLive();
        }
    }

    private void SetupNativeGifLayout(string mode)
    {
        _rootGrid.Children.Clear();
        _gifImages.Clear();
        if (_gifFrames.Count == 0) return;

        var monitors = GetAllMonitors();
        var currentFrame = _gifFrames[_currentGifIndex % _gifFrames.Count];

        if (monitors.Count <= 1 || mode.Equals("stretch", StringComparison.OrdinalIgnoreCase))
        {
            var gifImage = new Image
            {
                Stretch = Stretch.UniformToFill,
                Source = currentFrame,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            _gifImages.Add(gifImage);
            _rootGrid.Children.Add(gifImage);
            return;
        }

        var canvas = new Canvas
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ClipToBounds = true
        };

        int virtLeft = (int)SystemParameters.VirtualScreenLeft;
        int virtTop = (int)SystemParameters.VirtualScreenTop;

        if (int.TryParse(mode, out int specificIndex))
        {
            if (specificIndex >= 0 && specificIndex < monitors.Count)
            {
                var m = monitors[specificIndex];
                var gifImage = new Image
                {
                    Stretch = Stretch.UniformToFill,
                    Source = currentFrame,
                    Width = m.Width,
                    Height = m.Height
                };
                Canvas.SetLeft(gifImage, m.Left - virtLeft);
                Canvas.SetTop(gifImage, m.Top - virtTop);
                _gifImages.Add(gifImage);
                canvas.Children.Add(gifImage);
            }
        }
        else
        {
            foreach (var m in monitors)
            {
                var gifImage = new Image
                {
                    Stretch = Stretch.UniformToFill,
                    Source = currentFrame,
                    Width = m.Width,
                    Height = m.Height
                };
                Canvas.SetLeft(gifImage, m.Left - virtLeft);
                Canvas.SetTop(gifImage, m.Top - virtTop);
                _gifImages.Add(gifImage);
                canvas.Children.Add(gifImage);
            }
        }

        _rootGrid.Children.Add(canvas);
    }

    private static IntPtr FindProgmanHandle()
    {
        IntPtr progman = FindWindow("Progman", null);
        if (progman == IntPtr.Zero)
        {
            EnumWindows((topHwnd, _) =>
            {
                var sb = new StringBuilder(256);
                GetClassName(topHwnd, sb, sb.Capacity);
                if (sb.ToString() == "Progman")
                {
                    progman = topHwnd;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
        }
        return progman;
    }

    public static void EnsureDesktopIconsOnTop()
    {
        try
        {
            using (var advKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", true))
            {
                advKey?.SetValue("HideIcons", 0, Microsoft.Win32.RegistryValueKind.DWord);
            }
        }
        catch { }

        try
        {
            IntPtr progman = FindProgmanHandle();
            if (progman != IntPtr.Zero)
            {
                IntPtr defView = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (defView != IntPtr.Zero)
                {
                    ShowWindow(defView, SW_SHOW);
                    EnableWindow(defView, true);
                    SetWindowPos(defView, HWND_TOP, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);

                    IntPtr listView = FindWindowEx(defView, IntPtr.Zero, "SysListView32", null);
                    if (listView != IntPtr.Zero)
                    {
                        ShowWindow(listView, SW_SHOW);
                        EnableWindow(listView, true);
                    }
                }
            }

            EnumWindows((topHwnd, _) =>
            {
                IntPtr shell = FindWindowEx(topHwnd, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (shell != IntPtr.Zero)
                {
                    ShowWindow(shell, SW_SHOW);
                    EnableWindow(shell, true);
                    SetWindowPos(shell, HWND_TOP, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);

                    IntPtr lv = FindWindowEx(shell, IntPtr.Zero, "SysListView32", null);
                    if (lv != IntPtr.Zero)
                    {
                        ShowWindow(lv, SW_SHOW);
                        EnableWindow(lv, true);
                    }
                }
                return true;
            }, IntPtr.Zero);
        }
        catch { }
    }

    public static void RepairDesktopState(IntPtr keepLiveHwnd = default)
    {
        try
        {
            IntPtr progman = FindProgmanHandle();
            if (progman != IntPtr.Zero)
            {
                var childrenToClean = new List<IntPtr>();
                var workerWindows = new List<IntPtr>();

                EnumChildWindows(progman, (childHwnd, _) =>
                {
                    var sb = new StringBuilder(256);
                    GetClassName(childHwnd, sb, sb.Capacity);
                    string cls = sb.ToString();

                    if (cls.StartsWith("HwndWrapper", StringComparison.OrdinalIgnoreCase) && childHwnd != keepLiveHwnd)
                    {
                        childrenToClean.Add(childHwnd);
                    }
                    else if (cls == "WorkerW")
                    {
                        workerWindows.Add(childHwnd);
                    }
                    return true;
                }, IntPtr.Zero);

                foreach (var orphan in childrenToClean)
                {
                    try
                    {
                        Log($"Cleaning orphaned LiveWallpaper window from Progman: 0x{orphan.ToInt64():X}");
                        ShowWindow(orphan, SW_HIDE);
                        SetParent(orphan, IntPtr.Zero);
                        PostMessage(orphan, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                    }
                    catch { }
                }

                if (keepLiveHwnd == IntPtr.Zero)
                {
                    foreach (var w in workerWindows)
                    {
                        ShowWindow(w, SW_SHOW);
                        SetWindowPos(w, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
                        InvalidateRect(w, IntPtr.Zero, true);
                    }
                    if (_hiddenWorkerW != IntPtr.Zero)
                    {
                        ShowWindow(_hiddenWorkerW, SW_SHOW);
                        InvalidateRect(_hiddenWorkerW, IntPtr.Zero, true);
                        _hiddenWorkerW = IntPtr.Zero;
                    }
                }
            }

            EnsureDesktopIconsOnTop();
        }
        catch (Exception ex)
        {
            Log($"RepairDesktopState exception: {ex.Message}");
        }
    }

    public void AttachBehindDesktopIcons()
    {
        try
        {
            var helper = new WindowInteropHelper(this);
            IntPtr hWnd = helper.Handle;

            RepairDesktopState(hWnd);

            IntPtr progman = FindProgmanHandle();
            Log($"Progman handle: 0x{progman.ToInt64():X}");

            if (progman != IntPtr.Zero)
            {
                SendMessageTimeout(progman, 0x052C, UIntPtr.Zero, IntPtr.Zero, 0, 1000, out _);
            }

            IntPtr workerWInProgman = IntPtr.Zero;
            IntPtr defViewInProgman = IntPtr.Zero;

            if (progman != IntPtr.Zero)
            {
                for (int attempt = 0; attempt < 6; attempt++)
                {
                    workerWInProgman = FindWindowEx(progman, IntPtr.Zero, "WorkerW", null);
                    defViewInProgman = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
                    if (workerWInProgman != IntPtr.Zero || defViewInProgman != IntPtr.Zero)
                    {
                        break;
                    }
                    System.Threading.Thread.Sleep(40);
                }
            }

            int screenX = (int)SystemParameters.VirtualScreenLeft;
            int screenY = (int)SystemParameters.VirtualScreenTop;
            int screenW = (int)SystemParameters.VirtualScreenWidth;
            int screenH = (int)SystemParameters.VirtualScreenHeight;

            IntPtr targetParent = IntPtr.Zero;
            IntPtr insertAfter = IntPtr.Zero;

            if (defViewInProgman != IntPtr.Zero)
            {
                targetParent = progman;
                insertAfter = defViewInProgman;

                if (workerWInProgman != IntPtr.Zero)
                {
                    _hiddenWorkerW = workerWInProgman;
                    ShowWindow(workerWInProgman, SW_HIDE);
                }
            }
            else
            {
                EnumWindows((topHwnd, _) =>
                {
                    IntPtr shell = FindWindowEx(topHwnd, IntPtr.Zero, "SHELLDLL_DefView", null);
                    if (shell != IntPtr.Zero)
                    {
                        IntPtr worker = FindWindowEx(IntPtr.Zero, topHwnd, "WorkerW", null);
                        if (worker != IntPtr.Zero)
                        {
                            targetParent = worker;
                        }
                        else
                        {
                            targetParent = topHwnd;
                            insertAfter = shell;
                        }
                        return false;
                    }
                    return true;
                }, IntPtr.Zero);
            }

            if (targetParent != IntPtr.Zero)
            {
                int style = GetWindowLong(hWnd, GWL_STYLE);
                style = (style | WS_CHILD | WS_VISIBLE | WS_CLIPSIBLINGS | WS_CLIPCHILDREN) & ~WS_POPUP;
                SetWindowLong(hWnd, GWL_STYLE, style);

                int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
                exStyle = (exStyle | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE) & ~WS_EX_TRANSPARENT & ~WS_EX_LAYERED;
                SetWindowLong(hWnd, GWL_EXSTYLE, exStyle);

                SetParent(hWnd, targetParent);

                int finalX = screenX;
                int finalY = screenY;
                int finalW = screenW;
                int finalH = screenH;

                var pt = new POINT { X = screenX, Y = screenY };
                if (ScreenToClient(targetParent, ref pt))
                {
                    finalX = pt.X;
                    finalY = pt.Y;
                }

                if (GetWindowRect(targetParent, out RECT rcTarget))
                {
                    int pW = rcTarget.Right - rcTarget.Left;
                    int pH = rcTarget.Bottom - rcTarget.Top;
                    if (pW > 0 && pH > 0)
                    {
                        finalW = pW;
                        finalH = pH;
                    }
                }

                if (insertAfter != IntPtr.Zero)
                {
                    SetWindowPos(hWnd, insertAfter, finalX, finalY, finalW, finalH, SWP_NOACTIVATE | SWP_SHOWWINDOW | SWP_FRAMECHANGED);
                }
                else
                {
                    SetWindowPos(hWnd, HWND_BOTTOM, finalX, finalY, finalW, finalH, SWP_NOACTIVATE | SWP_SHOWWINDOW | SWP_FRAMECHANGED);
                }

                ShowWindow(hWnd, SW_SHOW);
                EnsureDesktopIconsOnTop();
                _isAttached = true;
                DisableChildInput();
                Log($"Desktop attachment completed. Pos: ({finalX},{finalY}) Size: {finalW}x{finalH}");
            }
            else
            {
                SetWindowPos(hWnd, HWND_BOTTOM, screenX, screenY, screenW, screenH, SWP_NOACTIVATE | SWP_SHOWWINDOW);
                EnsureDesktopIconsOnTop();
                _isAttached = true;
            }
        }
        catch (Exception ex)
        {
            Log($"AttachBehindDesktopIcons exception: {ex.Message}");
        }
    }

    public void DisableChildInput()
    {
        try
        {
            var helper = new WindowInteropHelper(this);
            IntPtr hWnd = helper.Handle;
            if (hWnd == IntPtr.Zero) return;

            EnumChildWindows(hWnd, (childHwnd, _) =>
            {
                EnableWindow(childHwnd, false);
                return true;
            }, IntPtr.Zero);
        }
        catch { }
    }

    public static bool StartLive(string filePath)
    {
        try
        {
            Log($"StartLive requested with: {filePath}");
            if (!File.Exists(filePath))
            {
                Log($"File not found for StartLive: {filePath}");
                return false;
            }

            NativeTuning.EnsureOriginalWallpaperSaved();
            string ext = System.IO.Path.GetExtension(filePath).ToLowerInvariant();

            // If already open and using WebView2, switch video in-place (no process recreation, 0 added RAM)
            if (_instance != null && _instance.IsLoaded && ext != ".gif" && _instance._webView?.CoreWebView2 != null)
            {
                Log($"Switching video in existing instance: {filePath}");
                _instance.SwitchVideo(filePath);
                EnsureDesktopIconsOnTop();
                return true;
            }

            StopLive();

            _instance = new LiveWallpaperWindow(filePath);
            _instance.Show();
            return true;
        }
        catch (Exception ex)
        {
            Log($"StartLive exception: {ex.Message}");
            StopLive();
            return false;
        }
    }

    public static void StopLive()
    {
        try
        {
            if (_instance != null)
            {
                Log("StopLive called.");
                try
                {
                    var h = new WindowInteropHelper(_instance).Handle;
                    if (h != IntPtr.Zero)
                    {
                        ShowWindow(h, SW_HIDE);
                        SetParent(h, IntPtr.Zero);
                    }
                }
                catch { }
                _instance.DisposeCurrentMedia();
                _instance.Close();
                _instance = null;
            }
            CurrentPresetOrFile = string.Empty;

            RepairDesktopState(IntPtr.Zero);
            NativeTuning.TrimWorkingSet();
        }
        catch (Exception ex)
        {
            Log($"StopLive exception: {ex.Message}");
            RepairDesktopState(IntPtr.Zero);
        }
    }
}
