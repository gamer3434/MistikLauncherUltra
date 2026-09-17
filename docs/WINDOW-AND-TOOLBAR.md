# Pencere ve üst çubuk / Window and toolbar

## Preview.9 — Güncel aydınlatma / Current lighting

**Gerekçe / Reasoning:** Kullanıcı merkezde duran çarpı ve köşeleri yuvarlatılmış dikdörtgen istedi. / The user requested a centered cross and rounded rectangle.

**Uygulama / Implementation:** 38×24 dikdörtgen, 5 piksel köşe yarıçapı ve simetrik vektör çarpı. Tema, RGB · renk geçişi, Kapalı seçenekleri; çerçeve ve ışık eşzamanlı sekiz saniyelik döngü kullanır. Diğer pencere düğmelerinde aydınlatma yoktur. / 38×24 rectangle, radius 5 and symmetric vector cross. Native WPF keyframes animate only the close outline/glow together.

**Sonuç / Conclusion:** Korumalı yerel test imzalı paket **209** kontrolü geçti; gerçek zaman içinde renk değişimi ve ışık eşleşmesi iki dilde doğrulandı. / Protected locally test-signed package passed 209 checks, including actual color change over time and synchronized glow in both languages.

**Gerekçe / Reasoning:** Kullanıcının RedX ekran görüntüleri koyu, sakin yüzeyler ve renkli seçili öğeler gösteriyor. Üst çubuk, yan menü ve başlatma alanı aynı temayı kullanmalı; kişiselleştirme temel pencere işlevlerini korumalı.

**Uygulama / Implementation:**

- Ayarlar → Üst kısayol çubuğu: Ana panel, Sürümler, Mod merkezi, Karakter, Sunucular, Ayarlar seçeneklerini ayrı ayrı açıp kapatın. Tümünü kapatmak çubuğu gizler. / Settings → Top shortcut bar: toggle six shortcuts individually; disable all to hide it.
- Ayarlar → Pencere düğmesi stili: macOS, Windows, Minimal, Neon, Retro, Cam, Terminal, Hap, Cyberpunk, Balon, Aurora, Elmas, Neon çerçeve, Buzlu cam, Samuray, Hologram. Seçimler anında uygulanır ve kaydedilir. / Settings → Window button style: choose among 16 immediately applied, persisted appearances.
- Tüm stiller WPF `WindowChrome` ile tek özel başlık çubuğu kullanır. Windows stili sade kare düğmelerdir. Büyütülmüş pencerede 6 piksel çerçeve payı ayrılır; küçültme, büyütme/geri yükleme, kapatma, sürükleme ve yeniden boyutlandırma korunur. / All styles use one custom WPF WindowChrome caption; Windows uses neutral square buttons. Maximized windows reserve a 6-pixel frame inset.
- Yan menü kaldırıldı. Ortalı üst menü Segoe MDL2 çizgi ikonları, tema dolgusu ve vurgu yazısı kullanır; oyuncu adı, skin yüzü ve dil sağ üsttedir. Profil düğmesi ayarları açar. Klavye odağı ve iki dilde erişilebilir adlar bulunur. / Centered top navigation replaces the sidebar; player name, skin face and language appear at the right. The profile button opens settings.
- 20 temada ana aksiyon yazısı siyah/beyaz olarak göreli parlaklığa göre seçilir ve en az 4.5:1 kontrast doğrulanır. / Main action text meets 4.5:1 in all 20 themes.

Kod / Code: `MistikLauncher/WindowAppearance.cs`, `MainWindow.xaml`, `MainWindow.xaml.cs`, `Pages/ModernPages.cs`, `ColorThemes.cs`, `Core.cs`, `Locales/tr.json`, `Locales/en.json`.

**Sonuç / Conclusion:** Sürüm / version `6.0.0-preview.5`; korumalı pakette / protected package **136 checks passed**, including 16 styles, minimize/maximize/restore, title-bar inset, profile navigation, shortcut customization, contrast and both languages. Actual Minecraft gameplay and multiple-monitor taskbar layout remain unverified. See `screenshots/home-tr.png`, `home-en.png`, `settings-tr.png`, `settings-en.png`.
