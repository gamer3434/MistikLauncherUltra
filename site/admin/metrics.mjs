const codes = Object.freeze({
  'MLU-INTEGRITY': 'Dosya bütünlüğü', 'MLU-MEMORY': 'Bellek', 'MLU-JAVA': 'Java sürümü',
  'MLU-MOD-DEPENDENCY': 'Mod bağımlılığı', 'MLU-MOD-MIXIN': 'Mod uyumsuzluğu',
  'MLU-CLASSPATH': 'Eksik sınıf', 'MLU-FILE': 'Eksik dosya', 'MLU-EXIT': 'Oyun çıkışı',
  'MLU-LAUNCH': 'Oyun başlatma', 'MLU-APP': 'Launcher hatası'
});
export function summarize(data, now) {
  const rows = object => Object.values(object && typeof object === 'object' ? object : {});
  const recent = (time, span) => Number.isFinite(time) && time <= now + 60000 && time >= now - span;
  const validVersion = version => typeof version === 'string' && /^\d{1,3}\.\d{1,3}\.\d{1,3}$/.test(version);
  const installations = rows(data?.installations).filter(item => item && validVersion(item.version) && Number.isFinite(item.firstSeen) && Number.isFinite(item.lastSeen));
  const sessions = rows(data?.sessions).filter(item => item && validVersion(item.version));
  const versions = {};
  for (const item of installations) versions[item.version] = (versions[item.version] || 0) + 1;
  const errors = [];
  for (const perInstallation of rows(data?.errors)) for (const item of rows(perInstallation)) {
    if (!item || !Object.hasOwn(codes, item.code) || !validVersion(item.version) || !Number.isFinite(item.lastSeen) || !Number.isSafeInteger(item.count) || item.count < 1) continue;
    errors.push({code: item.code, label: codes[item.code], version: item.version, lastSeen: item.lastSeen, count: item.count});
  }
  const categories = {};
  for (const item of errors) categories[item.label] = (categories[item.label] || 0) + item.count;
  return { installations: installations.length, active: sessions.filter(item => recent(item.lastSeen, 5 * 60000)).length,
    daily: installations.filter(item => recent(item.lastSeen, 24 * 3600000)).length,
    recentErrors: errors.filter(item => recent(item.lastSeen, 24 * 3600000)).length,
    versions: Object.entries(versions).sort((a,b) => b[1] - a[1]), categories: Object.entries(categories).sort((a,b) => b[1] - a[1]),
    errors: errors.sort((a,b) => b.lastSeen - a.lastSeen).slice(0, 50) };
}
