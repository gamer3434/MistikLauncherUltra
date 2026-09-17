param(
    [Parameter(Mandatory)][string]$PackagePath,
    [Parameter(Mandatory)][string]$Thumbprint,
    [switch]$AllowLocalTestSignature
)
$ErrorActionPreference = 'Stop'
$package = (Resolve-Path -LiteralPath $PackagePath).Path
$certificate = Get-Item -LiteralPath "Cert:/CurrentUser/My/$Thumbprint"
if (!$certificate.HasPrivateKey -or $certificate.NotAfter -le (Get-Date) -or $certificate.NotBefore -gt (Get-Date)) {
    throw 'A valid private signing certificate is required / Geçerli imzalama sertifikası gerekli.'
}
if ('1.3.6.1.5.5.7.3.3' -notin @($certificate.EnhancedKeyUsageList | ForEach-Object { [string]$_.ObjectId })) {
    throw 'Certificate is not for code signing / Sertifika kod imzalamaya uygun değil.'
}
foreach ($name in @('MistikLauncher.exe','MistikLauncher.dll','MistikUpdater.exe')) {
    $file = Join-Path $package $name
    if (!(Test-Path -LiteralPath $file -PathType Leaf)) { throw "Missing package file: $name" }
    $signature = Set-AuthenticodeSignature -LiteralPath $file -Certificate $certificate -HashAlgorithm SHA256
    $verified = Get-AuthenticodeSignature -LiteralPath $file
    $chain = [System.Security.Cryptography.X509Certificates.X509Chain]::new()
    $chain.ChainPolicy.RevocationMode = 'NoCheck'
    $chainValid = $chain.Build($certificate)
    $localRootOnly = !$chainValid -and @($chain.ChainStatus).Count -gt 0 -and @($chain.ChainStatus | Where-Object { $_.Status -ne 'UntrustedRoot' }).Count -eq 0
    $chain.Dispose()
    # PowerShell can report an untrusted self-signed root as UnknownError instead of NotTrusted.
    $accepted = $verified.Status -eq 'Valid' -or ($AllowLocalTestSignature -and $localRootOnly -and $verified.Status -in @('NotTrusted','UnknownError'))
    if (!$accepted -or $verified.SignerCertificate.Thumbprint -ne $certificate.Thumbprint) {
        throw "Signature verification failed: $name ($($verified.Status)): $($signature.StatusMessage)"
    }
    Write-Host "$name : $($verified.Status)"
}
Export-Certificate -Cert $certificate -FilePath (Join-Path $package 'publisher.cer') | Out-Null
@{
    Publisher = $certificate.Subject
    Thumbprint = $certificate.Thumbprint
    Trust = $(if ($AllowLocalTestSignature) { 'Local test only; not publicly trusted / Yalnızca yerel test; genel güven sağlamaz' } else { 'Windows certificate validation passed at build time' })
    Timestamped = $false
} | ConvertTo-Json | Set-Content (Join-Path $package 'signing-info.json') -Encoding utf8
