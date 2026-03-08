namespace FortniteReplayAnalyzer;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, args) => DebugOutputWriter.LogError("Unhandled UI exception.", args.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, args) => DebugOutputWriter.LogError("Unhandled process exception.", args.ExceptionObject as Exception);

        ApplicationConfiguration.Initialize();
        Application.Run(new FortniteReplayAnalyzer());
    }
}
