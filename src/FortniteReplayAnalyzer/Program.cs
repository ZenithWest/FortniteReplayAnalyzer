using FortniteReplayReader;
using FortniteReplayReader.Models;
using Microsoft.VisualBasic.ApplicationServices;
using System.Runtime.Intrinsics.Arm;
using Unreal.Core.Models.Enums;

namespace FortniteReplayAnalyzer;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var enable_debug_load_replay = false;

        if (enable_debug_load_replay)
        {
            var filename = "C:\\Users\\Zenit\\AppData\\Local\\FortniteGame\\Saved\\Demos\\UnsavedReplay-2026.03.11-21.53.21.replay";
            var reader = new ReplayReader(null, ParseMode.Full);
            var blah = reader.TryReadReplay(filename, out var replay, out var exception);
            enable_debug_load_replay = false;
        }

        var settings = AnalyzerSettingsStore.Load();
        DebugOutputWriter.SetEnabled(settings.DebugOutputEnabled);

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, args) => DebugOutputWriter.LogError("Unhandled UI exception.", args.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, args) => DebugOutputWriter.LogError("Unhandled process exception.", args.ExceptionObject as Exception);

        ApplicationConfiguration.Initialize();
        Application.Run(new FortniteReplayAnalyzer(settings));
    }
}
