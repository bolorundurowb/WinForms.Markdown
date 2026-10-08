using System.Diagnostics;
using AwesomeAssertions;
using WinForms.Markdown.Markdown;
using WinForms.Markdown.Rendering;
using Xunit;

namespace WinForms.Markdown.Tests;

/// <summary>The parser and layout must never throw, hang or overflow the stack, whatever the input.</summary>
public class MarkdownRobustnessTests
{
    private static readonly string[] Fragments =
    {
        "*", "**", "***", "_", "__", "~", "~~", "`", "``", "```", "[", "]", "(", ")", "<", ">", "<u>", "</u>", "<br>",
        "!", "#", "##", "-", "- ", "1. ", "> ", "\\", "&", "&amp;", "\n", "\n\n", "  \n", " ", "  ", "\t", "a", "word",
        "https://example.com", "---", "===", "😀", " ",
    };

    [Fact]
    public void RandomMarkdown_NeverThrowsAndAlwaysLaysOut()
    {
        var random = new Random(12345);
        var metrics = new FakeMetrics();

        for (var iteration = 0; iteration < 3000; iteration++)
        {
            var parts = random.Next(1, 40);
            var text = string.Concat(Enumerable.Range(0, parts).Select(_ => Fragments[random.Next(Fragments.Length)]));

            var document = MarkdownParser.Parse(text);
            var layout = MarkdownLayoutEngine.Layout(document, metrics, random.Next(1, 300), new LayoutInsets(5, 5, 5, 5), random.Next(2) == 0);

            layout.ContentHeight.Should().BeGreaterThanOrEqualTo(10);
        }
    }

    [Theory]
    [InlineData(">", 20000)]
    [InlineData("- ", 20000)]
    [InlineData("[", 20000)]
    [InlineData("*", 50000)]
    [InlineData("_a ", 20000)]
    [InlineData("`", 20000)]
    [InlineData("<u>", 5000)]
    public void PathologicalInput_FinishesQuicklyWithoutStackOverflow(string unit, int repeat)
    {
        var text = string.Concat(Enumerable.Repeat(unit, repeat));
        var stopwatch = Stopwatch.StartNew();

        var document = MarkdownParser.Parse(text);
        MarkdownLayoutEngine.Layout(document, new FakeMetrics(), 300, new LayoutInsets(5, 5, 5, 5), true);

        stopwatch.Elapsed.TotalSeconds.Should().BeLessThan(10);
    }

    [Fact]
    public void DeeplyNestedLinks_DoNotOverflowTheStack()
    {
        var text = string.Concat(Enumerable.Repeat("[", 3000)) + "x" + string.Concat(Enumerable.Repeat("](a)", 3000));

        MarkdownParser.Parse(text).Blocks.Should().NotBeEmpty();
    }

    [Fact]
    public void DeeplyNestedQuotesAndLists_DoNotOverflowTheStack()
    {
        var text = string.Concat(Enumerable.Repeat("> - ", 2000)) + "deep";

        var document = MarkdownParser.Parse(text);
        var layout = MarkdownLayoutEngine.Layout(document, new FakeMetrics(), 300, new LayoutInsets(5, 5, 5, 5), true);

        layout.Runs.Should().NotBeEmpty();
    }

    [Fact]
    public void Layout_ZeroOrTinyWidth_StillTerminates()
    {
        var document = MarkdownParser.Parse("# Heading\n\nSome **styled** text with a veryveryverylongword.\n\n- item\n\n> quote");

        foreach (var width in new[] { 0, 1, 2, 10 })
        {
            var layout = MarkdownLayoutEngine.Layout(document, new FakeMetrics(), width, new LayoutInsets(10, 10, 10, 10), true);
            layout.Runs.Should().NotBeEmpty();
        }
    }
}
