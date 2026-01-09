# TermRunner MVP

WPF + WebView2 + xterm.js + ConPTY minimal terminal runner.

## Build

```
dotnet build .\TermRunner.sln
```

## Run

```
dotnet run --project .\TermRunner\TermRunner.csproj
```

A single tab opens and launches `cmd.exe`. Try `dir` or `ver` in the terminal.

## Notes

- xterm.js assets are vendored under `TermRunner\TerminalWeb` (xterm 5.3.0, xterm-addon-fit 0.8.0).
- WebView2 Runtime is required on the machine (installed by default on Windows 11).
