# Mistik Launcher 6.2.5

## Türkçe

- Ana panelin karşılama ve profil özetleri tek kompakt kartta toplandı. Hazırlık durumu ve hızlı işlemler küçük pencerede görünür; ayrıntılar tam genişlikte okunur. Ağır görsel efekt eklenmedi.
- Mod merkezine Modrinth yanında CurseForge kaynağı eklendi. CurseForge araması seçili Minecraft sürümünü ve mod yükleyicisini kullanır. Dosyalar ve zorunlu bağımlılıklar uyumluluk, SHA-1 ve JAR yapısıyla doğrulanmadan kurulum başlamaz. İndirme izni kapalı projeler için resmî site açılabilir.
- CurseForge'un resmî API'si kendi API anahtarınızı gerektirir. Anahtar Ayarlar → Entegrasyonlar'daki maskeli alandan girilir ve şifreli ayarlarda saklanır. Anahtar indirme CDN'ine gönderilmez. Anahtarsız kullanımda resmî site araması sunulur.
- Kurulu mod listesi 40'ar genişler; ilk görünümde tüm modların kartları ve dosya boyutları oluşturulmaz. Yeni arama önceki isteği iptal eder; geciken sonuç ve hata yeni ekranı değiştirmez.
- Kaynak seçimi ve arama sayfanın üstündedir. CurseForge'un bildirdiği eski ana mod veya bağımlılık dosyası kuruluysa yeni sürüm eklenmeden önce çakışma bildirilir.
- Java araması, mod hazırlığı, dosya doğrulaması ve seçili oyun optimizasyonu arka planda çalışır. GPU tercihi yalnız başlatılan işlemin dosyasını değiştirir; tüm Java kurulumlarını tarayan ve başka uygulamaları değiştiren işlemler kaldırıldı.

## English

- A compact home layout combines welcome and profile summaries. Readiness and quick actions remain visible at the minimum window size, with full-width details and no additional heavy effects.
- The mod center supports CurseForge alongside Modrinth. CurseForge searches use the selected Minecraft version and loader. Compatible files and required dependencies validate SHA-1 and JAR structure before installation; projects that disable third-party downloads can be opened on the official website.
- The official CurseForge API requires your own API key. Enter it in the masked Settings → Integrations field; existing encrypted settings protect it. It is never sent to the download CDN. Users without a key can open official website searches.
- Installed mods render in batches of forty. Superseded searches are cancelled and late responses or errors cannot overwrite the latest results.
- Source selection and search appear first. Existing older root-mod or dependency filenames reported by CurseForge are rejected before any new bundle files are installed.
- Java discovery, mod preparation, runtime verification and game optimization run in the background. GPU preferences target the selected process executable; broad Java directory scans and unrelated application changes were removed.

## Downloads / İndirmeler

- `MistikSetup-Online-6.2.5.exe`
- `MistikSetup-Offline-6.2.5.exe`
- `MistikLauncher-6.2.5-win-x64.zip`
- `MistikRepair-6.2.5.exe`
- `SHA256SUMS.txt`

The regular build is unobfuscated and includes .NET. Close the launcher before offline repair; player settings, mods and worlds are preserved. / Normal derleme kod gizleme içermez; .NET pakete dahildir. Offline onarım öncesi launcher'ı kapatın; oyuncu ayarları, modlar ve dünyalar korunur.

CurseForge uses the [official API](https://docs.curseforge.com/rest-api/). Tests use isolated files and fake API responses; no live CurseForge key was provided. A full Minecraft play session and hardware FPS benchmark were not performed. Packages are unsigned. / CurseForge kontrolleri ayrı dosyalar ve sahte API yanıtlarıyla çalışır; canlı API anahtarı sağlanmadı. Tam Minecraft oyun oturumu ve donanım FPS ölçümü yapılmadı. Paketler imzasızdır.
