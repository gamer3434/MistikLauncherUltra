# Mistik Launcher 6.2.1

## Türkçe

- Aynı haritayı yeniden kurmak mevcut dünyayı silmez; ayrı bir kopya oluşturur. Başarısız arşiv açma işlemi eski dünyaya dokunmaz.
- Modlar tam Minecraft sürümü ve yükleyicisiyle eşleştirilir. Forge/NeoForge ve Fabric/Quilt ayrılır. Gerekli bağımlılıkların sabit sürüm kimlikleri korunur; eksik, devre dışı veya çakışan bağımlılıklar başarı olarak gösterilmez.
- Sıraya alınan modlar doğru sürüm/yükleyici havuzuna gider. Özel ve yıl numaralı profillerin kimliği profil mirasından okunur; çoklu yükleyici JAR'ları yanlışlıkla askıya alınmaz.
- Eski ve yeni Windows native arşivleri doğrulanıp seçili oyun profilinde açılır. Eksik/bozuk DLL'ler yerel sağlıklı arşivden de onarılır. Takılan indirmeler zaman aşımıyla sonlanır ve önceki dosyaları korur.
- Düşük bellekli bilgisayarlarda oyun belleği fiziksel RAM'i aşmayacak şekilde sınırlandırılır.
- Bağlantı hatası veya bozuk skin mevcut paketi korur. Skin sıfırlama, yerel seçim ve profil değişimi sırasında eski indirmeler yeni seçimi değiştiremez. Paket değişimi geçici klasör ve geri alma ile yapılır.
- Güncelleme yardımcısı doğrulanmış yeni paketten çalıştırılır. Bozuk paket sonrası yeniden indirme mümkündür; başarısız geçici paketler temizlenir. Ayarlar/güncelleme sayfasına dönüldüğünde son durum yenilenir.
- Tünel kapatma yalnızca launcher'ın başlattığı süreci durdurur. Başka SSH/playit/bore süreçleri korunur; eski asenkron istekler durdurulan tüneli yeniden açamaz. UPnP temizliği sahip olunan eşlemelerle sınırlıdır.
- Ana ayar dosyası kayıp veya `null` olduğunda sağlıklı şifreli yedek kullanılır. Bozuk kurulum kaydı kontrollü biçimde reddedilir; açıkça iptal edilen sürüm isteği önbellek başarısına dönüşmez.
- Ely.by sayfası izin/onay düğmelerine otomatik tıklamaz.

## English

- Reinstalling a map creates a separate world and preserves existing progress, including failed extraction.
- Mods require exact Minecraft and loader compatibility. Pinned required dependency versions are retained; missing, disabled or conflicting dependencies fail visibly. Queued mods use the same profile pool as launcher synchronization.
- Profile inheritance identifies custom and year-based versions. Quilt and multi-loader metadata are handled without suspending compatible files.
- Legacy and modern Windows native archives are verified and extracted for the selected profile. Missing or damaged DLLs can be repaired from healthy local archives. Stalled downloads time out while retaining existing files.
- Low-memory launch limits respect physical memory. Skin replacements preserve the previous pack on failure; stale downloads cannot override a reset, local selection or profile change.
- Update handoff uses the newly verified helper, permits a fresh retry after damaged staging and cleans failed temporary packages. Cached settings/update pages refresh their current status on return.
- Tunnel shutdown targets the launcher-owned process only. Unrelated SSH/playit/bore sessions survive; stale starts and callbacks cannot revive stopped tunnels. UPnP cleanup targets owned mappings.
- Missing/null settings restore the encrypted backup. Invalid installer ownership records are rejected and explicit metadata cancellation is honored. Ely.by consent controls remain under user control.

## Downloads / İndirmeler

- `MistikSetup-Online-6.2.1.exe`
- `MistikSetup-Offline-6.2.1.exe`
- `MistikLauncher-6.2.1-win-x64.zip`
- `MistikRepair-6.2.1.exe`
- `SHA256SUMS.txt`

The regular build is unobfuscated and includes .NET. Close the launcher before offline repair; player settings, mods and worlds are preserved. / Normal derleme kod gizleme içermez; .NET pakete dahildir. Offline onarım öncesi launcher'ı kapatın; oyuncu ayarları, modlar ve dünyalar korunur.

Packages are unsigned. Runtime regression checks use isolated test profiles and archives; they do not represent a full Minecraft play session. / Paketler imzasızdır. Oyun dosyası regresyon kontrolleri ayrı test profilleri ve arşivlerle çalışır; tam Minecraft oyun oturumu testi değildir.
