namespace XuiDbManager;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        try
        {
            if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
            {
                Environment.Exit(SelfTest.Run());
                return;
            }

            ApplicationConfiguration.Initialize();
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, e) => ShowFatal(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                    AppLog.Error(ex, "Unhandled app-domain exception");
            };
            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            ShowFatal(ex);
        }
    }

    private static void ShowFatal(Exception ex)
    {
        AppLog.Error(ex, "Fatal UI exception");
        MessageBox.Show(
            $"{ex.Message}{Environment.NewLine}{Environment.NewLine}Details were written to:{Environment.NewLine}{AppLog.LogFile}",
            "XUI DB Manager",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }
}
