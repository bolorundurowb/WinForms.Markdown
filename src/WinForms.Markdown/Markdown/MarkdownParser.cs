namespace WinForms.Markdown.Markdown;

/// <summary>
/// A small, dependency-free Markdown parser. It converts raw Markdown text into a
/// lightweight AST (<see cref="MarkdownDocument"/>) covering headings (ATX and setext),
/// paragraphs, lists (nested, ordered and unordered), block quotes, fenced code blocks,
/// horizontal rules, and the inline elements bold, italic, underline, strikethrough, code
/// spans, hyperlinks, escapes and line breaks. It intentionally knows nothing about
/// rendering or GDI+.
/// </summary>
/// <remarks>
/// Not supported: tables, raw HTML (other than <c>&lt;u&gt;</c> and <c>&lt;br&gt;</c>),
/// reference-style links and indented code blocks. Images are parsed as links.
/// </remarks>
public static class MarkdownParser
{
    // Containers (block quotes, list items) recurse; cap the depth so hostile input
    // such as thousands of '>' characters cannot overflow the stack.
    private const int MaxDepth = 32;

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
        var normalized = markdown!.Replace("\r\n", "\n").Replace('\r', '\n');

        var lines = normalized.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            lines[i] = ExpandLeadingTabs(lines[i]);
        }

        var blocks = ParseBlocks(lines, 0);
        return blocks.Count == 0 ? MarkdownDocument.Empty : new MarkdownDocument(blocks);
    }

    // ------------------------------------------------------------------
    // Block structure
    // ------------------------------------------------------------------

    private static List<Block> ParseBlocks(IReadOnlyList<string> lines, int depth)
    {
        var blocks = new List<Block>();
        var paragraph = new List<string>();

        void FlushParagraph()
        {
            if (paragraph.Count == 0)
            {
                return;
            }

            // Trailing spaces on inner lines are kept: two of them make a hard break.
            var last = paragraph.Count - 1;
            paragraph[last] = paragraph[last].TrimEnd();

            var inlines = InlineParser.Parse(string.Join("\n", paragraph));
            paragraph.Clear();

            if (inlines.Count > 0)
            {
                blocks.Add(new ParagraphBlock(inlines));
            }
        }

        var i = 0;
        while (i < lines.Count)
        {
            var line = lines[i];

            if (IsBlank(line))
            {
                FlushParagraph();
                i++;
                continue;
            }

            if (TryParseFence(line, out var fenceChar, out var fenceLength, out var fenceIndent, out var language))
            {
                FlushParagraph();

                var code = new List<string>();
                i++;
                while (i < lines.Count)
                {
                    if (IsClosingFence(lines[i], fenceChar, fenceLength))
                    {
                        i++;
                        break;
                    }

                    code.Add(RemoveIndent(lines[i], fenceIndent));
                    i++;
                }

                blocks.Add(new CodeBlock(code, language));
                continue;
            }

            if (TryParseAtxHeading(line, out var level, out var headingText))
            {
                FlushParagraph();
                blocks.Add(new HeadingBlock(level, InlineParser.Parse(headingText)));
                i++;
                continue;
            }

            if (paragraph.Count > 0 && IsSetextUnderline(line, out var setextLevel))
            {
                var text = string.Join("\n", paragraph).TrimEnd();
                paragraph.Clear();
                blocks.Add(new HeadingBlock(setextLevel, InlineParser.Parse(text)));
                i++;
                continue;
            }

            if (IsThematicBreak(line))
            {
                FlushParagraph();
                blocks.Add(new ThematicBreakBlock());
                i++;
                continue;
            }

            if (IsBlockQuoteStart(line))
            {
                FlushParagraph();
                i = ParseBlockQuote(lines, i, depth, blocks);
                continue;
            }

            if (TryParseListMarker(line, out var marker) && (paragraph.Count == 0 || CanInterruptParagraph(marker!)))
            {
                FlushParagraph();
                i = ParseList(lines, i, marker!, depth, blocks);
                continue;
            }

            paragraph.Add(line.TrimStart());
            i++;
        }

        FlushParagraph();
        return blocks;
    }

    private static int ParseBlockQuote(IReadOnlyList<string> lines, int start, int depth, List<Block> output)
    {
        var inner = new List<string>();
        var i = start;

        while (i < lines.Count)
        {
            var line = lines[i];

            if (TryStripQuoteMarker(line, out var rest))
            {
                inner.Add(rest);
                i++;
                continue;
            }

            // Lazy continuation: an unmarked line continues a quoted paragraph.
            if (!IsBlank(line) && LastIsParagraphText(inner) && !StartsInterruptingBlock(line))
            {
                inner.Add(line.TrimStart());
                i++;
                continue;
            }

            break;
        }

        output.Add(new BlockQuoteBlock(depth >= MaxDepth ? Fallback(inner) : ParseBlocks(inner, depth + 1)));
        return i;
    }

    private static int ParseList(IReadOnlyList<string> lines, int start, ListMarker first, int depth, List<Block> output)
    {
        var items = new List<ListItem>();
        var loose = false;
        var i = start;

        while (i < lines.Count
            && TryParseListMarker(lines[i], out var parsed)
            && SameList(first, parsed!)
            && !IsThematicBreak(lines[i]))
        {
            var marker = parsed!;
            var itemLines = new List<string>();
            var firstLine = lines[i];
            itemLines.Add(marker.ContentOffset < firstLine.Length ? firstLine.Substring(marker.ContentOffset) : string.Empty);
            i++;

            var sawBlank = false;
            while (i < lines.Count)
            {
                var line = lines[i];

                if (IsBlank(line))
                {
                    sawBlank = true;
                    itemLines.Add(string.Empty);
                    i++;
                    continue;
                }

                if (LeadingSpaces(line) >= marker.ContentOffset)
                {
                    sawBlank = false;
                    itemLines.Add(line.Substring(marker.ContentOffset));
                    i++;
                    continue;
                }

                // Lazy continuation of the item's last paragraph line.
                if (!sawBlank
                    && LastIsParagraphText(itemLines)
                    && !StartsInterruptingBlock(line)
                    && !TryParseListMarker(line, out _))
                {
                    itemLines.Add(line.TrimStart());
                    i++;
                    continue;
                }

                break;
            }

            var trailingBlanks = 0;
            while (itemLines.Count > 0 && IsBlank(itemLines[itemLines.Count - 1]))
            {
                itemLines.RemoveAt(itemLines.Count - 1);
                trailingBlanks++;
            }

            if (trailingBlanks > 0
                && i < lines.Count
                && TryParseListMarker(lines[i], out var nextMarker)
                && SameList(first, nextMarker!)
                && !IsThematicBreak(lines[i]))
            {
                loose = true;
            }

            var blocks = depth >= MaxDepth ? Fallback(itemLines) : ParseBlocks(itemLines, depth + 1);
            items.Add(new ListItem(blocks));
        }

        output.Add(new ListBlock(first.IsOrdered, first.Number, !loose, items));
        return i;
    }

    private static List<Block> Fallback(IReadOnlyList<string> lines)
    {
        var trimmed = new List<string>(lines.Count);
        foreach (var line in lines)
        {
            trimmed.Add(line.TrimStart());
        }

        var inlines = InlineParser.Parse(string.Join("\n", trimmed).Trim());
        var result = new List<Block>();
        if (inlines.Count > 0)
        {
            result.Add(new ParagraphBlock(inlines));
        }

        return result;
    }

    // ------------------------------------------------------------------
    // Line classification
    // ------------------------------------------------------------------

    private sealed class ListMarker
    {
        public bool IsOrdered;
        public int Number;
        public char Delimiter;
        public int ContentOffset;
        public bool IsEmpty;
    }

    private static bool SameList(ListMarker a, ListMarker b)
        => a.IsOrdered == b.IsOrdered && a.Delimiter == b.Delimiter;

    private static bool CanInterruptParagraph(ListMarker marker)
        => !marker.IsEmpty && (!marker.IsOrdered || marker.Number == 1);

    private static bool TryParseListMarker(string line, out ListMarker? marker)
    {
        marker = null;

        var indent = LeadingSpaces(line);
        if (indent > 3 || indent >= line.Length)
        {
            return false;
        }

        var pos = indent;
        var isOrdered = false;
        var number = 0;
        char delimiter;

        var c = line[pos];
        if (c == '-' || c == '+' || c == '*')
        {
            delimiter = c;
            pos++;
        }
        else if (c >= '0' && c <= '9')
        {
            var digitsStart = pos;
            while (pos < line.Length && line[pos] >= '0' && line[pos] <= '9')
            {
                pos++;
            }

            if (pos - digitsStart > 9 || pos >= line.Length || (line[pos] != '.' && line[pos] != ')'))
            {
                return false;
            }

            number = int.Parse(line.Substring(digitsStart, pos - digitsStart), System.Globalization.CultureInfo.InvariantCulture);
            isOrdered = true;
            delimiter = line[pos];
            pos++;
        }
        else
        {
            return false;
        }

        int contentOffset;
        bool isEmpty;

        if (pos >= line.Length)
        {
            contentOffset = pos + 1;
            isEmpty = true;
        }
        else if (line[pos] != ' ')
        {
            return false;
        }
        else
        {
            var spaces = 0;
            while (pos + spaces < line.Length && line[pos + spaces] == ' ')
            {
                spaces++;
            }

            if (pos + spaces >= line.Length)
            {
                contentOffset = pos + 1;
                isEmpty = true;
            }
            else
            {
                // Five or more spaces would start an (unsupported) indented code block.
                contentOffset = spaces >= 5 ? pos + 1 : pos + spaces;
                isEmpty = false;
            }
        }

        marker = new ListMarker
        {
            IsOrdered = isOrdered,
            Number = number,
            Delimiter = delimiter,
            ContentOffset = contentOffset,
            IsEmpty = isEmpty,
        };
        return true;
    }

    private static bool TryParseFence(string line, out char fenceChar, out int length, out int indent, out string? language)
    {
        fenceChar = '\0';
        length = 0;
        language = null;
        indent = LeadingSpaces(line);

        if (indent > 3 || indent >= line.Length)
        {
            return false;
        }

        var c = line[indent];
        if (c != '`' && c != '~')
        {
            return false;
        }

        var pos = indent;
        while (pos < line.Length && line[pos] == c)
        {
            pos++;
        }

        var run = pos - indent;
        if (run < 3)
        {
            return false;
        }

        var info = line.Substring(pos).Trim();
        if (c == '`' && info.IndexOf('`') >= 0)
        {
            return false;
        }

        if (info.Length > 0)
        {
            var space = info.IndexOfAny(new[] { ' ', '\t' });
            language = space > 0 ? info.Substring(0, space) : info;
        }

        fenceChar = c;
        length = run;
        return true;
    }

    private static bool IsClosingFence(string line, char fenceChar, int fenceLength)
    {
        var indent = LeadingSpaces(line);
        if (indent > 3)
        {
            return false;
        }

        var pos = indent;
        while (pos < line.Length && line[pos] == fenceChar)
        {
            pos++;
        }

        if (pos - indent < fenceLength)
        {
            return false;
        }

        return IsBlank(line.Substring(pos));
    }

    private static bool TryParseAtxHeading(string line, out int level, out string text)
    {
        level = 0;
        text = string.Empty;

        var indent = LeadingSpaces(line);
        if (indent > 3)
        {
            return false;
        }

        var pos = indent;
        while (pos < line.Length && line[pos] == '#')
        {
            pos++;
        }

        var hashes = pos - indent;
        if (hashes < 1 || hashes > 6)
        {
            return false;
        }

        // The hashes must be followed by whitespace or the end of the line.
        if (pos < line.Length && line[pos] != ' ' && line[pos] != '\t')
        {
            return false;
        }

        var content = line.Substring(pos).Trim();

        // Strip an optional closing sequence of hashes ("# Title ##").
        var end = content.Length;
        var j = end;
        while (j > 0 && content[j - 1] == '#')
        {
            j--;
        }

        if (j < end && (j == 0 || content[j - 1] == ' ' || content[j - 1] == '\t'))
        {
            content = content.Substring(0, j).TrimEnd();
        }

        level = hashes;
        text = content;
        return true;
    }

    private static bool IsSetextUnderline(string line, out int level)
    {
        level = 0;
        if (LeadingSpaces(line) > 3)
        {
            return false;
        }

        var trimmed = line.Trim();
        if (trimmed.Length == 0)
        {
            return false;
        }

        var c = trimmed[0];
        if (c != '=' && c != '-')
        {
            return false;
        }

        foreach (var ch in trimmed)
        {
            if (ch != c)
            {
                return false;
            }
        }

        level = c == '=' ? 1 : 2;
        return true;
    }

    private static bool IsThematicBreak(string line)
    {
        var indent = LeadingSpaces(line);
        if (indent > 3)
        {
            return false;
        }

        var marker = '\0';
        var count = 0;
        for (var i = indent; i < line.Length; i++)
        {
            var c = line[i];
            if (c == ' ' || c == '\t')
            {
                continue;
            }

            if (c != '-' && c != '*' && c != '_')
            {
                return false;
            }

            if (marker == '\0')
            {
                marker = c;
            }
            else if (c != marker)
            {
                return false;
            }

            count++;
        }

        return count >= 3;
    }

    private static bool IsBlockQuoteStart(string line)
    {
        var indent = LeadingSpaces(line);
        return indent <= 3 && indent < line.Length && line[indent] == '>';
    }

    private static bool TryStripQuoteMarker(string line, out string rest)
    {
        rest = string.Empty;
        if (!IsBlockQuoteStart(line))
        {
            return false;
        }

        var pos = LeadingSpaces(line) + 1;
        if (pos < line.Length && (line[pos] == ' ' || line[pos] == '\t'))
        {
            pos++;
        }

        rest = line.Substring(pos);
        return true;
    }

    private static bool StartsInterruptingBlock(string line)
    {
        return TryParseFence(line, out _, out _, out _, out _)
            || TryParseAtxHeading(line, out _, out _)
            || IsThematicBreak(line)
            || IsBlockQuoteStart(line)
            || (TryParseListMarker(line, out var marker) && CanInterruptParagraph(marker!));
    }

    private static bool LastIsParagraphText(IReadOnlyList<string> lines)
    {
        if (lines.Count == 0)
        {
            return false;
        }

        var last = lines[lines.Count - 1];
        return !IsBlank(last)
            && !TryParseFence(last, out _, out _, out _, out _)
            && !TryParseAtxHeading(last, out _, out _)
            && !IsThematicBreak(last);
    }

    // ------------------------------------------------------------------
    // String helpers
    // ------------------------------------------------------------------

    private static bool IsBlank(string line) => string.IsNullOrWhiteSpace(line);

    private static int LeadingSpaces(string line)
    {
        var i = 0;
        while (i < line.Length && line[i] == ' ')
        {
            i++;
        }

        return i;
    }

    private static string RemoveIndent(string line, int maxIndent)
    {
        var remove = Math.Min(LeadingSpaces(line), maxIndent);
        return remove == 0 ? line : line.Substring(remove);
    }

    /// <summary>Converts leading tabs to spaces (tab stops of four) so indentation is uniform.</summary>
    private static string ExpandLeadingTabs(string line)
    {
        var i = 0;
        var column = 0;
        while (i < line.Length && (line[i] == ' ' || line[i] == '\t'))
        {
            column += line[i] == '\t' ? 4 - (column % 4) : 1;
            i++;
        }

        if (i == 0 || line.IndexOf('\t', 0, i) < 0)
        {
            return line;
        }

        return new string(' ', column) + line.Substring(i);
    }
}
