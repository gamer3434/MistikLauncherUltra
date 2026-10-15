import { summarize } from './metrics.mjs';
import { mountSupport } from './support.mjs';
const $ = id => document.getElementById(id);
function status(message, error = false) { if ($('status').textContent !== message) $('status').textContent = message; $('status').dataset.error = String(error); }
let displayedErrors = [];
function clear() {
  $('dashboard').hidden = true;
  for (const id of ['installations','active','daily','recentErrors','weekly','monthly','newDaily','newWeekly','lastUpdated','lastHeartbeat','validSessions','ignoredRecords']) $(id).textContent = '—';
  for (const id of ['versions','categories','errors','installationRows']) $(id).replaceChildren();
  displayedErrors = []; $('errorCount').textContent = ''; $('installationCount').textContent = ''; $('coverageNote').textContent = '';
}
function table(id, rows, columns, emptyMessage = 'Henüz veri yok') {
  const body = $(id); body.replaceChildren();
  if (!rows.length) rows = [Array.from({length:columns}, (_,i) => i ? '—' : emptyMessage)];
  for (const row of rows) { const tr = document.createElement('tr'); for (const value of row) { const td = document.createElement('td'); td.textContent = String(value); tr.append(td); } body.append(tr); }
}
function errorFilter(id, values, label) {
  const select = $(id), selected = select.value;
  const options = [...select.children];
  if (options.length === values.length + 1 && options[0].textContent === label && values.every((value, index) => options[index + 1].value === value)) return;
  select.replaceChildren(Object.assign(document.createElement('option'), {value:'', textContent:label}));
  for (const value of values) select.append(Object.assign(document.createElement('option'), {value, textContent:value}));
  select.value = values.includes(selected) ? selected : '';
}
function renderErrors() {
  const category = $('errorCategory').value, version = $('errorVersion').value;
  const matches = displayedErrors.filter(item => (!category || item.label === category) && (!version || item.version === version));
  const visible = matches.slice(0, 50);
  table('errors', visible.map(item => [item.label, item.version, date(item.lastSeen), number(item.count)]), 4, displayedErrors.length ? 'Bu filtrelere uyan bildirim yok' : 'Henüz hata bildirilmedi');
  $('errorCount').textContent = `${visible.length} / ${displayedErrors.length} kayıt gösteriliyor${matches.length > visible.length ? ` · Filtreye uyan ${number(matches.length)} kaydın en yeni 50 tanesi` : ''}`;
  $('resetErrors').disabled = !category && !version;
}
$('errorCategory').onchange = renderErrors;
$('errorVersion').onchange = renderErrors;
$('resetErrors').onclick = () => { $('errorCategory').value = ''; $('errorVersion').value = ''; renderErrors(); };
function externalLink(url, label) {
  const link = document.createElement('a'); link.href = url; link.textContent = label;
  link.target = '_blank'; link.rel = 'noopener noreferrer'; return link;
}
const number = value => new Intl.NumberFormat('tr-TR').format(value);
const date = value => value === null || value === undefined ? 'Henüz yok' : new Date(value).toLocaleString('tr-TR');
async function fetchJson(url) {
  const response = await fetch(url, {headers:{Accept:'application/json'}, cache:'no-store', signal:AbortSignal.timeout(10000)});
  if (!response.ok) throw new Error('source unavailable');
  return response.json();
}
async function loadLauncherDetails() {
  const panel = $('releaseDetails');
  try {
    const release = await fetchJson('https://api.github.com/repos/gamer3434/MistikLauncherUltra/releases/latest');
    if (release.draft !== false || release.prerelease !== false || !/^v\d+\.\d+\.\d+$/.test(release.tag_name || '') || release.html_url !== `https://github.com/gamer3434/MistikLauncherUltra/releases/tag/${release.tag_name}`) throw new Error('release');
    const published = Date.parse(release.published_at);
    const version = release.tag_name.slice(1);
    const names = new Set([`MistikSetup-Online-${version}.exe`, `MistikSetup-Offline-${version}.exe`, `MistikRepair-${version}.exe`, `MistikLauncher-${version}-win-x64.zip`]);
    const assets = [...names].flatMap(name => {
      const matches = (Array.isArray(release.assets) ? release.assets : []).filter(asset => asset && asset.name === name && asset.browser_download_url === `https://github.com/gamer3434/MistikLauncherUltra/releases/download/${release.tag_name}/${name}`);
      return matches.length === 1 ? matches : [];
    });
    const validCount = count => Number.isSafeInteger(count) && count >= 0;
    const total = assets.reduce((sum, asset) => sum + (validCount(asset.download_count) ? asset.download_count : 0), 0);
    const downloads = assets.length === names.size && assets.every(asset => validCount(asset.download_count)) && Number.isSafeInteger(total) ? total : null;
    panel.replaceChildren();
    const versionLine = document.createElement('strong'); versionLine.className = 'release-version'; versionLine.textContent = 'v' + version; panel.append(versionLine);
    const facts = document.createElement('div'); facts.className = 'release-facts';
    for (const value of [Number.isFinite(published) ? date(published) : 'Tarih yok', downloads === null ? 'İndirme sayısı doğrulanamadı' : number(downloads) + ' program dosyası indirmesi']) {
      const item = document.createElement('span'); item.textContent = value; facts.append(item);
    }
    panel.append(facts);
    panel.append(externalLink(release.html_url, 'Sürüm notlarını aç ↗'));
    const source = document.createElement('p'); source.className = 'source-note'; source.textContent = 'Kaynak: GitHub · Son yayın · Tekrar indirmeler dahildir; SHA256SUMS indirmeleri hariçtir. Kullanıcı veya kurulum sayısı değildir. Alınma: ' + date(Date.now()); panel.append(source);
    for (const asset of assets) {
      const item = document.createElement('div'); item.className = 'asset-row';
      const label = externalLink(asset.browser_download_url, asset.name);
      const details = document.createElement('span'); details.textContent = `${Number.isSafeInteger(asset.size) && asset.size >= 0 ? number(Math.round(asset.size / 1048576 * 10) / 10) + ' MB' : 'Boyut bilinmiyor'} · ${Number.isSafeInteger(asset.download_count) && asset.download_count >= 0 ? number(asset.download_count) : '—'} indirme`;
      item.append(label, details); panel.append(item);
    }
  } catch { panel.replaceChildren(Object.assign(document.createElement('p'), {className:'empty-state', textContent:'Sürüm bilgisi şu anda alınamadı.'})); }
}
async function loadCatalogMods() {
  const panel = $('catalogMods');
  try {
    const params = new URLSearchParams({limit:'6', index:'downloads', facets:JSON.stringify([['project_type:mod']])});
    const result = await fetchJson('https://api.modrinth.com/v2/search?' + params); panel.replaceChildren();
    for (const mod of (Array.isArray(result.hits) ? result.hits : []).slice(0, 6)) {
      if (typeof mod.slug !== 'string' || !/^[\w-]{1,100}$/.test(mod.slug)) continue;
      const item = document.createElement('article'); item.className = 'mod-item';
      const title = externalLink('https://modrinth.com/mod/' + encodeURIComponent(mod.slug), typeof mod.title === 'string' ? mod.title : 'Mod'); title.className = 'mod-title';
      const description = document.createElement('p'); description.textContent = typeof mod.description === 'string' ? mod.description : '';
      const meta = document.createElement('span'); meta.className = 'mod-downloads'; meta.textContent = '↓ ' + (Number.isSafeInteger(mod.downloads) && mod.downloads >= 0 ? number(mod.downloads) : '—') + ' Modrinth indirmesi';
      item.append(title, description, meta); panel.append(item);
    }
    if (!panel.childElementCount) throw new Error('empty catalog');
  } catch { panel.replaceChildren(Object.assign(document.createElement('p'), {className:'empty-state', textContent:'Mod kataloğu şu anda alınamadı.'})); }
}
function render(data, now, receivedAt = now) {
  const result = summarize(data, now);
  const observed = result.installations > 0 || result.errorRows > 0;
  for (const id of ['installations','active','daily','recentErrors','weekly','monthly','newDaily','newWeekly']) $(id).textContent = observed ? number(result[id]) : '—';
  $('lastHeartbeat').textContent = date(result.lastHeartbeat);
  $('validSessions').textContent = number(result.validSessions);
  $('ignoredRecords').textContent = number(result.ignoredRecords);
  $('coverageNote').textContent = observed
    ? `${number(result.installations)} geçerli katılım kaydı; ${number(result.errorInstallations)} anonim kimlikte ${number(result.totalErrors)} birikimli hata olayı. Toplam launcher kullanıcı sayısı ve paylaşımı kapalı kurulumlar bilinmiyor.${result.ignoredRecords ? ` ${number(result.ignoredRecords)} geçersiz veya kurulumla eşleşmeyen kayıt hesaplamaya alınmadı.` : ''}${result.unlinkedErrorRows ? ` ${number(result.unlinkedErrorRows)} hata kaydının eşleşen kurulum kaydı yok.` : ''}`
    : result.ignoredRecords ? 'Gelen kayıtlar doğrulanamadı; bu sayılardan kullanım sonucu çıkarılamaz. Geçersiz zaman, sürüm veya eşleşmeyen oturum kayıtları sayılmaz.'
    : 'Henüz katılım kaydı yok. Bu, launcher’ı kimsenin kullanmadığı anlamına gelmez. Toplam kullanıcı sayısı bilinmiyor; istatistik paylaşımı Ayarlar’dan isteğe bağlı açılır.';
  table('versions', result.versionRows.map(row => [row[0], number(row[1]), number(row[2]), number(row[3]) + '%']), 4);
  table('categories', result.categoryRows.map(row => [row[0], number(row[1]), number(row[2]), date(row[3])]), 4);
  const installations = result.installationRows.slice(0, 100);
  table('installationRows', installations.map(item => ['Kayıt ' + item.ordinal, item.version, date(item.firstSeen), date(item.lastSeen), item.active ? 'Son 5 dk aktif' : 'Etkin sinyal yok', number(item.errors)]), 6, 'Henüz katılım kaydı yok');
  $('installationCount').textContent = `${number(installations.length)} / ${number(result.installations)} kayıt · Sıra numaraları bu görünüm içindir; kalıcı kimlik değildir.`;
  displayedErrors = result.errors;
  errorFilter('errorCategory', [...new Set(displayedErrors.map(item => item.label))].sort((a,b) => a.localeCompare(b, 'tr')), 'Tüm kategoriler');
  errorFilter('errorVersion', [...new Set(displayedErrors.map(item => item.version))].sort((a,b) => b.localeCompare(a, undefined, {numeric:true})), 'Tüm sürümler');
  renderErrors();
  $('lastUpdated').textContent = new Date(receivedAt).toLocaleTimeString('tr-TR');
  $('lastUpdated').dateTime = new Date(receivedAt).toISOString();
  $('dashboard').hidden = false;
}
try {
  const response = await fetch('./admin-config.json', {cache:'no-store'});
  if (!response.ok) throw new Error('config');
  const config = await response.json();
  if (!['apiKey','authDomain','databaseURL','projectId'].every(key => typeof config[key] === 'string' && config[key].length)) throw new Error('config');
  const origin = new URL(config.databaseURL);
  if (origin.protocol !== 'https:' || !/\.(?:firebaseio\.com|firebasedatabase\.app)$/.test(origin.hostname)) throw new Error('config');
  const [{initializeApp}, authSdk, dbSdk] = await Promise.all([
    import('https://www.gstatic.com/firebasejs/12.19.0/firebase-app.js'),
    import('https://www.gstatic.com/firebasejs/12.19.0/firebase-auth.js'),
    import('https://www.gstatic.com/firebasejs/12.19.0/firebase-database.js')
  ]);
  const app = initializeApp(config), auth = authSdk.getAuth(app), database = dbSdk.getDatabase(app);
  await authSdk.setPersistence(auth, authSdk.browserSessionPersistence);
  let redirectError;
  try { await authSdk.getRedirectResult(auth); } catch (error) { redirectError = error?.code || "bağlantı"; }
  let unsubscribe = () => {}, connectedStop = () => {}, offsetStop = () => {}, timer, data, receivedAt, connected = false, offset = 0, generation = 0, support;
  const stopMetrics = () => {
    generation++; unsubscribe(); connectedStop(); offsetStop(); clearInterval(timer); support?.stop(); support = undefined;
    unsubscribe = connectedStop = offsetStop = () => {};
    data = undefined; connected = false; offset = 0; clear();
  };
  $('login').disabled = false;
  $('login').onclick = async () => {
    $('login').disabled = true;
    try { await authSdk.signInWithRedirect(auth, new authSdk.GoogleAuthProvider()); }
    catch (error) { clear(); status(error?.code === 'auth/unauthorized-domain' ? 'Bu site Firebase Authentication yetkili alan adlarına eklenmemiş.' : 'Google giriş sayfası açılamadı. (' + (error?.code || 'bağlantı') + ')', true); }
    finally { $('login').disabled = false; }
  };
  $('logout').onclick = async () => {
    $('logout').disabled = true;
    try { await authSdk.signOut(auth); }
    catch { status('Oturum kapatılamadı. Bağlantını kontrol edip yeniden dene.', true); }
    finally { $('logout').disabled = false; }
  };
  authSdk.onAuthStateChanged(auth, user => {
    stopMetrics();
    $('login').hidden = !!user; $('logout').hidden = !user;
    if (!user) { status(redirectError ? 'Google girişi tamamlanamadı. (' + redirectError + ')' : 'Bu panel yalnızca yetkili Google hesabına açıktır.', !!redirectError); return; }
    if (!user.emailVerified || !user.providerData?.some(provider => provider.providerId === 'google.com')) { status('Doğrulanmış, yetkili Google hesabıyla giriş yap.', true); return; }
    status('Yetki ve Firebase verileri kontrol ediliyor…');
    void loadLauncherDetails(); void loadCatalogMods();
    const current = generation;
    support = mountSupport({database, sdk:dbSdk, now:() => Date.now() + offset});
    let readError = false;
    const update = () => { if (current === generation && !readError && connected && data !== undefined) { render(data, Date.now() + offset, receivedAt); status('Bağlı · Canlı veriler; aktiflik her 30 saniyede yenilenir.'); } };
    connectedStop = dbSdk.onValue(dbSdk.ref(database, '.info/connected'), snapshot => {
      if (current !== generation || readError) return;
      connected = snapshot.val() === true;
      support?.connection(connected);
      if (!connected) { clear(); status('Bağlantı bekleniyor; mevcut sayılar doğrulanamıyor.', true); } else update();
    });
    offsetStop = dbSdk.onValue(dbSdk.ref(database, '.info/serverTimeOffset'), snapshot => { if (current !== generation) return; offset = Number(snapshot.val()) || 0; support?.refresh(); update(); });
    unsubscribe = dbSdk.onValue(dbSdk.ref(database, 'launcherMetrics'), snapshot => { if (current !== generation) return; readError = false; data = snapshot.val() || {}; receivedAt = Date.now() + offset; update(); }, () => {
      if (current !== generation) return;
      readError = true; data = undefined; clear(); status('Veriler okunamadı. Hesabın yönetici yetkisini ve veritabanı kurallarını kontrol edin.', true);
    });
    timer = setInterval(update, 30000);
  }, () => { stopMetrics(); $('login').hidden = false; $('logout').hidden = true; status('Google oturumu doğrulanamadı.', true); });
} catch {
  clear(); $('login').disabled = true;
  status('Panel yapılandırılmamış veya Firebase bağlantısı kurulamadı. Kurulum bilgilerini ve ağı kontrol edin.', true);
}
