using AwesomeAssertions;
using WinForms.Markdown.Markdown;
using WinForms.Markdown.Rendering;
using Xunit;

namespace WinForms.Markdown.Tests;

/// <summary>Layout tests using deterministic fake metrics (8px per character, 16px lines), so they run anywhere.</summary>
public class MarkdownLayoutEngineTests
{
    private static string Words(int count) => string.Join(" ", Enumerable.Repeat("word", count));

    [Fact]
    public void EmptyDocument_HasNoRunsAndOnlyInsets()
    {
        var layout = MarkdownLayoutEngine.Layout(MarkdownDocument.Empty, new FakeMetrics(), 400, new LayoutInsets(10, 8, 10, 6), true);

        layout.Runs.Should().BeEmpty();
        layout.ContentHeight.Should().Be(14);
    }

    [Fact]
    public void Text_StaysInsideBothInsets()
    {
        // Regression: padding used to be subtracted twice, narrowing the column by Padding.Horizontal.
        var layout = TestHelpers.Layout(Words(60), width: 200, inset: 10);

        layout.Runs.Min(r => r.Bounds.Left).Should().Be(10);
        layout.Runs.Max(r => r.Bounds.Right).Should().BeLessThanOrEqualTo(190);
        layout.Runs.Max(r => r.Bounds.Right).Should().BeGreaterThan(150);
    }

    [Fact]
    public void LongText_WrapsOntoMultipleLines()
    {
        var layout = TestHelpers.Layout(Words(40), width: 200);

        layout.Runs.Select(r => r.LineTop).Distinct().Count().Should().BeGreaterThan(3);
    }

    [Fact]
    public void LongHeading_Wraps()
    {
        var layout = TestHelpers.Layout("# " + Words(20), width: 200);

        layout.Runs.Select(r => r.LineTop).Distinct().Count().Should().BeGreaterThan(1);
        layout.Runs.Max(r => r.Bounds.Right).Should().BeLessThanOrEqualTo(200);
    }

    [Fact]
    public void Heading_UsesLargerBoldStyle()
    {
        var layout = TestHelpers.Layout("# Title\n\nplain");

        var heading = layout.Runs.Single(r => r.Text == "Title");
        var plain = layout.Runs.Single(r => r.Text == "plain");

        (heading.Style.Flags & InlineStyle.Bold).Should().Be(InlineStyle.Bold);
        heading.Style.Scale.Should().BeGreaterThan(plain.Style.Scale);
        heading.Bounds.Height.Should().BeGreaterThan(plain.Bounds.Height);
    }

    [Fact]
    public void EmptyHeading_TakesNoSpace()
    {
        var withHeading = TestHelpers.Layout("#\n\ntext");
        var without = TestHelpers.Layout("text");

        withHeading.ContentHeight.Should().Be(without.ContentHeight);
    }

    [Fact]
    public void PunctuationAfterStyledText_DoesNotWrapAlone()
    {
        // "ab" + space + "cd:" cannot fit in 5 characters; the colon must stay glued to "cd".
        var layout = TestHelpers.Layout("ab **cd**:", width: 5 * FakeMetrics.CharWidth);

        var bold = layout.Runs.Single(r => r.Text == "cd");
        var colon = layout.Runs.Single(r => r.Text == ":");
        var first = layout.Runs.Single(r => r.Text == "ab");

        colon.LineTop.Should().Be(bold.LineTop);
        bold.LineTop.Should().BeGreaterThan(first.LineTop);
    }

    [Fact]
    public void TrailingPunctuationAfterLink_StaysWithTheLink()
    {
        var layout = TestHelpers.Layout("go [home](https://x.y).", width: 6 * FakeMetrics.CharWidth);

        var link = layout.Runs.Single(r => r.Url is not null);
        var dot = layout.Runs.Single(r => r.Text == ".");

        dot.LineTop.Should().Be(link.LineTop);
    }

    [Fact]
    public void LinkAcrossWords_IsOneContinuousRun()
    {
        var layout = TestHelpers.Layout("[the full docs](https://x.y)");

        var link = layout.Runs.Should().ContainSingle(r => r.Url != null).Which;
        link.Text.Should().Be("the full docs");
        link.Role.Should().Be(ColorRole.Link);
        (link.Style.Flags & InlineStyle.Underline).Should().Be(InlineStyle.Underline);
    }

    [Fact]
    public void StrikethroughAcrossWords_IsOneRun()
    {
        var layout = TestHelpers.Layout("~~a b c~~");

        var run = layout.Runs.Should().ContainSingle().Which;
        run.Text.Should().Be("a b c");
        (run.Style.Flags & InlineStyle.Strikethrough).Should().Be(InlineStyle.Strikethrough);
    }

    [Fact]
    public void RepeatedSpaces_Collapse()
    {
        var layout = TestHelpers.Layout("a      b");

        layout.Runs.Should().ContainSingle().Which.Text.Should().Be("a b");
    }

    [Fact]
    public void WordLongerThanLine_IsBrokenAndLosesNoCharacters()
    {
        var word = new string('x', 53);
        var layout = TestHelpers.Layout(word, width: 10 * FakeMetrics.CharWidth);

        layout.Runs.Count.Should().BeGreaterThan(5);
        layout.Runs.Max(r => r.Bounds.Right).Should().BeLessThanOrEqualTo(80);
        string.Concat(layout.Runs.Select(r => r.Text)).Should().Be(word);
    }

    [Fact]
    public void WordBreaking_NeverSplitsSurrogatePairs()
    {
        var emoji = string.Concat(Enumerable.Repeat("😀", 30));
        var layout = TestHelpers.Layout(emoji, width: 7 * FakeMetrics.CharWidth);

        foreach (var run in layout.Runs)
        {
            char.IsLowSurrogate(run.Text[0]).Should().BeFalse();
            char.IsHighSurrogate(run.Text[run.Text.Length - 1]).Should().BeFalse();
        }

        string.Concat(layout.Runs.Select(r => r.Text)).Should().Be(emoji);
    }

    [Fact]
    public void SoftBreak_FollowsPreserveLineBreaksOption()
    {
        var preserved = TestHelpers.Layout("a\nb", preserveLineBreaks: true);
        var joined = TestHelpers.Layout("a\nb", preserveLineBreaks: false);

        preserved.Runs.Select(r => r.LineTop).Distinct().Count().Should().Be(2);
        joined.Runs.Select(r => r.LineTop).Distinct().Count().Should().Be(1);
    }

    [Fact]
    public void HardBreak_AlwaysBreaks()
    {
        var layout = TestHelpers.Layout("a  \nb", preserveLineBreaks: false);

        layout.Runs.Select(r => r.LineTop).Distinct().Count().Should().Be(2);
    }

    [Fact]
    public void BulletList_PlacesMarkersLeftOfIndentedText()
    {
        var layout = TestHelpers.Layout("- one\n- two");

        var markers = layout.Runs.Where(r => r.Text == "•").ToList();
        markers.Should().HaveCount(2);

        var one = layout.Runs.Single(r => r.Text == "one");
        one.Bounds.Left.Should().BeGreaterThan(markers[0].Bounds.Right);
        markers[0].LineTop.Should().Be(one.LineTop);
    }

    [Fact]
    public void OrderedList_NumbersFromStart()
    {
        var layout = TestHelpers.Layout("5. a\n6. b");

        layout.Runs.Should().Contain(r => r.Text == "5.");
        layout.Runs.Should().Contain(r => r.Text == "6.");
    }

    [Fact]
    public void NestedList_IsIndentedFurther()
    {
        var layout = TestHelpers.Layout("- outer\n  - inner");

        var outer = layout.Runs.Single(r => r.Text == "outer");
        var inner = layout.Runs.Single(r => r.Text == "inner");
        inner.Bounds.Left.Should().BeGreaterThan(outer.Bounds.Left);
        inner.LineTop.Should().BeGreaterThan(outer.LineTop);
    }

    [Fact]
    public void TightList_IsMoreCompactThanLooseList()
    {
        var tight = TestHelpers.Layout("- a\n- b\n- c");
        var loose = TestHelpers.Layout("- a\n\n- b\n\n- c");

        tight.ContentHeight.Should().BeLessThan(loose.ContentHeight);
    }

    [Fact]
    public void BlockQuote_DrawsBarAndIndentsText()
    {
        var layout = TestHelpers.Layout("> quoted text");

        var bar = layout.Shapes.Should().ContainSingle(s => s.Kind == ShapeKind.QuoteBar).Which;
        var run = layout.Runs.Single(r => r.Text == "quoted text");
        run.Bounds.Left.Should().BeGreaterThan(bar.Bounds.Right);
        bar.Bounds.Height.Should().Be(run.LineBottom - run.LineTop);
    }

    [Fact]
    public void CodeBlock_HasBackgroundAndMonospaceRuns_WithoutWrapping()
    {
        var layout = TestHelpers.Layout("```\nshort\nthis line is much longer than the available width\n```", width: 120);

        layout.Shapes.Should().ContainSingle(s => s.Kind == ShapeKind.CodeBackground);
        layout.Runs.Should().OnlyContain(r => (r.Style.Flags & InlineStyle.Code) != 0);
        layout.Runs.Select(r => r.LineTop).Distinct().Count().Should().Be(2);

        // The overflow is reachable through horizontal scrolling.
        layout.ContentWidth.Should().BeGreaterThan(120);
    }

    [Fact]
    public void CodeBlock_FitsWithoutScroll_WhenLinesAreShort()
    {
        var layout = TestHelpers.Layout("```\nshort\n```", width: 300);

        layout.ContentWidth.Should().BeLessThanOrEqualTo(300);
    }

    [Fact]
    public void InlineCode_IsHighlightedMonospace()
    {
        var layout = TestHelpers.Layout("use `code` here");

        var code = layout.Runs.Single(r => r.Text == "code");
        code.Highlight.Should().BeTrue();
        (code.Style.Flags & InlineStyle.Code).Should().Be(InlineStyle.Code);
        layout.Runs.Where(r => r.Text != "code").Should().OnlyContain(r => !r.Highlight);
    }

    [Fact]
    public void ThematicBreak_DrawsRuleSpanningTheColumn()
    {
        var layout = TestHelpers.Layout("a\n\n---\n\nb", width: 200, inset: 10);

        var rule = layout.Shapes.Should().ContainSingle(s => s.Kind == ShapeKind.Rule).Which;
        rule.Bounds.Left.Should().Be(10);
        rule.Bounds.Right.Should().Be(190);
    }

    [Fact]
    public void Runs_AreOrderedByLine_AndFindFirstRunIsConservative()
    {
        var layout = TestHelpers.Layout(string.Join("\n\n", Enumerable.Range(0, 80).Select(i => $"paragraph {i}")), width: 300);

        layout.Runs.Select(r => r.LineTop).Should().BeInAscendingOrder();

        foreach (var top in new[] { 0, 37, 500, 1500, 100000 })
        {
            var first = layout.FindFirstRun(top);
            for (var i = 0; i < first; i++)
            {
                layout.Runs[i].LineBottom.Should().BeLessThanOrEqualTo(top);
            }
        }
    }

    [Fact]
    public void MixedHeights_AreBottomAlignedOnTheLine()
    {
        var layout = TestHelpers.Layout("# Big small");

        var runs = layout.Runs.ToList();
        runs.Select(r => r.Bounds.Bottom).Distinct().Count().Should().Be(1);
    }

    [Fact]
    public void ContentHeight_GrowsWithContent()
    {
        var shortLayout = TestHelpers.Layout("# one");
        var longLayout = TestHelpers.Layout("# one\n\n# two\n\n# three\n\n# four");

        longLayout.ContentHeight.Should().BeGreaterThan(shortLayout.ContentHeight);
    }
}
