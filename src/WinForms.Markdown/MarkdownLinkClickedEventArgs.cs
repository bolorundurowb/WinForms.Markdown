namespace WinForms.Markdown;

/// <summary>Data for <see cref="MarkdownControl.LinkClicked"/>.</summary>
public sealed class MarkdownLinkClickedEventArgs : EventArgs
{
    public MarkdownLinkClickedEventArgs(string url) => Url = url;

    /// <summary>The link destination exactly as written in the Markdown.</summary>
    public string Url { get; }

    /// <summary>
    /// Set to <c>true</c> to stop the control from opening the link itself. Without a
    /// handler the control opens absolute <c>http</c>, <c>https</c> and <c>mailto</c>
    /// links in the default application and ignores everything else.
    /// </summary>
    public bool Handled { get; set; }
}
