# Privacy

Eventide is **local-first**:

- **No network connections.** Eventide contains no network code. There is no telemetry, no update check, no account and no cloud.
- **No external resources.** Fonts and icons are embedded.
- **Location.** The automatic mode needs an approximate position to calculate sunset and sunrise.
  - You can enter it by hand or click *Use Windows location*. In that case Eventide asks the Windows location service **once**, which shows the Windows permission prompt.
  - The position is rounded to two decimals (about 1 km) and stored only in the settings file on this PC. The calculation runs locally.
  - The tray never asks for the location.
  - Without a location, Eventide estimates the sun times from the time zone.
- Links on the *About* page (project page, sksdesign.de) open in your browser only when you click them.

## Data stored by Eventide

| Location | Content |
| --- | --- |
| `%AppData%\Eventide\settings.json` (portable: `data\settings.json`) | enabled, mode, intensity / own value, location (rounded), schedule times, transition, pause end, language |
| `%AppData%\Eventide\error.log` (portable: `data\error.log`) | only if the tray hits an unexpected error: time and exception text |
| `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\Eventide` | start with Windows (if enabled) |
| `HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ICM\GdiIcmGammaRange` | extended gamma range (only if you enable it; system-wide) |
| `HKLM\SOFTWARE\sksdesign\Eventide\GammaRangeSetByEventide` | marker that Eventide set the value (so the uninstaller only removes its own change) |

The uninstaller asks whether to delete `%AppData%\Eventide`. It removes the autostart entry, and removes the gamma range value only if Eventide set it.
