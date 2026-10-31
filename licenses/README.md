# Licenses

## Eventide

Eventide is released under the **MIT License** – see [LICENSE.md](LICENSE.md).
Copyright © 2026 Sven Klocke (*sksdesign | complicatiion*).

| Document | Content |
| --- | --- |
| [LICENSE.md](LICENSE.md) | MIT License of Eventide |
| [COPYRIGHT.md](COPYRIGHT.md) | Copyright and trademark notes |
| [LEGAL-NOTICE.md](LEGAL-NOTICE.md) | Disclaimer, limitation of liability, notes on display colours and the gamma range |
| [PRIVACY.md](PRIVACY.md) | Privacy notes (local-first, no network, no telemetry) |

## Third-party components

Components shipped with Eventide keep their own copyrights and licenses. The full license texts are in [third-party/](third-party/).

| Component | Version | License | Shipped as | File |
| --- | --- | --- | --- | --- |
| .NET Runtime incl. Windows Forms | 10.0 | MIT | self-contained runtime | [third-party/DOTNET.md](third-party/DOTNET.md) |
| Avalonia UI (incl. Fluent theme, Inter font package) | 11.3.14 | MIT | library (settings window) | [third-party/AVALONIA.md](third-party/AVALONIA.md) |
| SkiaSharp / HarfBuzzSharp (via Avalonia) | – | MIT (+ Skia BSD-3-Clause, HarfBuzz MIT-style) | native libraries | [third-party/SKIASHARP.md](third-party/SKIASHARP.md) |
| CommunityToolkit.Mvvm | 8.4.0 | MIT | library | [third-party/COMMUNITYTOOLKIT-MVVM.md](third-party/COMMUNITYTOOLKIT-MVVM.md) |
| Material Design Icons (Pictogrammers) | 7.4.47 | Apache-2.0 | vector icons (window) and rendered PNGs (tray menu) | [third-party/MATERIAL-DESIGN-ICONS.md](third-party/MATERIAL-DESIGN-ICONS.md) |
| Inter (font) | 4.x | SIL OFL 1.1 | embedded font | [third-party/INTER-FONT.md](third-party/INTER-FONT.md) |

Shared license texts: [Apache License 2.0](third-party/LICENSE-APACHE-2.0.md) · [SIL Open Font License 1.1](third-party/LICENSE-OFL-1.1.md) · [MIT](third-party/LICENSE-MIT.md) · [BSD 3-Clause](third-party/LICENSE-BSD-3-CLAUSE.md)

## Algorithms (no third-party code)

| Algorithm | Source | Use |
| --- | --- | --- |
| Sunrise / sunset | NOAA Global Monitoring Laboratory, "General Solar Position Calculations" (public domain, US government work) | own implementation in `Eventide.Core/Solar` |
| Colour temperature → RGB | Tanner Helland, "How to Convert Temperature (K) to RGB" (published curve fit) | own implementation in `Eventide.Core/Color` |

Eventide deliberately does **not** use CoordinateSharp (AGPL-3.0 / commercial dual licence) or Redshift's colour table (GPL-3.0), so it can stay MIT-licensed.

Build-time only (not shipped): xUnit (Apache-2.0), Microsoft.NET.Test.Sdk (MIT), Inno Setup (own licence, installer only).
