using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace NexWin.Native;

public class WallpaperPhotoItem
{
    public string Id { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string ShortUrl { get; set; } = string.Empty;
    public int Views { get; set; }
    public int Favorites { get; set; }
    public string Purity { get; set; } = "sfw";
    public string Category { get; set; } = "general";
    public int Width { get; set; }
    public int Height { get; set; }
    public string Resolution { get; set; } = string.Empty;
    public string Ratio { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string FileType { get; set; } = "image/jpeg";
    public string FullPath { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public string LocalThumbnailPath { get; set; } = string.Empty;
    public bool IsDownloaded { get; set; }
    public string LocalFilePath { get; set; } = string.Empty;
}

public static class WallhavenService
{
    private const string API_KEY = "r3jSE6WvbtMgum77kxkZrzkkIJH9QkQW";
    private const string BASE_URL = "https://wallhaven.cc/api/v1/search";

    private const int SPI_SETDESKWALLPAPER = 20;
    private const int SPIF_UPDATEINIFILE = 0x01;
    private const int SPIF_SENDCHANGE = 0x02;

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

    private static readonly HttpClient _httpClient = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    static WallhavenService()
    {
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "NexWin/1.0.15 (Windows NT 10.0; Win64; x64)");
        _httpClient.DefaultRequestHeaders.Add("X-API-Key", API_KEY);
    }

    public static string GetWallpapersDirectory()
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NexWin",
            "StaticWallpapers"
        );
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        return dir;
    }

    public static string GetThumbCacheDirectory()
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NexWin",
            "StaticThumbCache"
        );
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        return dir;
    }

    public static async Task<List<WallpaperPhotoItem>> SearchWallpapersAsync(
        string query = "",
        string categories = "111", // general, anime, people
        string purity = "100",     // 100=sfw, 110=sfw+sketchy, 111=sfw+sketchy+nsfw
        string sorting = "toplist", // toplist, hot, date_added, views, favorites, random
        string topRange = "1M",    // 1d, 3d, 1w, 1M, 3M, 6M, 1y
        string atleast = "1920x1080",
        string ratios = "",        // 16x9, 21x9, 16x10
        int page = 1)
    {
        var results = new List<WallpaperPhotoItem>();
        string thumbDir = GetThumbCacheDirectory();
        string wallpapersDir = GetWallpapersDirectory();

        try
        {
            var queryParams = new List<string>
            {
                $"apikey={API_KEY}",
                $"categories={Uri.EscapeDataString(categories)}",
                $"purity={Uri.EscapeDataString(purity)}",
                $"sorting={Uri.EscapeDataString(sorting)}",
                $"page={page}"
            };

            if (!string.IsNullOrWhiteSpace(query))
            {
                queryParams.Add($"q={Uri.EscapeDataString(query.Trim())}");
            }

            if (sorting.Equals("toplist", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(topRange))
            {
                queryParams.Add($"topRange={Uri.EscapeDataString(topRange)}");
            }

            if (!string.IsNullOrWhiteSpace(atleast) && !atleast.Equals("any", StringComparison.OrdinalIgnoreCase))
            {
                queryParams.Add($"atleast={Uri.EscapeDataString(atleast)}");
            }

            if (!string.IsNullOrWhiteSpace(ratios))
            {
                queryParams.Add($"ratios={Uri.EscapeDataString(ratios)}");
            }

            string requestUrl = $"{BASE_URL}?{string.Join("&", queryParams)}";
            var response = await _httpClient.GetStringAsync(requestUrl);

            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in data.EnumerateArray())
                {
                    string id = item.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
                    if (string.IsNullOrEmpty(id)) continue;

                    string fullPath = item.TryGetProperty("path", out var pProp) ? pProp.GetString() ?? "" : "";
                    if (string.IsNullOrEmpty(fullPath)) continue;

                    string url = item.TryGetProperty("url", out var uProp) ? uProp.GetString() ?? "" : "";
                    string shortUrl = item.TryGetProperty("short_url", out var suProp) ? suProp.GetString() ?? "" : "";
                    string purityVal = item.TryGetProperty("purity", out var puProp) ? puProp.GetString() ?? "sfw" : "sfw";
                    string categoryVal = item.TryGetProperty("category", out var cProp) ? cProp.GetString() ?? "general" : "general";
                    int dimX = item.TryGetProperty("dimension_x", out var dxProp) ? dxProp.GetInt32() : 1920;
                    int dimY = item.TryGetProperty("dimension_y", out var dyProp) ? dyProp.GetInt32() : 1080;
                    string res = item.TryGetProperty("resolution", out var rProp) ? rProp.GetString() ?? $"{dimX}x{dimY}" : $"{dimX}x{dimY}";
                    string ratio = item.TryGetProperty("ratio", out var raProp) ? raProp.GetString() ?? "" : "";
                    long fileSize = item.TryGetProperty("file_size", out var fsProp) ? fsProp.GetInt64() : 0;
                    string fileType = item.TryGetProperty("file_type", out var ftProp) ? ftProp.GetString() ?? "image/jpeg" : "image/jpeg";
                    int views = item.TryGetProperty("views", out var vProp) ? vProp.GetInt32() : 0;
                    int favs = item.TryGetProperty("favorites", out var fProp) ? fProp.GetInt32() : 0;

                    string thumbUrl = string.Empty;
                    if (item.TryGetProperty("thumbs", out var thumbs))
                    {
                        if (thumbs.TryGetProperty("small", out var sThumb)) thumbUrl = sThumb.GetString() ?? "";
                        else if (thumbs.TryGetProperty("large", out var lThumb)) thumbUrl = lThumb.GetString() ?? "";
                    }

                    string fileExt = Path.GetExtension(fullPath);
                    if (string.IsNullOrEmpty(fileExt)) fileExt = fileType.Contains("png") ? ".png" : ".jpg";
                    string localTarget = Path.Combine(wallpapersDir, $"wallhaven_{id}{fileExt}");
                    bool isDownloaded = File.Exists(localTarget) && new FileInfo(localTarget).Length > 1024;

                    string localThumbFile = Path.Combine(thumbDir, $"thumb_{id}.jpg");
                    string localThumbPath = File.Exists(localThumbFile) && new FileInfo(localThumbFile).Length > 100
                        ? localThumbFile
                        : string.Empty;

                    results.Add(new WallpaperPhotoItem
                    {
                        Id = id,
                        Url = url,
                        ShortUrl = shortUrl,
                        Purity = purityVal,
                        Category = categoryVal,
                        Width = dimX,
                        Height = dimY,
                        Resolution = res,
                        Ratio = ratio,
                        FileSizeBytes = fileSize,
                        FileType = fileType,
                        FullPath = fullPath,
                        ThumbnailUrl = thumbUrl,
                        LocalThumbnailPath = localThumbPath,
                        Views = views,
                        Favorites = favs,
                        IsDownloaded = isDownloaded,
                        LocalFilePath = isDownloaded ? localTarget : string.Empty
                    });
                }
            }

            // Parallel thumbnail pre-caching for instant smooth preview cards
            var thumbTasks = new List<Task>();
            foreach (var item in results)
            {
                if (string.IsNullOrEmpty(item.ThumbnailUrl)) continue;
                string localThumb = Path.Combine(thumbDir, $"thumb_{item.Id}.jpg");
                if (File.Exists(localThumb) && new FileInfo(localThumb).Length > 100)
                {
                    item.LocalThumbnailPath = localThumb;
                }
                else
                {
                    thumbTasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            var bytes = await _httpClient.GetByteArrayAsync(item.ThumbnailUrl);
                            await File.WriteAllBytesAsync(localThumb, bytes);
                            item.LocalThumbnailPath = localThumb;
                        }
                        catch { }
                    }));
                }
            }

            if (thumbTasks.Count > 0)
            {
                await Task.WhenAny(Task.WhenAll(thumbTasks), Task.Delay(1800));
            }
        }
        catch { }

        return results;
    }

    public static async Task<string> DownloadWallpaperAsync(WallpaperPhotoItem item, IProgress<double>? progress = null)
    {
        string dir = GetWallpapersDirectory();
        string ext = Path.GetExtension(item.FullPath);
        if (string.IsNullOrEmpty(ext)) ext = item.FileType.Contains("png") ? ".png" : ".jpg";
        string localPath = Path.Combine(dir, $"wallhaven_{item.Id}{ext}");

        if (File.Exists(localPath) && new FileInfo(localPath).Length > 1024)
        {
            item.IsDownloaded = true;
            item.LocalFilePath = localPath;
            progress?.Report(1.0);
            return localPath;
        }

        string tempPath = localPath + ".download";
        using (var response = await _httpClient.GetAsync(item.FullPath, HttpCompletionOption.ResponseHeadersRead))
        {
            response.EnsureSuccessStatusCode();
            long totalBytes = response.Content.Headers.ContentLength ?? item.FileSizeBytes;

            using (var contentStream = await response.Content.ReadAsStreamAsync())
            using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
            {
                var buffer = new byte[81920];
                long totalRead = 0;
                int read;

                while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, read);
                    totalRead += read;

                    if (totalBytes > 0)
                    {
                        double p = (double)totalRead / totalBytes;
                        progress?.Report(Math.Min(0.99, p));
                    }
                }
            }
        }

        if (File.Exists(localPath)) File.Delete(localPath);
        File.Move(tempPath, localPath);

        item.IsDownloaded = true;
        item.LocalFilePath = localPath;
        progress?.Report(1.0);

        return localPath;
    }

    public static bool ApplyAsDesktopWallpaper(string imagePath)
    {
        return NativeTuning.SetDesktopWallpaper(imagePath);
    }

    public static List<LocalStaticWallpaperItem> GetLocalWallpapers()
    {
        var list = new List<LocalStaticWallpaperItem>();
        try
        {
            string dir = GetWallpapersDirectory();
            if (!Directory.Exists(dir)) return list;
            var files = Directory.GetFiles(dir, "*.*", SearchOption.TopDirectoryOnly);

            foreach (var file in files)
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext != ".jpg" && ext != ".jpeg" && ext != ".png" && ext != ".bmp" && ext != ".webp")
                {
                    continue;
                }

                var fi = new FileInfo(file);
                if (fi.Length < 1024) continue;

                string res = "";
                try
                {
                    using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        var frame = BitmapFrame.Create(fs, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                        if (frame != null && frame.PixelWidth > 0 && frame.PixelHeight > 0)
                        {
                            res = $"{frame.PixelWidth}x{frame.PixelHeight}";
                        }
                    }
                }
                catch { }

                list.Add(new LocalStaticWallpaperItem
                {
                    FilePath = file,
                    FileName = fi.Name,
                    ThumbnailPath = file,
                    FileSizeBytes = fi.Length,
                    DateAdded = fi.LastWriteTime,
                    Resolution = res
                });
            }

            list.Sort((a, b) => b.DateAdded.CompareTo(a.DateAdded));
        }
        catch { }

        return list;
    }

    public static bool DeleteLocalWallpaper(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                return true;
            }
        }
        catch { }
        return false;
    }
}

public class LocalStaticWallpaperItem
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ThumbnailPath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime DateAdded { get; set; }
    public string Resolution { get; set; } = string.Empty;
}

