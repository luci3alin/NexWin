using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace NexWin.Native;

public class PixabayVideoItem
{
    public int Id { get; set; }
    public string PageUrl { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
    public int Duration { get; set; }
    public string User { get; set; } = string.Empty;
    public int Views { get; set; }
    public int Likes { get; set; }
    public int Downloads { get; set; }

    public string ThumbnailUrl { get; set; } = string.Empty;
    public string LocalThumbnailPath { get; set; } = string.Empty;
    public string VideoUrl { get; set; } = string.Empty;
    public string Resolution { get; set; } = "1080p";
    public int Width { get; set; } = 1920;
    public int Height { get; set; } = 1080;
    public long FileSizeBytes { get; set; }
    public bool IsDownloaded { get; set; }
    public string LocalFilePath { get; set; } = string.Empty;
}

public class LocalWallpaperItem
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ThumbnailPath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime DateAdded { get; set; }
}

public static class PixabayVideoService
{
    private const string API_KEY = "57654430-33cb18ab7086b52631d19efac";
    private const string BASE_URL = "https://pixabay.com/api/videos/";

    private static readonly HttpClient _httpClient = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    static PixabayVideoService()
    {
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "NexWin/1.0.15 (Windows NT 10.0; Win64; x64)");
    }

    public static string GetWallpapersDirectory()
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NexWin",
            "LiveWallpapers"
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
            "ThumbCache"
        );
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        return dir;
    }

    public static async Task<List<PixabayVideoItem>> SearchVideosAsync(string query = "nature", string category = "", int perPage = 18, int minWidth = 0, int minHeight = 0)
    {
        var results = new List<PixabayVideoItem>();
        string thumbDir = GetThumbCacheDirectory();
        try
        {
            string cleanQuery = string.IsNullOrWhiteSpace(query) ? "background" : query.Trim();
            string url = $"{BASE_URL}?key={API_KEY}&q={Uri.EscapeDataString(cleanQuery)}&per_page={perPage}&safesearch=true";

            if (minWidth > 0)
            {
                url += $"&min_width={minWidth}";
            }
            if (minHeight > 0)
            {
                url += $"&min_height={minHeight}";
            }

            if (!string.IsNullOrWhiteSpace(category) && !category.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                url += $"&category={Uri.EscapeDataString(category.ToLowerInvariant())}";
            }

            var response = await _httpClient.GetStringAsync(url);
            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            if (root.TryGetProperty("hits", out var hits) && hits.ValueKind == JsonValueKind.Array)
            {
                string wallpapersDir = GetWallpapersDirectory();

                foreach (var hit in hits.EnumerateArray())
                {
                    int id = hit.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : 0;
                    if (id == 0) continue;

                    string tags = hit.TryGetProperty("tags", out var tProp) ? tProp.GetString() ?? "" : "";
                    int duration = hit.TryGetProperty("duration", out var dProp) ? dProp.GetInt32() : 0;
                    string user = hit.TryGetProperty("user", out var uProp) ? uProp.GetString() ?? "" : "";
                    int views = hit.TryGetProperty("views", out var vProp) ? vProp.GetInt32() : 0;
                    int likes = hit.TryGetProperty("likes", out var lProp) ? lProp.GetInt32() : 0;
                    int downloads = hit.TryGetProperty("downloads", out var dlProp) ? dlProp.GetInt32() : 0;
                    string pageUrl = hit.TryGetProperty("pageURL", out var pUrlProp) ? pUrlProp.GetString() ?? "" : "";

                    string videoUrl = string.Empty;
                    string thumbUrl = string.Empty;
                    string resolution = "1080p";
                    int vidWidth = 1920;
                    int vidHeight = 1080;
                    long sizeBytes = 0;

                    if (hit.TryGetProperty("videos", out var videos) && videos.ValueKind == JsonValueKind.Object)
                    {
                        // Prioritize highest quality for wallpaper display (large -> medium -> small -> tiny)
                        JsonElement chosenVariant = default;
                        string chosenKey = "";

                        if (videos.TryGetProperty("large", out var lg) && lg.TryGetProperty("url", out var lUrl) && !string.IsNullOrEmpty(lUrl.GetString()))
                        {
                            chosenVariant = lg;
                            chosenKey = "large";
                        }
                        else if (videos.TryGetProperty("medium", out var med) && med.TryGetProperty("url", out var mUrl) && !string.IsNullOrEmpty(mUrl.GetString()))
                        {
                            chosenVariant = med;
                            chosenKey = "medium";
                        }
                        else if (videos.TryGetProperty("small", out var sm) && sm.TryGetProperty("url", out var sUrl) && !string.IsNullOrEmpty(sUrl.GetString()))
                        {
                            chosenVariant = sm;
                            chosenKey = "small";
                        }
                        else if (videos.TryGetProperty("tiny", out var tn) && tn.TryGetProperty("url", out var tnUrl) && !string.IsNullOrEmpty(tnUrl.GetString()))
                        {
                            chosenVariant = tn;
                            chosenKey = "tiny";
                        }

                        if (!string.IsNullOrEmpty(chosenKey))
                        {
                            videoUrl = chosenVariant.TryGetProperty("url", out var vu) ? vu.GetString() ?? "" : "";
                            sizeBytes = chosenVariant.TryGetProperty("size", out var sz) ? sz.GetInt64() : 0;
                            vidWidth = chosenVariant.TryGetProperty("width", out var wp) ? wp.GetInt32() : 1920;
                            vidHeight = chosenVariant.TryGetProperty("height", out var hp) ? hp.GetInt32() : 1080;

                            // Fast lightweight thumbnail (prefer medium or small)
                            if (videos.TryGetProperty("medium", out var mTh) && mTh.TryGetProperty("thumbnail", out var mtUrl) && !string.IsNullOrEmpty(mtUrl.GetString()))
                            {
                                thumbUrl = mtUrl.GetString() ?? "";
                            }
                            else if (videos.TryGetProperty("small", out var sTh) && sTh.TryGetProperty("thumbnail", out var stUrl) && !string.IsNullOrEmpty(stUrl.GetString()))
                            {
                                thumbUrl = stUrl.GetString() ?? "";
                            }
                            else if (chosenVariant.TryGetProperty("thumbnail", out var tu))
                            {
                                thumbUrl = tu.GetString() ?? "";
                            }

                            if (vidWidth >= 3840 || vidHeight >= 2160) resolution = "4K UHD";
                            else if (vidWidth >= 2560 || vidHeight >= 1440) resolution = "1440p QHD";
                            else if (vidWidth >= 1920 || vidHeight >= 1080) resolution = "1080p FHD";
                            else resolution = $"{vidWidth}x{vidHeight}";
                        }
                    }

                    if (string.IsNullOrEmpty(videoUrl)) continue;

                    string localTarget = Path.Combine(wallpapersDir, $"pixabay_{id}.mp4");
                    bool isDownloaded = File.Exists(localTarget);

                    string localThumbFile = Path.Combine(thumbDir, $"thumb_{id}.jpg");
                    string localThumbPath = File.Exists(localThumbFile) ? localThumbFile : string.Empty;

                    results.Add(new PixabayVideoItem
                    {
                        Id = id,
                        PageUrl = pageUrl,
                        Tags = tags,
                        Duration = duration,
                        User = user,
                        Views = views,
                        Likes = likes,
                        Downloads = downloads,
                        ThumbnailUrl = thumbUrl,
                        LocalThumbnailPath = localThumbPath,
                        VideoUrl = videoUrl,
                        Resolution = resolution,
                        Width = vidWidth,
                        Height = vidHeight,
                        FileSizeBytes = sizeBytes,
                        IsDownloaded = isDownloaded,
                        LocalFilePath = isDownloaded ? localTarget : string.Empty
                    });
                }
            }

            // Fast parallel thumbnail pre-caching so cards show previews immediately without UI lag
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

    public static async Task<string> DownloadVideoAsync(PixabayVideoItem item, IProgress<double>? progress = null)
    {
        string dir = GetWallpapersDirectory();
        string localVideoPath = Path.Combine(dir, $"pixabay_{item.Id}.mp4");
        string localThumbPath = Path.Combine(dir, $"pixabay_{item.Id}.jpg");

        // If already downloaded and valid, return immediately
        if (File.Exists(localVideoPath) && new FileInfo(localVideoPath).Length > 1024)
        {
            item.IsDownloaded = true;
            item.LocalFilePath = localVideoPath;
            progress?.Report(1.0);
            return localVideoPath;
        }

        // Download thumbnail if missing
        if (!string.IsNullOrEmpty(item.ThumbnailUrl) && !File.Exists(localThumbPath))
        {
            try
            {
                var thumbBytes = await _httpClient.GetByteArrayAsync(item.ThumbnailUrl);
                await File.WriteAllBytesAsync(localThumbPath, thumbBytes);
            }
            catch { }
        }

        // Stream video download with progress tracking
        string tempFile = localVideoPath + ".download";
        using (var response = await _httpClient.GetAsync(item.VideoUrl, HttpCompletionOption.ResponseHeadersRead))
        {
            response.EnsureSuccessStatusCode();
            long totalBytes = response.Content.Headers.ContentLength ?? item.FileSizeBytes;

            using (var contentStream = await response.Content.ReadAsStreamAsync())
            using (var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
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

        if (File.Exists(localVideoPath))
        {
            File.Delete(localVideoPath);
        }
        File.Move(tempFile, localVideoPath);

        item.IsDownloaded = true;
        item.LocalFilePath = localVideoPath;
        progress?.Report(1.0);

        return localVideoPath;
    }

    public static List<LocalWallpaperItem> GetLocalWallpapers()
    {
        var list = new List<LocalWallpaperItem>();
        try
        {
            string dir = GetWallpapersDirectory();
            var files = Directory.GetFiles(dir, "*.*", SearchOption.TopDirectoryOnly);

            foreach (var file in files)
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext != ".mp4" && ext != ".webm" && ext != ".gif" && ext != ".wmv" && ext != ".mov")
                {
                    continue;
                }

                var fi = new FileInfo(file);
                if (fi.Length < 1024) continue;

                string baseName = Path.GetFileNameWithoutExtension(file);
                string possibleThumb = Path.Combine(dir, baseName + ".jpg");
                if (!File.Exists(possibleThumb))
                {
                    possibleThumb = Path.Combine(dir, baseName + ".png");
                }

                list.Add(new LocalWallpaperItem
                {
                    FilePath = file,
                    FileName = fi.Name,
                    ThumbnailPath = File.Exists(possibleThumb) ? possibleThumb : string.Empty,
                    FileSizeBytes = fi.Length,
                    DateAdded = fi.LastWriteTime
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
                string baseName = Path.GetFileNameWithoutExtension(filePath);
                string dir = Path.GetDirectoryName(filePath) ?? "";
                string thumbJpg = Path.Combine(dir, baseName + ".jpg");
                string thumbPng = Path.Combine(dir, baseName + ".png");
                if (File.Exists(thumbJpg)) File.Delete(thumbJpg);
                if (File.Exists(thumbPng)) File.Delete(thumbPng);
                return true;
            }
        }
        catch { }
        return false;
    }
}
