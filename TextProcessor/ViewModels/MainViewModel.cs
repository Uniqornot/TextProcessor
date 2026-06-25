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
    private void PasteInput()
    {
        if (Clipboard.ContainsText())
            InputText = Clipboard.GetText();
    }

    [RelayCommand]
    private void CopyOutput()
    {
        if (HasOutput)
            Clipboard.SetText(OutputText);
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
    }

    partial void OnOutputTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasOutput));
        OnPropertyChanged(nameof(OutputCharCount));
        OnPropertyChanged(nameof(OutputWordCount));
        OnPropertyChanged(nameof(OutputLineCount));
        OnPropertyChanged(nameof(OutputParagraphCount));
    }
}
