using WinForms.Markdown.Markdown;
using WinForms.Markdown.Rendering;

namespace WinForms.Markdown.Tests;

internal static class TestHelpers
{
    /// <summary>Flattens nested inlines into (accumulated style, text) pairs. Links add no style; code adds <see cref="InlineStyle.Code"/>.</summary>
    public static List<(InlineStyle Style, string Text)> Flatten(IReadOnlyList<Inline> inlines)
    {
        var result = new List<(InlineStyle, string)>();

        void Walk(IReadOnlyList<Inline> items, InlineStyle style)
        {
            foreach (var inline in items)
            {
                switch (inline)
                {
                    case TextInline text:
                        result.Add((style, text.Text));
                        break;

                    case CodeInline code:
                        result.Add((style | InlineStyle.Code, code.Text));
                        break;

                    case FormattedInline formatted:
                        Walk(formatted.Children, style | formatted.Style);
                        break;

                    case LinkInline link:
                        Walk(link.Children, style);
                        break;
                }
            }
        }

        Walk(inlines, InlineStyle.Regular);
        return result;
    }

    public static IReadOnlyList<Inline> ParseInlines(string markdown)
    {
        var document = MarkdownParser.Parse(markdown);
        var paragraph = (ParagraphBlock)document.Blocks[0];
        return paragraph.Inlines;
    }

    public static MarkdownLayout Layout(string markdown, int width = 200, int inset = 0, bool preserveLineBreaks = true)
        => MarkdownLayoutEngine.Layout(
            MarkdownParser.Parse(markdown),
            new FakeMetrics(),
            width,
            new LayoutInsets(inset, inset, inset, inset),
            preserveLineBreaks);
}

/// <summary>Deterministic monospace metrics: 8px per character and 16px lines at scale 1.</summary>
internal sealed class FakeMetrics : ITextMetrics
{
    public const int CharWidth = 8;
    public const int Line = 16;

    public int MeasureWidth(string text, TextStyle style)
        => (int)Math.Round(text.Length * CharWidth * style.Scale);

    public int LineHeight(TextStyle style)
        => (int)Math.Round(Line * style.Scale);
}
