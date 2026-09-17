# Code signing policy / Kod imzalama politikası

## Current status / Güncel durum

The current Preview.9 artifact was built without a signing certificate and is **unsigned**: its application, updater, uninstaller and setup executables carry no Authenticode signature. SHA-256 manifests/checksums detect file changes; they do not establish publisher identity or Windows trust. Optional signing is maintainer-only.

Güncel Preview.9 çıktısı sertifika verilmeden üretildi ve **imzasızdır**: uygulama, güncelleyici, kaldırıcı ve kurulum EXE'lerinde Authenticode imzası yoktur. SHA-256 manifest/checksum dosyaları dosya değişikliklerini tespit eder; yayıncı kimliğini veya Windows güvenini kanıtlamaz. İsteğe bağlı imzalama yalnızca maintainer içindir.

An intact self-signed signature can still produce SmartScreen warnings. A publicly trusted certificate also does not guarantee warning-free distribution. / Sağlam bir yerel imza SmartScreen uyarısını kaldırmaz; genel güvenilir imza da uyarısızlığı garanti etmez. [Microsoft documentation / Microsoft belgesi](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation).

## Maintainer-only signing / Yalnızca maintainer imzalama

Use `scripts/Build-Protected.ps1 -SigningThumbprint ...` followed by `scripts/Build-Installers.ps1 -SigningThumbprint ...` only in a controlled maintainer environment. `-AllowLocalTestSignature` is for an explicitly untrusted test certificate in the current-user store; it does not create public trust. Never distribute private keys.

## Free publicly trusted option / Ücretsiz genel güven seçeneği

[SignPath Foundation](https://signpath.org/) offers signing for eligible open-source projects. This project **has not applied or been approved**; no SignPath-signed artifact is distributed. Applying requires the maintainer's participation and the foundation's review. Its requirements include open-source licensing for the components, maintenance/documentation, MFA, defined author/reviewer/approver roles and a published signing/privacy policy. Eligibility and license coverage must be confirmed by the repository owner; project metadata alone is not a license audit. [Conditions](https://signpath.org/terms.html).

SignPath Foundation uygun açık kaynak projeleri için ücretsiz imzalama sunar. Bu proje için **başvuru yapılmadı ve onay alınmadı**; mevcut dosyalar SignPath imzalı değildir. Başvuru depo sahibinin katılımını ve vakfın incelemesini gerektirir. Lisans kapsamı, bakım/belgeler, MFA, yazar/inceleyici/onaylayıcı rolleri ve imzalama/gizlilik politikası koşulları bulunur. Lisans uygunluğu depo sahibi tarafından doğrulanmalıdır; proje metadata'sı tek başına lisans denetimi değildir.

Prepared project information / Hazır proje bilgileri:

- Repository / Depo: `https://github.com/gamer3434/MistikLauncherUltra`
- Project / Proje: Mistik Launcher Ultra, Windows x64, WPF/.NET 8
- Owner / Sahip: `gamer3434`
- Downloads / İndirmeler: GitHub Releases; online/offline setup and portable ZIP
- Build / Derleme: `scripts/Build-Protected.ps1`, `scripts/Build-Installers.ps1`
- Remaining / Gereken: maintainer contact, confirmed licensing/MFA/roles/privacy policy, application and approval / iletişim, lisans/MFA/rol/gizlilik doğrulaması, başvuru ve onay

No contact email, API key, legal agreement or signing-service account has been supplied or submitted on the maintainer's behalf. / Depo sahibi adına e-posta, API anahtarı, hukuki sözleşme veya imzalama hesabı verilmedi ya da gönderilmedi.
