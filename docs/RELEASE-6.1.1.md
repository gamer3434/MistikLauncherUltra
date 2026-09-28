# Mistik Launcher Ultra 6.1.1

## Türkçe

- **Ayarlar → Launcher güncellemeleri** bölümünde resmî son kararlı sürümün duyurusu gösterilir. Türkçe duyuru varsa seçili dile göre kısa, düz metin olarak görünür.
- Sürüm karşılaştırması bozuk veya aşırı büyük etiketleri reddeder; geçersiz etiket güncellemeyi başlatamaz.
- Web sitesindeki sürüm, duyuru, indirme bağlantıları ve SHA-256 değerleri yeni kararlı GitHub Release yayımlandığında birlikte yenilenir. GitHub API erişilemezse son doğrulanmış statik bilgiler görünür.
- Kullanıcı profili, skin ve ayarlar siteye aktarılmaz.

## English

- **Settings → Launcher updates** shows a short plain-text announcement from the official latest stable release, in the selected language when that section exists.
- Version comparison rejects malformed or oversized tags before an update can start.
- The website refreshes its version, announcement, download links and SHA-256 values together when a new stable GitHub Release is published. Verified static content remains available if the GitHub API is unavailable.
- Player profiles, skins and settings are not sent to the website.

Validation: Windows/.NET 8 build; **298** ordinary and **300** protected checks passed. Online and offline installers passed package verification. Website release parser passed **3** Node tests.
