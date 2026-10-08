using AwesomeAssertions;
using WinForms.Markdown.Markdown;
using Xunit;

namespace WinForms.Markdown.Tests;

public class MarkdownBlockTests
{
    [Fact]
    public void Parse_BulletList_ProducesItems()
    {
        var document = MarkdownParser.Parse("- one\n- two\n- three");

        var list = document.Blocks.Should().ContainSingle().Which.Should().BeOfType<ListBlock>().Which;
        list.IsOrdered.Should().BeFalse();
        list.IsTight.Should().BeTrue();
        list.Items.Should().HaveCount(3);
    }

    [Theory]
    [InlineData("- a\n- b")]
    [InlineData("* a\n* b")]
    [InlineData("+ a\n+ b")]
    public void Parse_AllBulletMarkers_AreLists(string input)
    {
        MarkdownParser.Parse(input).Blocks.Should().ContainSingle().Which.Should().BeOfType<ListBlock>().Which.Items.Should().HaveCount(2);
    }

    [Fact]
    public void Parse_OrderedList_KeepsStartNumber()
    {
        var document = MarkdownParser.Parse("5. five\n6. six");

        var list = document.Blocks.Should().ContainSingle().Which.Should().BeOfType<ListBlock>().Which;
        list.IsOrdered.Should().BeTrue();
        list.Start.Should().Be(5);
        list.Items.Should().HaveCount(2);
    }

    [Fact]
    public void Parse_BlankLineBetweenItems_MakesListLoose()
    {
        var list = MarkdownParser.Parse("- a\n\n- b").Blocks.Should().ContainSingle().Which.Should().BeOfType<ListBlock>().Which;

        list.IsTight.Should().BeFalse();
        list.Items.Should().HaveCount(2);
    }

    [Fact]
    public void Parse_IndentedItems_NestAsSublist()
    {
        var list = MarkdownParser.Parse("- a\n  - b\n  - c\n- d").Blocks.Should().ContainSingle().Which.Should().BeOfType<ListBlock>().Which;

        list.Items.Should().HaveCount(2);
        var nested = list.Items[0].Blocks[1].Should().BeOfType<ListBlock>().Which;
        nested.Items.Should().HaveCount(2);
    }

    [Fact]
    public void Parse_OrderedItemOtherThanOne_DoesNotInterruptParagraph()
    {
        var document = MarkdownParser.Parse("text\n2. not a list");

        var paragraph = document.Blocks.Should().ContainSingle().Which.Should().BeOfType<ParagraphBlock>().Which;
        TestHelpers.Flatten(paragraph.Inlines).Should().Contain(x => x.Text == "2. not a list");
    }

    [Fact]
    public void Parse_BulletInterruptsParagraph()
    {
        var document = MarkdownParser.Parse("text\n- item");

        document.Blocks.Should().HaveCount(2);
        document.Blocks[1].Should().BeOfType<ListBlock>();
    }

    [Fact]
    public void Parse_HyphenWithoutSpace_IsNotAList()
    {
        MarkdownParser.Parse("-not a list").Blocks.Should().ContainSingle().Which.Should().BeOfType<ParagraphBlock>();
    }

    [Fact]
    public void Parse_BlockQuote_ContainsBlocks()
    {
        var quote = MarkdownParser.Parse("> hello\n> # title").Blocks.Should().ContainSingle().Which.Should().BeOfType<BlockQuoteBlock>().Which;

        quote.Blocks.Should().HaveCount(2);
        quote.Blocks[0].Should().BeOfType<ParagraphBlock>();
        quote.Blocks[1].Should().BeOfType<HeadingBlock>();
    }

    [Fact]
    public void Parse_BlockQuote_SupportsLazyContinuationAndNesting()
    {
        var document = MarkdownParser.Parse("> one\ntwo\n>> inner\n\nafter");

        document.Blocks.Should().HaveCount(2);
        var quote = document.Blocks[0].Should().BeOfType<BlockQuoteBlock>().Which;
        quote.Blocks[0].Should().BeOfType<ParagraphBlock>();
        quote.Blocks[1].Should().BeOfType<BlockQuoteBlock>();
    }

    [Fact]
    public void Parse_FencedCode_KeepsLinesVerbatim()
    {
        var code = MarkdownParser.Parse("```csharp\nvar a_b = *1*;\n\n  indented\n```").Blocks
            .Should().ContainSingle().Which.Should().BeOfType<CodeBlock>().Which;

        code.Language.Should().Be("csharp");
        code.Lines.Should().HaveCount(3);
        code.Lines[0].Should().Be("var a_b = *1*;");
        code.Lines[1].Should().Be(string.Empty);
        code.Lines[2].Should().Be("  indented");
    }

    [Fact]
    public void Parse_UnclosedFence_RunsToEndOfDocument()
    {
        var code = MarkdownParser.Parse("~~~\nabc\ndef").Blocks.Should().ContainSingle().Which.Should().BeOfType<CodeBlock>().Which;

        code.Lines.Should().HaveCount(2);
        code.Language.Should().BeNull();
    }

    [Fact]
    public void Parse_FenceInsideFence_NeedsLongerClosingFence()
    {
        var code = MarkdownParser.Parse("````\n```\ninner\n```\n````").Blocks.Should().ContainSingle().Which.Should().BeOfType<CodeBlock>().Which;

        code.Lines.Should().HaveCount(3);
    }

    [Theory]
    [InlineData("---")]
    [InlineData("***")]
    [InlineData("___")]
    [InlineData("- - -")]
    [InlineData("  * * *  ")]
    public void Parse_ThematicBreaks(string input)
    {
        MarkdownParser.Parse(input).Blocks.Should().ContainSingle().Which.Should().BeOfType<ThematicBreakBlock>();
    }

    [Fact]
    public void Parse_SetextHeadings()
    {
        var document = MarkdownParser.Parse("Title\n=====\n\nSub\n---");

        document.Blocks[0].Should().BeOfType<HeadingBlock>().Which.Level.Should().Be(1);
        document.Blocks[1].Should().BeOfType<HeadingBlock>().Which.Level.Should().Be(2);
    }

    [Fact]
    public void Parse_AtxHeading_StripsClosingHashes()
    {
        var heading = MarkdownParser.Parse("## Title ##").Blocks.Should().ContainSingle().Which.Should().BeOfType<HeadingBlock>().Which;

        heading.Level.Should().Be(2);
        heading.Inlines.Should().ContainSingle().Which.Should().BeOfType<TextInline>().Which.Text.Should().Be("Title");
    }

    [Theory]
    [InlineData("#hashtag")]
    [InlineData("####### seven")]
    public void Parse_NotAHeading_IsParagraph(string input)
    {
        MarkdownParser.Parse(input).Blocks.Should().ContainSingle().Which.Should().BeOfType<ParagraphBlock>();
    }

    [Fact]
    public void Parse_BareHash_IsEmptyHeading()
    {
        var heading = MarkdownParser.Parse("#").Blocks.Should().ContainSingle().Which.Should().BeOfType<HeadingBlock>().Which;

        heading.Inlines.Should().BeEmpty();
    }

    [Fact]
    public void Parse_ListItemContainingCodeAndQuote()
    {
        var list = MarkdownParser.Parse("1. step\n\n   ```\n   run\n   ```\n\n   > note").Blocks
            .Should().ContainSingle().Which.Should().BeOfType<ListBlock>().Which;

        var item = list.Items.Should().ContainSingle().Which;
        item.Blocks.Should().HaveCount(3);
        item.Blocks[1].Should().BeOfType<CodeBlock>();
        item.Blocks[2].Should().BeOfType<BlockQuoteBlock>();
    }
}
