namespace DevDeck.Core
{
    /// <summary>How a line of output should be read, and so how the log colours it.</summary>
    /// <remarks>
    ///  V2 declared this at the top of LogPanel.cs, next to the control that renders it. It is a
    ///  plain enum with no UI in it, and ProcessRunner - which has no UI either - takes one in
    ///  every call, so it moved here rather than dragging a WinForms file across with it.
    /// </remarks>
    internal enum LogLevel
    {
        Info,
        Success,
        Warning,
        Error
    }
}
