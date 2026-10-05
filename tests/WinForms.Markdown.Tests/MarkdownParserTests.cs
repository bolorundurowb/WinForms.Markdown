using AwesomeAssertions;
using WinForms.Markdown.Markdown;
using Xunit;

namespace WinForms.Markdown.Tests;

public class MarkdownParserTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_EmptyOrWhitespace_ReturnsEmptyDocument(string? input)
    {
        var document = MarkdownParser.Parse(input);

        document.Blocks.Should().BeEmpty();
    }

    [Fact]
    public void Parse_Headings_ExtractsLevelAndText()
    {
        var document = MarkdownParser.Parse("# Hello\n\n### World");

        var h1 = document.Blocks[0].Should().BeOfType<HeadingBlock>().Which;
        var h3 = document.Blocks[1].Should().BeOfType<HeadingBlock>().Which;

        h1.Level.Should().Be(1);
        h3.Level.Should().Be(3);
        h1.Inlines[0].Should().BeOfType<TextInline>().Which.Text.Should().Be("Hello");
        h3.Inlines[0].Should().BeOfType<TextInline>().Which.Text.Should().Be("World");
    }

    [Fact]
    public void Parse_BlankLine_SeparatesParagraphs()
    {
        var document = MarkdownParser.Parse("first\n\nsecond");

        document.Blocks.Should().HaveCount(2);
        document.Blocks[0].Should().BeOfType<ParagraphBlock>();
        document.Blocks[1].Should().BeOfType<ParagraphBlock>();
    }

    [Fact]
    public void Parse_Paragraph_JoinsConsecutiveLines()
    {
        var document = MarkdownParser.Parse("line one\nline two");

        var paragraph = document.Blocks.Should().ContainSingle().Which.Should().BeOfType<ParagraphBlock>().Which;
        var text = paragraph.Inlines.Should().ContainSingle().Which.Should().BeOfType<TextInline>().Which;

        text.Text.Should().Be("line one\nline two");
    }

    [Fact]
    public void Parse_InlineFormatting_ProducesExpectedStyles()
    {
        var document = MarkdownParser.Parse("a **b** *c* <u>d</u> ~e~ ~~f~~ __g__ _h_");

        var paragraph = document.Blocks.Should().ContainSingle().Which.Should().BeOfType<ParagraphBlock>().Which;
        var flat = Flatten(paragraph.Inlines);

        flat.First(x => x.Text == "b").Should().Be((InlineStyle.Bold, "b"));
        flat.First(x => x.Text == "c").Should().Be((InlineStyle.Italic, "c"));
        flat.First(x => x.Text == "d").Should().Be((InlineStyle.Underline, "d"));
        flat.First(x => x.Text == "e").Should().Be((InlineStyle.Underline, "e"));
        flat.First(x => x.Text == "f").Should().Be((InlineStyle.Strikethrough, "f"));
        flat.First(x => x.Text == "g").Should().Be((InlineStyle.Bold, "g"));
        flat.First(x => x.Text == "h").Should().Be((InlineStyle.Italic, "h"));
    }

    [Fact]
    public void Parse_NestedFormatting_CombinesStyles()
    {
        var document = MarkdownParser.Parse("**bold *and italic***");

        var paragraph = document.Blocks.Should().ContainSingle().Which.Should().BeOfType<ParagraphBlock>().Which;
        var flat = Flatten(paragraph.Inlines);

        flat.First(x => x.Text == "and italic").Should().Be((InlineStyle.Bold | InlineStyle.Italic, "and italic"));
    }

    [Fact]
    public void Parse_InlineLink_CapturesLabelAndUrl()
    {
        var document = MarkdownParser.Parse("See [the docs](https://example.com/docs).");

        var paragraph = document.Blocks.Should().ContainSingle().Which.Should().BeOfType<ParagraphBlock>().Which;
        var link = paragraph.Inlines[1].Should().BeOfType<LinkInline>().Which;

        link.Url.Should().Be("https://example.com/docs");
        var text = link.Children.Should().ContainSingle().Which.Should().BeOfType<TextInline>().Which;
        text.Text.Should().Be("the docs");
    }

    [Fact]
    public void Parse_Autolink_UsesUrlAsLabel()
    {
        var document = MarkdownParser.Parse("Visit <https://example.com>.");

        var paragraph = document.Blocks.Should().ContainSingle().Which.Should().BeOfType<ParagraphBlock>().Which;
        var link = paragraph.Inlines[1].Should().BeOfType<LinkInline>().Which;

        link.Url.Should().Be("https://example.com");
        var text = link.Children.Should().ContainSingle().Which.Should().BeOfType<TextInline>().Which;
        text.Text.Should().Be("https://example.com");
    }

    private static List<(InlineStyle Style, string Text)> Flatten(IReadOnlyList<Inline> inlines)
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
}
