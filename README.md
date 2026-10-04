# WinForms.Markdown

A native, lightweight, open-source Markdown control for Windows Forms. Markdown
is parsed into a small AST and rendered directly onto the control using GDI+
(`TextRenderer`) — **no web browser, no `WebView2`, and no HTML** anywhere in the
pipeline.

## Features

- Renders Markdown natively with `System.Drawing` / GDI+.
- Vertical scrolling via the built-in `AutoScroll` scrollbar.
- `ForeColor`, `BackColor`, `Font`, and `Padding` act as baseline styling.
- Parser (AST/tokenizer) and drawing engine are cleanly separated.

### Supported syntax

| Element       | Markdown                                     |
|---------------|----------------------------------------------|
| Headings      | `# H1`, `## H2`, `### H3` (through `######`) |
| Bold          | `**bold**` or `__bold__`                     |
| Italic        | `*italic*` or `_italic_`                     |
| Underline     | `<u>underline</u>` or `~underline~`          |
| Strikethrough | `~~strikethrough~~`                          |
| Paragraphs    | Consecutive lines                            |

## Usage

Install the package, then drop a `MarkdownControl` onto a form:

```csharp
using WinForms.Markdown;

var form = new Form
{
    Text = "Hello Markdown",
    ClientSize = new Size(720, 480),
};

var markdown = new MarkdownControl
{
    Dock = DockStyle.Fill,
    MarkdownText = "# Hello\n\nThis is **bold** and *italic* text.",
};

form.Controls.Add(markdown);
Application.Run(form);
```

## Build

```powershell
dotnet build WinForms.Markdown.csproj -c Release
dotnet pack WinForms.Markdown.csproj -c Release
```

## License

[MIT](LICENSE)
