# NightLens

A Windows 11 desktop astrophotography planner. NightLens brings the observing-night forecast, Moon and darkness information, and equipment-specific target suggestions into one readable dashboard.

## Features

- Nightly observing verdict, seeing, transparency, clouds, temperature, wind and dew risk.
- Detailed conditions, hourly forecast, Moon phase, moonrise/set and twilight times.
- Up to three recommended objects for the selected night, shown one at a time.
- Equipment-aware framing and capture guidance, including multiple saved setups.
- Smart telescope selection, camera profiles, calibrated gain overrides and app-supported exposure choices.
- Bortle/SQM site settings, selectable deep-sky catalogs, and CSV catalog imports.
- Object pictures, altitude trajectories, coordinates, photometry and an offline sky map.
- Windows Location Services with a saved-location fallback.
- Light, Dark and red Night themes.

Target planning covers the full night and is independent of current weather and the time of day. Exposure, gain, and integration-time suggestions are starting estimates; confirm the settings supported by your equipment.

## Build on Windows

Use Windows 11 with .NET Framework 4.8 and Windows PowerShell. In the repository folder, run:

```powershell
powershell -ExecutionPolicy Bypass -File .\Source\Build.ps1
```

The first build downloads the Microsoft Roslyn 4.8.0 compiler from NuGet into `Source/.build`. To use an existing compiler instead:

```powershell
.\Source\Build.ps1 -CompilerPath 'C:\path\to\csc.exe'
```

The build creates `NightLens.exe` in this folder. Close a running copy before rebuilding.

## Run and distribute

Open `NightLens.exe`. Keep these folders beside it:

```text
Assets/
Catalogs/
EquipmentLibrary/
StarCatalog/
```

Include the source/credit documents when making a portable distribution. No Python, Node.js, API key or application account is required to run NightLens. Generated executables, compiler downloads and distribution ZIPs are excluded from Git; portable downloads can be attached to GitHub Releases later.

## Repository contents

| Folder | Contents |
| --- | --- |
| `Source` | C# application, included tests, build script, astronomy engine and time-zone mapping |
| `Assets` | Application icons and reference images |
| `Catalogs` | Deep-sky catalogs, derivation notes, authors and licenses |
| `EquipmentLibrary` | Camera and smart telescope profiles and provenance |
| `StarCatalog` | Offline star map data and its license |

`Read me.txt` contains detailed application instructions. `Capture guidance.txt`, `Weather data and verdict rules.txt`, and `Object data and image sources.txt` describe the planning rules and data sources. `Verification.txt` records historical validation of the original 8.0 build; it is not a report of a new live forecast test.

## Data and privacy

User settings and imported catalogs are stored under `%LOCALAPPDATA%\NightLens`, outside this repository. They are not included in this source ZIP. Image cache files and local settings should not be committed.

Forecast and city-search requests send observing coordinates or city names to Open-Meteo and 7Timer. Survey images are requested from CDS. Catalog calculations and the star map run locally. Weather and seeing are forecasts, not measurements at the observing site. Provider coverage and usage terms apply; the included documentation notes the free Open-Meteo endpoint's personal/noncommercial use.

## Publish privately using GitHub Desktop

1. Choose **File > Add local repository**.
2. Select this `NightLens` folder.
3. Click **Publish repository**.
4. Set **Name** to **NightLens** and leave **Keep this code private** checked.
5. Select your personal account and click **Publish repository**.

The repository becomes a private GitHub repository only after that publishing step. The local preparation does not create a remote repository or change the visibility of an existing one.

## Credits and licensing

A project-wide license has not been selected. Bundled third-party material retains its own notices and licenses:

- Astronomy Engine: `Source/ASTRONOMY-LICENSE.txt`.
- Unicode CLDR: `Source/UNICODE-LICENSE.txt`.
- OpenNGC and derived catalogs: `Catalogs/OpenNGC-LICENSE.txt`, authors and credits in `Catalogs/`.
- Star data and constellation artwork: notices in `StarCatalog/`.
- Reference photographs and remote survey imagery: `Object data and image sources.txt` and `CDS DSS2 color properties.txt`.
- Equipment specification sources: `EquipmentLibrary/Read me.txt` and profile source links.

This repository contains the Windows version only.
