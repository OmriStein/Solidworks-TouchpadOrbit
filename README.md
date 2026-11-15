# Touchpad Orbit for SOLIDWORKS

Lets you rotate and zoom SOLIDWORKS models with a laptop touchpad.

## Gestures
Over the model in a part or assembly:

- **Rotate:** drag with two fingers.
- **Pan:** hold **Ctrl** and drag with two fingers. The model moves with your fingers without rotating or changing zoom, like Ctrl + middle-mouse drag.
- **Zoom:** tap and hold with two fingers, then drag. Drag up to zoom in, down to zoom out. It zooms toward the cursor.
- **Pinch:** also zooms toward the cursor.
- **Roll:** twist two fingers around each other to turn the model around the axis pointing out of the screen, like Alt + middle-mouse drag.
- A plain two-finger tap still right-clicks (about 0.3 s later than usual).
- Outside the model (for example in the FeatureManager tree), two-finger scrolling works as normal. A mouse wheel and one-finger drag behave as normal everywhere.

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
| PanSpeed | 1.0 | Pan speed multiplier (1.0 = a full pad-width swipe moves the model one view width) |
| ZoomSpeed | 1.0 | Zoom speed multiplier (tap-and-hold drag and pinch) |
| InvertHorizontal / InvertVertical | 0 | Set to 1 to flip a rotation direction |
| InvertZoom | 0 | Set to 1 to make dragging down zoom in |
| TwistSpeed / InvertTwist | 1.0 / 0 | Roll speed (1.0 = the model turns as much as your fingers) and direction |
| DragStartPercent | 1.5 | How far the fingers must move (% of pad width) before rotate/pan starts |
| PinchStartPercent | 4 | How much the finger spread must change (% of pad width) before pinch zoom starts |
| TwistStartDegrees | 12 | How far the fingers must twist before a roll starts |
| TapMovePercent | 3 | Within the first TapMs, how far the fingers can move and still count as a tap (right-click) |
| TapMs | 250 | Longest the two-finger tap can last (ms) |
| GapMs | 350 | Longest gap allowed between the tap and putting the fingers back down (ms) |
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
