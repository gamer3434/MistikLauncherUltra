# Mistik Launcher 6 — Modernization preview / Modernizasyon önizlemesi

**Preview, not a completed stable replacement.** Home, settings and navigation are bilingual. Full legacy-page translation and actual Minecraft/mod/server verification remain outstanding. GitHub access is authenticated; the preview is published on `codex/modernization-preview`.

**Önizleme; kararlı sürümün tamamlanmış yeni versiyonu değildir.** Ana panel, ayarlar ve gezinme iki dillidir. Eski sayfaların tam çevirisi ile gerçek oyun/mod/sunucu testleri henüz tamamlanmadı. GitHub erişimi doğrulandı; önizleme `codex/modernization-preview` dalına gönderildi.

## Türkçe

Yeni ana panel oyuncu profili, sürüm, bellek ve hızlı işlemleri gösterir. Ayarlarda oyuncu adı, 1–32 GB bellek, cilt sağlayıcısı, renk ve otomatik kapanma seçilir. Dil listesi anında Türkçe / English geçişi sağlar ve tercih kaydedilir. Sayfa araması, oyun klasörü ve günlük kısayolları eklendi. Slate-blue renkler, Segoe UI yazı tipi, kaydırılabilir sayfalar ve görünür klavye odağı kullanılır.

Ayarlar atomik kaydedilir; önceki değerler `.bak` dosyasından kurtarılır. Donanım/IP telemetrisi ve kimlik doğrulamasız Firebase uzaktan yönetimi kaldırıldı. Sabit yönetici şifreleri ve eski kontrolsüz EXE değiştirme devre dışı bırakıldı; resmî doğrulanmış paket güncellemeleri yeni sistemle uygulanır. Açılışta sessiz kurulum, diğer başlatıcı işlemlerini sonlandırma ve otomatik sistem müdahaleleri kaldırıldı. MQTT için TLS yapılandırıldı; başlangıçta otomatik bağlantı kaldırıldı. Topluluk aktarımının güvenlik/kullanılabilirlik incelemesi sürüyor.

### Çalıştırma

Windows x64 için `artifacts/MistikLauncher-6-preview-win-x64.zip` paketini ayrı bir klasöre çıkartıp `MistikLauncher.exe` dosyasını açın. Paket kendi .NET çalışma zamanını içerir. **Paketin tüm dosyalarını bir arada tutun.** Bu önizleme taşınabilirdir; kurulum, kaldırma ve sertifika güven deposuna ekleme işlemleri kapalıdır. Veriler `%APPDATA%\.mistik_ultra` içinde kalır. Denemeden önce oyun klasörünü ve ayarları yedekleyin.

1. Kenar çubuğunda Türkçe veya English seçin.
2. Ayarlar sayfasında oyuncu adını ve belleği girip Değişiklikleri kaydet düğmesine basın.
3. Sürümler sayfasından oyun sürümünü indirin, alt çubukta seçin.
4. Uyumlu modları seçip Oyunu başlat düğmesini kullanın.

Gerçek oyun indirme/başlatma, mod taşıma, cilt ve sunucu çalıştırma bu değişiklikte uçtan uca test edilmedi. Microsoft hesabı kimlik doğrulaması eklenmedi. Minecraft ve mod lisanslarına uyun.

### Derleme ve doğrulama

Windows ve .NET 8 SDK gerekir:

```powershell
dotnet build MistikLauncher/MistikLauncher.csproj -c Release
dotnet run --project Validation -c Release -- artifacts/screenshots-ordinary
./scripts/Build-Protected.ps1
```

Koruma betiği sabit sürümlü aracı yükler, taşınabilir sürümü derler, korumalı DLL ile doğrulamayı çalıştırır ve ZIP üretir. Özel eşleme ve PDB dosyaları pakete eklenmez. `checksums.json` bozulma tespiti içindir; dijital imza değildir.

## English

The redesigned home shows player, version, memory and quick actions. Settings validate player names and 1–32 GB RAM, and offer skin provider, accent and automatic closing. The Turkish / English selector updates at runtime and persists the preference. Navigation search, game-folder and log shortcuts, scrollable layouts, Segoe UI typography and visible keyboard focus improve everyday use.

Configuration writes are atomic with recovery from the previous backup. Hardware/IP telemetry and unauthenticated Firebase administration were removed. Hardcoded administrator access and legacy arbitrary executable replacement are disabled; verified official package updates now use the new updater. Startup no longer silently installs, terminates other launcher processes or changes system preferences. MQTT uses TLS; automatic startup connection was removed. Community relay security/usability review remains outstanding.

### Run

Extract `artifacts/MistikLauncher-6-preview-win-x64.zip` to a dedicated Windows x64 folder and run `MistikLauncher.exe`. It includes its .NET runtime. **Keep all package files together.** Installation, uninstall execution and certificate trust changes are disabled in this portable preview. Data remains under `%APPDATA%\.mistik_ultra`. Back up settings and game files before trying it.

1. Select Türkçe or English in the sidebar.
2. Enter player name and RAM in Settings, then Save changes.
3. Download a version under Versions and select it in the bottom bar.
4. Choose compatible mods and select Launch game.

Actual game downloads/launches, mod migration, skins and server execution were not tested end to end. Microsoft account authentication was not added. Respect Minecraft and mod licenses.

### Build

Use the three commands above with .NET 8 SDK on Windows. The protection script restores Obfuscar 2.2.49, publishes a self-contained portable build, validates the protected DLL and packages it. Private obfuscation maps and debug symbols are excluded.

## Language resources / Dil kaynakları

Embedded `MistikLauncher/Locales/tr.json` and `en.json` share identical keys. `Localization.T("play")` returns **Oyunu başlat** / **Launch game**. New pages respond immediately to language events. Catalogued legacy strings are updated without recreating running server controllers. Full legacy-page translation remains incomplete.

Gömülü kaynakların anahtarları aynıdır. Yeni sayfalar dil değişikliğine anında yanıt verir; eski sayfaların tam çevirisi sürüyor. Eksik metinler [envanterde](docs/localization-remaining.txt) listelenir.

## Protection limits / Koruma sınırları

Eligible private member names and string constants are obfuscated. WPF entry points and JSON names are preserved. String hiding is reversible; it is not strong encryption and cannot secure embedded secrets. Public source remains readable. This preview is not Authenticode signed; SmartScreen and antivirus behavior cannot be guaranteed.

Özel üyeler ve metin sabitleri gizlenir; WPF/JSON adları korunur. Gizleme geri çevrilebilir; güçlü şifreleme değildir. Açık kaynak okunabilir kalır. Bu önizleme Authenticode imzası taşımaz; antivirüs veya SmartScreen garantisi yoktur.

## Screenshots / Ekran görüntüleri

Real off-screen WPF renders, not a running Minecraft session. / Gerçek WPF görüntüleri; açık Minecraft oturumu değildir.

![English home](docs/screenshots/home-en.png)
![Türkçe ayarlar](docs/screenshots/settings-tr.png)

## Files / Dosyalar

| File | Purpose / Amaç |
|---|---|
| `MistikLauncher/Core.cs` | Config recovery, normalization, telemetry removal, TLS / Ayarlar ve güvenlik |
| `MistikLauncher/App.xaml.cs` | Portable startup / Taşınabilir açılış |
| `MistikLauncher/MainWindow.xaml` | Shell, search, language, focus / Ana pencere |
| `MistikLauncher/MainWindow.xaml.cs` | Runtime switching and navigation / Dil değiştirme |
| `MistikLauncher/Pages/ModernPages.cs` | Bilingual home and settings / İki dilli sayfalar |
| `MistikLauncher/Localization.cs` | Embedded resource loader / Dil kaynakları |
| `MistikLauncher/ReleaseSecurity.cs` | Update and uninstall safeguards / Güvenlik |
| `scripts/Build-Protected.ps1` | Protected package / Korumalı paket |
| `Validation/Program.cs` | Thirteen checks and WPF renders / On üç denetim |
| `.github/workflows/validate.yml` | Windows CI / Windows doğrulama |

See [audit and outstanding work](docs/AUDIT.md). / [Denetim ve kalan işler](docs/AUDIT.md).

## Auto-MCS güncellemeleri / Auto-MCS updates

Sunucular sayfasındaki **Güncellemeleri denetle / güncelle** düğmesi resmî `macarooni-man/auto-mcs` deposunun son kararlı Windows ZIP paketini bulur. Eski sabit v5.3.0 bağlantısı kaldırıldı. Paket SHA-256 ve uzunluk ile doğrulanır, geçici klasörde hazırlanır ve EXE atomik değiştirilir. Önceki EXE `.bak` olarak korunur; oyun/sunucu ayarlarına dokunulmaz.

**Açılışta otomatik güncelleme** varsayılan olarak açıktır; Sunucular sayfasından kapatılabilir. Yüklü Auto-MCS başlatıcı açıldığında arka planda güncellenir. İlk kurulum kullanıcı düğmesiyle başlar. Açık Auto-MCS için güncelleme ertelenir; internet veya doğrulama hatası eski kurulumu korur. Güncel ve doğrulanmış sürüm tekrar indirilmez. Sunucu sayfasına girince uygulama otomatik açılmaz.

The **Check for updates / update** button selects the latest stable official Windows ZIP instead of the fixed mirrored release. Package size and official SHA-256 are verified before atomic EXE replacement, with the previous binary retained as `.bak`. Automatic startup updates are enabled by default and configurable on Servers. First installation requires the install button. Running Auto-MCS defers replacement; offline/digest failures preserve the old executable. Existing server data is left untouched.

The server page now displays installed/latest versions, progress, actionable status and release notes in Turkish and English. Ordinary/protected validation includes updater fixtures; an optional live check successfully downloaded and verified official v2.3.9 without executing it.

```powershell
dotnet run --project Validation -c Release -- artifacts/screenshots --live-mcs
```

![Türkçe sunucu çalışma alanı](docs/screenshots/server-tr.png)
![English server workspace](docs/screenshots/server-en.png)

## Otomatik launcher güncellemesi / Automatic launcher updates

**Ayarlar → Launcher güncellemeleri** bölümünde otomatik güncelleme varsayılan olarak açıktır. Başlatıcı açılışta ve açık kaldığı sürece her 30 dakikada resmî GitHub deposunun son **kararlı Release** sürümünü denetler. Daha yeni sürüm varsa Windows x64 ZIP paketinin resmî SHA-256 değeri, boyutu ve iç dosya manifesti doğrulanır. İndirme/ayar düzenleme gibi açık bir işlem sürüyorsa güncelleme ertelenir. Launcher güncelleme yardımcısı hazır olunca kapanır; yardımcı dosyaları yedekleyip değiştirir ve launcher'ı yeniden açar. Oyun dünyaları, ayarlar ve Auto-MCS kurulumuna dokunulmaz. Dosya değiştirme hatasında değiştirilen dosyalar geri alınır; yedekler geçici güncelleme klasöründe korunur.

**Bu paketi bir kez çıkartıp yeni EXE'yi açmanız gerekir.** Eski launcher sürümlerinde yeni yardımcı program bulunmaz. Kaynak kod commitleri otomatik güncelleme tetiklemez; yeni Windows ZIP içeren bir GitHub Release gerekir. Önizleme/prerelease ve eski sürümler otomatik yüklenmez.

Automatic updates are enabled under **Settings → Launcher updates**. The official latest stable GitHub Release is checked at startup and every 30 minutes. Newer Windows ZIPs are verified against the official SHA-256, declared size and complete internal manifest. Active work defers updates. A separate self-contained helper waits for the launcher to close, backs up/replaces package files and restarts it. Settings, worlds and Auto-MCS data are preserved. File replacement failures roll back changed files. Install this portable package once to bootstrap the new updater; source commits alone do not trigger updates.

### Yeni sürüm yayımlama / Publishing a new release

1. `MistikLauncher.csproj` içindeki `Version` değerini artırın ve değişiklikleri commit edin.
2. Aynı sürümle `vX.Y.Z` etiketi oluşturup GitHub'a gönderin.
3. `.github/workflows/release.yml` Windows üzerinde korumalı paketi derleyip doğrular ve ZIP içeren Release oluşturur. GitHub, asset SHA-256 değerini hesaplar; launcher bu değeri kullanır.

Bump the project Version, commit and push a matching `vX.Y.Z` tag. The release workflow validates the tag/version, builds/tests the protected package and publishes it. Tags containing a suffix create prereleases, which automatic updates skip. Packaging includes `MistikUpdater.exe` and `update-manifest.json`.

### Tasarım yenilemesi / Design refresh

Gece laciverti `#0D1727`, yüzey `#192C46`, açık metin `#EFF5FF`, ikincil metin `#ADBED6`, turkuaz/mavi vurgu ve Minecraft'a özgü blok çizimi. Ana panel, gezinme, eski kart renkleri ve ayarlar tutarlı palete geçirildi. Kaydet düğmesi kaydırmadan bağımsız alt alanda kalır. Önbellekli yeni sayfaların dil geçişi ayrıca doğrulanır.

Midnight navy, blue surfaces, turquoise accents, consistent typography and a Minecraft block illustration unify the home, shell and settings. Shared legacy cards inherit the new palette. Settings keep Save changes in a fixed footer. Cached bilingual pages are explicitly refreshed and validated.
# Forge ve renk temaları / Forge and color themes

Forge profillerinin görünmemesi ve seçim kaydının başarısız olması düzeltildi. Kurulum artık resmî Forge yükleyicisini kullanır. Ayarlardan beş uyumlu renk temasını anında seçebilirsiniz. Ayrıntılar, test kapsamı ve Java gereksinimleri: [Forge ve tema notları](docs/FORGE-AND-THEMES.md).

Official inherited Forge profiles are now selectable and selection persists. Installation uses the official Forge installer. Settings offers five coordinated, immediately applied color themes. See [Forge and theme notes](docs/FORGE-AND-THEMES.md) for validation limits and Java requirements.
