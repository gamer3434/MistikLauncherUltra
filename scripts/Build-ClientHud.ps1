param()
$ErrorActionPreference='Stop'
$ProgressPreference='SilentlyContinue'
$repo=Split-Path $PSScriptRoot -Parent
$version='8.10.2'
$cache=Join-Path $env:LOCALAPPDATA "MistikLauncherBuildTools/gradle-$version"
$gradle=Join-Path $cache "gradle-$version/bin/gradle.bat"
$localGradle=Join-Path $repo "artifacts/client-hud-tools/gradle-$version/bin/gradle.bat"
if(!(Test-Path -LiteralPath $gradle) -and (Test-Path -LiteralPath $localGradle)) {
    New-Item -ItemType Directory -Path $cache -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $repo "artifacts/client-hud-tools/gradle-$version") -Destination $cache -Recurse -Force
}
if (!(Test-Path -LiteralPath $gradle)) {
    New-Item -ItemType Directory -Path $cache -Force | Out-Null
    $zip=Join-Path $cache 'gradle.zip'
    $url="https://services.gradle.org/distributions/gradle-$version-bin.zip"
    $expected=(Invoke-RestMethod -Uri "$url.sha256").Trim()
    if($expected -notmatch '^[a-fA-F0-9]{64}$') { throw 'Invalid Gradle checksum metadata' }
    Invoke-WebRequest -Uri $url -OutFile $zip -UseBasicParsing -TimeoutSec 180
    if((Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash -ne $expected) { throw 'Gradle checksum mismatch' }
    Expand-Archive -LiteralPath $zip -DestinationPath $cache -Force
}
if (!$env:JAVA_HOME) {
    $java=Get-ChildItem -Path "$env:ProgramFiles/Java/jdk-21*/bin/java.exe" -File -ErrorAction SilentlyContinue | Select-Object -First 1
    if(!$java) { $java=Get-ChildItem -LiteralPath (Join-Path $env:APPDATA '.mistik_ultra/java') -Filter java.exe -Recurse -File | Select-Object -First 1 }
    if(!$java) { throw 'Java 21 is required to build the in-game HUD' }
    $env:JAVA_HOME=Split-Path (Split-Path $java.FullName -Parent) -Parent
}
$project=Join-Path $repo 'ClientHud'
if($project -cmatch '[^\x00-\x7F]') {
    # Java's Windows test worker cannot decode non-ASCII classpath argument files.
    $project=Join-Path $env:LOCALAPPDATA 'MistikLauncherBuildTools/client-hud'
    New-Item -ItemType Directory -Path $project -Force | Out-Null
    $source=Join-Path $project 'src'
    if(Test-Path -LiteralPath $source) {
        if([IO.Path]::GetFullPath($source) -ne [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'MistikLauncherBuildTools/client-hud/src'))) { throw 'Invalid HUD build staging path' }
        Remove-Item -LiteralPath $source -Recurse -Force
    }
    Copy-Item -LiteralPath (Join-Path $repo 'ClientHud/src') -Destination $project -Recurse -Force
    Copy-Item -LiteralPath (Join-Path $repo 'ClientHud/build.gradle'),(Join-Path $repo 'ClientHud/settings.gradle'),(Join-Path $repo 'ClientHud/gradle.properties') -Destination $project -Force
}
& $gradle -p $project --no-daemon build
if($LASTEXITCODE) { throw 'HUD mod build failed' }
if($project -ne (Join-Path $repo 'ClientHud')) {
    $output=Join-Path $repo 'ClientHud/build/libs'
    New-Item -ItemType Directory -Path $output -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $project 'build/libs/mistik-hud-1.0.0.jar') -Destination $output -Force
}
