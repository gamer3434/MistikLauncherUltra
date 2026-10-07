param([string]$Dotnet='dotnet',[string]$SigningThumbprint,[switch]$AllowLocalTestSignature)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
Push-Location $repo
try {
    [xml]$project=Get-Content MistikLauncher/MistikLauncher.csproj
    $version=$project.SelectSingleNode('/Project/PropertyGroup/Version').InnerText
    $zip=(Resolve-Path "artifacts/MistikLauncher-$version-win-x64.zip").Path
    $output=Join-Path $repo 'artifacts/installers'
    $resolvedOutput=[IO.Path]::GetFullPath($output)
    $allowedRoot=[IO.Path]::GetFullPath((Join-Path $repo 'artifacts'))+[IO.Path]::DirectorySeparatorChar
    if(!$resolvedOutput.StartsWith($allowedRoot,[StringComparison]::OrdinalIgnoreCase)){ throw 'Installer output path must remain inside artifacts' }
    if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output -Recurse -Force }
    New-Item -ItemType Directory -Force $output | Out-Null
    & $Dotnet publish Installer/Installer.csproj -c Release -t:Rebuild "-p:Version=$version" -p:PayloadZipPath= -o artifacts/online-setup
    if($LASTEXITCODE){ throw 'Online setup build failed' }
    Copy-Item artifacts/online-setup/MistikSetup.exe "$output/MistikSetup-Online-$version.exe" -Force
    & $Dotnet publish Installer/Installer.csproj -c Release -t:Rebuild "-p:Version=$version" "-p:PayloadZipPath=$zip" -o artifacts/offline-setup
    if($LASTEXITCODE){ throw 'Offline setup build failed' }
    Copy-Item artifacts/offline-setup/MistikSetup.exe "$output/MistikSetup-Offline-$version.exe" -Force
    & $Dotnet publish Repair/Repair.csproj -c Release "-p:Version=$version" "-p:PayloadZipPath=$zip" -o artifacts/repair
    if($LASTEXITCODE){ throw 'Repair tool build failed' }
    Copy-Item artifacts/repair/MistikRepair.exe "$output/MistikRepair-$version.exe" -Force
    if($SigningThumbprint){
        & "$PSScriptRoot/Sign-Release.ps1" -PackagePath $output -Thumbprint $SigningThumbprint -AllowLocalTestSignature:$AllowLocalTestSignature -FileNames @("MistikSetup-Online-$version.exe","MistikSetup-Offline-$version.exe","MistikRepair-$version.exe")
    }
    $reportFolder=Join-Path $repo 'artifacts/pc-test-installer'
    New-Item -ItemType Directory -Force $reportFolder | Out-Null
    $test=Start-Process -FilePath "$output/MistikSetup-Offline-$version.exe" -ArgumentList @('--verify-package',('"'+(Join-Path $reportFolder 'offline-verification.json')+'"')) -WindowStyle Hidden -Wait -PassThru
    if($test.ExitCode){ throw 'Embedded offline package verification failed' }
    $repairTest=Start-Process -FilePath "$output/MistikRepair-$version.exe" -ArgumentList @('--verify-package',('"'+(Join-Path $reportFolder 'repair-verification.json')+'"')) -WindowStyle Hidden -Wait -PassThru
    if($repairTest.ExitCode){ throw 'Embedded repair package verification failed' }
    $files=@($zip,"$output/MistikSetup-Online-$version.exe","$output/MistikSetup-Offline-$version.exe","$output/MistikRepair-$version.exe")
    $lines=foreach($file in $files){ (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLower()+'  '+(Split-Path $file -Leaf) }
    $lines | Set-Content "$output/SHA256SUMS.txt" -Encoding ascii
    Write-Host "Online + offline installers ready / Online ve yerel kurulum hazır: $version"
} finally { Pop-Location }
