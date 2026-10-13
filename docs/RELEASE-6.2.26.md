## Türkçe

- Uzun mod başlıklarında, 800×500 ve 768×540 pencere boyutlarında içerik alanı ve kurulum düğmeleri kullanılabilir kalır.
- Profil seçimi değişince uyumlu sürümler mevcut metadata üzerinden yenilenir; açıklama/galeri veya seçili sekme kaybolmaz. Kurulum, tıklama anındaki profile bağlanır ve devam eden indirmelerde profil değişikliği güvenli şekilde reddedilir.
- Harita ZIP indirmesi 256 MiB, tek açılmış dosya 256 MiB ve toplam açılma 1 GiB ile sınırlıdır. Geçersiz yollar, yinelenen girdiler ve aşırı dosya sayısı kurulumdan önce reddedilir.
- Mod silme ve toplu temizleme dosyaları kalıcı olarak silmek yerine yedeğe taşır. Eski profilin ekranda kalan düğmesi yeni profilin modunu değiştiremez; başarısız toplu işlem geri alınır.
- Güncelleme geri alınırken bir dosyada hata olsa bile diğer dosyalar kurtarılır. Eksik kurtarmada özgün hata ve yedek konumu bildirilir; kurtarma dosyaları korunur.

Resmî Java 21 ve 25 paketleri gerçekten indirildi, hash ve Windows runtime dosyaları doğrulandı, Java sürüm komutları çalıştırıldı. Resmî Modrinth metadata/CDN/hash indirme yolu ayrıca gerçek hizmetle denendi. Minecraft oyun içi oynanış ve farklı fiziksel DPI ekranları ayrıca denenmedi.

## English

- Long mod metadata remains usable in 800×500 and 768×540 windows, preserving content space and installation controls.
- Selecting a profile refreshes compatible versions from existing metadata without losing descriptions, gallery or selected tab. Installation binds to the click-time profile and retains change-during-download protection.
- Map downloads are limited to 256 MiB, extracted files to 256 MiB each, and total expansion to 1 GiB. Invalid paths, duplicates and excessive entry counts are rejected before installation.
- Mod removal and cleanup preserve backups rather than permanently deleting files. Stale profile controls cannot modify the newly selected profile; failed bulk changes roll back.
- Update rollback attempts remaining restorations even after a file fails. Incomplete recovery reports the original error and backup location and preserves recovery files.

Official Java 21 and 25 were downloaded, checksums and Windows runtime files verified, and Java version commands executed. Official Modrinth metadata/CDN/hash installation was also exercised live. Minecraft gameplay and physical mixed-DPI displays were not tested separately.
