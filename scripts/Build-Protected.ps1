param([string]$Dotnet = 'dotnet')
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
Push-Location $repo
try {
    & $Dotnet tool restore
    if ($LASTEXITCODE) { throw 'Tool restore failed / Araç yüklenemedi' }
    & $Dotnet publish MistikLauncher/MistikLauncher.csproj -c Release --self-contained true -p:PublishSingleFile=false -o artifacts/portable
    if ($LASTEXITCODE) { throw 'Build failed / Derleme başarısız' }
    $inputPath = (Resolve-Path artifacts/portable).Path
    $outputPath = Join-Path $repo artifacts/obfuscated
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
    & $Dotnet build Validation/Validation.csproj -c Release
    if ($LASTEXITCODE) { throw 'Validation build failed / Doğrulama derlenemedi' }
    $validationBin = Join-Path $repo Validation/bin/Release/net8.0-windows/win-x64
    Copy-Item "$outputPath/MistikLauncher.dll" "$validationBin/MistikLauncher.dll" -Force
    & $Dotnet "$validationBin/Validation.dll" artifacts/screenshots
    if ($LASTEXITCODE) { throw 'Protected validation failed / Korumalı doğrulama başarısız' }
    Copy-Item "$outputPath/MistikLauncher.dll" "$inputPath/MistikLauncher.dll" -Force
    # Debug symbols and private obfuscation maps are excluded from distributed packages.
    Get-ChildItem $inputPath -Filter '*.pdb' | Remove-Item
    Get-ChildItem $inputPath -File | Get-FileHash -Algorithm SHA256 |
        Select-Object @{Name='File'; Expression={Split-Path $_.Path -Leaf}}, Hash |
        ConvertTo-Json | Set-Content "$inputPath/checksums.json" -Encoding utf8
    Compress-Archive -Path "$inputPath/*" -DestinationPath artifacts/MistikLauncher-6-preview-win-x64.zip -Force
    Write-Host 'Protected portable package ready / Korumalı taşınabilir paket hazır.'
} finally { Pop-Location }
