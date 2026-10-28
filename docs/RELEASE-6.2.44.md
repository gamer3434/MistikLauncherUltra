## Türkçe

- Fabric/Quilt modları oyun başlamadan önce otomatik güncellenir. Minecraft sürümü ve yükleyici değişmez; yalnızca daha yeni kararlı Modrinth dosyaları seçilir. Ayarlar'dan kapatılabilir.
- Dosyalar önce ayrı klasörde hazırlanır ve SHA512 ile doğrulanır. Eski modların tamamı `.backups/auto-update-*` klasöründe tutulur. Devre dışı ve Modrinth'te tanınmayan yerel modlar korunur.
- Bağımlılık sabitlemeleri, doğrulanamayan sürüm koşulları, yeni Java/yükleyici gereksinimleri ve değişmiş dosyalar otomatik kurulumdan geçmez. Eksik yeni bağımlılık gerektiren güncellemeler elle yönetilir. Diğer Java işlemleri açıkken dosyalar değiştirilmez.
- Mod merkezine Modrinth bağlantısı yapıştırılabilir. Herkese açık yeni projeler arama indeksine girmeden de bulunabilir; erişilemeyen veya mod olmayan projeler açıklanır. Taslak/özel projeler indirilemez.

## English

- Before launch, Fabric/Quilt mods automatically check newer stable Modrinth releases for the exact Minecraft version and loader. This can be disabled in Settings.
- Downloads are staged and SHA512 verified before replacing installed files. Complete originals remain under `.backups/auto-update-*`. Disabled and unidentified local mods are preserved.
- Dependency pins, unverified version predicates, changed Java/loader requirements and concurrent file changes prevent automatic application. Updates needing missing dependencies require manual management. Files are not replaced while another Java process is running.
- Paste a direct Modrinth project link into the catalog. Public projects can be resolved before search indexing; inaccessible and non-mod projects receive clear messages. Draft/private projects cannot be downloaded.

Checks cover staged updates, backups, exact game/loader selection, release channels, downgrade prevention, runtime/API dependency pins, failed downloads, cancellation, locked files and concurrent changes. Actual gameplay with third-party updated mods is not verified.
