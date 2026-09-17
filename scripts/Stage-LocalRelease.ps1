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
    $release.assets=Invoke-RestMethod -Uri "$api/releases/$($release.id)/assets?per_page=100" -Headers $headers
    foreach($asset in $release.assets){$assets[$asset.name]=$asset}
    $folder=Join-Path $repo 'artifacts/pc-test-release-stage'
    New-Item -ItemType Directory -Force $folder | Out-Null
    function Upload([string]$path,[string]$name){
        $hash=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLower()
        if($assets.ContainsKey($name)){if($assets[$name].digest -ne "sha256:$hash"){throw "Existing staged asset differs: $name"}; return}
        $configuration=@(('header = "Authorization: Bearer '+$credentials.password+'"'),'header = "Accept: application/vnd.github+json"','header = "User-Agent: MistikRelease"','header = "Content-Type: application/octet-stream"') -join "`n"
        $uri="https://uploads.github.com/repos/gamer3434/MistikLauncherUltra/releases/$($release.id)/assets?name=$([Uri]::EscapeDataString($name))"
        $response=$configuration | & "$env:SystemRoot/System32/curl.exe" --config - --silent --show-error --fail --request POST --data-binary ('@'+$path) --max-time 180 $uri
        $configuration=$null
        if($LASTEXITCODE){throw "Part upload failed: $name"}
        $uploaded=($response -join "`n") | ConvertFrom-Json
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
                $count=0
                while($count -lt $buffer.Length -and $source.Position -lt $source.Length){$count+=$source.Read($buffer,$count,$buffer.Length-$count)}
                $partName="stage-$version-$name-$index.part"; $partPath=Join-Path $folder 'part.bin'
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
