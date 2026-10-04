using System.Drawing;
using WinForms.Markdown.Markdown;
using WinForms.Markdown.Rendering;
using Xunit;

namespace WinForms.Markdown.Tests;

public class MarkdownRendererTests
{
    [Fact]
    public void Layout_EmptyDocument_HasNoRuns()
    {
        using var font = new Font("Segoe UI", 10f);
        using var renderer = new MarkdownRenderer(font);

        MarkdownLayout layout = renderer.Layout(MarkdownDocument.Empty, 400, Color.Black, new Padding(10));

        Assert.Empty(layout.Runs);
    }

    [Fact]
    public void Layout_BoldRun_HasBoldFont()
    {
        using var font = new Font("Segoe UI", 10f);
        using var renderer = new MarkdownRenderer(font);
        MarkdownDocument document = MarkdownParser.Parse("**bold**");

        MarkdownLayout layout = renderer.Layout(document, 400, Color.Black, new Padding(10));

        TextRun run = layout.Runs.Single(r => r.Text == "bold");
        Assert.True(run.Font.Bold);
    }

    [Fact]
    public void Layout_UnderlineAndStrikethrough_SetFontStyles()
    {
        using var font = new Font("Segoe UI", 10f);
        using var renderer = new MarkdownRenderer(font);
        MarkdownDocument document = MarkdownParser.Parse("<u>under</u> ~~strike~~");

        MarkdownLayout layout = renderer.Layout(document, 400, Color.Black, new Padding(10));

        Assert.Contains(layout.Runs, r => r.Text == "under" && r.Font.Underline);
        Assert.Contains(layout.Runs, r => r.Text == "strike" && r.Font.Strikeout);
    }

    [Fact]
    public void Layout_Heading_UsesLargerBoldFont()
    {
        using var font = new Font("Segoe UI", 10f);
        using var renderer = new MarkdownRenderer(font);
        MarkdownDocument document = MarkdownParser.Parse("# Title\n\nplain");

        MarkdownLayout layout = renderer.Layout(document, 400, Color.Black, new Padding(10));

        TextRun heading = layout.Runs.Single(r => r.Text == "Title");
        TextRun plain = layout.Runs.Single(r => r.Text == "plain");

        Assert.True(heading.Font.Bold);
        Assert.True(heading.Font.Size > plain.Font.Size);
    }

    [Fact]
    public void Layout_WrapsLongTextOntoMultipleLines()
    {
        using var font = new Font("Segoe UI", 10f);
        using var renderer = new MarkdownRenderer(font);
        string text = string.Join(" ", Enumerable.Repeat("word", 200));
        MarkdownDocument document = MarkdownParser.Parse(text);

        MarkdownLayout layout = renderer.Layout(document, 200, Color.Black, new Padding(10));

        var lineYPositions = layout.Runs.Where(r => r.Text == "word").Select(r => r.Bounds.Y).Distinct().ToList();
        Assert.True(lineYPositions.Count > 1);
    }

    [Fact]
    public void Layout_ContentHeight_GrowsWithContent()
    {
        using var font = new Font("Segoe UI", 10f);
        using var renderer = new MarkdownRenderer(font);
        MarkdownDocument shortDoc = MarkdownParser.Parse("# one");
        MarkdownDocument longDoc = MarkdownParser.Parse("# one\n\n# two\n\n# three\n\n# four");

        MarkdownLayout shortLayout = renderer.Layout(shortDoc, 400, Color.Black, new Padding(10));
        MarkdownLayout longLayout = renderer.Layout(longDoc, 400, Color.Black, new Padding(10));

        Assert.True(longLayout.ContentHeight > shortLayout.ContentHeight);
    }
}
