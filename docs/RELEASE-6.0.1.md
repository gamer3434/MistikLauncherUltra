# Mistik Launcher 6.0.1

## Gerekçe / Reasoning
Yayımlanan preview etiketleri güncelleyici tarafından reddediliyor, güncellemeler kaldırma kayıtlarını yenilemiyor ve kurulumun son aşamasındaki hatalar yeniden denemeyi engelliyordu.
Published preview tags were rejected, updates left uninstall records stale, and final installation errors prevented retry.

## Düzeltmeler / Implementation
- Sayısal preview karşılaştırması; aynı sürümü tekrar indirme ve kararlı sürümden preview'a düşme engellenir. Numeric preview comparison prevents repeat updates and stable-to-preview downgrades.
- Güncelleme manifesti, dosya sahipliği ve Windows sürüm kaydı yenilenir. Sonradan eklenen dosyalar da kaldırılır; kullanıcı dosyaları korunur. Updates refresh the manifest, ownership and Windows version; uninstall includes added files and preserves user data.
- Kurulumun son aşamasındaki hatalar hedef dosyalarını geri alır; tekrar kurulum mümkündür. Finalization errors roll back installed files, permitting retry.
- Paketlerin kurulum sahipliği kaydını değiştirmesi engellenir. Packages cannot supply reserved ownership metadata.
- Kod korumasıyla uyumsuz anonim JSON kaydı, açık kayıt türüyle değiştirildi. An explicit record fixes JSON serialization after obfuscation.

## Testler ve sınırlar / Validation and limits
Korumalı pakette 218 kontrol geçti; preview karşılaştırması, güncelleme sahipliği, kullanıcı dosyalarını koruma ve başarısız kurulum sonrası tekrar denemeyi kapsar. Online ve yerel kurulum dosyaları ile SHA-256 özetleri sağlanır.
218 protected-package checks passed, covering preview comparison, ownership updates, user-data preservation and retry after installation failure. Online/offline installers and SHA-256 checksums are provided.

Bu yayın imzasızdır; SHA-256 yayıncı kimliğini veya SmartScreen güvenini sağlamaz. Ücretli sertifika alınmadı, güven deposu değiştirilmedi. Eski sayfaların tüm çevirileri ve üçüncü taraf Auto-MCS sihirbazının kapsamlı doğrulaması hâlâ eksiktir.
This release is unsigned: SHA-256 does not establish publisher identity or SmartScreen trust. No paid certificate was purchased or trust store changed. Complete legacy translations and comprehensive third-party Auto-MCS wizard verification remain outstanding.

Kurulum dili sağ üstten, launcher dili Ayarlar'dan seçilir. Preview.9 kullanıcıları için ilk geçişte launcher'ı kaldırıp 6.0.1 kurulumunu çalıştırmak önerilir: eski güncelleme yardımcısı ilk geçişte kendi eski kayıt davranışını kullanır; yeni kayıt düzeltmesi 6.0.1 yardımcısıyla yapılan sonraki güncellemelerde geçerlidir. Oyun verileri `%APPDATA%\.mistik_ultra` içinde korunur.
Choose setup language at upper right and launcher language in Settings. For the initial move from Preview.9, uninstall the launcher then run the 6.0.1 setup: the old update helper still uses its old metadata behavior for that first transition; metadata fixes apply to later updates performed by the 6.0.1 helper. Game data in `%APPDATA%\.mistik_ultra` is preserved.
