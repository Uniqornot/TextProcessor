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

    private static readonly Regex ThreePlusNewlinesRegex = new(
        @"\n{3,}",
        RegexOptions.Compiled);

    // Диапазоны BMP + суррогатные пары (U+1F300–U+1FAFF) без \p{Extended_Pictographic}
    private static readonly Regex EmojiRegex = new(
        @"[\u2700-\u27BF\u2600-\u26FF\uFE0F]|[\uD83C-\uD83E][\uDC00-\uDFFF]",
        RegexOptions.Compiled);

    private static readonly Regex FormatCharsRegex = new(
        @"\p{Cf}",
        RegexOptions.Compiled);

    private static readonly Regex UnicodeSpacesRegex = new(
        @"[\u2000-\u200A]",
        RegexOptions.Compiled);

    private static readonly Regex HorizontalRuleRegex = new(
        @"(?m)^\s*[-*_]{3,}\s*$",
        RegexOptions.Compiled);

    private static readonly Regex MarkdownHeaderStripRegex = new(
        @"(?m)^#{1,6}\s+",
        RegexOptions.Compiled);

    private static readonly Regex MarkdownBoldRegex = new(
        @"\*\*(.+?)\*\*",
        RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex MarkdownUnderlineRegex = new(
        @"__(.+?)__",
        RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex MarkdownItalicStarRegex = new(
        @"(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)",
        RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex MarkdownItalicUnderscoreRegex = new(
        @"(?<!_)_(?!_)(.+?)(?<!_)_(?!_)",
        RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex BulletListRegex = new(
        @"(?m)^[\-\*•]\s+",
        RegexOptions.Compiled);

    private static readonly Regex NumberedListRegex = new(
        @"(?m)^\s*\d+[.)]\s+",
        RegexOptions.Compiled);

    private static readonly Regex AiColonRegex = new(
        @"(?m)^([\p{L}][\p{L}\s]{0,40}):\s+(?=\S)",
        RegexOptions.Compiled);

    private static readonly Regex EllipsisRegex = new(
        @"\.{3,}",
        RegexOptions.Compiled);

    private static readonly Regex DashGluedRegex = new(
        @"([^\s\-])([\u2013\u2014])([^\s\-])",
        RegexOptions.Compiled);

    private static readonly Regex DashWithSpacesRegex = new(
        @"\s*[\u2013\u2014]\s*",
        RegexOptions.Compiled);

    private static readonly Regex MultiHyphenRegex = new(
        @"\s*-{2,}\s*",
        RegexOptions.Compiled);

    private static readonly Regex SpacedHyphenRegex = new(
        @"(?<=\s)-(?=\s)",
        RegexOptions.Compiled);

    private static readonly Regex SpacedEmDashRegex = new(
        @"\s—\s",
        RegexOptions.Compiled);

    private static readonly Regex StraightQuotesRegex = new(
        @"""([^""]+)""",
        RegexOptions.Compiled);

    /// <summary>
    /// Только невидимые Unicode-символы — без Markdown, эмодзи и правил ИИ.
    /// Используется внутри fullClean.
    /// </summary>
    private static string StripInvisibleChars(string input)
    {
        var result = input;
        result = result.Replace("\uFEFF", string.Empty);
        result = result.Replace("\u200B", " ");
        result = result.Replace("\u200C", " ");
        result = result.Replace("\u200D", " ");
        result = result.Replace("\u2060", string.Empty);
        result = result.Replace("\u00AD", string.Empty);
        result = result.Replace('\u00A0', ' ');
        result = result.Replace('\u202F', ' ');
        result = UnicodeSpacesRegex.Replace(result, " ");
        result = FormatCharsRegex.Replace(result, string.Empty);
        return result;
    }

    /// <summary>
    /// Удаление скрытых символов, Markdown-разметки и эмодзи.
    /// </summary>
    public static string RemoveHiddenChars(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var result = input;

        // 1. Невидимые Unicode-символы
        result = result.Replace("\uFEFF", string.Empty);
        result = result.Replace("\u200B", " ");
        result = result.Replace("\u200C", " ");
        result = result.Replace("\u200D", " ");
        result = result.Replace("\u2060", string.Empty);
        result = result.Replace("\u00AD", string.Empty);
        result = result.Replace("\u00A0", " ");
        result = result.Replace("\u202F", " ");
        result = Regex.Replace(result, @"[\u2000-\u200A]", " ");
        result = Regex.Replace(result, @"\p{Cf}", string.Empty);

        // 2. Горизонтальные разделители Markdown (---, ***, ___)
        result = Regex.Replace(result, @"(?m)^\s*[-*_]{3,}\s*$", string.Empty);

        // 3. Заголовки Markdown (# ## ### ...)
        result = Regex.Replace(result, @"(?m)^#{1,6}\s+", string.Empty);

        // 4. Нумерованные списки (1. или 1) в начале строки)
        result = Regex.Replace(result, @"(?m)^\s*\d+[.)]\s+", string.Empty);

        // 5. Маркеры списков (- * • ...)
        result = Regex.Replace(result, @"(?m)^\s*[-*•▪◦‣]\s+", string.Empty);

        // 6. Жирный: **text** и __text__
        result = Regex.Replace(result, @"\*\*(.+?)\*\*", "$1", RegexOptions.Singleline);
        result = Regex.Replace(result, @"__(.+?)__", "$1", RegexOptions.Singleline);

        // 7. Курсив: *text* и _text_
        result = Regex.Replace(result, @"(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)", "$1", RegexOptions.Singleline);
        result = Regex.Replace(result, @"(?<!_)_(?!_)(.+?)(?<!_)_(?!_)", "$1", RegexOptions.Singleline);

        // 8. Escape-слэши Markdown (\* \_ \- ...)
        result = Regex.Replace(result, @"\\([*_\-.!#\[\]()`>+])", "$1");

        // 9. ИИ-паттерн «Метка: текст» в начале строки (только буквы в метке, без цифр)
        result = Regex.Replace(result, @"(?m)^([\p{L}][\p{L}\s]{0,40}):\s+(?=\S)", "$1 - ");

        // 10. Удаление эмодзи
        result = Regex.Replace(result, @"[\u2700-\u27BF\u2600-\u26FF\u2300-\u23FF\u2B00-\u2BFF\uFE0F]", string.Empty);
        result = Regex.Replace(result, @"[\uD83C-\uDBFF][\uDC00-\uDFFF]", string.Empty);

        // 11. Нормализация пробелов после удалений
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
    /// Умная нормализация кавычек, тире и многоточий.
    /// </summary>
    public static string SmartNormalize(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        var result = input;

        result = StraightQuotesRegex.Replace(result, "«$1»");
        result = EllipsisRegex.Replace(result, "…");

        // Нормализация тире (4 шага, без повторного срабатывания)
        result = DashGluedRegex.Replace(result, "$1 — $3");
        result = DashWithSpacesRegex.Replace(result, " — ");
        result = MultiHyphenRegex.Replace(result, " — ");
        result = SpacedHyphenRegex.Replace(result, "—");
        result = SpacedEmDashRegex.Replace(result, " — ");

        result = result.Replace('`', '\'').Replace('´', '\'');

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
        result = result.Replace('«', '"');
        result = result.Replace('»', '"');
        result = result.Replace('\u2018', '\'');
        result = result.Replace('\u2019', '\'');
        result = result.Replace('\u201C', '"');
        result = result.Replace('\u201D', '"');
        return result;
    }

    private static readonly Regex EmDashSpacesRegex = new(
        @"\s*\u2014\s*",
        RegexOptions.Compiled);

    private static readonly Regex EnDashSpacesRegex = new(
        @"\s*\u2013\s*",
        RegexOptions.Compiled);

    /// <summary>
    /// Обратная нормализация тире: em/en-dash и прочие варианты → дефис-минус.
    /// </summary>
    public static string DeNormalizeDashes(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        var result = input;

        result = EmDashSpacesRegex.Replace(result, " - ");
        result = EnDashSpacesRegex.Replace(result, " - ");
        result = result.Replace("\u2015", "-");
        result = result.Replace("\u2012", "-");
        result = result.Replace("--", "-");
        result = MultipleSpacesRegex.Replace(result, " ");

        return result.Trim();
    }

    /// <summary>
    /// Полная очистка: скрытые символы, Markdown и нормализация пробелов/абзацев.
    /// </summary>
    public static string FullClean(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        var result = RemoveHiddenChars(input);
        result = NormalizeLineEndings(result);

        var processedLines = result.Split('\n')
            .Select(line => MultipleSpacesRegex.Replace(line, " ").Trim());
        result = string.Join('\n', processedLines);

        result = ThreePlusNewlinesRegex.Replace(result, "\n\n");
        return result.Trim();
    }

    /// <summary>
    /// Подсчёт символов.
    /// </summary>
    public static int CountCharacters(string text) => text.Length;

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
            : ParagraphSplitRegex.Split(text.Trim())
                .Count(p => !string.IsNullOrWhiteSpace(p));

    private static string NormalizeLineEndings(string text) =>
        text.Replace("\r\n", "\n").Replace('\r', '\n');

    private static string CollapseSpacesInLines(string text)
    {
        var lines = NormalizeLineEndings(text).Split('\n');
        for (var i = 0; i < lines.Length; i++)
            lines[i] = MultipleSpacesRegex.Replace(lines[i], " ");

        return string.Join('\n', lines);
    }
}
