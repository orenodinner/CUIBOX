using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace TermRunner.Pty;

public sealed class PtyIoPump : IDisposable
{
    private readonly SafeFileHandle _outputReader;
    private readonly SafeFileHandle _inputWriter;
    private readonly Channel<byte[]> _inputChannel;
    private readonly CancellationTokenSource _cts = new();
    private Task? _readTask;
    private Task? _writeTask;
    private bool _disposed;

    public PtyIoPump(SafeFileHandle outputReader, SafeFileHandle inputWriter)
    {
        _outputReader = outputReader;
        _inputWriter = inputWriter;
        _inputChannel = Channel.CreateUnbounded<byte[]>();
    }

    public event EventHandler<byte[]>? Output;

    public void Start()
    {
        if (_readTask != null || _writeTask != null)
        {
            return;
        }

        _readTask = Task.Run(ReadLoop);
        _writeTask = Task.Run(WriteLoop);
    }

    public void EnqueueInput(byte[] data)
    {
        if (_disposed)
        {
            return;
        }

        _inputChannel.Writer.TryWrite(data);
    }

    private void ReadLoop()
    {
        var buffer = new byte[4096];

        while (!_cts.IsCancellationRequested)
        {
            if (!NativeMethods.ReadFile(_outputReader, buffer, buffer.Length, out var read, IntPtr.Zero))
            {
                break;
            }

            if (read > 0)
            {
                var chunk = new byte[read];
                Buffer.BlockCopy(buffer, 0, chunk, 0, read);
                Output?.Invoke(this, chunk);
            }
        }
    }

    private async Task WriteLoop()
    {
        try
        {
            while (await _inputChannel.Reader.WaitToReadAsync(_cts.Token).ConfigureAwait(false))
            {
                while (_inputChannel.Reader.TryRead(out var data))
                {
                    if (!NativeMethods.WriteFile(_inputWriter, data, data.Length, out _, IntPtr.Zero))
                    {
                        return;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cts.Cancel();
        _inputChannel.Writer.TryComplete();
        NativeMethods.CancelIoEx(_outputReader, IntPtr.Zero);

        TryWait(_readTask);
        TryWait(_writeTask);

        _cts.Dispose();
    }

    private static void TryWait(Task? task)
    {
        if (task == null)
        {
            return;
        }

        try
        {
            task.Wait(TimeSpan.FromSeconds(1));
        }
        catch
        {
        }
    }
}
