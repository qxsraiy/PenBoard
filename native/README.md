# Native Direct2D overlay prototype

This is a standalone memory/rendering prototype, not yet wired into PenBoard.

Measured on x64:

```text
Working Set: 33.5 MB
Private:     13 MB
```

Build (x64):

```powershell
call "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvars64.bat"
cl /nologo /O2 /std:c++17 /EHsc /DUNICODE /D_UNICODE /Fe:PenBoard.NativeOverlay.x64.exe PenBoard.NativeOverlay.cpp /link /SUBSYSTEM:WINDOWS user32.lib gdi32.lib d2d1.lib dwrite.lib
```

Build (ARM64):

```powershell
call "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvarsamd64_arm64.bat"
cl /nologo /O2 /std:c++17 /EHsc /DUNICODE /D_UNICODE /Fe:PenBoard.NativeOverlay.arm64.exe PenBoard.NativeOverlay.cpp /link /SUBSYSTEM:WINDOWS user32.lib gdi32.lib d2d1.lib dwrite.lib
```

Current prototype:

- full-screen transparent layered window;
- Direct2D ink strokes;
- pointer/mouse input;
- Esc closes, C clears;
- bottom toolbar with close button.

Not implemented yet:

- Windows Ink recognition and text injection;
- language/candidates/backspace;
- annotation save PNG/PDF;
- tray/update integration;
- multi-monitor/DPI edge cases;
- MSI packaging.

The prototype confirms the memory target is feasible; a full rewrite is still required before it can replace the WPF overlay.
