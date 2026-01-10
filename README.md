# CUIBOX (MVP)

Windows TUI app that embeds external terminal processes using Ratatui + crossterm + cockpit (ConPTY/PTY).

## Run

1) Edit `profiles.json` to define the processes.
2) Start:

```
cargo run
```

## Controls

- `Ctrl+T`: new tab (round-robin profile)
- `Ctrl+W`: close active tab
- `Ctrl+N`: next tab
- `Ctrl+P`: previous tab
- `Ctrl+Q`: quit
- All other keys go to the active process.

## profiles.json format

Object form only:

```
{
  "profiles": [
    {
      "name": "PowerShell",
      "command": "powershell.exe",
      "args": ["-NoLogo"],
      "cwd": "C:\\",
      "env": {"FOO": "bar"}
    }
  ]
}
```

Fields:
- `name` (string, required)
- `command` (string, optional; when omitted, default shell is used)
- `args` (array of string, optional)
- `cwd` (string, optional)
- `env` (object string->string, optional)

## Notes / Limitations

- Tab title shows profile id as `(#id)` when available.
- The UI is tab-only (no split panes).
