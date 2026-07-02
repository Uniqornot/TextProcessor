using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace TextProcessor;

public partial class App : Application
{
    private static readonly string LogPath =
        Path.Combine(Path.GetTempPath(), "textprocessor_error.log");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log("Dispatcher", e.Exception);
        ShowError(e.Exception);
        e.Handled = true;
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            Log("Domain", ex);
            ShowError(ex);
        }
    }

    private static void Log(string source, Exception ex)
    {
        try
        {
            File.AppendAllText(LogPath,
                $"[{DateTime.Now:O}] {source}: {ex}\n\n");
        }
        catch
        {
            // логирование не должно падать само
        }
    }

    private static void ShowError(Exception ex)
    {
        MessageBox.Show(
            $"Произошла ошибка:\n\n{ex.Message}",
            "Обработчик текста",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }
}
