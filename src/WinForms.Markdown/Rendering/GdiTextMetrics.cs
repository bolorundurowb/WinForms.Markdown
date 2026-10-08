using WinForms.Markdown.Markdown;

namespace WinForms.Markdown.Rendering;

/// <summary>
/// <see cref="ITextMetrics"/> backed by GDI (<see cref="TextRenderer"/>). It owns the
/// <see cref="Font"/> instances it creates and caches measurements, since layout
/// measures the same words many times (especially while resizing).
/// </summary>
internal sealed class GdiTextMetrics : ITextMetrics, IDisposable
{
    private const TextFormatFlags MeasureFlags =
        TextFormatFlags.NoPadding |
        TextFormatFlags.NoPrefix |
        TextFormatFlags.Left |
        TextFormatFlags.Top |
        TextFormatFlags.SingleLine;

    // Keeps the width cache from growing without bound on huge, varied documents.
    private const int MaxCachedWidths = 50_000;

    private static FontFamily? _codeFamily;

    private readonly Font _baseFont;
    private readonly Dictionary<TextStyle, Font> _fonts = new();
    private readonly Dictionary<TextStyle, int> _heights = new();
    private readonly Dictionary<(string Text, TextStyle Style), int> _widths = new();

    public GdiTextMetrics(Font baseFont)
        => _baseFont = baseFont ?? throw new ArgumentNullException(nameof(baseFont));

    public int MeasureWidth(string text, TextStyle style)
    {
        if (text.Length == 0)
        {
            return 0;
        }

        var key = (text, style);
        if (_widths.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var width = TextRenderer.MeasureText(text, GetFont(style), new Size(int.MaxValue, int.MaxValue), MeasureFlags).Width;

        if (_widths.Count >= MaxCachedWidths)
        {
            _widths.Clear();
        }

        _widths[key] = width;
        return width;
    }

    public int LineHeight(TextStyle style)
    {
        if (!_heights.TryGetValue(style, out var height))
        {
            height = TextRenderer.MeasureText("Xg", GetFont(style), new Size(int.MaxValue, int.MaxValue), MeasureFlags).Height;
            _heights[style] = height;
        }

        return height;
    }

    /// <summary>Returns the (cached, owned) font for <paramref name="style"/>.</summary>
    public Font GetFont(TextStyle style)
    {
        if (!_fonts.TryGetValue(style, out var font))
        {
            font = CreateFont(style);
            _fonts[style] = font;
        }

        return font;
    }

    private Font CreateFont(TextStyle style)
    {
        var fontStyle = ToFontStyle(style.Flags);
        var family = (style.Flags & InlineStyle.Code) != 0 ? CodeFamily : _baseFont.FontFamily;
        var size = _baseFont.Size * style.Scale;

        try
        {
            return new Font(family, size, fontStyle, _baseFont.Unit);
        }
        catch (ArgumentException)
        {
            // The family doesn't provide the requested bold/italic face; fall back to
            // regular and keep only the decorations GDI can synthesise.
            var decorations = fontStyle & (FontStyle.Underline | FontStyle.Strikeout);
            return new Font(family, size, decorations, _baseFont.Unit);
        }
    }

    private static FontFamily CodeFamily
    {
        get
        {
            if (_codeFamily is null)
            {
                foreach (var name in new[] { "Consolas", "Courier New" })
                {
                    try
                    {
                        _codeFamily = new FontFamily(name);
                        break;
                    }
                    catch (ArgumentException)
                    {
                        // Not installed; try the next one.
                    }
                }

                _codeFamily ??= FontFamily.GenericMonospace;
            }

            return _codeFamily;
        }
    }

    private static FontStyle ToFontStyle(InlineStyle style)
    {
        var result = FontStyle.Regular;
        if ((style & InlineStyle.Bold) != 0)
        {
            result |= FontStyle.Bold;
        }

        if ((style & InlineStyle.Italic) != 0)
        {
            result |= FontStyle.Italic;
        }

        if ((style & InlineStyle.Underline) != 0)
        {
            result |= FontStyle.Underline;
        }

        if ((style & InlineStyle.Strikethrough) != 0)
        {
            result |= FontStyle.Strikeout;
        }

        return result;
    }

    public void Dispose()
    {
        foreach (var font in _fonts.Values)
        {
            font.Dispose();
        }

        _fonts.Clear();
        _heights.Clear();
        _widths.Clear();
    }
}
