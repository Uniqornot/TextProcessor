using System.Text;
using System.Text.RegularExpressions;
using System.Linq;

namespace TextProcessor.Services;

/// <summary>
/// Сервис обработки текста — очистка, нормализация и подсчёт статистики.
/// </summary>
public static class TextProcessingService
{
    private static readonly Regex WordRegex = new(
        @"[\p{L}\p{N}]+(?:[-'][\p{L}\p{N}]+)*",
        RegexOptions.Compiled);

    private static readonly Regex ParagraphSplitRegex = new(
        @"\n\s*\n",
        RegexOptions.Compiled);

    private static readonly Regex MultipleSpacesRegex = new(
        @"[ \t]+",
        RegexOptions.Compiled);

    private static readonly Regex ThreePlusEmptyLinesRegex = new(
        @"(\r?\n\s*){3,}",
        RegexOptions.Compiled);

    private static readonly Regex MarkdownBlockquoteRegex = new(
        @"(?m)^[ \t]*>[ \t]+",
        RegexOptions.Compiled);

    private static readonly Regex EllipsisRegex = new(
        @"\.{3,}", RegexOptions.Compiled);

    // Explicit typography allowlist: preserve mathematical minus and ASCII options.
    private static readonly Regex TypographyDashRegex = new(
        @"[ \t]*[\u2012-\u2015\u2E3A\u2E3B\uFE31\uFE32\uFE58][ \t]*",
        RegexOptions.Compiled);

    /// <summary>Удаление скрытых символов и Markdown-разметки с сохранением содержания.</summary>
    public static string RemoveHiddenChars(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        var result = NormalizeLineEndings(input);

        // 1. Невидимые Unicode-символы
        result = result.Replace("\uFEFF", string.Empty);
        result = result.Replace("\u200B", " ");
        result = result.Replace("\u2060", string.Empty);
        result = result.Replace("\u00AD", string.Empty);
        result = result.Replace("\u00A0", " ");
        result = result.Replace("\u202F", " ");
        result = Regex.Replace(result, @"[\u2000-\u200A]", " ");
        result = Regex.Replace(result, @"[\p{Cf}-[\u200C\u200D]]", string.Empty);

        // 2. Горизонтальные разделители Markdown (---, ***, ___)
        result = Regex.Replace(result, @"(?m)^[ \t]*[-*_]{3,}[ \t]*$", string.Empty);

        // 3. Заголовки Markdown (# ## ### ...), в т.ч. с ведущими пробелами
        result = Regex.Replace(result, @"(?m)^[ \t]*#{1,6}[ \t]+", string.Empty);

        // 4. Цитаты Markdown (> текст)
        result = MarkdownBlockquoteRegex.Replace(result, string.Empty);

        // Нумерация списков сохраняется как часть содержания.

        // 6. Маркеры списков (- * • ...)
        result = Regex.Replace(result, @"(?m)^[ \t]*[-*•▪◦‣][ \t]+", string.Empty);

        // 7. Жирный: **text** и __text__
        result = Regex.Replace(result, @"\*\*(.+?)\*\*", "$1", RegexOptions.Singleline);
        result = Regex.Replace(result, @"(?<![\w/])__([^\r\n]+?)__(?![\w/])", "$1");

        // 8. Курсив: *text* и _text_
        result = Regex.Replace(result, @"(?<![\w*])\*(?!\*)([^\r\n]+?)(?<!\*)\*(?![\w*])", "$1", RegexOptions.Singleline);
        result = Regex.Replace(result, @"(?<![\w/])_(?!_)([^\r\n]+?)(?<!_)_(?![\w/])", "$1", RegexOptions.Singleline);

        // 9. Escape-слэши Markdown (\* \_ \- ...)
        result = Regex.Replace(result, @"\\([*_\-.!#\[\]()`>+])", "$1");

        // Preserve colons, emoji and supplementary Unicode (letters, music, math).

        // 12. Нормализация пробелов после удалений
        result = string.Join("\n",
            result.Split('\n')
                .Select(line => Regex.Replace(line, @"[ \t]{2,}", " ").TrimEnd()));
        result = Regex.Replace(result, @"\n{3,}", "\n\n");
        result = result.Trim();

        return result;
    }

    /// <summary>
    /// Удаление лишних пробелов и табуляций в каждой строке.
    /// </summary>
    public static string RemoveExtraSpaces(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        var lines = NormalizeLineEndings(input).Split('\n');
        var sb = new StringBuilder();

        for (var i = 0; i < lines.Length; i++)
        {
            var line = MultipleSpacesRegex.Replace(lines[i].TrimEnd(), " ").TrimStart();
            sb.Append(line);
            if (i < lines.Length - 1)
                sb.Append('\n');
        }

        return sb.ToString().Trim();
    }

    /// <summary>
    /// Преобразование текста в одну строку.
    /// </summary>
    public static string ToSingleLine(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        var result = input.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');
        result = MultipleSpacesRegex.Replace(result, " ");
        return result.Trim();
    }

    /// <summary>
    /// Исправление абзацев и переносов строк.
    /// </summary>
    public static string FixParagraphs(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        var result = NormalizeLineEndings(input);

        var lines = result.Split('\n');
        for (var i = 0; i < lines.Length; i++)
            lines[i] = lines[i].TrimEnd().TrimStart();

        result = string.Join('\n', lines);
        result = ThreePlusEmptyLinesRegex.Replace(result, "\n\n");
        return result.Trim();
    }

    /// <summary>
    /// Умная нормализация как в AndroidTextCleaner:
    /// типографские кавычки и тире → обычные символы, многоточие → один знак.
    /// </summary>
    public static string SmartNormalize(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        var result = input;

        result = DenormalizeQuotes(result);
        result = EllipsisRegex.Replace(result, "…");

        result = DeNormalizeDashes(result);

        return result.Trim();
    }

    /// <summary>
    /// Обратная нормализация кавычек: «ёлочки» и типографские → прямые.
    /// </summary>
    public static string DenormalizeQuotes(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        var result = input;
        // Основные двойные типографские кавычки: русские «ёлочки»,
        // английские “лапки”, немецкие „лапки“ и одинарные угловые.
        foreach (var quote in new[] { '«', '»', '\u201C', '\u201D', '\u201E', '\u201F', '‹', '›', '〝', '〞', '〟', '＂', '「', '」', '『', '』', '❝', '❞' })
            result = result.Replace(quote, '"');

        // Apostrophes inside words stay apostrophes; standalone single quotes are quotation marks.
        result = Regex.Replace(result, @"[‘’‚‛]", match =>
            match.Index > 0 && match.Index + 1 < result.Length &&
            char.IsLetterOrDigit(result[match.Index - 1]) &&
            char.IsLetterOrDigit(result[match.Index + 1]) ? "'" : "\"");
        return result;
    }

    /// <summary>Приведение типографских тире к ASCII-дефису.</summary>
    public static string DeNormalizeDashes(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;
        return TypographyDashRegex.Replace(input, " - ").Trim();
    }

    /// <summary>
    /// Полная очистка с тем же конвейером, что и в AndroidTextCleaner:
    /// скрытые символы/Markdown → абзацы → типографика → пробелы.
    /// </summary>
    public static string FullClean(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        // URLs are opaque content: punctuation and Markdown rules must not rewrite them.
        var urls = new List<string>();
        var prefix = "URL" + Guid.NewGuid().ToString("N") + "X";
        var protectedInput = Regex.Replace(input, @"(?:https?://|www\.)[^\s<>""«»“”]+", match =>
        {
            urls.Add(match.Value);
            return prefix + (urls.Count - 1) + "Z";
        });
        var result = RemoveHiddenChars(protectedInput);
        result = FixParagraphs(result);
        result = SmartNormalize(result);
        result = RemoveExtraSpaces(result);
        for (var i = 0; i < urls.Count; i++)
            result = result.Replace(prefix + i + "Z", urls[i]);
        return result;
    }

    /// <summary>
    /// Подсчёт символов.
    /// </summary>
    public static int CountCharacters(string? text) => text?.Length ?? 0;

    /// <summary>
    /// Подсчёт слов (поддержка кириллицы через Unicode-категории).
    /// </summary>
    public static int CountWords(string text) =>
        string.IsNullOrWhiteSpace(text) ? 0 : WordRegex.Matches(text).Count;

    /// <summary>
    /// Подсчёт строк.
    /// </summary>
    public static int CountLines(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 1;

        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        return normalized.Split('\n').Length;
    }

    /// <summary>
    /// Подсчёт абзацев.
    /// </summary>
    public static int CountParagraphs(string text) =>
        string.IsNullOrWhiteSpace(text)
            ? 0
            : ParagraphSplitRegex.Split(NormalizeLineEndings(text).Trim())
                .Count(p => !string.IsNullOrWhiteSpace(p));

    private static string NormalizeLineEndings(string text) =>
        text.Replace("\r\n", "\n").Replace('\r', '\n');

}
