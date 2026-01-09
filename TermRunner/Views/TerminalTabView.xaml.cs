using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using TermRunner.Runtime;
using TermRunner.Terminal;

namespace TermRunner.Views;

public partial class TerminalTabView : UserControl, IDisposable
{
    private TerminalWebViewHost? _host;
    private TerminalSession? _session;
    private readonly string _sessionId = Guid.NewGuid().ToString("N");
    private bool _initialized;
    private bool _started;
    private bool _disposed;

    public TerminalTabView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        _host = new TerminalWebViewHost(TerminalWebView, _sessionId);
        _host.Ready += OnFrontendReady;

        _session = new TerminalSession(_sessionId);
        _session.AttachFrontend(_host);

        var webRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TerminalWeb");
        await _host.InitializeAsync(webRoot).ConfigureAwait(true);
        _host.SendInit();
    }

    private void OnFrontendReady(object? sender, TerminalReadyEventArgs e)
    {
        if (_started || e.SessionId != _sessionId)
        {
            return;
        }

        _started = true;
        _ = _session?.StartAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Dispose();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _session?.Dispose();
        _host?.Dispose();
        _session = null;
        _host = null;
    }
}
