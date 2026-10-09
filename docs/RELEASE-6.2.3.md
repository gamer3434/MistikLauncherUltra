# Mistik Launcher 6.2.3

## Türkçe

- Başarısız ayar kaydı aktif oyuncu adı, RAM, giriş yöntemi, kapanma ve dil tercihlerini değiştirmez. Taslak korunur ve hata gösterilir. Otomatik güncelleme tercihi sayfalar arasında yenilenir; başarısız kayıt seçimi geri alır. Ayar kaydı ve mod havuzu geçişi aynı kilidi kullanır.
- Mod bağımlılık zinciri indirme başladığı profile bağlı kalır. İndirme sırasında başka profil seçilirse o profilin modlarına dosya yazılmaz. Aktif mod yazma ve havuz geçişleri birlikte korunur.
- .minecraft mod aktarımı devre dışı bırakılmış dosyaları yeniden etkinleştirmez; mevcut modları korur. Kopyalar geçici dosyadan tamamlanır ve tek profil havuzuna aktarılır.
- Oyun başlatma komutu Windows işletim sistemi, sürüm ve mimari kurallarını doğrulamayla aynı şekilde uygular. Native arşivler sıradan kütüphane gibi eklenmez; döngülü profil mirası ve dizin dışına çıkan kütüphane yolları kontrollü biçimde reddedilir.
- Kaldırıcı salt okunur program dosyalarını herhangi bir dosyayı silmeden bildirir. Eksik sahiplik kaydı başarı olarak kabul edilmez. Oyuncuya ait salt okunur dosyalar korunur ve kaldırmayı engellemez.

## English

- Failed settings saves preserve the active player name, memory, authentication, close and language preferences. Drafts remain available for retry. Automatic update preferences refresh across cached pages and revert on failed persistence. Settings commits and mod pool transitions share the same lock.
- Required mod dependencies retain the original selected profile throughout downloads. Changing profiles cannot redirect the old dependency chain into the new active mod directory. Active writes and pool synchronization share the existing lock.
- Importing .minecraft mods preserves disabled and existing files. Copies commit from temporary files and stay in one profile pool.
- Launch libraries and JVM arguments use the same Windows OS, version and architecture rules as runtime verification. Native archives are excluded from ordinary classpaths; cyclic inheritance and escaping library paths fail safely.
- Uninstall reports read-only program files before deleting anything and rejects incomplete ownership records. Read-only user files remain intact and do not prevent uninstall.

## Downloads / İndirmeler

- `MistikSetup-Online-6.2.3.exe`
- `MistikSetup-Offline-6.2.3.exe`
- `MistikLauncher-6.2.3-win-x64.zip`
- `MistikRepair-6.2.3.exe`
- `SHA256SUMS.txt`

The regular build is unobfuscated and includes .NET. Close the launcher before offline repair; player settings, mods and worlds are preserved. / Normal derleme kod gizleme içermez; .NET pakete dahildir. Offline onarım öncesi launcher'ı kapatın; oyuncu ayarları, modlar ve dünyalar korunur.

Packages are unsigned. Regression checks use isolated profiles and files; they do not represent a full Minecraft play session. / Paketler imzasızdır. Regresyon kontrolleri ayrı test profilleri ve dosyalarla çalışır; tam Minecraft oyun oturumu testi değildir.