using System;
using System.Collections.Generic;
using System.Linq;
using NexIcon = NexWin.Native.MainWindow.NexIcon;

namespace NexWin.Native;

public class GlobalSearchResult
{
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // "Navigare", "Optimizare", "Gaming", "Personalizare", "Sistem"
    public string TargetPage { get; set; } = string.Empty; // "Tuning", "Gaming", "Customizer", "Debloat", "Hardware", "Settings"
    public string ActionPayload { get; set; } = string.Empty;
    public NexIcon Icon { get; set; } = NexIcon.Search;
}

public static class GlobalSearchService
{
    private static readonly List<GlobalSearchResult> _index = new();

    static GlobalSearchService()
    {
        NexLocale.LanguageChanged += BuildStaticIndex;
        BuildStaticIndex();
    }

    private static void BuildStaticIndex()
    {
        var list = new List<GlobalSearchResult>
        {
            // 1. Core Pages
            new()
            {
                Title = NexLocale.T("search_item_1_title"),
                Subtitle = NexLocale.T("search_item_1_sub"),
                Category = NexLocale.T("search_cat_nav"),
                TargetPage = "Gaming",
                Icon = NexIcon.Cpu
            },
            new()
            {
                Title = NexLocale.T("search_item_2_title"),
                Subtitle = NexLocale.T("search_item_2_sub"),
                Category = NexLocale.T("search_cat_cust"),
                TargetPage = "Customizer",
                ActionPayload = "wallpapers",
                Icon = NexIcon.Layers
            },
            new()
            {
                Title = NexLocale.T("search_item_3_title"),
                Subtitle = NexLocale.T("search_item_3_sub"),
                Category = NexLocale.T("search_cat_cust"),
                TargetPage = "Customizer",
                Icon = NexIcon.Layers
            },
            new()
            {
                Title = NexLocale.T("search_item_4_title"),
                Subtitle = NexLocale.T("search_item_4_sub"),
                Category = NexLocale.T("search_cat_opt"),
                TargetPage = "Tuning",
                Icon = NexIcon.Check
            },
            new()
            {
                Title = NexLocale.T("search_item_5_title"),
                Subtitle = NexLocale.T("search_item_5_sub"),
                Category = NexLocale.T("search_cat_sys"),
                TargetPage = "Debloat",
                Icon = NexIcon.Refresh
            },
            new()
            {
                Title = NexLocale.T("search_item_6_title"),
                Subtitle = NexLocale.T("search_item_6_sub"),
                Category = NexLocale.T("search_cat_sys"),
                TargetPage = "Hardware",
                Icon = NexIcon.Cpu
            },
            new()
            {
                Title = NexLocale.T("search_item_7_title"),
                Subtitle = NexLocale.T("search_item_7_sub"),
                Category = NexLocale.T("search_cat_sys"),
                TargetPage = "Tuning",
                ActionPayload = "restore_points",
                Icon = NexIcon.Info
            },

            // 2. Specific Tweaks & Optimizations
            new()
            {
                Title = NexLocale.T("search_item_8_title"),
                Subtitle = NexLocale.T("search_item_8_sub"),
                Category = NexLocale.T("search_cat_opt"),
                TargetPage = "Tuning",
                ActionPayload = "telemetry",
                Icon = NexIcon.Shield
            },
            new()
            {
                Title = NexLocale.T("search_item_9_title"),
                Subtitle = NexLocale.T("search_item_9_sub"),
                Category = NexLocale.T("search_cat_opt"),
                TargetPage = "Tuning",
                ActionPayload = "clean_temp",
                Icon = NexIcon.Refresh
            },
            new()
            {
                Title = NexLocale.T("search_item_10_title"),
                Subtitle = NexLocale.T("search_item_10_sub"),
                Category = NexLocale.T("search_cat_gaming"),
                TargetPage = "Gaming",
                ActionPayload = "hpet",
                Icon = NexIcon.Cpu
            },
            new()
            {
                Title = NexLocale.T("search_item_11_title"),
                Subtitle = NexLocale.T("search_item_11_sub"),
                Category = NexLocale.T("search_cat_gaming"),
                TargetPage = "Gaming",
                ActionPayload = "gpu_priority",
                Icon = NexIcon.Cpu
            },
            new()
            {
                Title = NexLocale.T("search_item_12_title"),
                Subtitle = NexLocale.T("search_item_12_sub"),
                Category = NexLocale.T("search_cat_gaming"),
                TargetPage = "Gaming",
                ActionPayload = "power_plan",
                Icon = NexIcon.Cpu
            },
            new()
            {
                Title = NexLocale.T("search_item_13_title"),
                Subtitle = NexLocale.T("search_item_13_sub"),
                Category = NexLocale.T("search_cat_gaming"),
                TargetPage = "Gaming",
                ActionPayload = "game_bar",
                Icon = NexIcon.Cpu
            },
            new()
            {
                Title = NexLocale.T("search_item_14_title"),
                Subtitle = NexLocale.T("search_item_14_sub"),
                Category = NexLocale.T("search_cat_opt"),
                TargetPage = "Tuning",
                ActionPayload = "dns",
                Icon = NexIcon.Network
            },
            new()
            {
                Title = NexLocale.T("search_item_15_title"),
                Subtitle = NexLocale.T("search_item_15_sub"),
                Category = NexLocale.T("search_cat_sys"),
                TargetPage = "Debloat",
                ActionPayload = "onedrive",
                Icon = NexIcon.Refresh
            },
            new()
            {
                Title = NexLocale.T("search_item_16_title"),
                Subtitle = NexLocale.T("search_item_16_sub"),
                Category = NexLocale.T("search_cat_cust"),
                TargetPage = "Customizer",
                ActionPayload = "autopause",
                Icon = NexIcon.Layers
            }
        };

        lock (_index)
        {
            _index.Clear();
            _index.AddRange(list);
        }
    }

    public static List<GlobalSearchResult> Search(string query, int maxResults = 10)
    {
        List<GlobalSearchResult> snapshot;
        lock (_index)
        {
            snapshot = _index.ToList();
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return snapshot.Take(maxResults).ToList();
        }

        string clean = query.Trim().ToLowerInvariant();
        var words = clean.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return snapshot
            .Select(item =>
            {
                int score = 0;
                string t = item.Title.ToLowerInvariant();
                string s = item.Subtitle.ToLowerInvariant();
                string c = item.Category.ToLowerInvariant();

                if (t.StartsWith(clean)) score += 100;
                else if (t.Contains(clean)) score += 50;

                if (s.Contains(clean)) score += 20;
                if (c.Contains(clean)) score += 15;

                foreach (var w in words)
                {
                    if (t.Contains(w)) score += 10;
                    if (s.Contains(w)) score += 5;
                }

                return new { Item = item, Score = score };
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Select(x => x.Item)
            .Take(maxResults)
            .ToList();
    }
}
