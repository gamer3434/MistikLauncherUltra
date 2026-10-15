## Türkçe

- Ayarlar’a “Yöneticinin uyumlu modlarını ve bağımlılıklarını tekrar sormadan otomatik kurmasına izin ver” seçeneği eklendi. Kullanıcı bu izni bir kez açarsa sonraki uygun isteklerde onay penceresi çıkmaz; kurulum durum çubuğunda görünür.
- Yeni izin varsayılan kapalıdır. Eski destek paylaşımı veya öneri izni otomatik kuruluma dönüştürülmez; yönetici bunu başka kullanıcılar adına açamaz.
- Destek paylaşımı veya öneriler kapatıldığında otomatik kurulum izni de kaldırılır. Oyun durumu, seçili profil, iptal, Modrinth kimliği ve dosya doğrulaması her iki modda da korunur.
- Admin paneli ve gizlilik açıklamaları, alıcının seçtiği otomatik veya elle onay akışını doğru anlatır.

817 normal ve 829 paket kontrolü ile 46 web testi geçti. Yeni kontroller, eski ayarların taşınmasını, iznin kaydını/iptalini, onay penceresinin atlanmasını ve profil/oyun/izin engellerini sahte kurulum işlemleriyle doğruladı. Gerçek kullanıcıya uzaktan mod kurulmadı.

## English

- Settings now offers “Allow the administrator to install compatible mods and dependencies automatically without asking again.” Once a recipient explicitly enables it, later eligible requests install without a dialog; progress remains visible in the status bar.
- The new permission defaults to off. Existing support or suggestion consent is never upgraded to automatic installation; administrators cannot enable it for other users.
- Disabling support or suggestions also revokes the automatic preference. Game state, selected profile, cancellation, Modrinth identity and verified downloads remain enforced in both modes.
- Admin and privacy copy now accurately describe the recipient's automatic or manual approval mode.

817 ordinary and 829 packaged checks plus 46 web tests passed. New checks exercised upgrade defaults, preference persistence/revocation, bypassing the confirmation dialog and profile/game/consent guards using fake install actions. No remote mod was installed for a real user.
