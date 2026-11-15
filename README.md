# Touchpad Orbit for SOLIDWORKS

Lets you rotate and zoom SOLIDWORKS models with a laptop touchpad.

## Gestures
- **Rotate:** tap and hold with two fingers, then drag. Lift to stop.
- **Zoom:** two-finger scroll or pinch. It zooms toward the cursor, replacing the original touchpad zoom that zoomed around the center of the screen.
- A plain two-finger tap still right-clicks (about 0.3 s later than usual).
- Works in parts and assemblies. A mouse wheel and one-finger drag behave as normal.

## Install
1. Download the zip from the [latest release](https://github.com/OmriStein/Solidworks-TouchpadOrbit/releases/latest), or take the files in [`dist/`](dist).
2. Double-click **Install Touchpad Orbit.cmd** and approve the admin prompt.
3. Restart SOLIDWORKS.

If it isn't active, go to **Tools > Add-Ins** (hover over the SOLIDWORKS logo to show the menu). Find **Touchpad Orbit** under *Other Add-ins* and tick both boxes.

## Uninstall
Close SOLIDWORKS, then double-click **Uninstall Touchpad Orbit.cmd** and approve the admin prompt.

## Settings (optional)
Settings are in the registry under `HKCU\Software\TouchpadOrbit`. Restart SOLIDWORKS after changing them.

| Value | Default | Meaning |
|---|---|---|
| RotateSpeed | 1.0 | Rotation speed multiplier |
| InvertHorizontal / InvertVertical | 0 | Set to 1 to flip a rotation direction |
| TapMs | 250 | Longest the first two-finger tap can last (ms) |
| GapMs | 350 | Longest gap allowed between the tap and putting the fingers back down (ms) |
| ZoomToCursor | 1 | Set to 0 to keep SOLIDWORKS' own touchpad zoom |
| ZoomSpeed / InvertZoom | 1.0 / 0 | Zoom speed and direction |
| Debug | 1 | Writes a log to `%LOCALAPPDATA%\TouchpadOrbit\log.txt` |

Requirements: Windows, SOLIDWORKS 2021 or later (64-bit), and a precision touchpad.

## How it works
Touchpad Orbit is a regular SOLIDWORKS COM add-in (`src/TouchpadOrbit.cs`). It reads finger positions from the precision touchpad via Windows Raw Input, recognises the two-finger tap-and-hold, and drives the view through the SOLIDWORKS API (`RotateAboutCenter`, `ZoomByFactor`, `TranslateBy`). The installer contains the compiled DLL, copies the SOLIDWORKS interop DLLs from your own SOLIDWORKS install, and registers it with `RegAsm`.

## Building from source
Requires SOLIDWORKS and Visual Studio / Build Tools (for `csc.exe`).

```powershell
powershell -ExecutionPolicy Bypass -File build\build.ps1
```

This compiles `src\TouchpadOrbit.cs` and regenerates the installers in `dist\` (plus a release zip in `obj\`).

## License & contact
[MIT](LICENSE) © 2026 Omri Stein.
Bugs and suggestions: please open an [issue](https://github.com/OmriStein/Solidworks-TouchpadOrbit/issues).

SOLIDWORKS is a trademark of Dassault Systèmes. This project is not affiliated with or endorsed by Dassault Systèmes.
