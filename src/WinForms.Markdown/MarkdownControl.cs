using System.ComponentModel;
using System.Diagnostics;
using WinForms.Markdown.Markdown;
using WinForms.Markdown.Rendering;

namespace WinForms.Markdown;

/// <summary>
/// A native, browser-free Markdown viewer for Windows Forms. Markdown is parsed
/// into a lightweight AST and rendered directly onto the control with GDI text
/// measurement and drawing — no <c>WebBrowser</c>/<c>WebView2</c> and no HTML
/// is involved. Scrolling is provided by the built-in <see cref="AutoScroll"/>
/// vertical scrollbar.
/// </summary>
[DefaultProperty(nameof(MarkdownText))]
public class MarkdownControl : ScrollableControl
{
    private const TextFormatFlags DrawFlags =
        TextFormatFlags.NoPadding |
        TextFormatFlags.NoPrefix |
        TextFormatFlags.Left |
        TextFormatFlags.Top;

    private string _markdownText = string.Empty;
    private Color _linkColor = Color.FromArgb(0, 102, 204);
    private MarkdownRenderer? _renderer;
    private MarkdownDocument _document = MarkdownDocument.Empty;
    private MarkdownLayout? _layout;

    /// <summary>Initialises a new instance of the <see cref="MarkdownControl"/> class.</summary>
    public MarkdownControl()
    {
        SetStyle(
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw,
            true);

        AutoScroll = true;
        BackColor = SystemColors.Window;
        ForeColor = SystemColors.WindowText;
        Padding = new Padding(10, 8, 10, 8);
    }

    /// <summary>
    /// Gets or sets the raw Markdown text. Setting this re-parses the document
    /// and repaints the control.
    /// </summary>
    [DefaultValue("")]
    [Localizable(true)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public string MarkdownText
    {
        get => _markdownText;
        set
        {
            value ??= string.Empty;
            if (string.Equals(_markdownText, value, StringComparison.Ordinal))
            {
                return;
            }

            _markdownText = value;
            RebuildLayout();
            Invalidate();
        }
    }

    /// <summary>The parsed AST for the current <see cref="MarkdownText"/>.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public MarkdownDocument Document => _document;

    /// <summary>Gets or sets the colour used to paint hyperlinks.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public Color LinkColor
    {
        get => _linkColor;
        set
        {
            if (_linkColor == value)
            {
                return;
            }

            _linkColor = value;
            RebuildLayout();
            Invalidate();
        }
    }

    private MarkdownRenderer Renderer
    {
        get
        {
            _renderer ??= new MarkdownRenderer(Font);
            return _renderer;
        }
    }

    /// <summary>Re-parses the current text and recomputes layout and scroll bounds.</summary>
    private void RebuildLayout()
    {
        _document = MarkdownParser.Parse(_markdownText);

        // The native vertical scrollbar reduces ClientSize.Width when it appears,
        // which raises Resize and triggers a re-layout at the narrower width, so we
        // simply always wrap to the current client width (no manual reservation).
        var availableWidth = Math.Max(1, ClientSize.Width - Padding.Horizontal);
        _layout = Renderer.Layout(_document, availableWidth, ForeColor, LinkColor, Padding);
        AutoScrollMinSize = new Size(0, _layout.ContentHeight);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var g = e.Graphics;

        using (var background = new SolidBrush(BackColor))
        {
            g.FillRectangle(background, ClientRectangle);
        }

        if (_layout is null || _layout.Runs.Count == 0)
        {
            return;
        }

        // GDI text (TextRenderer.DrawText) ignores the Graphics world transform, so
        // a TranslateTransform for scrolling has no effect and leaves text painted
        // at unscrolled coordinates once the scroll position moves away from the
        // origin. Offset each run manually instead.
        var scroll = AutoScrollPosition;
        foreach (var run in _layout.Runs)
        {
            var location = new Point(run.Bounds.X + scroll.X, run.Bounds.Y + scroll.Y);
            TextRenderer.DrawText(g, run.Text, run.Font, location, run.Color, DrawFlags);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Cursor = HitTest(e.Location) is null ? Cursors.Default : Cursors.Hand;
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        Cursor = Cursors.Default;
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        var url = HitTest(e.Location)?.Url;
        if (url is not null)
        {
            OpenLink(url);
        }
    }

    private TextRun? HitTest(Point clientPoint)
    {
        if (_layout is null)
        {
            return null;
        }

        var point = new Point(clientPoint.X - AutoScrollPosition.X, clientPoint.Y - AutoScrollPosition.Y);
        foreach (var run in _layout.Runs)
        {
            if (run.Url is not null && run.Bounds.Contains(point))
            {
                return run;
            }
        }

        return null;
    }

    private static void OpenLink(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !IsWebScheme(uri))
        {
            return;
        }

        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }

    private static bool IsWebScheme(Uri uri)
    {
        return uri.Scheme == Uri.UriSchemeHttp
            || uri.Scheme == Uri.UriSchemeHttps
            || uri.Scheme == Uri.UriSchemeMailto;
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        RebuildLayout();
        Invalidate();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        ResetRenderer();
        RebuildLayout();
        Invalidate();
    }

    protected override void OnForeColorChanged(EventArgs e)
    {
        base.OnForeColorChanged(e);
        RebuildLayout();
        Invalidate();
    }

    protected override void OnPaddingChanged(EventArgs e)
    {
        base.OnPaddingChanged(e);
        RebuildLayout();
        Invalidate();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        RebuildLayout();
    }

    private void ResetRenderer()
    {
        _renderer?.Dispose();
        _renderer = null;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ResetRenderer();
        }

        base.Dispose(disposing);
    }
}
