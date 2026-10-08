using AwesomeAssertions;
using WinForms.Markdown.Markdown;
using Xunit;

namespace WinForms.Markdown.Tests;

public class MarkdownInlineTests
{
    [Theory]
    [InlineData("snake_case_name")]
    [InlineData("2 * 3 * 4")]
    [InlineData("~5 minutes or ~10")]
    [InlineData("a * b")]
    [InlineData("a_b_c and d_e_f")]
    public void Parse_LooseOrIntrawordDelimiters_AreLiteral(string input)
    {
        var inlines = TestHelpers.ParseInlines(input);

        inlines.Should().ContainSingle().Which.Should().BeOfType<TextInline>().Which.Text.Should().Be(input);
    }

    [Fact]
    public void Parse_UnderscoreAroundWholeWord_IsItalic()
    {
        var flat = TestHelpers.Flatten(TestHelpers.ParseInlines("an _emphasised_ word"));

        flat.Should().Contain(x => x.Style == InlineStyle.Italic && x.Text == "emphasised");
    }

    [Fact]
    public void Parse_ItalicContainingBold_NestsCorrectly()
    {
        var flat = TestHelpers.Flatten(TestHelpers.ParseInlines("*italic **bold***"));

        flat.Should().Contain(x => x.Style == InlineStyle.Italic && x.Text == "italic ");
        flat.Should().Contain(x => x.Style == (InlineStyle.Italic | InlineStyle.Bold) && x.Text == "bold");
        flat.Should().NotContain(x => x.Text.Contains("*"));
    }

    [Fact]
    public void Parse_TripleDelimiters_AreBoldAndItalic()
    {
        var flat = TestHelpers.Flatten(TestHelpers.ParseInlines("***both***"));

        flat.Should().ContainSingle().Which.Should().Be((InlineStyle.Bold | InlineStyle.Italic, "both"));
    }

    [Fact]
    public void Parse_UnmatchedDelimiter_StaysLiteral()
    {
        var inlines = TestHelpers.ParseInlines("**not closed and *also not");

        inlines.Should().ContainSingle().Which.Should().BeOfType<TextInline>().Which.Text.Should().Be("**not closed and *also not");
    }

    [Fact]
    public void Parse_BackslashEscapes_ProduceLiteralCharacters()
    {
        var inlines = TestHelpers.ParseInlines("\\*not italic\\* and \\[not a link\\](x)");

        inlines.Should().ContainSingle().Which.Should().BeOfType<TextInline>().Which.Text.Should().Be("*not italic* and [not a link](x)");
    }

    [Fact]
    public void Parse_CodeSpan_DisablesFormattingInside()
    {
        var inlines = TestHelpers.ParseInlines("use `snake_case_*x*` here");

        inlines.Should().HaveCount(3);
        inlines[1].Should().BeOfType<CodeInline>().Which.Text.Should().Be("snake_case_*x*");
    }

    [Fact]
    public void Parse_DoubleBacktickSpan_CanContainBacktick()
    {
        var inlines = TestHelpers.ParseInlines("``a`b``");

        inlines.Should().ContainSingle().Which.Should().BeOfType<CodeInline>().Which.Text.Should().Be("a`b");
    }

    [Fact]
    public void Parse_UnclosedBacktick_IsLiteral()
    {
        var inlines = TestHelpers.ParseInlines("a `b");

        inlines.Should().ContainSingle().Which.Should().BeOfType<TextInline>().Which.Text.Should().Be("a `b");
    }

    [Fact]
    public void Parse_Entities_AreDecoded()
    {
        var inlines = TestHelpers.ParseInlines("a &amp; b &lt;c&gt; &#65;&#x42; &bogus;");

        inlines.Should().ContainSingle().Which.Should().BeOfType<TextInline>().Which.Text.Should().Be("a & b <c> AB &bogus;");
    }

    [Fact]
    public void Parse_TwoTrailingSpaces_MakeHardBreak()
    {
        var inlines = TestHelpers.ParseInlines("one  \ntwo");

        inlines[0].Should().BeOfType<TextInline>().Which.Text.Should().Be("one");
        inlines[1].Should().BeOfType<LineBreakInline>().Which.IsHard.Should().BeTrue();
    }

    [Fact]
    public void Parse_TrailingBackslash_MakesHardBreak()
    {
        var inlines = TestHelpers.ParseInlines("one\\\ntwo");

        inlines[1].Should().BeOfType<LineBreakInline>().Which.IsHard.Should().BeTrue();
    }

    [Fact]
    public void Parse_BrTag_MakesHardBreak()
    {
        var inlines = TestHelpers.ParseInlines("one<br>two");

        inlines[1].Should().BeOfType<LineBreakInline>().Which.IsHard.Should().BeTrue();
    }

    [Fact]
    public void Parse_Image_FallsBackToLinkWithAltText()
    {
        var inlines = TestHelpers.ParseInlines("![logo](https://example.com/logo.png)");

        var link = inlines.Should().ContainSingle().Which.Should().BeOfType<LinkInline>().Which;
        link.Url.Should().Be("https://example.com/logo.png");
        link.Children.Should().ContainSingle().Which.Should().BeOfType<TextInline>().Which.Text.Should().Be("logo");
    }

    [Fact]
    public void Parse_LinkUrlWithBalancedParentheses_IsKeptWhole()
    {
        var inlines = TestHelpers.ParseInlines("[wiki](https://en.wikipedia.org/wiki/Foo_(bar))");

        inlines.Should().ContainSingle().Which.Should().BeOfType<LinkInline>().Which.Url.Should().Be("https://en.wikipedia.org/wiki/Foo_(bar)");
    }

    [Fact]
    public void Parse_LinkLabel_CanBeFormatted()
    {
        var inlines = TestHelpers.ParseInlines("[**bold** label](https://example.com)");

        var link = inlines.Should().ContainSingle().Which.Should().BeOfType<LinkInline>().Which;
        TestHelpers.Flatten(link.Children).Should().Contain(x => x.Style == InlineStyle.Bold && x.Text == "bold");
    }

    [Fact]
    public void Parse_LinkWithTitle_DropsTitle()
    {
        var inlines = TestHelpers.ParseInlines("[a](https://example.com \"Title\")");

        inlines.Should().ContainSingle().Which.Should().BeOfType<LinkInline>().Which.Url.Should().Be("https://example.com");
    }

    [Fact]
    public void Parse_NonWebAutolink_IsLiteral()
    {
        var inlines = TestHelpers.ParseInlines("a <not a link> b");

        inlines.Should().ContainSingle().Which.Should().BeOfType<TextInline>().Which.Text.Should().Be("a <not a link> b");
    }
}
