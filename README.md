# overscanFixer

A lightweight, powerful Windows display utility for fixing monitor and TV overscan issues.

## Features

- **Work Area Insets**: Adjust the Windows desktop usable work area on each side independently or linked uniformly.
- **Pixel & Percentage Dual-Control**: Set margins using exact pixel counts or screen percentages with real-time two-way synchronization.
- **Live Diagram Preview**: Visual preview of safe areas and border margins mapped to your monitor's aspect ratio.
- **Calibration Overlay**: Fullscreen transparent calibration grid with ruler ticks and boundary guides to pinpoint exact display edges.
- **Click-Through Black Border Masks**: Optional border masking windows layered with `WS_EX_TRANSPARENT` click-through support.
- **Global Hotkey**: Toggle overscan adjustments on/off quickly using `Ctrl + Alt + O`.
- **System Tray Integration**: Background operation with quick access context menu and minimize-to-tray.
- **Multi-Monitor Support**: Select and configure individual connected displays.
- **Startup with Windows**: Optional automatic launch on system boot.

## Tech Stack

- **Target Framework**: .NET 8.0 (Windows)
- **UI Framework**: WPF + Windows Forms Interop (`NotifyIcon`)
- **Native APIs**: Win32 `SystemParametersInfo` (`SPI_SETWORKAREA`), `RegisterHotKey`, `SetWindowLongPtr`

## Getting Started

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or higher
- Windows 10 / 11

### Building & Running

```powershell
# Restore & build
dotnet build

# Run application
dotnet run

# Publish standalone executable
dotnet publish -c Release -r win-x64 --self-contained false -o publish
```
