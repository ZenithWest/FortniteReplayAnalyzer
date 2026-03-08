using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FortniteReplayAnalyzer;

internal static class DebugOutputWriter
{
    private static readonly object Sync = new();
    private static readonly string RootPath = Path.Combine(AppContext.BaseDirectory, "DebugOutput");
    private static readonly string LogDirectory = Path.Combine(RootPath, "Logs");
    private static readonly string ReplayDirectory = Path.Combine(RootPath, "ReplayLogs");
    private static readonly string SessionLogPath = Path.Combine(LogDirectory, $"session-{DateTime.Now:yyyyMMdd-HHmmss}.log");
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    static DebugOutputWriter()
    {
        Directory.CreateDirectory(LogDirectory);
        Directory.CreateDirectory(ReplayDirectory);
        LogInfo("Debug output initialized.");
    }

    public static void LogInfo(string message) => WriteLog("INFO", message);

    public static void LogWarning(string message) => WriteLog("WARN", message);

    public static void LogError(string message, Exception? exception = null)
    {
        var fullMessage = exception is null
            ? message
            : $"{message}{Environment.NewLine}{exception}";
        WriteLog("ERROR", fullMessage);
    }

    public static void WriteReplaySnapshot(string replayPath, object payload)
    {
        var outputPath = Path.Combine(ReplayDirectory, GetReplayLogFileName(replayPath));
        var json = JsonSerializer.Serialize(payload, JsonOptions);

        lock (Sync)
        {
            File.WriteAllText(outputPath, json);
        }
    }

    private static void WriteLog(string level, string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}{Environment.NewLine}";
        lock (Sync)
        {
            File.AppendAllText(SessionLogPath, line);
        }
    }

    private static string GetReplayLogFileName(string replayPath)
    {
        var fileName = Path.GetFileNameWithoutExtension(replayPath);
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(invalidChar, '_');
        }

        return fileName + ".json";
    }
}
