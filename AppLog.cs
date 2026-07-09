namespace XuiDbManager;

public static class AppLog
{
    public static string LogDirectory { get; } = Path.Combine(AppContext.BaseDirectory, "logs");
    public static string LogFile { get; } = Path.Combine(LogDirectory, "xui-dbmanager.log");

    public static void Error(Exception ex, string context)
    {
        Directory.CreateDirectory(LogDirectory);
        File.AppendAllText(LogFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
    }
}
