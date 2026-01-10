# CUIBOX (MVP)

Windows TUI app that embeds external terminal processes using Ratatui + crossterm + cockpit (ConPTY/PTY).

## Run

1) Edit `profiles.json` to define the processes.
2) Start:

```
cargo run
```

## Controls

- `Ctrl+N`: next tab
- `Ctrl+P`: previous tab
- `Ctrl+Q`: quit
- All other keys go to the active process.

## profiles.json format

You can use either of these formats:

Object form:

```
{
  "profiles": [
    {
      "name": "PowerShell",
      "command": "powershell.exe",
      "args": ["-NoLogo"],
      "cwd": "C:\\",
      "env": {"FOO": "bar"},
      "scrollback": 10000
    }
  ]
}
```

Array form:

```
[
  {
    "name": "CMD",
    "command": "cmd.exe",
    "args": []
  }
]
```

Fields:
- `name` (string, required)
- `command` (string, required)
- `args` (array of string, optional)
- `cwd` (string, optional)
- `env` (object string->string, optional)
- `scrollback` (number, optional)

## Notes / Limitations

- `cockpit` currently supports up to 4 panes; this MVP limits profiles to 4.
- The UI is tab-only (no split panes).