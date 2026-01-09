using System;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace TermRunner.Terminal;

public sealed class TerminalWebViewHost : ITerminalFrontend, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly WebView2 _webView;
    private readonly Dispatcher _dispatcher;
    private bool _navigationCompleted;
    private bool _initPending;
    private bool _disposed;

    public TerminalWebViewHost(WebView2 webView, string sessionId)
    {
        _webView = webView;
        _dispatcher = webView.Dispatcher;
        SessionId = sessionId;
    }

    public string SessionId { get; }

    public event EventHandler<TerminalReadyEventArgs>? Ready;
    public event EventHandler<TerminalInputEventArgs>? Input;
    public event EventHandler<TerminalResizeEventArgs>? Resized;

    public async Task InitializeAsync(string webRootPath)
    {
        if (_disposed)
        {
            return;
        }

        await _webView.EnsureCoreWebView2Async().ConfigureAwait(true);
        var core = _webView.CoreWebView2;
        if (core == null)
        {
            return;
        }

        core.Settings.IsWebMessageEnabled = true;
        core.WebMessageReceived += OnWebMessageReceived;
        core.NavigationCompleted += OnNavigationCompleted;
        core.SetVirtualHostNameToFolderMapping("termrunner.local", webRootPath, CoreWebView2HostResourceAccessKind.Allow);
        core.Navigate("https://termrunner.local/index.html");
    }

    public void SendInit()
    {
        _initPending = true;
        TrySendInit();
    }

    public void WriteOutput(string text)
    {
        if (_disposed || string.IsNullOrEmpty(text))
        {
            return;
        }

        var message = new OutgoingMessage<OutputPayload>
        {
            Type = "output",
            Payload = new OutputPayload
            {
                SessionId = SessionId,
                Text = text
            }
        };

        PostMessage(message);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_webView.CoreWebView2 != null)
        {
            _webView.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
            _webView.CoreWebView2.NavigationCompleted -= OnNavigationCompleted;
        }
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        _navigationCompleted = true;
        TrySendInit();
    }

    private void TrySendInit()
    {
        if (!_initPending || !_navigationCompleted || _disposed)
        {
            return;
        }

        var message = new OutgoingMessage<InitPayload>
        {
            Type = "init",
            Payload = new InitPayload
            {
                SessionId = SessionId,
                Theme = "dark",
                FontSize = 13,
                ScrollbackLines = 5000
            }
        };

        PostMessage(message);
        _initPending = false;
    }

    private void PostMessage<TPayload>(OutgoingMessage<TPayload> message)
    {
        var json = JsonSerializer.Serialize(message, JsonOptions);
        _ = _dispatcher.InvokeAsync(() =>
        {
            if (_webView.CoreWebView2 != null && !_disposed)
            {
                _webView.CoreWebView2.PostWebMessageAsJson(json);
            }
        });
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        IncomingMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<IncomingMessage>(e.WebMessageAsJson, JsonOptions);
        }
        catch (JsonException)
        {
            return;
        }

        if (message?.Type == null)
        {
            return;
        }

        switch (message.Type)
        {
            case "ready":
                HandleReady(message.Payload);
                break;
            case "input":
                HandleInput(message.Payload);
                break;
            case "resize":
                HandleResize(message.Payload);
                break;
        }
    }

    private void HandleReady(JsonElement payload)
    {
        var data = payload.Deserialize<ReadyPayload>(JsonOptions);
        if (data == null || data.SessionId != SessionId)
        {
            return;
        }

        Ready?.Invoke(this, new TerminalReadyEventArgs(data.SessionId, data.Cols, data.Rows));
    }

    private void HandleInput(JsonElement payload)
    {
        var data = payload.Deserialize<InputPayload>(JsonOptions);
        if (data == null || data.SessionId != SessionId)
        {
            return;
        }

        Input?.Invoke(this, new TerminalInputEventArgs(data.SessionId, data.Text ?? string.Empty));
    }

    private void HandleResize(JsonElement payload)
    {
        var data = payload.Deserialize<ResizePayload>(JsonOptions);
        if (data == null || data.SessionId != SessionId)
        {
            return;
        }

        Resized?.Invoke(this, new TerminalResizeEventArgs(data.SessionId, data.Cols, data.Rows));
    }

    private sealed class IncomingMessage
    {
        public string? Type { get; set; }
        public JsonElement Payload { get; set; }
    }

    private sealed class OutgoingMessage<TPayload>
    {
        public string Type { get; set; } = string.Empty;
        public TPayload Payload { get; set; } = default!;
    }

    private sealed class InitPayload
    {
        public string SessionId { get; set; } = string.Empty;
        public string Theme { get; set; } = "dark";
        public int FontSize { get; set; }
        public int ScrollbackLines { get; set; }
    }

    private sealed class OutputPayload
    {
        public string SessionId { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }

    private sealed class ReadyPayload
    {
        public string SessionId { get; set; } = string.Empty;
        public int Cols { get; set; }
        public int Rows { get; set; }
    }

    private sealed class InputPayload
    {
        public string SessionId { get; set; } = string.Empty;
        public string? Text { get; set; }
    }

    private sealed class ResizePayload
    {
        public string SessionId { get; set; } = string.Empty;
        public int Cols { get; set; }
        public int Rows { get; set; }
    }
}
