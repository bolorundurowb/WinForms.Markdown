namespace WinForms.Markdown.Markdown;

/// <summary>
/// Styles that can be applied to an inline span of Markdown text. The flags are
/// combined as spans nest (for example bold inside italic).
/// </summary>
[Flags]
public enum InlineStyle
{
    Regular = 0,
    Bold = 1 << 0,
    Italic = 1 << 1,
    Underline = 1 << 2,
    Strikethrough = 1 << 3,

    /// <summary>Monospace text. Applied by layout to code spans and code blocks.</summary>
    Code = 1 << 4,
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

/// <summary>An inline code span: <c>`code`</c>.</summary>
public sealed class CodeInline : Inline
{
    public CodeInline(string text) => Text = text;

    public string Text { get; }
}

/// <summary>A line break inside a paragraph or heading.</summary>
public sealed class LineBreakInline : Inline
{
    public LineBreakInline(bool isHard) => IsHard = isHard;

    /// <summary>
    /// <c>true</c> for an explicit break (two trailing spaces, a trailing backslash or
    /// <c>&lt;br&gt;</c>); <c>false</c> for a plain newline within a paragraph.
    /// </summary>
    public bool IsHard { get; }
}

/// <summary>
/// A hyperlink: <c>[label](url)</c> or an autolink such as <c>&lt;https://example.com&gt;</c>.
/// Images (<c>![alt](url)</c>) are represented as links whose label is the alt text.
/// </summary>
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

/// <summary>A Markdown heading (<c>#</c> through <c>######</c>, or a setext heading).</summary>
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

/// <summary>A fenced code block.</summary>
public sealed class CodeBlock : Block
{
    public CodeBlock(IReadOnlyList<string> lines, string? language)
    {
        Lines = lines;
        Language = language;
    }

    public IReadOnlyList<string> Lines { get; }

    /// <summary>The first word of the fence's info string, if any.</summary>
    public string? Language { get; }
}

/// <summary>A block quote (<c>&gt; text</c>) containing other blocks.</summary>
public sealed class BlockQuoteBlock : Block
{
    public BlockQuoteBlock(IReadOnlyList<Block> blocks) => Blocks = blocks;

    public IReadOnlyList<Block> Blocks { get; }
}

/// <summary>A single item of a <see cref="ListBlock"/>.</summary>
public sealed class ListItem
{
    public ListItem(IReadOnlyList<Block> blocks) => Blocks = blocks;

    public IReadOnlyList<Block> Blocks { get; }
}

/// <summary>A bulleted or numbered list.</summary>
public sealed class ListBlock : Block
{
    public ListBlock(bool isOrdered, int start, bool isTight, IReadOnlyList<ListItem> items)
    {
        IsOrdered = isOrdered;
        Start = start;
        IsTight = isTight;
        Items = items;
    }

    public bool IsOrdered { get; }

    /// <summary>The number of the first item of an ordered list.</summary>
    public int Start { get; }

    /// <summary><c>true</c> when no blank lines separate the items.</summary>
    public bool IsTight { get; }

    public IReadOnlyList<ListItem> Items { get; }
}

/// <summary>A horizontal rule (<c>---</c>, <c>***</c> or <c>___</c>).</summary>
public sealed class ThematicBreakBlock : Block
{
}

/// <summary>The parsed representation of an entire Markdown document.</summary>
public sealed class MarkdownDocument
{
    public MarkdownDocument(IReadOnlyList<Block> blocks) => Blocks = blocks;

    public IReadOnlyList<Block> Blocks { get; }

    public static MarkdownDocument Empty { get; } = new(Array.Empty<Block>());
}
