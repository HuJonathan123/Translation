# Hotkey Clipboard Prototype

This WinForms prototype validates the desktop capture flow described in `skill.md` step 2:

- registers global hotkey `Ctrl+Alt+T` via `RegisterHotKey`
- sends `Ctrl+C` via `SendInput`
- waits briefly for clipboard update
- reads selected text through `Clipboard.GetText`
- optionally restores the previous clipboard content
- keeps focus in the selected application and queues one rapid follow-up hotkey press

## Build

```powershell
.\src\HotkeyClipboardPrototype\build.ps1
```

The executable is written to:

```text
src\HotkeyClipboardPrototype\bin\HotkeyClipboardPrototype.exe
```

## Manual validation

1. Run the executable.
2. Select text in Notepad, a browser, VS Code, or another Windows app.
3. Press `Ctrl+Alt+T`.
4. Release `Ctrl`; the prototype immediately sends `Ctrl+C` to the focused app.
5. Confirm the prototype window shows the selected text.
6. Try a non-text selection or empty selection and confirm it reports that no text was detected.
