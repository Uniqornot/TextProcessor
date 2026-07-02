using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TextProcessor.Services;

namespace TextProcessor.ViewModels;

/// <summary>
/// ViewModel главного окна приложения.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string inputText = string.Empty;

    [ObservableProperty]
    private string outputText = string.Empty;

    [ObservableProperty]
    private bool isFeaturesExpanded;

    public int CharCount => TextProcessingService.CountCharacters(InputText);

    public int WordCount => TextProcessingService.CountWords(InputText);

    public int LineCount => TextProcessingService.CountLines(InputText);

    public int ParagraphCount => TextProcessingService.CountParagraphs(InputText);

    public int OutputCharCount => TextProcessingService.CountCharacters(OutputText);

    public int OutputWordCount => TextProcessingService.CountWords(OutputText);

    public int OutputLineCount => string.IsNullOrEmpty(OutputText)
        ? 0
        : TextProcessingService.CountLines(OutputText);

    public int OutputParagraphCount => TextProcessingService.CountParagraphs(OutputText);

    public bool HasOutput => !string.IsNullOrEmpty(OutputText);

    // Порог, выше которого перенос слов отключается ради скорости раскладки WPF TextBox.
    private const int WrapThreshold = 15000;

    public TextWrapping InputWrapping =>
        InputText.Length > WrapThreshold ? TextWrapping.NoWrap : TextWrapping.Wrap;

    public TextWrapping OutputWrapping =>
        OutputText.Length > WrapThreshold ? TextWrapping.NoWrap : TextWrapping.Wrap;

    [RelayCommand]
    private void FullClean() =>
        OutputText = TextProcessingService.FullClean(InputText);

    [RelayCommand]
    private void RemoveExtraSpaces() =>
        OutputText = TextProcessingService.RemoveExtraSpaces(InputText);

    [RelayCommand]
    private void ToSingleLine() =>
        OutputText = TextProcessingService.ToSingleLine(InputText);

    [RelayCommand]
    private void FixParagraphs() =>
        OutputText = TextProcessingService.FixParagraphs(InputText);

    [RelayCommand]
    private void SmartNormalize() =>
        OutputText = TextProcessingService.SmartNormalize(InputText);

    [RelayCommand]
    private void DeNormalizeDashes() =>
        OutputText = TextProcessingService.DeNormalizeDashes(InputText);

    [RelayCommand]
    private void DenormalizeQuotes() =>
        OutputText = TextProcessingService.DenormalizeQuotes(InputText);

    [RelayCommand]
    private void RemoveHiddenChars() =>
        OutputText = TextProcessingService.RemoveHiddenChars(InputText);

    [RelayCommand]
    private void ManageLineBreaks() =>
        OutputText = TextProcessingService.FixParagraphs(InputText);

    [RelayCommand]
    private async Task PasteInput()
    {
        var result = await RunOnStaThreadAsync(TryGetClipboardText);
        if (result.Success && !string.IsNullOrEmpty(result.Text))
            InputText = result.Text;
    }

    [RelayCommand]
    private async Task CopyOutput()
    {
        if (!HasOutput)
            return;

        var value = OutputText;
        var ok = await RunOnStaThreadAsync(() => TrySetClipboardText(value));
        if (!ok)
        {
            MessageBox.Show(
                "Не удалось скопировать текст: буфер обмена сейчас занят другой программой " +
                "(например, менеджером буфера обмена). Попробуйте нажать «Копировать» ещё раз.",
                "Обработчик текста",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    /// <summary>
    /// Выполняет работу с буфером обмена в фоновом потоке, чтобы UI не зависал.
    /// </summary>
    private static Task<T> RunOnStaThreadAsync<T>(Func<T> action)
    {
        var tcs = new TaskCompletionSource<T>();
        var thread = new Thread(() =>
        {
            try { tcs.SetResult(action()); }
            catch (Exception) { tcs.SetResult(default!); }
        })
        {
            IsBackground = true
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return tcs.Task;
    }

    // Работа с буфером обмена напрямую через Win32 API: повтор идёт именно на
    // захвате буфера (OpenClipboard), поэтому это надёжнее OLE-обёртки WPF,
    // когда буфер удерживает другой процесс (менеджер буфера обмена).
    private const uint CfUnicodeText = 13;
    private const uint GmemMoveable = 0x0002;
    private const int ClipboardOpenAttempts = 50;
    private const int ClipboardOpenDelayMs = 40;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetClipboardData(uint uFormat);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool IsClipboardFormatAvailable(uint format);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalFree(IntPtr hMem);

    private static bool TryOpenClipboard()
    {
        for (var attempt = 0; attempt < ClipboardOpenAttempts; attempt++)
        {
            if (OpenClipboard(IntPtr.Zero))
                return true;
            Thread.Sleep(ClipboardOpenDelayMs);
        }

        return false;
    }

    private static bool TrySetClipboardText(string value)
    {
        value ??= string.Empty;

        if (!TryOpenClipboard())
            return false;

        try
        {
            if (!EmptyClipboard())
                return false;

            var bytes = (UIntPtr)((value.Length + 1) * 2);
            var hGlobal = GlobalAlloc(GmemMoveable, bytes);
            if (hGlobal == IntPtr.Zero)
                return false;

            var target = GlobalLock(hGlobal);
            if (target == IntPtr.Zero)
            {
                GlobalFree(hGlobal);
                return false;
            }

            try
            {
                if (value.Length > 0)
                    Marshal.Copy(value.ToCharArray(), 0, target, value.Length);
                Marshal.WriteInt16(target, value.Length * 2, 0); // завершающий null
            }
            finally
            {
                GlobalUnlock(hGlobal);
            }

            if (SetClipboardData(CfUnicodeText, hGlobal) == IntPtr.Zero)
            {
                GlobalFree(hGlobal); // система не приняла память — освобождаем сами
                return false;
            }

            return true; // при успехе памятью владеет система
        }
        finally
        {
            CloseClipboard();
        }
    }

    private static (bool Success, string Text) TryGetClipboardText()
    {
        if (!IsClipboardFormatAvailable(CfUnicodeText))
            return (true, string.Empty); // в буфере нет текста — это не ошибка

        if (!TryOpenClipboard())
            return (false, string.Empty);

        try
        {
            var handle = GetClipboardData(CfUnicodeText);
            if (handle == IntPtr.Zero)
                return (true, string.Empty);

            var ptr = GlobalLock(handle);
            if (ptr == IntPtr.Zero)
                return (true, string.Empty);

            try
            {
                return (true, Marshal.PtrToStringUni(ptr) ?? string.Empty);
            }
            finally
            {
                GlobalUnlock(handle);
            }
        }
        finally
        {
            CloseClipboard();
        }
    }

    [RelayCommand]
    private void ClearInput()
    {
        InputText = string.Empty;
        OutputText = string.Empty;
    }

    [RelayCommand]
    private void ClearOutput() =>
        OutputText = string.Empty;

    partial void OnInputTextChanged(string value)
    {
        OnPropertyChanged(nameof(CharCount));
        OnPropertyChanged(nameof(WordCount));
        OnPropertyChanged(nameof(LineCount));
        OnPropertyChanged(nameof(ParagraphCount));
        OnPropertyChanged(nameof(InputWrapping));
    }

    partial void OnOutputTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasOutput));
        OnPropertyChanged(nameof(OutputCharCount));
        OnPropertyChanged(nameof(OutputWordCount));
        OnPropertyChanged(nameof(OutputLineCount));
        OnPropertyChanged(nameof(OutputParagraphCount));
        OnPropertyChanged(nameof(OutputWrapping));
    }
}
