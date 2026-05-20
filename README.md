# Claude Codex Terminal

Visual Studio extension that adds `Codex` and `Claude` commands to Solution Explorer context menus.

The commands launch Windows Terminal using the current user's `wt.exe` app execution alias when available:

- `Codex`: `wt.exe -p "Codex" -d "<folder>"`
- `Claude`: `wt.exe -p "Claude" -d "<folder>"`

The VSIX package is included at the repository root as `ClaudeCodexTerminal.vsix`.
