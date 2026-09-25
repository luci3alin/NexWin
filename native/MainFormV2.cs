using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace NexWin.Native;

internal sealed class MainFormV2 : Form
{
    private readonly Color Background = Color.FromArgb(7, 7, 10);
    private readonly Color Sidebar = Color.FromArgb(10, 10, 16);
    private readonly Color Card = Color.FromArgb(15, 15, 24);
    private readonly Color Hover = Color.FromArgb(25, 24, 40);
    private readonly Color Muted = Color.FromArgb(155, 165, 185);
    private readonly Color Purple = Color.FromArgb(139, 92, 246);
    private readonly Color Cyan = Color.FromArgb(6, 182, 212);
    private readonly Color Green = Color.FromArgb(52, 211, 153);
    private readonly Color Amber = Color.FromArgb(251, 191, 36);
    private readonly Color Red = Color.FromArgb(248, 113, 113);
    private readonly string ScriptsDirectory = Path.Combine(AppContext.BaseDirectory, "scripts", "powershell");
    private readonly HashSet<string> AllowedScripts = new(StringComparer.OrdinalIgnoreCase)
    {
        "Get-SystemStatus.ps1", "Get-ProcessList.ps1", "Get-StartupApps.ps1", "Get-DiskAnalyzer.ps1",
        "Invoke-DnsBenchmark.ps1", "Invoke-AiRemoval.ps1", "Invoke-GamingTweaks.ps1", "Invoke-UsbOptimization.ps1",
        "Invoke-GpuDriverProtection.ps1", "Invoke-DebloatServices.ps1", "Invoke-ProcessLasso.ps1", "Invoke-ClassicApps.ps1",
        "Invoke-VisualEffects.ps1", "Invoke-Maintenance.ps1", "Invoke-RestorePoint.ps1", "Invoke-OneClickBoost.ps1",
        "Stop-ProcessSafe.ps1", "Save-NexWinSnapshot.ps1", "Restore-NexWinSnapshot.ps1"
    };
    private readonly Panel content = new() { Dock = DockStyle.Fill, BackColor = Color.FromArgb(7, 7, 10), AutoScroll = true };
    private readonly RichTextBox log = new() { Dock = DockStyle.Fill, ReadOnly = true, BorderStyle = BorderStyle.None, BackColor = Color.FromArgb(10, 10, 16), ForeColor = Color.FromArgb(190, 200, 215), Font = new Font("Consolas", 9) };
    private readonly Label osLabel = new() { AutoSize = true, ForeColor = Color.White, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
    private readonly Label osDetail = new() { AutoSize = true, ForeColor = Color.FromArgb(155, 165, 185), Font = new Font("Segoe UI", 8) };
    private readonly Label processValue = new() { AutoSize = true, ForeColor = Color.White, Font = new Font("Segoe UI", 8, FontStyle.Bold) };
    private readonly Label ramValue = new() { AutoSize = true, ForeColor = Color.White, Font = new Font("Segoe UI", 8, FontStyle.Bold) };
    private readonly Label cpuValue = new() { AutoSize = true, ForeColor = Color.White, Font = new Font("Segoe UI", 8, FontStyle.Bold) };
    private readonly Button refreshButton;
    private readonly CancellationTokenSource lifetime = new();
    private NavButton? activeNav;
    private bool operationRunning;
    private SystemStatus? currentStatus;

    public MainFormV2()
    {
        Text = "NexWin - Windows 11 Optimizer";
        ClientSize = new Size(1240, 820); MinimumSize = new Size(1050, 680);
        BackColor = Background; ForeColor = Color.White; Font = new Font("Segoe UI", 9);
        StartPosition = FormStartPosition.CenterScreen;
        var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, BackColor = Background, Margin = Padding.Empty, Padding = Padding.Empty };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 276)); shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 76)); shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(shell);
        var sidebar = BuildSidebar(); shell.Controls.Add(sidebar, 0, 0); shell.SetRowSpan(sidebar, 2);
        refreshButton = ActionButton("↻  Refresh", Cyan, RefreshStatusAsync); refreshButton.Dock = DockStyle.Fill; refreshButton.Margin = new Padding(8, 4, 0, 4);
        shell.Controls.Add(BuildHeader(), 1, 0); shell.Controls.Add(content, 1, 1);
        HandleCreated += (_, _) => ApplyDarkTitleBar();
        Shown += async (_, _) => { ShowDashboard(); await RefreshStatusAsync(); };
        FormClosed += (_, _) => lifetime.Cancel();
    }

    private Control BuildSidebar()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Sidebar, Padding = new Padding(14, 12, 14, 12) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Sidebar, Margin = Padding.Empty, Padding = Padding.Empty };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 116));
        var brand = new Panel { Dock = DockStyle.Fill, BackColor = Sidebar };
        var logo = new PictureBox { Location = new Point(2, 7), Size = new Size(43, 43), SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent };
        var logoPath = Path.Combine(AppContext.BaseDirectory, "logo.png");
        if (!File.Exists(logoPath)) logoPath = Path.Combine(AppContext.BaseDirectory, "logo.ico");
        if (File.Exists(logoPath))
        {
            try
            {
                if (logoPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) { using var image = Image.FromFile(logoPath); logo.Image = new Bitmap(image); }
                else { using var icon = new Icon(logoPath); logo.Image = icon.ToBitmap(); }
            }
            catch { }
        }
        brand.Controls.Add(logo);
        brand.Controls.Add(new Label { Text = "NexWin", AutoSize = true, Location = new Point(56, 7), Font = new Font("Segoe UI", 16, FontStyle.Bold), ForeColor = Color.White });
        brand.Controls.Add(new Label { Text = "Windows 11 Tuning", AutoSize = true, Location = new Point(57, 35), Font = new Font("Segoe UI", 8), ForeColor = Cyan });
        layout.Controls.Add(brand, 0, 0);
        var nav = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Sidebar, Padding = new Padding(0, 3, 0, 0), Margin = Padding.Empty };
        nav.Controls.Add(new Label { Text = "MODULE OPTIMIZARE", AutoSize = true, ForeColor = Color.FromArgb(120, 130, 150), Font = new Font("Segoe UI", 8, FontStyle.Bold), Location = new Point(10, 3) });
        AddNav(nav, "▦", "Dashboard", ShowDashboard); AddNav(nav, "◌", "Profiluri & Presets", ShowProfiles); AddNav(nav, "∿", "Performance Lab", ShowPerformance);
        AddNav(nav, "ϟ", "Gaming Optimizations", () => ShowActions("Gaming Optimizations", new[] { ("Aplică profil gaming", "Invoke-GamingTweaks.ps1", new[] { "-All" }), ("Revert gaming", "Invoke-GamingTweaks.ps1", new[] { "-Revert" }), ("Optimizează rețeaua", "Invoke-GamingTweaks.ps1", new[] { "-OptimizeNetwork" }), ("Benchmark DNS", "Invoke-DnsBenchmark.ps1", new[] { "-Benchmark" }) }));
        AddNav(nav, "◈", "AI & Recall Removal", () => ShowActions("AI & Recall Removal", new[] { ("Elimină componente AI", "Invoke-AiRemoval.ps1", new[] { "-All" }), ("Restaurează AI", "Invoke-AiRemoval.ps1", new[] { "-Revert" }), ("Reduce telemetria", "Invoke-DebloatServices.ps1", new[] { "-DisableTelemetry" }) }));
        AddNav(nav, "≡", "Services & Debloat", () => ShowActions("Services & Debloat", new[] { ("Debloat servicii", "Invoke-DebloatServices.ps1", new[] { "-All" }), ("Restaurează servicii", "Invoke-DebloatServices.ps1", new[] { "-Revert" }), ("Efecte vizuale", "Invoke-VisualEffects.ps1", new[] { "-Optimize" }), ("Mentenanță", "Invoke-Maintenance.ps1", new[] { "-CleanTemp", "-CleanShaderCache", "-RunTrim" }) }));
        AddNav(nav, "▤", "Disk Analyzer & Storage", ShowDisk); AddNav(nav, "◉", "Process Inspector", ShowProcesses);
        AddNav(nav, "↥", "Startup Manager", () => ShowActions("Startup Manager", new[] { ("Scanează aplicațiile de pornire", "Get-StartupApps.ps1", Array.Empty<string>()) }));
        AddNav(nav, "⇆", "Process Lasso & RAM", () => ShowActions("Process Lasso & RAM", new[] { ("SmartTrim RAM", "Invoke-ProcessLasso.ps1", new[] { "-SmartTrim" }), ("ProBalance", "Invoke-ProcessLasso.ps1", new[] { "-ProBalance" }), ("Reduce power throttling", "Invoke-ProcessLasso.ps1", new[] { "-DisablePowerThrottling" }) }));
        AddNav(nav, "⌁", "Classic Applications", () => ShowActions("Classic Applications", new[] { ("Photo Viewer clasic", "Invoke-ClassicApps.ps1", new[] { "-EnablePhotoViewer" }), ("Notepad clasic", "Invoke-ClassicApps.ps1", new[] { "-EnableClassicNotepad" }), ("Paint clasic", "Invoke-ClassicApps.ps1", new[] { "-EnableClassicPaint" }) }));
        layout.Controls.Add(nav, 0, 1);
        var footer = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(8, 8, 13), Padding = new Padding(0, 8, 0, 0) };
        var target = new Panel { Dock = DockStyle.Top, Height = 38, BackColor = Color.FromArgb(16, 16, 26), BorderStyle = BorderStyle.FixedSingle };
        target.Controls.Add(new Label { Text = "✓", AutoSize = true, ForeColor = Green, Font = new Font("Segoe UI", 11, FontStyle.Bold), Location = new Point(10, 8) });
        target.Controls.Add(new Label { Text = "Target procese", AutoSize = true, ForeColor = Color.FromArgb(190, 200, 220), Font = new Font("Segoe UI", 8), Location = new Point(34, 10) });
        target.Controls.Add(new Label { Text = "< 200", AutoSize = true, ForeColor = Green, Font = new Font("Consolas", 8, FontStyle.Bold), Location = new Point(174, 10) }); footer.Controls.Add(target);
        var logButton = ActionButton("〉  Consolă PowerShell Live", Color.FromArgb(145, 155, 180), () => { ShowLogs(); return Task.CompletedTask; }); logButton.Dock = DockStyle.Bottom; logButton.Height = 34; footer.Controls.Add(logButton);
        layout.Controls.Add(footer, 0, 2); panel.Controls.Add(layout); return panel;
    }

    private void AddNav(Panel nav, string icon, string text, Action action)
    {
        var y = nav.Controls.OfType<Control>().Select(c => c.Bottom).DefaultIfEmpty(0).Max() + 5;
        var button = new NavButton { IconText = icon, LabelText = text, Width = 226, Height = 40, Location = new Point(0, y), BackColor = Sidebar, ForeColor = Color.FromArgb(175, 185, 205), Cursor = Cursors.Hand };
        button.Click += (_, _) => { SelectNav(button); action(); }; button.MouseEnter += (_, _) => { if (button != activeNav) button.BackColor = Hover; }; button.MouseLeave += (_, _) => { if (button != activeNav) button.BackColor = Color.Transparent; };
        nav.Controls.Add(button); if (activeNav == null && text == "Dashboard") SelectNav(button);
    }

    private void SelectNav(NavButton button)
    {
        if (activeNav != null) { activeNav.IsActive = false; activeNav.BackColor = Sidebar; activeNav.ForeColor = Color.FromArgb(175, 185, 205); }
        activeNav = button; activeNav.IsActive = true; activeNav.BackColor = Color.FromArgb(51, 17, 78); activeNav.ForeColor = Color.White;
    }

    private Control BuildHeader()
    {
        var header = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(10, 10, 16), Padding = new Padding(18, 10, 18, 10) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 1, BackColor = Color.FromArgb(10, 10, 16), Margin = Padding.Empty, Padding = Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 17)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 19)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
        var system = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 5, 10, 0) }; system.Controls.Add(osLabel); osDetail.Location = new Point(0, 27); system.Controls.Add(osDetail); layout.Controls.Add(system, 0, 0);
        layout.Controls.Add(HeaderGauge("∿", "Procese", processValue, Purple), 1, 0); layout.Controls.Add(HeaderGauge("▤", "Memorie RAM", ramValue, Cyan), 2, 0); layout.Controls.Add(HeaderGauge("◉", "Încărcare CPU", cpuValue, Green), 3, 0); layout.Controls.Add(refreshButton, 4, 0);
        header.Controls.Add(layout); return header;
    }

    private Control HeaderGauge(string icon, string title, Label value, Color accent)
    {
        var card = new RoundedCard { Dock = DockStyle.Fill, BackColor = Color.FromArgb(18, 18, 29), AccentColor = accent, Margin = new Padding(4, 0, 4, 0), Padding = new Padding(0) };
        card.Controls.Add(new Label { Text = icon, AutoSize = true, ForeColor = accent, Font = new Font("Segoe UI Symbol", 10), Location = new Point(9, 10) }); card.Controls.Add(new Label { Text = title, AutoSize = true, ForeColor = Color.FromArgb(165, 175, 195), Font = new Font("Segoe UI", 8), Location = new Point(29, 6) }); value.Location = new Point(29, 25); card.Controls.Add(value); return card;
    }

    private Button ActionButton(string text, Color accent, Func<Task>? action)
    {
        var button = new Button { Text = text, AutoSize = true, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(20, 20, 32), ForeColor = accent, Cursor = Cursors.Hand, Padding = new Padding(12, 0, 12, 0), Margin = new Padding(5, 4, 5, 4), Font = new Font("Segoe UI", 8, FontStyle.Bold) };
        button.FlatAppearance.BorderColor = Color.FromArgb(Math.Min(255, accent.R + 20), Math.Min(255, accent.G + 20), Math.Min(255, accent.B + 20)); button.FlatAppearance.BorderSize = 1; button.FlatAppearance.MouseOverBackColor = Color.FromArgb(31, 29, 49);
        if (action != null) button.Click += async (_, _) => await action(); return button;
    }

    private Panel Page(string title, string subtitle, out FlowLayoutPanel grid)
    {
        content.SuspendLayout(); content.Controls.Clear();
        var body = new Panel { Dock = DockStyle.Fill, BackColor = Background, Padding = new Padding(24, 0, 24, 24) };
        grid = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, AutoScroll = true, BackColor = Background, Padding = new Padding(8, 4, 8, 12), Margin = Padding.Empty };
        var cardGrid = grid;
        cardGrid.Resize += (_, _) => ResizeCards(cardGrid);
        cardGrid.Layout += (_, _) => ResizeCards(cardGrid);
        body.Controls.Add(grid);
        content.Controls.Add(body); content.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 88, BackColor = Background, Padding = new Padding(32, 20, 32, 8), Controls = { new Label { Text = title, AutoSize = true, ForeColor = Color.White, Font = new Font("Segoe UI", 20, FontStyle.Bold), Location = new Point(32, 11) }, new Label { Text = subtitle, AutoSize = true, ForeColor = Muted, Font = new Font("Segoe UI", 9), Location = new Point(34, 49) } } });
        content.ResumeLayout(); return body;
    }

    private void AddCard(FlowLayoutPanel grid, string title, string description, Color accent, params (string text, Color color, Func<Task> action)[] actions)
    {
        var card = new RoundedCard { Width = 430, Height = 166, AccentColor = accent, BackColor = Card, Margin = new Padding(8), Padding = new Padding(18) };
        card.Anchor = AnchorStyles.Left | AnchorStyles.Top;
        card.Controls.Add(new Label { Text = title, AutoSize = true, ForeColor = Color.White, Font = new Font("Segoe UI", 11, FontStyle.Bold), Location = new Point(18, 18) });
        card.Controls.Add(new Label { Text = description, AutoSize = true, MaximumSize = new Size(390, 54), ForeColor = Color.FromArgb(175, 185, 205), Font = new Font("Segoe UI", 8), Location = new Point(18, 49) });
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 43, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = Color.Transparent, Padding = new Padding(0, 2, 0, 0) };
        foreach (var action in actions) buttons.Controls.Add(ActionButton(action.text, action.color, action.action));
        card.Controls.Add(buttons); grid.Controls.Add(card); ResizeCards(grid);
    }

    private static void ResizeCards(FlowLayoutPanel grid)
    {
        if (grid.ClientSize.Width <= 0) return;
        var available = Math.Max(320, grid.ClientSize.Width - grid.Padding.Horizontal - 24);
        var twoColumns = available >= 900;
        var width = twoColumns ? Math.Max(360, (available - 16) / 2) : Math.Max(320, available);
        foreach (var card in grid.Controls.OfType<RoundedCard>())
        {
            if (card.Width != width) card.Width = width;
        }
    }

    private void ShowDashboard()
    {
        var body = Page("Dashboard", "Monitorizează starea sistemului și aplică optimizări controlate.", out var grid);
        AddCard(grid, "One-Click Boost", "Aplică pașii recomandați și creează automat un punct de restaurare înainte de modificări.", Purple, ("Vezi planul", Color.FromArgb(175, 185, 205), () => { MessageBox.Show("Punct de restaurare\nAI și telemetrie\nGaming și energie\nServicii și mentenanță", "Plan One-Click Boost", MessageBoxButtons.OK, MessageBoxIcon.Information); return Task.CompletedTask; }), ("Aplică", Purple, () => ConfirmAndRunAsync("One-Click Boost", "Vor fi aplicate mai multe optimizări Windows. Un punct de restaurare este creat automat.", "Invoke-OneClickBoost.ps1")));
        AddCard(grid, "Profil Gaming", "Energie, GPU și rețea pentru latență redusă. VBS/HVCI pot fi dezactivate.", Amber, ("Aplică profil", Amber, () => ConfirmAndRunAsync("Profil Gaming", "Acest profil poate modifica securitatea, energia și rețeaua Windows.", "Invoke-GamingTweaks.ps1", "-All")));
        AddCard(grid, "Snapshot & rollback", "Salvează configurația înainte de modificări și revino rapid la starea anterioară.", Cyan, ("Salvează snapshot", Cyan, () => RunSafeAsync("Save-NexWinSnapshot.ps1")), ("Restaurează", Amber, () => ConfirmAndRunAsync("Restaurare snapshot", "Vor fi restaurate setările disponibile în snapshot.", "Restore-NexWinSnapshot.ps1")));
        AddCard(grid, "Stare sistem", "Status live pentru procese, memorie, CPU și planul de alimentare.", Green, ("Actualizează", Green, RefreshStatusAsync), ("Performance Lab", Color.FromArgb(175, 185, 205), () => { ShowPerformance(); return Task.CompletedTask; }));
        _ = body;
    }

    private void ShowProfiles()
    {
        Page("Profiluri & Presets", "Alege un profil și verifică pașii înainte să aplici modificările.", out var grid);
        AddProfile(grid, "Balanced", "Optimizări cu risc redus, păstrează protecțiile Windows.", Green, new[] { ("Efecte vizuale", "Invoke-VisualEffects.ps1", new[] { "-Optimize" }) }, false);
        AddProfile(grid, "Gaming", "Energie, GPU și rețea pentru latență redusă.", Amber, new[] { ("Gaming tweaks", "Invoke-GamingTweaks.ps1", new[] { "-All" }), ("Efecte vizuale", "Invoke-VisualEffects.ps1", new[] { "-Optimize" }) }, true);
        AddProfile(grid, "Privacy", "AI și telemetrie reduse, fără dezactivarea Defender.", Cyan, new[] { ("Windows AI", "Invoke-AiRemoval.ps1", new[] { "-All" }), ("Telemetrie", "Invoke-DebloatServices.ps1", new[] { "-DisableTelemetry" }) }, false);
        AddProfile(grid, "Laptop / Battery", "Evită setările agresive de energie și USB.", Green, new[] { ("Efecte vizuale", "Invoke-VisualEffects.ps1", new[] { "-Optimize" }) }, false);
    }

    private void AddProfile(FlowLayoutPanel grid, string name, string description, Color accent, (string label, string script, string[] args)[] steps, bool highRisk)
    {
        AddCard(grid, name + (highRisk ? "  ·  Risc ridicat" : ""), description, accent, ("Vezi planul", Color.FromArgb(175, 185, 205), () => { MessageBox.Show(string.Join(Environment.NewLine, steps.Select(s => "• " + s.label + "  →  " + s.script)), "Plan " + name, MessageBoxButtons.OK, MessageBoxIcon.Information); return Task.CompletedTask; }), ("Aplică", accent, () => highRisk ? ConfirmAndRunManyAsync(name, description, steps) : RunManyAsync(steps)));
    }

    private void ShowPerformance()
    {
        Page("Performance Lab", "Măsurători reale înainte și după optimizare, fără promisiuni fixe de FPS.", out var grid);
        AddCard(grid, "Snapshot performanță", "Salvează RAM, CPU, procese și setările active pentru comparații reale.", Cyan, ("Salvează snapshot curent", Cyan, SavePerformanceSnapshotAsync));
        AddCard(grid, "Comparație înainte / după", "Încarcă ultimele două snapshot-uri și afișează diferențele disponibile.", Purple, ("Compară snapshot-uri", Color.FromArgb(175, 185, 205), CompareSnapshotsAsync));
        var current = currentStatus == null ? "Statusul se încarcă..." : $"Procese: {currentStatus.ProcessCount}\nRAM: {currentStatus.UsedMemoryGB:0.0} / {currentStatus.TotalMemoryGB:0.0} GB\nCPU: {currentStatus.CpuLoad:0.0}%";
        AddCard(grid, "Status măsurat acum", current, Green, ("Refresh", Green, RefreshStatusAsync));
    }

    private void ShowActions(string title, (string label, string script, string[] args)[] actions)
    {
        Page(title, "Operații Windows controlate, cu confirmare pentru setările sensibile și log live.", out var grid);
        foreach (var action in actions) { var accent = action.script.Contains("Revert", StringComparison.OrdinalIgnoreCase) ? Green : Color.FromArgb(175, 185, 205); AddCard(grid, action.label, action.script + " " + string.Join(" ", action.args), accent, ("Execută", accent, () => ExecuteActionAsync(action.label, action.script, action.args))); }
    }

    private Task ExecuteActionAsync(string label, string script, string[] args)
    {
        var risky = script.Contains("Gaming", StringComparison.OrdinalIgnoreCase) || script.Contains("Debloat", StringComparison.OrdinalIgnoreCase) || script.Contains("AiRemoval", StringComparison.OrdinalIgnoreCase) || script.Contains("GpuDriver", StringComparison.OrdinalIgnoreCase);
        return risky ? ConfirmAndRunAsync("Confirmă operația", $"{label}\n\nAceastă acțiune poate modifica setări de securitate sau servicii Windows.", script, args) : RunSafeAsync(script, args);
    }

    private void ShowProcesses()
    {
        var body = Page("Process Inspector", "Procesele active sunt sortate după consumul de memorie; oprește doar procesele de utilizator.", out _);
        var host = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, BackColor = Background, Padding = new Padding(8, 4, 8, 12) }; host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); host.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160)); host.RowStyles.Add(new RowStyle(SizeType.Absolute, 46)); host.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, BackColor = Card, ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, HeaderStyle = ColumnHeaderStyle.Nonclickable, Font = new Font("Segoe UI", 9) }; list.Columns.Add("Proces", 330); list.Columns.Add("PID", 100); list.Columns.Add("Memorie (MB)", 130);
        var refresh = ActionButton("Refresh procese", Cyan, () => LoadProcessesAsync(list)); refresh.Dock = DockStyle.Fill; refresh.Margin = new Padding(0, 4, 8, 4); var kill = ActionButton("Oprește selectat", Red, async () => { if (list.SelectedItems.Count == 0) return; var pid = list.SelectedItems[0].SubItems[1].Text; if (MessageBox.Show("Oprești procesul selectat?", "Confirmare", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes) await RunSafeAsync("Stop-ProcessSafe.ps1", "-Pid", pid); }); kill.Dock = DockStyle.Fill; kill.Margin = new Padding(8, 4, 0, 4);
        host.Controls.Add(refresh, 0, 0); host.Controls.Add(kill, 1, 0); host.Controls.Add(list, 0, 1); host.SetColumnSpan(list, 2); body.Controls.Add(host); _ = LoadProcessesAsync(list);
    }

    private void ShowDisk()
    {
        var body = Page("Disk Analyzer & Storage", "Scanează rapid o locație fără să blochezi interfața.", out _); var host = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, BackColor = Background, Padding = new Padding(8, 4, 8, 12) }; host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); host.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130)); host.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); host.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); host.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var path = new TextBox { Text = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), Dock = DockStyle.Fill, BackColor = Card, ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(0, 4, 8, 4) }; var scan = ActionButton("Scanează", Cyan, null); scan.Dock = DockStyle.Fill; scan.Margin = new Padding(8, 4, 0, 4); var hint = new Label { Text = "Introdu o locație și apasă Scanează", AutoSize = true, ForeColor = Muted, Dock = DockStyle.Fill, Padding = new Padding(2, 9, 0, 0) }; var results = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, BackColor = Card, ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, HeaderStyle = ColumnHeaderStyle.Nonclickable }; results.Columns.Add("Nume", 430); results.Columns.Add("Tip", 110); results.Columns.Add("Dimensiune", 150);
        host.Controls.Add(path, 0, 0); host.Controls.Add(scan, 1, 0); host.Controls.Add(hint, 0, 1); host.SetColumnSpan(hint, 2); host.Controls.Add(results, 0, 2); host.SetColumnSpan(results, 2); scan.Click += async (_, _) => await ScanDirectoryAsync(path.Text, results); body.Controls.Add(host);
    }

    private void ShowLogs()
    {
        var body = Page("Consolă PowerShell Live", "Istoricul operațiilor rămâne vizibil și poate fi copiat.", out _); var host = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(10, 10, 16), Padding = new Padding(8, 4, 8, 12) }; var clear = ActionButton("Șterge log", Color.FromArgb(175, 185, 205), () => { log.Clear(); return Task.CompletedTask; }); clear.Dock = DockStyle.Top; clear.Height = 36; host.Controls.Add(log); host.Controls.Add(clear); body.Controls.Add(host);
    }

    private async Task RefreshStatusAsync()
    {
        if (operationRunning) return;
        try { var result = await RunScriptAsync("Get-SystemStatus.ps1"); currentStatus = JsonSerializer.Deserialize<SystemStatus>(ExtractJson(result.Stdout), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); if (currentStatus == null) return; processValue.Text = currentStatus.ProcessCount.ToString(); ramValue.Text = $"{currentStatus.UsedMemoryGB:0.0} / {currentStatus.TotalMemoryGB:0.0} GB ({currentStatus.MemoryPercent:0.0}%)"; cpuValue.Text = $"{currentStatus.CpuLoad:0.0}%"; osLabel.Text = currentStatus.OsName; osDetail.Text = $"Build {currentStatus.OsBuild}   ·   {currentStatus.PowerPlan}"; }
        catch (Exception ex) { AppendLog("STATUS ERROR: " + ex.Message, true); }
    }

    private async Task RunSafeAsync(string script, params string[] args)
    {
        try { operationRunning = true; SetButtonsEnabled(false); AppendLog($">>> {script} {string.Join(" ", args)}", false); var result = await RunScriptAsync(script, args); AppendLog(result.Success ? $"<<< OK ({result.ExitCode})" : $"<<< EȘEC ({result.ExitCode})", !result.Success); }
        catch (Exception ex) { AppendLog("ERROR: " + ex.Message, true); }
        finally { operationRunning = false; SetButtonsEnabled(true); await RefreshStatusAsync(); }
    }

    private Task ConfirmAndRunAsync(string title, string text, string script, params string[] args) => MessageBox.Show(text + "\n\nContinui?", title, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes ? RunSafeAsync(script, args) : Task.CompletedTask;
    private Task RunManyAsync((string label, string script, string[] args)[] steps) => RunManyCoreAsync(steps);
    private Task ConfirmAndRunManyAsync(string title, string text, (string label, string script, string[] args)[] steps) => MessageBox.Show(text + "\n\nContinui?", title, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes ? RunManyCoreAsync(steps) : Task.CompletedTask;
    private async Task RunManyCoreAsync((string label, string script, string[] args)[] steps) { foreach (var step in steps) await RunSafeAsync(step.script, step.args); }

    private async Task<ScriptResult> RunScriptAsync(string script, params string[] args)
    {
        if (!AllowedScripts.Contains(script) || Path.GetFileName(script) != script) throw new InvalidOperationException("Script nepermis."); var fullPath = Path.GetFullPath(Path.Combine(ScriptsDirectory, script)); if (!File.Exists(fullPath)) throw new FileNotFoundException("Script lipsă", fullPath);
        var info = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = AppContext.BaseDirectory }; info.ArgumentList.Add("-NoProfile"); info.ArgumentList.Add("-ExecutionPolicy"); info.ArgumentList.Add("Bypass"); info.ArgumentList.Add("-File"); info.ArgumentList.Add(fullPath); foreach (var arg in args) { if (arg.Contains('\0') || arg.Contains('\r') || arg.Contains('\n')) throw new InvalidOperationException("Argument invalid."); info.ArgumentList.Add(arg); }
        using var process = new Process { StartInfo = info }; process.Start(); var stdoutTask = process.StandardOutput.ReadToEndAsync(); var stderrTask = process.StandardError.ReadToEndAsync(); await process.WaitForExitAsync(lifetime.Token); var stdout = await stdoutTask; var stderr = await stderrTask; if (!string.IsNullOrWhiteSpace(stdout)) AppendLog(stdout.Trim(), false); if (!string.IsNullOrWhiteSpace(stderr)) AppendLog(stderr.Trim(), true); return new ScriptResult(process.ExitCode, stdout, stderr);
    }

    private async Task SavePerformanceSnapshotAsync()
    {
        try { var result = await RunScriptAsync("Get-SystemStatus.ps1"); var json = ExtractJson(result.Stdout); JsonDocument.Parse(json); var directory = DataDirectory(); Directory.CreateDirectory(directory); var file = Path.Combine(directory, "native-benchmarks.json"); var list = File.Exists(file) ? JsonSerializer.Deserialize<List<PerfSnapshot>>(await File.ReadAllTextAsync(file)) ?? new() : new(); list.Insert(0, new PerfSnapshot(DateTimeOffset.Now, json)); await WriteAtomicAsync(file, JsonSerializer.Serialize(list.Take(20))); MessageBox.Show("Snapshot salvat.", "Performance Lab", MessageBoxButtons.OK, MessageBoxIcon.Information); } catch (Exception ex) { MessageBox.Show(ex.Message, "Snapshot", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private async Task CompareSnapshotsAsync()
    {
        try { var file = Path.Combine(DataDirectory(), "native-benchmarks.json"); if (!File.Exists(file)) { MessageBox.Show("Nu există încă snapshot-uri.", "Performance Lab"); return; } var list = JsonSerializer.Deserialize<List<PerfSnapshot>>(await File.ReadAllTextAsync(file)) ?? new(); if (list.Count < 2) { MessageBox.Show("Creează două snapshot-uri pentru comparație.", "Performance Lab"); return; } var before = JsonSerializer.Deserialize<SystemStatus>(list[1].StatusJson)!; var after = JsonSerializer.Deserialize<SystemStatus>(list[0].StatusJson)!; MessageBox.Show($"Procese: {before.ProcessCount} → {after.ProcessCount}\nRAM: {before.UsedMemoryGB:0.0} GB → {after.UsedMemoryGB:0.0} GB\nCPU: {before.CpuLoad:0.0}% → {after.CpuLoad:0.0}%", "Comparație ultimele snapshot-uri", MessageBoxButtons.OK, MessageBoxIcon.Information); } catch (Exception ex) { MessageBox.Show(ex.Message, "Performance Lab", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private async Task LoadProcessesAsync(ListView list)
    {
        list.Items.Clear(); var rows = await Task.Run(() => Process.GetProcesses().OrderByDescending(p => { try { return p.WorkingSet64; } catch { return 0; } }).Take(100).Select(p => { try { return new[] { p.ProcessName, p.Id.ToString(), (p.WorkingSet64 / 1024d / 1024d).ToString("0.0") }; } catch { return Array.Empty<string>(); } }).Where(r => r.Length == 3).ToList()); foreach (var row in rows) { var item = new ListViewItem(row[0]); item.SubItems.Add(row[1]); item.SubItems.Add(row[2]); list.Items.Add(item); }
    }

    private async Task ScanDirectoryAsync(string directory, ListView results)
    {
        try { results.Items.Clear(); var entries = await Task.Run(() => Directory.EnumerateFileSystemEntries(directory).Select(path => new FileInfo(path)).Select(file => (file.Name, Type: file.Attributes.HasFlag(FileAttributes.Directory) ? "Folder" : "Fișier", Size: file.Attributes.HasFlag(FileAttributes.Directory) ? "-" : (file.Length / 1024d / 1024d).ToString("0.0") + " MB")).OrderByDescending(x => x.Size).Take(200).ToList()); foreach (var entry in entries) { var item = new ListViewItem(entry.Name); item.SubItems.Add(entry.Type); item.SubItems.Add(entry.Size); results.Items.Add(item); } } catch (Exception ex) { MessageBox.Show(ex.Message, "Disk Analyzer", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    private void ApplyDarkTitleBar()
    {
        if (!OperatingSystem.IsWindows()) return;
        var darkMode = 1;
        var caption = ColorRef(Color.FromArgb(10, 10, 16));
        var captionText = ColorRef(Color.White);
        DwmSetWindowAttribute(Handle, 20, ref darkMode, sizeof(int));
        DwmSetWindowAttribute(Handle, 35, ref caption, sizeof(int));
        DwmSetWindowAttribute(Handle, 36, ref captionText, sizeof(int));
    }

    private static int ColorRef(Color color) => color.R | (color.G << 8) | (color.B << 16);
    private void SetButtonsEnabled(bool enabled) { foreach (Control control in Controls) SetRecursive(control, enabled); refreshButton.Enabled = enabled; }
    private static void SetRecursive(Control control, bool enabled) { foreach (Control child in control.Controls) { if (child is Button button) button.Enabled = enabled; SetRecursive(child, enabled); } }
    private void AppendLog(string text, bool error) { if (InvokeRequired) { BeginInvoke(() => AppendLog(text, error)); return; } log.SelectionStart = log.TextLength; log.SelectionColor = error ? Red : Color.FromArgb(180, 200, 220); log.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}"); log.ScrollToCaret(); }
    private static string ExtractJson(string output) => output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault(line => line.TrimStart().StartsWith("{"))?.Trim() ?? throw new InvalidOperationException("Status invalid.");
    private static string DataDirectory() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NexWin");
    private static async Task WriteAtomicAsync(string file, string content) { var temp = file + ".tmp"; await File.WriteAllTextAsync(temp, content); File.Move(temp, file, true); }
    private sealed record ScriptResult(int ExitCode, string Stdout, string Stderr) { public bool Success => ExitCode == 0; }
    private sealed record PerfSnapshot(DateTimeOffset At, string StatusJson);
    private sealed class SystemStatus { public int ProcessCount { get; set; } public double TotalMemoryGB { get; set; } public double UsedMemoryGB { get; set; } public double MemoryPercent { get; set; } public double CpuLoad { get; set; } public string OsName { get; set; } = "Windows"; public string OsBuild { get; set; } = ""; public string PowerPlan { get; set; } = ""; }
}

internal sealed class NavButton : Button
{
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string IconText { get; set; } = "";
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string LabelText { get; set; } = "";
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool IsActive { get; set; }

    public NavButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Text = "";
        Font = new Font("Segoe UI", 9);
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        if (!IsActive) BackColor = Color.FromArgb(25, 24, 40);
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (!IsActive) BackColor = Color.Transparent;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
        using var path = ShapePath(bounds, 7);
        var fill = IsActive ? Color.FromArgb(51, 17, 78) : BackColor;
        if (fill.A > 0)
        {
            using var brush = new SolidBrush(fill);
            e.Graphics.FillPath(brush, path);
        }
        if (IsActive)
        {
            using var pen = new Pen(Color.FromArgb(139, 40, 220), 1);
            e.Graphics.DrawPath(pen, path);
        }

        using var iconFont = new Font("Segoe UI Symbol", 10);
        using var labelFont = new Font("Segoe UI", 9);
        using var iconBrush = new SolidBrush(IsActive ? Color.FromArgb(224, 198, 255) : Color.FromArgb(155, 165, 185));
        using var labelBrush = new SolidBrush(ForeColor);
        var iconRect = new Rectangle(12, 0, 22, Height);
        var labelRect = new Rectangle(42, 0, Math.Max(1, Width - 50), Height);
        using var iconFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        using var labelFormat = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
        e.Graphics.DrawString(IconText, iconFont, iconBrush, iconRect, iconFormat);
        e.Graphics.DrawString(LabelText, labelFont, labelBrush, labelRect, labelFormat);
    }

    private static GraphicsPath ShapePath(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class RoundedCard : Panel
{
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Color AccentColor { get; set; } = Color.FromArgb(139, 92, 246);

    public RoundedCard()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        using var path = ShapePath(new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1)), 10);
        Region = new Region(path);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? Color.FromArgb(7, 7, 10));
        using var path = ShapePath(new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1)), 10);
        using var brush = new SolidBrush(BackColor);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.FillPath(brush, path);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = ShapePath(new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1)), 10);
        using var border = new Pen(Color.FromArgb(42, 42, 61), 1);
        e.Graphics.DrawPath(border, path);
        using var accent = new SolidBrush(AccentColor);
        e.Graphics.FillRectangle(accent, 18, 8, Math.Max(20, Width - 36), 3);
    }

    private static GraphicsPath ShapePath(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
