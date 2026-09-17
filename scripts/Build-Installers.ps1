param([string]$Dotnet='dotnet',[string]$SigningThumbprint,[switch]$AllowLocalTestSignature)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
Push-Location $repo
try {
    [xml]$project=Get-Content MistikLauncher/MistikLauncher.csproj
    $version=$project.SelectSingleNode('/Project/PropertyGroup/Version').InnerText
    $zip=(Resolve-Path "artifacts/MistikLauncher-$version-win-x64.zip").Path
    $output=Join-Path $repo 'artifacts/installers'
    if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output -Recurse -Force }
    New-Item -ItemType Directory -Force $output | Out-Null
    & $Dotnet publish Installer/Installer.csproj -c Release -t:Rebuild "-p:Version=$version" -p:PayloadZipPath= -o artifacts/online-setup
    if($LASTEXITCODE){ throw 'Online setup build failed' }
    Copy-Item artifacts/online-setup/MistikSetup.exe "$output/MistikSetup-Online-$version.exe" -Force
    & $Dotnet publish Installer/Installer.csproj -c Release -t:Rebuild "-p:Version=$version" "-p:PayloadZipPath=$zip" -o artifacts/offline-setup
    if($LASTEXITCODE){ throw 'Offline setup build failed' }
    Copy-Item artifacts/offline-setup/MistikSetup.exe "$output/MistikSetup-Offline-$version.exe" -Force
    if($SigningThumbprint){
        & "$PSScriptRoot/Sign-Release.ps1" -PackagePath $output -Thumbprint $SigningThumbprint -AllowLocalTestSignature:$AllowLocalTestSignature -FileNames @("MistikSetup-Online-$version.exe","MistikSetup-Offline-$version.exe")
    }
    $reportFolder=Join-Path $repo 'artifacts/pc-test-installer'
    New-Item -ItemType Directory -Force $reportFolder | Out-Null
    $test=Start-Process -FilePath "$output/MistikSetup-Offline-$version.exe" -ArgumentList @('--verify-package',('"'+(Join-Path $reportFolder 'offline-verification.json')+'"')) -WindowStyle Hidden -Wait -PassThru
    if($test.ExitCode){ throw 'Embedded offline package verification failed' }
    $files=@($zip,"$output/MistikSetup-Online-$version.exe","$output/MistikSetup-Offline-$version.exe")
    $lines=foreach($file in $files){ (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLower()+'  '+(Split-Path $file -Leaf) }
    $lines | Set-Content "$output/SHA256SUMS.txt" -Encoding ascii
    Write-Host "Online + offline installers ready / Online ve yerel kurulum hazır: $version"
} finally { Pop-Location }
