using AwesomeAssertions;
using WinForms.Markdown.Markdown;
using WinForms.Markdown.Rendering;
using Xunit;

namespace WinForms.Markdown.Tests;

/// <summary>Layout through the real GDI metrics (needs Windows and the Segoe UI font).</summary>
public class MarkdownRendererTests
{
    private static (MarkdownLayout Layout, GdiTextMetrics Metrics) Layout(string markdown, int width = 400)
    {
        var font = new Font("Segoe UI", 10f);
        var metrics = new GdiTextMetrics(font);
        var layout = MarkdownLayoutEngine.Layout(MarkdownParser.Parse(markdown), metrics, width, new LayoutInsets(10, 10, 10, 10), true);
        return (layout, metrics);
    }

    [Fact]
    public void Layout_EmptyDocument_HasNoRuns()
    {
        var (layout, metrics) = Layout(string.Empty);
        using (metrics)
        {
            layout.Runs.Should().BeEmpty();
        }
    }

    [Fact]
    public void Layout_BoldRun_HasBoldFont()
    {
        var (layout, metrics) = Layout("**bold**");
        using (metrics)
        {
            var run = layout.Runs.Single(r => r.Text == "bold");
            metrics.GetFont(run.Style).Bold.Should().BeTrue();
        }
    }

    [Fact]
    public void Layout_UnderlineAndStrikethrough_SetFontStyles()
    {
        var (layout, metrics) = Layout("<u>under</u> ~~strike~~");
        using (metrics)
        {
            layout.Runs.Should().Contain(r => r.Text == "under" && metrics.GetFont(r.Style).Underline);
            layout.Runs.Should().Contain(r => r.Text == "strike" && metrics.GetFont(r.Style).Strikeout);
        }
    }

    [Fact]
    public void Layout_Heading_UsesLargerBoldFont()
    {
        var (layout, metrics) = Layout("# Title\n\nplain");
        using (metrics)
        {
            var heading = metrics.GetFont(layout.Runs.Single(r => r.Text == "Title").Style);
            var plain = metrics.GetFont(layout.Runs.Single(r => r.Text == "plain").Style);

            heading.Bold.Should().BeTrue();
            heading.Size.Should().BeGreaterThan(plain.Size);
        }
    }

    [Fact]
    public void Layout_CodeRun_UsesMonospaceFamily()
    {
        var (layout, metrics) = Layout("some `code` here");
        using (metrics)
        {
            var code = metrics.GetFont(layout.Runs.Single(r => r.Text == "code").Style);
            var plain = metrics.GetFont(layout.Runs.First(r => r.Text.Contains("some")).Style);

            code.FontFamily.Name.Should().NotBe(plain.FontFamily.Name);
        }
    }

    [Fact]
    public void Layout_WrapsLongTextAndStaysWithinWidth()
    {
        var (layout, metrics) = Layout(string.Join(" ", Enumerable.Repeat("word", 200)), width: 200);
        using (metrics)
        {
            layout.Runs.Select(r => r.LineTop).Distinct().Count().Should().BeGreaterThan(1);
            layout.Runs.Max(r => r.Bounds.Right).Should().BeLessThanOrEqualTo(190);
        }
    }

    [Fact]
    public void Layout_Link_IsUnderlinedAndCarriesUrl()
    {
        var (layout, metrics) = Layout("[docs](https://example.com)");
        using (metrics)
        {
            var run = layout.Runs.Single(r => r.Text == "docs");
            run.Url.Should().Be("https://example.com");
            run.Role.Should().Be(ColorRole.Link);
            metrics.GetFont(run.Style).Underline.Should().BeTrue();
        }
    }

    [Fact]
    public void Metrics_CachesMeasurements_AndReturnsStableWidths()
    {
        var (_, metrics) = Layout(string.Empty);
        using (metrics)
        {
            var style = new TextStyle(1f, InlineStyle.Regular);
            var first = metrics.MeasureWidth("hello world", style);

            metrics.MeasureWidth("hello world", style).Should().Be(first);
            first.Should().BeGreaterThan(metrics.MeasureWidth("hello", style));
        }
    }
}
