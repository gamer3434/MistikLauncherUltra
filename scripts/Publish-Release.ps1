param([switch]$Publish)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
Push-Location $repo
try {
    [xml]$project=Get-Content MistikLauncher/MistikLauncher.csproj
    $version=$project.SelectSingleNode('/Project/PropertyGroup/Version').InnerText
    $tag="v$version"; $api='https://api.github.com/repos/gamer3434/MistikLauncherUltra'
    # Obtain existing Git credentials only in memory. Never print or save the secret.
    $credentialInput="protocol=https`nhost=github.com`n`n"
    $raw=$credentialInput | git credential fill
    if($LASTEXITCODE){ throw 'GitHub credentials unavailable' }
    $credentials=($raw -join "`n") | ConvertFrom-StringData
    if(!$credentials.password){ throw 'GitHub credentials unavailable' }
    $headers=@{Authorization="Bearer $($credentials.password)";Accept='application/vnd.github+json';'X-GitHub-Api-Version'='2022-11-28';'User-Agent'='MistikRelease'}
    $identity=Invoke-RestMethod -Uri 'https://api.github.com/user' -Headers $headers
    if($identity.login -ne 'gamer3434'){ throw 'Expected gamer3434 GitHub account' }
    $head=(git rev-parse HEAD).Trim()
    $assets=@('artifacts/installers/SHA256SUMS.txt',"artifacts/MistikLauncher-$version-win-x64.zip","artifacts/installers/MistikSetup-Online-$version.exe","artifacts/installers/MistikSetup-Offline-$version.exe")
    foreach($asset in $assets){ if(!(Test-Path -LiteralPath $asset -PathType Leaf)){ throw "Missing release file: $asset" } }
    foreach($line in Get-Content artifacts/installers/SHA256SUMS.txt){
        if($line -notmatch '^([a-f0-9]{64})  (.+)$'){ throw 'Malformed checksums file' }
        $expected=$Matches[1]; $name=$Matches[2]; $file=$assets | Where-Object { (Split-Path $_ -Leaf) -eq $name }
        if(!$file -or (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLower() -ne $expected){ throw "Checksum mismatch: $name" }
    }
    $body=Get-Content -LiteralPath docs/RELEASE-PREVIEW-9.md -Raw
    # GitHub's by-tag endpoint cannot resolve an unpublished tag in a draft release.
    $allReleases=Invoke-RestMethod -Uri "$api/releases?per_page=100" -Headers $headers
    $matchingReleases=@($allReleases | Where-Object tag_name -eq $tag)
    if($matchingReleases.Count -gt 1){ throw 'Multiple releases use this tag; review before publishing' }
    $release=if($matchingReleases.Count){$matchingReleases[0]}else{$null}
    if(!$release){ $payload=@{tag_name=$tag;target_commitish=$head;name="Mistik Launcher $version";body=$body;draft=$true;prerelease=$true} | ConvertTo-Json; $release=Invoke-RestMethod -Uri "$api/releases" -Method Post -Headers $headers -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($payload)) }
    foreach($asset in $assets){
        $name=Split-Path $asset -Leaf; $hash=(Get-FileHash -LiteralPath $asset -Algorithm SHA256).Hash.ToLower()
        $existing=@($release.assets | Where-Object name -eq $name)
        if($existing.Count){ if($existing[0].digest -ne "sha256:$hash"){ throw "Existing release asset differs: $name" }; continue }
        if(!$release.draft){ throw 'Do not mutate published release assets' }
        $uri="https://uploads.github.com/repos/gamer3434/MistikLauncherUltra/releases/$($release.id)/assets?name=$([Uri]::EscapeDataString($name))"
        Write-Host "Uploading: $name"
        # Windows' built-in curl streams large binaries reliably. Credentials travel on
        # stdin, never in process arguments, disk files, verbose logs or console output.
        $curl=Join-Path $env:SystemRoot 'System32/curl.exe'
        if(!(Test-Path -LiteralPath $curl)){ throw 'Windows curl is required for release uploads' }
        $configuration=@(('header = "Authorization: Bearer '+$credentials.password+'"'),'header = "Accept: application/vnd.github+json"','header = "X-GitHub-Api-Version: 2022-11-28"','header = "User-Agent: MistikRelease"','header = "Content-Type: application/octet-stream"') -join "`n"
        $responseText=$configuration | & $curl --config - --silent --show-error --fail-with-body --http1.1 --connect-timeout 30 --max-time 900 --request POST --data-binary ('@'+(Resolve-Path $asset).Path) $uri
        $configuration=$null
        if($LASTEXITCODE){ $uploadCode=$LASTEXITCODE; $apiError=''; try { $apiError=(($responseText -join "`n") | ConvertFrom-Json).message } catch { }; throw "Release upload failed: $name (curl $uploadCode): $apiError" }
        $uploaded=($responseText -join "`n") | ConvertFrom-Json
        if($uploaded.digest -ne "sha256:$hash"){ throw "GitHub digest mismatch: $name" }
        Write-Host "Verified upload: $name ($($uploaded.size) bytes)"
    }
    if($Publish -and $release.draft){ $payload=@{draft=$false} | ConvertTo-Json; $release=Invoke-RestMethod -Uri "$api/releases/$($release.id)" -Method Patch -Headers $headers -ContentType 'application/json' -Body $payload }
    Write-Host "Release: $($release.html_url); draft=$($release.draft)"
} finally { $raw=$null; $credentials=$null; $headers=$null; Pop-Location }
