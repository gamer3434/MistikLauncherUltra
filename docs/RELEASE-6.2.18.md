# Mistik Launcher 6.2.18

- GitHub API hız sınırına ulaşıldığında güncelleyici resmi son sürüm sayfasını kullanarak yeni sürümü bulur.
- İndirilecek paketin SHA-256 özeti, GitHub sürümünün `SHA256SUMS.txt` dosyasındaki tam dosya adıyla doğrulanır.
- When the GitHub API is rate-limited, the updater discovers the latest stable release from the official release page and verifies its exact package against the published SHA-256 list.
