using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using System.Globalization;

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

    private static readonly Regex MarkdownPrefixRegex = new(
        @"(?m)^[ \t]*(?:(?:#{1,6}|>|[•▪◦‣*])[ \t]+)+",
        RegexOptions.Compiled);

    private static readonly Regex ProtectedContentRegex = new(
        @"(`+)[\s\S]*?\1|\\[*_\-.!#\[\]()`>+]|(?:https?://|ftp://|www\.)[^\s<>""«»“”„‟‹›〝〞〟＂「」『』❝❞‘’‚‛]+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex EllipsisRegex = new(
        @"\.{3,}", RegexOptions.Compiled);

    // Explicit typography allowlist: preserve mathematical minus and ASCII options.
    private static readonly Regex TypographyDashRegex = new(
        @"[ \t]*[\u2012-\u2015\u2E3A\u2E3B\uFE31\uFE32\uFE58][ \t]*",
        RegexOptions.Compiled);

    /// <summary>Удаление скрытых символов и Markdown-разметки с сохранением содержания.</summary>
    public static string RemoveHiddenChars(string input) =>
        TransformProtected(input, RemoveHiddenCharsCore);

    private static string RemoveHiddenCharsCore(string input)
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

        // Keep ASCII hyphens: a normalized dash at line start must survive a second clean.
        // Strip nested formatting to a fixed point so one and two cleanings agree.
        string previous;
        do
        {
            previous = result;
            result = Regex.Replace(result, @"(?m)^[ \t]*(?:-{3,}|\*{3,}|_{3,})[ \t]*$", string.Empty);
            result = MarkdownPrefixRegex.Replace(result, string.Empty);
            result = StripEmphasis(result);
        } while (result != previous);

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
    public static string SmartNormalize(string input) =>
        TransformProtected(input, SmartNormalizeCore);

    private static string SmartNormalizeCore(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        var result = input;

        result = DenormalizeQuotesCore(result);
        result = EllipsisRegex.Replace(result, "…");

        result = DeNormalizeDashesCore(result);

        return result.Trim();
    }

    /// <summary>
    /// Обратная нормализация кавычек: «ёлочки» и типографские → прямые.
    /// </summary>
    public static string DenormalizeQuotes(string input) =>
        TransformProtected(input, DenormalizeQuotesCore);

    private static string DenormalizeQuotesCore(string input)
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
            IsWordAt(result, PreviousScalarIndex(result, match.Index)) &&
            IsWordAt(result, match.Index + 1) ? "'" : "\"");
        return result;
    }

    /// <summary>Приведение типографских тире к ASCII-дефису.</summary>
    public static string DeNormalizeDashes(string input) =>
        TransformProtected(input, DeNormalizeDashesCore);

    private static string DeNormalizeDashesCore(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;
        return TypographyDashRegex.Replace(input, " - ").Trim();
    }

    /// <summary>
    /// Полная очистка с тем же конвейером, что и в AndroidTextCleaner:
    /// скрытые символы/Markdown → абзацы → типографика → пробелы.
    /// </summary>
    public static string FullClean(string input) =>
        TransformProtected(input, FullCleanCore);

    private static string FullCleanCore(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        var result = RemoveHiddenCharsCore(input);
        result = FixParagraphs(result);
        result = SmartNormalizeCore(result);
        return RemoveExtraSpaces(result);
    }

    // The tokens cannot collide with input. Restore them in one pass (not one scan per URL).
    private static string TransformProtected(string input, Func<string, string> transform)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        var prefix = "\uE000";
        while (input.Contains(prefix, StringComparison.Ordinal))
            prefix += "\uE000";
        const string suffix = "\uE001";
        var values = new List<string>();
        var protectedInput = ProtectedContentRegex.Replace(input, match =>
        {
            var value = match.Value;
            var closingMarkup = string.Empty;
            // A URL in **emphasis** must not consume its closing Markdown delimiter.
            if (!value.StartsWith('`') && !value.StartsWith('\\'))
            {
                foreach (var marker in new[] { "***", "___", "**", "__", "*", "_" })
                {
                    if (match.Index >= marker.Length &&
                        input.AsSpan(match.Index - marker.Length, marker.Length).SequenceEqual(marker) &&
                        value.EndsWith(marker, StringComparison.Ordinal))
                    {
                        value = value[..^marker.Length];
                        closingMarkup = marker;
                        break;
                    }
                }
            }
            values.Add(value);
            return prefix + (values.Count - 1).ToString(CultureInfo.InvariantCulture) + suffix + closingMarkup;
        });
        var result = transform(protectedInput);
        return Regex.Replace(result, Regex.Escape(prefix) + @"([0-9]+)" + suffix,
            match => values[int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)]);
    }

    private static int PreviousScalarIndex(string text, int index) =>
        index >= 2 && char.IsLowSurrogate(text[index - 1]) && char.IsHighSurrogate(text[index - 2])
            ? index - 2 : index - 1;

    private static bool IsWordAt(string text, int index) =>
        index >= 0 && index < text.Length && CharUnicodeInfo.GetUnicodeCategory(text, index) is
            UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or
            UnicodeCategory.TitlecaseLetter or UnicodeCategory.ModifierLetter or
            UnicodeCategory.OtherLetter or UnicodeCategory.DecimalDigitNumber or
            UnicodeCategory.LetterNumber or UnicodeCategory.OtherNumber or
            UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or
            UnicodeCategory.EnclosingMark;

    // Pair delimiter runs in a linear scan. Regex .+? across a whole paragraph could
    // delete arithmetic operators and repeatedly scan long, unmatched Markdown.
    private static string StripEmphasis(string input)
    {
        var removed = new bool[input.Length];
        var openers = new Stack<(int Index, int Length, char Marker)>();
        for (var i = 0; i < input.Length;)
        {
            if (input[i] is '\r' or '\n')
                openers.Clear();
            if (input[i] is not ('*' or '_'))
            {
                i++;
                continue;
            }
            var start = i;
            var marker = input[i];
            while (i < input.Length && input[i] == marker)
                i++;
            var length = i - start;
            if (length > 3)
                continue;
            var canClose = start > 0 && !char.IsWhiteSpace(input[start - 1]) &&
                (i == input.Length || (!IsWordAt(input, i) && input[i] != '_'));
            var remaining = length;
            if (canClose)
            {
                while (openers.TryPeek(out var opener) && opener.Marker == marker && opener.Length <= remaining)
                {
                    openers.Pop();
                    Array.Fill(removed, true, opener.Index, opener.Length);
                    Array.Fill(removed, true, start + length - remaining, opener.Length);
                    remaining -= opener.Length;
                }
            }
            var canOpen = i < input.Length && !char.IsWhiteSpace(input[i]) &&
                (start == 0 || (!IsWordAt(input, PreviousScalarIndex(input, start)) && input[start - 1] != '_'));
            if (remaining > 0 && canOpen)
                openers.Push((start + length - remaining, remaining, marker));
        }
        var result = new StringBuilder(input.Length);
        for (var i = 0; i < input.Length; i++)
            if (!removed[i])
                result.Append(input[i]);
        return result.ToString();
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
