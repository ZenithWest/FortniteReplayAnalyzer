using System.Drawing;
using System.Text.Json;

namespace FortniteReplayAnalyzer;

internal sealed class AnalyzerSettings
{
    public string DefaultReplaysFolder { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FortniteGame",
        "Saved",
        "Demos");

    public bool DebugOutputEnabled { get; set; } = false;
    public string AccentColor { get; set; } = "#1976D2";
    public string SurfaceColor { get; set; } = "#F7F9FC";
    public string BackgroundColor { get; set; } = "#FFFFFF";

    public AnalyzerSettings Clone()
    {
        return new AnalyzerSettings
        {
            DefaultReplaysFolder = DefaultReplaysFolder,
            DebugOutputEnabled = DebugOutputEnabled,
            AccentColor = AccentColor,
            SurfaceColor = SurfaceColor,
            BackgroundColor = BackgroundColor
        };
    }

    public Color GetAccentColor() => ParseColor(AccentColor, Color.FromArgb(25, 118, 210));
    public Color GetSurfaceColor() => ParseColor(SurfaceColor, Color.FromArgb(247, 249, 252));
    public Color GetBackgroundColor() => ParseColor(BackgroundColor, Color.White);

    public static string ToColorText(Color color) => ColorTranslator.ToHtml(color);

    private static Color ParseColor(string? value, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        try
        {
            return ColorTranslator.FromHtml(value);
        }
        catch
        {
            return fallback;
        }
    }
}

internal static class AnalyzerSettingsStore
{
    private static readonly string SettingsPath = Path.Combine(AppContext.BaseDirectory, "Setting.txt");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static AnalyzerSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new AnalyzerSettings();
            }

            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<AnalyzerSettings>(json, JsonOptions) ?? new AnalyzerSettings();
        }
        catch
        {
            return new AnalyzerSettings();
        }
    }

    public static void Save(AnalyzerSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(SettingsPath, json);
    }
}
