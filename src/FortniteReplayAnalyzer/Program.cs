namespace FortniteReplayAnalyzer;

using FortniteReplayReader;
using System.Text.Json;

internal static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();

        var replayFile = "C:\\Users\\Zenit\\AppData\\Local\\FortniteGame\\Saved\\Demos\\UnsavedReplay-2026.02.23-20.11.58.replay";
        var reader = new ReplayReader();
        var replay = reader.ReadReplay(replayFile);

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        StreamWriter writer = new StreamWriter("output.json");

        writer.WriteLine(JsonSerializer.Serialize(replay, options));

        Application.Run(new FortniteReplayAnalyzer());

    }
}