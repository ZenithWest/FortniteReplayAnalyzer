using System.Collections.Concurrent;
using System.Text.Json;

namespace FortniteReplayAnalyzer;

internal static class CosmeticIconCache
{
    private static readonly HttpClient HttpClient = new();
    private static readonly ConcurrentDictionary<string, Task<string?>> PendingDownloads = new(StringComparer.OrdinalIgnoreCase);
    private static readonly string CacheFolder = Path.Combine(AppContext.BaseDirectory, "DebugOutput", "CosmeticCache");

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

        using var stream = File.OpenRead(cachePath);
        return Image.FromStream(stream);
    }

    private static async Task<string?> DownloadIconAsync(string cosmeticId, string cachePath)
    {
        try
        {
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
            await using var fileStream = File.Create(cachePath);
            await iconStream.CopyToAsync(fileStream).ConfigureAwait(false);
            return cachePath;
        }
        catch (Exception ex)
        {
            DebugOutputWriter.LogError($"Failed to download cosmetic icon for '{cosmeticId}'.", ex);
            return null;
        }
    }

    private static string SanitizeFileName(string input)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(input.Select(ch => invalid.Contains(ch) ? '_' : ch));
    }
}
