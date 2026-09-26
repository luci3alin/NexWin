using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows.Documents;
using System.Windows.Interop;
using Microsoft.Win32;
using Button = System.Windows.Controls.Button;
using ListView = System.Windows.Controls.ListView;
using TextBox = System.Windows.Controls.TextBox;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Path = System.IO.Path;
using Ellipse = System.Windows.Shapes.Ellipse;

namespace NexWin.Native;

public sealed partial class MainWindow : Window
{
    public static readonly DependencyProperty IsNavActiveProperty =
        DependencyProperty.RegisterAttached("IsNavActive", typeof(bool), typeof(MainWindow), new PropertyMetadata(false));

    public static void SetIsNavActive(UIElement element, bool value) => element.SetValue(IsNavActiveProperty, value);
    public static bool GetIsNavActive(UIElement element) => (bool)element.GetValue(IsNavActiveProperty);

    private readonly Brush BackgroundBrush = new SolidColorBrush(Color.FromRgb(7, 13, 20));
    private readonly Brush CardBrush = new SolidColorBrush(Color.FromRgb(11, 18, 28));
    private readonly Brush TextBrush = new SolidColorBrush(Color.FromRgb(255, 255, 255));
    private readonly Brush MutedBrush = new SolidColorBrush(Color.FromRgb(126, 142, 158));
    private readonly Brush PinkBrush = new SolidColorBrush(Color.FromRgb(255, 42, 133));
    private readonly Brush CyanBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248));
    private readonly Brush GreenBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129));
    private readonly Brush AmberBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11));
    private readonly Brush PurpleBrush = new SolidColorBrush(Color.FromRgb(168, 85, 247));
    private readonly Brush RedBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68));

    private readonly string ScriptsDirectory = Path.Combine(AppContext.BaseDirectory, "scripts", "powershell");
    private readonly HashSet<string> AllowedScripts = new(StringComparer.OrdinalIgnoreCase)
    {
        "Get-SystemStatus.ps1", "Get-ProcessList.ps1", "Get-StartupApps.ps1", "Get-DiskAnalyzer.ps1",
        "Invoke-DnsBenchmark.ps1", "Invoke-AiRemoval.ps1", "Invoke-GamingTweaks.ps1", "Invoke-UsbOptimization.ps1",
        "Invoke-GpuDriverProtection.ps1", "Invoke-DebloatServices.ps1", "Invoke-ProcessLasso.ps1", "Invoke-ClassicApps.ps1",
        "Invoke-VisualEffects.ps1", "Invoke-Maintenance.ps1", "Invoke-RestorePoint.ps1", "Invoke-OneClickBoost.ps1",
        "Stop-ProcessSafe.ps1", "Save-NexWinSnapshot.ps1", "Restore-NexWinSnapshot.ps1"
    };

    private readonly CancellationTokenSource lifetime = new();
    private readonly StringBuilder inMemoryLogs = new();
    private Button? activeNav;
    private StackPanel? cardGrid;
    private bool operationRunning;
    private SystemStatus? currentStatus;
    private RichTextBox? logRichBox;

    // Optimization Report and Step Tracking
    public class OptimizationStep
    {
        public string Title { get; set; } = "";
        public string Details { get; set; } = "";
        public bool Success { get; set; } = true;

        public OptimizationStep() { }
        public OptimizationStep(string title, string details, bool success = true)
        {
            Title = title;
            Details = details;
            Success = success;
        }
    }

    public class OptimizationReport
    {
        public string SuiteName { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public long TotalDurationMs { get; set; }
        public bool Success { get; set; } = true;
        public List<OptimizationStep> Steps { get; set; } = new();

        public OptimizationReport() { }
        public OptimizationReport(string suiteName, DateTime timestamp, long durationMs, bool success, List<OptimizationStep> steps)
        {
            SuiteName = suiteName;
            Timestamp = timestamp;
            TotalDurationMs = durationMs;
            Success = success;
            Steps = steps;
        }
    }

    public enum NexIcon
    {
        Shield,
        Gamepad,
        Gauge,
        Sliders,
        Disk,
        Cpu,
        Rocket,
        Window,
        Network,
        Revert,
        Check,
        Clean,
        Bolt,
        Warning,
        Info,
        Search,
        Power,
        Layers,
        FileText,
        Photo,
        Paint,
        Refresh,
        Audio,
        Pulse,
        Filter,
        Trash,
        Folder,
        Download,
        Thermometer,
        Memory,
        Flame,
        Save,
        Play,
        Gpu,
        Fan,
        Motherboard,
        Chip,
        X,
        Bell
    }

    public static FrameworkElement CreateVectorIcon(NexIcon icon, Brush fill, double size = 18)
    {
        var pathData = icon switch
        {
            NexIcon.Bell => "M12 22c1.1 0 2-.9 2-2h-4c0 1.1.9 2 2 2zm6-6v-5c0-3.07-1.63-5.64-4.5-6.32V4c0-.83-.67-1.5-1.5-1.5s-1.5.67-1.5 1.5v.68C7.64 5.36 6 7.92 6 11v5l-2 2v1h16v-1l-2-2zm-2 1H8v-6c0-2.48 1.51-4.5 4-4.5s4 2.02 4 4.5v6z",
            NexIcon.Play => "M8 5v14l11-7z",
            NexIcon.Thermometer => "M15 13V5a3 3 0 0 0-6 0v8a5 5 0 1 0 6 0zm-3-10a1 1 0 0 1 1 1v5h-2V4a1 1 0 0 1 1-1z",
            NexIcon.Memory => "M2 7a1 1 0 0 1 1-1h18a1 1 0 0 1 1 1v10a1 1 0 0 1-1 1H3a1 1 0 0 1-1-1V7zm2 1v8h6.5v-1h1v1H20V8H4zm2 2h2v4H6v-4zm3.5 0h2v4h-2v-4zm3.5 0h2v4h-2v-4zm3.5 0h2v4h-2v-4z",
            NexIcon.Flame => "M12 2.69l5.66 5.66a8 8 0 1 1-11.31 0z",
            NexIcon.Save => "M17 3H5a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V7l-4-4zm-5 16c-1.66 0-3-1.34-3-3s1.34-3 3-3 3 1.34 3 3-1.34 3-3 3zm3-10H5V5h10v4z",
            NexIcon.Download => "M19 9h-4V3H9v6H5l7 7 7-7zM5 18v2h14v-2H5z",
            NexIcon.Pulse => "M3 12h3.5l2-5 3.5 10 2.5-7 1.5 3.5H21v-1.5h-4l-1.5-3.5-2.5 7-3.5-10-2 5H3V12z",
            NexIcon.Filter => "M10 18h4v-2h-4v2zM3 6v2h18V6H3zm3 7h12v-2H6v2z",
            NexIcon.Trash => "M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zM19 4h-3.5l-1-1h-5l-1 1H5v2h14V4z",
            NexIcon.Folder => "M10 4H4c-1.1 0-1.99.9-1.99 2L2 18c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V8c0-1.1-.9-2-2-2h-8l-2-2z",
            NexIcon.Shield => "M12 2L4 5.5v5.8c0 5.2 3.4 10 8 11.2 4.6-1.2 8-6 8-11.2V5.5L12 2zm-1 13.5l-3.5-3.5 1.4-1.4 2.1 2.1 5.6-5.6 1.4 1.4-7 7z",
            NexIcon.Gamepad => "M21 6H3c-1.1 0-2 .9-2 2v8c0 1.1.9 2 2 2h18c1.1 0 2-.9 2-2V8c0-1.1-.9-2-2-2zm-11 7H8v2H6v-2H4v-2h2V9h2v2h2v2zm4.5 1.5c-.83 0-1.5-.67-1.5-1.5s.67-1.5 1.5-1.5 1.5.67 1.5 1.5-.67 1.5-1.5 1.5zm4-3c-.83 0-1.5-.67-1.5-1.5S17.67 8.5 18.5 8.5s1.5.67 1.5 1.5-.67 1.5-1.5 1.5z",
            NexIcon.Gauge => "M12 4a9 9 0 0 0-9 9c0 2.8 1.3 5.3 3.3 6.9l1.4-1.4A7 7 0 0 1 5 13a7 7 0 1 1 12.2 4.7l1.4 1.4A9 9 0 0 0 12 4zm0 6a2 2 0 0 0-1.7 1l-2.6-1.5-.7 1.2 2.6 1.5a2 2 0 1 0 2.4-2.2z",
            NexIcon.Sliders => "M3 5h8v2H3V5zm0 6h4v2H3v-2zm0 6h10v2H3v-2zm12-8v6h2v-2h6v-2h-6V9h-2zm-4-6v6h2V7h8V5h-8V3h-2zm6 12v6h2v-2h2v-2h-2v-2h-2z",
            NexIcon.Disk => "M3 7a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V7zm2 0v10h14V7H5zm2 2h3v6H7V9zm4 1h5v2h-5v-2zm0 3h5v1h-5v-1z",
            NexIcon.Cpu => "M5 5a1 1 0 0 1 1-1h12a1 1 0 0 1 1 1v12a1 1 0 0 1-1 1H6a1 1 0 0 1-1-1V5zm2 2v10h10V7H7zm2 2h6v6H9V9zm-2-7h2v2H7V2zm4 0h2v2h-2V2zm4 0h2v2h-2V2zm-8 18h2v2H7v-2zm4 0h2v2h-2v-2zm4 0h2v2h-2v-2zM2 7h2v2H2V7zm0 4h2v2H2v-2zm0 4h2v2H2v-2zm18-8h2v2h-2V7zm0 4h2v2h-2v-2zm0 4h2v2h-2v-2z",
            NexIcon.Gpu => "M2 4v16h2v-2h1V6H4V4H2zm4 2h15a1 1 0 0 1 1 1v10a1 1 0 0 1-1 1H6a1 1 0 0 1-1-1V7a1 1 0 0 1 1-1zm3.5 2a3 3 0 1 0 0 6 3 3 0 0 0 0-6zm0 1.5a1.5 1.5 0 1 1 0 3 1.5 1.5 0 0 1 0-3zm7-1.5a3 3 0 1 0 0 6 3 3 0 0 0 0-6zm0 1.5a1.5 1.5 0 1 1 0 3 1.5 1.5 0 0 1 0-3zm-8 8.5h2v1.5H7.5V17zm3 0h2v1.5h-2V17zm3 0h2v1.5h-2V17z",
            NexIcon.Fan => "M12 2a10 10 0 1 0 10 10A10 10 0 0 0 12 2zm0 2a8 8 0 0 1 7.45 5.09c-1.57.06-3.21.36-4.52 1.03a3.5 3.5 0 0 0-2.43-1.07V4.05A8.04 8.04 0 0 1 12 4zm-1.5 5.05a3.5 3.5 0 0 0-1.07 2.43c-.67-1.31-.97-2.95-1.03-4.52A8 8 0 0 1 10.5 4.05v5zm-5.41 7.4A8 8 0 0 1 4.05 12h5.05a3.5 3.5 0 0 0 1.07 2.43c-1.31.67-2.95.97-4.52 1.03-.58 0-1.12-.03-1.65-.08zm7.41 3.5a8.04 8.04 0 0 1-2.95-.55v-5.05a3.5 3.5 0 0 0 2.43 1.07c1.31.67 2.95.97 4.52 1.03A8 8 0 0 1 12.5 19.95zm2.02-6.52a1.5 1.5 0 1 1-2.04-2.04 1.5 1.5 0 0 1 2.04 2.04zm4.39.62c-.06 1.57-.36 3.21-1.03 4.52a3.5 3.5 0 0 0-1.07-2.43V11.1c.58 0 1.12.03 1.65.08A8 8 0 0 1 18.91 14.05z",
            NexIcon.Motherboard => "M4 3a1 1 0 0 0-1 1v16a1 1 0 0 0 1 1h16a1 1 0 0 0 1-1V4a1 1 0 0 0-1-1H4zm1 2h14v14H5V5zm2 2v5h5V7H7zm1 1h3v3H8V8zm6 0h1v5h-1V8zm2 0h1v5h-1V8zM7 14h10v2H7v-2zm0 3h7v1H7v-1z",
            NexIcon.Chip => "M6 4a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V6a2 2 0 0 0-2-2H6zm0 2h12v12H6V6zm2 2v3h3V8H8zm5 0v3h3V8h-3zm-5 5v3h3v-3H8zm5 0v3h3v-3h-3z",
            NexIcon.Rocket => "M13.13 2.19c-1.85-.3-3.66.44-4.8 1.95L7.04 6H4a2 2 0 0 0-2 2v1l3.5 3.5-3.21 3.21a1 1 0 0 0 0 1.41l1.59 1.59a1 1 0 0 0 1.41 0L8.5 15.5 12 19h1a2 2 0 0 0 2-2v-3.04l1.86-1.29c1.51-1.14 2.25-2.95 1.95-4.8l-.81-4.87-4.87-.8zm-.55 4.9a1.5 1.5 0 1 1 2.12-2.12 1.5 1.5 0 0 1-2.12 2.12z",
            NexIcon.Window => "M3 4a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2v16a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V4zm2 0v3h14V4H5zm0 5v11h14V9H5zm2-3.5a1 1 0 1 0 0-2 1 1 0 0 0 0 2zm3 0a1 1 0 1 0 0-2 1 1 0 0 0 0 2zm3 0a1 1 0 1 0 0-2 1 1 0 0 0 0 2z",
            NexIcon.Network => "M12 2a10 10 0 1 0 10 10A10 10 0 0 0 12 2zm-1 17.93A8 8 0 0 1 4.07 13H7a15 15 0 0 0 1.5 5.57A8.1 8.1 0 0 1 11 19.93zm-2-8.93H4.07a8 8 0 0 1 0-2H9c-.1 1-.15 1.67-.15 2H9zm2-7.93a8.1 8.1 0 0 1 2.43 1.36A15 15 0 0 0 15 9h-4V3.07zM11 11H9.08a17.2 17.2 0 0 1 .42-4h2.5zm0 2v4h-2.5a17.2 17.2 0 0 1-.42-4zm2 0h2.5a17.2 17.2 0 0 1-.42 4H13zm0-2V9h2.08a17.2 17.2 0 0 1 .42 2zm1.92 8.93A15 15 0 0 0 16.5 15H19.93a8 8 0 0 1-5.01 4.93zM17 13h2.93a8 8 0 0 1 0-2H17c0 .33.05 1 .05 2zm-2-8.57A15 15 0 0 0 13.57 3.07 8 8 0 0 1 18.93 9H16.5a15 15 0 0 0-1.5-4.57z",
            NexIcon.Revert => "M12 5V1L7 6l5 5V7c3.31 0 6 2.69 6 6s-2.69 6-6 6-6-2.69-6-6H4c0 4.42 3.58 8 8 8s8-3.58 8-8-3.58-8-8-8z",
            NexIcon.Check => "M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41L9 16.17z",
            NexIcon.Clean => "M19.36 10.04l-7.4-7.4a2 2 0 0 0-2.83 0L3.25 8.52a2 2 0 0 0 0 2.83l7.4 7.4a2 2 0 0 0 2.83 0l5.88-5.88a2 2 0 0 0 0-2.83zm-8.81 7.4l-7.4-7.4 5.88-5.88 7.4 7.4-5.88 5.88zM21 19h-8v2h8v-2z",
            NexIcon.Bolt => "M11 21h-1l1-7H7.5c-.88 0-.33-.75-.31-.78C8.48 10.94 10.42 7.54 13 3h1l-1 7h3.5c.49 0 .78.36.49.78L11 21z",
            NexIcon.Warning => "M1 21h22L12 2 1 21zm12-3h-2v-2h2v2zm0-4h-2v-4h2v4z",
            NexIcon.Info => "M12 2a10 10 0 1 0 10 10A10 10 0 0 0 12 2zm1 15h-2v-6h2v6zm0-8h-2V7h2v2z",
            NexIcon.Search => "M15.5 14h-.79l-.28-.27A6.47 6.47 0 0 0 16 9.5 6.5 6.5 0 1 0 9.5 16c1.61 0 3.09-.59 4.23-1.57l.27.28v.79l5 4.99L20.49 19l-4.99-5zm-6 0C7.01 14 5 11.99 5 9.5S7.01 5 9.5 5 14 7.01 14 9.5 11.99 14 9.5 14z",
            NexIcon.Power => "M13 3h-2v10h2V3zm4.83 2.17l-1.42 1.42A6.92 6.92 0 0 1 19 12c0 3.87-3.13 7-7 7s-7-3.13-7-7c0-2.05.88-3.9 2.59-5.41L6.17 5.17A8.93 8.93 0 0 0 3 12a9 9 0 0 0 9 9 9 9 0 0 0 9-9c0-2.74-1.23-5.18-3.17-6.83z",
            NexIcon.Layers => "M11.99 18.54l-7.37-5.73L3 14.07l9 7 9-7-1.63-1.27-7.38 5.74zM12 16l7.36-5.73L21 9.07l-9-7-9 7 1.63 1.27L12 16z",
            NexIcon.FileText => "M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8l-6-6zm2 16H8v-2h8v2zm0-4H8v-2h8v2zm-3-5V3.5L18.5 9H13z",
            NexIcon.Photo => "M21 19V5a2 2 0 0 0-2-2H5a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2zM8.5 13.5l2.5 3.01L14.5 12l4.5 6H5l3.5-4.5z",
            NexIcon.Paint => "M12 3a9 9 0 0 0 0 18c.83 0 1.5-.67 1.5-1.5 0-.39-.15-.74-.39-1.01-.23-.26-.38-.61-.38-.99 0-.83.67-1.5 1.5-1.5H16c2.76 0 5-2.24 5-5 0-4.42-4.03-8-9-8zm-5.5 9c-.83 0-1.5-.67-1.5-1.5S5.67 9 6.5 9 8 9.67 8 10.5 7.33 12 6.5 12zm3-4C8.67 8 8 7.33 8 6.5S8.67 5 9.5 5s1.5.67 1.5 1.5S10.33 8 9.5 8zm5 0c-.83 0-1.5-.67-1.5-1.5S13.67 5 14.5 5s1.5.67 1.5 1.5S15.33 8 14.5 8zm3 4c-.83 0-1.5-.67-1.5-1.5S16.67 9 17.5 9s1.5.67 1.5 1.5-.67 1.5-1.5 1.5z",
            NexIcon.Refresh => "M17.65 6.35A7.958 7.958 0 0 0 12 4c-4.42 0-7.99 3.58-7.99 8s3.57 8 7.99 8c3.73 0 6.84-2.55 7.73-6h-2.08A5.99 5.99 0 0 1 12 18c-3.31 0-6-2.69-6-6s2.69-6 6-6c1.66 0 3.14.69 4.22 1.78L13 11h7V4l-2.35 2.35z",
            NexIcon.Audio => "M12 3v10.55c-.59-.34-1.27-.55-2-.55-2.21 0-4 1.79-4 4s1.79 4 4 4 4-1.79 4-4V7h4V3h-6z",
            NexIcon.X => "M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z",
            _ => "M12 2a10 10 0 1 0 10 10A10 10 0 0 0 12 2z"
        };

        return new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse(pathData),
            Fill = fill,
            Stretch = Stretch.Uniform,
            Width = size,
            Height = size,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    public readonly record struct RowAction(string Text, NexIcon? Icon, Brush Color, Func<Task> Action, bool IsPrimary = false, bool IsRevert = false, string Tooltip = "");

    private static RowAction ActionBtn(string text, NexIcon? icon, Brush color, Func<Task> action, bool isPrimary = true) =>
        new(text, icon, color, action, isPrimary, false, text);

    private static RowAction RevertBtn(Func<Task> action, string? tooltip = null) =>
        new("", NexIcon.Revert, Brushes.SlateGray, action, false, true, tooltip ?? NexLocale.T("common_revert_tooltip", "Restaurează starea inițială"));

    private static RowAction RevertBtn(string tooltip, Func<Task> action) =>
        new("", NexIcon.Revert, Brushes.SlateGray, action, false, true, tooltip);

    private OptimizationReport? lastReport;

    // Hardware Monitoring Fields
    private DispatcherTimer? monitorTimer;
    private long lastIdleTime;
    private long lastKernelTime;
    private long lastUserTime;
    private string detectedCpuName = "i9-13900HX";
    private string detectedGpuName = "RTX 4070";
    private double lastGpuUsage = 8.0;
    private bool gpuSamplingInProgress;
    private NativeTuning.SpeedtestResult? lastSpeedtestResult;
    private bool isSpeedtestRunning = false;
    private CancellationTokenSource? speedtestCts;
    private double lastCpuUsage = 7.0;
    private Action? activePageTick;

    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME
    {
        public uint Low;
        public uint High;
        public long ToLong() => ((long)High << 32) | Low;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out FILETIME idleTime, out FILETIME kernelTime, out FILETIME userTime);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private class MEMORYSTATUSEX
    {
        public uint dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

    public MainWindow()
    {
        InitializeComponent();
        try
        {
            var iconUri = new Uri("pack://application:,,,/assets/logo.ico", UriKind.RelativeOrAbsolute);
            Icon = BitmapFrame.Create(iconUri);
        }
        catch
        {
            try
            {
                string iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "logo.ico");
                if (System.IO.File.Exists(iconPath))
                    Icon = new BitmapImage(new Uri(iconPath, UriKind.Absolute));
            }
            catch { }
        }
        SizeChanged += (_, _) => ResizeCards();
        StateChanged += Window_StateChanged;
        KeyDown += MainWindow_KeyDown;
        Closed += (_, _) =>
        {
            lifetime.Cancel();
            monitorTimer?.Stop();
            _autoWallpaperTimer?.Stop();
            LiveWallpaperWindow.EnsureDesktopIconsOnTop();
        };
    }

    private DispatcherTimer? appUpdateWatcherTimer;
    private int _currentUpgradesCount = 0;

    public void UpdateTopNotificationsBadge(int count)
    {
        _currentUpgradesCount = count;
        Dispatcher.Invoke(() =>
        {
            if (count > 0)
            {
                if (NotificationDot != null)
                {
                    NotificationDot.Visibility = Visibility.Visible;
                }
                if (TooltipNotificationDot != null)
                {
                    TooltipNotificationDot.Visibility = Visibility.Visible;
                }
                if (TxtNotificationsTooltip != null)
                {
                    TxtNotificationsTooltip.Text = NexLocale.T("top_notifications_avail_tooltip", "Sunt actualizări disponibile");
                }
            }
            else
            {
                if (NotificationDot != null)
                {
                    NotificationDot.Visibility = Visibility.Collapsed;
                }
                if (TooltipNotificationDot != null)
                {
                    TooltipNotificationDot.Visibility = Visibility.Collapsed;
                }
                if (TxtNotificationsTooltip != null)
                {
                    TxtNotificationsTooltip.Text = NexLocale.T("top_notifications_tooltip_none", "Nu există notificări noi");
                }
            }
        });
    }

    private void StartAppUpdateWatcher()
    {
        if (appUpdateWatcherTimer != null) return;
        appUpdateWatcherTimer = new DispatcherTimer { Interval = TimeSpan.FromHours(4) };
        appUpdateWatcherTimer.Tick += async (_, _) =>
        {
            await RunBackgroundUpdateCheckAsync(notifyToasts: NativeTuning.GetSoftwareUpdateNotificationSetting());
        };
        appUpdateWatcherTimer.Start();

        // Check for updates on startup in background (2.5 seconds after launch)
        Task.Delay(2500).ContinueWith(async _ =>
        {
            await RunBackgroundUpdateCheckAsync(notifyToasts: NativeTuning.GetSoftwareUpdateNotificationSetting());
        });
    }

    public NativeTuning.NexWinSelfUpdateInfo? _nexwinSelfUpdateInfo;

    public async Task<int> RunBackgroundUpdateCheckAsync(bool notifyToasts = false)
    {
        try
        {
            // 1. Immediately check NexWin Self-Update (fast ~200ms)
            var selfUpdate = await NativeTuning.CheckNexWinSelfUpdateAsync();
            Dispatcher.Invoke(() =>
            {
                _nexwinSelfUpdateInfo = selfUpdate;
                if (selfUpdate.IsUpdateAvailable)
                {
                    UpdateTopNotificationsBadge(1 + (cachedUpgradesList?.Count ?? 0));
                    
                    // Show in-app banner toast with direct update action
                    ShowToastWithAction(
                        NexLocale.T("nexwin_update_toast_title", "Actualizare NexWin Nouă!"),
                        NexLocale.Format("nexwin_update_toast_msg", selfUpdate.LatestVersion),
                        NexIcon.Rocket,
                        CyanBrush,
                        NexLocale.T("notif_remote_btn_update", "Actualizează acum"),
                        () => ShowNotificationsModal());

                    NativeTuning.SendWindowsNativeToast(
                        NexLocale.T("nexwin_update_toast_title", "Actualizare NexWin Nouă!"),
                        NexLocale.Format("nexwin_update_toast_msg", selfUpdate.LatestVersion));
                }
            });

            // 2. Check 3rd party apps via Winget in background
            var upgrades = await NativeTuning.CheckForAppUpgradesDetailedAsync();
            Dispatcher.Invoke(() =>
            {
                cachedUpgradesList = upgrades;
                int totalNotifs = upgrades.Count + (selfUpdate.IsUpdateAvailable ? 1 : 0);
                UpdateTopNotificationsBadge(totalNotifs);

                if (upgrades.Count > 0 && notifyToasts)
                {
                    var names = string.Join(", ", upgrades.Select(u => u.Name).Take(3));
                    if (upgrades.Count > 3) names += NexLocale.Format("apps_update_and_others_format", upgrades.Count - 3);

                    NativeTuning.SendWindowsNativeToast(
                        NexLocale.T("apps_update_native_toast_title", "NexWin - Actualizări Disponibile"),
                        NexLocale.Format("apps_update_native_toast_msg_format", upgrades.Count, names));

                    ShowToastWithAction(
                        NexLocale.T("apps_update_toast_title", "Actualizări disponibile!"),
                        NexLocale.Format("apps_update_toast_msg_format", upgrades.Count, names),
                        NexIcon.Bell, AmberBrush, NexLocale.T("apps_update_toast_btn", "Vezi actualizări"),
                        () => ShowNotificationsModal());
                }
            });

            return (selfUpdate.IsUpdateAvailable ? 1 : 0) + (cachedUpgradesList?.Count ?? 0);
        }
        catch
        {
            return 0;
        }
    }

    // ================= AUTO WALLPAPER ROTATION ENGINE =================
    private DispatcherTimer? _autoWallpaperTimer;
    private int _autoWallpaperIndex = 0;
    private static readonly Random _autoWallpaperRnd = new();

    public void InitAutoWallpaperTimer()
    {
        _autoWallpaperTimer?.Stop();
        _autoWallpaperTimer = null;

        if (!NativeTuning.GetAutoWallpaperEnabled()) return;

        int minutes = NativeTuning.GetAutoWallpaperIntervalMinutes();
        if (minutes < 1) minutes = 15;

        _autoWallpaperTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(minutes)
        };
        _autoWallpaperTimer.Tick += (_, _) =>
        {
            ApplyNextAutoWallpaper(notify: false);
        };
        _autoWallpaperTimer.Start();
    }

    public bool ApplyNextAutoWallpaper(bool notify = false)
    {
        try
        {
            string mediaType = NativeTuning.GetAutoWallpaperMediaType();
            var allItems = new List<(string Path, bool IsLive)>();

            if (mediaType == "all" || mediaType == "static")
            {
                var photos = WallhavenService.GetLocalWallpapers();
                if (photos != null)
                {
                    foreach (var p in photos)
                    {
                        if (!string.IsNullOrEmpty(p.FilePath) && File.Exists(p.FilePath))
                            allItems.Add((p.FilePath, false));
                    }
                }
            }

            if (mediaType == "all" || mediaType == "video")
            {
                var videos = PixabayVideoService.GetLocalWallpapers();
                if (videos != null)
                {
                    foreach (var v in videos)
                    {
                        if (!string.IsNullOrEmpty(v.FilePath) && File.Exists(v.FilePath))
                            allItems.Add((v.FilePath, true));
                    }
                }
            }

            if (allItems.Count == 0) return false;

            bool isRandom = NativeTuning.GetAutoWallpaperRandom();
            int pickIndex;
            if (isRandom)
            {
                pickIndex = _autoWallpaperRnd.Next(allItems.Count);
            }
            else
            {
                _autoWallpaperIndex = (_autoWallpaperIndex + 1) % allItems.Count;
                pickIndex = _autoWallpaperIndex;
            }

            var selected = allItems[pickIndex];
            if (selected.IsLive)
            {
                LiveWallpaperWindow.StartLive(selected.Path);
            }
            else
            {
                if (LiveWallpaperWindow.IsRunning)
                {
                    LiveWallpaperWindow.StopLive();
                }
                WallhavenService.ApplyAsDesktopWallpaper(selected.Path);
            }

            if (notify)
            {
                string fileName = Path.GetFileNameWithoutExtension(selected.Path);
                ShowToast(NexLocale.T("cust_auto_wp_changed_title"), NexLocale.Format("cust_auto_wp_changed_msg", fileName), NexIcon.Check, GreenBrush);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        NativeTuning.EnsureOriginalWallpaperSaved();
        NexLocale.LanguageChanged += () => Dispatcher.Invoke(() =>
        {
            ApplyLanguageToChrome();
            RefreshCurrentPage();
        });
        ApplyLanguageToChrome();
        SelectNav(NavDashboard);
        ShowDashboard();
        DetectHardwareNames();
        StartHardwareMonitoring();
        StartAppUpdateWatcher();
        _ = SyncCommunityGoalAsync();
        var cmdArgs = Environment.GetCommandLineArgs();
        bool isScreenshotRun = cmdArgs.Any(a => a.Equals("--screenshot", StringComparison.OrdinalIgnoreCase) || a.Equals("--screenshot-page", StringComparison.OrdinalIgnoreCase));
        if (!isScreenshotRun)
        {
            InitAutoWallpaperTimer();
        }
        await RefreshStatusAsync();
    }

    public bool StartInTray { get; set; }
    private bool _isExplicitExit = false;
    private bool _trayInitialized = false;
    private IntPtr _trayHwnd = IntPtr.Zero;
    private IntPtr _trayIconHandle = IntPtr.Zero;
    private ContextMenu? _trayContextMenu;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        try
        {
            var handle = new WindowInteropHelper(this).Handle;
            _trayHwnd = handle;
            var source = HwndSource.FromHwnd(handle);
            source?.AddHook(WndProc);

            var cmdArgs = Environment.GetCommandLineArgs();
            bool isScreenshotRun = cmdArgs.Any(a => a.Equals("--screenshot", StringComparison.OrdinalIgnoreCase) || a.Equals("--screenshot-page", StringComparison.OrdinalIgnoreCase));
            if (!isScreenshotRun)
            {
                InitializeSystemTray(handle);
                if (StartInTray)
                {
                    ShowInTaskbar = false;
                    Hide();
                }
            }

            // Pre-warm heavy WMI / Process / Driver caches in background so module navigation is 0ms
            _ = Task.Run(async () =>
            {
                try { _cachedDriversList ??= await NativeTuning.ScanSystemDriversAsync().ConfigureAwait(false); } catch { }
                try { _ = NativeTuning.GetProcessInspectorList(); } catch { }
                try { RefreshActivationCacheInBackground(); } catch { }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"OnSourceInitialized hook error: {ex.Message}");
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        var cmdArgs = Environment.GetCommandLineArgs();
        bool isScreenshotRun = cmdArgs.Any(a => a.Equals("--screenshot", StringComparison.OrdinalIgnoreCase) || a.Equals("--screenshot-page", StringComparison.OrdinalIgnoreCase));
        if (!_isExplicitExit && !isScreenshotRun && _trayInitialized)
        {
            e.Cancel = true;
            MinimizeToTray();
            return;
        }
        RemoveSystemTrayIcon();
        base.OnClosing(e);
    }

    public void MinimizeToTray()
    {
        ShowInTaskbar = false;
        Hide();
    }

    public void RestoreFromTray()
    {
        ShowInTaskbar = true;
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    public void ExitApplicationCompletely()
    {
        _isExplicitExit = true;
        RemoveSystemTrayIcon();
        try
        {
            LiveWallpaperWindow.StopLive();
            NativeTuning.RestoreOriginalWallpaper(disableAuto: false);
        }
        catch { }
        Application.Current.Shutdown();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_GETMINMAXINFO)
        {
            WmGetMinMaxInfo(hwnd, lParam);
            handled = true;
        }
        else if (msg == WM_TRAYICON)
        {
            int mouseMsg = (int)(lParam.ToInt64() & 0xFFFF);
            if (mouseMsg == WM_LBUTTONUP || mouseMsg == WM_LBUTTONDBLCLK)
            {
                Dispatcher.BeginInvoke(new Action(RestoreFromTray));
                handled = true;
            }
            else if (mouseMsg == WM_RBUTTONUP)
            {
                Dispatcher.BeginInvoke(new Action(ShowTrayContextMenu));
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    private const int WM_GETMINMAXINFO = 0x0024;
    private const int WM_TRAYICON = 0x8000 + 101;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_LBUTTONDBLCLK = 0x0203;
    private const int WM_RBUTTONUP = 0x0205;
    private const uint NIM_ADD = 0x00000000;
    private const uint NIM_MODIFY = 0x00000001;
    private const uint NIM_DELETE = 0x00000002;
    private const uint NIF_MESSAGE = 0x00000001;
    private const uint NIF_ICON = 0x00000002;
    private const uint NIF_TIP = 0x00000004;
    private const uint IMAGE_ICON = 1;
    private const uint LR_LOADFROMFILE = 0x00000010;
    private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATAW
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Shell_NotifyIconW(uint dwMessage, ref NOTIFYICONDATAW lpdata);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr ExtractAssociatedIconW(IntPtr hInst, StringBuilder lpIconPath, out ushort lpiIcon);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadImageW(IntPtr hinst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private void InitializeSystemTray(IntPtr hwnd)
    {
        try
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] icoCandidates =
            {
                Path.Combine(baseDir, "logo.ico"),
                Path.Combine(baseDir, "assets", "logo.ico"),
                Path.Combine(baseDir, "Assets", "logo.ico")
            };
            foreach (var icoPath in icoCandidates)
            {
                if (File.Exists(icoPath))
                {
                    _trayIconHandle = LoadImageW(IntPtr.Zero, icoPath, IMAGE_ICON, 32, 32, LR_LOADFROMFILE);
                    if (_trayIconHandle != IntPtr.Zero) break;
                }
            }
            if (_trayIconHandle == IntPtr.Zero)
            {
                string exePath = Environment.ProcessPath ?? Path.Combine(baseDir, "NexWin.exe");
                var sb = new StringBuilder(exePath, 260);
                _trayIconHandle = ExtractAssociatedIconW(IntPtr.Zero, sb, out _);
            }

            var nid = new NOTIFYICONDATAW
            {
                cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(),
                hWnd = hwnd,
                uID = 1001,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = WM_TRAYICON,
                hIcon = _trayIconHandle,
                szTip = NexLocale.T("tray_tooltip", "NexWin System Optimizer (Activ în fundal)")
            };

            _trayInitialized = Shell_NotifyIconW(NIM_ADD, ref nid);
        }
        catch { }
    }

    private void RemoveSystemTrayIcon()
    {
        if (!_trayInitialized || _trayHwnd == IntPtr.Zero) return;
        try
        {
            var nid = new NOTIFYICONDATAW
            {
                cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(),
                hWnd = _trayHwnd,
                uID = 1001
            };
            Shell_NotifyIconW(NIM_DELETE, ref nid);
            _trayInitialized = false;
        }
        catch { }
    }

    private void ShowTrayContextMenu()
    {
        try
        {
            NexLocale.LoadSettings();

            if (_trayHwnd != IntPtr.Zero)
            {
                SetForegroundWindow(_trayHwnd);
            }

            _trayContextMenu = new ContextMenu
            {
                Style = (Style)FindResource("DarkContextMenuStyle"),
                Background = new SolidColorBrush(Color.FromRgb(11, 19, 36)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(42, 62, 88)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6),
                Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint
            };

            MenuItem CreateTrayItem(string text, NexIcon iconKind, Brush fg, RoutedEventHandler onClick)
            {
                var item = new MenuItem
                {
                    Style = (Style)FindResource("DarkMenuItemStyle"),
                    Header = text,
                    Icon = CreateVectorIcon(iconKind, fg, 13),
                    Foreground = fg,
                    Background = Brushes.Transparent,
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Padding = new Thickness(10, 7, 14, 7),
                    Cursor = Cursors.Hand
                };
                item.Click += onClick;
                return item;
            }

            _trayContextMenu.Items.Add(CreateTrayItem(
                NexLocale.T("tray_menu_open", "Deschide NexWin"),
                NexIcon.Gauge,
                new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                (_, _) => RestoreFromTray()));

            _trayContextMenu.Items.Add(CreateTrayItem(
                NexLocale.T("tray_menu_boost", "One-Click Boost Rapid (RAM & Cache)"),
                NexIcon.Bolt,
                new SolidColorBrush(Color.FromRgb(52, 211, 153)),
                (_, _) =>
                {
                    RestoreFromTray();
                    ShowOneClickBoostModal();
                }));

            if (LiveWallpaperWindow.IsRunning)
            {
                _trayContextMenu.Items.Add(CreateTrayItem(
                    NexLocale.T("tray_menu_stop_live", "Oprește Live Wallpaper"),
                    NexIcon.Layers,
                    new SolidColorBrush(Color.FromRgb(251, 191, 36)),
                    (_, _) => LiveWallpaperWindow.StopLive()));
            }

            _trayContextMenu.Items.Add(CreateTrayItem(
                NexLocale.T("tray_menu_support", "Susține Proiectul"),
                NexIcon.Bolt,
                new SolidColorBrush(Color.FromRgb(245, 158, 11)),
                (_, _) =>
                {
                    RestoreFromTray();
                    ShowSupportProjectModal();
                }));

            _trayContextMenu.Items.Add(new Separator
            {
                Background = new SolidColorBrush(Color.FromRgb(30, 44, 66)),
                Height = 1,
                Margin = new Thickness(6, 4, 6, 4),
                Template = new ControlTemplate(typeof(Separator))
                {
                    VisualTree = new FrameworkElementFactory(typeof(Border))
                }
            });
            if (_trayContextMenu.Items[^1] is Separator sep && sep.Template != null)
            {
                var bdFactory = new FrameworkElementFactory(typeof(Border));
                bdFactory.SetValue(Border.HeightProperty, 1.0);
                bdFactory.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(30, 44, 66)));
                bdFactory.SetValue(Border.MarginProperty, new Thickness(6, 4, 6, 4));
                sep.Template = new ControlTemplate(typeof(Separator)) { VisualTree = bdFactory };
            }

            _trayContextMenu.Items.Add(CreateTrayItem(
                NexLocale.T("tray_menu_exit", "Ieșire completă (Închide)"),
                NexIcon.Power,
                new SolidColorBrush(Color.FromRgb(248, 113, 113)),
                (_, _) => ExitApplicationCompletely()));

            _trayContextMenu.IsOpen = true;
        }
        catch { }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    private static void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
    {
        var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
        var hMonitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        if (hMonitor != IntPtr.Zero)
        {
            var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (GetMonitorInfo(hMonitor, ref monitorInfo))
            {
                var rcWork = monitorInfo.rcWork;
                var rcMonitor = monitorInfo.rcMonitor;

                mmi.ptMaxPosition.x = Math.Abs(rcWork.left - rcMonitor.left);
                mmi.ptMaxPosition.y = Math.Abs(rcWork.top - rcMonitor.top);
                mmi.ptMaxSize.x = Math.Abs(rcWork.right - rcWork.left);
                mmi.ptMaxSize.y = Math.Abs(rcWork.bottom - rcWork.top);

                mmi.ptMinTrackSize.x = 1100;
                mmi.ptMinTrackSize.y = 720;
            }
        }
        Marshal.StructureToPtr(mmi, lParam, true);
    }

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            if (WindowRootBorder != null)
            {
                WindowRootBorder.CornerRadius = new CornerRadius(0);
                WindowRootBorder.BorderThickness = new Thickness(0);
            }
            if (SidebarBorder != null)
            {
                SidebarBorder.CornerRadius = new CornerRadius(0);
            }
            if (TopAccentLine != null)
            {
                TopAccentLine.CornerRadius = new CornerRadius(0);
            }
            if (RootShell != null)
            {
                RootShell.Margin = new Thickness(8);
            }
            if (MaximizeIconPath != null)
            {
                MaximizeIconPath.Data = Geometry.Parse("M 2.5,0.5 H 9.5 V 7.5 H 7.5 M 0.5,2.5 H 7.5 V 9.5 H 0.5 Z");
            }
            if (BtnMaximize != null)
            {
                BtnMaximize.ToolTip = "Restaurare";
            }
        }
        else
        {
            if (WindowRootBorder != null)
            {
                WindowRootBorder.CornerRadius = new CornerRadius(10);
                WindowRootBorder.BorderThickness = new Thickness(1);
            }
            if (SidebarBorder != null)
            {
                SidebarBorder.CornerRadius = new CornerRadius(9, 0, 0, 9);
            }
            if (TopAccentLine != null)
            {
                TopAccentLine.CornerRadius = new CornerRadius(9, 9, 0, 0);
            }
            if (RootShell != null)
            {
                RootShell.Margin = new Thickness(0);
            }
            if (MaximizeIconPath != null)
            {
                MaximizeIconPath.Data = Geometry.Parse("M 0.5,0.5 H 9.5 V 9.5 H 0.5 Z");
            }
            if (BtnMaximize != null)
            {
                BtnMaximize.ToolTip = "Maximizare";
            }
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            if (e.ClickCount == 2)
            {
                Maximize_Click(sender, e);
                return;
            }
            DragMove();
        }
    }

    private void SidebarTargetProcesses_Click(object sender, MouseButtonEventArgs e)
    {
        NavigateTo("Processes");
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Discord_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetDataObject("luci3alin", true);
            ShowToast(NexLocale.T("discord_contact_title", "Discord Contact"), NexLocale.T("discord_contact_copied", "Username 'luci3alin' a fost copiat în clipboard!"), NexIcon.Check, GreenBrush);
        }
        catch
        {
            ShowToast(NexLocale.T("discord_contact_title", "Discord Contact"), NexLocale.T("discord_contact_info", "Username Discord: luci3alin"), NexIcon.Info, CyanBrush);
        }
    }

    private void GlobalSearch_Click(object sender, RoutedEventArgs e)
    {
        ShowGlobalSearchModal();
    }

    private void MainWindow_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.K && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            e.Handled = true;
            ShowGlobalSearchModal();
        }
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await RefreshStatusAsync();
    private void LogNavButton_Click(object sender, RoutedEventArgs e) { SelectNav(null); ShowLogs(); }

    public void ApplyLanguageToChrome()
    {
        if (TxtModuleHeader != null) TxtModuleHeader.Text = NexLocale.T("nav_modules_header");
        if (TxtNavDashboard != null) TxtNavDashboard.Text = NexLocale.T("nav_dashboard");
        if (TxtNavProfiles != null) TxtNavProfiles.Text = NexLocale.T("nav_profiles");
        if (TxtNavPerformance != null) TxtNavPerformance.Text = NexLocale.T("nav_hardware");
        if (TxtNavGaming != null) TxtNavGaming.Text = NexLocale.T("nav_gaming");
        if (TxtNavDrivers != null) TxtNavDrivers.Text = NexLocale.T("nav_drivers", "Drivere");
        if (TxtNavAi != null) TxtNavAi.Text = NexLocale.T("nav_ai");
        if (TxtNavServices != null) TxtNavServices.Text = NexLocale.T("nav_services");
        if (TxtNavDisk != null) TxtNavDisk.Text = NexLocale.T("nav_disk");
        if (TxtNavProcesses != null) TxtNavProcesses.Text = NexLocale.T("nav_processes");
        if (TxtNavStartup != null) TxtNavStartup.Text = NexLocale.T("nav_startup");
        if (TxtNavApps != null) TxtNavApps.Text = NexLocale.T("nav_apps");
        if (TxtNavCustomizer != null) TxtNavCustomizer.Text = NexLocale.T("nav_customizer");
        if (TxtNavNetwork != null) TxtNavNetwork.Text = NexLocale.T("nav_network");
        if (TxtNavSnapshot != null) TxtNavSnapshot.Text = NexLocale.T("nav_snapshot");
        if (TxtNavActivator != null) TxtNavActivator.Text = NexLocale.T("nav_activator");
        if (TxtNavSettings != null) TxtNavSettings.Text = NexLocale.T("nav_settings");

        if (TxtTargetProcessesLabel != null) TxtTargetProcessesLabel.Text = NexLocale.T("nav_target_processes");
        if (SidebarProcessSubtext != null) SidebarProcessSubtext.Text = NexLocale.T("nav_processes_subtext");
        if (TxtReportNavLabel != null) TxtReportNavLabel.Text = NexLocale.T("nav_report_button");
        if (TxtLogNavLabel != null) TxtLogNavLabel.Text = NexLocale.T("nav_log_button");

        if (TxtGlobalSearch != null) TxtGlobalSearch.Text = NexLocale.T("top_search");
        if (TxtTopSettings != null) TxtTopSettings.Text = NexLocale.T("top_settings");
        if (TxtRamLabel != null) TxtRamLabel.Text = NexLocale.T("top_ram_label");

        if (BtnGlobalSearch != null) BtnGlobalSearch.ToolTip = NexLocale.T("top_search_tooltip");
        if (TxtNotificationsTooltip != null) TxtNotificationsTooltip.Text = _currentUpgradesCount > 0
            ? NexLocale.T("top_notifications_avail_tooltip", "Sunt actualizări disponibile")
            : NexLocale.T("top_notifications_tooltip", "Notificări și Actualizări");
        if (TooltipNotificationDot != null) TooltipNotificationDot.Visibility = _currentUpgradesCount > 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        if (BtnSettings != null) BtnSettings.ToolTip = NexLocale.T("top_settings_tooltip");
        if (BtnMinimize != null) BtnMinimize.ToolTip = NexLocale.T("top_minimize_tooltip");
        if (BtnMaximize != null) BtnMaximize.ToolTip = NexLocale.T("top_maximize_tooltip");
        if (BtnClose != null) BtnClose.ToolTip = NexLocale.T("top_close_tooltip");

        if (FooterMottoText != null) FooterMottoText.Text = NexLocale.T("footer_motto");
        if (FooterWindowsText != null) FooterWindowsText.Text = NexLocale.T("footer_optimized_for");

        UpdateSidebarSupportGoalUI();
    }

    public async Task SyncCommunityGoalAsync()
    {
        try
        {
            UpdateSidebarSupportGoalUI(NativeTuning.GetCurrentCommunityGoal());
            var synced = await NativeTuning.FetchCommunityGoalAsync();
            await Dispatcher.InvokeAsync(() => UpdateSidebarSupportGoalUI(synced));
        }
        catch { }
    }

    public void UpdateSidebarSupportGoalUI(NativeTuning.CommunityGoalInfo? goal = null)
    {
        try
        {
            goal ??= NativeTuning.GetCurrentCommunityGoal();
            if (TxtSidebarSupportTitle != null)
                TxtSidebarSupportTitle.Text = NexLocale.T("support_goal_sidebar_title", "Susține Proiectul");
            if (TxtSidebarSupportCta != null)
                TxtSidebarSupportCta.Text = NexLocale.T("support_goal_sidebar_cta", "Contribuie ›");
            if (SidebarSupportCard != null)
                SidebarSupportCard.ToolTip = NexLocale.T("support_goal_sidebar_tooltip", "Susține dezvoltarea NexWin și vezi progresul obiectivului activ");

            int pct = goal.Percentage;
            if (TxtSidebarSupportPct != null)
                TxtSidebarSupportPct.Text = $"{pct}%";
            if (TxtSidebarSupportAmount != null)
                TxtSidebarSupportAmount.Text = $"{goal.CurrentAmount:0} / {goal.TargetAmount:0} {goal.Currency}";

            if (SidebarSupportProgressFill != null)
            {
                double maxBarWidth = 176.0;
                SidebarSupportProgressFill.Width = pct <= 0 ? 0.0 : Math.Clamp((pct / 100.0) * maxBarWidth, 6.0, maxBarWidth);
            }
        }
        catch { }
    }

    private void SidebarSupport_Click(object sender, MouseButtonEventArgs e)
    {
        ShowSupportProjectModal();
    }

    internal void RefreshCurrentPage()
    {
        var target = activeNav?.Tag?.ToString() ?? "Dashboard";
        switch (target)
        {
            case "Dashboard": ShowDashboard(); break;
            case "Profiles": ShowProfiles(); break;
            case "Performance": ShowPerformance(); break;
            case "Gaming": ShowGaming(); break;
            case "Drivers": ShowDrivers(); break;
            case "AI": ShowAi(); break;
            case "Services": ShowServices(); break;
            case "Disk": ShowDisk(); break;
            case "Processes": ShowProcesses(); break;
            case "Startup": ShowStartup(); break;
            case "Ram": ShowProcesses(); break;
            case "Apps": ShowApps(); break;
            case "Classic": ShowApps(); break;
            case "Customizer": ShowCustomizer(); break;
            case "Network": ShowNetwork(); break;
            case "Snapshot": ShowSnapshot(); break;
            case "Activator": ShowActivator(); break;
            case "Settings": ShowSettings(); break;
            default: ShowDashboard(); break;
        }
    }

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;
        SelectNav(button);
        var target = button.Tag?.ToString();
        switch (target)
        {
            case "Dashboard": ShowDashboard(); break;
            case "Profiles": ShowProfiles(); break;
            case "Performance": ShowPerformance(); break;
            case "Gaming": ShowGaming(); break;
            case "Drivers": ShowDrivers(); break;
            case "AI": ShowAi(); break;
            case "Services": ShowServices(); break;
            case "Disk": ShowDisk(); break;
            case "Processes": ShowProcesses(); break;
            case "Startup": ShowStartup(); break;
            case "Ram": ShowProcesses(); break;
            case "Apps": ShowApps(); break;
            case "Classic": ShowApps(); break;
            case "Customizer": ShowCustomizer(); break;
            case "Network": ShowNetwork(); break;
            case "Snapshot": ShowSnapshot(); break;
            case "Activator": ShowActivator(); break;
            case "Settings": ShowSettings(); break;
        }
    }

    internal void NavigateTo(string page)
    {
        switch (page)
        {
            case "Disk": SelectNav(NavDisk); ShowDisk(); break;
            case "DiskTree": activeDiskTab = "Arbore directoare (TreeSize)"; SelectNav(NavDisk); ShowDisk(); break;
            case "Processes": SelectNav(NavProcesses); ShowProcesses(); break;
            case "Gaming": _activeGamingView = "Games"; SelectNav(NavGaming); ShowGaming(); break;
            case "Drivers": SelectNav(NavDrivers); ShowDrivers(); break;
            case "Nvidia": case "NvidiaInspector": _activeGamingView = "NvidiaInspector"; SelectNav(NavGaming); ShowGaming(); break;
            case "NvidiaBottom": _activeGamingView = "NvidiaInspector"; SelectNav(NavGaming); ShowGaming(); PageScroll.ScrollToBottom(); break;
            case "GamingBottom": SelectNav(NavGaming); ShowGaming(); PageScroll.ScrollToBottom(); break;
            case "AI": case "Debloat": SelectNav(NavServices); ShowAi(); break;
            case "Services": SelectNav(NavServices); ShowServices(); break;
            case "Profiles": SelectNav(NavProfiles); ShowProfiles(); break;
            case "Performance": SelectNav(NavPerformance); ShowPerformance(); break;
            case "Customizer": activeCustomizerTab = "Wallpaper"; SelectNav(NavCustomizer); ShowCustomizer(); break;
            case "CustomizerWallpaper": activeCustomizerTab = "Wallpaper"; SelectNav(NavCustomizer); ShowCustomizer(); break;
            case "CustomizerStatic": activeCustomizerTab = "Wallpaper"; _wallpaperEngineMode = "Static"; SelectNav(NavCustomizer); ShowCustomizer(); break;
            case "CustomizerLibrary": activeCustomizerTab = "Wallpaper"; _wallpaperEngineMode = "Library"; _libraryCategory = "Live"; SelectNav(NavCustomizer); ShowCustomizer(); break;
            case "CustomizerLibraryStatic": activeCustomizerTab = "Wallpaper"; _wallpaperEngineMode = "Library"; _libraryCategory = "Static"; SelectNav(NavCustomizer); ShowCustomizer(); break;
            case "CustomizerStart": activeCustomizerTab = "Start"; SelectNav(NavCustomizer); ShowCustomizer(); break;
            case "Network": SelectNav(NavNetwork); ShowNetwork(); break;
            case "Snapshot": SelectNav(NavSnapshot); ShowSnapshot(); break;
            case "Startup": SelectNav(NavStartup); ShowStartup(); break;
            case "Activator": SelectNav(NavActivator); ShowActivator(); break;
            case "Settings": SelectNav(NavSettings); ShowSettings(); break;
            case "Ram": SelectNav(NavProcesses); ShowProcesses(); break;
            case "Apps": activeAppsTab = "Catalog"; SelectNav(NavApps); ShowApps(); break;
            case "AppsUpdates": activeAppsTab = "Updates"; SelectNav(NavApps); ShowApps(); break;
            case "AppsInstalled": activeAppsTab = "Installed"; SelectNav(NavApps); ShowApps(); break;
            case "InstallModalDemo":
                activeAppsTab = "Catalog";
                SelectNav(NavApps);
                ShowApps();
                var demoApps = new List<NativeTuning.SoftwareAppItem>
                {
                    new() { Id = "discord", WingetId = "Discord.Discord", Name = "Discord", Category = "Gaming & Mesagerie", Description = "Comunicație vocală cu latență redusă și chat", BrandColorHex = "#5865F2" },
                    new() { Id = "spotify", WingetId = "Spotify.Spotify", Name = "Spotify", Category = "Media & Sunet", Description = "Streaming muzică și podcasturi fără întreruperi", BrandColorHex = "#1DB954" }
                };
                _ = ExecuteAppInstallQueueAsync(demoApps);
                break;
            case "Logs": SelectNav(null); ShowLogs(); break;
            case "GlobalSearchDemo": ShowGlobalSearchModal(); break;
            case "BoostDemo": ShowOneClickBoostModal(); break;
            case "GamingConfigDemo": ShowGamingModal(); break;
            case "NotificationsDemo":
                if (cachedUpgradesList == null || cachedUpgradesList.Count == 0)
                {
                    cachedUpgradesList = new List<NativeTuning.AppUpgradeDetail>
                    {
                        new() { Id = "Discord.Discord", Name = "Discord", InstalledVersion = "1.0.9034", AvailableVersion = "1.0.9168", IsSelected = false },
                        new() { Id = "7zip.7zip", Name = "7-Zip", InstalledVersion = "23.01", AvailableVersion = "24.08", IsSelected = false },
                        new() { Id = "Mozilla.Firefox", Name = "Mozilla Firefox", InstalledVersion = "131.0.2", AvailableVersion = "132.0.1", IsSelected = false },
                        new() { Id = "OBSProject.OBSStudio", Name = "OBS Studio", InstalledVersion = "30.1.2", AvailableVersion = "30.2.3", IsSelected = false },
                        new() { Id = "VideoLAN.VLC", Name = "VLC Media Player", InstalledVersion = "3.0.20", AvailableVersion = "3.0.21", IsSelected = false },
                        new() { Id = "Spotify.Spotify", Name = "Spotify", InstalledVersion = "1.2.46", AvailableVersion = "1.2.50", IsSelected = false }
                    };
                }
                _nexwinSelfUpdateInfo = new NativeTuning.NexWinSelfUpdateInfo
                {
                    IsUpdateAvailable = true,
                    CurrentVersion = "1.0.87",
                    LatestVersion = "1.0.88",
                    DownloadUrl = "https://github.com/luci3alin/NexWin/releases/latest/download/NexWin-Update.zip",
                    ReleaseNotes = "Actualizare automată în-place cu un singur click, fără reinstalare manuală."
                };
                UpdateTopNotificationsBadge(cachedUpgradesList.Count + 1);
                ShowNotificationsModal();
                break;
            case "NotificationsSelectedDemo":
                cachedUpgradesList = new List<NativeTuning.AppUpgradeDetail>
                {
                    new() { Id = "Discord.Discord", Name = "Discord", InstalledVersion = "1.0.9034", AvailableVersion = "1.0.9168", IsSelected = false },
                    new() { Id = "7zip.7zip", Name = "7-Zip", InstalledVersion = "23.01", AvailableVersion = "24.08", IsSelected = true },
                    new() { Id = "Mozilla.Firefox", Name = "Mozilla Firefox", InstalledVersion = "131.0.2", AvailableVersion = "132.0.1", IsSelected = false }
                };
                UpdateTopNotificationsBadge(cachedUpgradesList.Count);
                NavigateTo("AppsUpdates");
                break;
            case "ModalDemo": ShowConfirmModalAsync("Confirmare aplicare profil", "Acest profil optimizează placa video, setările de energie și rețeaua pentru latență scăzută.\nModificările sunt complet reversibile prin punctul de restaurare.", AmberBrush, "Continuă", "Anulează"); break;
            case "DnsDemo":
                ShowDnsBenchmarkResultsModal(new List<NativeTuning.DnsBenchmarkItem>
                {
                    new() { Name = "Cloudflare DNS", Primary = "1.1.1.1", Secondary = "1.0.0.1", LatencyMs = 12.4, IsFastest = true, Tag = "Recomandat Gaming" },
                    new() { Name = "Google Public DNS", Primary = "8.8.8.8", Secondary = "8.8.4.4", LatencyMs = 18.2, IsFastest = false, Tag = "Stabil & Rapid" },
                    new() { Name = "Quad9 Security", Primary = "9.9.9.9", Secondary = "149.112.112.112", LatencyMs = 24.8, IsFastest = false, Tag = "Protecție Malware" }
                });
                break;
            case "ReportDemo":
            case "HealthReportDemo":
                SelectNav(NavDashboard);
                ShowDashboard();
                var (tempBytesR, tempFilesR) = NativeTuning.GetTempAndCacheSize();
                double tempGbR = tempBytesR / (1024.0 * 1024.0 * 1024.0);
                var startupAppsR = NativeTuning.GetStartupAppsNative();
                var teleR = NativeTuning.GetHardwareTelemetry(lastCpuUsage);
                int sScore = tempGbR < 0.5 ? 20 : (tempGbR < 2.0 ? 17 : (tempGbR < 5.0 ? 14 : (tempGbR < 15.0 ? 10 : (tempGbR < 30.0 ? 6 : 3))));
                int stScore = startupAppsR.Count <= 3 ? 20 : (startupAppsR.Count <= 6 ? 16 : (startupAppsR.Count <= 10 ? 12 : (startupAppsR.Count <= 15 ? 8 : 4)));
                double dfPct = teleR.DiskTotalGb > 0 ? (teleR.DiskFreeGb / teleR.DiskTotalGb * 100.0) : 50.0;
                int dScore = teleR.DiskHealthPercent >= 95 && dfPct >= 20.0 ? 20 : (teleR.DiskHealthPercent >= 90 ? 17 : 13);
                double mTemp = Math.Max(teleR.CpuTempC, teleR.GpuTempC);
                int tScore = mTemp < 55.0 ? 20 : (mTemp < 68.0 ? 17 : (mTemp < 78.0 ? 13 : 8));
                int cScore = 20;
                bool gmOn = (NativeTuning.GetRegistryDword(RegistryHive.CurrentUser, @"Software\Microsoft\GameBar", "AutoGameModeEnabled") ?? 1) == 1;
                if (!gmOn) cScore -= 3;
                bool ucOn = (NativeTuning.GetRegistryDword(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "EnableLUA") ?? 1) == 1;
                if (!ucOn) cScore -= 4;

                ShowOptimizationReportModal(new OptimizationReport(NexLocale.T("diag_report_title", "Diagnoză Sistem NexWin"), DateTime.Now, 120, true, new List<OptimizationStep>
                {
                    new(NexLocale.T("diag_step1_title", "1. Curățenie Stocare & Cache"), NexLocale.Format("diag_step1_detail_format", sScore, tempGbR, tempFilesR), sScore >= 14),
                    new(NexLocale.T("diag_step2_title", "2. Aplicații la Pornire (Startup)"), NexLocale.Format("diag_step2_detail_format", stScore, startupAppsR.Count), stScore >= 14),
                    new(NexLocale.T("diag_step3_title", "3. Sănătate SMART & Spațiu Disc"), NexLocale.Format("diag_step3_detail_format", dScore, teleR.DiskHealthPercent, teleR.DiskFreeGb, dfPct), dScore >= 14),
                    new(NexLocale.T("diag_step4_title", "4. Temperaturi Hardware (CPU/GPU)"), NexLocale.Format("diag_step4_detail_format", tScore, mTemp, teleR.CpuTempC, teleR.GpuTempC), tScore >= 14),
                    new(NexLocale.T("diag_step5_title", "5. Securitate & Setări Windows 11"), NexLocale.Format("diag_step5_detail_format", cScore, (gmOn ? NexLocale.T("status_enabled", "Activat") : NexLocale.T("status_inactive", "Inactiv")), (ucOn ? NexLocale.T("diag_uac_secure", "Securizat") : NexLocale.T("status_disabled", "Dezactivat"))), cScore >= 14)
                }));
                break;
            default: SelectNav(NavDashboard); ShowDashboard(); break;
        }
    }

    private void SelectNav(Button? button)
    {
        activePageTick = null;
        if (activeNav != null)
        {
            SetIsNavActive(activeNav, false);
        }
        activeNav = button;
        if (activeNav != null)
        {
            SetIsNavActive(activeNav, true);
        }
    }

    private void PreparePage(string title, string subtitle)
    {
        PageRoot.Children.Clear();
        if (title == "Dashboard") AddDashboardHero(title, subtitle); else AddPageHero(title, subtitle);
        cardGrid = new StackPanel { Margin = new Thickness(0, 0, 0, 4) };
        PageRoot.Children.Add(cardGrid);
    }

    private void ResizeCards()
    {
        if (cardGrid == null) return;
        foreach (var card in cardGrid.Children.OfType<Border>())
        {
            card.MaxWidth = Math.Max(600, PageScroll.ActualWidth - 8);
        }
    }

    private UIElement CreateDonutChart(double pct, double size = 72, Brush? strokeBrush = null)
    {
        var grid = new Grid { Width = size, Height = size, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var bgEllipse = new Ellipse
        {
            Width = size,
            Height = size,
            Stroke = new SolidColorBrush(Color.FromRgb(18, 28, 42)),
            StrokeThickness = 6.0,
            Fill = Brushes.Transparent
        };
        grid.Children.Add(bgEllipse);

        pct = Math.Clamp(pct, 0.1, 99.9);
        double r = (size - 6.0) / 2.0;
        double cx = size / 2.0;
        double cy = size / 2.0;
        double angle = (pct / 100.0) * 360.0;
        double rad = (angle - 90.0) * Math.PI / 180.0;
        double startX = cx;
        double startY = cy - r;
        double endX = cx + r * Math.Cos(rad);
        double endY = cy + r * Math.Sin(rad);
        bool isLargeArc = angle > 180.0;

        var geom = new PathGeometry();
        var fig = new PathFigure { StartPoint = new Point(startX, startY), IsClosed = false };
        fig.Segments.Add(new ArcSegment(new Point(endX, endY), new Size(r, r), 0, isLargeArc, SweepDirection.Clockwise, true));
        geom.Figures.Add(fig);

        var arcPath = new System.Windows.Shapes.Path
        {
            Data = geom,
            Stroke = strokeBrush ?? CyanBrush,
            StrokeThickness = 6.0,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };
        grid.Children.Add(arcPath);

        var text = new TextBlock
        {
            Text = $"{pct:0}%",
            FontSize = 13.5,
            FontWeight = FontWeights.Bold,
            Foreground = TextBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        grid.Children.Add(text);

        return grid;
    }

    private Border AddCard(string title, string description, Brush accent, params (string text, NexIcon? icon, Brush? color, Func<Task> action)[] actions)
    {
        if (cardGrid == null) throw new InvalidOperationException("Page not initialized.");
        var art = title.Contains("Gaming", StringComparison.OrdinalIgnoreCase) ? FeatureArt.Gaming :
                  title.Contains("Snapshot", StringComparison.OrdinalIgnoreCase) ? FeatureArt.Snapshot :
                  title.Contains("Stare", StringComparison.OrdinalIgnoreCase) || title.Contains("Status", StringComparison.OrdinalIgnoreCase) ? FeatureArt.Status :
                  title.Contains("Performance", StringComparison.OrdinalIgnoreCase) || title.Contains("Măsurători", StringComparison.OrdinalIgnoreCase) ? FeatureArt.Performance :
                  title.Contains("AI", StringComparison.OrdinalIgnoreCase) || title.Contains("Recall", StringComparison.OrdinalIgnoreCase) || title.Contains("Privacy", StringComparison.OrdinalIgnoreCase) ? FeatureArt.Ai :
                  title.Contains("Services", StringComparison.OrdinalIgnoreCase) || title.Contains("Servicii", StringComparison.OrdinalIgnoreCase) || title.Contains("Debloat", StringComparison.OrdinalIgnoreCase) || title.Contains("Mentenanță", StringComparison.OrdinalIgnoreCase) || title.Contains("Efecte", StringComparison.OrdinalIgnoreCase) ? FeatureArt.Services :
                  title.Contains("Disk", StringComparison.OrdinalIgnoreCase) || title.Contains("Storage", StringComparison.OrdinalIgnoreCase) ? FeatureArt.Disk :
                  title.Contains("RAM", StringComparison.OrdinalIgnoreCase) || title.Contains("Lasso", StringComparison.OrdinalIgnoreCase) || title.Contains("ProBalance", StringComparison.OrdinalIgnoreCase) || title.Contains("SmartTrim", StringComparison.OrdinalIgnoreCase) ? FeatureArt.Ram :
                  title.Contains("Startup", StringComparison.OrdinalIgnoreCase) || title.Contains("Pornire", StringComparison.OrdinalIgnoreCase) ? FeatureArt.Startup :
                  title.Contains("Classic", StringComparison.OrdinalIgnoreCase) || title.Contains("Clasic", StringComparison.OrdinalIgnoreCase) || title.Contains("Notepad", StringComparison.OrdinalIgnoreCase) || title.Contains("Photo", StringComparison.OrdinalIgnoreCase) || title.Contains("Paint", StringComparison.OrdinalIgnoreCase) ? FeatureArt.Classic :
                  FeatureArt.Boost;

        var card = new Border
        {
            Tag = "card",
            Height = 126,
            Margin = new Thickness(0, 0, 0, 11),
            BorderBrush = new SolidColorBrush(Color.FromRgb(24, 36, 52)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Background = CardBackground(),
            ClipToBounds = true
        };

        var layout = new Grid();
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(270) });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });

        // Column 0: Circular Badge
        var icon = FeatureIcon(art, accent);
        Grid.SetColumn(icon, 0);
        layout.Children.Add(icon);

        // Column 1: Title, Subtitle, Buttons
        var copy = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 10, 0) };
        copy.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 15.5,
            FontWeight = FontWeights.Bold,
            Foreground = TextBrush
        });
        copy.Children.Add(new TextBlock
        {
            Text = description,
            FontSize = 12,
            Foreground = MutedBrush,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 3, 0, 10),
            MaxWidth = 600
        });

        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        double btnWidth = actions.Length == 1 ? 150 : 138;
        for (var index = 0; index < actions.Length; index++)
        {
            var isPrimary = actions[index].text.Equals("Aplică", StringComparison.OrdinalIgnoreCase) ||
                            actions[index].text.Equals("Apply", StringComparison.OrdinalIgnoreCase) ||
                            actions[index].text.Equals(NexLocale.T("btn_apply", "Aplică"), StringComparison.OrdinalIgnoreCase);
            buttons.Children.Add(MakeCardButton(actions[index].text, actions[index].icon, actions[index].color, actions[index].action, isPrimary, btnWidth));
        }
        copy.Children.Add(buttons);
        Grid.SetColumn(copy, 1);
        layout.Children.Add(copy);

        // Column 2: Real 3D Graphic Illustration
        var illustration = FeatureIllustration(art);
        Grid.SetColumn(illustration, 2);
        layout.Children.Add(illustration);

        // Column 3: Interactive Chevron Arrow Button
        var arrowBtn = new Border
        {
            Cursor = Cursors.Hand,
            Background = Brushes.Transparent,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ToolTip = NexLocale.T("common_click_configure_tooltip", "Apasă pentru configurare detaliată"),
            Child = new TextBlock
            {
                Text = "›",
                FontSize = 26,
                FontWeight = FontWeights.Light,
                Foreground = new SolidColorBrush(Color.FromRgb(120, 140, 165)),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            }
        };
        arrowBtn.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            OpenCardConfig(title);
        };
        Grid.SetColumn(arrowBtn, 3);
        layout.Children.Add(arrowBtn);

        // Card Click opens configuration (excluding direct clicks on buttons)
        card.Cursor = Cursors.Hand;
        card.MouseLeftButtonUp += (s, e) =>
        {
            if (e.OriginalSource is DependencyObject dep && FindParent<Button>(dep) != null) return;
            OpenCardConfig(title);
        };

        card.Child = layout;
        cardGrid.Children.Add(card);
        ResizeCards();
        return card;
    }

    private Border AddActionRow(string title, string description, NexIcon icon, string badgeText, Brush accentBrush, string technicalDetails, params RowAction[] buttons)
    {
        return AddActionRow(title, description, null, icon, badgeText, null, accentBrush, technicalDetails, buttons);
    }

    private Border AddActionRow(string title, string description, NexIcon icon, string badgeText, Brush? badgeColor, Brush accentBrush, string technicalDetails, params RowAction[] buttons)
    {
        return AddActionRow(title, description, null, icon, badgeText, badgeColor, accentBrush, technicalDetails, buttons);
    }

    private Border AddActionRow(string title, string description, ImageSource? realIcon, NexIcon icon, string badgeText, Brush? badgeColor, Brush accentBrush, string technicalDetails, params RowAction[] buttons)
    {
        if (cardGrid == null) throw new InvalidOperationException("Page not initialized.");

        var card = new Border
        {
            Tag = "actionRow",
            MinHeight = 80,
            Margin = new Thickness(0, 0, 0, 9),
            BorderBrush = new SolidColorBrush(Color.FromRgb(24, 36, 52)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Background = CardBackground(),
            Padding = new Thickness(16, 12, 16, 12)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) }); // Fixed track for parallel buttons with full text fit

        // Col 0: Crisp Real Icon or Vector Icon Box with Category Color Tint
        var iconBgColor = Color.FromArgb(24, 56, 189, 248);
        var iconBorderColor = Color.FromArgb(50, 56, 189, 248);
        if (accentBrush is SolidColorBrush scbAccent)
        {
            var c = scbAccent.Color;
            iconBgColor = Color.FromArgb(28, c.R, c.G, c.B);
            iconBorderColor = Color.FromArgb(65, c.R, c.G, c.B);
        }

        var iconBox = new Border
        {
            Width = 40,
            Height = 40,
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(iconBgColor),
            BorderBrush = new SolidColorBrush(iconBorderColor),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (realIcon != null)
        {
            var img = new Image
            {
                Source = realIcon,
                Width = 24,
                Height = 24,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            iconBox.Child = img;
        }
        else
        {
            iconBox.Child = CreateVectorIcon(icon, accentBrush, 20);
        }

        Grid.SetColumn(iconBox, 0);
        grid.Children.Add(iconBox);

        // Col 1: Text info
        var contentPanel = new StackPanel
        {
            Margin = new Thickness(10, 0, 14, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        var titlePanel = new StackPanel { Orientation = Orientation.Horizontal };
        titlePanel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = TextBrush,
            VerticalAlignment = VerticalAlignment.Center
        });

        if (!string.IsNullOrEmpty(badgeText))
        {
            var bColor = badgeColor;
            if (bColor == null)
            {
                if (badgeText.Contains("Activ") && !badgeText.Contains("Inactiv")) bColor = GreenBrush;
                else if (badgeText.Contains("Dezactivat")) bColor = GreenBrush;
                else if (badgeText.Contains("Inactiv") || badgeText.Contains("Neconfigurat")) bColor = MutedBrush;
                else if (badgeText.Contains("Restart") || badgeText.Contains("Jocuri")) bColor = AmberBrush;
                else if (badgeText.Contains("Zero") || badgeText.Contains("Confidențialitate")) bColor = PurpleBrush;
                else bColor = GreenBrush;
            }

            var scb = (bColor as SolidColorBrush) ?? (SolidColorBrush)GreenBrush;
            var c = scb.Color;
            var badge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(32, c.R, c.G, c.B)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(65, c.R, c.G, c.B)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(7, 2, 7, 2),
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = badgeText,
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = bColor
                }
            };
            titlePanel.Children.Add(badge);
        }
        contentPanel.Children.Add(titlePanel);

        contentPanel.Children.Add(new TextBlock
        {
            Text = description,
            FontSize = 11.5,
            Foreground = MutedBrush,
            Margin = new Thickness(0, 3, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 620
        });

        if (!string.IsNullOrEmpty(technicalDetails))
        {
            contentPanel.Children.Add(new TextBlock
            {
                Text = technicalDetails,
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(85, 105, 128)),
                FontFamily = new FontFamily("Consolas"),
                Margin = new Thickness(0, 4, 0, 0)
            });
        }
        Grid.SetColumn(contentPanel, 1);
        grid.Children.Add(contentPanel);

        // Col 2: Action Buttons - Strictly parallel alignment grid
        var btnGrid = new Grid
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(236) });
        btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });

        if (buttons.Length == 1)
        {
            var b0 = MakeActionRowButton(buttons[0]);
            b0.Width = 236;
            Grid.SetColumn(b0, 0);
            btnGrid.Children.Add(b0);
        }
        else if (buttons.Length >= 2 && buttons[1].IsRevert)
        {
            var b0 = MakeActionRowButton(buttons[0]);
            b0.Width = 236;
            Grid.SetColumn(b0, 0);
            btnGrid.Children.Add(b0);

            var b1 = MakeActionRowButton(buttons[1]);
            b1.Width = 36;
            Grid.SetColumn(b1, 2);
            btnGrid.Children.Add(b1);
        }
        else if (buttons.Length == 2)
        {
            btnGrid.ColumnDefinitions.Clear();
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(136) });
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(136) });

            var b0 = MakeActionRowButton(buttons[0]);
            b0.Width = 136;
            Grid.SetColumn(b0, 0);
            btnGrid.Children.Add(b0);

            var b1 = MakeActionRowButton(buttons[1]);
            b1.Width = 136;
            Grid.SetColumn(b1, 2);
            btnGrid.Children.Add(b1);
        }
        Grid.SetColumn(btnGrid, 2);
        grid.Children.Add(btnGrid);

        card.Child = grid;
        cardGrid.Children.Add(card);
        ResizeCards();
        return card;
    }

    private Button MakeActionRowButton(RowAction act)
    {
        var button = new Button
        {
            Cursor = Cursors.Hand,
            ToolTip = string.IsNullOrEmpty(act.Tooltip) ? act.Text : act.Tooltip
        };

        if (act.IsRevert)
        {
            button.Style = (Style)FindResource("ActionIconButtonStyle");
            button.Width = 36;
            button.Height = 34;
            button.Margin = new Thickness(0);
            button.Content = CreateVectorIcon(NexIcon.Revert, new SolidColorBrush(Color.FromRgb(142, 158, 175)), 14);
        }
        else
        {
            button.Height = 34;
            button.Margin = new Thickness(0);
            bool isActivation = act.Text.StartsWith("Activează", StringComparison.OrdinalIgnoreCase) ||
                                act.Text.StartsWith("Activate", StringComparison.OrdinalIgnoreCase) ||
                                act.Text.StartsWith("Enable", StringComparison.OrdinalIgnoreCase) ||
                                act.Text.StartsWith("Reactivează", StringComparison.OrdinalIgnoreCase) ||
                                act.Text.StartsWith("Reactivate", StringComparison.OrdinalIgnoreCase) ||
                                act.Text.StartsWith("Restaurează", StringComparison.OrdinalIgnoreCase) ||
                                act.Text.StartsWith("Restore", StringComparison.OrdinalIgnoreCase);

            bool isDanger = !isActivation && act.Color == RedBrush;

            if (isDanger)
            {
                button.Style = (Style)FindResource("DangerActionButtonStyle");
                button.Padding = new Thickness(0);
            }
            else if (isActivation || act.Color == CyanBrush)
            {
                button.Style = (Style)FindResource("BlueGradientButtonStyle");
                button.Padding = new Thickness(0);
            }
            else if (act.IsPrimary)
            {
                button.Style = (Style)FindResource("BlueGradientButtonStyle");
                button.Padding = new Thickness(0);
            }
            else
            {
                button.Style = (Style)FindResource("SecondaryButtonStyle");
                button.Padding = new Thickness(0);
            }

            var stack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            if (act.Icon.HasValue)
            {
                var iconView = CreateVectorIcon(act.Icon.Value, (isDanger || act.IsPrimary) ? Brushes.White : act.Color, 13);
                iconView.Margin = new Thickness(0, 0, 6, 0);
                stack.Children.Add(iconView);
            }
            stack.Children.Add(new TextBlock
            {
                Text = act.Text,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            });
            button.Content = stack;
        }

        button.Click += async (_, _) => await act.Action();
        return button;
    }

    private void OpenCardConfig(string title)
    {
        if (title.Contains("Boost", StringComparison.OrdinalIgnoreCase))
            ShowOneClickBoostModal();
        else if (title.Contains("Gaming", StringComparison.OrdinalIgnoreCase))
            ShowGamingModal();
        else if (title.Contains("Snapshot", StringComparison.OrdinalIgnoreCase))
            ShowSnapshotModal();
        else if (title.Contains("Stare", StringComparison.OrdinalIgnoreCase) || title.Contains("Status", StringComparison.OrdinalIgnoreCase))
            ShowPerformance();
        else if (title.Contains("Disk", StringComparison.OrdinalIgnoreCase))
            ShowDisk();
        else if (title.Contains("Process", StringComparison.OrdinalIgnoreCase))
            ShowProcesses();
    }

    private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        var parent = VisualTreeHelper.GetParent(child);
        while (parent != null)
        {
            if (parent is T typed) return typed;
            parent = VisualTreeHelper.GetParent(parent);
        }
        return null;
    }

    private Button MakeButton(string text, Brush color, Func<Task> action) =>
        MakeButton(text, null, color, action);

    private Button MakeButton(string text, NexIcon? icon, Brush color, Func<Task> action)
    {
        var button = new Button
        {
            Style = (Style)FindResource("SecondaryButtonStyle"),
            Foreground = color,
            BorderBrush = new SolidColorBrush(Color.FromRgb(42, 58, 77)),
            Margin = new Thickness(4, 0, 4, 0),
            FontSize = 11.5,
            Height = 32,
            MinWidth = 90,
            Cursor = Cursors.Hand
        };
        var stack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        if (icon.HasValue)
        {
            var iconView = CreateVectorIcon(icon.Value, color, 12);
            iconView.Margin = new Thickness(0, 0, 6, 0);
            stack.Children.Add(iconView);
        }
        stack.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
        button.Content = stack;
        button.Click += async (_, _) => await action();
        return button;
    }

    private Button MakeCardButton(string text, NexIcon? icon, Brush? color, Func<Task> action, bool isPrimary, double fixedWidth = 138)
    {
        var button = new Button
        {
            Margin = new Thickness(0, 0, 10, 0),
            Cursor = Cursors.Hand,
            Height = 34,
            Width = fixedWidth
        };

        if (isPrimary)
        {
            button.Style = (Style)FindResource("PrimaryGradientButtonStyle");
            button.Padding = new Thickness(0);
        }
        else
        {
            button.Style = (Style)FindResource("SecondaryButtonStyle");
            button.Padding = new Thickness(0);
        }

        var stack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        if (icon.HasValue)
        {
            var iconView = CreateVectorIcon(icon.Value, isPrimary ? Brushes.White : (color ?? MutedBrush), 13);
            iconView.Margin = new Thickness(0, 0, 7, 0);
            stack.Children.Add(iconView);
        }
        stack.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });
        button.Content = stack;

        button.Click += async (_, _) => await action();
        return button;
    }

    private void AddDashboardHero(string title, string subtitle)
    {
        var hero = new StackPanel { Margin = new Thickness(0, 2, 0, 16) };
        hero.Children.Add(new TextBlock { Text = title, FontSize = 28, FontWeight = FontWeights.Bold, Foreground = TextBrush });
        hero.Children.Add(new TextBlock { Text = subtitle, FontSize = 12.5, Foreground = MutedBrush, Margin = new Thickness(0, 4, 0, 0) });
        PageRoot.Children.Add(hero);
    }

    private void AddPageHero(string title, string subtitle)
    {
        var hero = new StackPanel { Margin = new Thickness(0, 2, 0, 18) };
        hero.Children.Add(new TextBlock { Text = title, FontSize = 24, FontWeight = FontWeights.Bold, Foreground = TextBrush });
        hero.Children.Add(new TextBlock { Text = subtitle, FontSize = 12.5, Foreground = MutedBrush, Margin = new Thickness(0, 3, 0, 0) });
        PageRoot.Children.Add(hero);
    }

    private Border FeatureIcon(FeatureArt art, Brush accent)
    {
        var container = new Border
        {
            Width = 50,
            Height = 50,
            CornerRadius = new CornerRadius(25),
            Background = new SolidColorBrush(Color.FromRgb(17, 24, 34)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(27, 39, 54)),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var icon = art switch
        {
            FeatureArt.Gaming => CreateVectorIcon(NexIcon.Gamepad, accent, 24),
            FeatureArt.Snapshot => CreateVectorIcon(NexIcon.Layers, accent, 24),
            FeatureArt.Status => CreateVectorIcon(NexIcon.Cpu, accent, 24),
            _ => CreateVectorIcon(NexIcon.Gauge, accent, 24)
        };

        container.Child = icon;
        return container;
    }

    private FrameworkElement FeatureIllustration(FeatureArt art)
    {
        var filename = art switch
        {
            FeatureArt.Boost => "card_boost.png",
            FeatureArt.Gaming => "card_gaming.png",
            FeatureArt.Snapshot => "card_snapshot.png",
            _ => "card_status.png"
        };

        var mask = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 0)
        };
        mask.GradientStops.Add(new GradientStop(Colors.Transparent, 0.0));
        mask.GradientStops.Add(new GradientStop(Color.FromArgb(40, 0, 0, 0), 0.22));
        mask.GradientStops.Add(new GradientStop(Color.FromArgb(160, 0, 0, 0), 0.52));
        mask.GradientStops.Add(new GradientStop(Colors.Black, 0.82));
        mask.GradientStops.Add(new GradientStop(Colors.Black, 1.0));

        try
        {
            var uri = new Uri($"pack://application:,,,/assets/{filename}", UriKind.Absolute);
            var img = new Image
            {
                Source = new BitmapImage(uri),
                Stretch = Stretch.UniformToFill,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Height = 124,
                Width = 270,
                Opacity = 0.88,
                OpacityMask = mask
            };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            return img;
        }
        catch
        {
            var localPath = Path.Combine(AppContext.BaseDirectory, "assets", filename);
            if (File.Exists(localPath))
            {
                return new Image
                {
                    Source = new BitmapImage(new Uri(localPath, UriKind.Absolute)),
                    Stretch = Stretch.UniformToFill,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center,
                    Height = 124,
                    Width = 270,
                    Opacity = 0.88,
                    OpacityMask = mask
                };
            }
            return new Grid { Width = 270, Height = 124 };
        }
    }

    private Brush CardBackground()
    {
        return new LinearGradientBrush(
            Color.FromRgb(13, 21, 32),
            Color.FromRgb(10, 16, 24),
            new Point(0, 0),
            new Point(0, 1)
        );
    }

    private static Border CreateHardwareBadge(NexIcon icon, Color primaryCol, Color glowCol, double size = 38, double icoSize = 18)
    {
        var b = new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(Math.Max(6, size * 0.26)),
            Background = new LinearGradientBrush(
                Color.FromArgb(240, primaryCol.R, primaryCol.G, primaryCol.B),
                Color.FromArgb(170, (byte)(primaryCol.R * 0.45), (byte)(primaryCol.G * 0.45), (byte)(primaryCol.B * 0.45)),
                new Point(0, 0), new Point(1, 1)),
            BorderBrush = new LinearGradientBrush(
                Color.FromArgb(235, glowCol.R, glowCol.G, glowCol.B),
                Color.FromArgb(100, primaryCol.R, primaryCol.G, primaryCol.B),
                new Point(0, 0), new Point(1, 1)),
            BorderThickness = new Thickness(1.2),
            Child = CreateVectorIcon(icon, Brushes.White, icoSize)
        };
        b.Effect = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = primaryCol,
            BlurRadius = 12,
            ShadowDepth = 1,
            Opacity = 0.38
        };
        return b;
    }

    private enum FeatureArt { Boost, Gaming, Snapshot, Status, Performance, Ai, Services, Disk, Ram, Startup, Classic }


    private (UIElement element, Action<double> pushValue) CreateDynamicSparkline(Brush waveBrush, double height = 28)
    {
        var grid = new Grid { Height = height, Margin = new Thickness(0, 8, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var labels = new Grid { VerticalAlignment = VerticalAlignment.Stretch };
        labels.RowDefinitions.Add(new RowDefinition());
        labels.RowDefinitions.Add(new RowDefinition());
        labels.RowDefinitions.Add(new RowDefinition());
        labels.Children.Add(new TextBlock { Text = "100%", FontSize = 7.5, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)), VerticalAlignment = VerticalAlignment.Top });
        var t50 = new TextBlock { Text = "50%", FontSize = 7.5, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetRow(t50, 1); labels.Children.Add(t50);
        var t0 = new TextBlock { Text = "0%", FontSize = 7.5, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)), VerticalAlignment = VerticalAlignment.Bottom };
        Grid.SetRow(t0, 2); labels.Children.Add(t0);
        Grid.SetColumn(labels, 0);
        grid.Children.Add(labels);

        var pathGeom = new PathGeometry();
        var pathFig = new PathFigure { IsClosed = false };
        pathGeom.Figures.Add(pathFig);

        var wavePath = new System.Windows.Shapes.Path
        {
            Data = pathGeom,
            Stroke = waveBrush,
            StrokeThickness = 1.6,
            Opacity = 0.9
        };
        Grid.SetColumn(wavePath, 1);
        grid.Children.Add(wavePath);

        var history = new List<double>();
        for (int i = 0; i < 20; i++) history.Add(12.0);

        void PushValue(double v)
        {
            history.Add(Math.Clamp(v, 0, 100));
            if (history.Count > 24) history.RemoveAt(0);

            pathFig.Segments.Clear();
            double w = 220.0;
            double stepX = w / Math.Max(1, history.Count - 1);

            for (int i = 0; i < history.Count; i++)
            {
                double x = i * stepX;
                double y = height - (history[i] / 100.0 * (height - 4)) - 2;
                if (i == 0)
                {
                    pathFig.StartPoint = new Point(x, y);
                }
                else
                {
                    pathFig.Segments.Add(new LineSegment(new Point(x, y), true));
                }
            }
        }

        PushValue(12.0);
        return (grid, PushValue);
    }


    private sealed record ScriptResult(int ExitCode, string Stdout, string Stderr) { public bool Success => ExitCode == 0; }
    private sealed record PerfSnapshot(DateTimeOffset At, string StatusJson);
    private sealed record ProcessRow(string Name, int Pid, string Memory);
    private sealed record DiskRow(string Name, string Type, string Size, long RawBytes = 0);
    private sealed class SystemStatus
    {
        public int ProcessCount { get; set; }
        public double TotalMemoryGB { get; set; }
        public double UsedMemoryGB { get; set; }
        public double MemoryPercent { get; set; }
        public double CpuLoad { get; set; }
        public string OsName { get; set; } = "Microsoft Windows 11 Pro";
        public string OsBuild { get; set; } = "26200";
        public string PowerPlan { get; set; } = "High Performance";
    }
}