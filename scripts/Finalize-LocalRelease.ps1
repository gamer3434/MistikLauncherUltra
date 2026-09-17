param([Parameter(Mandatory)][string]$ReleaseId)
$ErrorActionPreference='Stop'
if($ReleaseId -notmatch '^\d+$'){throw 'Invalid release id'}
if($env:GITHUB_REPOSITORY -ne 'gamer3434/MistikLauncherUltra'){throw 'Unexpected repository'}
[xml]$project=Get-Content MistikLauncher/MistikLauncher.csproj
$version=$project.SelectSingleNode('/Project/PropertyGroup/Version').InnerText
$tag="v$version"; $api='https://api.github.com/repos/gamer3434/MistikLauncherUltra'
$headers=@{Authorization="Bearer $env:GH_TOKEN";Accept='application/vnd.github+json';'User-Agent'='MistikRelease'}
$release=Invoke-RestMethod -Uri "$api/releases/$ReleaseId" -Headers $headers
if(!$release.draft -or $release.tag_name -ne $tag){throw 'Only this version draft can be finalized'}
$release.assets=Invoke-RestMethod -Uri "$api/releases/$ReleaseId/assets?per_page=100" -Headers $headers
$metadataName="stage-$version-manifest.json"
$metadataAsset=@($release.assets | Where-Object name -eq $metadataName)
if($metadataAsset.Count -ne 1){throw 'Missing stage manifest'}
$work=Join-Path $env:RUNNER_TEMP ('MistikRelease-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $work | Out-Null
$downloadHeaders=@{Authorization="Bearer $env:GH_TOKEN";Accept='application/octet-stream';'User-Agent'='MistikRelease'}
$metadataPath=Join-Path $work 'manifest.json'
Invoke-WebRequest -Uri $metadataAsset[0].url -Headers $downloadHeaders -OutFile $metadataPath
if((Get-FileHash $metadataPath -Algorithm SHA256).Hash.ToLower() -ne $metadataAsset[0].digest.Substring(7)){throw 'Stage manifest digest mismatch'}
$manifest=Get-Content $metadataPath -Raw | ConvertFrom-Json
$expected=@("MistikLauncher-$version-win-x64.zip","MistikSetup-Online-$version.exe","MistikSetup-Offline-$version.exe")
if($manifest.Version -ne $version -or @($manifest.Files).Count -ne 3){throw 'Invalid manifest'}
$cleanup=@($metadataAsset[0].id)
foreach($file in $manifest.Files){
    if($file.Name -notin $expected -or $file.Hash -notmatch '^[a-f0-9]{64}$' -or $file.Size -le 0 -or $file.Size -gt 512MB -or @($file.Parts).Count -gt 100){throw 'Invalid staged file'}
    $expected=@($expected | Where-Object {$_ -ne $file.Name})
    $output=Join-Path $work $file.Name
    $destination=[IO.File]::Create($output)
    try {
        $index=0
        foreach($part in $file.Parts){
            $name="stage-$version-$($file.Name)-$index.part"
            if($part.Name -ne $name -or $part.Hash -notmatch '^[a-f0-9]{64}$'){throw 'Invalid part name or hash'}
            $asset=@($release.assets | Where-Object name -eq $name)
            if($asset.Count -ne 1 -or $asset[0].size -gt 16MB -or $asset[0].digest -ne "sha256:$($part.Hash)"){throw 'Part metadata mismatch'}
            $path=Join-Path $work 'part.bin'
            Invoke-WebRequest -Uri $asset[0].url -Headers $downloadHeaders -OutFile $path
            if((Get-FileHash $path -Algorithm SHA256).Hash.ToLower() -ne $part.Hash){throw 'Part data mismatch'}
            $source=[IO.File]::OpenRead($path); try {$source.CopyTo($destination)} finally {$source.Dispose()}
            $cleanup+=$asset[0].id; $index++
        }
    } finally {$destination.Dispose()}
    if((Get-Item $output).Length -ne $file.Size -or (Get-FileHash $output -Algorithm SHA256).Hash.ToLower() -ne $file.Hash){throw 'Combined file differs from tested local binary'}
    $existing=@($release.assets | Where-Object name -eq $file.Name)
    if($existing.Count){if($existing[0].digest -ne "sha256:$($file.Hash)"){throw 'Existing final asset differs'}}
    else {
        $configuration=@(('header = "Authorization: Bearer '+$env:GH_TOKEN+'"'),'header = "Accept: application/vnd.github+json"','header = "User-Agent: MistikRelease"','header = "Content-Type: application/octet-stream"') -join "`n"
        $uri="https://uploads.github.com/repos/gamer3434/MistikLauncherUltra/releases/$ReleaseId/assets?name=$([Uri]::EscapeDataString($file.Name))"
        $result=$configuration | & "$env:SystemRoot/System32/curl.exe" --config - --silent --show-error --fail --request POST --data-binary ('@'+$output) $uri
        $configuration=$null
        if($LASTEXITCODE){throw 'Final binary upload failed'}
        $uploaded=($result -join "`n") | ConvertFrom-Json
        if($uploaded.digest -ne "sha256:$($file.Hash)"){throw 'Final GitHub digest mismatch'}
    }
    Write-Host "Verified final binary: $($file.Name)"
}
if($expected.Count){throw 'Missing expected final binary'}
$fresh=Invoke-RestMethod -Uri "$api/releases/$ReleaseId" -Headers $headers
if(!$fresh.draft -or $fresh.tag_name -ne $tag){throw 'Draft changed during assembly'}
foreach($id in $cleanup){Invoke-RestMethod -Uri "$api/releases/assets/$id" -Headers $headers -Method Delete | Out-Null}
$payload=@{draft=$false} | ConvertTo-Json
$published=Invoke-RestMethod -Uri "$api/releases/$ReleaseId" -Headers $headers -Method Patch -ContentType 'application/json' -Body $payload
Write-Host "Published: $($published.html_url)"
