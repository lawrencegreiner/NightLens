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
- Saved observing sites with a dashboard selector, site-specific Bortle/SQM and horizon masks.
- Interactive skyline editor and Stellarium .hrz imports; target windows exclude obstructions.
- Windows Location Services with a saved-location fallback.
- NightLens loading screen with live startup stages.
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

Include the source/credit documents when making a portable distribution. No Python, Node.js, API key or application account is required to run NightLens. Generated executables, compiler downloads and distribution ZIPs are excluded from Git; GitHub Actions publishes portable downloads for version tags or a manual release run.

## Automated Windows builds and releases

The **Windows portable build and release** workflow runs on GitHub's hosted Windows build server. No personal access token or extra repository secret is required; the publishing job uses GitHub's built-in repository token.

- Pushes to `main` and pull requests build the app, run offline checks, and save a portable build under the workflow run's **Artifacts**. They do not publish a release.
- Pushing a new tag such as `v8.0.1` automatically builds and publishes that version as a GitHub Release after all checks pass.
- For a manual release, open **Actions > Windows portable build and release > Run workflow**, select `main`, enter a new version such as `8.0.1`, check **Publish a new portable release**, and run it. The workflow creates the matching tag and release.
- Leave the publish checkbox off for a build-only test. A blank version uses the source's current default version for this mode.

Use `MAJOR.MINOR.PATCH` versions with numeric components at most 65534. The workflow stamps the executable and About window from the requested version. The source's default version is used for ordinary local builds. Existing releases are never overwritten; use a new version for each release. If an upload fails after creating a draft, inspect/remove that failed draft before retrying the same version.

Portable release assets are a Windows ZIP and `SHA256SUMS.txt`. The workflow checks catalogs, equipment guidance, astronomical darkness, offline target planning, reference images, weather verdicts, sky-map data, daylight-saving behavior, and update package validation/rollback. It then verifies each file in the portable ZIP before uploading, checks GitHub's asset digests, and only then publishes. These offline checks do not replace manual UI testing or live forecast-service checks. Workflow artifacts are retained for 14 days; published release downloads remain attached to the release.

Local equivalents:

```powershell
.\Source\Build.ps1 -Version 8.0.1 -Verify
.\Scripts\Package-Portable.ps1 -Version 8.0.1
```

The first build requires NuGet access for the pinned C# compiler. Hosted runners use the Windows SDK metadata if desktop Windows metadata is absent. Release automation does not code-sign the app or change repository visibility. This repository is public; its release downloads do not require a GitHub sign-in.

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

Forecast and city-search requests send observing coordinates or city names to Open-Meteo and 7Timer. Color reference images use DSS2 static HiPS tiles from CSIRO/CDS. Catalog calculations and the star map run locally. Weather and seeing are forecasts, not measurements at the observing site. Provider coverage and usage terms apply; the included documentation notes the free Open-Meteo endpoint's personal/noncommercial use.

## In-app updates (8.1.0 and later)

NightLens checks the public GitHub releases at startup and every six hours. A newer stable version appears in the gear menu and dashboard footer. Open **Gear > Updates** for **Check now**, the automatic-check preference, and **Download and restart**. GitHub sign-in is not required.

The old 8.0.x app has no updater. Download and extract 8.1.1 or later once to get this feature; later versions can be installed from inside NightLens.

After you choose Download and restart, NightLens downloads the versioned portable ZIP, compares it with GitHub's SHA-256 digest, and validates the package's per-file manifest. The app closes only after the updater is ready. A separate temporary helper waits for the running app to exit, backs up existing managed files, installs the checked package, and restarts NightLens. Copy failures trigger rollback; the recovery folder is retained under `%LOCALAPPDATA%\NightLens\Updates`. Equipment, location, imported catalogs and update-check preferences stay in the user-data folder. Additional files in the portable app folder are retained.

Updates need a writable local app folder. Source checkouts containing `.git` are not overwritten; use a separate extracted portable copy. If other programs lock app files, the update stops and reports recovery information. The updater does not request administrator access or alter Windows security settings. Packages remain unsigned; checksums detect altered/incomplete downloads but are not a code-signing certificate. The trusted update source is this repository's stable GitHub releases.

The ZIP includes `update-manifest.json`; keep it in the portable package. Future workflow releases create this manifest automatically. Only stable `vMAJOR.MINOR.PATCH` releases are offered. Downgrades and preview builds are not installed through this updater.

## Credits and licensing

A project-wide license has not been selected. Bundled third-party material retains its own notices and licenses:

- Astronomy Engine: `Source/ASTRONOMY-LICENSE.txt`.
- Unicode CLDR: `Source/UNICODE-LICENSE.txt`.
- OpenNGC and derived catalogs: `Catalogs/OpenNGC-LICENSE.txt`, authors and credits in `Catalogs/`.
- Star data and constellation artwork: notices in `StarCatalog/`.
- Reference photographs and remote survey imagery: `Object data and image sources.txt` and `CDS DSS2 color properties.txt`.
- Equipment specification sources: `EquipmentLibrary/Read me.txt` and profile source links.

This repository contains the Windows version only.

## Saved sites and local horizons (8.2.0)

Open **Gear > Saved observing sites** to save the current location or add/edit a travel site. Each site keeps its coordinates, time zone, sky brightness and skyline. Select a site from the dashboard to plan there; choose Windows location to resume automatic positioning. Equipment setups remain available across sites.

Open **Gear > Horizon mask** to click/drag skyline points, fine-tune azimuth/altitude, or import a Stellarium `.hrz` file. North is 0 degrees and azimuth increases eastward. Import supports degrees, radians or gradians, altitude or zenith distance, plus a landscape rotation. Set the matching format/rotation from Stellarium landscape.ini when needed. The altitude floor defaults to 30 degrees. Planning counts only uninterrupted windows of at least 30 minutes above both the floor and skyline, sampled every minute when a mask/custom floor is used. A saved skyline is not applied more than 1 km from its recorded position.

Color images use independently hosted DSS2 survey tiles, local tile reprojection, persistent caching and bounded downloads. Real survey plate artifacts and saturated cores may remain. The original startup artwork is drawn as vectors; object reference images are scientific survey data. See `Startup artwork.txt` for artwork provenance.

Version 8.2.1 groups settings under **Observing site**, **Planning**, and **Appearance**. Updates and About remain directly accessible. Pan-STARRS cutouts with blank white coverage borders fall back to DSS2 automatically. The loading screen uses an original optical-aperture design.

Version 8.2.2 removes Pan-STARRS from reference images. Horizon-aware ranking also favors longer uninterrupted clear windows (3 points/hour, capped at 6 hours) while retaining equipment, altitude, Moon and brightness factors. Each recommended window must clear the active skyline for at least 30 continuous minutes.
