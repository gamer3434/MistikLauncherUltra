import { summarize } from './metrics.mjs';
const $ = id => document.getElementById(id);
function status(message, error = false) { $('status').textContent = message; $('status').dataset.error = String(error); }
function clear() { $('dashboard').hidden = true; for (const id of ['installations','active','daily','recentErrors']) $(id).textContent = '—'; for (const id of ['versions','categories','errors']) $(id).replaceChildren(); }
function table(id, rows, columns) {
  const body = $(id); body.replaceChildren();
  if (!rows.length) rows = [Array.from({length:columns}, (_,i) => i ? '—' : 'Henüz veri yok')];
  for (const row of rows) { const tr = document.createElement('tr'); for (const value of row) { const td = document.createElement('td'); td.textContent = String(value); tr.append(td); } body.append(tr); }
}
function render(data, now) {
  const result = summarize(data, now);
  for (const id of ['installations','active','daily','recentErrors']) $(id).textContent = new Intl.NumberFormat('tr-TR').format(result[id]);
  table('versions', result.versions, 2); table('categories', result.categories, 2);
  table('errors', result.errors.map(item => [item.label, item.version, new Date(item.lastSeen).toLocaleString('tr-TR'), item.count]), 4);
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
  let unsubscribe = () => {}, connectedStop = () => {}, offsetStop = () => {}, timer, data, connected = false, offset = 0;
  $('login').disabled = false;
  $('login').onclick = async () => {
    $('login').disabled = true;
    try { await authSdk.signInWithPopup(auth, new authSdk.GoogleAuthProvider()); }
    catch { clear(); status('Giriş tamamlanamadı. Açılır pencere iznini ve Google hesabını kontrol edin.', true); }
    finally { $('login').disabled = false; }
  };
  $('logout').onclick = async () => { await authSdk.signOut(auth); };
  authSdk.onAuthStateChanged(auth, user => {
    unsubscribe(); connectedStop(); offsetStop(); clearInterval(timer); data = undefined; connected = false; clear();
    $('login').hidden = !!user; $('logout').hidden = !user;
    if (!user) { status('Bu panel yalnızca yetkili Google hesabına açıktır.'); return; }
    status('Yetki ve Firebase verileri kontrol ediliyor…');
    const update = () => { if (connected && data !== undefined) { render(data, Date.now() + offset); status('Bağlı · Canlı veriler; aktiflik her 30 saniyede yenilenir.'); } };
    connectedStop = dbSdk.onValue(dbSdk.ref(database, '.info/connected'), snapshot => {
      connected = snapshot.val() === true;
      if (!connected) { clear(); status('Bağlantı bekleniyor; mevcut sayılar doğrulanamıyor.', true); } else update();
    });
    offsetStop = dbSdk.onValue(dbSdk.ref(database, '.info/serverTimeOffset'), snapshot => { offset = Number(snapshot.val()) || 0; update(); });
    unsubscribe = dbSdk.onValue(dbSdk.ref(database, 'launcherMetrics'), snapshot => { data = snapshot.val() || {}; update(); }, () => {
      data = undefined; clear(); status('Veriler okunamadı. Hesabın yönetici yetkisini ve veritabanı kurallarını kontrol edin.', true);
    });
    timer = setInterval(update, 30000);
  }, () => { clear(); status('Google oturumu doğrulanamadı.', true); });
} catch {
  clear(); $('login').disabled = true;
  status('Panel yapılandırılmamış veya Firebase bağlantısı kurulamadı. Kurulum bilgilerini ve ağı kontrol edin.', true);
}
