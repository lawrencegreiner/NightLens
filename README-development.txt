NightLens Windows source code

Contains the C# Windows Forms source, tests, build script, application icon,
equipment library, target catalogs, star catalog, reference images, and licenses.

Build on Windows 11 with Windows PowerShell and .NET Framework 4.8:
  powershell -ExecutionPolicy Bypass -File .\Source\Build.ps1
Run that command from this extracted NightLens-Windows-Source folder.
The build script downloads the Microsoft C# compiler if needed (internet required
for that first build). NightLens.exe is created beside the Source folder.
Keep Assets, Catalogs, EquipmentLibrary, and StarCatalog beside the executable.

Your personal settings and image cache are not included.
Third-party licenses and source credits are retained in the corresponding folders.
The Mac version uses a separate Electron interface; its source archive includes
its own README-development.txt and Build-Mac.py.
