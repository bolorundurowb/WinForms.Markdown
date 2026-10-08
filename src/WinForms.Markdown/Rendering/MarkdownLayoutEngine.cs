using System.Drawing;
using System.Globalization;
using System.Text;
using WinForms.Markdown.Markdown;

namespace WinForms.Markdown.Rendering;

/// <summary>
/// Lays a <see cref="MarkdownDocument"/> out into positioned <see cref="TextRun"/>s and
/// <see cref="LayoutShape"/>s. The engine is pure: all text measurement goes through
/// <see cref="ITextMetrics"/>, so it has no dependency on GDI and can be tested anywhere.
/// </summary>
internal sealed class MarkdownLayoutEngine
{
    // Relative heading sizes (multiplier of the base font size), indexed by level.
    private static readonly float[] HeadingScales = { 0f, 1.75f, 1.5f, 1.25f, 1.1f, 1.0f, 0.9f };

    private const int QuoteBarWidth = 3;
    private const int QuoteGap = 9;
    private const int CodePadding = 6;

    // Nested containers never shrink the text column below this width.
    private const int MinContentWidth = 24;

    private readonly ITextMetrics _metrics;
    private readonly bool _preserveLineBreaks;
    private readonly List<TextRun> _runs = new();
    private readonly List<LayoutShape> _shapes = new();
    private readonly TextStyle _baseStyle = new(1f, InlineStyle.Regular);
    private readonly int _baseLine;
    private readonly int _blockSpacing;
    private readonly int _tightSpacing;
    private int _maxRight;
    private int _maxLineHeight;

    private MarkdownLayoutEngine(ITextMetrics metrics, bool preserveLineBreaks)
    {
        _metrics = metrics;
        _preserveLineBreaks = preserveLineBreaks;
        _baseLine = Math.Max(1, metrics.LineHeight(_baseStyle));
        _blockSpacing = Math.Max(4, (int)Math.Round(_baseLine * 0.5));
        _tightSpacing = Math.Max(2, _baseLine / 6);
    }

    /// <summary>
    /// Lays <paramref name="document"/> out for a control that is <paramref name="width"/>
    /// pixels wide (the full client width; the insets are applied here).
    /// </summary>
    /// <param name="document">The parsed document.</param>
    /// <param name="metrics">Text measurement.</param>
    /// <param name="width">The full width available, including the insets.</param>
    /// <param name="insets">Space reserved around the content.</param>
    /// <param name="preserveLineBreaks">
    /// When <c>true</c> a single newline inside a paragraph starts a new line; when
    /// <c>false</c> it is a space, as in CommonMark.
    /// </param>
    public static MarkdownLayout Layout(
        MarkdownDocument document,
        ITextMetrics metrics,
        int width,
        LayoutInsets insets,
        bool preserveLineBreaks)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        if (metrics is null)
        {
            throw new ArgumentNullException(nameof(metrics));
        }

        var engine = new MarkdownLayoutEngine(metrics, preserveLineBreaks);

        var left = insets.Left;
        var right = Math.Max(left + 1, width - insets.Right);
        var y = engine.LayoutBlocks(document.Blocks, left, right, insets.Top, engine._blockSpacing, 0);

        // Runs are emitted block by block; sort (stably) so painting can binary-search by line.
        var runs = new List<TextRun>(engine._runs.OrderBy(r => r.LineTop));
        return new MarkdownLayout(
            runs,
            engine._shapes,
            engine._maxRight + insets.Right,
            y + insets.Bottom,
            engine._maxLineHeight);
    }

    // ------------------------------------------------------------------
    // Blocks
    // ------------------------------------------------------------------

    private int LayoutBlocks(IReadOnlyList<Block> blocks, int left, int right, int y, int spacing, int depth)
    {
        var first = true;
        foreach (var block in blocks)
        {
            var start = first ? y : y + spacing;
            var end = LayoutBlock(block, left, right, start, depth);

            // Blocks that produced nothing (an empty heading) take no space.
            if (end > start)
            {
                y = end;
                first = false;
            }
        }

        return y;
    }

    private int LayoutBlock(Block block, int left, int right, int y, int depth)
    {
        switch (block)
        {
            case HeadingBlock heading:
            {
                var level = Math.Min(6, Math.Max(1, heading.Level));
                var style = new TextStyle(HeadingScales[level], InlineStyle.Bold);
                return LayoutInlines(heading.Inlines, left, right, y, style);
            }

            case ParagraphBlock paragraph:
                return LayoutInlines(paragraph.Inlines, left, right, y, _baseStyle);

            case CodeBlock code:
                return LayoutCodeBlock(code, left, right, y);

            case BlockQuoteBlock quote:
                return LayoutBlockQuote(quote, left, right, y, depth);

            case ListBlock list:
                return LayoutList(list, left, right, y, depth);

            case ThematicBreakBlock:
                _shapes.Add(new LayoutShape(ShapeKind.Rule, new Rectangle(left, y + _baseLine / 2, Math.Max(1, right - left), 1)));
                return y + _baseLine;

            default:
                return y;
        }
    }

    private int LayoutCodeBlock(CodeBlock code, int left, int right, int y)
    {
        var style = new TextStyle(1f, InlineStyle.Code);
        var lineHeight = Math.Max(1, _metrics.LineHeight(style));
        var textLeft = left + CodePadding;
        var cursor = y + CodePadding;
        var maxTextRight = textLeft;

        foreach (var line in code.Lines)
        {
            var text = line.Replace("\t", "    ");
            if (text.Length > 0)
            {
                var width = _metrics.MeasureWidth(text, style);
                AddRun(new TextRun(text, style, ColorRole.Text, new Rectangle(textLeft, cursor, width, lineHeight), cursor, cursor + lineHeight, null, false));
                maxTextRight = Math.Max(maxTextRight, textLeft + width);
            }

            cursor += lineHeight;
        }

        if (code.Lines.Count == 0)
        {
            cursor += lineHeight;
        }

        var bottom = cursor + CodePadding;

        // The panel spans the column, but grows (and so can be scrolled to) for long lines.
        var panelRight = Math.Max(right, maxTextRight + CodePadding);
        _shapes.Add(new LayoutShape(ShapeKind.CodeBackground, new Rectangle(left, y, panelRight - left, bottom - y)));
        NoteRight(panelRight);
        return bottom;
    }

    private int LayoutBlockQuote(BlockQuoteBlock quote, int left, int right, int y, int depth)
    {
        var innerLeft = ClampIndent(left, QuoteBarWidth + QuoteGap, right);
        var end = LayoutBlocks(quote.Blocks, innerLeft, right, y, _blockSpacing, depth + 1);

        if (end > y)
        {
            _shapes.Add(new LayoutShape(ShapeKind.QuoteBar, new Rectangle(left, y, QuoteBarWidth, end - y)));
        }

        return end;
    }

    private int LayoutList(ListBlock list, int left, int right, int y, int depth)
    {
        var count = list.Items.Count;
        if (count == 0)
        {
            return y;
        }

        var markers = new string[count];
        var markerWidth = 0;
        for (var i = 0; i < count; i++)
        {
            markers[i] = list.IsOrdered
                ? (list.Start + i).ToString(CultureInfo.InvariantCulture) + "."
                : (depth % 2 == 0 ? "•" : "–");
            markerWidth = Math.Max(markerWidth, _metrics.MeasureWidth(markers[i], _baseStyle));
        }

        var gap = Math.Max(6, _metrics.MeasureWidth(" ", _baseStyle));
        var indent = Math.Max(markerWidth + gap, _baseLine);
        var contentLeft = ClampIndent(left, indent, right);
        var spacing = list.IsTight ? _tightSpacing : _blockSpacing;

        for (var i = 0; i < count; i++)
        {
            var start = i == 0 ? y : y + spacing;
            var end = LayoutBlocks(list.Items[i].Blocks, contentLeft, right, start, spacing, depth + 1);

            // An empty item still occupies a line so its marker is visible.
            if (end <= start)
            {
                end = start + _baseLine;
            }

            var width = _metrics.MeasureWidth(markers[i], _baseStyle);
            var x = Math.Max(left, contentLeft - gap - width);
            AddRun(new TextRun(markers[i], _baseStyle, ColorRole.Text, new Rectangle(x, start, width, _baseLine), start, start + _baseLine, null, false));

            y = end;
        }

        return y;
    }

    private static int ClampIndent(int left, int indent, int right)
        => Math.Min(left + indent, Math.Max(left, right - MinContentWidth));

    // ------------------------------------------------------------------
    // Inlines
    // ------------------------------------------------------------------

    private readonly struct Atom
    {
        public Atom(string text, TextStyle style, ColorRole role, string? url)
        {
            Text = text;
            Style = style;
            Role = role;
            Url = url;
            IsBreak = false;
            IsHard = false;
        }

        public Atom(bool isHard, TextStyle style)
        {
            Text = string.Empty;
            Style = style;
            Role = ColorRole.Text;
            Url = null;
            IsBreak = true;
            IsHard = isHard;
        }

        public string Text { get; }

        public TextStyle Style { get; }

        public ColorRole Role { get; }

        public string? Url { get; }

        public bool IsBreak { get; }

        public bool IsHard { get; }
    }

    private int LayoutInlines(IReadOnlyList<Inline> inlines, int left, int right, int y, TextStyle blockStyle)
    {
        var atoms = new List<Atom>();
        Flatten(inlines, blockStyle, ColorRole.Text, null, atoms);
        if (atoms.Count == 0)
        {
            return y;
        }

        var flow = new Flow(this, left, Math.Max(1, right - left), y, blockStyle);
        foreach (var atom in atoms)
        {
            flow.Add(atom);
        }

        return flow.Finish();
    }

    private static void Flatten(IReadOnlyList<Inline> inlines, TextStyle style, ColorRole role, string? url, List<Atom> atoms)
    {
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case TextInline text:
                    if (text.Text.Length > 0)
                    {
                        atoms.Add(new Atom(text.Text, style, role, url));
                    }

                    break;

                case CodeInline code:
                    if (code.Text.Length > 0)
                    {
                        atoms.Add(new Atom(code.Text, style.With(InlineStyle.Code), role, url));
                    }

                    break;

                case FormattedInline formatted:
                    Flatten(formatted.Children, style.With(formatted.Style), role, url, atoms);
                    break;

                case LinkInline link:
                    Flatten(link.Children, style.With(InlineStyle.Underline), ColorRole.Link, link.Url, atoms);
                    break;

                case LineBreakInline lineBreak:
                    atoms.Add(new Atom(lineBreak.IsHard, style));
                    break;
            }
        }
    }

    private void AddRun(TextRun run)
    {
        _runs.Add(run);
        _maxLineHeight = Math.Max(_maxLineHeight, run.LineBottom - run.LineTop);
        NoteRight(run.Bounds.Right);
    }

    private void NoteRight(int x)
    {
        if (x > _maxRight)
        {
            _maxRight = x;
        }
    }

    /// <summary>One measured piece of text: a word fragment or a single space.</summary>
    private readonly struct Piece
    {
        public Piece(string text, TextStyle style, ColorRole role, string? url, int width)
        {
            Text = text;
            Style = style;
            Role = role;
            Url = url;
            Width = width;
        }

        public string Text { get; }

        public TextStyle Style { get; }

        public ColorRole Role { get; }

        public string? Url { get; }

        public int Width { get; }

        public bool SameFormat(Piece other)
            => Style == other.Style && Role == other.Role && string.Equals(Url, other.Url, StringComparison.Ordinal);
    }

    /// <summary>
    /// Word-wraps a stream of inline atoms. Text is split into unbreakable <em>chunks</em>
    /// (consecutive non-space characters, even across style changes, so <c>**Note**:</c>
    /// never wraps between the bold text and the colon) separated by single, collapsible
    /// spaces. A chunk wider than the line is broken at character level.
    /// </summary>
    private sealed class Flow
    {
        private readonly MarkdownLayoutEngine _engine;
        private readonly int _left;
        private readonly int _available;
        private readonly int _startY;
        private readonly int _minHeight;
        private readonly List<Piece> _line = new();
        private readonly List<Piece> _chunk = new();
        private readonly StringBuilder _word = new();
        private int _y;
        private int _lineWidth;
        private int _chunkWidth;
        private bool _hasPendingSpace;
        private Atom _pendingSpace;

        public Flow(MarkdownLayoutEngine engine, int left, int available, int y, TextStyle blockStyle)
        {
            _engine = engine;
            _left = left;
            _available = available;
            _y = y;
            _startY = y;
            _minHeight = Math.Max(1, engine._metrics.LineHeight(blockStyle));
        }

        public void Add(Atom atom)
        {
            if (atom.IsBreak)
            {
                AddBreak(atom);
                return;
            }

            foreach (var c in atom.Text)
            {
                if (IsBreakingSpace(c))
                {
                    EndPiece(atom);
                    FlushChunk();
                    if (!_hasPendingSpace)
                    {
                        _pendingSpace = atom;
                        _hasPendingSpace = true;
                    }
                }
                else
                {
                    _word.Append(c);
                }
            }

            EndPiece(atom);
        }

        public int Finish()
        {
            FlushChunk();
            Commit();
            return _y;
        }

        private static bool IsBreakingSpace(char c) => char.IsWhiteSpace(c) && c != ' ';

        private void AddBreak(Atom atom)
        {
            FlushChunk();

            if (!atom.IsHard && !_engine._preserveLineBreaks)
            {
                // A soft break is just a space.
                if (!_hasPendingSpace)
                {
                    _pendingSpace = atom;
                    _hasPendingSpace = true;
                }

                return;
            }

            _hasPendingSpace = false;
            if (_line.Count > 0)
            {
                Commit();
            }
            else if (_y > _startY)
            {
                // Consecutive breaks leave a blank line.
                _y += _minHeight;
            }
        }

        private void EndPiece(Atom atom)
        {
            if (_word.Length == 0)
            {
                return;
            }

            var text = _word.ToString();
            _word.Clear();

            var width = _engine._metrics.MeasureWidth(text, atom.Style);
            _chunk.Add(new Piece(text, atom.Style, atom.Role, atom.Url, width));
            _chunkWidth += width;
        }

        private void FlushChunk()
        {
            if (_chunk.Count == 0)
            {
                return;
            }

            if (_line.Count > 0)
            {
                var spaceWidth = 0;
                Piece space = default;
                if (_hasPendingSpace)
                {
                    spaceWidth = _engine._metrics.MeasureWidth(" ", _pendingSpace.Style);
                    space = new Piece(" ", _pendingSpace.Style, _pendingSpace.Role, _pendingSpace.Url, spaceWidth);
                }

                if (_lineWidth + spaceWidth + _chunkWidth <= _available)
                {
                    if (_hasPendingSpace)
                    {
                        _line.Add(space);
                        _lineWidth += spaceWidth;
                    }

                    AppendChunkToLine();
                    return;
                }

                Commit();
            }

            _hasPendingSpace = false;

            if (_chunkWidth <= _available)
            {
                AppendChunkToLine();
                return;
            }

            foreach (var piece in _chunk)
            {
                AddBreaking(piece);
            }

            _chunk.Clear();
            _chunkWidth = 0;
        }

        private void AppendChunkToLine()
        {
            _line.AddRange(_chunk);
            _lineWidth += _chunkWidth;
            _chunk.Clear();
            _chunkWidth = 0;
            _hasPendingSpace = false;
        }

        /// <summary>Adds a piece that may be wider than the line, splitting it as needed.</summary>
        private void AddBreaking(Piece piece)
        {
            var text = piece.Text;
            var width = piece.Width;

            while (true)
            {
                var remaining = _available - _lineWidth;
                if (width <= remaining)
                {
                    _line.Add(new Piece(text, piece.Style, piece.Role, piece.Url, width));
                    _lineWidth += width;
                    return;
                }

                var fit = FitCount(text, piece.Style, remaining);
                if (fit <= 0)
                {
                    if (_line.Count > 0)
                    {
                        Commit();
                        continue;
                    }

                    // Even one character is wider than the line: force progress.
                    fit = char.IsHighSurrogate(text[0]) && text.Length > 1 ? 2 : 1;
                }

                if (fit >= text.Length)
                {
                    _line.Add(new Piece(text, piece.Style, piece.Role, piece.Url, width));
                    _lineWidth += width;
                    return;
                }

                var head = text.Substring(0, fit);
                var headWidth = _engine._metrics.MeasureWidth(head, piece.Style);
                _line.Add(new Piece(head, piece.Style, piece.Role, piece.Url, headWidth));
                _lineWidth += headWidth;
                Commit();

                text = text.Substring(fit);
                width = _engine._metrics.MeasureWidth(text, piece.Style);
            }
        }

        /// <summary>The largest prefix length of <paramref name="text"/> that fits in <paramref name="remaining"/> pixels.</summary>
        private int FitCount(string text, TextStyle style, int remaining)
        {
            if (remaining <= 0)
            {
                return 0;
            }

            var lo = 0;
            var hi = text.Length - 1;
            while (lo < hi)
            {
                var mid = (lo + hi + 1) / 2;
                if (_engine._metrics.MeasureWidth(text.Substring(0, mid), style) <= remaining)
                {
                    lo = mid;
                }
                else
                {
                    hi = mid - 1;
                }
            }

            // Never split a surrogate pair.
            if (lo > 0 && lo < text.Length && char.IsHighSurrogate(text[lo - 1]))
            {
                lo--;
            }

            return lo;
        }

        /// <summary>Turns the buffered line into runs and advances to the next line.</summary>
        private void Commit()
        {
            if (_line.Count == 0)
            {
                return;
            }

            var lineHeight = _minHeight;
            foreach (var piece in _line)
            {
                lineHeight = Math.Max(lineHeight, _engine._metrics.LineHeight(piece.Style));
            }

            // Adjacent pieces with the same formatting become one run, so underlines,
            // strikethrough and link hit areas are continuous across the spaces.
            var x = _left;
            var text = new StringBuilder();
            var current = _line[0];

            void EmitRun()
            {
                var runText = text.ToString();
                text.Clear();

                var width = _engine._metrics.MeasureWidth(runText, current.Style);
                var height = Math.Min(lineHeight, Math.Max(1, _engine._metrics.LineHeight(current.Style)));
                var bounds = new Rectangle(x, _y + lineHeight - height, width, height);
                var highlight = (current.Style.Flags & InlineStyle.Code) != 0;

                _engine.AddRun(new TextRun(runText, current.Style, current.Role, bounds, _y, _y + lineHeight, current.Url, highlight));
                x += width;
            }

            foreach (var piece in _line)
            {
                if (text.Length > 0 && !piece.SameFormat(current))
                {
                    EmitRun();
                }

                if (text.Length == 0)
                {
                    current = piece;
                }

                text.Append(piece.Text);
            }

            if (text.Length > 0)
            {
                EmitRun();
            }

            _y += lineHeight;
            _line.Clear();
            _lineWidth = 0;
        }
    }
}
