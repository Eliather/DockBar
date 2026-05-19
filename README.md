# DockBar
DockBar is a dock-style sidebar for Windows built with WPF. Version `1.5.3` is a stability release focused on keeping the dock alive and populated after suspend, hibernate, and display resume events on laptops.

<img width="256" height="256" alt="Dock" src="https://github.com/user-attachments/assets/eb6fd915-77f7-4298-b41b-90a7d14f41d1" />

<img width="1920" height="1080" alt="{0A9D8F93-0DB9-42CC-979A-CF66218595FC}" src="https://github.com/user-attachments/assets/302484ca-4aa6-4e54-9b0d-35e484cdc4ff" />


Video demo:
https://github.com/user-attachments/assets/9a4ea52f-8131-471e-8bd3-89122aa3dec7

## Overview
DockBar gives Windows a compact side dock with shortcuts, edit mode, smooth auto-hide, persistent configuration, and a redesigned settings experience.

This version is built around three priorities:

- Fast interaction without heavy background polling.
- Small focused services instead of one oversized window class doing everything.
- A cleaner interface direction for settings and dialogs, avoiding the old utility or "Windows 98 tool window" look.

## Release Summary
DockBar `1.5.3` is a focused recovery and reliability release. The main goal is to keep the dock from coming back empty after the machine resumes from suspend or hibernate, especially on laptops that sleep when the lid is closed.

The key change in this build is explicit recovery after power and display transitions: the window now restores its layout, refreshes visible shortcuts and icons, and reloads the persisted shortcut list if the in-memory collection comes back empty after resume.

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

## What Changed In 1.5.3
- Added explicit dock recovery when Windows resumes from suspend or hibernate.
- Added recovery after session unlock and display topology changes so the dock recalculates monitor bounds, pagination, and topmost state.
- Visible shortcuts are rebuilt after resume and shortcut icons are refreshed to avoid a blank bar with stale UI state.
- If the in-memory shortcut list comes back empty after resume, DockBar now reloads `%AppData%\DockBar\shortcuts.json` automatically instead of forcing a full app restart.

## Performance Improvements
- Icon caching in `IconService` to avoid reloading the same files repeatedly.
- Shell item name and icon caching for `shell:AppsFolder` entries.
- Session cache for Microsoft Store app discovery.
- Batched config saves when adding multiple shortcuts in one action.

## Requirements
- Windows 10 or Windows 11
- .NET SDK 9.0
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
- `BackgroundOpacity` is now managed by the app: `0.72` when Glass is enabled and `1.0` when it is disabled.

## Architecture
- `MainWindow.xaml(.cs)`: dock UI, auto-hide, drag/drop, pagination, fullscreen behavior, and visual application of config.
- `SettingsWindow.xaml(.cs)`: compact settings UI, live preview, color picker, and Glass toggle.
- `AddLinkWindow.xaml(.cs)`: add executables, folders, URIs, or commands with validation.
- `RenameWindow.xaml(.cs)`: rename shortcut dialog.
- `StoreAppPickerWindow.xaml(.cs)`: installed-app picker with async loading state.
- `Models/`: configuration and shortcut models.
- `Services/ConfigService.cs`: load and save config with backward-compatible defaults.
- `Services/IconService.cs`: icon resolution and cache.
- `Services/ShellItemService.cs`: shell item names, icons, and cache.
- `Services/StoreAppService.cs`: installed Store app lookup and session cache.
- `Resources/Theme.xaml`: shared colors, spacing, buttons, sliders, and scroll styling.

## Visual Direction
The current UI direction is based on:

- Layered dark surfaces instead of flat black panels.
- Rounded cards with tighter spacing and less wasted space.
- Custom controls for sliders, buttons, and scroll rails instead of raw default WPF styling.
- A unified appearance panel where preview and color decisions happen together.
- Reusable theme resources instead of hardcoded one-off colors in each window.

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

This generates `DockBarSetup.exe`. The script auto-detects `makensis.exe` from `PATH` or the common NSIS install folders.

## Troubleshooting
- Glass effect does not appear: make sure DWM composition is enabled in Windows.
- The dock still looks solid: disable and re-enable `Efecto Glass` to force a fresh config write.
- The app is missing from Alt+Tab: this is intentional.
- The JSON keeps regenerating: check `%AppData%` permissions and config validity.
- Build is locked: close any running DockBar instance first.

## Privacy
- No telemetry.
- No cloud sync.
- No network dependency for local dock behavior.
