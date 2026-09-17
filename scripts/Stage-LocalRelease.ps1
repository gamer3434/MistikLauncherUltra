param()
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
Push-Location $repo
try {
    [xml]$project=Get-Content MistikLauncher/MistikLauncher.csproj
    $version=$project.SelectSingleNode('/Project/PropertyGroup/Version').InnerText
    $api='https://api.github.com/repos/gamer3434/MistikLauncherUltra'
    $raw="protocol=https`nhost=github.com`n`n" | git credential fill
    $credentials=($raw -join "`n") | ConvertFrom-StringData
    $headers=@{Authorization="Bearer $($credentials.password)";Accept='application/vnd.github+json';'User-Agent'='MistikRelease'}
    $all=Invoke-RestMethod -Uri "$api/releases?per_page=100" -Headers $headers
    $matching=@($all | Where-Object tag_name -eq "v$version")
    if($matching.Count -ne 1 -or !$matching[0].draft){throw 'Expected one unpublished draft'}
    $release=$matching[0]; $assets=@{}
    function GetAssets {
        $result=@()
        for($page=1;$page -le 4;$page++){
            $items=Invoke-RestMethod -Uri "$api/releases/$($release.id)/assets?per_page=100&page=$page" -Headers $headers
            $result+=@($items)
            if(@($items).Count -lt 100){break}
        }
        return $result
    }
    $release.assets=@(GetAssets)
    foreach($asset in $release.assets){$assets[$asset.name]=$asset}
    $folder=Join-Path $repo 'artifacts/pc-test-release-stage'
    New-Item -ItemType Directory -Force $folder | Out-Null
    function Upload([string]$path,[string]$name){
        $hash=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLower()
        if($assets.ContainsKey($name)){
            if($assets[$name].digest -eq "sha256:$hash"){return}
            if($assets[$name].state -eq 'starter' -and !$assets[$name].digest -and $assets[$name].uploader.login -eq 'gamer3434'){Invoke-RestMethod -Uri $assets[$name].url -Method Delete -Headers $headers | Out-Null; $assets.Remove($name)}
            else {throw "Existing staged asset differs: $name"}
        }
        $configuration=@(('header = "Authorization: Bearer '+$credentials.password+'"'),'header = "Accept: application/vnd.github+json"','header = "User-Agent: MistikRelease"','header = "Content-Type: application/octet-stream"') -join "`n"
        $uri="https://uploads.github.com/repos/gamer3434/MistikLauncherUltra/releases/$($release.id)/assets?name=$([Uri]::EscapeDataString($name))"
        for($attempt=1;$attempt -le 3;$attempt++) {
            $response=@($configuration | & "$env:SystemRoot/System32/curl.exe" --config - --silent --show-error --fail-with-body --request POST --data-binary ('@'+$path) --max-time 180 --write-out "`nCURL_STATUS:%{http_code}" $uri)
            $exit=$LASTEXITCODE
            if(!$exit){break}
            $status=([string]$response[-1]).Replace('CURL_STATUS:','')
            if($attempt -eq 3 -or ($status -notin @('408','429','500','502','503','504') -and $exit -notin @(28,52,56))){throw "Part upload failed: $name ($status, curl $exit)"}
            # A timed-out response may still have saved the asset. Resolve it before retrying.
            $live=Invoke-RestMethod -Uri "$api/releases/$($release.id)" -Headers $headers
            if(!$live.draft){throw 'Release is no longer a draft'}
            $list=@(GetAssets)
            $saved=@($list | Where-Object name -eq $name)
            if($saved.Count){
                if($saved[0].digest -eq "sha256:$hash"){$assets[$name]=$saved[0]; Write-Host "Recovered verified stage: $name"; return}
                if($saved[0].state -eq 'starter' -and !$saved[0].digest -and $saved[0].uploader.login -eq 'gamer3434'){Invoke-RestMethod -Uri $saved[0].url -Method Delete -Headers $headers | Out-Null}
                else {throw 'Unexpected asset after interrupted upload'}
            }
            Write-Host "Retrying stage: $name (attempt $($attempt+1))"
            Start-Sleep -Seconds 2
        }
        $configuration=$null
        $uploaded=(($response | Where-Object {$_ -notmatch '^CURL_STATUS:'}) -join "`n") | ConvertFrom-Json
        if($uploaded.digest -ne "sha256:$hash"){throw "Part digest mismatch: $name"}
        $assets[$name]=$uploaded; Write-Host "Verified stage: $name"
    }
    $files=@("artifacts/MistikLauncher-$version-win-x64.zip","artifacts/installers/MistikSetup-Online-$version.exe","artifacts/installers/MistikSetup-Offline-$version.exe")
    $checksumLines=Get-Content artifacts/installers/SHA256SUMS.txt
    foreach($file in $files){$expectedLine=(Get-FileHash $file -Algorithm SHA256).Hash.ToLower()+'  '+(Split-Path $file -Leaf); if($expectedLine -notin $checksumLines){throw 'Local release checksum mismatch'}}
    $manifest=@{Version=$version;Files=@()}
    foreach($file in $files){
        $filePath=(Resolve-Path $file).Path; $name=Split-Path $filePath -Leaf
        $record=@{Name=$name;Size=(Get-Item $filePath).Length;Hash=(Get-FileHash $filePath -Algorithm SHA256).Hash.ToLower();Parts=@()}
        $source=[IO.File]::OpenRead($filePath)
        try {
            $buffer=[byte[]]::new(16MB); $index=0
            while($source.Position -lt $source.Length){
                $partName="stage-$version-$name-$index.part"
                # Preserve verified earlier 16 MiB pieces; use 4 MiB for new pieces.
                $wanted=4MB
                if($assets.ContainsKey($partName) -and $assets[$partName].state -eq 'uploaded' -and $assets[$partName].digest -and $assets[$partName].size -gt 0 -and $assets[$partName].size -le 16MB){$wanted=[int]$assets[$partName].size}
                $wanted=[int][Math]::Min([long]$wanted,$source.Length-$source.Position)
                $count=0
                while($count -lt $wanted -and $source.Position -lt $source.Length){$count+=$source.Read($buffer,$count,$wanted-$count)}
                $partPath=Join-Path $folder 'part.bin'
                $output=[IO.File]::Create($partPath); try {$output.Write($buffer,0,$count)} finally {$output.Dispose()}
                $record.Parts+=@{Name=$partName;Hash=(Get-FileHash $partPath -Algorithm SHA256).Hash.ToLower()}
                Upload $partPath $partName; $index++
            }
        } finally {$source.Dispose()}
        $manifest.Files+=$record
    }
    $path=Join-Path $folder 'manifest.json'; $manifest | ConvertTo-Json -Depth 8 | Set-Content $path -Encoding utf8
    Upload $path "stage-$version-manifest.json"
    $payload=@{ref='main';inputs=@{release_id=[string]$release.id}} | ConvertTo-Json -Depth 3
    Invoke-RestMethod -Uri "$api/actions/workflows/finalize-local-release.yml/dispatches" -Method Post -Headers $headers -ContentType 'application/json' -Body $payload | Out-Null
    Write-Host "Dispatched signed-release assembly: $($release.id)"
} finally {$raw=$null;$credentials=$null;$headers=$null;Pop-Location}
