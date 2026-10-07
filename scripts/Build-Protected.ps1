param([string]$Dotnet = 'dotnet', [string]$SigningThumbprint, [switch]$AllowLocalTestSignature)
& "$PSScriptRoot/Build-Package.ps1" -Obfuscate @PSBoundParameters
