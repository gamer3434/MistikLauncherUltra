# Mistik Launcher Ultra 6.2.8

- Ayrı Güncellemeler menüsü kaldırıldı. Güncelleme durumu, otomatik güncelleme ve güncelleme kontrolü Ayarlar bölümünde kalır.
- Eski güncelleme bağlantıları mevcut Ayarlar sayfasını açar.
- API anahtarı yazılırken otomatik launcher güncellemesi ertelenir.
- Yeni regresyon kontrolleri, eski sürümden yüklenen şifreli oyuncu adı, bellek, dil, skin sağlayıcısı ve API anahtarının korunmasını doğrular. Güncelleme şifreli ayarlar dosyasını değiştirmez.

- CurseForge geçersiz veya reddedilmiş anahtarda ayar bağlantısı gösterir. Edge CDN indirmelerine yalnız doğrulanmış resmî edge alan adında gerekli kimlik başlığı eklenir; yönlendirmeler anahtarı diğer medya sunucularına taşımaz. [Resmî CDN değişikliği](https://blog.curseforge.com/introducing-api-key-authentication-for-curseforge-file-downloads/).

## English

The separate Updates menu was removed; update controls remain in Settings. Legacy update navigation opens the same settings page. Automatic launcher updates are deferred while editing the masked API key. Regression checks cover encrypted player profile and key persistence across version migration and application file replacement. Rejected keys show actionable setup help. Official edge CDN requests now include the required API header, with strict host validation and no credential forwarding to other media hosts.

## Downloads / İndirmeler

- `MistikSetup-Online-6.2.8.exe`
- `MistikSetup-Offline-6.2.8.exe`
- `MistikLauncher-6.2.8-win-x64.zip`
- `MistikRepair-6.2.8.exe`
- `SHA256SUMS.txt`

Kurucuda mevcut kurulumu onarma seçeneği vardır. Launcher'ı önce kapatın; ayarlar, modlar ve dünyalar korunur. API anahtarları kaynak koduna veya dağıtılan pakete eklenmez; Windows hesabına bağlı şifreli yerel ayarlarda saklanır. Başka Windows hesabına geçerken anahtar yeniden girilmelidir.

The installers include a repair option. Close the launcher first; settings, mods and worlds are kept. API keys stay in local Windows user-bound encrypted settings and are not included in source or distributed packages. A different Windows account needs its own key setup.

.NET included. Packages unsigned. Full Minecraft sessions, hardware FPS and live online installer downloads were not tested. / .NET dahildir. Paketler imzasızdır. Tam Minecraft oturumu, donanım FPS ölçümü ve canlı online kurulum indirmesi denenmedi.