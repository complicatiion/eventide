# Features

## 0.0.4

| Area | Feature |
| --- | --- |
| Tray | State icon (on / off / paused, light / dark taskbar), left click toggles, right-click menu with *Settings…* on top and one *Disable / Enable filter* button, tooltip with status |
| Modes | Automatic (sunset → sunrise, NOAA), schedule (window across midnight), always on |
| Intensity | Soft 5000 K, Medium 4200 K (default), Warm 3800 K, Strong 3200 K, Extreme 2700 K, custom 1900 – 6500 K |
| Transitions | 0 – 120 min smoothstep at dusk / dawn, 1 s fade on switch / preview |
| Pause | 15 min, 30 min, 1 h (default), 2 h, 4 h, until tomorrow, own duration (minutes / hours); pause card on the status page; default duration marked in the tray menu; resume |
| Autostart | HKCU Run, tray menu, settings window, installer option, on by default at first start |
| Displays | Per-display gamma ramp, calibration kept and restored, reapply every 30 s and on display / power / session events |
| Installer | Built automatically after every Release build (Inno Setup, self-contained build folder) |
| Gamma range | Detects the Windows limit, uses the warmest accepted value, installer page and UAC helper to enable `GdiIcmGammaRange` |
| Hints | HDR, Windows Night light, remote session, unsupported display, approximate location |
| Settings window | Status, Schedule, Filter (live preview), General, About; Save / Discard; EN / DE without restart |
| Location | Manual or Windows location (opt-in, rounded to ≈ 1 km); fallback from the time zone |
| Privacy | No network, no telemetry, settings only local |

## Ideas for later versions

- Global hotkey (e.g. Alt + Shift + End) for on / off
- Exceptions per program (pause automatically while Photoshop or a game runs)
- Optional dimming (brightness) in addition to the colour temperature
- Per-monitor intensity
- City search for the location (offline list)
- Day temperature other than 6500 K
