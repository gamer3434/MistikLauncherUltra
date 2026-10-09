# Mistik Launcher Ultra 6.2.6

- Modrinth araması seçili Minecraft sürümü ve mod yükleyicisine göre filtrelenir. Profil değiştiğinde geciken arama sonucu veya hata eski mod listesini göstermez.
- Başarısız mod kurulumu artık başarı olarak gösterilmez; kurulum düğmesi yeniden denemeye açılır.
- CurseForge dosya geçmişi sayfalardan tamamlanır ve gerçek tarihe göre sıralanır. Birbiriyle uyumsuz gerekli bağımlılıklar kurulmadan reddedilir.
- CurseForge kurulumu doğrudan maskeli anahtar alanını açar. Resmî başvuru ve anahtar yönetimi bağlantıları eklendi; sayfadan çıkıp dönmek kaydedilmemiş anahtar taslağını silmez. Anahtar yalnız Kaydet ile şifreli yerel ayarlara yazılır.

## English

Mod searches follow the selected Minecraft version and loader, including profile changes during a pending search. Failed installs remain retryable instead of reporting success. CurseForge file history is paginated and sorted by actual timestamps; incompatible required dependencies are rejected before installation. Setup opens the masked key field directly and provides official application and key management links. Unsaved key drafts survive navigation without being saved automatically.

## Downloads / İndirmeler

- `MistikSetup-Online-6.2.6.exe`
- `MistikSetup-Offline-6.2.6.exe`
- `MistikLauncher-6.2.6-win-x64.zip`
- `MistikRepair-6.2.6.exe`
- `SHA256SUMS.txt`

Normal derleme .NET içerir. Onarım öncesi launcher kapatılmalıdır; oyuncu ayarları, modlar ve dünyalar korunur.

CurseForge için kişisel API başvurusunun onaylanması gerekir. Anahtarınızı Ayarlar → Entegrasyonlar alanına girin; sohbetlere veya GitHub'a eklemeyin. [Resmî başvuru bilgisi](https://support.curseforge.com/support/solutions/articles/9000208346-about-the-curseforge-api-and-how-to-apply-for-a-key).

Tests use isolated data and fake API responses. Live CurseForge access, a full Minecraft session and hardware FPS measurements were not tested. Packages are unsigned. / Kontroller ayrı test verileri ve sahte API yanıtlarıyla çalışır. Canlı CurseForge erişimi, tam Minecraft oturumu ve donanım FPS ölçümü denenmedi. Paketler imzasızdır.