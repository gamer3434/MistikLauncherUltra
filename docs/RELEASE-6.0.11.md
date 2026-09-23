# Mistik Launcher 6.0.11 — Security cleanup and smoother menus / Güvenlik temizliği ve akıcı menüler

## Changes / Değişiklikler

- Removed the obsolete MQTT/cloud update channel, its unchecked EXE replacement path, and the stale `update.json` feed pointing to v5.5.2. Supported launcher updates still use GitHub release assets with digest and manifest verification. / Eski MQTT/bulut güncellemesi, doğrulanmamış EXE değiştirme yolu ve v5.5.2'yi gösteren `update.json` kaldırıldı. Desteklenen güncelleme, GitHub yayın dosyalarını özet ve manifest doğrulamasıyla kullanır.
- Removed unused cloud-account client code and its embedded Firebase client key from current source. Skin PNG validation remains local. Removed the legacy source backup containing a hardcoded administrator password; local secret/player-data files are ignored by Git. / Kullanılmayan bulut hesap kodu ve gömülü Firebase istemci anahtarı güncel kaynaktan çıkarıldı. Skin PNG doğrulaması yerel olarak sürer. Sabit yönetici parolası içeren eski kaynak yedeği kaldırıldı; yerel gizli ve oyuncu dosyaları Git tarafından yok sayılır.
- Cached menus keep their visual tree until the language changes; rapid navigation keeps the latest selection. The Optimization shortcut remains available when optional shortcuts are hidden. / Önbellekteki menüler dil değişene kadar yeniden çizilmez; hızlı geçişte son seçim korunur. İsteğe bağlı kısayollar kapalıyken Optimizasyon erişilebilir kalır.
- Graphics optimization respects its switch, preserves a chosen FPS limit on launch, and keeps a backup of original Minecraft options. / Grafik optimizasyonu anahtarına uyar, seçilen FPS sınırını oyun açılırken korur ve özgün Minecraft ayarlarını yedekler.
- The Performance page now has Turkish and English labels and feedback. Old log cleanup runs off the UI thread and retains `latest.log` and crash reports. / Performans sayfasının başlıkları ve geri bildirimleri Türkçe/İngilizcedir. Eski günlük temizliği arayüzü kilitlemez; `latest.log` ve çökme raporları korunur.

## Security boundary / Güvenlik sınırı

Previous public Git commits and old release binaries still contain historical administrator passwords and the Firebase client identifier. This patch does not rewrite release history or revoke Google Cloud keys; any reused password must be changed, and the Firebase key should be restricted or rotated in Google Cloud if appropriate. Anonymous reads of the Firebase database root and profile collection returned HTTP 401 during this release audit. / Önceki açık Git commitleri ve eski yayın dosyaları tarihsel yönetici parolalarını ve Firebase istemci tanımlayıcısını hâlâ içerir. Bu yama yayın geçmişini yeniden yazmaz veya Google Cloud anahtarını iptal etmez; tekrar kullanılan parolalar değiştirilmeli, Firebase anahtarı gerekirse Google Cloud'da kısıtlanmalı ya da yenilenmelidir. Bu denetimde Firebase veritabanı köküne ve profil koleksiyonuna anonim okuma istekleri HTTP 401 döndürdü.

## Validation / Doğrulama

Normal validation: 273 checks passed, including both languages, menu navigation, graphics settings, skin validation and security recovery. / Normal doğrulama: iki dil, menü geçişi, grafik ayarları, skin doğrulaması ve güvenlik kurtarması dahil 273 kontrol geçti.

Protected package: 275 checks passed. Online and offline installers built; the offline embedded package passed verification. / Korumalı paket: 275 kontrol geçti. Online ve yerel kurucular derlendi; yerel kurucunun gömülü paketi doğrulandı.

SignPath Foundation review is pending; this release is unsigned. / SignPath Foundation incelemesi sürüyor; bu sürüm imzasızdır.
