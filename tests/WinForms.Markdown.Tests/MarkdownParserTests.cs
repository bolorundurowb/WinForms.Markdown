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
        MarkdownDocument document = MarkdownParser.Parse(input);

        Assert.Empty(document.Blocks);
    }

    [Fact]
    public void Parse_Headings_ExtractsLevelAndText()
    {
        MarkdownDocument document = MarkdownParser.Parse("# Hello\n\n### World");

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
        MarkdownDocument document = MarkdownParser.Parse("first\n\nsecond");

        Assert.Equal(2, document.Blocks.Count);
        Assert.IsType<ParagraphBlock>(document.Blocks[0]);
        Assert.IsType<ParagraphBlock>(document.Blocks[1]);
    }

    [Fact]
    public void Parse_Paragraph_JoinsConsecutiveLines()
    {
        MarkdownDocument document = MarkdownParser.Parse("line one\nline two");

        var paragraph = Assert.IsType<ParagraphBlock>(Assert.Single(document.Blocks));
        var text = Assert.IsType<TextInline>(Assert.Single(paragraph.Inlines));

        Assert.Equal("line one\nline two", text.Text);
    }

    [Fact]
    public void Parse_InlineFormatting_ProducesExpectedStyles()
    {
        MarkdownDocument document = MarkdownParser.Parse("a **b** *c* <u>d</u> ~e~ ~~f~~ __g__ _h_");

        var paragraph = Assert.IsType<ParagraphBlock>(Assert.Single(document.Blocks));
        List<(InlineStyle Style, string Text)> flat = Flatten(paragraph.Inlines);

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
        MarkdownDocument document = MarkdownParser.Parse("**bold *and italic***");

        var paragraph = Assert.IsType<ParagraphBlock>(Assert.Single(document.Blocks));
        List<(InlineStyle Style, string Text)> flat = Flatten(paragraph.Inlines);

        Assert.Equal((InlineStyle.Bold | InlineStyle.Italic, "and italic"), flat.First(x => x.Text == "and italic"));
    }

    private static List<(InlineStyle Style, string Text)> Flatten(IReadOnlyList<Inline> inlines)
    {
        var result = new List<(InlineStyle, string)>();

        void Walk(IReadOnlyList<Inline> items, InlineStyle style)
        {
            foreach (Inline inline in items)
            {
                switch (inline)
                {
                    case TextInline text:
                        result.Add((style, text.Text));
                        break;

                    case FormattedInline formatted:
                        Walk(formatted.Children, style | formatted.Style);
                        break;
                }
            }
        }

        Walk(inlines, InlineStyle.Regular);
        return result;
    }
}
