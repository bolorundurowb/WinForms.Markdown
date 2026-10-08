using System.Drawing;
using WinForms.Markdown.Markdown;

namespace WinForms.Markdown.Rendering;

/// <summary>
/// A font description that is independent of GDI: a size relative to the control's base
/// font plus the <see cref="InlineStyle"/> flags. The text-metrics implementation maps
/// it onto a real <c>Font</c>.
/// </summary>
internal readonly struct TextStyle : IEquatable<TextStyle>
{
    public TextStyle(float scale, InlineStyle flags)
    {
        Scale = scale;
        Flags = flags;
    }

    /// <summary>Font size as a multiple of the base font size.</summary>
    public float Scale { get; }

    public InlineStyle Flags { get; }

    public TextStyle With(InlineStyle extra) => new TextStyle(Scale, Flags | extra);

    public bool Equals(TextStyle other) => Scale.Equals(other.Scale) && Flags == other.Flags;

    public override bool Equals(object? obj) => obj is TextStyle other && Equals(other);

    public override int GetHashCode() => (Scale.GetHashCode() * 397) ^ (int)Flags;

    public static bool operator ==(TextStyle left, TextStyle right) => left.Equals(right);

    public static bool operator !=(TextStyle left, TextStyle right) => !left.Equals(right);
}

/// <summary>Measures text for layout. Implemented over GDI in the control and by a fake in tests.</summary>
internal interface ITextMetrics
{
    /// <summary>The rendered width, in pixels, of <paramref name="text"/> (which never contains a newline).</summary>
    int MeasureWidth(string text, TextStyle style);

    /// <summary>The height, in pixels, of one line of text in <paramref name="style"/>.</summary>
    int LineHeight(TextStyle style);
}

/// <summary>Which colour a text run is painted with.</summary>
internal enum ColorRole
{
    Text,
    Link,
}

/// <summary>A single positioned run of text (one style, one colour, one line).</summary>
internal sealed class TextRun
{
    public TextRun(string text, TextStyle style, ColorRole role, Rectangle bounds, int lineTop, int lineBottom, string? url, bool highlight)
    {
        Text = text;
        Style = style;
        Role = role;
        Bounds = bounds;
        LineTop = lineTop;
        LineBottom = lineBottom;
        Url = url;
        Highlight = highlight;
    }

    public string Text { get; }

    public TextStyle Style { get; }

    public ColorRole Role { get; }

    public Rectangle Bounds { get; }

    /// <summary>Top of the line this run sits on (runs on one line may have different heights).</summary>
    public int LineTop { get; }

    public int LineBottom { get; }

    /// <summary>The hyperlink address, when this run is part of a link.</summary>
    public string? Url { get; }

    /// <summary>True for inline code spans, which are painted on a tinted background.</summary>
    public bool Highlight { get; }
}

internal enum ShapeKind
{
    CodeBackground,
    QuoteBar,
    Rule,
}

/// <summary>A filled rectangle painted behind or between text runs.</summary>
internal sealed class LayoutShape
{
    public LayoutShape(ShapeKind kind, Rectangle bounds)
    {
        Kind = kind;
        Bounds = bounds;
    }

    public ShapeKind Kind { get; }

    public Rectangle Bounds { get; }
}

/// <summary>Space reserved around the document.</summary>
internal readonly struct LayoutInsets
{
    public LayoutInsets(int left, int top, int right, int bottom)
    {
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }

    public int Left { get; }

    public int Top { get; }

    public int Right { get; }

    public int Bottom { get; }
}

/// <summary>The result of laying a document out against a given width.</summary>
internal sealed class MarkdownLayout
{
    public MarkdownLayout(
        IReadOnlyList<TextRun> runs,
        IReadOnlyList<LayoutShape> shapes,
        int contentWidth,
        int contentHeight,
        int maxLineHeight)
    {
        Runs = runs;
        Shapes = shapes;
        ContentWidth = contentWidth;
        ContentHeight = contentHeight;
        MaxLineHeight = maxLineHeight;

        var links = new List<TextRun>();
        foreach (var run in runs)
        {
            if (run.Url is not null)
            {
                links.Add(run);
            }
        }

        LinkRuns = links;
    }

    /// <summary>All runs, ordered by <see cref="TextRun.LineTop"/>.</summary>
    public IReadOnlyList<TextRun> Runs { get; }

    public IReadOnlyList<LayoutShape> Shapes { get; }

    /// <summary>The runs that carry a hyperlink.</summary>
    public IReadOnlyList<TextRun> LinkRuns { get; }

    /// <summary>
    /// The width the content needs including the right inset. Only exceeds the layout
    /// width when something (a wide code block) cannot be wrapped.
    /// </summary>
    public int ContentWidth { get; }

    public int ContentHeight { get; }

    public int MaxLineHeight { get; }

    /// <summary>
    /// Returns the index of the first run that could be visible at or below
    /// <paramref name="top"/> (a conservative bound; callers stop once
    /// <see cref="TextRun.LineTop"/> passes the bottom of the area they paint).
    /// </summary>
    public int FindFirstRun(int top)
    {
        var threshold = top - MaxLineHeight;
        var lo = 0;
        var hi = Runs.Count;
        while (lo < hi)
        {
            var mid = lo + (hi - lo) / 2;
            if (Runs[mid].LineTop < threshold)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        return lo;
    }
}
