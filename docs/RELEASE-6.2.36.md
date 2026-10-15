## Türkçe

- Mod ayrıntıları ve galeri resimleri yüklenemezse doğrudan tekrar denenebilir. Galeriden çıkınca bekleyen istekler iptal edilir; eski yanıtlar yeni görüntünün üzerine yazamaz.
- Kurulu modun farklı sürümünü yükleme düğmesi çakışmayı açıklayarak devre dışı kalır. Uzun ayar hataları Kaydet düğmesini ve içerik alanını kaplamaz; tam metin erişilebilir biçimde korunur.
- Mod açma/kapatma, kaldırma ve temizleme dosya işlemleri arka planda çalışır. Toplu taşıma doğru kaynak profili eşitler; hedef listesini yenilemek ikinci bir taşıma başlatamaz. CustomSkinLoader doğru profilde doğrulanmış birincil dosyayı kurar.
- Profil tercihi kaydedilemezse önceki seçim korunur; oyun başlatma ve sürüm düğmeleri yanlış başarı bildirmez. Sürüm düşürme, liste yüklenirken veya sürüm seçilmemişken başlatılamaz.
- Güncelleme etiketi, dosya adı ve resmî adres birlikte doğrulanır. Bozuk yayın kayıtları sağlam önbelleği ezmez. Paketlerde ve mod hedeflerinde dış klasör bağlantıları reddedilir. Çelişkili etkin/devre dışı mod kopyaları yedek oluştururken ve geri yüklerken mevcut dosyaları değiştirmeden reddedilir.
- Hızlı aç/kapat ve kapanış sırasında istatistik paylaşımını durdurma isteğinin bekleyen açma işleminin ardından kaybolması düzeltildi. Admin panelinde eski oturum verilerinin geri görünmesi ve otomatik yenilemenin filtre seçimini bozması engellendi.

776 normal kontrol, ayrıca bir canlı Modrinth indirme/içerik doğrulaması, 37 web testi ve 61 tarayıcı kontrolü geçti. Gerçek kullanıcı modları, hesapları ve paylaşım tercihleri değiştirilmedi. Oyun içi oynanış ve tüm donanım kombinasyonları denenmedi.

## English

- Failed mod details and gallery images can be retried directly. Leaving the gallery cancels pending requests and stale responses cannot overwrite reopened content.
- Conflicting version installs explain why the installed mod must be removed first. Long settings errors preserve the Save action and content viewport, with the full message available accessibly.
- Mod toggles, removals and cleanup perform file work in the background. Migration synchronizes the correct source profile and target refresh cannot start a duplicate migration. CustomSkinLoader installs the verified primary file into the selected profile.
- Failed profile saves preserve the previous selection without launching the game or reporting false success. Downgrade actions require a selected release and pause during loading.
- Update tags, filenames and official addresses must match. Invalid release responses preserve healthy cache metadata. Linked directories are rejected in payloads and mod destinations. Contradictory enabled/disabled copies are rejected during backup creation and restoration without changing installed files or a previous archive.
- Opt-out and shutdown now override earlier queued telemetry enable requests. Admin authentication failures cannot revive old metrics, and automatic refresh preserves stable filter selections.

776 ordinary checks, one additional live Modrinth download/content verification, 37 web tests and 61 browser checks passed. Real user mods, accounts and sharing preferences were not changed. Gameplay and every hardware combination were not tested.
