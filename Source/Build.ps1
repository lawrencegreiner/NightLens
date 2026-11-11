param([string]$CompilerPath)
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
$arguments = @('/nologo', '/target:winexe', '/optimize+',
    ('/win32icon:' + (Join-Path $packageRoot 'Assets\NightLens.ico')),
    ('/out:' + (Join-Path $packageRoot 'NightLens.exe')),
    '/reference:System.Windows.Forms.dll', '/reference:System.Drawing.dll',
    '/reference:System.Web.Extensions.dll', '/reference:System.Device.dll',
    '/reference:System.Xml.Linq.dll', '/reference:System.Runtime.dll',
    ('/reference:' + (Join-Path $env:WINDIR 'System32\WinMetadata\Windows.Devices.winmd')),
    ('/reference:' + (Join-Path $env:WINDIR 'System32\WinMetadata\Windows.Foundation.winmd')),
    ('/resource:' + (Join-Path $sourceRoot 'windowsZones.xml') + ',windowsZones.xml'),
    (Join-Path $sourceRoot 'NightLens.cs'), (Join-Path $sourceRoot 'UI.cs'),
    (Join-Path $sourceRoot 'Catalogs.cs'), (Join-Path $sourceRoot 'Style.cs'), (Join-Path $sourceRoot 'V2Tests.cs'),
    (Join-Path $sourceRoot 'CapturePlanning.cs'), (Join-Path $sourceRoot 'CaptureUI.cs'), (Join-Path $sourceRoot 'V4Tests.cs'), (Join-Path $sourceRoot 'ThemeTests.cs'),
    (Join-Path $sourceRoot 'Conditions.cs'), (Join-Path $sourceRoot 'ObservingWeather.cs'), (Join-Path $sourceRoot 'WeatherViews.cs'), (Join-Path $sourceRoot 'WeatherTests.cs'), (Join-Path $sourceRoot 'EquipmentLibrary.cs'), (Join-Path $sourceRoot 'EquipmentForm.cs'), (Join-Path $sourceRoot 'EquipmentTests.cs'), (Join-Path $sourceRoot 'ObjectDetails.cs'), (Join-Path $sourceRoot 'ImageService.cs'), (Join-Path $sourceRoot 'ImageServiceTests.cs'), (Join-Path $sourceRoot 'AltitudeChart.cs'), (Join-Path $sourceRoot 'SkyMap.cs'), (Join-Path $sourceRoot 'ObjectTests.cs'), (Join-Path $sourceRoot 'astronomy.cs'))
& $compilerPath @arguments
if ($LASTEXITCODE -ne 0) { throw 'The build failed. Close NightLens if it is running and try again.' }
Write-Output ('Built ' + (Join-Path $packageRoot 'NightLens.exe'))


