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
    private static string GetSavedActiveProfileId()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\NexWin");
            return key?.GetValue("ActiveProfileId")?.ToString() ?? "balanced";
        }
        catch { return "balanced"; }
    }

    private static void SetSavedActiveProfileId(string profileId)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\NexWin");
            key?.SetValue("ActiveProfileId", profileId);
        }
        catch { }
    }

    private void ShowProfiles()
    {
        bool isEn = NexLocale.CurrentLanguage == AppLanguage.En;
        PreparePage(
            isEn ? "Quick Profiles & Presets" : "Profiluri Rapide",
            isEn ? "Choose how your PC should run with a single click - simple, safe, and reversible."
                 : "Alege cu un singur click cum vrei să funcționeze calculatorul tău - simplu, clar și fără setări complicate."
        );

        string currentActive = GetSavedActiveProfileId();

        // Friendly Top Status Banner
        var topBanner = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(11, 20, 33)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(28, 48, 74)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(18, 13, 18, 13),
            Margin = new Thickness(0, 0, 0, 16)
        };
        var bannerDock = new DockPanel { LastChildFill = false };
        var bannerLeft = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        bannerLeft.Children.Add(CreateVectorIcon(NexIcon.Check, GreenBrush, 18));

        string activeDisplayName = currentActive switch
        {
            "high_performance" => "High Performance",
            "gaming" => "Gaming Boost",
            "more_battery" => "More Battery & Silent",
            _ => "Balanced (Recomandat)"
        };

        bannerLeft.Children.Add(new TextBlock
        {
            Text = isEn ? $"Active Profile: {activeDisplayName}" : $"Profil Activ Curent: {activeDisplayName}",
            FontSize = 13.5,
            FontWeight = FontWeights.Bold,
            Foreground = TextBrush,
            Margin = new Thickness(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        DockPanel.SetDock(bannerLeft, Dock.Left);
        bannerDock.Children.Add(bannerLeft);

        var bannerRight = new TextBlock
        {
            Text = isEn ? "Switch anytime with 1 click" : "Poți schimba profilul oricând cu 1 click",
            FontSize = 11.5,
            Foreground = MutedBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(bannerRight, Dock.Right);
        bannerDock.Children.Add(bannerRight);
        topBanner.Child = bannerDock;
        PageRoot.Children.Add(topBanner);

        // 2x2 Grid of Friendly Presets
        var cardsGrid = new Grid();
        cardsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        cardsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        cardsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        cardsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        cardsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });
        cardsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        Border BuildFriendlyPresetCard(
            string id,
            string title,
            string badge,
            string shortDesc,
            string[] bullets,
            NexIcon icon,
            Brush accent,
            Func<Task> onApplyAsync)
        {
            bool isActive = string.Equals(currentActive, id, StringComparison.OrdinalIgnoreCase);
            var accentColor = ((SolidColorBrush)accent).Color;

            var card = new Border
            {
                Background = isActive
                    ? new SolidColorBrush(Color.FromRgb(13, 26, 42))
                    : new SolidColorBrush(Color.FromRgb(11, 18, 28)),
                BorderBrush = isActive
                    ? accent
                    : new SolidColorBrush(Color.FromRgb(26, 40, 58)),
                BorderThickness = new Thickness(isActive ? 1.8 : 1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(20, 18, 20, 18)
            };

            var stack = new StackPanel();

            // Header row: Icon + Title + Badge
            var headerRow = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
            var leftTitle = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var iconBox = new Border
            {
                Width = 36,
                Height = 36,
                CornerRadius = new CornerRadius(9),
                Background = new SolidColorBrush(Color.FromArgb(38, accentColor.R, accentColor.G, accentColor.B)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(90, accentColor.R, accentColor.G, accentColor.B)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 12, 0),
                Child = CreateVectorIcon(icon, accent, 18)
            };
            leftTitle.Children.Add(iconBox);

            var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            titleStack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 15.5,
                FontWeight = FontWeights.Bold,
                Foreground = TextBrush
            });
            titleStack.Children.Add(new TextBlock
            {
                Text = badge,
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = accent,
                Margin = new Thickness(0, 1, 0, 0)
            });
            leftTitle.Children.Add(titleStack);
            DockPanel.SetDock(leftTitle, Dock.Left);
            headerRow.Children.Add(leftTitle);

            if (isActive)
            {
                var activePill = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(42, 16, 185, 129)),
                    BorderBrush = GreenBrush,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(9, 3, 9, 3),
                    VerticalAlignment = VerticalAlignment.Top,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Child = new TextBlock
                    {
                        Text = isEn ? "ACTIVE" : "ACTIV",
                        FontSize = 10,
                        FontWeight = FontWeights.Bold,
                        Foreground = GreenBrush
                    }
                };
                DockPanel.SetDock(activePill, Dock.Right);
                headerRow.Children.Add(activePill);
            }
            stack.Children.Add(headerRow);

            // Friendly 1-line description
            stack.Children.Add(new TextBlock
            {
                Text = shortDesc,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(198, 212, 228)),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 17,
                Margin = new Thickness(0, 0, 0, 12)
            });

            // 3 Simple Bullet Points
            var bulletBox = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(9, 15, 24)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(22, 35, 52)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 0, 0, 14)
            };
            var bulletStack = new StackPanel();
            foreach (var bText in bullets)
            {
                var bRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
                bRow.Children.Add(new TextBlock
                {
                    Text = "•",
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    Foreground = accent,
                    Margin = new Thickness(0, -1, 8, 0)
                });
                bRow.Children.Add(new TextBlock
                {
                    Text = bText,
                    FontSize = 11.5,
                    Foreground = MutedBrush
                });
                bulletStack.Children.Add(bRow);
            }
            bulletBox.Child = bulletStack;
            stack.Children.Add(bulletBox);

            // Single Friendly Activate Button
            var applyBtn = new Button
            {
                Content = isActive
                    ? (isEn ? $"Re-apply {title}" : $"Profil Activ ({title})")
                    : (isEn ? $"Activate {title}" : $"Activează {title}"),
                Style = (Style)FindResource(isActive ? "SecondaryButtonStyle" : "PrimaryGradientButtonStyle"),
                Height = 36,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Cursor = Cursors.Hand
            };
            applyBtn.Click += async (_, _) =>
            {
                SetSavedActiveProfileId(id);
                await onApplyAsync();
                ShowProfiles();
            };
            stack.Children.Add(applyBtn);

            card.Child = stack;
            return card;
        }

        // 1. Gaming Boost (#1 Best for Gaming - Includes High Performance + Game Priority + Zero Lag)
        var cardGaming = BuildFriendlyPresetCard(
            "gaming",
            "Gaming Boost",
            isEn ? "★ #1 FOR GAMING (Includes High Performance + Max FPS)" : "★ CEL MAI BUN PENTRU JOCURI (Include High Performance + Max FPS)",
            isEn ? "The ultimate profile for gaming! Combines 100% CPU/GPU High Performance power with maximum game priority and lowest ping/input lag."
                 : "Alege acest profil când te joci! Include toată puterea din High Performance (CPU/GPU 100%) + prioritate maximă pentru jocuri și latență zero.",
            isEn
                ? new[] { "Includes 100% High Performance CPU & GPU power", "Maximum FPS priority & stops background lag", "Zero input lag (Mouse/Keyboard) & lower network ping" }
                : new[] { "Include puterea 100% CPU & GPU din High Performance", "Prioritate maximă FPS în jocuri + oprire procese inutile", "Latență minimă la mouse/tastatură și ping redus în rețea" },
            NexIcon.Gamepad,
            AmberBrush,
            () => ExecuteNativeSuiteAsync(
                "Gaming Boost",
                isEn ? "Activating Gaming Boost (Ultra Power + Max FPS)..." : "Se activează Gaming Boost (Putere Maximă + Max FPS)...",
                isEn ? "Gaming Boost is active! Maximum FPS & power unlocked." : "Gaming Boost este activ! Putere maximă și FPS Boost deblocate.",
                async () =>
                {
                    await NativeTuning.SetPowerSchemeAsync(true);
                    return await NativeTuning.ApplyGamingTweaksNativeAsync(false);
                })
        );
        Grid.SetRow(cardGaming, 0);
        Grid.SetColumn(cardGaming, 0);
        cardsGrid.Children.Add(cardGaming);

        // 2. High Performance (Workstation, Video Editing & Multitasking)
        var cardHighPerf = BuildFriendlyPresetCard(
            "high_performance",
            "High Performance",
            isEn ? "For Heavy Work, Video Editing & Multitasking" : "Pentru Muncă Grea, Editare & Multitasking (Fără Jocuri)",
            isEn ? "Best when working in demanding apps, rendering, or browsing with many tabs—keeps CPU at 100% without closing background services."
                 : "Alege acest profil când lucrezi în programe grele, editare sau multitasking intens. Ține procesorul la 100%, fără să oprească serviciile Windows.",
            isEn
                ? new[] { "CPU & GPU running at 100% for fast app loading", "Keeps all Windows services & background apps open", "Ideal for productivity, streaming, and rendering" }
                : new[] { "Procesor la 100% pentru deschidere instantanee a programelor", "Păstrează active toate aplicațiile și serviciile din fundal", "Ideal pentru muncă, editare foto/video și zeci de tab-uri" },
            NexIcon.Rocket,
            CyanBrush,
            () => ExecuteNativeSuiteAsync(
                "High Performance",
                isEn ? "Activating High Performance mode..." : "Se activează modul High Performance...",
                isEn ? "High Performance mode is now active!" : "Modul High Performance a fost activat!",
                async () =>
                {
                    await NativeTuning.SetPowerSchemeAsync(true);
                    return await NativeTuning.ApplyMaintenanceNativeAsync();
                })
        );
        Grid.SetRow(cardHighPerf, 0);
        Grid.SetColumn(cardHighPerf, 2);
        cardsGrid.Children.Add(cardHighPerf);

        // 3. Balanced (Daily Use)
        var cardBalanced = BuildFriendlyPresetCard(
            "balanced",
            "Balanced",
            isEn ? "Recommended for Normal Daily Use" : "Recomandat pentru Utilizare Zilnică Normală",
            isEn ? "The ideal everyday balance between snappy speed, low temperatures, and quiet fans."
                 : "Echilibrul ideal de zi cu zi între viteză foarte bună, temperaturi scăzute și liniște.",
            isEn
                ? new[] { "Automatically boosts speed only when needed", "Keeps CPU/GPU temperatures low", "Cleans temporary cache & optimizes RAM" }
                : new[] { "Crește viteza automat doar când este nevoie", "Păstrează temperaturile și zgomotul scăzute", "Curăță fișierele temporare și memoria RAM" },
            NexIcon.Gauge,
            GreenBrush,
            () => ExecuteNativeSuiteAsync(
                "Balanced",
                isEn ? "Activating Balanced mode..." : "Se activează modul Balanced...",
                isEn ? "Balanced mode is now active!" : "Modul Balanced a fost activat!",
                () => NativeTuning.ApplyMaintenanceNativeAsync())
        );
        Grid.SetRow(cardBalanced, 2);
        Grid.SetColumn(cardBalanced, 0);
        cardsGrid.Children.Add(cardBalanced);

        // 4. More Battery & Silent
        var cardBattery = BuildFriendlyPresetCard(
            "more_battery",
            "More Battery",
            isEn ? "Extended Battery & Quiet Mode" : "Economisire Baterie & Silențios",
            isEn ? "Extends laptop battery life and keeps fans completely quiet during light work or browsing."
                 : "Prelungește durata bateriei și menține ventilatoarele complet silențioase.",
            isEn
                ? new[] { "Minimum power consumption on battery", "Pauses non-essential background services", "Silent fans and cool operation" }
                : new[] { "Consum minim de energie și baterie", "Oprește serviciile care consumă în fundal", "Ventilatoare silențioase și temperaturi reci" },
            NexIcon.Power,
            PurpleBrush,
            () => ExecuteNativeSuiteAsync(
                "More Battery",
                isEn ? "Activating More Battery mode..." : "Se activează modul More Battery...",
                isEn ? "More Battery mode is now active!" : "Modul More Battery a fost activat!",
                async () =>
                {
                    var logs = await NativeTuning.ApplyDebloatServicesNativeAsync(false);
                    await NativeTuning.SetPowerSchemeAsync(false);
                    return logs;
                })
        );
        Grid.SetRow(cardBattery, 2);
        Grid.SetColumn(cardBattery, 2);
        cardsGrid.Children.Add(cardBattery);

        PageRoot.Children.Add(cardsGrid);
    }

    // ================= HARDWARE & THERMALS (PIXEL-PERFECT media_1789758060712.jpg) =================
    private UIElement CreateMetricDonut(double pct, string label, double size = 96, Brush? strokeBrush = null)
    {
        var grid = new Grid { Width = size, Height = size, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var bgEllipse = new Ellipse
        {
            Width = size,
            Height = size,
            Stroke = new SolidColorBrush(Color.FromRgb(19, 28, 46)),
            StrokeThickness = 6.0,
            Fill = Brushes.Transparent
        };
        grid.Children.Add(bgEllipse);

        pct = Math.Clamp(pct, 0.5, 99.5);
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

        var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        textStack.Children.Add(new TextBlock
        {
            Text = $"{pct:0}%",
            FontSize = 17,
            FontWeight = FontWeights.Bold,
            Foreground = TextBrush,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        textStack.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 9.5,
            Foreground = MutedBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 1, 0, 0)
        });
        grid.Children.Add(textStack);

        return grid;
    }

    private UIElement CreateSparkline(Brush waveBrush, double height = 32)
    {
        var grid = new Grid { Height = height, Margin = new Thickness(0, 8, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var labels = new Grid { VerticalAlignment = VerticalAlignment.Stretch };
        labels.RowDefinitions.Add(new RowDefinition());
        labels.RowDefinitions.Add(new RowDefinition());
        labels.RowDefinitions.Add(new RowDefinition());
        var t100 = new TextBlock { Text = "100%", FontSize = 7.5, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)), VerticalAlignment = VerticalAlignment.Top };
        Grid.SetRow(t100, 0); labels.Children.Add(t100);
        var t50 = new TextBlock { Text = "50%", FontSize = 7.5, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetRow(t50, 1); labels.Children.Add(t50);
        var t0 = new TextBlock { Text = "0%", FontSize = 7.5, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)), VerticalAlignment = VerticalAlignment.Bottom };
        Grid.SetRow(t0, 2); labels.Children.Add(t0);
        Grid.SetColumn(labels, 0);
        grid.Children.Add(labels);

        var canvas = new Canvas { ClipToBounds = true };
        var pathGeom = new PathGeometry();
        var pathFig = new PathFigure { StartPoint = new Point(0, height * 0.65), IsClosed = false };

        // Sine wave pattern
        for (double x = 10; x <= 360; x += 15)
        {
            double y = (height * 0.5) + (Math.Sin(x * 0.15) * (height * 0.35));
            pathFig.Segments.Add(new LineSegment(new Point(x, y), true));
        }
        pathGeom.Figures.Add(pathFig);

        var wavePath = new System.Windows.Shapes.Path
        {
            Data = pathGeom,
            Stroke = waveBrush,
            StrokeThickness = 1.5,
            Opacity = 0.85
        };
        canvas.Children.Add(wavePath);

        Grid.SetColumn(canvas, 1);
        grid.Children.Add(canvas);

        return grid;
    }

    private (UIElement element, Action<double> update) CreateDynamicMetricDonut(double initialPct, string label, double size = 94, Brush? strokeBrush = null)
    {
        var grid = new Grid { Width = size, Height = size, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var bgEllipse = new Ellipse
        {
            Width = size,
            Height = size,
            Stroke = new SolidColorBrush(Color.FromRgb(19, 28, 46)),
            StrokeThickness = 6.0,
            Fill = Brushes.Transparent
        };
        grid.Children.Add(bgEllipse);

        var geom = new PathGeometry();
        var fig = new PathFigure { IsClosed = false };
        var arcSeg = new ArcSegment();
        fig.Segments.Add(arcSeg);
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

        var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        var valText = new TextBlock
        {
            Text = $"{initialPct:0}%",
            FontSize = 17,
            FontWeight = FontWeights.Bold,
            Foreground = TextBrush,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        textStack.Children.Add(valText);
        textStack.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 9.5,
            Foreground = MutedBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 1, 0, 0)
        });
        grid.Children.Add(textStack);

        void UpdateDonut(double pct)
        {
            pct = Math.Clamp(pct, 0.5, 99.5);
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

            fig.StartPoint = new Point(startX, startY);
            arcSeg.Point = new Point(endX, endY);
            arcSeg.Size = new Size(r, r);
            arcSeg.IsLargeArc = isLargeArc;
            arcSeg.SweepDirection = SweepDirection.Clockwise;
            arcSeg.IsStroked = true;

            valText.Text = $"{pct:0}%";
        }

        UpdateDonut(initialPct);
        return (grid, UpdateDonut);
    }

}