param([string]$Dotnet = 'dotnet', [string]$SigningThumbprint, [switch]$AllowLocalTestSignature)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
Push-Location $repo
try {
    & $Dotnet tool restore
    if ($LASTEXITCODE) { throw 'Tool restore failed / Araç yüklenemedi' }
    $buildRoot = Join-Path $repo artifacts/build
    [xml]$projectXml = Get-Content MistikLauncher/MistikLauncher.csproj
    $version = [string]$projectXml.SelectSingleNode("/Project/PropertyGroup/Version").InnerText
    $portableDir = Join-Path $repo "artifacts/portable-$version"
    if (Test-Path -LiteralPath $portableDir) { Remove-Item -LiteralPath $portableDir -Recurse -Force }
    & $Dotnet publish MistikLauncher/MistikLauncher.csproj -c Release --self-contained true -p:PublishSingleFile=false "-p:BaseOutputPath=$buildRoot/" -o $portableDir
    if ($LASTEXITCODE) { throw 'Build failed / Derleme başarısız' }
    $inputPath = (Resolve-Path $portableDir).Path
    $outputPath = Join-Path $repo artifacts/obfuscated
    if (Test-Path -LiteralPath $outputPath) { Remove-Item -LiteralPath $outputPath -Recurse -Force }
    New-Item -ItemType Directory -Force $outputPath | Out-Null
    # Find framework directories without assuming a specific SDK patch version.
    $runtimeLines = & $Dotnet --list-runtimes
    $searchPaths = foreach ($line in $runtimeLines) {
        if ($line -match '^Microsoft\.(?:NETCore|WindowsDesktop)\.App\s+(\S+)\s+\[(.+)\]') {
            '<AssemblySearchPath path="' + [System.Security.SecurityElement]::Escape((Join-Path $Matches[2] $Matches[1])) + '" />'
        }
    }
    $inputXml = [System.Security.SecurityElement]::Escape($inputPath)
    $outputXml = [System.Security.SecurityElement]::Escape($outputPath)
    $config = @"
<Obfuscator>
  <Var name="InPath" value="$inputXml" />
  <Var name="OutPath" value="$outputXml" />
  <Var name="KeepPublicApi" value="true" />
  <Var name="HidePrivateApi" value="true" />
  <Var name="HideStrings" value="true" />
  <Var name="UseUnicodeNames" value="true" />
  $($searchPaths -join [Environment]::NewLine)
  <Module file="$inputXml/MistikLauncher.dll">
    <SkipType name="MistikLauncher.MainWindow" skipMethods="true" skipFields="true" skipProperties="true" skipEvents="true" />
    <SkipType name="MistikLauncher.Application" skipMethods="true" skipFields="true" skipProperties="true" skipEvents="true" />
    <SkipType name="MistikLauncher.Windows.*" skipMethods="true" skipFields="true" skipProperties="true" skipEvents="true" />
    <SkipType name="MistikLauncher.LauncherConfig" skipMethods="true" skipFields="true" skipProperties="true" skipEvents="true" />
  </Module>
</Obfuscator>
"@
    $config | Set-Content artifacts/obfuscar.xml -Encoding utf8
    & $Dotnet tool run obfuscar.console artifacts/obfuscar.xml
    if ($LASTEXITCODE) { throw 'Obfuscation failed / Kod koruması başarısız' }
    & $Dotnet build Validation/Validation.csproj -c Release "-p:BaseOutputPath=$buildRoot/"
    if ($LASTEXITCODE) { throw 'Validation build failed / Doğrulama derlenemedi' }
    $validationBin = Join-Path $buildRoot Release/net8.0-windows/win-x64
    Copy-Item "$outputPath/MistikLauncher.dll" "$validationBin/MistikLauncher.dll" -Force
    Copy-Item "$outputPath/MistikLauncher.dll" "$inputPath/MistikLauncher.dll" -Force
    & $Dotnet publish Updater/Updater.csproj -c Release -o artifacts/helper
    if ($LASTEXITCODE) { throw 'Update helper build failed / Güncelleme yardımcısı derlenemedi' }
    Copy-Item artifacts/helper/MistikUpdater.exe "$inputPath/MistikUpdater.exe" -Force
    & $Dotnet publish Uninstaller/Uninstaller.csproj -c Release -o artifacts/uninstaller
    if ($LASTEXITCODE) { throw 'Uninstaller build failed / Kaldırıcı derlenemedi' }
    Copy-Item artifacts/uninstaller/MistikUninstall.exe "$inputPath/MistikUninstall.exe" -Force
    & $Dotnet publish UpdateFixture/UpdateFixture.csproj -c Release -o artifacts/fixture
    if ($LASTEXITCODE) { throw 'Updater fixture build failed' }
    # Debug symbols and private obfuscation maps are excluded from distributed packages.
    Get-ChildItem $inputPath -Filter '*.pdb' | Remove-Item
    if ($SigningThumbprint) {
        & "$PSScriptRoot/Sign-Release.ps1" -PackagePath $inputPath -Thumbprint $SigningThumbprint -AllowLocalTestSignature:$AllowLocalTestSignature
        Copy-Item "$inputPath/MistikLauncher.dll" "$validationBin/MistikLauncher.dll" -Force
    }
    $checksums = @(Get-ChildItem $inputPath -File -Recurse |
        Where-Object { $_.Name -notin @('checksums.json','update-manifest.json') } |
        ForEach-Object {
            @{ File = [IO.Path]::GetRelativePath($inputPath, $_.FullName).Replace('\','/'); Hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash }
        })
    $checksums | ConvertTo-Json -Depth 3 | Set-Content "$inputPath/checksums.json" -Encoding utf8
    [xml]$projectXml = Get-Content MistikLauncher/MistikLauncher.csproj
    $version = [string]$projectXml.SelectSingleNode("/Project/PropertyGroup/Version").InnerText
    $files = @(Get-ChildItem $inputPath -File -Recurse | Where-Object { $_.Name -ne 'update-manifest.json' } | ForEach-Object {
        @{ Path = [IO.Path]::GetRelativePath($inputPath, $_.FullName).Replace('\','/'); Hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash }
    })
    @{ Product='MistikLauncher'; Version=$version; Files=$files } | ConvertTo-Json -Depth 5 | Set-Content "$inputPath/update-manifest.json" -Encoding utf8
    & $Dotnet "$validationBin/Validation.dll" artifacts/screenshots --verify-package $inputPath --helper-smoke artifacts/helper/MistikUpdater.exe artifacts/fixture/MistikLauncher.exe
    if ($LASTEXITCODE) { throw "Packaged manifest verification failed" }
    $zipPath = "artifacts/MistikLauncher-$version-win-x64.zip"
    Compress-Archive -Path "$inputPath/*" -DestinationPath $zipPath -Force
    Write-Host 'Protected portable package ready / Korumalı taşınabilir paket hazır.'
} finally { Pop-Location }
