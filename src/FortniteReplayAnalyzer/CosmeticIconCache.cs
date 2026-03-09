using System.Collections.Concurrent;
using System.Text.Json;

namespace FortniteReplayAnalyzer;

internal static class CosmeticIconCache
{
    private static readonly HttpClient HttpClient = new();
    private static readonly ConcurrentDictionary<string, Task<string?>> PendingDownloads = new(StringComparer.OrdinalIgnoreCase);
    private static readonly SemaphoreSlim DownloadThrottle = new(2, 2);
    private static readonly string CacheFolder = Path.Combine(AppContext.BaseDirectory, "Assets", "Cosmetics");
    private static readonly Image PlaceholderImage = BuildPlaceholderImage();

    public static string GetCachePath(string cosmeticId)
    {
        Directory.CreateDirectory(CacheFolder);
        return Path.Combine(CacheFolder, $"{SanitizeFileName(cosmeticId)}.png");
    }

    public static async Task<string?> EnsureIconAsync(string? cosmeticId)
    {
        if (string.IsNullOrWhiteSpace(cosmeticId))
        {
            return null;
        }

        var cachePath = GetCachePath(cosmeticId);
        if (File.Exists(cachePath))
        {
            return cachePath;
        }

        var task = PendingDownloads.GetOrAdd(cosmeticId, id => DownloadIconAsync(id, cachePath));
        try
        {
            return await task.ConfigureAwait(false);
        }
        finally
        {
            PendingDownloads.TryRemove(cosmeticId, out _);
        }
    }

    public static Image? LoadCachedImage(string? cosmeticId)
    {
        if (string.IsNullOrWhiteSpace(cosmeticId))
        {
            return null;
        }

        var cachePath = GetCachePath(cosmeticId);
        if (!File.Exists(cachePath))
        {
            return null;
        }

        try
        {
            using var stream = new FileStream(cachePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            if (memoryStream.Length == 0)
            {
                return null;
            }
            memoryStream.Position = 0;
            using var image = Image.FromStream(memoryStream);
            return new Bitmap(image);
        }
        catch (IOException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            TryDeleteCorruptCacheFile(cachePath);
            return null;
        }
    }

    public static Image GetPlaceholderImage() => (Image)PlaceholderImage.Clone();

    public static void QueueBackgroundDownload(string? cosmeticId)
    {
        if (string.IsNullOrWhiteSpace(cosmeticId))
        {
            return;
        }

        _ = Task.Run(async () => await EnsureIconAsync(cosmeticId).ConfigureAwait(false));
    }

    private static async Task<string?> DownloadIconAsync(string cosmeticId, string cachePath)
    {
        var tempPath = $"{cachePath}.{Guid.NewGuid():N}.tmp";
        var throttleEntered = false;
        try
        {
            await DownloadThrottle.WaitAsync().ConfigureAwait(false);
            throttleEntered = true;
            var encodedId = Uri.EscapeDataString(cosmeticId);
            var response = await HttpClient.GetStringAsync($"https://fortnite-api.com/v2/cosmetics/br/search?matchMethod=contains&id={encodedId}").ConfigureAwait(false);
            using var document = JsonDocument.Parse(response);
            if (!document.RootElement.TryGetProperty("data", out var data))
            {
                return null;
            }

            if (!data.TryGetProperty("images", out var images) || !images.TryGetProperty("smallIcon", out var smallIconElement))
            {
                return null;
            }

            var iconUrl = smallIconElement.GetString();
            if (string.IsNullOrWhiteSpace(iconUrl))
            {
                return null;
            }

            await using var iconStream = await HttpClient.GetStreamAsync(iconUrl).ConfigureAwait(false);
            await using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await iconStream.CopyToAsync(fileStream).ConfigureAwait(false);
            }

            File.Move(tempPath, cachePath, overwrite: true);
            return cachePath;
        }
        catch (Exception ex)
        {
            DebugOutputWriter.LogError($"Failed to download cosmetic icon for '{cosmeticId}'.", ex);
            return null;
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                TryDeleteCorruptCacheFile(tempPath);
            }

            if (throttleEntered)
            {
                DownloadThrottle.Release();
            }
        }
    }

    private static string SanitizeFileName(string input)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(input.Select(ch => invalid.Contains(ch) ? '_' : ch));
    }

    private static void TryDeleteCorruptCacheFile(string cachePath)
    {
        try
        {
            if (File.Exists(cachePath))
            {
                File.Delete(cachePath);
            }
        }
        catch
        {
            // Ignore cleanup failures; the caller already falls back to no image.
        }
    }

    private static Bitmap BuildPlaceholderImage()
    {
        var bitmap = new Bitmap(32, 32);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.FromArgb(234, 237, 242));
        using var brush = new SolidBrush(Color.FromArgb(169, 177, 190));
        graphics.FillEllipse(brush, 10, 5, 12, 12);
        graphics.FillEllipse(brush, 6, 18, 20, 10);
        return bitmap;
    }
}
