param([string]$ArtifactDirectory='dist')
$ErrorActionPreference='Stop'
if(!$env:GH_TOKEN){throw 'GH_TOKEN is required; GitHub Actions supplies its built-in repository token.'}
if($env:GITHUB_REPOSITORY -notmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$'){throw 'Invalid repository name.'}
$manifest=Get-Content -LiteralPath (Join-Path $ArtifactDirectory 'build-metadata.json') -Raw | ConvertFrom-Json
if($manifest.version -notmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$' -or $manifest.tag -ne ('v'+$manifest.version) -or $manifest.commit -notmatch '^[0-9a-f]{40}$'){throw 'Invalid release metadata.'}
if($manifest.commit -ne $env:GITHUB_SHA){throw 'Artifact source does not match this workflow run.'}
if($manifest.archive -ne ('NightLens-Windows11-'+$manifest.tag+'.zip')){throw 'Invalid portable ZIP filename.'}
$zip=Join-Path $ArtifactDirectory $manifest.archive
if((Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant() -ne $manifest.sha256){throw 'Artifact ZIP checksum does not match.'}
$api='https://api.github.com/repos/'+$env:GITHUB_REPOSITORY
$headers=@{Authorization=('Bearer '+$env:GH_TOKEN);Accept='application/vnd.github+json';'User-Agent'='NightLens-release-workflow';'X-GitHub-Api-Version'='2022-11-28'}
try{
    $existing=$null
    try{$existing=Invoke-RestMethod -Uri ($api+'/releases/tags/'+$manifest.tag) -Headers $headers -TimeoutSec 30}catch{if(!$_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne 404){throw}}
    if($existing){throw 'This release already exists. Existing releases are never replaced. Choose a new version, or remove a failed draft before retrying.'}
    $tag=$null
    try{$tag=Invoke-RestMethod -Uri ($api+'/git/ref/tags/'+$manifest.tag) -Headers $headers -TimeoutSec 30}catch{if(!$_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne 404){throw}}
    if($tag){$obj=$tag.object;for($i=0;$obj.type -eq 'tag' -and $i -lt 5;$i++){$obj=(Invoke-RestMethod -Uri $obj.url -Headers $headers -TimeoutSec 30).object};if($obj.type -ne 'commit' -or $obj.sha -ne $manifest.commit){throw 'The release tag points to different source code.'}}
    $notes=@"
Portable Windows release built and checked by GitHub Actions.

### Download and run
1. Download **$($manifest.archive)** under Assets.
2. Extract the entire ZIP to a writable folder on Windows 11.
3. Open the NightLens folder and double-click **NightLens.exe**.
4. Keep Assets, Catalogs, EquipmentLibrary and StarCatalog beside the executable.

No installer, compiler, Python or Node.js is required to run. Equipment and location settings remain under %LOCALAPPDATA%\NightLens. The ZIP includes source credits, licenses and usage instructions. This application is unsigned.

The build passed offline planning, equipment, catalog, weather and astronomy checks, plus per-file portable ZIP integrity checks. SHA256SUMS.txt contains the download checksum. A private repository requires an authorized GitHub sign-in to download.

Source commit: $($manifest.commit)
"@
    $payload=@{tag_name=$manifest.tag;target_commitish=$manifest.commit;name=('NightLens '+$manifest.version+' - Portable Windows');body=$notes;draft=$true;prerelease=$false} | ConvertTo-Json
    $release=Invoke-RestMethod -Method Post -Uri ($api+'/releases') -Headers $headers -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($payload)) -TimeoutSec 30
    $upload=$release.upload_url.Split('{')[0]
    if(([Uri]$upload).Host -ne 'uploads.github.com'){throw 'Unexpected upload host.'}
    foreach($name in @($manifest.archive,'SHA256SUMS.txt')){
        $file=Join-Path $ArtifactDirectory $name;$type=if($name.EndsWith('.zip')){'application/zip'}else{'text/plain'}
        $asset=Invoke-RestMethod -Method Post -Uri ($upload+'?name='+[Uri]::EscapeDataString($name)) -Headers $headers -ContentType $type -InFile $file -TimeoutSec 120
        $hash=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()
        if($asset.state -ne 'uploaded' -or $asset.size -ne (Get-Item -LiteralPath $file).Length -or $asset.digest -ne ('sha256:'+$hash)){throw ('GitHub asset integrity check failed: '+$name)}
    }
    $published=Invoke-RestMethod -Method Patch -Uri ($api+'/releases/'+$release.id) -Headers $headers -ContentType 'application/json' -Body '{"draft":false,"make_latest":"true"}' -TimeoutSec 30
    if($published.draft){throw 'Release is still a draft.'}
    if($env:GITHUB_STEP_SUMMARY){Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value ('Published ['+$published.name+']('+$published.html_url+').')}
    Write-Output ('Published '+$published.html_url)
}finally{$headers.Clear()}
