const codes = Object.freeze({
  'MLU-INTEGRITY': 'Dosya bütünlüğü', 'MLU-MEMORY': 'Bellek', 'MLU-JAVA': 'Java sürümü',
  'MLU-MOD-DEPENDENCY': 'Mod bağımlılığı', 'MLU-MOD-MIXIN': 'Mod uyumsuzluğu',
  'MLU-CLASSPATH': 'Eksik sınıf', 'MLU-FILE': 'Eksik dosya', 'MLU-EXIT': 'Oyun çıkışı',
  'MLU-LAUNCH': 'Oyun başlatma', 'MLU-APP': 'Launcher hatası'
});
export function summarize(data, now) {
  const entries = object => Object.entries(object && typeof object === 'object' && !Array.isArray(object) ? object : {});
  const timestamp = time => Number.isSafeInteger(time) && time >= 0 && time <= now + 60000;
  const recent = (time, span) => timestamp(time) && time >= now - span;
  const validVersion = version => typeof version === 'string' && /^\d{1,3}\.\d{1,3}\.\d{1,3}$/.test(version);
  const installations = new Map(), sessions = new Map();
  let ignoredRecords = 0, orphanSessions = 0, unlinkedErrorRows = 0;
  for (const [id, item] of entries(data?.installations)) {
    if (!item || !validVersion(item.version) || !timestamp(item.firstSeen) || !timestamp(item.lastSeen) || item.firstSeen > item.lastSeen) { ignoredRecords++; continue; }
    installations.set(id, item);
  }
  for (const [id, item] of entries(data?.sessions)) {
    if (!item || !validVersion(item.version) || !timestamp(item.lastSeen)) { ignoredRecords++; continue; }
    if (!installations.has(id)) { orphanSessions++; ignoredRecords++; continue; }
    sessions.set(id, item);
  }
  const versions = new Map(), categories = new Map(), errorOwners = new Set(), ownerErrors = new Map();
  for (const [id, item] of installations) {
    const row = versions.get(item.version) || {count:0, active:0};
    row.count++; if (recent(sessions.get(id)?.lastSeen, 5 * 60000)) row.active++;
    versions.set(item.version, row);
  }
  const errors = [];
  for (const [id, perInstallation] of entries(data?.errors)) for (const [, item] of entries(perInstallation)) {
    if (!item || !Object.hasOwn(codes, item.code) || !validVersion(item.version) || !timestamp(item.lastSeen) || !Number.isSafeInteger(item.count) || item.count < 1 || item.count > 1000000000) { ignoredRecords++; continue; }
    errors.push({code: item.code, label: codes[item.code], version: item.version, lastSeen: item.lastSeen, count: item.count});
    errorOwners.add(id); ownerErrors.set(id, (ownerErrors.get(id) || 0) + item.count);
    if (!installations.has(id)) unlinkedErrorRows++;
    const category = categories.get(item.code) || {label:codes[item.code], count:0, owners:new Set(), lastSeen:0};
    category.count += item.count; category.owners.add(id); category.lastSeen = Math.max(category.lastSeen, item.lastSeen);
    categories.set(item.code, category);
  }
  const day = 24 * 3600000, installationValues = [...installations.values()];
  const activeSince = span => installationValues.filter(item => recent(item.lastSeen, span)).length;
  const newSince = span => installationValues.filter(item => recent(item.firstSeen, span)).length;
  const versionRows = [...versions].sort((a,b) => b[1].count - a[1].count).map(([version, value]) =>
    [version, value.count, value.active, Math.round(value.count / installations.size * 1000) / 10]);
  const categoryRows = [...categories.values()].sort((a,b) => b.count - a.count).map(item => [item.label, item.count, item.owners.size, item.lastSeen]);
  const installationRows = [...installations].sort((a,b) => b[1].lastSeen - a[1].lastSeen).map(([id, item], index) => ({
    ordinal:index + 1, version:item.version, firstSeen:item.firstSeen, lastSeen:item.lastSeen,
    active:recent(sessions.get(id)?.lastSeen, 5 * 60000), errors:ownerErrors.get(id) || 0
  }));
  return { installations: installations.size, active: [...sessions.values()].filter(item => recent(item.lastSeen, 5 * 60000)).length,
    daily: activeSince(day), weekly: activeSince(7 * day), monthly: activeSince(30 * day),
    newDaily: newSince(day), newWeekly: newSince(7 * day),
    recentErrors: errors.filter(item => recent(item.lastSeen, 24 * 3600000)).length,
    totalErrors: errors.reduce((total,item) => total + item.count, 0), errorInstallations:errorOwners.size, errorRows:errors.length,
    lastHeartbeat: installationValues.reduce((last,item) => Math.max(last,item.lastSeen), 0) || null,
    validSessions:sessions.size, ignoredRecords, orphanSessions, unlinkedErrorRows,
    versionRows, categoryRows, installationRows,
    versions:versionRows.map(row => row.slice(0,2)), categories:categoryRows.map(row => row.slice(0,2)),
    errors: errors.sort((a,b) => b.lastSeen - a.lastSeen) };
}
