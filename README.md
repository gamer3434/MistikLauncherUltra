# Mistik Launcher 6 — Modernization preview / Modernizasyon önizlemesi

**Preview, not a completed stable replacement.** Home, settings and navigation are bilingual. Actual Vanilla/Forge gameplay and an isolated server were tested; full legacy translation and broader feature verification remain outstanding. Preview source is published on `codex/modernization-preview`.

**Önizleme; tamamlanmış kararlı sürüm değildir.** Gerçek Vanilla/Forge oyun ve ayrı sunucu testleri yapıldı. Eski sayfaların tam çevirisi ve daha geniş özellik testleri sürüyor. Kaynak `codex/modernization-preview` dalında.

### Güncel sürüm / Current version — 6.0.0-preview.8

Bulut hesabı oluşturma/giriş arayüzü ve otomatik profil eşitlemesi kaldırıldı. Profil ve ayarlar yerel kaydedilir. Önceki sürümlerin bulut kayıtları silinmedi. Çıkış düğmesi eşit çaplı elipsle çizilir ve ortalanır. Aydınlatma seçenekleri: **Tema**, **RGB · renk geçişi**, **Kapalı**. RGB hem çemberin hem ışığının rengini yumuşak biçimde değiştirir. Ayrı Rainbow ve sabit RGB renk kutusu kaldırıldı; eski Rainbow tercihi RGB'ye taşınır.

Cloud account creation/sign-in UI and automatic profile synchronization were removed. Profiles/settings stay local; old cloud records were not deleted. The centered close ring uses circular ellipse geometry. Lighting choices: **Theme**, **RGB · color cycle**, **Off**. RGB smoothly animates the ring and glow together. The separate Rainbow choice and static RGB hex field were removed; existing Rainbow preferences migrate to RGB.

Minecraft startup failure/nonzero exit restores the launcher and displays diagnostics. / Minecraft açılış hatası veya sıfırdan farklı çıkış kodu launcher'ı geri açar ve hata analizini gösterir. Real Vanilla/Forge gameplay results and previous remaining issues: [PC report](docs/PC-TESTS-PREVIEW-7.md).

Protected, SHA-256 locally test-signed portable package / Korumalı, SHA-256 yerel test imzalı paket: `artifacts/MistikLauncher-6.0.0-preview.8-win-x64.zip`. Local test signatures are not publicly trusted and cannot guarantee AV/SmartScreen acceptance. / Yerel test imzası genel yayıncı güveni veya antivirüs uyarısızlık garantisi sağlamaz.

**198 checks passed**, including color change over time and synchronized glow in both languages. / **198 kontrol geçti**; iki dilde zaman içinde renk değişimi ve eşzamanlı ışık doğrulandı. [Current window controls / Güncel pencere kontrolleri](docs/WINDOW-AND-TOOLBAR.md).

Optional signing / İsteğe bağlı imzalama: `./scripts/Build-Protected.ps1 -SigningThumbprint YOUR_CERTIFICATE_THUMBPRINT`. Use `-AllowLocalTestSignature` only for an explicitly untrusted local test certificate in your current-user personal store. Private keys are never copied into the package. / Yalnızca açıkça güvenilir olmayan yerel test sertifikası için `-AllowLocalTestSignature` kullanın. Özel anahtar pakete kopyalanmaz. The local test package is not timestamped / Yerel test paketi zaman damgalı değildir.

## Türkçe

Yeni ana panel oyuncu profili, sürüm, bellek ve hızlı işlemleri gösterir. Ayarlarda oyuncu adı, 1–32 GB bellek, skin sağlayıcısı, renk ve otomatik kapanma seçilir. Üst menüdeki dil listesi anında Türkçe / English geçişi sağlar. Arama kaldırıldı; gezinme üstte, oyuncu adı ve skin yüzü sağ üsttedir. RedX referansındaki kömür siyahı yüzeyler, 20 renk seçeneği, Segoe UI ve görünür klavye odağı kullanılır.

Ayarlar atomik kaydedilir; önceki değerler `.bak` dosyasından kurtarılır. Donanım/IP telemetrisi ve kimlik doğrulamasız Firebase uzaktan yönetimi kaldırıldı. Sabit yönetici şifreleri ve eski kontrolsüz EXE değiştirme devre dışı bırakıldı; resmî doğrulanmış paket güncellemeleri yeni sistemle uygulanır. Açılışta sessiz kurulum, diğer başlatıcı işlemlerini sonlandırma ve otomatik sistem müdahaleleri kaldırıldı. MQTT için TLS yapılandırıldı; başlangıçta otomatik bağlantı kaldırıldı. Topluluk aktarımının güvenlik/kullanılabilirlik incelemesi sürüyor.

### Çalıştırma

Windows x64 için `artifacts/MistikLauncher-6-preview-win-x64.zip` paketini ayrı bir klasöre çıkartıp `MistikLauncher.exe` dosyasını açın. Paket kendi .NET çalışma zamanını içerir. **Paketin tüm dosyalarını bir arada tutun.** Bu önizleme taşınabilirdir; kurulum, kaldırma ve sertifika güven deposuna ekleme işlemleri kapalıdır. Veriler `%APPDATA%\.mistik_ultra` içinde kalır. Denemeden önce oyun klasörünü ve ayarları yedekleyin.

1. Sağ üstte Türkçe veya English seçin.
2. Ayarlar sayfasında oyuncu adını ve belleği girip Değişiklikleri kaydet düğmesine basın.
3. Sürümler sayfasından oyun sürümünü indirin, alt çubukta seçin.
4. Uyumlu modları seçip Oyunu başlat düğmesini kullanın.

Vanilla/Forge dünya testi ve ayrı yerel sunucu doğrulandı. Her mod kombinasyonu, Microsoft/Ely.by girişi ve genel tüneller test edilmedi; Auto-MCS sunucu oluşturma sihirbazı hata verdi. Microsoft hesabı kimlik doğrulaması eklenmedi. Minecraft ve mod lisanslarına uyun.

### Derleme ve doğrulama

Windows ve .NET 8 SDK gerekir:

```powershell
dotnet build MistikLauncher/MistikLauncher.csproj -c Release
dotnet run --project Validation -c Release -- artifacts/screenshots-ordinary
./scripts/Build-Protected.ps1
```

Koruma betiği sabit sürümlü aracı yükler, taşınabilir sürümü derler, korumalı DLL ile doğrulamayı çalıştırır ve ZIP üretir. Özel eşleme ve PDB dosyaları pakete eklenmez. `checksums.json` bozulma tespiti içindir; dijital imza değildir.

## English

The redesigned home shows player, version, memory and quick actions. Settings validate player names and 1–32 GB RAM, and offer skin provider, accent and automatic closing. The top-right Turkish / English selector updates at runtime. Navigation is centered at the top, with player name and skin face at the right. Search has been removed. Charcoal surfaces, 20 accent palettes, Segoe UI typography and visible keyboard focus follow the supplied RedX references.

Configuration writes are atomic with recovery from the previous backup. Hardware/IP telemetry and unauthenticated Firebase administration were removed. Hardcoded administrator access and legacy arbitrary executable replacement are disabled; verified official package updates now use the new updater. Startup no longer silently installs, terminates other launcher processes or changes system preferences. MQTT uses TLS; automatic startup connection was removed. Community relay security/usability review remains outstanding.

### Run

Extract `artifacts/MistikLauncher-6-preview-win-x64.zip` to a dedicated Windows x64 folder and run `MistikLauncher.exe`. It includes its .NET runtime. **Keep all package files together.** Installation, uninstall execution and certificate trust changes are disabled in this portable preview. Data remains under `%APPDATA%\.mistik_ultra`. Back up settings and game files before trying it.

1. Select Türkçe or English at the top right.
2. Enter player name and RAM in Settings, then Save changes.
3. Download a version under Versions and select it in the bottom bar.
4. Choose compatible mods and select Launch game.

Vanilla/Forge gameplay and an isolated local server were verified. Every mod combination, Microsoft/Ely.by login and public tunnels remain untested; the third-party Auto-MCS server wizard failed. Microsoft account authentication was not added. Respect Minecraft and mod licenses.

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
| `MistikLauncher/MainWindow.xaml` | Top navigation, player profile, language, focus / Üst menü ve profil |
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

**6.0.0-preview.4:** Pencere stilleri artık önizlemeli kartlardan seçiliyor. Mod merkezi → Kurulu Modlar altında modları silmeden etkinleştirebilir/devre dışı bırakabilirsiniz; dosyalar korunur ve oyun yeniden açıldığında durum uygulanır. [Kullanım ve doğrulama](docs/MOD-TOGGLES.md).

**6.0.0-preview.4:** Choose window styles from preview cards. Installed mods can be enabled/disabled without deletion; files are preserved and the next game start applies the change. [Usage and validation](docs/MOD-TOGGLES.md).

**6.0.0-preview.5:** Gezinme üst çubuğa taşındı, yazılar ve ikonlar ortalandı. Oyuncu adı ve skin yüzü sağ üsttedir. Tüm pencere stilleri tek özel başlık çubuğu kullanır; büyütülmüş pencerede çerçeve payı ayrılır. Firebase hesapları profil, skin ve ayarları eşitler; oturum Windows DPAPI ile korunur. [Bulut profili kullanımı](docs/CLOUD-PROFILES.md).

**6.0.0-preview.5:** Navigation is centered at the top; player name and skin face appear at the right. All window styles use one custom caption with a maximized frame inset. Firebase accounts sync profiles, skins and settings; Windows DPAPI protects local sessions. [Cloud profile guide](docs/CLOUD-PROFILES.md).

Protected portable validation: **136 checks** including package hashes and updater process handoff. Protected live Firebase validation: **143 checks** including authenticated upload/restore, cross-user denial and invalid update rejection. Both runs cover Turkish and English UI. Actual Minecraft/Forge gameplay and multiple-monitor taskbar behavior still require manual verification.

**6.0.0-preview.6:** Mod sürüm taşımasındaki dosya kaybı ve çakışmalar, NeoForge algısı, yanlış otomatik askılama, bulut skin/oturum doğrulaması ve eski sürüm etiketi düzeltildi. Mod indirmeleri SHA-512 ile doğrulanır, atomik yüklenir ve önceki dosyalar yedeklenir. Kapalı modun durumu korunur. Oturumlar DPAPI ile şifreli, dağıtım DLL'si ad/metin gizleme ile korumalıdır. [Hata ve koruma raporu](docs/AUDIT-PREVIEW-6.md).

**6.0.0-preview.6:** Fixes destructive mod transfers, collisions, NeoForge classification, false suspension, cloud skin/session validation and stale version labels. Mod downloads require SHA-512 verification and use atomic installation with backups, preserving disabled state. DPAPI encrypts sessions; the distributed DLL uses name/string obfuscation. [Audit and protection report](docs/AUDIT-PREVIEW-6.md).

Latest protected package: **159 checks passed**. Protected live Firebase + official Modrinth installation: **167 checks passed**, without executing the downloaded mod. The broader stable-release limitations above still apply.

Üst kısayol çubuğunu ve 16 pencere düğmesi görünümünü Ayarlardan kişiselleştirebilirsiniz. Temalar üst çubuğa, yan menüye ve başlatma alanına uygulanır. [Pencere ve üst çubuk rehberi](docs/WINDOW-AND-TOOLBAR.md).

Customize the top shortcut bar and 16 window control appearances in Settings. Themes coordinate toolbar, sidebar and launch actions. [Window and toolbar guide](docs/WINDOW-AND-TOOLBAR.md).

Forge profillerinin görünmemesi ve seçim kaydının başarısız olması düzeltildi. Kurulum artık resmî Forge yükleyicisini kullanır. Ayarlardan 20 uyumlu renk temasını anında seçebilirsiniz. Ayrıntılar, test kapsamı ve Java gereksinimleri: [Forge ve tema notları](docs/FORGE-AND-THEMES.md).

Official inherited Forge profiles are now selectable and selection persists. Installation uses the official Forge installer. Settings offers 20 coordinated, immediately applied color themes. See [Forge and theme notes](docs/FORGE-AND-THEMES.md) for validation limits and Java requirements.
