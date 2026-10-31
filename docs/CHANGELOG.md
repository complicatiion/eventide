# Changelog

All notable changes to Eventide. Newest version first; every version is added to this one file.

## 0.0.4 – 2026-10-31

### Fixes

- **Crash when exiting from the tray menu** (`ObjectDisposedException` in `TrayPipeServer.Dispose`).
  - Cause: Windows Forms already disposes the tray context when `ExitThread` ends the message loop. The `using` block in `TrayHost` disposed it a second time, and the second `Cancel()` hit an already disposed `CancellationTokenSource`.
  - Fix: `TrayContext.Dispose` runs only once, `TrayPipeServer.Dispose` can be called several times, and `TrayHost` no longer uses a second `using`.
- **Tray menu:** text sat slightly higher than the icon. Icon, check frame and text are now drawn centred on the full row height.
  - Rows have symmetric padding (≈ 30 px at 100 %).
  - The image size is known before the first layout.

### Changes

- **Default night temperature: Medium (≈ 4200 K)** again. This only applies to new settings files; an existing `settings.json` keeps its value.
- **Strong is now ≈ 3200 K** (was 3400 K) – in the cards, the tray menu, the installer text and the preview banner.
- **Status page:**
  - *Turn off / Turn on*, *Pause* and *Resume* have the same minimum width (180 px).
  - The end of a pause is shown as "Paused until" plus the time in a green pill ("Mon 07:00" on another day).
- **Schedule page:** the result of *Use Windows location* is shown in a status pill:
  - green = location set,
  - **red** (soft red, built like the green pill) = could not be determined, access denied or invalid coordinates,
  - grey = requesting.

## 0.0.3 – 2026-10-31

### Settings window

- **Window:**
  - Default size 980 × 860 (was 940 × 680), so Status and Schedule normally need no vertical scroll bar. On small screens the window is limited to 90 % of the work area.
  - **Soft page changes:** fade in and slide up (≈ 0.25 s). Every page starts at the top.
- **Sidebar order:** Status → Filter → Schedule → General → About.
- **Info icon:** a small round "i" in accent green in front of these hints:
  - pause duration,
  - decimal degrees,
  - "Requested once …",
  - transition,
  - live preview,
  - gamma range.
- **Card headings:** bigger (15 px instead of 11 px), still in accent green. Applies to Location, Time window, Transition, Custom value, Night colour temperature and Third-party components.
- **Status:**
  - The on / off button is right-aligned again.
  - The pause chips are slightly narrower and fit on one line. The chip *Until tomorrow morning* is now called *Until tomorrow* (also in the tray menu).
  - The note "No location set …" has two lines: the second line says where to set it.
- **Filter:** new preset **Warm (≈ 3800 K)** between Medium and Strong, giving six cards in two rows (also in the tray menu).
- **General:**
  - **Language:** shows flat 2D flags (UK / Germany). The names stay English: *English*, *German*. *Automatic* shows a globe.
  - **Gamma range:** *Disable* sits under *Enable*. Both buttons are as wide as *Open folder*.
  - **Settings file:** the path is shown in a pill, shortened with "…" if long, full path as tooltip.
- **About:**
  - Version, licence and author line are shown in accent green.
  - The heading and text of the third-party card are bigger.

### Tray

- **Start with Windows is on by default.** It is switched on exactly once at the first tray start, controlled by the new setting `AutostartInitialized`.
  - Before, it depended on whether a settings file already existed, so older test installations stayed off.
  - If you switch it off yourself later, it stays off.

## 0.0.2 – 2026-10-31

### Settings window

- **Sidebar:** the author line under the logo was removed.
- **Info page:**
  - The description and the privacy sentence were removed.
  - The buttons are centred and renamed to *Licenses* · *sksdesign.de* · *GitHub repository*.
  - The card *Third-party components* is centred.
- **Status page:**
  - The on / off button is centred in the status card (one button, text changes).
  - New **pause card**:
    - fixed durations as chips: 15 min · 30 min · **1 h (default)** · 2 h · 4 h · until tomorrow morning;
    - own duration (number field plus minutes / hours) and *Pause*;
    - while paused: end time and *Resume*.
  - The chosen duration is saved as default (`PauseMinutes`).
  - Notes start with **"Note:"**, warnings (HDR, Night light, colour range, remote session, tray not running) with **"Warning:"**.

### Tray

- **Disable filter / Enable filter:** one button in the menu that changes its text, replacing the checkbox entry *Eventide enabled*. It works like the left click and also ends a pause.
- The pause submenu lists all fixed durations. The default is bold and marked "· default"; an own default duration appears as the first entry.

### Defaults

- The night colour temperature is **Soft (≈ 5000 K)** instead of Medium. This only applies to new settings files.

### Installer and build

- Release builds are **self-contained** (`win-x64`). The normal build folder `bin\Release\net10.0-windows10.0.19041.0\win-x64\` is the input of the installer.
- **Every Release build creates the installer automatically.** MSBuild target `EventideInstaller` → ISCC → `artifacts\eventide_v<version>_setup.exe`.
  - Warning only, if Inno Setup is not installed.
  - Can be switched off with `-p:EventideSkipInstaller=true`.
- `eventide.iss`:
  - takes the build folder by default and reads the version from `Eventide.exe`;
  - stops with a clear message if the EXE is missing;
  - packs no `.pdb` files.
- `publish.ps1` and CI skip the automatic installer, because they build their own.

### Fixes

- `Eventide.Platform.Windows`: removed `SupportedOSPlatformVersion`, which conflicted with `net10.0-windows` (NETSDK1135).

## 0.0.1 – 2026-10-31

First version.

### Tray (Eventide.exe)

- Tray-only app built on Windows Forms (`NotifyIcon`). There is no main window, and Avalonia is not loaded in this mode.
- The tray icon is a state indicator: accent `#B7D894` = on, grey = off, grey with an accent dot = paused. It has light and dark taskbar variants and switches automatically.
- **Left click:** filter on / off; this also ends a pause.
- **Right click:** dark menu in the Eventide palette with icons on the left and check states as an accent frame (no system blue). Rounded corners on Windows 11. Entries:
  - **Settings…** (top, bold)
  - Status line
  - Eventide enabled
  - Mode: Automatic (sunset) / Schedule (shows the configured times) / Always on
  - Intensity: Soft 5000 K / Medium 4200 K / Strong 3400 K / Extreme 2700 K; *Custom* appears when it is selected
  - Pause: 1 hour / until tomorrow morning; while paused: *Resume* with the end time
  - Start with Windows
  - Exit
- Starting `Eventide.exe` a second time opens the settings window of the running tray.
- First start: Eventide adds itself to the autostart (HKCU Run) and shows a balloon hint.

### Filter engine

- Gamma ramp per display (`SetDeviceGammaRamp`).
  - The ramp found at start, e.g. a calibration, is kept and scaled. It is restored on exit, sign-out and crash.
  - A warm ramp left behind by a crashed tool is detected and replaced by the identity ramp.
- Kelvin → RGB with Tanner Helland's curve fit, normalised to 6500 K = no change. Interpolation in mired space.
- Modes:
  - *Automatic*: sunset / sunrise calculated with the NOAA algorithm (own implementation, no CoordinateSharp because of its AGPL licence). Handles polar night and midnight sun.
  - *Schedule*: fixed window, may span midnight.
  - *Always on*.
- Transition 0–120 min (default 45), smoothstep curve: it fades in after the start and out before the end of the night window. Switching on and off and the preview fade over about one second.
- While a tint is active, re-applies the ramp every 30 s; it is also re-applied after display changes, resume from sleep and unlock. At 6500 K the ramps of other tools are left alone. A cancelled sign-out turns the filter back on.
- When Windows refuses a ramp (no `GdiIcmGammaRange`), Eventide finds the warmest accepted value by bisection, caches it per display and reports it.
- Detects HDR (DisplayConfig advanced colour), Windows Night light (best effort), remote sessions (RDP / Citrix → inactive) and displays that reject colour changes.

### Settings window (`Eventide.exe --settings`, Avalonia)

- Separate process: starts on demand, frees its memory on close, single instance (a second open brings it to the front).
- Pages:
  - **Status:** live state from the tray every 2 s, on / off, pause, resume, tiles, hints, *Start tray* when the tray is not running.
  - **Schedule:** mode cards, location (manual, or *Use Windows location* rounded to ≈ 1 km), time window, transition.
  - **Filter:** intensity cards, own value 1900–6500 K, colour swatch, **live preview** while the page is open.
  - **General:** start with Windows, language (Automatic / English / Deutsch, switches without restart), extended gamma range (enable / disable via UAC helper), settings file.
  - **About:** version, licence, links, third-party components.
- Preview messages are sent one at a time, newest wins, and are refreshed every 2 s while the page is open, so the screen never stays tinted when the window closes.
- *Discard* on the left, *Save* (main action) on the right. Saving only writes the edited values; on / off and pause stay owned by the tray.
- Dark theme in the Color Hunt palette `#202734 · #36415A · #B7D894 · #E0F8C7`. Switches, radio buttons, sliders and selections use the accent colour.

### Settings and IPC

- `%AppData%\Eventide\settings.json`; portable: `data\settings.json` when `Eventide.portable` is next to the EXE.
  - Read-modify-write under a named mutex.
  - A damaged file is kept as `.damaged` and replaced by defaults.
- Named pipe per user and session (`CurrentUserOnly`) between tray and settings window: status, reload, preview, toggle, pause, resume, show settings, exit.

### Installer and build

- Inno Setup:
  - English / German, wizard images in the Eventide palette.
  - Options: start with Windows (written as the original user) and desktop shortcut.
  - Own page for the **extended gamma range** with an explanation, checked by default. It is skipped when the value is already set; restart prompt and finish-page hint when it was changed.
  - Uninstall restores the colours, removes the autostart entry and removes `GdiIcmGammaRange` only if Eventide set it.
- `build/publish.ps1`: tests, self-contained publish, portable ZIP with `Eventide.portable`, setup, SHA-256 files.
- GitHub Actions workflow: build and test; on tags also packages and a release.
- Unit tests for colour temperature, sun times, scheduler and settings store.
