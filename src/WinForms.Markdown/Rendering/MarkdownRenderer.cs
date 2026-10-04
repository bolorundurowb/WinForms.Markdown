using System.Drawing;
using System.Text;
using WinForms.Markdown;
using WinForms.Markdown.Markdown;

namespace WinForms.Markdown.Rendering;

/// <summary>
/// A single, positioned run of text ready to be painted. The <see cref="Font"/>
/// already carries any bold / italic / underline / strikethrough attributes.
/// </summary>
public sealed class TextRun
{
    public TextRun(string text, Font font, Color color, Rectangle bounds)
    {
        Text = text;
        Font = font;
        Color = color;
        Bounds = bounds;
    }

    public string Text { get; }

    public Font Font { get; }

    public Color Color { get; }

    public Rectangle Bounds { get; }
}

/// <summary>The result of laying a document out against a given width.</summary>
public sealed class MarkdownLayout
{
    public MarkdownLayout(IReadOnlyList<TextRun> runs, int contentWidth, int contentHeight)
    {
        Runs = runs;
        ContentWidth = contentWidth;
        ContentHeight = contentHeight;
    }

    public IReadOnlyList<TextRun> Runs { get; }

    public int ContentWidth { get; }

    public int ContentHeight { get; }
}

/// <summary>
/// Converts a parsed <see cref="MarkdownDocument"/> into a list of positioned
/// <see cref="TextRun"/>s using GDI text measurement. This class owns no GDI
/// device context of its own; it only measures text and caches the
/// <see cref="Font"/> instances it creates (which the owning control disposes).
/// </summary>
public sealed class MarkdownRenderer : IDisposable
{
    private const TextFormatFlags TextFlags =
        TextFormatFlags.NoPadding |
        TextFormatFlags.NoPrefix |
        TextFormatFlags.Left |
        TextFormatFlags.Top |
        TextFormatFlags.SingleLine;

    // Relative heading sizes (multiplier of the base font size).
    private static readonly float[] HeadingScales = { 0f, 1.75f, 1.5f, 1.25f, 1.1f, 1.0f, 0.9f };

    private readonly Font _baseFont;
    private readonly Dictionary<(float Size, FontStyle Style), Font> _fontCache = new();
    private readonly List<Font> _ownedFonts = new();

    public MarkdownRenderer(Font baseFont) => _baseFont = baseFont ?? throw new ArgumentNullException(nameof(baseFont));

    /// <summary>
    /// Lays the document out within <paramref name="availableWidth"/> (the width
    /// of the area available for text).
    /// </summary>
    public MarkdownLayout Layout(MarkdownDocument document, int availableWidth, Color foreColor, Padding padding)
    {
        var runs = new List<TextRun>();
        int left = padding.Left;
        int right = Math.Max(left + 1, availableWidth - padding.Right);
        int y = padding.Top;
        int maxWidth = 0;
        bool first = true;

        foreach (Block block in document.Blocks)
        {
            if (!first)
            {
                y += BlockSpacing;
            }

            first = false;

            switch (block)
            {
                case HeadingBlock heading:
                    y = LayoutHeading(heading, left, right, y, foreColor, runs, ref maxWidth);
                    break;

                case ParagraphBlock paragraph:
                    y = LayoutParagraph(paragraph, left, right, y, foreColor, runs, ref maxWidth);
                    break;
            }
        }

        int contentHeight = y + padding.Bottom;
        int contentWidth = maxWidth + padding.Right;
        return new MarkdownLayout(runs, contentWidth, contentHeight);
    }

    private int BlockSpacing => Math.Max(4, (int)Math.Round(_baseFont.Size * 0.8f));

    private int LayoutHeading(HeadingBlock heading, int left, int right, int y, Color color, List<TextRun> runs, ref int maxWidth)
    {
        float size = _baseFont.Size * HeadingScales[Polyfill.Clamp(heading.Level, 1, 6)];
        Font headingFont = GetFont(size, FontStyle.Bold);
        var inlineRuns = Flatten(heading.Inlines, size, FontStyle.Bold, color);

        int x = left;
        foreach (Run run in inlineRuns)
        {
            Size measured = Measure(run.Text, run.Font);
            runs.Add(new TextRun(run.Text, run.Font, run.Color, new Rectangle(x, y, measured.Width, measured.Height)));
            x += measured.Width;
        }

        maxWidth = Math.Max(maxWidth, x - left);
        return y + LineHeight(headingFont);
    }

    private int LayoutParagraph(ParagraphBlock paragraph, int left, int right, int y, Color color, List<TextRun> runs, ref int maxWidth)
    {
        float size = _baseFont.Size;
        var inlineRuns = Flatten(paragraph.Inlines, size, _baseFont.Style, color);
        return LayoutWrapped(inlineRuns, left, right, y, runs, ref maxWidth);
    }

    /// <summary>Word-wraps a stream of inline runs into positioned text runs.</summary>
    private int LayoutWrapped(List<Run> inlineRuns, int left, int right, int topY, List<TextRun> output, ref int maxWidth)
    {
        int available = right - left;
        int baseHeight = LineHeight(_baseFont);
        var line = new List<(Token Token, int Width)>();
        int lineWidth = 0;
        int lineHeight = baseHeight;
        int y = topY;
        int blockMaxWidth = 0;

        void Commit()
        {
            int x = left;
            int committedHeight = lineHeight;
            foreach ((Token token, int width) in line)
            {
                if (token.Kind == TokenKind.Word)
                {
                    output.Add(new TextRun(token.Text, token.Font, token.Color, new Rectangle(x, y, width, committedHeight)));
                }

                x += width;
            }

            blockMaxWidth = Math.Max(blockMaxWidth, x - left);
            y += committedHeight;
            line.Clear();
            lineWidth = 0;
            lineHeight = baseHeight;
        }

        foreach (Token token in Tokenize(inlineRuns))
        {
            if (token.Kind == TokenKind.Break)
            {
                if (line.Count > 0)
                {
                    Commit();
                }
                else
                {
                    y += baseHeight;
                }

                continue;
            }

            int width = Measure(token.Text, token.Font).Width;
            int height = LineHeight(token.Font);

            if (line.Count > 0 && lineWidth + width > available)
            {
                Commit();
            }

            if (token.Kind == TokenKind.Space)
            {
                if (line.Count > 0)
                {
                    line.Add((token, width));
                    lineWidth += width;
                    lineHeight = Math.Max(lineHeight, height);
                }

                continue;
            }

            line.Add((token, width));
            lineWidth += width;
            lineHeight = Math.Max(lineHeight, height);
        }

        if (line.Count > 0)
        {
            Commit();
        }

        maxWidth = Math.Max(maxWidth, blockMaxWidth);
        return y;
    }

    private static IEnumerable<Token> Tokenize(List<Run> runs)
    {
        foreach (Run run in runs)
        {
            var word = new StringBuilder();
            foreach (char c in run.Text)
            {
                if (c == '\n')
                {
                    if (word.Length > 0)
                    {
                        yield return new Token(word.ToString(), run.Font, run.Color, TokenKind.Word);
                        word.Clear();
                    }

                    yield return new Token("\n", run.Font, run.Color, TokenKind.Break);
                }
                else if (char.IsWhiteSpace(c))
                {
                    if (word.Length > 0)
                    {
                        yield return new Token(word.ToString(), run.Font, run.Color, TokenKind.Word);
                        word.Clear();
                    }

                    yield return new Token(c.ToString(), run.Font, run.Color, TokenKind.Space);
                }
                else
                {
                    word.Append(c);
                }
            }

            if (word.Length > 0)
            {
                yield return new Token(word.ToString(), run.Font, run.Color, TokenKind.Word);
            }
        }
    }

    /// <summary>Flattens nested inline nodes into a flat list of styled runs.</summary>
    private List<Run> Flatten(IReadOnlyList<Inline> inlines, float size, FontStyle baseStyle, Color color)
    {
        var runs = new List<Run>();

        void Walk(IReadOnlyList<Inline> items, FontStyle style)
        {
            foreach (Inline inline in items)
            {
                switch (inline)
                {
                    case TextInline text:
                        if (text.Text.Length > 0)
                        {
                            runs.Add(new Run(text.Text, GetFont(size, style), color));
                        }

                        break;

                    case FormattedInline formatted:
                        Walk(formatted.Children, style | ToFontStyle(formatted.Style));
                        break;
                }
            }
        }

        Walk(inlines, baseStyle);
        return runs;
    }

    private static FontStyle ToFontStyle(InlineStyle style)
    {
        FontStyle result = FontStyle.Regular;
        if ((style & InlineStyle.Bold) != 0)
        {
            result |= FontStyle.Bold;
        }

        if ((style & InlineStyle.Italic) != 0)
        {
            result |= FontStyle.Italic;
        }

        if ((style & InlineStyle.Underline) != 0)
        {
            result |= FontStyle.Underline;
        }

        if ((style & InlineStyle.Strikethrough) != 0)
        {
            result |= FontStyle.Strikeout;
        }

        return result;
    }

    private Font GetFont(float size, FontStyle style)
    {
        var key = (size, style);
        if (!_fontCache.TryGetValue(key, out Font? font))
        {
            font = new Font(_baseFont.FontFamily, size, style, _baseFont.Unit);
            _fontCache[key] = font;
            _ownedFonts.Add(font);
        }

        return font;
    }

    private static Size Measure(string text, Font font)
        => TextRenderer.MeasureText(text, font, new Size(int.MaxValue, int.MaxValue), TextFlags);

    private static int LineHeight(Font font)
        => TextRenderer.MeasureText("Xg", font, new Size(int.MaxValue, int.MaxValue), TextFlags).Height;

    public void Dispose()
    {
        foreach (Font font in _ownedFonts)
        {
            font.Dispose();
        }

        _ownedFonts.Clear();
        _fontCache.Clear();
    }

    private readonly record struct Run(string Text, Font Font, Color Color);

    private readonly record struct Token(string Text, Font Font, Color Color, TokenKind Kind);

    private enum TokenKind
    {
        Word,
        Space,
        Break,
    }
}
