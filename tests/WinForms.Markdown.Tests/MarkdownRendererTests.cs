using AwesomeAssertions;
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

        var layout = renderer.Layout(MarkdownDocument.Empty, 400, Color.Black, Color.Blue, new Padding(10));

        layout.Runs.Should().BeEmpty();
    }

    [Fact]
    public void Layout_BoldRun_HasBoldFont()
    {
        using var font = new Font("Segoe UI", 10f);
        using var renderer = new MarkdownRenderer(font);
        var document = MarkdownParser.Parse("**bold**");

        var layout = renderer.Layout(document, 400, Color.Black, Color.Blue, new Padding(10));

        var run = layout.Runs.Single(r => r.Text == "bold");
        run.Font.Bold.Should().BeTrue();
    }

    [Fact]
    public void Layout_UnderlineAndStrikethrough_SetFontStyles()
    {
        using var font = new Font("Segoe UI", 10f);
        using var renderer = new MarkdownRenderer(font);
        var document = MarkdownParser.Parse("<u>under</u> ~~strike~~");

        var layout = renderer.Layout(document, 400, Color.Black, Color.Blue, new Padding(10));

        layout.Runs.Should().Contain(r => r.Text == "under" && r.Font.Underline);
        layout.Runs.Should().Contain(r => r.Text == "strike" && r.Font.Strikeout);
    }

    [Fact]
    public void Layout_Heading_UsesLargerBoldFont()
    {
        using var font = new Font("Segoe UI", 10f);
        using var renderer = new MarkdownRenderer(font);
        var document = MarkdownParser.Parse("# Title\n\nplain");

        var layout = renderer.Layout(document, 400, Color.Black, Color.Blue, new Padding(10));

        var heading = layout.Runs.Single(r => r.Text == "Title");
        var plain = layout.Runs.Single(r => r.Text == "plain");

        heading.Font.Bold.Should().BeTrue();
        heading.Font.Size.Should().BeGreaterThan(plain.Font.Size);
    }

    [Fact]
    public void Layout_WrapsLongTextOntoMultipleLines()
    {
        using var font = new Font("Segoe UI", 10f);
        using var renderer = new MarkdownRenderer(font);
        var text = string.Join(" ", Enumerable.Repeat("word", 200));
        var document = MarkdownParser.Parse(text);

        var layout = renderer.Layout(document, 200, Color.Black, Color.Blue, new Padding(10));

        var lineYPositions = layout.Runs.Where(r => r.Text == "word").Select(r => r.Bounds.Y).Distinct().ToList();
        lineYPositions.Count.Should().BeGreaterThan(1);
    }

    [Fact]
    public void Layout_ContentHeight_GrowsWithContent()
    {
        using var font = new Font("Segoe UI", 10f);
        using var renderer = new MarkdownRenderer(font);
        var shortDoc = MarkdownParser.Parse("# one");
        var longDoc = MarkdownParser.Parse("# one\n\n# two\n\n# three\n\n# four");

        var shortLayout = renderer.Layout(shortDoc, 400, Color.Black, Color.Blue, new Padding(10));
        var longLayout = renderer.Layout(longDoc, 400, Color.Black, Color.Blue, new Padding(10));

        longLayout.ContentHeight.Should().BeGreaterThan(shortLayout.ContentHeight);
    }

    [Fact]
    public void Layout_Link_IsUnderlinedAndCarriesUrl()
    {
        using var font = new Font("Segoe UI", 10f);
        using var renderer = new MarkdownRenderer(font);
        var document = MarkdownParser.Parse("[docs](https://example.com)");

        var layout = renderer.Layout(document, 400, Color.Black, Color.Blue, new Padding(10));

        var run = layout.Runs.Single(r => r.Text == "docs");
        run.Url.Should().Be("https://example.com");
        run.Font.Underline.Should().BeTrue();
        run.Color.Should().Be(Color.Blue);
    }
}
