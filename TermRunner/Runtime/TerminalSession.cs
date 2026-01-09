using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TermRunner.Pty;
using TermRunner.Terminal;

namespace TermRunner.Runtime;

public sealed class TerminalSession : IDisposable
{
    private readonly string _sessionId;
    private readonly TaskCompletionSource<PtySize> _readyTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private ITerminalFrontend? _frontend;
    private ConPtyHost? _pty;
    private ProcessHost? _process;
    private PtyIoPump? _io;
    private int _started;
    private bool _disposed;

    public TerminalSession(string sessionId)
    {
        _sessionId = sessionId;
    }

    public void AttachFrontend(ITerminalFrontend frontend)
    {
        _frontend = frontend;
        frontend.Ready += OnReady;
        frontend.Input += OnInput;
        frontend.Resized += OnResized;
    }

    public async Task StartAsync()
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
        {
            return;
        }

        var size = await WaitReadyAsync().ConfigureAwait(false);
        _pty = ConPtyHost.Create(size.Cols, size.Rows);
        _process = ProcessHost.StartAttachedToPty(_pty.PseudoConsole, "cmd.exe", Environment.CurrentDirectory);

        _io = new PtyIoPump(_pty.OutputReader, _pty.InputWriter);
        _io.Output += OnOutput;
        _io.Start();
    }

    private async Task<PtySize> WaitReadyAsync()
    {
        var completed = await Task.WhenAny(_readyTcs.Task, Task.Delay(TimeSpan.FromSeconds(10))).ConfigureAwait(false);
        if (completed != _readyTcs.Task)
        {
            throw new TimeoutException("Terminal frontend did not become ready in time.");
        }

        return await _readyTcs.Task.ConfigureAwait(false);
    }

    private void OnReady(object? sender, TerminalReadyEventArgs e)
    {
        if (e.SessionId != _sessionId)
        {
            return;
        }

        _readyTcs.TrySetResult(new PtySize(e.Cols, e.Rows));
    }

    private void OnInput(object? sender, TerminalInputEventArgs e)
    {
        if (e.SessionId != _sessionId)
        {
            return;
        }

        SendTextInput(e.Text);
    }

    private void OnResized(object? sender, TerminalResizeEventArgs e)
    {
        if (e.SessionId != _sessionId)
        {
            return;
        }

        _pty?.Resize(e.Cols, e.Rows);
    }

    public void SendTextInput(string text)
    {
        if (string.IsNullOrEmpty(text) || _io == null)
        {
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(text);
        _io.EnqueueInput(bytes);
    }

    private void OnOutput(object? sender, byte[] data)
    {
        if (data.Length == 0)
        {
            return;
        }

        var text = Encoding.UTF8.GetString(data);
        _frontend?.WriteOutput(text);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_frontend != null)
        {
            _frontend.Ready -= OnReady;
            _frontend.Input -= OnInput;
            _frontend.Resized -= OnResized;
        }

        _io?.Dispose();
        _process?.Terminate();
        _process?.Dispose();
        _pty?.Dispose();
    }

    private readonly struct PtySize
    {
        public PtySize(int cols, int rows)
        {
            Cols = cols;
            Rows = rows;
        }

        public int Cols { get; }
        public int Rows { get; }
    }
}
