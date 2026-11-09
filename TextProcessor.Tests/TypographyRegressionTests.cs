using TextProcessor.Services;
using Xunit;

namespace TextProcessor.Tests;

public class TypographyRegressionTests
{
    [Theory]
    [InlineData("«»“”„‟‹›〝〞〟＂「」『』❝❞")]
    public void AllDoubleQuotes_UseAsciiAcrossActions(string quotes)
    {
        foreach (var c in quotes)
        {
            var input = $"{c}текст{c}";
            const string expected = "\"текст\"";
            Assert.Equal(expected, TextProcessingService.DenormalizeQuotes(input));
            Assert.Equal(expected, TextProcessingService.SmartNormalize(input));
            Assert.Equal(expected, TextProcessingService.FullClean(input));
        }
    }

    [Theory]
    [InlineData("\u2012")]
    [InlineData("\u2013")]
    [InlineData("\u2014")]
    [InlineData("\u2015")]
    [InlineData("\u2E3A")]
    [InlineData("\u2E3B")]
    [InlineData("\uFE31")]
    [InlineData("\uFE32")]
    [InlineData("\uFE58")]
    public void Dashes_UseSameRulesWithoutJoiningLines(string dash)
    {
        var input = $"один{dash}два\n{dash}\nтри";
        const string expected = "один - два\n - \nтри";
        Assert.Equal(expected, TextProcessingService.DeNormalizeDashes(input));
        Assert.Equal(expected, TextProcessingService.SmartNormalize(input));
        Assert.Equal("один - два\n-\nтри", TextProcessingService.FullClean(input));
    }

    [Theory]
    [InlineData("Цена: 100.25\n\nВведение: обзор.\nМетка: текст.")]
    [InlineData("well-known --help --output=file -n x − 5 = −2 2010-2020")]
    [InlineData("https://example.org/a_b_c?x=1&y=2#part\n\nhttps://example.org/a...b/--x")]
    [InlineData("𝄞 𝒜 𠀀 😀 © ™ ± ∞ ≠ ☀ ➕ 👩‍💻 می‌روم")]
    [InlineData("1. Первый пункт\n2) Второй пункт\n\n2026. Год")]
    [InlineData("Слова остаются: важная роль, таким образом, в заключение.")]
    public void FullClean_PreservesContent(string input)
    {
        var actual = TextProcessingService.FullClean(input);
        Assert.Equal(input, actual);
        Assert.Equal(actual, TextProcessingService.FullClean(actual));
    }

    [Fact]
    public void Apostrophes_ArePreservedInsideWords()
    {
        const string input = "don't l’amour O’Neill ‘цитата’";
        const string expected = "don't l'amour O'Neill \"цитата\"";
        Assert.Equal(expected, TextProcessingService.DenormalizeQuotes(input));
        Assert.Equal(expected, TextProcessingService.FullClean(input));
    }

    [Fact]
    public void FullPipeline_PreservesParagraphsColonsAndReferences()
    {
        const string input = "## «Заголовок»\r\n\r\n> **Цена:** 100 ⸺ 200.\r\n\r\nИсточник: https://example.org/a_b_c\r\n𠀀 𝄞 −2 --help";
        const string expected = "\"Заголовок\"\n\nЦена: 100 - 200.\n\nИсточник: https://example.org/a_b_c\n𠀀 𝄞 −2 --help";
        var actual = TextProcessingService.FullClean(input);
        Assert.Equal(expected, actual);
        Assert.Equal(actual, TextProcessingService.FullClean(actual));
    }
}
