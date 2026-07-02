using System.Windows;
using System.Windows.Threading;

namespace TextProcessor;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ShowError(e.Exception);
        e.Handled = true;
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            ShowError(ex);
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
