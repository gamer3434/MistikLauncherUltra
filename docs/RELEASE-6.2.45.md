## Türkçe

- Mod güncelleme kontrolü launcher boşta arka planda yapılır. Başlat düğmesi ağ yanıtını veya indirmeyi beklemez; devam eden kontrol iptal edilir.
- Yalnızca önceden hazırlanmış, seçili profile uygun güncellemeler oyun başlamadan uygulanır. Güncelleme hazır değilse mevcut modlarla açılır; kontrol sonraki boşta dönemde yeniden yapılır.
- Aynı profil için tamamlanmış kontroller 30 dakika içinde tekrarlanmaz. Profil değişiminde eski hazırlık bırakılır; launcher kapanınca bekleyen işler iptal edilir.
- Uygun güncelleme yoksa tüm modları geçici klasöre kopyalama ve kopyaları yeniden hash'leme kaldırıldı. SHA512 indirme kontrolü, güncelleme öncesi mevcut dosya doğrulaması, bağımlılık kontrolleri ve geri alınabilir yedekler korunur.
- Minecraft, yükleyici, mod seçimi ve grafik ayarları değiştirilmez. Yeni bir oyun motoruna geçilmez.

## English

- Mod updates are checked in the background while the launcher is idle. Play cancels unfinished checks instead of waiting for network requests or downloads.
- Only already prepared updates matching the selected profile are applied before starting the game. Otherwise the existing mods launch; checks can run again when idle.
- Completed checks for the same profile are throttled for 30 minutes. Changing profiles discards stale preparation, and closing the launcher cancels pending checks.
- Checks without compatible updates no longer copy every installed mod into staging or rehash those copies. SHA512 download verification, pre-commit installed-file validation, dependency checks and recoverable backups remain enforced.
- Minecraft, loader, selected mods and graphics options retain their existing behavior. This does not switch game engines.

Tests cover immediate launch with an unfinished check, prepared update ownership, profile changes, opt-out, running-game guards and zero staged mod copies when no compatible update exists. End-to-end gameplay and a real-PC startup-time benchmark have not been measured.
