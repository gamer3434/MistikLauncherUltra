# Mistik Launcher Ultra 6.1.0

## Türkçe

6.1.0, Minecraft başlatma güvenilirliğini artırır:

- Ana paneldeki **Başlatma hazırlığı** kartı seçili sürümü, Java gereksinimini, ayrılan belleği, boş diski ve etkin/kapalı mod sayılarını gösterir.
- **Dosyaları doğrula ve onar** eksik veya bozuk sürüm JAR/kütüphanelerini resmi HTTPS metadata'sındaki boyut ve SHA-1 ile kontrol eder. İndirme önce geçici dosyaya yapılır; doğrulama geçmeden mevcut dosya değiştirilmez.
- Forge/Fabric gibi miras profillerinde üst profil ve kütüphaneler birlikte denetlenir. Döngü, path traversal, bozuk metadata ve yarım indirme başlatmayı durdurur.
- Çökme penceresi, hata satırlarında açıkça adı geçen modları silmeden devre dışı bırakmak için **Güvenli kurtarma** düğmesi sunar.
- İzleme hatasında launcher gizli kalmaz; oyun kapandığında pencere geri açılır.
- Türkçe ve English arayüz, mevcut tema ve düşük maliyetli önbellekli navigasyon korunur.

## English

6.1.0 improves Minecraft launch reliability:

- The Home **Launch readiness** card shows the selected profile, Java requirement, allocated memory, free disk and enabled/disabled mod counts.
- **Verify and repair files** checks missing or corrupt version JARs and libraries against official HTTPS metadata, size and SHA-1. Downloads are staged to a temporary sibling and replace the existing file only after validation.
- Inherited Forge/Fabric profiles validate their parent profile and libraries together. Cycles, traversal, malformed metadata and partial downloads block launch safely.
- The crash dialog offers **Safe recovery** for disabling only mods explicitly named in error evidence, without deleting them.
- The launcher restores its window even if the game monitor encounters an unexpected error.
- Turkish and English UI, themes and cached low-latency navigation remain available.

Validation: **295 checks passed** in the ordinary run and **297** in the protected package on Windows/.NET 8, including bilingual WPF rendering, runtime repair fixtures and installer smoke checks.
