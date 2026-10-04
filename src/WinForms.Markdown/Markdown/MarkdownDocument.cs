namespace WinForms.Markdown.Markdown;

/// <summary>
/// Styles that can be applied to an inline span of Markdown text.
/// The flags map directly onto <see cref="System.Drawing.FontStyle"/> so that
/// a single <see cref="System.Drawing.Font"/> can carry every combination
/// (bold, italic, underline and strikethrough are all font-level attributes
/// understood natively by GDI).
/// </summary>
[Flags]
public enum InlineStyle
{
    Regular = 0,
    Bold = 1 << 0,
    Italic = 1 << 1,
    Underline = 1 << 2,
    Strikethrough = 1 << 3,
}

/// <summary>Base type for every inline element in a block.</summary>
public abstract class Inline
{
}

/// <summary>A plain run of text with no additional formatting.</summary>
public sealed class TextInline : Inline
{
    public TextInline(string text) => Text = text;

    public string Text { get; }
}

/// <summary>A hyperlink: <c>[label](url)</c> or an autolink such as <c>&lt;https://example.com&gt;</c>.</summary>
public sealed class LinkInline : Inline
{
    public LinkInline(string url, IReadOnlyList<Inline> children)
    {
        Url = url;
        Children = children;
    }

    public string Url { get; }

    public IReadOnlyList<Inline> Children { get; }
}

/// <summary>A span of text wrapped in one or more formatting styles.</summary>
public sealed class FormattedInline : Inline
{
    public FormattedInline(InlineStyle style, IReadOnlyList<Inline> children)
    {
        Style = style;
        Children = children;
    }

    public InlineStyle Style { get; }

    public IReadOnlyList<Inline> Children { get; }
}

/// <summary>Base type for every block-level element in a document.</summary>
public abstract class Block
{
}

/// <summary>A Markdown heading (<c>#</c> through <c>######</c>).</summary>
public sealed class HeadingBlock : Block
{
    public HeadingBlock(int level, IReadOnlyList<Inline> inlines)
    {
        Level = level;
        Inlines = inlines;
    }

    /// <summary>The heading level, from 1 (largest) to 6 (smallest).</summary>
    public int Level { get; }

    public IReadOnlyList<Inline> Inlines { get; }
}

/// <summary>A Markdown paragraph: a sequence of consecutive, non-blank lines.</summary>
public sealed class ParagraphBlock : Block
{
    public ParagraphBlock(IReadOnlyList<Inline> inlines) => Inlines = inlines;

    public IReadOnlyList<Inline> Inlines { get; }
}

/// <summary>The parsed representation of an entire Markdown document.</summary>
public sealed class MarkdownDocument
{
    public MarkdownDocument(IReadOnlyList<Block> blocks) => Blocks = blocks;

    public IReadOnlyList<Block> Blocks { get; }

    public static MarkdownDocument Empty { get; } = new(Array.Empty<Block>());
}
