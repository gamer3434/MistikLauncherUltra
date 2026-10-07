# Mistik Launcher Ultra 6.1.8

## Türkçe

- Sürüm listesi yenileme istekleri sırayla işlenir; bekleyen sayfalar güncel listeyi gösterir ve bağlantı hatası mevcut listeyi silmez.
- Yenilenen kartlar Türkçe/English seçimini korur. Kurulu Fabric profillerinin yinelenen kartları kaldırıldı.
- Geçersiz skin oyuncu adları kaydedilmez. Bozuk skin dosyaları mevcut kaynak paketi değiştirilmeden reddedilir; yerel skin yeniden seçilebilir.
- Güncelleme kurulumu hataları görünür durum ve yeniden deneme ile sonuçlanır.
- **MistikRepair-6.1.8.exe** ayrı, internet gerektirmeyen onarıcıdır. Launcher'ı kapatın, onarıcıyı açın, kurulum klasörünü kontrol edip **Onar** seçin. Eksik/bozuk program dosyaları SHA-256 doğrulamasıyla yenilenir; ayarlar, modlar, dünyalar ve diğer kişisel dosyalar korunur. Daha yeni kurulumlar eski sürüme indirilmez. Kurulum kaydı ve Windows uygulama kaydı birlikte kaybolmuşsa yeni bir klasöre normal kurulum gerekir.

## English

- Catalog refresh requests are serialized. Waiting pages display the refreshed catalog and offline failures retain the working list.
- Rebuilt cards retain the selected language. Installed Fabric profiles no longer have duplicate catalog cards.
- Invalid skin usernames are rejected before saving. Corrupt skins are rejected before changing the working resource pack; the current local skin can be selected again.
- Update handoff failures show an error and allow retry.
- **MistikRepair-6.1.8.exe** is a standalone offline repair tool. Close the launcher, open the tool, confirm the installation folder and select **Repair**. Missing/damaged application files are restored and SHA-256 verified; settings, mods, worlds and unrelated files are preserved. Newer installations are never downgraded. If both the installation record and Windows application registration are lost, install normally into a new folder.

## Downloads / İndirmeler

- `MistikSetup-Online-6.1.8.exe`
- `MistikSetup-Offline-6.1.8.exe`
- `MistikLauncher-6.1.8-win-x64.zip`
- `MistikRepair-6.1.8.exe`
- `SHA256SUMS.txt`

Packages are unsigned / Paketler imzasızdır.
## Depo temizliği / Repository cleanup

Kullanılmayan yönetici paneli ve boş telemetri çağrıları kaldırıldı. Firebase proje eşlemesi yerel dosyaya taşındı; yinelenen yapılandırma birleştirildi ve kapalı veritabanı kuralları korundu. Derleme çıktıları depodan çıkarıldı. Gizli bilgi örüntüleri ve özel dosyalar için CI kontrolü eklendi. / Removed retired admin code and empty telemetry calls, kept deployment mappings local, consolidated Firebase configuration, retained denied database access and added a credential-pattern check.
