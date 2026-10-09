# Mistik Launcher Ultra

Windows x64 için Türkçe / English Minecraft launcher. Oyuncu ayarları cihazında saklanır; sürüm, mod, skin ve yerel sunucu yönetimi içerir.

[Yayımlanan sürümleri indir](https://github.com/gamer3434/MistikLauncherUltra/releases/latest) · [Web sitesi](https://mistiklauncherultra.web.app/) · [6.2.5 değişiklikleri](docs/RELEASE-6.2.5.md)

## Kurulum ve onarım

Sürüm sayfasından online veya offline kurulum EXE'sini seçin. Taşınabilir kullanım için ZIP'in tüm dosyalarını aynı klasöre çıkartıp `MistikLauncher.exe` dosyasını açın. .NET çalışma zamanı pakete dahildir.

Program bozulursa launcher'ı kapatın, aynı veya daha yeni sürümün `MistikRepair-6.2.5.exe` dosyasını açıp kurulum klasöründe **Onar** seçin. Onarıcı internet gerektirmez; program dosyalarını doğrular ve yeniler. Ayarlar, modlar ve dünyalar korunur.

Mod merkezinde **Modrinth** veya **CurseForge** kaynağını seçebilirsiniz. CurseForge arama ve kurulum için **Ayarlar → Entegrasyonlar** bölümüne kendi API anahtarınızı girin; anahtar şifreli ayarlarda saklanır. Anahtar yoksa resmî site araması açılabilir. [CurseForge API bilgisi](https://docs.curseforge.com/rest-api/).

Varsayılan kurulum: `%LOCALAPPDATA%\Programs\MistikLauncherUltra`. Oyuncu verileri: `%APPDATA%\.mistik_ultra`. Şifreli ayarlar aynı Windows kullanıcı hesabında açılır. Paket şu anda genel yayıncı sertifikasıyla imzalı değildir.

## Kullanım

1. Sağ üstten Türkçe veya English seçin.
2. Ayarlarda oyuncu adını ve belleği kaydedin.
3. Sürümler sayfasından oyun sürümünü kurup alt çubuktan seçin.
4. Uyumlu modları seçip **Oyunu başlat** düğmesini kullanın.

Minecraft ve mod lisanslarına uyun. Microsoft hesabı kimlik doğrulaması bu launcher'a eklenmemiştir.

## Kaynaktan derleme

Windows ve .NET 8 SDK gerekir:

```powershell
dotnet build MistikLauncher/MistikLauncher.csproj -c Release
dotnet run --project Validation -c Release -- artifacts/screenshots-ordinary
node --test Validation/WebsiteReleaseTests.js Validation/WebsitePrivacyTests.js
./scripts/Build-Package.ps1
./scripts/Build-Installers.ps1
```

Normal paket kod gizleme olmadan derlenir. İsteğe bağlı korumalı derleme için `./scripts/Build-Protected.ps1` kullanılabilir. Paketler `artifacts/` altında üretilir. Sürüm yayımlama iş akışı yalnızca proje sürümüyle eşleşen `v*` etiketlerinde çalışır. EXE, ZIP, PDB ve kişisel yapılandırmalar kaynak deposuna eklenmez.

## Depo yapısı

| Klasör | İçerik |
| --- | --- |
| `MistikLauncher/` | Launcher ve dil kaynakları |
| `Installer/`, `Repair/`, `Uninstaller/`, `Updater/` | Kurulum, onarım ve güncelleme araçları |
| `Validation/`, `UpdateFixture/` | Doğrulama ve güncelleme test uygulaması |
| `site/` | Statik web sitesi |
| `scripts/`, `.github/` | Derleme ve yayın iş akışları |
| `docs/` | Sürüm notları ve teknik belgeler |

Eski Firebase hesapları, telemetri ve yönetici paneli kaldırılmıştır. İsteğe bağlı site dağıtımı için kişisel Firebase proje eşlemesi yerel `.firebaserc` dosyasındadır; depoda yalnızca örnek ve erişimi kapatan kurallar tutulur. [Gizlilik ve geçmiş temizliği](docs/REPOSITORY-PRIVACY.md).

## English

A bilingual Windows x64 Minecraft launcher with local player settings, version and mod management, skins and local server tools. Download the latest published installer or extract the complete portable ZIP. If an installation breaks, close the launcher and use the matching or newer repair EXE; settings, mods and worlds are preserved. Build and validation commands are above. Firebase account services and telemetry are retired; deployment mappings and credentials remain local.
