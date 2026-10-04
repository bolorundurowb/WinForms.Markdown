# WinForms.Markdown

[![Release](https://github.com/bolorundurowb/WinForms.Markdown/actions/workflows/release.yml/badge.svg)](https://github.com/bolorundurowb/WinForms.Markdown/actions/workflows/release.yml) ![.NET Framework 4.8](https://img.shields.io/badge/.NET%20Framework-4.8-512BD4?logo=dotnet) ![.NET 6](https://img.shields.io/badge/.NET-6.0-512BD4?logo=dotnet) ![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet) ![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)


A native, lightweight, open-source Markdown control for Windows Forms. Markdown
is parsed into a small AST and rendered directly onto the control using GDI+
(`TextRenderer`) — **no web browser, no `WebView2`, and no HTML** anywhere in the
pipeline.

## Target frameworks

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
| Hyperlinks    | `[text](url)` and `<https://example.com>`    |
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

## Control properties

| Property       | Type      | Default                        | Description                                                        |
|----------------|-----------|--------------------------------|--------------------------------------------------------------------|
| `MarkdownText` | `string`  | `""`                           | The raw Markdown to parse and render.                              |
| `ForeColor`    | `Color`   | `SystemColors.WindowText`      | Default text colour.                                               |
| `BackColor`    | `Color`   | `SystemColors.Window`          | Control background colour.                                         |
| `Font`         | `Font`    | `SystemFonts.DefaultFont`      | Base font; headings and inline styles derive from this.            |
| `Padding`      | `Padding` | `new Padding(10, 8, 10, 8)`    | Margin around the rendered document.                               |
| `LinkColor`    | `Color`   | `Color.FromArgb(0, 102, 204)`  | Colour used for hyperlinks.                                        |
| `AutoScroll`   | `bool`    | `true`                         | Enables the built-in vertical scrollbar.                           |

## Licence

[MIT](LICENSE)
