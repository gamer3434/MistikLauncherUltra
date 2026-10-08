# Mistik Launcher 6.2.2

## Türkçe

- Online kurulumda metadata ve paket gövdesi indirmeleri zaman aşımına tabidir. Eksik, iptal edilmiş veya doğrulaması başarısız indirme mevcut sağlıklı paketi değiştirmez.
- Mod havuzu geçişinde ayar kaydı başarısız olursa taşınan dosyalar geri alınır. Aynı havuza mod taşıma işlemi dosyalara dokunmadan sonlanır.
- Ayar yedeği hazırlama hataları ana dosya değiştirilmeden bildirilir; kayıt sonrası bildirim hatası başarılı kaydı başarısız gibi göstermez.
- Bozuk avatar önbelleği yeniden indirilir; doğrulanmamış resimler önbelleğe yazılmaz. Skin etkinleştirme ve sıfırlamada options.txt kayıt hataları kullanıcıya bildirilir ve mevcut paket korunur.
- Sunucu yöneticisi güncelleme kaydı yazılamazsa önceki EXE geri yüklenir; ilk kurulumda yarım güncelleme bırakılmaz.
- Başarılı güncelleme veya tamamlanan geri alma sonrası geçici yedekler temizlenir. Geri alma başarısız olduğunda kurtarma dosyaları korunur.

## English

- Online setup bounds metadata and payload body downloads with a timeout. Incomplete, cancelled or invalid downloads preserve the existing verified package.
- Mod pool changes roll back if saving settings fails. Migration to the same pool leaves files untouched.
- Backup preparation fails before committing primary settings. Post-save notification errors cannot turn a committed save into an apparent failure.
- Invalid avatar caches are refreshed and images are decoded before caching. Skin activation/reset reports options.txt write failures and preserves the existing pack.
- Server manager updates restore the previous executable when the version receipt cannot be saved; failed first installs leave no partial executable.
- Successful updates and completed rollbacks remove temporary backups. Failed rollbacks retain recovery files.

## Downloads / İndirmeler

- `MistikSetup-Online-6.2.2.exe`
- `MistikSetup-Offline-6.2.2.exe`
- `MistikLauncher-6.2.2-win-x64.zip`
- `MistikRepair-6.2.2.exe`
- `SHA256SUMS.txt`

The regular build is unobfuscated and includes .NET. Close the launcher before offline repair; player settings, mods and worlds are preserved. / Normal derleme kod gizleme içermez; .NET pakete dahildir. Offline onarım öncesi launcher'ı kapatın; oyuncu ayarları, modlar ve dünyalar korunur.

Packages are unsigned. Regression checks use isolated profiles and files; they do not represent a full Minecraft play session. / Paketler imzasızdır. Regresyon kontrolleri ayrı test profilleri ve dosyalarla çalışır; tam Minecraft oyun oturumu testi değildir.