# WinForms.Markdown

A native, lightweight, open-source Markdown control for Windows Forms. Markdown
is parsed into a small AST and rendered directly onto the control using GDI+
(`TextRenderer`) — **no web browser, no `WebView2`, and no HTML** anywhere in the
pipeline.

## Target frameworks

The library targets:

- .NET Framework 4.8 (`net48`)
- .NET 6 (`net6.0-windows`)
- .NET 8 (`net8.0-windows`)
- .NET 10 (`net10.0-windows`)

The sample application stays on a modern TFM.

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

## Repository layout

```
src/WinForms.Markdown/           # The library
samples/WinForms.Markdown.Demo/  # Runnable usage example
tests/WinForms.Markdown.Tests/   # Parser and renderer unit tests
```

## Build

```powershell
dotnet build WinForms.Markdown.slnx -c Release
dotnet test tests/WinForms.Markdown.Tests/WinForms.Markdown.Tests.csproj -c Release
dotnet pack src/WinForms.Markdown/WinForms.Markdown.csproj -c Release
```

## Licence

[MIT](LICENSE)
