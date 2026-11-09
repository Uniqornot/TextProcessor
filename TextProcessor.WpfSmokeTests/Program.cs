using System.Windows;
using System.Windows.Interop;
using TextProcessor;
using TextProcessor.ViewModels;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        try
        {
            var app = new App();
            app.InitializeComponent();
            var window = new MainWindow();
            app.MainWindow = window;
            if (new WindowInteropHelper(window).EnsureHandle() == IntPtr.Zero)
                throw new Exception("No HWND for clipboard ownership");
            if (window.DataContext is not MainViewModel vm)
                throw new Exception("Missing ViewModel");

            vm.InputText = "> **«Цена:** 100 — 200»\n\nhttps://example.org/a...b";
            vm.FullCleanCommand.Execute(null);
            Equal("\"Цена: 100 - 200\"\n\nhttps://example.org/a...b", vm.OutputText);
            if (!vm.HasOutput || vm.OutputParagraphCount != 2)
                throw new Exception("Output bindings/statistics are incorrect");
            vm.InputText = vm.OutputText;
            vm.FullCleanCommand.Execute(null);
            Equal(vm.InputText, vm.OutputText);
            vm.InputText = "well-known --help −2";
            vm.DeNormalizeDashesCommand.Execute(null);
            Equal(vm.InputText, vm.OutputText);
            vm.InputText = "e\u0301’amour “цитата”";
            vm.DenormalizeQuotesCommand.Execute(null);
            Equal("e\u0301'amour \"цитата\"", vm.OutputText);
            vm.InputText = "2 * 3 * 4";
            vm.RemoveHiddenCharsCommand.Execute(null);
            Equal(vm.InputText, vm.OutputText);
            vm.ClearInputCommand.Execute(null);
            Equal(string.Empty, vm.InputText);
            Equal(string.Empty, vm.OutputText);
            if (vm.HasOutput || vm.OutputParagraphCount != 0)
                throw new Exception("Clear command left stale output state");
            window.Close();
            app.Shutdown();
            Console.WriteLine("WPF smoke passed: resources, window/HWND, ViewModel commands and output state. Clipboard was not modified.");
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            return 1;
        }
    }

    private static void Equal(string expected, string actual)
    {
        if (expected != actual)
            throw new Exception($"Expected {expected}, got {actual}");
    }
}
