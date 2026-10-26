# Native Direct2D overlay

This is the native replacement path for the WPF overlay.

## Status

Working prototype:

- full-screen transparent layered window;
- Direct2D drawing;
- pointer/mouse strokes;
- bottom toolbar;
- Windows Ink recognition worker;
- candidate row;
- Unicode injection and backspace scaffolding;
- x64 and ARM64 builds.

Measured x64:

```text
Working Set: 33.5 MB
Private:     13 MB
```

## Current gaps

- candidate hit-testing/injection needs verification;
- annotation screen capture and PNG/PDF save are not implemented;
- C# tray/pill integration is not switched to this executable yet;
- multi-monitor/DPI and error logging need work.

## Build

x64:

```powershell
call "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvars64.bat"
cl /nologo /O2 /utf-8 /std:c++17 /EHsc /DUNICODE /D_UNICODE /Fe:PenBoard.NativeOverlay.x64.exe PenBoard.NativeOverlay.cpp /link /SUBSYSTEM:WINDOWS user32.lib gdi32.lib d2d1.lib dwrite.lib windowsapp.lib
```

ARM64:

```powershell
call "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvarsamd64_arm64.bat"
cl /nologo /O2 /utf-8 /std:c++17 /EHsc /DUNICODE /D_UNICODE /Fe:PenBoard.NativeOverlay.arm64.exe PenBoard.NativeOverlay.cpp /link /SUBSYSTEM:WINDOWS user32.lib gdi32.lib d2d1.lib dwrite.lib windowsapp.lib
```
