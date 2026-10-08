# WinForms.Markdown

[![Release](https://github.com/bolorundurowb/WinForms.Markdown/actions/workflows/release.yml/badge.svg)](https://github.com/bolorundurowb/WinForms.Markdown/actions/workflows/release.yml) ![.NET Framework 4.8](https://img.shields.io/badge/Framework-4.8-512BD4?logo=dotnet) ![.NET 6](https://img.shields.io/badge/.NET-6.0-512BD4?logo=dotnet) ![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet) ![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)


A native, lightweight, open-source Markdown control for Windows Forms. Markdown
is parsed into a small AST and rendered directly onto the control using GDI+
(`TextRenderer`) — **no web browser, no `WebView2`, and no HTML** anywhere in the
pipeline.

## Target frameworks

The library targets `net48`, `net6.0-windows`, `net8.0-windows` and `net10.0-windows`.
The sample application stays on a modern TFM.

## Features

- Renders Markdown natively with `System.Drawing` / GDI+.
- Vertical scrolling via the built-in `AutoScroll` scrollbar, plus keyboard scrolling
  (arrows, Page Up/Down, Home/End) when the control has focus. A horizontal scrollbar
  appears only when a code block is wider than the control.
- `ForeColor`, `BackColor`, `Font`, and `Padding` act as baseline styling.
- `LinkClicked` event so the host can handle (or veto) link clicks.
- Parsing, layout and painting are cleanly separated; the parser and layout engine have no GDI dependency.
- Text is only re-parsed when `MarkdownText` changes; resizing only re-wraps, and painting only draws the visible lines.

### Supported syntax

| Element         | Markdown                                                                     |
|-----------------|------------------------------------------------------------------------------|
| Headings        | `# H1` … `###### H6`, optional closing hashes, and `Setext` (`===` / `---`)  |
| Bold            | `**bold**` or `__bold__`                                                     |
| Italic          | `*italic*` or `_italic_`                                                     |
| Underline       | `<u>underline</u>` or `~underline~`                                          |
| Strikethrough   | `~~strikethrough~~`                                                          |
| Inline code     | `` `code` `` (use double backticks to include a single one)                  |
| Code blocks     | Fenced with ```` ``` ```` or `~~~` (language tag is parsed, not highlighted) |
| Hyperlinks      | `[text](url)` and `<https://example.com>`                                    |
| Images          | `![alt](url)` renders as a link showing the alt text (no image loading)      |
| Lists           | `-`, `*`, `+` and `1.` / `1)`; nested; tight and loose                       |
| Block quotes    | `> quote`, nested, with lazy continuation                                    |
| Horizontal rule | `---`, `***` or `___`                                                        |
| Paragraphs      | Consecutive lines; a blank line starts a new paragraph                       |
| Line breaks     | Two trailing spaces, a trailing `\`, or `<br>`                               |
| Escapes         | `\*`, `\_`, `\[` … and entities such as `&amp;`, `&#65;`                     |

Emphasis follows CommonMark's flanking rules, so `snake_case_names` and `2 * 3 * 4` are left alone.

By default a single newline inside a paragraph starts a new line. Set
`PreserveLineBreaks = false` for CommonMark behaviour (a newline is a space).

**Not supported:** tables, raw HTML (other than `<u>` and `<br>`), reference-style links,
indented code blocks, task-list checkboxes, image rendering, text selection / copy,
right-to-left layout and screen-reader support.

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

### Handling links

Without a handler, absolute `http`, `https` and `mailto` links open in the default
application and everything else is ignored. To intercept links:

```csharp
markdown.LinkClicked += (_, e) =>
{
    if (e.Url.StartsWith("app://"))
    {
        Navigate(e.Url);
        e.Handled = true;   // stop the control opening it
    }
};
```

## Control properties

| Property             | Type      | Default                       | Description                                                                         |
|----------------------|-----------|-------------------------------|-------------------------------------------------------------------------------------|
| `MarkdownText`       | `string`  | `""`                          | The raw Markdown to parse and render.                                               |
| `ForeColor`          | `Color`   | `SystemColors.WindowText`     | Default text colour.                                                                |
| `BackColor`          | `Color`   | `SystemColors.Window`         | Control background colour.                                                          |
| `Font`               | `Font`    | `SystemFonts.DefaultFont`     | Base font; headings and inline styles derive from this.                             |
| `Padding`            | `Padding` | `new Padding(10, 8, 10, 8)`   | Margin around the rendered document.                                                |
| `LinkColor`          | `Color`   | `Color.FromArgb(0, 102, 204)` | Colour used for hyperlinks.                                                         |
| `CodeBackColor`      | `Color`   | `Color.Empty`                 | Background of code spans/blocks. Empty derives a tint from `BackColor`/`ForeColor`. |
| `RuleColor`          | `Color`   | `Color.Empty`                 | Colour of horizontal rules and quote bars. Empty derives it likewise.               |
| `PreserveLineBreaks` | `bool`    | `true`                        | Whether a single newline in a paragraph starts a new line.                          |
| `AutoScroll`         | `bool`    | `true`                        | Enables the built-in scrollbars.                                                    |

| Event         | Description                                                                |
|---------------|----------------------------------------------------------------------------|
| `LinkClicked` | A link was clicked. Set `e.Handled = true` to suppress the default action. |

## Licence

[MIT](LICENSE)
