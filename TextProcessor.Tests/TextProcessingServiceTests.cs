using TextProcessor.Services;
using Xunit;

namespace TextProcessor.Tests;

public class TextProcessingServiceTests
{
    private const char EmDash = '\u2014';
    private const char EnDash = '\u2013';

    // ----- Граничные случаи: пустой/пробельный/null-подобный ввод -----

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\n\n")]
    [InlineData("\t  \r\n ")]
    public void FullClean_EmptyOrWhitespace_ReturnsEmpty(string input)
    {
        Assert.Equal(string.Empty, TextProcessingService.FullClean(input));
    }

    [Fact]
    public void FullClean_Null_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, TextProcessingService.FullClean(null!));
    }

    [Fact]
    public void RemoveHiddenChars_Null_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, TextProcessingService.RemoveHiddenChars(null!));
    }

    [Fact]
    public void AllMethods_HandleNullWithoutThrowing()
    {
        // Ни один метод не должен падать на null/пустой строке.
        _ = TextProcessingService.FullClean(null!);
        _ = TextProcessingService.RemoveHiddenChars(null!);
        _ = TextProcessingService.RemoveExtraSpaces(null!);
        _ = TextProcessingService.ToSingleLine(null!);
        _ = TextProcessingService.FixParagraphs(null!);
        _ = TextProcessingService.SmartNormalize(null!);
        _ = TextProcessingService.DeNormalizeDashes(null!);
        _ = TextProcessingService.DenormalizeQuotes(null!);
        Assert.Equal(0, TextProcessingService.CountWords(null!));
        Assert.Equal(0, TextProcessingService.CountParagraphs(null!));
    }

    // ----- Ключевое требование пользователя: только дефис, без em-dash -----

    [Fact]
    public void RemoveHiddenChars_TicketWithDigits_KeepsColon_NoEmDash()
    {
        var input = "3. **Тикет 8380028:** Общий тикет по багам.";
        var result = TextProcessingService.RemoveHiddenChars(input);

        Assert.DoesNotContain(EmDash, result);
        Assert.Contains("Тикет 8380028:", result);
    }

    [Fact]
    public void RemoveHiddenChars_LetterLabelColon_UsesHyphenNotEmDash()
    {
        var input = "Введение: краткий обзор темы.";
        var result = TextProcessingService.RemoveHiddenChars(input);

        Assert.DoesNotContain(EmDash, result);
        Assert.Contains(" - ", result);
    }

    [Fact]
    public void FullClean_DoesNotGenerateEmDashFromColons()
    {
        // Двоеточия у меток не должны превращаться в em-dash (только дефис).
        var input = "Заголовок: текст. Цена: 100.";
        var result = TextProcessingService.FullClean(input);

        Assert.DoesNotContain(EmDash, result);
    }

    [Fact]
    public void DeNormalizeDashes_ConvertsEmAndEnDashToHyphen()
    {
        var input = $"слово {EmDash} слово {EnDash} слово";
        var result = TextProcessingService.DeNormalizeDashes(input);

        Assert.DoesNotContain(EmDash, result);
        Assert.DoesNotContain(EnDash, result);
        Assert.Contains("-", result);
    }

    // ----- Markdown-очистка -----

    [Fact]
    public void RemoveHiddenChars_RemovesBoldAndItalicMarkers()
    {
        var input = "Это **жирный** и *курсивный* текст.";
        var result = TextProcessingService.RemoveHiddenChars(input);

        Assert.DoesNotContain("**", result);
        Assert.Contains("жирный", result);
        Assert.Contains("курсивный", result);
    }

    [Fact]
    public void RemoveHiddenChars_RemovesInvisibleZeroWidth()
    {
        var input = "текст\u200Bсо\u200Bскрытыми";
        var result = TextProcessingService.RemoveHiddenChars(input);

        Assert.DoesNotContain('\u200B', result);
    }

    // ----- Прочие преобразования -----

    [Fact]
    public void ToSingleLine_RemovesLineBreaks()
    {
        var input = "первая\nвторая\r\nтретья";
        var result = TextProcessingService.ToSingleLine(input);

        Assert.DoesNotContain('\n', result);
        Assert.DoesNotContain('\r', result);
    }

    [Fact]
    public void RemoveExtraSpaces_CollapsesMultipleSpaces()
    {
        var input = "слово    с     пробелами";
        var result = TextProcessingService.RemoveExtraSpaces(input);

        Assert.Equal("слово с пробелами", result);
    }

    [Fact]
    public void DenormalizeQuotes_ConvertsToStraightQuotes()
    {
        var input = "«текст» и “ещё”";
        var result = TextProcessingService.DenormalizeQuotes(input);

        Assert.DoesNotContain('«', result);
        Assert.DoesNotContain('»', result);
        Assert.Contains('"', result);
    }

    [Theory]
    [InlineData("«текст»", "\"текст\"")]
    [InlineData("„текст“", "\"текст\"")]
    [InlineData("‟текст”", "\"текст\"")]
    [InlineData("‹текст›", "\"текст\"")]
    public void DenormalizeQuotes_ConvertsCommonTypography(string input, string expected)
    {
        Assert.Equal(expected, TextProcessingService.DenormalizeQuotes(input));
    }

    [Fact]
    public void SmartNormalize_DashDoesNotRemoveLineBreaks()
    {
        var input = "первая строка\n—\nвторая строка";

        Assert.Equal("первая строка\n - \nвторая строка",
            TextProcessingService.SmartNormalize(input));
    }

    [Theory]
    [InlineData("«текст»", "\"текст\"")]
    [InlineData("“текст”", "\"текст\"")]
    [InlineData("„текст“", "\"текст\"")]
    public void SmartNormalize_UsesStraightQuotesLikeAndroid(string input, string expected)
    {
        Assert.Equal(expected, TextProcessingService.SmartNormalize(input));
    }

    [Fact]
    public void FullClean_AppliesAndroidTypographyPipeline()
    {
        var input = "> **«Текст»** — пример...";

        Assert.Equal("\"Текст\" - пример…", TextProcessingService.FullClean(input));
    }

    [Fact]
    public void RemoveHiddenChars_RemovesMarkdownBlockquoteMarker()
    {
        Assert.Equal("цитата", TextProcessingService.RemoveHiddenChars("> цитата"));
    }

    [Fact]
    public void DeNormalizeDashes_DoesNotRemoveLineBreaks()
    {
        var input = "первая строка\n—\nвторая строка";

        Assert.Equal("первая строка\n - \nвторая строка",
            TextProcessingService.DeNormalizeDashes(input));
    }

    // ----- Подсчёт статистики -----

    [Fact]
    public void CountWords_CountsCyrillicWords()
    {
        Assert.Equal(3, TextProcessingService.CountWords("одно два три"));
    }

    [Fact]
    public void CountParagraphs_CountsByBlankLines()
    {
        var input = "первый абзац\n\nвторой абзац\n\nтретий";
        Assert.Equal(3, TextProcessingService.CountParagraphs(input));
    }

    [Fact]
    public void CountLines_EmptyIsOne()
    {
        Assert.Equal(1, TextProcessingService.CountLines(""));
    }

    [Fact]
    public void CountCharacters_NullIsZero()
    {
        Assert.Equal(0, TextProcessingService.CountCharacters(null));
    }

    [Fact]
    public void CountParagraphs_HandlesCarriageReturnLineEndings()
    {
        Assert.Equal(2, TextProcessingService.CountParagraphs("первый\r\rвторой"));
    }

    // ----- Устойчивость к большому и «сложному» вводу (без зависаний/исключений) -----

    [Fact]
    public void FullClean_LargeInput_CompletesQuickly()
    {
        var big = string.Concat(Enumerable.Repeat(
            "Заголовок **важный** *курсив* текст Цена: 100 тире тут.\n", 20000));

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = TextProcessingService.FullClean(big);
        sw.Stop();

        Assert.NotNull(result);
        Assert.True(sw.ElapsedMilliseconds < 5000, $"FullClean слишком медленный: {sw.ElapsedMilliseconds} ms");
    }

    [Theory]
    [InlineData("*")]
    [InlineData("**")]
    [InlineData("___")]
    [InlineData("###")]
    [InlineData("- - - - -")]
    [InlineData(":::::")]
    public void RemoveHiddenChars_PathologicalMarkers_DoNotThrow(string input)
    {
        var result = TextProcessingService.RemoveHiddenChars(input);
        Assert.NotNull(result);
    }

    [Fact]
    public void Idempotent_FullCleanTwice_SameResult()
    {
        var input = "  ## Заголовок  **жирный**\n\nЦена: 100  ";
        var once = TextProcessingService.FullClean(input);
        var twice = TextProcessingService.FullClean(once);

        Assert.Equal(once, twice);
    }
}

