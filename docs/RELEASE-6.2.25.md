## Türkçe

- Mod taşıma, yerel JAR'ın SHA512 hash'inden doğrulanmış Modrinth proje kimliğini bulur. Dosya adı veya oyun içi mod kimliği artık proje adı olarak tahmin edilmez.
- Hedef profildeki mevcut modlar silinmez. Tanınmayan veya taşınamayan dosyalar kaynak profilde korunur ve sonuçta bildirilir.
- Hash'i doğrulanan kurulu modlar ve ortak bağımlılıklar yeniden indirilmez; gereksiz yedek oluşturulmaz. Bozuk dosyalar normal yedekleme yoluyla onarılır.
- Oyun çalışırken mod havuzu değişmez; bekleyen profil değişikliği oyun kapandıktan sonra uygulanır. Başlatmadan önce yeni eklenen yanlış yükleyicili modlar yeniden kontrol edilir.
- Java bulunamazsa gereken desteklenen Java sürümü bir kez kurulur; Java 25 gereken profil için önce Java 21 indirilmez.
- Güncelleme bildirimindeki bir hata sonraki kontrolleri kilitlemez. HTTP 304 doğrulaması önbelleğin güncelliğini yeniler.
- Tema, pencere düğmeleri, menü bağlantıları ve ışık tercihi kaydedilemezse önceki ayar ve seçimler korunur; görünür hata gösterilir.

Doğrulama: 649 uygulama kontrolü, paket doğrulaması dahil 661 kontrol ve 13 web testi geçti. Mod indirme ve Java seçim yolları kontrollü test yanıtlarıyla denendi; gerçek oyun ve Java indirmesi ayrıca çalıştırılmadı.

## English

- Mod migration resolves the verified Modrinth project from the local JAR's SHA512 hash instead of guessing from runtime IDs or filenames.
- Existing target mods are preserved. Unknown or unavailable mods remain in their source profile and are reported.
- Verified installed files and shared dependencies are reused without another download or redundant backup. Corrupted files are repaired through the normal backup path.
- Active-game mod pools stay unchanged; pending profile changes synchronize after exit. Newly added wrong-loader mods are checked again before launch.
- Missing Java installs the required supported runtime once, avoiding Java 21 followed by Java 25 for profiles requiring 25.
- Failing update observers cannot lock subsequent operations. HTTP 304 validation refreshes cache age.
- Failed appearance preference saves preserve previous themes, caption styles, navigation links and lighting selections and show an error.

Validation: 649 application checks, 661 checks including package verification, and 13 website tests passed. Mod download and Java selection paths used controlled responses; real game startup and Java downloads were not exercised separately.
