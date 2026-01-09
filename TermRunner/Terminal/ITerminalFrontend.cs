using System;

namespace TermRunner.Terminal;

public interface ITerminalFrontend
{
    string SessionId { get; }

    event EventHandler<TerminalReadyEventArgs>? Ready;
    event EventHandler<TerminalInputEventArgs>? Input;
    event EventHandler<TerminalResizeEventArgs>? Resized;

    void WriteOutput(string text);
}

public sealed class TerminalReadyEventArgs : EventArgs
{
    public TerminalReadyEventArgs(string sessionId, int cols, int rows)
    {
        SessionId = sessionId;
        Cols = cols;
        Rows = rows;
    }

    public string SessionId { get; }
    public int Cols { get; }
    public int Rows { get; }
}

public sealed class TerminalInputEventArgs : EventArgs
{
    public TerminalInputEventArgs(string sessionId, string text)
    {
        SessionId = sessionId;
        Text = text;
    }

    public string SessionId { get; }
    public string Text { get; }
}

public sealed class TerminalResizeEventArgs : EventArgs
{
    public TerminalResizeEventArgs(string sessionId, int cols, int rows)
    {
        SessionId = sessionId;
        Cols = cols;
        Rows = rows;
    }

    public string SessionId { get; }
    public int Cols { get; }
    public int Rows { get; }
}
