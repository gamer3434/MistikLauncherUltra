# Pencere ve üst çubuk / Window and toolbar

## Preview.8 — Güncel aydınlatma / Current lighting

**Gerekçe / Reasoning:** Kullanıcı ayrı Rainbow yerine düzgün dairesel, renk geçişli RGB istedi. / The user requested a true circular RGB color cycle instead of a separate Rainbow mode.

**Uygulama / Implementation:** Eşit çaplı 28×28 elips ve ortalı kapatma simgesi. Tema, RGB · renk geçişi, Kapalı seçenekleri; RGB çember ve ışık için eşzamanlı, sekiz saniyelik yumuşak renk döngüsüdür. Diğer pencere düğmelerinde aydınlatma yoktur. Sabit RGB kutusu ve Rainbow seçeneği kaldırıldı; önceki Rainbow tercihi RGB'ye taşınır. / Equal-diameter ellipse and centered close glyph; Theme, RGB color cycle, Off. Native WPF keyframes animate the ring and glow together. Other caption buttons have no glow.

**Sonuç / Conclusion:** Korumalı yerel test imzalı paket **198** kontrolü geçti; gerçek zaman içinde renk değişimi ve ışık eşleşmesi iki dilde doğrulandı. Bulut hesabı arayüzü ve otomatik profil eşitlemesi kaldırıldı. / Protected locally test-signed package passed 198 checks, including actual color change over time and synchronized glow in both languages. Cloud account UI and automatic profile synchronization were removed.

**Gerekçe / Reasoning:** Kullanıcının RedX ekran görüntüleri koyu, sakin yüzeyler ve renkli seçili öğeler gösteriyor. Üst çubuk, yan menü ve başlatma alanı aynı temayı kullanmalı; kişiselleştirme temel pencere işlevlerini korumalı.

**Uygulama / Implementation:**

- Ayarlar → Üst kısayol çubuğu: Ana panel, Sürümler, Mod merkezi, Karakter, Sunucular, Ayarlar seçeneklerini ayrı ayrı açıp kapatın. Tümünü kapatmak çubuğu gizler. / Settings → Top shortcut bar: toggle six shortcuts individually; disable all to hide it.
- Ayarlar → Pencere düğmesi stili: macOS, Windows, Minimal, Neon, Retro, Cam, Terminal, Hap, Cyberpunk, Balon, Aurora, Elmas, Neon çerçeve, Buzlu cam, Samuray, Hologram. Seçimler anında uygulanır ve kaydedilir. / Settings → Window button style: choose among 16 immediately applied, persisted appearances.
- Tüm stiller WPF `WindowChrome` ile tek özel başlık çubuğu kullanır. Windows stili sade kare düğmelerdir. Büyütülmüş pencerede 6 piksel çerçeve payı ayrılır; küçültme, büyütme/geri yükleme, kapatma, sürükleme ve yeniden boyutlandırma korunur. / All styles use one custom WPF WindowChrome caption; Windows uses neutral square buttons. Maximized windows reserve a 6-pixel frame inset.
- Yan menü kaldırıldı. Ortalı üst menü Segoe MDL2 çizgi ikonları, tema dolgusu ve vurgu yazısı kullanır; oyuncu adı, skin yüzü ve dil sağ üsttedir. Profil düğmesi ayarları açar. Klavye odağı ve iki dilde erişilebilir adlar bulunur. / Centered top navigation replaces the sidebar; player name, skin face and language appear at the right. The profile button opens settings.
- 20 temada ana aksiyon yazısı siyah/beyaz olarak göreli parlaklığa göre seçilir ve en az 4.5:1 kontrast doğrulanır. / Main action text meets 4.5:1 in all 20 themes.

Kod / Code: `MistikLauncher/WindowAppearance.cs`, `MainWindow.xaml`, `MainWindow.xaml.cs`, `Pages/ModernPages.cs`, `ColorThemes.cs`, `Core.cs`, `Locales/tr.json`, `Locales/en.json`.

**Sonuç / Conclusion:** Sürüm / version `6.0.0-preview.5`; korumalı pakette / protected package **136 checks passed**, including 16 styles, minimize/maximize/restore, title-bar inset, profile navigation, shortcut customization, contrast and both languages. Actual Minecraft gameplay and multiple-monitor taskbar layout remain unverified. See `screenshots/home-tr.png`, `home-en.png`, `settings-tr.png`, `settings-en.png`.
