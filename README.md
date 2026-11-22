# Touchpad Orbit for SOLIDWORKS

Lets you rotate, pan, zoom and roll SOLIDWORKS models, and pan and zoom drawings, with a laptop touchpad, the way you would with a mouse's middle button.

## Gestures
Over the model in a part or assembly (for drawings, see **In drawings** below):

- **Rotate:** drag with two fingers.
- **Pan:** tap and hold with two fingers, then drag (or hold **Ctrl** and drag with two). The model moves with your fingers without rotating or changing zoom, like Ctrl + middle-mouse drag.
- **Zoom:** pinch. It zooms toward the cursor.
- **Roll:** twist two fingers around each other to turn the model around the axis pointing out of the screen, like Alt + middle-mouse drag.
- A plain two-finger tap still right-clicks (about 0.35 s later than usual, so the add-in can tell it apart from the start of a pan).
- **In drawings:** drag with two fingers to pan and pinch to zoom toward the cursor. There is no rotate or roll in 2D.
- Outside the model (for example in the FeatureManager tree), two-finger scrolling works as normal. A mouse wheel and one-finger drag behave as normal everywhere.

## Install
1. Download the zip from the [latest release](https://github.com/OmriStein/Solidworks-TouchpadOrbit/releases/latest), or take the files in [`dist/`](dist).
2. Double-click **Install Touchpad Orbit.cmd** and approve the admin prompt. If any SOLIDWORKS windows are open, the installer waits until you close them all.
3. Start SOLIDWORKS.

If it isn't active, go to **Tools > Add-Ins** (hover over the SOLIDWORKS logo to show the menu). Find **Touchpad Orbit** under *Other Add-ins* and tick both boxes.

## Uninstall
Double-click **Uninstall Touchpad Orbit.cmd** and approve the admin prompt. Like the installer, it waits until every SOLIDWORKS window is closed.

## Settings (optional)
Settings are in the registry under `HKCU\Software\TouchpadOrbit`. Restart SOLIDWORKS after changing them.

| Value | Default | Meaning |
|---|---|---|
| RotateSpeed | 1.0 | Rotation speed multiplier |
| PanSpeed | 1.0 | Pan speed multiplier (1.0 = a full pad-width swipe moves the model one view width) |
| ZoomSpeed | 1.0 | Zoom speed multiplier (pinch) |
| InvertHorizontal / InvertVertical | 0 | Set to 1 to flip a rotation direction |
| TwistSpeed / InvertTwist | 1.0 / 0 | Roll speed (1.0 = the model turns as much as your fingers) and direction |
| DragStartPercent | 1.5 | How far the fingers must move (% of pad width) before rotate/pan starts |
| PinchStartPercent | 4 | How much the finger spread must change (% of pad width) before pinch zoom starts |
| TwistStartDegrees | 12 | How far the fingers must twist before a roll starts |
| TapMovePercent | 3 | Within the first TapMs, how far the fingers can move and still count as a tap (right-click) |
| TapMs | 250 | Longest the two-finger tap can last (ms) |
| GapMs | 350 | Longest gap allowed between the tap and putting the fingers back down (ms) |
| Debug | 1 | Writes a log to `%LOCALAPPDATA%\TouchpadOrbit\log.txt` (or `%TEMP%\TouchpadOrbit-log.txt` if that can't be written) |

Requirements: Windows, 64-bit SOLIDWORKS, and a precision touchpad. The same installer works with any SOLIDWORKS version.

## How it works
Touchpad Orbit is a regular SOLIDWORKS COM add-in (`src/TouchpadOrbit.cs`). It reads finger positions from the precision touchpad via Windows Raw Input and works out which gesture you're making (drag, pinch, twist, or a tap followed by a hold). In drawings only pan and zoom apply. It then drives the view through the SOLIDWORKS API (`RotateAboutCenter`, `TranslateBy`, `ZoomByFactor` and `RollBy`). Zooming keeps the point under the cursor fixed. Over the model, it blocks the touchpad's own scroll and right-click messages so they don't interfere, and sends its own right-click for a two-finger tap. It doesn't reference SOLIDWORKS' interop assemblies. It declares the `ISwAddin` interface itself and calls the API late-bound, the same way VBA macros do, so one build works with every SOLIDWORKS version. The installer contains the compiled DLL and registers it with `RegAsm`.

## Building from source
Requires Visual Studio or Build Tools (for `csc.exe`). SOLIDWORKS isn't needed to build.

```powershell
powershell -ExecutionPolicy Bypass -File build\build.ps1 -Version 1.6.0
```

This compiles `src\TouchpadOrbit.cs` and regenerates the installers in `dist\` (plus a release zip in `obj\`).

## Changelog
- **1.6.0:** Drawings: two-finger drag pans and pinch zooms toward the cursor.
- **1.5.0:** Tap and hold with two fingers, then drag, now pans (it used to zoom; pinch still zooms).
- **1.4.0:** Works with any SOLIDWORKS version. The add-in no longer depends on the SOLIDWORKS interop DLLs, which made installation fail on versions other than the one it was built with.
- **1.3.1:** Updated README (install notes, how it works, changelog). No change in behaviour.
- **1.3.0:** New gesture set: two-finger drag rotates, Ctrl + drag pans, tap-and-hold drag or pinch zooms toward the cursor, and twisting two fingers rolls the model. The two-finger-tap right-click is more reliable. The installer and uninstaller wait for SOLIDWORKS to close. Added new settings.
- **1.0.0:** First release. Two-finger tap-and-hold + drag rotates, and touchpad zoom goes toward the cursor.

## License & contact
[MIT](LICENSE) © 2026 Omri Stein.
Bugs and suggestions: please open an [issue](https://github.com/OmriStein/Solidworks-TouchpadOrbit/issues).

SOLIDWORKS is a trademark of Dassault Systèmes. This project is not affiliated with or endorsed by Dassault Systèmes.
