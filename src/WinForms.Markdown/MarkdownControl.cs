using System.ComponentModel;
using System.Diagnostics;
using WinForms.Markdown.Markdown;
using WinForms.Markdown.Rendering;

namespace WinForms.Markdown;

/// <summary>
/// A native, browser-free Markdown viewer for Windows Forms. Markdown is parsed
/// into a lightweight AST, laid out with GDI text measurement and painted directly onto
/// the control — no <c>WebBrowser</c>/<c>WebView2</c> and no HTML is involved.
/// Scrolling is provided by the built-in <see cref="ScrollableControl.AutoScroll"/> scrollbars
/// (the horizontal one only appears when a code block is wider than the control).
/// </summary>
[DefaultProperty(nameof(MarkdownText))]
[DefaultEvent(nameof(LinkClicked))]
public class MarkdownControl : ScrollableControl
{
    private const TextFormatFlags DrawFlags =
        TextFormatFlags.NoPadding |
        TextFormatFlags.NoPrefix |
        TextFormatFlags.Left |
        TextFormatFlags.Top;

    private string _markdownText = string.Empty;
    private Color _linkColor = Color.FromArgb(0, 102, 204);
    private Color _codeBackColor = Color.Empty;
    private Color _ruleColor = Color.Empty;
    private bool _preserveLineBreaks = true;

    private GdiTextMetrics? _metrics;
    private MarkdownDocument _document = MarkdownDocument.Empty;
    private MarkdownLayout? _layout;
    private int _layoutWidth = -1;

    /// <summary>Initialises a new instance of the <see cref="MarkdownControl"/> class.</summary>
    public MarkdownControl()
    {
        SetStyle(
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable,
            true);

        TabStop = true;
        AutoScroll = true;
        BackColor = SystemColors.Window;
        ForeColor = SystemColors.WindowText;
        Padding = new Padding(10, 8, 10, 8);
    }

    /// <summary>Raised when the user clicks a hyperlink.</summary>
    [Category("Action")]
    [Description("Occurs when a hyperlink is clicked. Set Handled to true to suppress the default behaviour.")]
    public event EventHandler<MarkdownLinkClickedEventArgs>? LinkClicked;

    /// <summary>
    /// Gets or sets the raw Markdown text. Setting this re-parses the document
    /// and repaints the control.
    /// </summary>
    [Category("Appearance")]
    [Description("The Markdown source to display.")]
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
            RebuildDocument();
        }
    }

    /// <summary>The parsed AST for the current <see cref="MarkdownText"/>.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public MarkdownDocument Document => _document;

    /// <summary>Gets or sets the colour used to paint hyperlinks.</summary>
    [Category("Appearance")]
    [Description("The colour used to paint hyperlinks.")]
    [DefaultValue(typeof(Color), "0, 102, 204")]
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
            Invalidate();
        }
    }

    /// <summary>
    /// Gets or sets the background of code spans and code blocks. When left empty a
    /// subtle tint derived from <see cref="Control.BackColor"/> and <see cref="Control.ForeColor"/> is used.
    /// </summary>
    [Category("Appearance")]
    [Description("The background of code spans and code blocks. Empty derives it from BackColor/ForeColor.")]
    [DefaultValue(typeof(Color), "")]
    public Color CodeBackColor
    {
        get => _codeBackColor;
        set
        {
            if (_codeBackColor == value)
            {
                return;
            }

            _codeBackColor = value;
            Invalidate();
        }
    }

    /// <summary>
    /// Gets or sets the colour of horizontal rules and block-quote bars. When left empty
    /// it is derived from <see cref="Control.BackColor"/> and <see cref="Control.ForeColor"/>.
    /// </summary>
    [Category("Appearance")]
    [Description("The colour of horizontal rules and block-quote bars. Empty derives it from BackColor/ForeColor.")]
    [DefaultValue(typeof(Color), "")]
    public Color RuleColor
    {
        get => _ruleColor;
        set
        {
            if (_ruleColor == value)
            {
                return;
            }

            _ruleColor = value;
            Invalidate();
        }
    }

    /// <summary>
    /// Gets or sets whether a single newline inside a paragraph starts a new line
    /// (<c>true</c>, the default) or is treated as a space, as CommonMark does (<c>false</c>).
    /// A line ending in two spaces or a backslash always breaks.
    /// </summary>
    [Category("Behavior")]
    [Description("Whether a single newline inside a paragraph starts a new line.")]
    [DefaultValue(true)]
    public bool PreserveLineBreaks
    {
        get => _preserveLineBreaks;
        set
        {
            if (_preserveLineBreaks == value)
            {
                return;
            }

            _preserveLineBreaks = value;
            ApplyLayout(force: true);
        }
    }

    /// <summary>Raises the <see cref="LinkClicked"/> event.</summary>
    protected virtual void OnLinkClicked(MarkdownLinkClickedEventArgs e) => LinkClicked?.Invoke(this, e);

    // ------------------------------------------------------------------
    // Parsing and layout
    // ------------------------------------------------------------------

    /// <summary>Re-parses the text, then lays it out.</summary>
    private void RebuildDocument()
    {
        _document = MarkdownParser.Parse(_markdownText);
        ApplyLayout(force: true);
    }

    /// <summary>
    /// Lays the parsed document out for the current client width and updates the scroll
    /// bounds. Parsing is not repeated; layout is skipped when the width is unchanged.
    /// </summary>
    private void ApplyLayout(bool force)
    {
        // ClientSize already excludes a visible vertical scrollbar. Watching
        // OnClientSizeChanged (rather than OnResize) re-wraps the text when the scrollbar
        // appears or disappears without the control itself being resized.
        var width = ClientSize.Width;
        if (!force && _layout is not null && width == _layoutWidth)
        {
            return;
        }

        _layoutWidth = width;
        _metrics ??= new GdiTextMetrics(Font);

        var padding = Padding;
        var layout = MarkdownLayoutEngine.Layout(
            _document,
            _metrics,
            Math.Max(1, width),
            new LayoutInsets(padding.Left, padding.Top, padding.Right, padding.Bottom),
            _preserveLineBreaks);

        _layout = layout;

        // Only ask for horizontal scrolling when something (a wide code block) overflows.
        AutoScrollMinSize = new Size(layout.ContentWidth > width ? layout.ContentWidth : 0, layout.ContentHeight);
        Invalidate();
    }

    private void ResetMetrics()
    {
        _metrics?.Dispose();
        _metrics = null;
    }

    // ------------------------------------------------------------------
    // Painting
    // ------------------------------------------------------------------

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var g = e.Graphics;
        var clip = e.ClipRectangle;

        using (var background = new SolidBrush(BackColor))
        {
            g.FillRectangle(background, clip);
        }

        var layout = _layout;
        var metrics = _metrics;
        if (layout is null || metrics is null || (layout.Runs.Count == 0 && layout.Shapes.Count == 0))
        {
            return;
        }

        // GDI text (TextRenderer.DrawText) ignores the Graphics world transform, so a
        // TranslateTransform for scrolling would have no effect. Offset each item manually.
        var scroll = AutoScrollPosition;

        var codeBack = CodeBackColor.IsEmpty ? Blend(BackColor, ForeColor, 0.07f) : CodeBackColor;
        var rule = RuleColor.IsEmpty ? Blend(BackColor, ForeColor, 0.30f) : RuleColor;

        using (var codeBrush = new SolidBrush(codeBack))
        using (var ruleBrush = new SolidBrush(rule))
        {
            foreach (var shape in layout.Shapes)
            {
                var bounds = shape.Bounds;
                bounds.Offset(scroll);
                if (!bounds.IntersectsWith(clip))
                {
                    continue;
                }

                g.FillRectangle(shape.Kind == ShapeKind.CodeBackground ? codeBrush : ruleBrush, bounds);
            }

            // Runs are sorted by line, so only the lines intersecting the clip are drawn.
            var top = clip.Top - scroll.Y;
            var bottom = clip.Bottom - scroll.Y;

            for (var i = layout.FindFirstRun(top); i < layout.Runs.Count; i++)
            {
                var run = layout.Runs[i];
                if (run.LineTop > bottom)
                {
                    break;
                }

                var bounds = run.Bounds;
                bounds.Offset(scroll);
                if (!bounds.IntersectsWith(clip))
                {
                    continue;
                }

                if (run.Highlight)
                {
                    g.FillRectangle(codeBrush, Rectangle.Inflate(bounds, 1, 0));
                }

                var color = run.Role == ColorRole.Link ? LinkColor : ForeColor;
                TextRenderer.DrawText(g, run.Text, metrics.GetFont(run.Style), bounds.Location, color, DrawFlags);
            }
        }
    }

    private static Color Blend(Color from, Color to, float amount)
    {
        int Mix(int a, int b) => (int)Math.Round(a + (b - a) * amount);
        return Color.FromArgb(Mix(from.R, to.R), Mix(from.G, to.G), Mix(from.B, to.B));
    }

    // ------------------------------------------------------------------
    // Mouse and keyboard
    // ------------------------------------------------------------------

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (!Focused)
        {
            Focus();
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Cursor = HitTestLink(e.Location) is null ? Cursors.Default : Cursors.Hand;
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

        var url = HitTestLink(e.Location);
        if (url is null)
        {
            return;
        }

        var args = new MarkdownLinkClickedEventArgs(url);
        OnLinkClicked(args);
        if (!args.Handled)
        {
            OpenLink(url);
        }
    }

    private string? HitTestLink(Point clientPoint)
    {
        var layout = _layout;
        if (layout is null)
        {
            return null;
        }

        var scroll = AutoScrollPosition;
        var point = new Point(clientPoint.X - scroll.X, clientPoint.Y - scroll.Y);
        foreach (var run in layout.LinkRuns)
        {
            if (run.Bounds.Contains(point))
            {
                return run.Url;
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

        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception)
        {
            // No registered handler, or the shell refused. A click must never crash the host.
        }
    }

    private static bool IsWebScheme(Uri uri)
    {
        return uri.Scheme == Uri.UriSchemeHttp
            || uri.Scheme == Uri.UriSchemeHttps
            || uri.Scheme == Uri.UriSchemeMailto;
    }

    protected override bool IsInputKey(Keys keyData)
    {
        switch (keyData & Keys.KeyCode)
        {
            case Keys.Up:
            case Keys.Down:
            case Keys.Left:
            case Keys.Right:
            case Keys.PageUp:
            case Keys.PageDown:
            case Keys.Home:
            case Keys.End:
                return true;

            default:
                return base.IsInputKey(keyData);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        var line = _metrics is null ? Font.Height : _metrics.LineHeight(new TextStyle(1f, InlineStyle.Regular));
        var page = Math.Max(line, ClientSize.Height - line);

        switch (e.KeyCode)
        {
            case Keys.Up:
                ScrollBy(0, -line);
                break;
            case Keys.Down:
                ScrollBy(0, line);
                break;
            case Keys.Left:
                ScrollBy(-line, 0);
                break;
            case Keys.Right:
                ScrollBy(line, 0);
                break;
            case Keys.PageUp:
                ScrollBy(0, -page);
                break;
            case Keys.PageDown:
                ScrollBy(0, page);
                break;
            case Keys.Home:
                ScrollTo(null, 0);
                break;
            case Keys.End:
                ScrollTo(null, int.MaxValue);
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    // AutoScrollPosition reports negative offsets but its setter takes positive ones.
    private void ScrollBy(int dx, int dy)
    {
        var current = AutoScrollPosition;
        AutoScrollPosition = new Point(Math.Max(0, -current.X + dx), Math.Max(0, -current.Y + dy));
    }

    private void ScrollTo(int? x, int y)
    {
        var current = AutoScrollPosition;
        AutoScrollPosition = new Point(x ?? -current.X, y);
    }

    // ------------------------------------------------------------------
    // Property-change plumbing
    // ------------------------------------------------------------------

    protected override void OnClientSizeChanged(EventArgs e)
    {
        base.OnClientSizeChanged(e);
        ApplyLayout(force: false);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyLayout(force: false);
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        ResetMetrics();
        ApplyLayout(force: true);
    }

    protected override void OnPaddingChanged(EventArgs e)
    {
        base.OnPaddingChanged(e);
        ApplyLayout(force: true);
    }

    // Colours are applied at paint time, so a repaint is all they need.
    protected override void OnForeColorChanged(EventArgs e)
    {
        base.OnForeColorChanged(e);
        Invalidate();
    }

    protected override void OnBackColorChanged(EventArgs e)
    {
        base.OnBackColorChanged(e);
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ResetMetrics();
        }

        base.Dispose(disposing);
    }
}
