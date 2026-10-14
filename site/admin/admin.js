import { summarize } from './metrics.mjs';
const $ = id => document.getElementById(id);
function status(message, error = false) { $('status').textContent = message; $('status').dataset.error = String(error); }
let displayedErrors = [];
function clear() { $('dashboard').hidden = true; for (const id of ['installations','active','daily','recentErrors','lastUpdated']) $(id).textContent = '—'; for (const id of ['versions','categories','errors']) $(id).replaceChildren(); displayedErrors = []; $('errorCount').textContent = ''; }
function table(id, rows, columns, emptyMessage = 'Henüz veri yok') {
  const body = $(id); body.replaceChildren();
  if (!rows.length) rows = [Array.from({length:columns}, (_,i) => i ? '—' : emptyMessage)];
  for (const row of rows) { const tr = document.createElement('tr'); for (const value of row) { const td = document.createElement('td'); td.textContent = String(value); tr.append(td); } body.append(tr); }
}
function errorFilter(id, values, label) {
  const select = $(id), selected = select.value;
  select.replaceChildren(Object.assign(document.createElement('option'), {value:'', textContent:label}));
  for (const value of values) select.append(Object.assign(document.createElement('option'), {value, textContent:value}));
  select.value = values.includes(selected) ? selected : '';
}
function renderErrors() {
  const category = $('errorCategory').value, version = $('errorVersion').value;
  const matches = displayedErrors.filter(item => (!category || item.label === category) && (!version || item.version === version));
  table('errors', matches.map(item => [item.label, item.version, new Date(item.lastSeen).toLocaleString('tr-TR'), new Intl.NumberFormat('tr-TR').format(item.count)]), 4, displayedErrors.length ? 'Bu filtrelere uyan bildirim yok' : 'Henüz hata bildirilmedi');
  $('errorCount').textContent = `${matches.length} / ${displayedErrors.length} kayıt gösteriliyor`;
  $('resetErrors').disabled = !category && !version;
}
$('errorCategory').onchange = renderErrors;
$('errorVersion').onchange = renderErrors;
$('resetErrors').onclick = () => { $('errorCategory').value = ''; $('errorVersion').value = ''; renderErrors(); };
function externalLink(url, label) {
  const link = document.createElement('a'); link.href = url; link.textContent = label;
  link.target = '_blank'; link.rel = 'noopener noreferrer'; return link;
}
async function loadLauncherDetails() {
  const panel = $('releaseDetails');
  try {
    const response = await fetch('https://api.github.com/repos/gamer3434/MistikLauncherUltra/releases/latest', {headers:{Accept:'application/vnd.github+json'}, cache:'no-store'});
    if (!response.ok) throw new Error('release');
    const release = await response.json();
    const published = Date.parse(release.published_at);
    const downloads = Array.isArray(release.assets) ? release.assets.reduce((sum, asset) => sum + (Number.isSafeInteger(asset.download_count) ? asset.download_count : 0), 0) : 0;
    const version = typeof release.tag_name === 'string' && /^v?\d+\.\d+\.\d+$/.test(release.tag_name) ? release.tag_name.replace(/^v/, '') : 'Bilinmiyor';
    panel.replaceChildren();
    const versionLine = document.createElement('strong'); versionLine.className = 'release-version'; versionLine.textContent = 'v' + version; panel.append(versionLine);
    const facts = document.createElement('div'); facts.className = 'release-facts';
    for (const value of [Number.isFinite(published) ? new Date(published).toLocaleDateString('tr-TR') : 'Tarih yok', new Intl.NumberFormat('tr-TR').format(downloads) + ' indirme']) {
      const item = document.createElement('span'); item.textContent = value; facts.append(item);
    }
    panel.append(facts);
    if (typeof release.html_url === 'string' && new URL(release.html_url).origin === 'https://github.com') panel.append(externalLink(release.html_url, 'Sürüm notlarını aç ↗'));
  } catch { panel.replaceChildren(Object.assign(document.createElement('p'), {className:'empty-state', textContent:'Sürüm bilgisi şu anda alınamadı.'})); }
}
async function loadCatalogMods() {
  const panel = $('catalogMods');
  try {
    const params = new URLSearchParams({limit:'6', index:'downloads', facets:JSON.stringify([['project_type:mod']])});
    const response = await fetch('https://api.modrinth.com/v2/search?' + params, {headers:{Accept:'application/json'}, cache:'no-store'});
    if (!response.ok) throw new Error('catalog');
    const result = await response.json(); panel.replaceChildren();
    for (const mod of (Array.isArray(result.hits) ? result.hits : []).slice(0, 6)) {
      if (typeof mod.slug !== 'string' || !/^[\w-]{1,100}$/.test(mod.slug)) continue;
      const item = document.createElement('article'); item.className = 'mod-item';
      const title = externalLink('https://modrinth.com/mod/' + encodeURIComponent(mod.slug), typeof mod.title === 'string' ? mod.title : 'Mod'); title.className = 'mod-title';
      const description = document.createElement('p'); description.textContent = typeof mod.description === 'string' ? mod.description : '';
      const meta = document.createElement('span'); meta.className = 'mod-downloads'; meta.textContent = '↓ ' + new Intl.NumberFormat('tr-TR').format(Number.isSafeInteger(mod.downloads) ? mod.downloads : 0);
      item.append(title, description, meta); panel.append(item);
    }
    if (!panel.childElementCount) throw new Error('empty catalog');
  } catch { panel.replaceChildren(Object.assign(document.createElement('p'), {className:'empty-state', textContent:'Mod kataloğu şu anda alınamadı.'})); }
}
function render(data, now, receivedAt = now) {
  const result = summarize(data, now);
  for (const id of ['installations','active','daily','recentErrors']) $(id).textContent = new Intl.NumberFormat('tr-TR').format(result[id]);
  table('versions', result.versions, 2); table('categories', result.categories, 2);
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
  let unsubscribe = () => {}, connectedStop = () => {}, offsetStop = () => {}, timer, data, receivedAt, connected = false, offset = 0;
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
    unsubscribe(); connectedStop(); offsetStop(); clearInterval(timer); data = undefined; connected = false; clear();
    $('login').hidden = !!user; $('logout').hidden = !user;
    if (!user) { status(redirectError ? 'Google girişi tamamlanamadı. (' + redirectError + ')' : 'Bu panel yalnızca yetkili Google hesabına açıktır.', !!redirectError); return; }
    status('Yetki ve Firebase verileri kontrol ediliyor…');
    void loadLauncherDetails(); void loadCatalogMods();
    const update = () => { if (connected && data !== undefined) { render(data, Date.now() + offset, receivedAt); status('Bağlı · Canlı veriler; aktiflik her 30 saniyede yenilenir.'); } };
    connectedStop = dbSdk.onValue(dbSdk.ref(database, '.info/connected'), snapshot => {
      connected = snapshot.val() === true;
      if (!connected) { clear(); status('Bağlantı bekleniyor; mevcut sayılar doğrulanamıyor.', true); } else update();
    });
    offsetStop = dbSdk.onValue(dbSdk.ref(database, '.info/serverTimeOffset'), snapshot => { offset = Number(snapshot.val()) || 0; update(); });
    unsubscribe = dbSdk.onValue(dbSdk.ref(database, 'launcherMetrics'), snapshot => { data = snapshot.val() || {}; receivedAt = Date.now() + offset; update(); }, () => {
      data = undefined; clear(); status('Veriler okunamadı. Hesabın yönetici yetkisini ve veritabanı kurallarını kontrol edin.', true);
    });
    timer = setInterval(update, 30000);
  }, () => { clear(); status('Google oturumu doğrulanamadı.', true); });
} catch {
  clear(); $('login').disabled = true;
  status('Panel yapılandırılmamış veya Firebase bağlantısı kurulamadı. Kurulum bilgilerini ve ağı kontrol edin.', true);
}
