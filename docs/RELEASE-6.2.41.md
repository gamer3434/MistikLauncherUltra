## Türkçe

- ViaFabric ile ViaFabricPlus gibi birlikte çalışamayacağını açıkça bildiren Fabric modları indirme, elle JAR ekleme ve yeniden etkinleştirme sırasında engellenir. Reddedilen işlem mevcut dosyaları değiştirmez.
- Fabric/Quilt oyunu açılmadan önce etkin modlar tekrar denetlenir; çakışan dosyaların adları gösterilir. Son kontrol oyun işlemi başlatılırken de yapılır.
- Devre dışı dosyalar dikkate alınmaz. GEODE gibi iç içe çok sürümlü paketlerin alternatifleri yanlışlıkla etkin mod sayılmaz; Forge/NeoForge tanımlayıcısı taşıyan karma paketler bu sınırlı kontrolden çıkarılır.

Kapsam: üst düzey Fabric bildirimlerindeki koşulsuz `breaks: "*"` kuralları. Sürüme bağlı kurallar ve iç içe bağımlılık çözümü yükleyiciye aittir; bu kontrol bütün mod hatalarını önleme garantisi vermez.

838 normal, 850 paket kontrolü ve 46 web testi geçti. Gerçek kurulu mod klasörü salt okunur denetlendi; kapalı ViaFabric yeniden eklenecekmiş gibi kontrol edildiğinde ViaFabricPlus çakışması engellendi. Oyun başlatılmadı.

## English

- Fabric mods that explicitly declare an unconditional conflict, such as ViaFabric and ViaFabricPlus, are blocked during download installation, manual JAR import and re-enabling. Rejected operations preserve existing files.
- Active mods are checked before Fabric/Quilt launch, including a final check immediately before starting the game process. Warnings identify both filenames.
- Disabled files are ignored. Nested multi-version alternatives such as GEODE are left to the loader; hybrid packages carrying Forge/NeoForge descriptors are excluded from this limited guard.

Scope: unconditional top-level Fabric `breaks: "*"` declarations. Version-dependent rules and nested dependency resolution remain the loader's responsibility; this cannot guarantee prevention of all mod failures.

838 ordinary and 850 packaged checks plus 46 web tests passed. A read-only probe of the real installed mods succeeded; offering the disabled ViaFabric as an incoming JAR correctly detected ViaFabricPlus. The game was not launched.
