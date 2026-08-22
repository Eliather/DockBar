# DockBar
DockBar is a dock-style sidebar for Windows built with WPF. Version `1.6.1` introduces full Steam game and URL shortcut integration, intelligent icon auto-cropping, instant sub-100ms Store app scanning with on-demand refresh, and a redesigned Glass/Dark update experience with live download progress.

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
DockBar `1.6.1` is a major feature, stability, and UX release over `1.6.0`. It adds full support for Steam `.url` shortcuts and `steam://` protocol links, auto-crops padded icons to eliminate tiny/diminutive graphics, introduces a refresh button in the installed app search, and brings a redesigned update dialog with live download progress.

## What Changed In 1.6.1
- **Steam Integration & `.url` / `steam://` Protocol Compatibility (`Services/SteamService.cs`)**: Full automatic support for Steam game shortcuts and protocols. Resolves multi-disk Steam libraries via `libraryfolders.vdf`, parses `.url` internet shortcuts, handles `steam://rungameid/<id>` and `steam://run/<id>` protocol links, discovers game metadata from `appmanifest_<id>.acf`, and extracts high-resolution icons directly from game executables and Steam icon caches.
- **Intelligent Icon Auto-Cropping & Known Folder Resolution (`Services/ShellItemService.cs` & `Services/IconService.cs`)**: Resolved Windows Shell known folder GUIDs (e.g. `{7C5A40EF-A0FB-4BFC-874A-C0F2E0B9FA8E}`) to physical filesystem paths. Added `AutoCropIfNeeded` bounding-box trimming to remove excessive transparent padding and Start Menu tile borders from Win32 and Store app icons, ensuring all dock icons render crisp and at full button size.
- **Instant Store App Scanning & On-Demand Refresh (`Services/StoreAppService.cs` & `StoreAppPickerWindow.xaml`)**: Replaced slow PowerShell sub-process queries with native Win32 Shell COM enumeration (`IShellItem` / `IShellItemImageFactory` at 48x48), dropping scan times to **< 100ms**. Added a dedicated "Actualizar / Refresh" button to re-scan newly installed programs without restarting DockBar.
- **Redesigned Glass/Dark Update Dialog & Full Localization (`UpdateWindow.xaml` & `Services/LocalizationService.cs`)**: Replaced standard OS message boxes with a custom modern update dialog featuring version comparison badges, release changelog viewer, real-time download progress bar, and 100% Spanish/English localization coverage.
- **Fixed Executable Shortcut Crash (`Services/IconService.cs`)**: Added explicit `CharSet = CharSet.Unicode` to `[StructLayout]` on `SHFILEINFO` and `SHGetFileInfoW` P/Invoke declarations, fixing 64-bit stack corruption (`0xC0000005`) when picking executables or `.lnk` shortcuts.
- **Fixed File Dialog Picker Bug (`MainWindow.xaml.cs`)**: Corrected localization filter keys (`Dialog_ExecutableFilter` and `Dialog_ImageFilter`) for `Win32.OpenFileDialog`, enabling standard Windows Explorer file picker windows when clicking "Archivo / ejecutable...".
- **DPI-Resilient Auto-Hide Hotspot (`EdgeHotspotWindow.cs` & `MainWindow.xaml.cs`)**: Updated edge hotspot window brush to non-zero Alpha 15 with guaranteed 8px minimum thickness to ensure reliable DWM mouse hit-testing across all DPI scaling settings (100%-200%).
- **Native Tray Icon Decoding (`App.xaml.cs`)**: Switched tray icon decoder to native Win32 `LoadImage`, enabling crisp 16x16 icon decoding for PNG-compressed 256px ICO frames.

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
- `UseTransparency` is still the persisted compatibility flag, but in the UI it is exposed as `Efecto Glass`.
- `BackgroundOpacity` is managed by the app: `0.72` when Glass is enabled and `1.0` when disabled.

## Architecture
- `MainWindow.xaml(.cs)`: dock UI, auto-hide, drag/drop, pagination, fullscreen behavior, and visual application of config.
- `SettingsWindow.xaml(.cs)`: compact settings UI, live preview, color picker, and Glass toggle.
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
### Option A: MSIX
1. Create a Windows packaging project in Visual Studio.
2. Set DockBar as the main application.
3. Configure manifest metadata and assets.
4. Build in Release and generate the package.

### Option B: NSIS installer
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
