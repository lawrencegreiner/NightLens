param([string]$CompilerPath, [string]$Version, [switch]$Verify)
$ErrorActionPreference = 'Stop'
$sourceRoot = $PSScriptRoot
$packageRoot = Split-Path -Parent $sourceRoot
$buildRoot = Join-Path $sourceRoot '.build'
if (-not $CompilerPath) { $CompilerPath = Join-Path $buildRoot 'compiler\tasks\net472\csc.exe' }
if (-not (Test-Path -LiteralPath $compilerPath)) {
    New-Item -ItemType Directory -Path $buildRoot -Force | Out-Null
    $archivePath = Join-Path $buildRoot 'compiler.zip'
    Invoke-WebRequest 'https://www.nuget.org/api/v2/package/Microsoft.Net.Compilers.Toolset/4.8.0' -OutFile $archivePath
    Expand-Archive -LiteralPath $archivePath -DestinationPath (Join-Path $buildRoot 'compiler') -Force
}
New-Item -ItemType Directory -Path $buildRoot -Force | Out-Null
$mainSource = Join-Path $sourceRoot 'NightLens.cs'
if ($Version) {
    if ($Version -notmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$') { throw 'Version must be MAJOR.MINOR.PATCH, for example 8.0.1.' }
    foreach ($part in $Version.Split('.')) { if ([long]$part -gt 65534) { throw 'Version components must be at most 65534.' } }
    $contents = Get-Content -LiteralPath $mainSource -Raw -Encoding UTF8
    $contents = [regex]::Replace($contents, '(Assembly(?:File)?Version\(")[^"]+("\))', ('${1}' + $Version + '.0${2}'))
    $mainSource = Join-Path $buildRoot 'NightLens.versioned.cs'
    [IO.File]::WriteAllText($mainSource, $contents, [Text.UTF8Encoding]::new($false))
}
$metadataRoot = Join-Path $env:WINDIR 'System32\WinMetadata'
$metadata = @('Windows.Devices.winmd','Windows.Foundation.winmd') | ForEach-Object { Join-Path $metadataRoot $_ }
if (@($metadata | Where-Object { -not (Test-Path -LiteralPath $_) }).Count) {
    $kits = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\UnionMetadata'
    $unions = @(Get-ChildItem -LiteralPath $kits -Filter Windows.winmd -File -Recurse -ErrorAction SilentlyContinue | Sort-Object FullName -Descending)
    if (-not $unions.Count) { throw 'Windows Runtime metadata is missing. Install the Windows 10/11 SDK (UnionMetadata/Windows.winmd).' }
    $metadata = @($unions[0].FullName)
}
$arguments = @('/nologo', '/target:winexe', '/optimize+',
    ('/win32icon:' + (Join-Path $packageRoot 'Assets\NightLens.ico')),
    ('/out:' + (Join-Path $packageRoot 'NightLens.exe')),
    '/reference:System.Windows.Forms.dll', '/reference:System.Drawing.dll',
    '/reference:System.Web.Extensions.dll', '/reference:System.Device.dll',
    '/reference:System.Xml.Linq.dll', '/reference:System.Runtime.dll', '/reference:System.IO.Compression.dll', '/reference:System.IO.Compression.FileSystem.dll',
    ('/resource:' + (Join-Path $sourceRoot 'windowsZones.xml') + ',windowsZones.xml'),
    $mainSource, (Join-Path $sourceRoot 'UI.cs'), (Join-Path $sourceRoot 'Sites.cs'), (Join-Path $sourceRoot 'SiteUI.cs'), (Join-Path $sourceRoot 'StartupScreen.cs'), (Join-Path $sourceRoot 'SurveyImages.cs'), (Join-Path $sourceRoot 'Updater.cs'), (Join-Path $sourceRoot 'UpdateUI.cs'),
    (Join-Path $sourceRoot 'Catalogs.cs'), (Join-Path $sourceRoot 'Style.cs'), (Join-Path $sourceRoot 'V2Tests.cs'),
    (Join-Path $sourceRoot 'CapturePlanning.cs'), (Join-Path $sourceRoot 'CaptureUI.cs'), (Join-Path $sourceRoot 'V4Tests.cs'), (Join-Path $sourceRoot 'ThemeTests.cs'),
    (Join-Path $sourceRoot 'Conditions.cs'), (Join-Path $sourceRoot 'ObservingWeather.cs'), (Join-Path $sourceRoot 'WeatherViews.cs'), (Join-Path $sourceRoot 'WeatherTests.cs'), (Join-Path $sourceRoot 'EquipmentLibrary.cs'), (Join-Path $sourceRoot 'EquipmentForm.cs'), (Join-Path $sourceRoot 'EquipmentTests.cs'), (Join-Path $sourceRoot 'ObjectDetails.cs'), (Join-Path $sourceRoot 'ImageService.cs'), (Join-Path $sourceRoot 'ImageServiceTests.cs'), (Join-Path $sourceRoot 'AltitudeChart.cs'), (Join-Path $sourceRoot 'SkyMap.cs'), (Join-Path $sourceRoot 'ObjectTests.cs'), (Join-Path $sourceRoot 'astronomy.cs'))
$arguments += @($metadata | ForEach-Object { "/reference:$_" })
& $compilerPath @arguments
if ($LASTEXITCODE -ne 0) { throw 'The build failed. Close NightLens if it is running and try again.' }
Write-Output ('Built ' + (Join-Path $packageRoot 'NightLens.exe'))



if ($Verify) {
    $checks = Join-Path $packageRoot 'NightLens.Checks.exe'
    $checkArgs = @($arguments | Where-Object { $_ -notlike '/target:*' -and $_ -notlike '/out:*' })
    $checkArgs += @('/target:exe', '/main:NightLens.ReleaseChecks', ('/out:' + $checks), (Join-Path $packageRoot 'Scripts\ReleaseChecks.cs'), (Join-Path $packageRoot 'Scripts\UpdaterChecks.cs'), (Join-Path $packageRoot 'Scripts\SiteImageChecks.cs'))
    & $compilerPath @checkArgs
    if ($LASTEXITCODE -ne 0) { throw 'Could not compile offline release checks.' }
    & $checks
    if ($LASTEXITCODE -ne 0) { throw 'Offline release checks failed; release must not be published.' }
}