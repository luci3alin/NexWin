using System;
using System.Collections.Generic;
using System.Linq;
using NexIcon = NexWin.Native.MainWindow.NexIcon;

namespace NexWin.Native;

public class GlobalSearchResult
{
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string TargetPage { get; set; } = string.Empty;
    public string ActionPayload { get; set; } = string.Empty;
    public string Keywords { get; set; } = string.Empty;
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
            // 1. Core Pages & Gaming
            new()
            {
                Title = NexLocale.T("search_item_1_title"),
                Subtitle = NexLocale.T("search_item_1_sub"),
                Category = NexLocale.T("search_cat_gaming"),
                TargetPage = "Gaming",
                Keywords = "gaming boost fps latency hpet gpu priority power plan optimizare jocuri performanta",
                Icon = NexIcon.Gamepad
            },
            new()
            {
                Title = NexLocale.T("search_item_17_title"),
                Subtitle = NexLocale.T("search_item_17_sub"),
                Category = NexLocale.T("search_cat_gaming"),
                TargetPage = "NvidiaInspector",
                Keywords = "nvidia profile inspector gpu geforce rtx gtx rebar reflex latency low latency frame rate limiter shader cache placa video dedicated",
                Icon = NexIcon.Gpu
            },
            new()
            {
                Title = NexLocale.T("search_item_10_title"),
                Subtitle = NexLocale.T("search_item_10_sub"),
                Category = NexLocale.T("search_cat_gaming"),
                TargetPage = "Gaming",
                ActionPayload = "hpet",
                Keywords = "hpet timer ceas precizie fps stabilitate micro-stuttering jitter cadru",
                Icon = NexIcon.Cpu
            },
            new()
            {
                Title = NexLocale.T("search_item_11_title"),
                Subtitle = NexLocale.T("search_item_11_sub"),
                Category = NexLocale.T("search_cat_gaming"),
                TargetPage = "Gaming",
                ActionPayload = "gpu_priority",
                Keywords = "gpu priority prioritate placa video placa grafica dedicated jocuri",
                Icon = NexIcon.Gpu
            },
            new()
            {
                Title = NexLocale.T("search_item_12_title"),
                Subtitle = NexLocale.T("search_item_12_sub"),
                Category = NexLocale.T("search_cat_gaming"),
                TargetPage = "Gaming",
                ActionPayload = "power_plan",
                Keywords = "power plan plan alimentare energie ultimate performance consum cpu curent",
                Icon = NexIcon.Power
            },
            new()
            {
                Title = NexLocale.T("search_item_13_title"),
                Subtitle = NexLocale.T("search_item_13_sub"),
                Category = NexLocale.T("search_cat_gaming"),
                TargetPage = "Gaming",
                ActionPayload = "game_bar",
                Keywords = "game bar dvr captura fundal xbox inregistrare input lag fps",
                Icon = NexIcon.Gamepad
            },

            // 2. Hardware & Drivers
            new()
            {
                Title = NexLocale.T("search_item_18_title"),
                Subtitle = NexLocale.T("search_item_18_sub"),
                Category = NexLocale.T("search_cat_drivers"),
                TargetPage = "Drivers",
                Keywords = "driver drivere manager hardware 126 componente update actualizare gpu chipset realtek intel amd audio sunet retea wifi bluetooth pnp inf",
                Icon = NexIcon.Refresh
            },
            new()
            {
                Title = NexLocale.T("search_item_6_title"),
                Subtitle = NexLocale.T("search_item_6_sub"),
                Category = NexLocale.T("search_cat_sys"),
                TargetPage = "Performance",
                Keywords = "hardware monitor senzori temperaturi cpu gpu ram vram disk utilizare sarcina frecvente",
                Icon = NexIcon.Cpu
            },
            new()
            {
                Title = NexLocale.T("search_item_25_title"),
                Subtitle = NexLocale.T("search_item_25_sub"),
                Category = NexLocale.T("search_cat_opt"),
                TargetPage = "Ram",
                Keywords = "ram memorie curatare memory cleaner eliberare working set cache standby procese ram usage",
                Icon = NexIcon.Memory
            },
            new()
            {
                Title = NexLocale.T("search_item_26_title"),
                Subtitle = NexLocale.T("search_item_26_sub"),
                Category = NexLocale.T("search_cat_sys"),
                TargetPage = "DiskTree",
                Keywords = "disk spatiu spatiu stocare treesize arbore directoare fisiere mari curatare ssd hdd gb partition",
                Icon = NexIcon.Folder
            },
            new()
            {
                Title = NexLocale.T("search_item_9_title"),
                Subtitle = NexLocale.T("search_item_9_sub"),
                Category = NexLocale.T("search_cat_opt"),
                TargetPage = "DiskTree",
                ActionPayload = "clean_temp",
                Keywords = "curatare cache fisiere temporare temp junk tempfiles eliberare disc windows temp",
                Icon = NexIcon.Clean
            },

            // 3. Wallpapers & Customizer
            new()
            {
                Title = NexLocale.T("search_item_2_title"),
                Subtitle = NexLocale.T("search_item_2_sub"),
                Category = NexLocale.T("search_cat_cust"),
                TargetPage = "CustomizerWallpaper",
                ActionPayload = "wallpapers",
                Keywords = "live wallpaper wallpaper animat fundal video pixabay mp4 desktop miscare live wallpapers",
                Icon = NexIcon.Layers
            },
            new()
            {
                Title = NexLocale.T("search_item_19_title"),
                Subtitle = NexLocale.T("search_item_19_sub"),
                Category = NexLocale.T("search_cat_cust"),
                TargetPage = "CustomizerStatic",
                Keywords = "wallhaven static 4k uhd qhd fhd poze fundal imagini wallpaper rezolutie gaming masini natura spatiu high resolution",
                Icon = NexIcon.Photo
            },
            new()
            {
                Title = NexLocale.T("search_item_20_title"),
                Subtitle = NexLocale.T("search_item_20_sub"),
                Category = NexLocale.T("search_cat_cust"),
                TargetPage = "CustomizerLibrary",
                Keywords = "biblioteca mea library colectie slideshow automat video static rotire interval schimbare fundal rotatie",
                Icon = NexIcon.Folder
            },
            new()
            {
                Title = NexLocale.T("search_item_3_title"),
                Subtitle = NexLocale.T("search_item_3_sub"),
                Category = NexLocale.T("search_cat_cust"),
                TargetPage = "CustomizerStart",
                Keywords = "personalizare windows start meniu taskbar bara activitati transparenta aspect centrare teme",
                Icon = NexIcon.Paint
            },
            new()
            {
                Title = NexLocale.T("search_item_16_title"),
                Subtitle = NexLocale.T("search_item_16_sub"),
                Category = NexLocale.T("search_cat_cust"),
                TargetPage = "CustomizerWallpaper",
                ActionPayload = "autopause",
                Keywords = "pauza automata auto pause live wallpaper jocuri cpu gpu 0% economisire",
                Icon = NexIcon.Layers
            },

            // 4. Software & Winget Updates
            new()
            {
                Title = NexLocale.T("search_item_21_title"),
                Subtitle = NexLocale.T("search_item_21_sub"),
                Category = NexLocale.T("search_cat_apps"),
                TargetPage = "AppsUpdates",
                Keywords = "actualizari aplicatii software winget update upgrades programe instalate notificari up to date",
                Icon = NexIcon.Bell
            },
            new()
            {
                Title = NexLocale.T("search_item_22_title"),
                Subtitle = NexLocale.T("search_item_22_sub"),
                Category = NexLocale.T("search_cat_apps"),
                TargetPage = "Apps",
                Keywords = "catalog aplicatii esentiale pachete instalare programe discord chrome spotify steam 7zip catalog winget",
                Icon = NexIcon.Folder
            },
            new()
            {
                Title = NexLocale.T("search_item_23_title"),
                Subtitle = NexLocale.T("search_item_23_sub"),
                Category = NexLocale.T("search_cat_apps"),
                TargetPage = "AppsInstalled",
                Keywords = "aplicatii instalate dezinstalare programe remove uninstaller curatare eliminare soft",
                Icon = NexIcon.Trash
            },

            // 5. System Tuning, Debloat & AI
            new()
            {
                Title = NexLocale.T("search_item_4_title"),
                Subtitle = NexLocale.T("search_item_4_sub"),
                Category = NexLocale.T("search_cat_opt"),
                TargetPage = "Performance",
                Keywords = "optimizari sistem tuning reglaje registru performanta latenta viteza windows tweaks",
                Icon = NexIcon.Check
            },
            new()
            {
                Title = NexLocale.T("search_item_5_title"),
                Subtitle = NexLocale.T("search_item_5_sub"),
                Category = NexLocale.T("search_cat_sys"),
                TargetPage = "Debloat",
                Keywords = "debloat debloater eliminare aplicatii nedorite bloatware windows curatare bloat",
                Icon = NexIcon.Clean
            },
            new()
            {
                Title = NexLocale.T("search_item_24_title"),
                Subtitle = NexLocale.T("search_item_24_sub"),
                Category = NexLocale.T("search_cat_sys"),
                TargetPage = "AI",
                Keywords = "copilot ai recall telemetrie dezactivare inteligenta artificiala privacy securitate",
                Icon = NexIcon.Shield
            },
            new()
            {
                Title = NexLocale.T("search_item_8_title"),
                Subtitle = NexLocale.T("search_item_8_sub"),
                Category = NexLocale.T("search_cat_opt"),
                TargetPage = "Performance",
                ActionPayload = "telemetry",
                Keywords = "telemetrie dezactivare microsoft tracking confidentialitate diagtrack privacy",
                Icon = NexIcon.Shield
            },
            new()
            {
                Title = NexLocale.T("search_item_15_title"),
                Subtitle = NexLocale.T("search_item_15_sub"),
                Category = NexLocale.T("search_cat_sys"),
                TargetPage = "Debloat",
                ActionPayload = "onedrive",
                Keywords = "onedrive cortana dezinstalare cloud sincronizare stergere",
                Icon = NexIcon.Trash
            },
            new()
            {
                Title = NexLocale.T("search_item_28_title"),
                Subtitle = NexLocale.T("search_item_28_sub"),
                Category = NexLocale.T("search_cat_sys"),
                TargetPage = "Services",
                Keywords = "servicii windows services background fundal diagtrack sysmain spooler oprire servicii",
                Icon = NexIcon.Sliders
            },
            new()
            {
                Title = NexLocale.T("search_item_29_title"),
                Subtitle = NexLocale.T("search_item_29_sub"),
                Category = NexLocale.T("search_cat_sys"),
                TargetPage = "Startup",
                Keywords = "startup manager programe pornire boot aplicatii pornire rapida intarziere autostart",
                Icon = NexIcon.Rocket
            },
            new()
            {
                Title = NexLocale.T("search_item_30_title"),
                Subtitle = NexLocale.T("search_item_30_sub"),
                Category = NexLocale.T("search_cat_opt"),
                TargetPage = "Profiles",
                Keywords = "profiluri performanta profil sistem gaming extrem echilibrat baterie economisire",
                Icon = NexIcon.Check
            },

            // 6. Network & Internet
            new()
            {
                Title = NexLocale.T("search_item_14_title"),
                Subtitle = NexLocale.T("search_item_14_sub"),
                Category = NexLocale.T("search_cat_network"),
                TargetPage = "Network",
                ActionPayload = "dns",
                Keywords = "dns optimizare retea internet cloudflare 1.1.1.1 google latenta ping",
                Icon = NexIcon.Network
            },
            new()
            {
                Title = NexLocale.T("search_item_27_title"),
                Subtitle = NexLocale.T("search_item_27_sub"),
                Category = NexLocale.T("search_cat_network"),
                TargetPage = "Network",
                Keywords = "dns benchmark test viteza ping retea cloudflare google adguard quad9 cel mai rapid test latenta",
                Icon = NexIcon.Network
            },

            // 7. Security, Activation, OTA & Settings
            new()
            {
                Title = NexLocale.T("search_item_7_title"),
                Subtitle = NexLocale.T("search_item_7_sub"),
                Category = NexLocale.T("search_cat_sys"),
                TargetPage = "Snapshot",
                ActionPayload = "restore_points",
                Keywords = "backup restaurare restore point punct restaurare snapshot siguranta rollback reversibil",
                Icon = NexIcon.Info
            },
            new()
            {
                Title = NexLocale.T("search_item_31_title"),
                Subtitle = NexLocale.T("search_item_31_sub"),
                Category = NexLocale.T("search_cat_sys"),
                TargetPage = "Activator",
                Keywords = "activator windows office licenta activare hwid kms permanent genuine cheie produs",
                Icon = NexIcon.Shield
            },
            new()
            {
                Title = NexLocale.T("search_item_32_title"),
                Subtitle = NexLocale.T("search_item_32_sub"),
                Category = NexLocale.T("search_cat_sys"),
                TargetPage = "Settings",
                Keywords = "actualizare nexwin ota updater update verificare versiune noua release changelog actualizeaza",
                Icon = NexIcon.Download
            },
            new()
            {
                Title = NexLocale.T("search_item_33_title"),
                Subtitle = NexLocale.T("search_item_33_sub"),
                Category = NexLocale.T("search_cat_sys"),
                TargetPage = "Settings",
                Keywords = "sustine donatie kofi ko-fi comunitate obiectiv paypal suport proiect donate",
                Icon = NexIcon.Flame
            },
            new()
            {
                Title = NexLocale.T("search_item_34_title"),
                Subtitle = NexLocale.T("search_item_34_sub"),
                Category = NexLocale.T("search_cat_sys"),
                TargetPage = "Settings",
                Keywords = "setari settings limba language română english tray startup autostart minimizare sistem",
                Icon = NexIcon.Sliders
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
                string k = item.Keywords.ToLowerInvariant();

                if (t.StartsWith(clean)) score += 100;
                else if (t.Contains(clean)) score += 50;

                if (k.Contains(clean)) score += 40;
                if (s.Contains(clean)) score += 20;
                if (c.Contains(clean)) score += 15;

                foreach (var w in words)
                {
                    if (t.Contains(w)) score += 14;
                    if (k.Contains(w)) score += 10;
                    if (s.Contains(w)) score += 6;
                    if (c.Contains(w)) score += 4;
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
