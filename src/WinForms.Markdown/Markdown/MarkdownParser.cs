using System.Text;

namespace WinForms.Markdown.Markdown;

/// <summary>
/// A small, dependency-free Markdown parser. It converts raw Markdown text into
/// a lightweight AST (<see cref="MarkdownDocument"/>) covering the block and
/// inline elements the control understands: headings, paragraphs, bold, italic,
/// underline and strikethrough. It intentionally does not know anything about
/// rendering or GDI+.
/// </summary>
public static class MarkdownParser
{
    // Order matters: longer / more specific delimiters must be matched before
    // their single-character variants (e.g. "~~" before "~", "__" before "_").
    private static readonly (string Open, string Close, InlineStyle Style)[] InlineMarkers =
    {
        ("~~", "~~", InlineStyle.Strikethrough),
        ("**", "**", InlineStyle.Bold),
        ("__", "__", InlineStyle.Bold),
        ("<u>", "</u>", InlineStyle.Underline),
        ("~", "~", InlineStyle.Underline),
        ("*", "*", InlineStyle.Italic),
        ("_", "_", InlineStyle.Italic),
    };

    /// <summary>Parses raw Markdown text into a document AST.</summary>
    public static MarkdownDocument Parse(string? markdown)
    {
        if (string.IsNullOrEmpty(markdown))
        {
            return MarkdownDocument.Empty;
        }

        // Normalise line endings so the rest of the parser only ever sees '\n'.
        // The null-forgiving operator is required on net48, which lacks the
        // NotNullWhen annotation on string.IsNullOrEmpty.
        string normalized = markdown!.Replace("\r\n", "\n").Replace('\r', '\n');

        var blocks = new List<Block>();
        var paragraphLines = new List<string>();

        void FlushParagraph()
        {
            if (paragraphLines.Count == 0)
            {
                return;
            }

            // Paragraph lines are joined with a newline, which the renderer
            // treats as an explicit (soft) line break.
            string joined = string.Join("\n", paragraphLines);
            var inlines = ParseInlines(joined, 0, joined.Length);
            blocks.Add(new ParagraphBlock(inlines));

            paragraphLines.Clear();
        }

        string[] lines = normalized.Split('\n');
        foreach (string rawLine in lines)
        {
            string line = rawLine.TrimEnd();

            if (TryParseHeading(line, out int level, out string headingText))
            {
                FlushParagraph();
                var inlines = new List<Inline>();
                inlines.AddRange(ParseInlines(headingText, 0, headingText.Length));
                blocks.Add(new HeadingBlock(level, inlines));
                continue;
            }

            if (line.Length == 0)
            {
                FlushParagraph();
                continue;
            }

            paragraphLines.Add(line);
        }

        FlushParagraph();
        return blocks.Count == 0 ? MarkdownDocument.Empty : new MarkdownDocument(blocks);
    }

    private static bool TryParseHeading(string line, out int level, out string text)
    {
        level = 0;
        text = string.Empty;

        int i = 0;
        while (i < line.Length && line[i] == '#' && i < 6)
        {
            i++;
        }

        // A heading requires the hashes to be followed by a space (or end of line).
        if (i == 0 || i == line.Length)
        {
            return false;
        }

        if (line[i] != ' ')
        {
            return false;
        }

        level = i;
        text = line[(i + 1)..].Trim();
        return true;
    }

    /// <summary>Parses inline formatting within a block of text.</summary>
    private static List<Inline> ParseInlines(string text, int start, int end)
    {
        var result = new List<Inline>();
        var buffer = new StringBuilder();
        int i = start;

        while (i < end)
        {
            bool matched = false;

            foreach ((string open, string close, InlineStyle style) in InlineMarkers)
            {
                if (!Matches(text, i, end, open))
                {
                    continue;
                }

                int closeIndex = FindClose(text, i + open.Length, end, open, close);
                if (closeIndex < 0)
                {
                    continue;
                }

                if (buffer.Length > 0)
                {
                    result.Add(new TextInline(buffer.ToString()));
                    buffer.Clear();
                }

                var children = ParseInlines(text, i + open.Length, closeIndex);
                result.Add(new FormattedInline(style, children));

                i = closeIndex + close.Length;
                matched = true;
                break;
            }

            if (!matched)
            {
                buffer.Append(text[i]);
                i++;
            }
        }

        if (buffer.Length > 0)
        {
            result.Add(new TextInline(buffer.ToString()));
        }

        return result;
    }

    private static bool Matches(string text, int index, int end, string token)
    {
        if (index + token.Length > end)
        {
            return false;
        }

        for (int i = 0; i < token.Length; i++)
        {
            if (text[index + i] != token[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Finds the closing delimiter for an already-matched opening delimiter.
    /// Repeated-character delimiters (<c>*</c>, <c>_</c>, <c>~</c>) are matched as
    /// whole runs so that a short delimiter never consumes part of a longer one.
    /// This allows, for example, <c>**bold *italic***</c> to close the bold at the
    /// trailing <c>**</c> and leave the leading <c>*</c> for the inner italic.
    /// </summary>
    private static int FindClose(string text, int from, int end, string open, string close)
    {
        // Delimiters with distinct open/close markers (the HTML underline tags)
        // are unambiguous: find the literal closing marker.
        if (open.Length != close.Length)
        {
            return text.IndexOf(close, from, end - from, StringComparison.Ordinal);
        }

        char c = close[0];
        int length = close.Length;
        int i = from;

        while (i < end)
        {
            if (text[i] != c)
            {
                i++;
                continue;
            }

            int runStart = i;
            int runLength = 0;
            while (i < end && text[i] == c)
            {
                i++;
                runLength++;
            }

            if (length == 1)
            {
                // A run of two is a nested two-character delimiter: skip it.
                if (runLength == 1 || runLength == 3)
                {
                    return runStart;
                }
            }
            else
            {
                // A run of one is a nested single-character delimiter: skip it.
                if (runLength == 2)
                {
                    return runStart;
                }

                if (runLength == 3)
                {
                    return runStart + 1;
                }
            }
        }

        return -1;
    }
}
