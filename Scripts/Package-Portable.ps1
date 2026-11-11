param([Parameter(Mandatory=$true)][string]$Version, [string]$OutputDirectory)
$ErrorActionPreference='Stop'
if($Version -notmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$'){throw 'Version must be MAJOR.MINOR.PATCH.'}
$root=Split-Path -Parent $PSScriptRoot
if(!$OutputDirectory){$OutputDirectory=Join-Path $root 'dist'}
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$exe=Join-Path $root 'NightLens.exe'
if((Get-Item -LiteralPath $exe).VersionInfo.FileVersion -ne ($Version+'.0')){throw 'Executable version does not match the requested release version.'}
$commit=git -C $root rev-parse HEAD
if($LASTEXITCODE -ne 0 -or $commit -notmatch '^[0-9a-f]{40}$'){throw 'Could not identify the source commit.'}
$name='NightLens-Windows11-v'+$Version+'.zip'
$archivePath=Join-Path $OutputDirectory $name
if(Test-Path -LiteralPath $archivePath){throw 'Output ZIP already exists. Choose a fresh output folder.'}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::Open($archivePath,[IO.Compression.ZipArchiveMode]::Create)
$expected=@{}
function Add-File([string]$File,[string]$EntryName){
    [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$File,$EntryName,[IO.Compression.CompressionLevel]::Optimal) | Out-Null
    $expected[$EntryName]=(Get-FileHash -LiteralPath $File -Algorithm SHA256).Hash
}
function Add-Text([string]$Text,[string]$EntryName){
    $bytes=[Text.Encoding]::UTF8.GetBytes($Text)
    $entry=$archive.CreateEntry($EntryName);$stream=$entry.Open()
    try{$stream.Write($bytes,0,$bytes.Length)}finally{$stream.Dispose()}
    $sha=[Security.Cryptography.SHA256]::Create();try{$expected[$EntryName]=[BitConverter]::ToString($sha.ComputeHash($bytes)).Replace('-','')}finally{$sha.Dispose()}
}
try{
    Add-File $exe 'NightLens/NightLens.exe'
    foreach($folder in @('Assets','Catalogs','EquipmentLibrary','StarCatalog')){
        foreach($file in Get-ChildItem -LiteralPath (Join-Path $root $folder) -File -Recurse){
            Add-File $file.FullName ('NightLens/'+$file.FullName.Substring($root.Length+1).Replace('\','/'))
        }
    }
    foreach($file in Get-ChildItem -LiteralPath $root -Filter '*.txt' -File | Where-Object {$_.Name -ne 'README-development.txt'}){Add-File $file.FullName ('NightLens/'+$file.Name)}
    foreach($license in @('ASTRONOMY-LICENSE.txt','UNICODE-LICENSE.txt')){Add-File (Join-Path $root ('Source\'+$license)) ('NightLens/Licenses/'+$license)}
    Add-Text ("NightLens $Version - Portable Windows application`n`nExtract the entire ZIP into a writable folder on Windows 11.`nOpen the NightLens folder and double-click NightLens.exe.`nKeep Assets, Catalogs, EquipmentLibrary and StarCatalog beside the executable.`nNo installer, compiler, Python, Node.js, account or API key is needed to run.`nInternet is needed for weather, city search and new reference pictures.`nEquipment and location settings stay under %LOCALAPPDATA%\NightLens.`nUse the gear for equipment, location, catalogs, Bortle and appearance.`nSee Read me.txt and the bundled documents for sources and planning limits.`nThis is an unsigned personal Windows application.`n") 'NightLens/START HERE.txt'
    Add-Text ("Version: $Version`nSource commit: $commit`nExecutable SHA-256: "+(Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLowerInvariant()+"`n") 'NightLens/Build information.txt'
    $manifestFiles=@($expected.Keys | Sort-Object | ForEach-Object { @{path=$_.Substring(10);sha256=$expected[$_].ToLowerInvariant()} })
    Add-Text (@{schema=1;version=$Version;files=$manifestFiles} | ConvertTo-Json -Depth 5) 'NightLens/update-manifest.json'
}finally{$archive.Dispose()}
$check=[IO.Compression.ZipFile]::OpenRead($archivePath)
try{
    foreach($required in @('NightLens/NightLens.exe','NightLens/Assets/NightLens.ico','NightLens/Catalogs/NGC.csv','NightLens/Catalogs/IC.csv','NightLens/Catalogs/Messier.csv','NightLens/Catalogs/Supplement.csv','NightLens/EquipmentLibrary/zwo-profiles.json','NightLens/StarCatalog/stars.json','NightLens/Assets/Objects/Moon.jpg','NightLens/Licenses/ASTRONOMY-LICENSE.txt','NightLens/Licenses/UNICODE-LICENSE.txt','NightLens/START HERE.txt')){if(!$check.GetEntry($required)){throw ('Missing portable file: '+$required)}}
    if($check.Entries.Count -ne $expected.Count){throw 'ZIP file count mismatch.'}
    foreach($entry in $check.Entries){
        if($entry.FullName -match '(^|/)(settings\.json|ObjectImages|\.git|\.build)(/|$)' -or $entry.FullName -like '*NightLens.Checks.exe'){throw 'A local-only file was included.'}
        $stream=$entry.Open();$sha=[Security.Cryptography.SHA256]::Create()
        try{$actual=[BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','')}finally{$stream.Dispose();$sha.Dispose()}
        if($actual -ne $expected[$entry.FullName]){throw ('ZIP data check failed: '+$entry.FullName)}
    }
}finally{$check.Dispose()}
$hash=(Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $OutputDirectory 'SHA256SUMS.txt'),($hash+'  '+$name+"`n"),[Text.UTF8Encoding]::new($false))
$manifest=@{version=$Version;tag=('v'+$Version);commit=$commit;archive=$name;sha256=$hash;bytes=(Get-Item -LiteralPath $archivePath).Length;files=$expected.Count}
[IO.File]::WriteAllText((Join-Path $OutputDirectory 'build-metadata.json'),($manifest | ConvertTo-Json),[Text.UTF8Encoding]::new($false))
Write-Output ("Portable package verified: $name ($($expected.Count) files), SHA-256 $hash")
