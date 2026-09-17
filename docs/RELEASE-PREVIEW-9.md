# Mistik Launcher 6.0.0-preview.9

## Reasoning / Gerekçe

The requested online setup must validate downloads, while the offline setup must include all runtime/application files. Installation and uninstall must preserve Minecraft data. Font bearings were causing the close glyph to look off center; a symmetric vector fixes that.

Online kurulum indirmeyi doğrulamalı; yerel kurulum tüm uygulama ve çalışma zamanı dosyalarını içermeli. Kurulum/kaldırma oyun verilerini korumalı. Çarpının font hizalama farkı simetrik vektörle giderilir.

## Implementation / Uygulama

- Rounded rectangle close outline (38×24) with centered vector cross; smooth RGB outline/glow, no other caption illumination. / Ortalı vektör çarpı, yuvarlatılmış dikdörtgen ve yalnızca çıkışta RGB.
- Turkish/English online and offline setup, optional desktop shortcut, Start menu shortcut, Windows uninstall entry and progress/cancellation. Per-user installation, no elevation. / Türkçe/İngilizce iki kurulum seçeneği, kısayollar, kaldırma kaydı, ilerleme ve iptal; yönetici izni gerekmez.
- Online setup downloads this release's portable ZIP from the official repository; verifies GitHub SHA-256 digest, expected size, HTTPS host, every manifest file and version. Offline setup uses its embedded ZIP and validates the same manifest. / Online resmi ZIP ve SHA-256 doğrulaması; yerel gömülü ZIP ve aynı dosya doğrulaması.
- Staging before commit; refuses nonempty folders, traversal, symlinks and damaged packages. Uninstall preflights locks and removes only recorded files. Unknown files and `%APPDATA%\.mistik_ultra` are preserved. / Kurulum öncesi hazırlık, yol ve bütünlük kontrolleri; yalnızca kayıtlı dosyaları kaldırma, kullanıcı verilerini koruma.
- SHA-256 Authenticode signatures on our application DLL/EXE, updater, uninstaller and two setup EXEs. Private certificate keys never leave the Windows certificate store. / Kendi uygulama ve kurulum dosyalarımızda SHA-256 imza; özel anahtar dağıtılmaz.
- Obsolete root executables and installer sources were removed from Git tracking; use the release downloads. A modified setup copy returned HashMismatch, confirming tamper detection. / Eski kök EXE'leri ve kurulum kaynakları Git'ten kaldırıldı; yayın dosyalarını kullanın. Değiştirilmiş kurulum kopyası HashMismatch ile reddedildi.

Code / Kod: `MistikLauncher/WindowAppearance.cs`, `Installer/Installer.csproj`, `Installer/Program.cs`, `Installer/InstallEngine.cs`, `Uninstaller/Program.cs`, `Uninstaller/Uninstaller.csproj`, `Validation/InstallerTests.cs`, `scripts/Sign-Release.ps1`, `scripts/Build-Protected.ps1`, `scripts/Build-Installers.ps1`, `scripts/Publish-Release.ps1`.

Build / Derle:

```powershell
./scripts/Build-Protected.ps1 -SigningThumbprint YOUR_THUMBPRINT -AllowLocalTestSignature
./scripts/Build-Installers.ps1 -SigningThumbprint YOUR_THUMBPRINT -AllowLocalTestSignature
```

For a publicly trusted certificate omit `-AllowLocalTestSignature`. Signing supports optional `-TimestampServer` in `Sign-Release.ps1`; this local release has no timestamp. / Genel güvenilir sertifikada yerel test seçeneğini kullanmayın. İmzalama betiği isteğe bağlı zaman damgasını destekler; bu yerel yayında zaman damgası yoktur.

## Conclusion / Sonuç

209 protected-package checks passed, including actual RGB color change and synchronized glow in both languages. The actual offline EXE verifies its embedded files, installs them to an isolated fixture, uninstalls owned files and checks user-file preservation. Preview remains prerelease: full legacy translation, community relay review and the third-party Auto-MCS wizard remain outstanding.

209 korumalı paket kontrolü geçti. Gerçek yerel kurulum EXE'si gömülü paketi doğrular, ayrı test klasörüne kurar, kendi dosyalarını kaldırır ve kullanıcı dosyasını korur. Önizlemedir; eski sayfaların tam çevirisi, topluluk bağlantı incelemesi ve üçüncü taraf Auto-MCS sihirbazı henüz tamamlanmadı.

**Certificate limitation / Sertifika sınırı:** this is a free self-signed local test certificate, not a publicly trusted publisher certificate. UntrustedRoot/UnknownError is not a missing or corrupted signature; changing the computer's trusted root store would not make downloads publicly trusted. AV/SmartScreen acceptance cannot be guaranteed. / Ücretsiz yerel test sertifikası genel yayıncı güveni sağlamaz. Bilgisayarın güven deposunu değiştirerek bu durum gizlenmez; antivirüs uyarısızlığı garanti edilmez.
