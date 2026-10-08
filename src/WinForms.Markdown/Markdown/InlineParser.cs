using System.Text;

namespace WinForms.Markdown.Markdown;

/// <summary>
/// Parses inline Markdown (emphasis, code spans, links, escapes, entities and line
/// breaks) into <see cref="Inline"/> nodes.
/// </summary>
/// <remarks>
/// Emphasis uses the CommonMark delimiter-run approach: <c>*</c>, <c>_</c> and <c>~</c>
/// runs are classified as left/right flanking, then matched with a stack. This is what
/// stops <c>snake_case_names</c> or <c>2 * 3 * 4</c> from being treated as emphasis and
/// makes mixed runs such as <c>***both***</c> or <c>*a **b***</c> nest correctly.
/// </remarks>
internal static class InlineParser
{
    // Guards against stack overflows from pathological nesting such as [[[[...]()]()]().
    private const int MaxDepth = 32;

    private static readonly string[] BreakTags = { "<br>", "<br/>", "<br />" };

    public static List<Inline> Parse(string text) => Parse(text, 0);

    private static List<Inline> Parse(string text, int depth)
    {
        var buffer = new StringBuilder();
        var head = new Node();
        var tail = head;
        var n = text.Length;
        var i = 0;

        void Append(Node node)
        {
            node.Prev = tail;
            tail.Next = node;
            tail = node;
        }

        void FlushText()
        {
            if (buffer.Length == 0)
            {
                return;
            }

            Append(new Node { Inline = new TextInline(buffer.ToString()) });
            buffer.Clear();
        }

        while (i < n)
        {
            var c = text[i];

            if (c == '\\')
            {
                if (i + 1 < n)
                {
                    var next = text[i + 1];
                    if (next == '\n')
                    {
                        FlushText();
                        Append(new Node { Inline = new LineBreakInline(true) });
                        i += 2;
                        continue;
                    }

                    if (IsAsciiPunctuation(next))
                    {
                        buffer.Append(next);
                        i += 2;
                        continue;
                    }
                }

                buffer.Append(c);
                i++;
                continue;
            }

            if (c == '\n')
            {
                var hard = EndsWithTwoSpaces(buffer);
                TrimEndSpaces(buffer);
                FlushText();
                Append(new Node { Inline = new LineBreakInline(hard) });
                i++;
                continue;
            }

            if (c == '`')
            {
                var runStart = i;
                while (i < n && text[i] == '`')
                {
                    i++;
                }

                var runLength = i - runStart;
                var close = FindBacktickClose(text, i, runLength);
                if (close < 0)
                {
                    buffer.Append('`', runLength);
                    continue;
                }

                FlushText();
                Append(new Node { Inline = new CodeInline(NormalizeCodeSpan(text.Substring(i, close - i))) });
                i = close + runLength;
                continue;
            }

            if (c == '!' && i + 1 < n && text[i + 1] == '[' && TryParseLink(text, i + 1, depth, out var image, out var imageEnd))
            {
                FlushText();
                Append(new Node { Inline = image! });
                i = imageEnd;
                continue;
            }

            if (c == '[' && TryParseLink(text, i, depth, out var link, out var linkEnd))
            {
                FlushText();
                Append(new Node { Inline = link! });
                i = linkEnd;
                continue;
            }

            if (c == '<')
            {
                if (TryParseAutolink(text, i, out var autolink, out var autolinkEnd))
                {
                    FlushText();
                    Append(new Node { Inline = autolink! });
                    i = autolinkEnd;
                    continue;
                }

                if (TryParseUnderlineTag(text, i, depth, out var underline, out var underlineEnd))
                {
                    FlushText();
                    Append(new Node { Inline = underline! });
                    i = underlineEnd;
                    continue;
                }

                if (TryParseBreakTag(text, i, out var breakEnd))
                {
                    TrimEndSpaces(buffer);
                    FlushText();
                    Append(new Node { Inline = new LineBreakInline(true) });
                    i = breakEnd;
                    continue;
                }

                buffer.Append(c);
                i++;
                continue;
            }

            if (c == '&' && TryDecodeEntity(text, i, out var decoded, out var entityLength))
            {
                buffer.Append(decoded);
                i += entityLength;
                continue;
            }

            if (c == '*' || c == '_' || c == '~')
            {
                var runStart = i;
                while (i < n && text[i] == c)
                {
                    i++;
                }

                var runLength = i - runStart;

                // '~' is underline (1) or strikethrough (2); longer runs are literal.
                if (c == '~' && runLength > 2)
                {
                    buffer.Append(c, runLength);
                    continue;
                }

                var before = runStart > 0 ? text[runStart - 1] : '\n';
                var after = i < n ? text[i] : '\n';
                Classify(c, before, after, out var canOpen, out var canClose);

                if (!canOpen && !canClose)
                {
                    buffer.Append(c, runLength);
                    continue;
                }

                FlushText();
                Append(new Node
                {
                    IsDelimiter = true,
                    Char = c,
                    Count = runLength,
                    OriginalCount = runLength,
                    CanOpen = canOpen,
                    CanClose = canClose,
                });
                continue;
            }

            buffer.Append(c);
            i++;
        }

        FlushText();
        ProcessEmphasis(head);
        return ToInlines(head.Next, null);
    }

    // ------------------------------------------------------------------
    // Emphasis
    // ------------------------------------------------------------------

    private sealed class Node
    {
        public Inline? Inline;
        public bool IsDelimiter;
        public char Char;
        public int Count;
        public int OriginalCount;
        public bool CanOpen;
        public bool CanClose;
        public Node? Prev;
        public Node? Next;
    }

    private static void Classify(char c, char before, char after, out bool canOpen, out bool canClose)
    {
        var beforeSpace = char.IsWhiteSpace(before);
        var afterSpace = char.IsWhiteSpace(after);
        var beforePunct = IsPunctuation(before);
        var afterPunct = IsPunctuation(after);

        var leftFlanking = !afterSpace && (!afterPunct || beforeSpace || beforePunct);
        var rightFlanking = !beforeSpace && (!beforePunct || afterSpace || afterPunct);

        if (c == '_')
        {
            // Underscores may not open/close emphasis inside a word.
            canOpen = leftFlanking && (!rightFlanking || beforePunct);
            canClose = rightFlanking && (!leftFlanking || afterPunct);
        }
        else
        {
            canOpen = leftFlanking;
            canClose = rightFlanking;
        }
    }

    private static void ProcessEmphasis(Node head)
    {
        // For each (char, length-class, canOpen) remember the node before which a closer
        // already failed to find an opener, so repeated scans stay cheap.
        var bottoms = new Dictionary<(char, int, bool), Node>();
        var closer = head.Next;

        while (closer is not null)
        {
            if (!closer.IsDelimiter || !closer.CanClose || closer.Count <= 0)
            {
                closer = closer.Next;
                continue;
            }

            var key = (closer.Char, closer.Char == '~' ? closer.Count : closer.Count % 3, closer.CanOpen);
            var bottom = bottoms.TryGetValue(key, out var stored) ? stored : head;

            var opener = closer.Prev;
            while (opener is not null && opener != bottom && opener != head)
            {
                if (opener.IsDelimiter && opener.Count > 0 && opener.CanOpen && Compatible(opener, closer))
                {
                    break;
                }

                opener = opener.Prev;
            }

            if (opener is null || opener == bottom || opener == head)
            {
                bottoms[key] = closer.Prev ?? head;

                if (!closer.CanOpen)
                {
                    // Can never match anything: demote to literal text.
                    closer.IsDelimiter = false;
                    closer.Inline = new TextInline(new string(closer.Char, closer.Count));
                }

                closer = closer.Next;
                continue;
            }

            int use;
            InlineStyle style;
            if (closer.Char == '~')
            {
                use = closer.Count;
                style = use == 1 ? InlineStyle.Underline : InlineStyle.Strikethrough;
            }
            else
            {
                use = opener.Count >= 2 && closer.Count >= 2 ? 2 : 1;
                style = use == 2 ? InlineStyle.Bold : InlineStyle.Italic;
            }

            var children = ToInlines(opener.Next, closer);
            var formatted = new Node { Inline = new FormattedInline(style, children) };

            // Splice: opener -> formatted -> closer (everything in between is now a child).
            formatted.Prev = opener;
            formatted.Next = closer;
            opener.Next = formatted;
            closer.Prev = formatted;

            opener.Count -= use;
            closer.Count -= use;

            if (opener.Count == 0)
            {
                Unlink(opener);
            }

            if (closer.Count == 0)
            {
                var next = closer.Next;
                Unlink(closer);
                closer = next;
            }
        }
    }

    private static bool Compatible(Node opener, Node closer)
    {
        if (opener.Char != closer.Char)
        {
            return false;
        }

        if (opener.Char == '~')
        {
            return opener.Count == closer.Count;
        }

        // CommonMark "rule of three".
        if ((opener.CanClose || closer.CanOpen)
            && (opener.OriginalCount + closer.OriginalCount) % 3 == 0
            && !(opener.OriginalCount % 3 == 0 && closer.OriginalCount % 3 == 0))
        {
            return false;
        }

        return true;
    }

    private static void Unlink(Node node)
    {
        if (node.Prev is not null)
        {
            node.Prev.Next = node.Next;
        }

        if (node.Next is not null)
        {
            node.Next.Prev = node.Prev;
        }
    }

    /// <summary>Converts a node range to inlines, merging adjacent text and flattening leftover delimiters.</summary>
    private static List<Inline> ToInlines(Node? start, Node? stop)
    {
        var result = new List<Inline>();
        StringBuilder? pending = null;

        void FlushPending()
        {
            if (pending is { Length: > 0 })
            {
                result.Add(new TextInline(pending.ToString()));
                pending.Clear();
            }
        }

        for (var node = start; node is not null && node != stop; node = node.Next)
        {
            if (node.IsDelimiter)
            {
                if (node.Count > 0)
                {
                    pending ??= new StringBuilder();
                    pending.Append(node.Char, node.Count);
                }
            }
            else if (node.Inline is TextInline text)
            {
                pending ??= new StringBuilder();
                pending.Append(text.Text);
            }
            else if (node.Inline is not null)
            {
                FlushPending();
                result.Add(node.Inline);
            }
        }

        FlushPending();
        return result;
    }

    // ------------------------------------------------------------------
    // Code spans, tags, entities
    // ------------------------------------------------------------------

    private static int FindBacktickClose(string text, int from, int length)
    {
        var i = from;
        while (i < text.Length)
        {
            if (text[i] != '`')
            {
                i++;
                continue;
            }

            var start = i;
            while (i < text.Length && text[i] == '`')
            {
                i++;
            }

            if (i - start == length)
            {
                return start;
            }
        }

        return -1;
    }

    private static string NormalizeCodeSpan(string content)
    {
        content = content.Replace('\n', ' ');

        if (content.Length >= 2
            && content[0] == ' '
            && content[content.Length - 1] == ' '
            && content.Trim(' ').Length > 0)
        {
            return content.Substring(1, content.Length - 2);
        }

        return content;
    }

    private static bool TryParseUnderlineTag(string text, int index, int depth, out FormattedInline? result, out int next)
    {
        result = null;
        next = index;

        if (string.Compare(text, index, "<u>", 0, 3, StringComparison.OrdinalIgnoreCase) != 0)
        {
            return false;
        }

        var close = text.IndexOf("</u>", index + 3, StringComparison.OrdinalIgnoreCase);
        if (close < 0)
        {
            return false;
        }

        var inner = text.Substring(index + 3, close - (index + 3));
        var children = depth >= MaxDepth
            ? new List<Inline> { new TextInline(inner) }
            : Parse(inner, depth + 1);

        result = new FormattedInline(InlineStyle.Underline, children);
        next = close + 4;
        return true;
    }

    private static bool TryParseBreakTag(string text, int index, out int next)
    {
        next = index;
        foreach (var tag in BreakTags)
        {
            if (string.Compare(text, index, tag, 0, tag.Length, StringComparison.OrdinalIgnoreCase) == 0)
            {
                next = index + tag.Length;
                return true;
            }
        }

        return false;
    }

    private static bool TryDecodeEntity(string text, int index, out string value, out int length)
    {
        value = string.Empty;
        length = 0;

        var limit = Math.Min(text.Length, index + 12);
        var semicolon = -1;
        for (var i = index + 1; i < limit; i++)
        {
            if (text[i] == ';')
            {
                semicolon = i;
                break;
            }
        }

        if (semicolon < 0)
        {
            return false;
        }

        var name = text.Substring(index + 1, semicolon - index - 1);
        string? decoded = name switch
        {
            "amp" => "&",
            "lt" => "<",
            "gt" => ">",
            "quot" => "\"",
            "apos" => "'",
            "nbsp" => " ",
            "copy" => "©",
            "mdash" => "—",
            "ndash" => "–",
            "hellip" => "…",
            _ => null,
        };

        if (decoded is null && name.Length > 1 && name[0] == '#')
        {
            var hex = name[1] == 'x' || name[1] == 'X';
            var digits = name.Substring(hex ? 2 : 1);
            if (digits.Length > 0
                && digits.Length <= 7
                && int.TryParse(
                    digits,
                    hex ? System.Globalization.NumberStyles.AllowHexSpecifier : System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var codePoint)
                && codePoint > 0
                && codePoint <= 0x10FFFF
                && (codePoint < 0xD800 || codePoint > 0xDFFF))
            {
                decoded = char.ConvertFromUtf32(codePoint);
            }
        }

        if (decoded is null)
        {
            return false;
        }

        value = decoded;
        length = semicolon - index + 1;
        return true;
    }

    // ------------------------------------------------------------------
    // Links
    // ------------------------------------------------------------------

    private static bool TryParseLink(string text, int index, int depth, out LinkInline? link, out int next)
    {
        link = null;
        next = index;
        var end = text.Length;

        if (index >= end || text[index] != '[')
        {
            return false;
        }

        var labelEnd = FindBalanced(text, index + 1, end, '[', ']');
        if (labelEnd < 0 || labelEnd + 1 >= end || text[labelEnd + 1] != '(')
        {
            return false;
        }

        var destinationEnd = FindBalanced(text, labelEnd + 2, end, '(', ')');
        if (destinationEnd < 0)
        {
            return false;
        }

        var url = ExtractUrl(text.Substring(labelEnd + 2, destinationEnd - (labelEnd + 2)));
        if (url.Length == 0)
        {
            return false;
        }

        var label = text.Substring(index + 1, labelEnd - index - 1);
        var children = depth >= MaxDepth
            ? new List<Inline> { new TextInline(label) }
            : Parse(label, depth + 1);

        if (children.Count == 0)
        {
            children.Add(new TextInline(url));
        }

        link = new LinkInline(url, children);
        next = destinationEnd + 1;
        return true;
    }

    private static bool TryParseAutolink(string text, int index, out LinkInline? link, out int next)
    {
        link = null;
        next = index;
        if (index >= text.Length || text[index] != '<')
        {
            return false;
        }

        var close = text.IndexOf('>', index + 1);
        if (close < 0)
        {
            return false;
        }

        var url = text.Substring(index + 1, close - (index + 1)).Trim();
        if (url.IndexOf(' ') >= 0 || url.IndexOf('\n') >= 0 || !HasWebScheme(url))
        {
            return false;
        }

        link = new LinkInline(url, new Inline[] { new TextInline(url) });
        next = close + 1;
        return true;
    }

    private static int FindBalanced(string text, int from, int end, char open, char close)
    {
        var depth = 1;
        for (var i = from; i < end; i++)
        {
            if (text[i] == '\\' && i + 1 < end)
            {
                i++;
                continue;
            }

            if (text[i] == open)
            {
                depth++;
            }
            else if (text[i] == close && --depth == 0)
            {
                return i;
            }
        }

        return -1;
    }

    private static string ExtractUrl(string destination)
    {
        var value = destination.Trim();
        if (value.Length == 0)
        {
            return string.Empty;
        }

        string url;
        if (value[0] == '<')
        {
            var close = value.IndexOf('>');
            url = close > 1 ? value.Substring(1, close - 1).Trim() : value;
        }
        else
        {
            var space = value.IndexOfAny(new[] { ' ', '\t', '\n' });
            url = space > 0 ? value.Substring(0, space) : value;
        }

        return Unescape(url);
    }

    private static string Unescape(string value)
    {
        if (value.IndexOf('\\') < 0)
        {
            return value;
        }

        var sb = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '\\' && i + 1 < value.Length && IsAsciiPunctuation(value[i + 1]))
            {
                i++;
            }

            sb.Append(value[i]);
        }

        return sb.ToString();
    }

    private static bool HasWebScheme(string url)
    {
        return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------
    // Character helpers
    // ------------------------------------------------------------------

    private static bool IsAsciiPunctuation(char c)
    {
        return (c >= '!' && c <= '/')
            || (c >= ':' && c <= '@')
            || (c >= '[' && c <= '`')
            || (c >= '{' && c <= '~');
    }

    private static bool IsPunctuation(char c) => char.IsPunctuation(c) || char.IsSymbol(c);

    private static bool EndsWithTwoSpaces(StringBuilder buffer)
        => buffer.Length >= 2 && buffer[buffer.Length - 1] == ' ' && buffer[buffer.Length - 2] == ' ';

    private static void TrimEndSpaces(StringBuilder buffer)
    {
        while (buffer.Length > 0 && buffer[buffer.Length - 1] == ' ')
        {
            buffer.Length--;
        }
    }
}
