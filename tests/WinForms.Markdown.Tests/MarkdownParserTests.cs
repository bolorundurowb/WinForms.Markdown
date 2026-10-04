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

        Assert.Empty(document.Blocks);
    }

    [Fact]
    public void Parse_Headings_ExtractsLevelAndText()
    {
        var document = MarkdownParser.Parse("# Hello\n\n### World");

        var h1 = Assert.IsType<HeadingBlock>(document.Blocks[0]);
        var h3 = Assert.IsType<HeadingBlock>(document.Blocks[1]);

        Assert.Equal(1, h1.Level);
        Assert.Equal(3, h3.Level);
        Assert.Equal("Hello", Assert.IsType<TextInline>(h1.Inlines[0]).Text);
        Assert.Equal("World", Assert.IsType<TextInline>(h3.Inlines[0]).Text);
    }

    [Fact]
    public void Parse_BlankLine_SeparatesParagraphs()
    {
        var document = MarkdownParser.Parse("first\n\nsecond");

        Assert.Equal(2, document.Blocks.Count);
        Assert.IsType<ParagraphBlock>(document.Blocks[0]);
        Assert.IsType<ParagraphBlock>(document.Blocks[1]);
    }

    [Fact]
    public void Parse_Paragraph_JoinsConsecutiveLines()
    {
        var document = MarkdownParser.Parse("line one\nline two");

        var paragraph = Assert.IsType<ParagraphBlock>(Assert.Single(document.Blocks));
        var text = Assert.IsType<TextInline>(Assert.Single(paragraph.Inlines));

        Assert.Equal("line one\nline two", text.Text);
    }

    [Fact]
    public void Parse_InlineFormatting_ProducesExpectedStyles()
    {
        var document = MarkdownParser.Parse("a **b** *c* <u>d</u> ~e~ ~~f~~ __g__ _h_");

        var paragraph = Assert.IsType<ParagraphBlock>(Assert.Single(document.Blocks));
        var flat = Flatten(paragraph.Inlines);

        Assert.Equal((InlineStyle.Bold, "b"), flat.First(x => x.Text == "b"));
        Assert.Equal((InlineStyle.Italic, "c"), flat.First(x => x.Text == "c"));
        Assert.Equal((InlineStyle.Underline, "d"), flat.First(x => x.Text == "d"));
        Assert.Equal((InlineStyle.Underline, "e"), flat.First(x => x.Text == "e"));
        Assert.Equal((InlineStyle.Strikethrough, "f"), flat.First(x => x.Text == "f"));
        Assert.Equal((InlineStyle.Bold, "g"), flat.First(x => x.Text == "g"));
        Assert.Equal((InlineStyle.Italic, "h"), flat.First(x => x.Text == "h"));
    }

    [Fact]
    public void Parse_NestedFormatting_CombinesStyles()
    {
        var document = MarkdownParser.Parse("**bold *and italic***");

        var paragraph = Assert.IsType<ParagraphBlock>(Assert.Single(document.Blocks));
        var flat = Flatten(paragraph.Inlines);

        Assert.Equal((InlineStyle.Bold | InlineStyle.Italic, "and italic"), flat.First(x => x.Text == "and italic"));
    }

    [Fact]
    public void Parse_InlineLink_CapturesLabelAndUrl()
    {
        var document = MarkdownParser.Parse("See [the docs](https://example.com/docs).");

        var paragraph = Assert.IsType<ParagraphBlock>(Assert.Single(document.Blocks));
        var link = Assert.IsType<LinkInline>(paragraph.Inlines[1]);

        Assert.Equal("https://example.com/docs", link.Url);
        Assert.Equal("the docs", Assert.IsType<TextInline>(Assert.Single(link.Children)).Text);
    }

    [Fact]
    public void Parse_Autolink_UsesUrlAsLabel()
    {
        var document = MarkdownParser.Parse("Visit <https://example.com>.");

        var paragraph = Assert.IsType<ParagraphBlock>(Assert.Single(document.Blocks));
        var link = Assert.IsType<LinkInline>(paragraph.Inlines[1]);

        Assert.Equal("https://example.com", link.Url);
        Assert.Equal("https://example.com", Assert.IsType<TextInline>(Assert.Single(link.Children)).Text);
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
