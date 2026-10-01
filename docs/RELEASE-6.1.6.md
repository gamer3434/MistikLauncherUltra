# Mistik Launcher Ultra 6.1.6

## Türkçe

- Sürümler menüsü ilk açılışta 40 kart gösterir. Daha fazla sürüm gerektiğinde yüklenir; uzun listelerde ilk çizim yükü azaltılır.
- Menü düğmeleri aynı görsel şablonu paylaşır; her düğmede yeniden XAML çözümlemesi yapılmaz.
- Arkadaşlar sayfasında değişmeyen oyuncu kartları ve avatarlar tekrar yüklenmez; sayfa kapalıyken yenileme durur, yeniden açılınca devam eder.
- Takılan güncelleme indirmeleri zaman aşımıyla sonlanır; güncelleme denetimi tekrar kullanılabilir.
- Hazır güncelleme, launcher meşgulse bekletilir; indirilmiş paket korunur.
- Windows dosya sürümü artık launcher sürümünü izler.

## English

- The Versions menu initially displays 40 cards. More versions load on demand, reducing the initial rendering work for long lists.
- Menu buttons share one visual template, avoiding repeated XAML parsing for each button.
- The Friends page skips unchanged player cards and avatar downloads. Refreshing stops while the page is closed and resumes when reopened.
- Stalled update downloads time out, allowing update checks to run again.
- A prepared update waits while the launcher is busy; the downloaded package is retained.
- The Windows file version now follows the launcher version.
