using TextProcessor.Services;
using Xunit;

namespace TextProcessor.Tests;

public class ReviewRegressionTests
{
    [Theory]
    [InlineData("— Начало", "- Начало")]
    [InlineData("- Начало", "- Начало")]
    [InlineData("2 * 3 * 4 = 24", "2 * 3 * 4 = 24")]
    [InlineData("x**2 + y**2", "x**2 + y**2")]
    [InlineData("***важно***", "важно")]
    [InlineData("> ## Заголовок", "Заголовок")]
    [InlineData("\\*буквально\\*", "\\*буквально\\*")]
    [InlineData("**https://example.org/a_b**", "https://example.org/a_b")]
    [InlineData("__https://example.org/a_b__", "https://example.org/a_b")]
    [InlineData("„https://example.org/path“", "\"https://example.org/path\"")]
    public void FullClean_IsSafeAndIdempotent(string input, string expected)
    {
        var first = TextProcessingService.FullClean(input);
        Assert.Equal(expected, first);
        Assert.Equal(first, TextProcessingService.FullClean(first));
    }

    [Theory]
    [InlineData("https://example.org/a...b/—x")]
    [InlineData("HTTPS://example.org/a...b/—x")]
    [InlineData("https://example.org/a_b_c")]
    public void IndividualActions_PreserveUrls(string url)
    {
        Assert.Equal(url, TextProcessingService.RemoveHiddenChars(url));
        Assert.Equal(url, TextProcessingService.SmartNormalize(url));
        Assert.Equal(url, TextProcessingService.DeNormalizeDashes(url));
        Assert.Equal(url, TextProcessingService.DenormalizeQuotes(url));
        Assert.Equal(url, TextProcessingService.FullClean(url));
    }

    [Theory]
    [InlineData("e\u0301’amour", "e\u0301'amour")]
    [InlineData("𐐀’𐐨", "𐐀'𐐨")]
    public void Apostrophes_SupportUnicodeLettersAndMarks(string input, string expected)
    {
        Assert.Equal(expected, TextProcessingService.FullClean(input));
    }

    [Fact]
    public void EmojiTags_AreNotRemovedFromFlagSequences()
    {
        const string scotland = "\U0001F3F4\U000E0067\U000E0062\U000E0073\U000E0063\U000E0074\U000E007F";
        Assert.Equal(scotland, TextProcessingService.FullClean(scotland));
    }

    [Fact]
    public void ProtectedContent_CannotCollideWithInputOrRewriteCode()
    {
        const string input = "\uE0000\uE001 \uE000\uE0001\uE001 `2 * 3 * 4 --help` \\*literal\\* https://example.org/a...b";
        Assert.Equal(input, TextProcessingService.FullClean(input));
    }

    [Fact]
    public void Cleanup_MixedCorpusIsIdempotent()
    {
        var random = new Random(42);
        var samples = new[] { "**текст**", "***важно***", "- строка", "— строка", "> ## Тема",
            "https://example.org/a_b", "__https://example.org/x__", "2 * 3 * 4", "x**2 + y**2",
            "\\*буквально\\*", "`--help a...b`", "𠀀 𝄞 −2", "«цитата»", "Цена: 100" };
        for (var i = 0; i < 300; i++)
        {
            var input = string.Join("\n\n", Enumerable.Range(0, 5).Select(_ => samples[random.Next(samples.Length)]));
            var once = TextProcessingService.FullClean(input);
            Assert.Equal(once, TextProcessingService.FullClean(once));
        }
    }

    [Fact]
    public void LongUnpairedDelimiters_DoNotCauseRepeatedParagraphScans()
    {
        var input = string.Concat(Enumerable.Repeat("*word ", 30000));
        var watch = System.Diagnostics.Stopwatch.StartNew();
        Assert.Equal(input.TrimEnd(), TextProcessingService.FullClean(input));
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ManyUrls_StayUnchanged()
    {
        var input = string.Join("\n", Enumerable.Range(0, 5000).Select(i => $"https://example.org/a_b/{i}...x"));
        Assert.Equal(input, TextProcessingService.FullClean(input));
    }
}
