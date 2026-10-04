namespace WinForms.Markdown.Demo;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

internal sealed class MainForm : Form
{
    private const string SampleMarkdown = """
        # WinForms.Markdown

        A **native**, browser-free Markdown control rendered with GDI+.

        ## Inline formatting

        - **bold** and __bold__
        - *italic* and _italic_
        - <u>underline</u> and ~underline~
        - ~~strikethrough~~
        - [WinForms.Markdown](https://github.com/bolorundurowb/WinForms.Markdown)

        ### A heading with *mixed* **styles**

        This paragraph demonstrates that `MarkdownControl` is just a control: set
        the `MarkdownText` property and the document is parsed and repainted.

        ### Headings

        # Header 1
        ## Header 2
        ### Header 3

        ### Lorem ipsum

        Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod
        tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam.
        """;

    public MainForm()
    {
        Text = "WinForms.Markdown — Demo";
        ClientSize = new Size(720, 560);
        MinimumSize = new Size(360, 240);
        Font = new Font("Segoe UI", 10f);

        var markdown = new MarkdownControl
        {
            Dock = DockStyle.Fill,
            MarkdownText = SampleMarkdown,
        };

        Controls.Add(markdown);
    }
}
