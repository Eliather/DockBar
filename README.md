# DockBar
DockBar is a dock-style sidebar for Windows built with WPF. Version `1.7.0` introduces a true native hardware-composited Glass effect with per-pixel alpha transparency, dynamic text contrast shadow, 0%-100% smooth opacity scaling, and a redesigned Settings window.

<img width="256" height="256" alt="Dock" src="https://github.com/user-attachments/assets/eb6fd915-77f7-4298-b41b-90a7d14f41d1" />

<img width="1920" height="1080" alt="{0A9D8F93-0DB9-42CC-979A-CF66218595FC}" src="https://github.com/user-attachments/assets/302484ca-4aa6-4e54-9b0d-35e484cdc4ff" />


Video demo:
https://github.com/user-attachments/assets/9a4ea52f-8131-471e-8bd3-89122aa3dec7

## Overview
DockBar gives Windows a compact side dock with shortcuts, edit mode, smooth auto-hide, persistent configuration, and a redesigned settings experience.

This version is built around four priorities:

- **Ultra-fast interaction**: Replaced heavy background subprocess calls (e.g. PowerShell app enumeration) with native Win32 Shell COM APIs (< 5ms response time).
- **P/Invoke memory safety**: Explicit Unicode struct layout alignment for Win32 API calls (`SHFILEINFO` / `SHGetFileInfoW`).
- **High DPI compatibility**: Edge hotspot reveal window engineered with non-zero Alpha 15 for DWM hit-testing across 100%–200% display scaling.
- **Zero third-party dependencies**: Lightweight C# 13 + .NET 10.0 codebase relying solely on native Windows APIs and stdlib.

## Release Summary
DockBar `1.7.0` introduces a full Glass overhaul with clean per-pixel alpha transparency, dynamic text shadow for maximum legibilidad across any wallpaper, and a completely spacious and reorganized Settings experience.

## What Changed In 1.7.0
- **True Glass & Per-Pixel Alpha Composition (`GlassEffectHelper.cs` & `MainWindow.xaml`)**: Integrated `<WindowChrome.WindowChrome>` frame extension with `ACCENT_ENABLE_TRANSPARENTGRADIENT`, eliminating muddy/milky acrylic fog and providing crystal-clear 100% transparency at 0% opacity and smooth translucent tinting up to 100%.
- **Dynamic Text Contrast Shadow (`MainWindow.xaml` & `MainWindow.xaml.cs`)**: Added smart contrasting drop shadows behind shortcut names (black drop shadow for light text, white drop shadow for dark text), ensuring 100% legibility over any light or dark wallpaper.
- **Redesigned Settings Layout (`SettingsWindow.xaml`)**: Reorganized the Appearance section into a balanced 2-column layout with preview, HEX color box, HSV canvas, dedicated settings rows, and full visibility for all 12 quick palette color swatches.
- **Opacity Slider Zero-Point Fix (`SettingsWindow.xaml.cs`)**: Fixed an issue where sliding opacity all the way to 0% reset to 45% default. Now supports the full 0% to 100% range stably.

## What It Does
- Left or right borderless dock with topmost behavior.
- Smooth auto-hide with a small reveal edge.
- Drag and drop for `.lnk`, `.exe`, and folders.
- URI / command shortcuts and Microsoft Store app support.
- Edit mode for reorder, rename, icon changes, and removal.
- Pagination in normal mode when the visible area is exceeded.
- Tray icon with quick actions.
- Hidden from Alt+Tab and Win+Tab.
- Persistent config stored in `%AppData%\DockBar\shortcuts.json`.

## Performance Improvements
- **Native Shell COM Enumeration**: Store app lookup executed natively in < 5ms without PowerShell.
- **Streamed JSON Deserialization**: Direct stream decoding for GitHub updates and JSON config.
- **Icon caching**: `IconService` prevents redundant file icon extractions.
- **Shell Item Caching**: Cached display names and icons for `shell:AppsFolder` entries.
- **Batched config saves**: Atomic config persistence when managing multiple shortcuts.

## Requirements
- Windows 10 or Windows 11
- .NET SDK 10.0
- Visual Studio, VS Code, or terminal with `dotnet`

## Build And Run
```bash
dotnet build
dotnet run
```

If the build fails because the executable is locked, close DockBar first and build again.

## Configuration
Config file:

```text
%AppData%\DockBar\shortcuts.json
```

Example:

```json
{
  "DockSide": "Left",
  "DockWidth": 175,
  "IconSize": 40,
  "AutoHideDelaySeconds": 0,
  "HideAnimationMs": 200,
  "UseTransparency": true,
  "BackgroundOpacity": 0.72,
  "BackgroundR": 17,
  "BackgroundG": 24,
  "BackgroundB": 39,
  "UseLightText": true,
  "AutoStartEnabled": false,
  "Shortcuts": [
    { "Name": "Explorer", "Path": "C:\\Windows\\explorer.exe" },
    { "Name": "Documents", "Path": "C:\\Users\\Public\\Documents" },
    { "Name": "Steam", "Path": "C:\\Program Files (x86)\\Steam\\Steam.exe" }
  ]
}
```

Notes:
- `UseTransparency` is the persisted compatibility flag (exposed in UI as `Efecto Glass`).
- `BackgroundOpacity` defines the dock background opacity when Glass is enabled (defaults to `0.72`, customizable in Settings).

## Architecture
- `MainWindow.xaml(.cs)`: dock UI, auto-hide, drag/drop, pagination, fullscreen behavior, and visual application of config.
- `GlassEffectHelper.cs`: hardware-accelerated Windows 11 DWM Desktop Acrylic (`DWMWA_SYSTEMBACKDROP_TYPE`) and Windows 10 Acrylic Blur behind integration.
- `SettingsWindow.xaml(.cs)`: compact settings UI, live preview, color picker, Glass toggle, and opacity slider.
- `AddLinkWindow.xaml(.cs)`: add executables, folders, URIs, or commands with validation.
- `UpdateWindow.xaml(.cs)`: modern update window with release notes and live progress bar.
- `StoreAppPickerWindow.xaml(.cs)`: installed-app picker with instant COM enumeration and on-demand refresh.
- `Models/`: configuration and shortcut models.
- `Services/ConfigService.cs`: load and save config with backward-compatible defaults.
- `Services/IconService.cs`: icon resolution, P/Invoke jumbo extraction, and cache.
- `Services/ShellItemService.cs`: shell item names, icons, known folder resolution, and auto-crop.
- `Services/SteamService.cs`: Steam library discovery, app manifest parsing, .url parsing, and game icon resolution.
- `Services/StoreAppService.cs`: native Win32 Shell COM enumeration for Store apps.
- `Services/UpdateService.cs`: background update checking with Stream deserialization & strict timeout.
- `Resources/Theme.xaml`: shared colors, spacing, buttons, sliders, and scroll styling.

## Packaging

### Option A: MSIX Package (Microsoft Store & Sideloading)
DockBar includes a zero-dependency automated build script for modern Windows MSIX packages:

```powershell
.\build-msix.ps1
```

* Generates `DockBar.msix` using official Microsoft packaging tools (auto-downloaded if not present in your environment).
* Automatically signs the package with your developer certificate (`CN=Eliather`) for immediate double-click testing.
* Pre-configured with full trust capability (`runFullTrust`) and native Windows startup task integration (`windows.startupTask`).
* **Ready for Microsoft Store**: To publish, update `Package\AppxManifest.xml` with your Partner Center identity values (`Name`, `Publisher`, `PublisherDisplayName`), run `.\build-msix.ps1`, and upload `DockBar.msix`.

### Option B: NSIS installer (Classic Win32)
```bash
.\build-installer.ps1
```

If you prefer the manual path:

```bash
dotnet publish DockBar.csproj -c Release -r win-x64 --self-contained false -o publish
& "C:\Program Files (x86)\NSIS\makensis.exe" DockBar.nsi
```

This generates `DockBarSetup.exe`. The script auto-detects `makensis.exe` from `PATH` or common NSIS folders.

## Privacy
- No telemetry.
- No cloud sync.
- No network dependency for local dock behavior.
