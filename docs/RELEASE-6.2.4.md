# Mistik Launcher 6.2.4

## Türkçe

- Java 21 ve 25 resmî Adoptium sürüm bilgisi, SHA-256, arşiv boyutu ve Windows runtime yapısıyla doğrulanır. Yeni kurulum hazır olmadan mevcut Java değiştirilmez; başarısız kurulum geri alınabilir ve geçici dosyalar temizlenir.
- Java onarımı açık Java işlemlerini kapatmaz. Kilitli runtime korunur, diğer Java sürümüne dokunulmaz. Bozuk yerel runtime kullanılmaz; indirmeler ve Java araması zaman sınırıyla çalışır.
- Oyun hazırlanırken ikinci başlatma isteği yok sayılır. Hazırlık sırasında profil değişirse eski profil yeni mod havuzuyla başlatılmaz. Sunucu yöneticisi başlatma sırasında da tekrar açılmaz.
- Ana panelde eski profilin geciken onarım sonucu ve ilerleme bildirimi yeni profilin hazırlık kartını değiştirmez. Başarısız onarım sonrası yeniden denetleme kullanılabilir kalır.
- Auto-MCS güncellemesi aynı kurulumdaki farklı launcher işlemleri arasında kilitlenir; eşzamanlı güncellemeler program dosyası ve sürüm kaydını bozmaz.

## English

- Java 21 and 25 downloads validate official Adoptium metadata, SHA-256, archive size and the complete Windows runtime structure. Installation stages before replacing the existing runtime, supports rollback and cleans temporary files.
- Java repair leaves running Java processes and other runtime versions intact. Locked installations are preserved. Invalid local runtimes are excluded; downloads and PATH lookup have deadlines.
- Repeated game launches are ignored during preparation. A changed profile cannot start with another profile's active mod pool. Server manager launches also reject overlapping requests.
- Delayed repair results and progress cannot overwrite another profile's readiness card. Failed repairs leave an accessible check-again action.
- Auto-MCS updates hold a shared installation lock across launcher processes to protect executable and receipt commits.

## Downloads / İndirmeler

- `MistikSetup-Online-6.2.4.exe`
- `MistikSetup-Offline-6.2.4.exe`
- `MistikLauncher-6.2.4-win-x64.zip`
- `MistikRepair-6.2.4.exe`
- `SHA256SUMS.txt`

The regular build is unobfuscated and includes .NET. Close the launcher before offline repair; player settings, mods and worlds are preserved. / Normal derleme kod gizleme içermez; .NET pakete dahildir. Offline onarım öncesi launcher'ı kapatın; oyuncu ayarları, modlar ve dünyalar korunur.

Packages are unsigned. Regression checks use isolated profiles and files; they do not represent a full Minecraft play session. / Paketler imzasızdır. Regresyon kontrolleri ayrı test profilleri ve dosyalarla çalışır; tam Minecraft oyun oturumu testi değildir.
