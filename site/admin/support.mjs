const plain = value => value && typeof value === 'object' && !Array.isArray(value);
const text = (value, max) => typeof value === 'string' && value.trim().length > 0 && value.length <= max && !/[\u0000-\u001f\u007f]/.test(value);
export const validDeviceId = value => typeof value === 'string' && /^[A-Za-z0-9_-]{1,128}$/.test(value);
export const validProjectId = value => typeof value === 'string' && /^[A-Za-z0-9]{8}$/.test(value);
export function readDevices(data, now) {
  if (!plain(data) || !Number.isSafeInteger(now)) return [];
  return Object.entries(data).flatMap(([uid, item]) => {
    if (!validDeviceId(uid) || !plain(item) || !text(item.username, 32) || !text(item.profile, 160) || !/^\d{1,3}\.\d{1,3}\.\d{1,3}$/.test(item.version) ||
        !Number.isSafeInteger(item.lastSeen) || item.lastSeen < 0 || item.lastSeen > now + 60000 ||
        !Number.isSafeInteger(item.modCount) || item.modCount < 0 || item.modCount > 100000 ||
        typeof item.truncated !== 'boolean' || typeof item.allowRequests !== 'boolean') return [];
    const source = item.mods ?? [];
    if (!Array.isArray(source) || source.length > 300 || item.modCount < source.length || (!item.truncated && item.modCount !== source.length)) return [];
    const mods = [];
    for (const mod of source) {
      if (!plain(mod) || !text(mod.name, 180) || /[\\/:]/.test(mod.name) || !/\.jar(?:\.disabled)?$/i.test(mod.name) || typeof mod.enabled !== 'boolean') return [];
      mods.push({name:mod.name, enabled:mod.enabled});
    }
    return [{uid, username:item.username, profile:item.profile, version:item.version, lastSeen:item.lastSeen,
      modCount:item.modCount, truncated:item.truncated, allowRequests:item.allowRequests, mods,
      active:item.lastSeen >= now - 5 * 60000}];
  }).sort((a, b) => b.lastSeen - a.lastSeen).slice(0, 100);
}
export function searchProjects(data) {
  if (!plain(data) || !Array.isArray(data.hits)) return [];
  const seen = new Set();
  return data.hits.slice(0, 10).flatMap(item => {
    if (!plain(item) || item.project_type !== 'mod' || !validProjectId(item.project_id) || !text(item.title, 120) || seen.has(item.project_id)) return [];
    seen.add(item.project_id);
    return [{projectId:item.project_id, projectName:item.title}];
  });
}
export function requestPayload(device, project, id, createdAt) {
  if (!device?.active || device.allowRequests !== true || !validDeviceId(device.uid) || !text(device.profile, 160) ||
      !validProjectId(project?.projectId) || !text(project?.projectName, 120) || !/^[a-f0-9]{32}$/.test(id)) throw new Error('invalid target');
  return {id, projectId:project.projectId, projectName:project.projectName, profile:device.profile, createdAt};
}
export function mountSupport({database, sdk, now}) {
  const $ = id => document.getElementById(id);
  let stopped = false, connected = false, raw, selected, project, pending, requestLoaded = false, submitting = false;
  let searchAbort, searchGeneration = 0, detailGeneration = 0, rows = [], stopRequest = () => {};
  const message = value => { $('supportStatus').textContent = value; };
  const node = (tag, value, className) => Object.assign(document.createElement(tag), {textContent:value, ...(className ? {className} : {})});
  function cancelSearch() { searchGeneration++; searchAbort?.abort(); searchAbort = undefined; project = undefined; $('supportSearchResults').replaceChildren(); }
  function actions() {
    const device = rows.find(item => item.uid === selected);
    $('supportSend').disabled = !connected || !device?.active || !device.allowRequests || !project || !requestLoaded || pending !== null || submitting;
    $('supportSearchButton').disabled = !connected || !device?.active || !device.allowRequests || submitting;
  }
  function details() {
    const device = rows.find(item => item.uid === selected);
    $('supportDetail').hidden = !device;
    if (!device) { actions(); return; }
    $('supportName').textContent = device.username;
    $('supportFacts').textContent = `Launcher ${device.version} · ${device.profile} · ${device.active && connected ? 'Son 5 dk aktif' : 'Güncel etkinlik doğrulanamadı'}`;
    const enabled = device.mods.filter(mod => mod.enabled).length;
    $('supportModCount').textContent = `${device.mods.length} / ${device.modCount} mod dosyası · Gösterilenlerde ${enabled} açık, ${device.mods.length - enabled} kapalı${device.truncated ? ' · Liste ilk 300 dosyayla sınırlı; açık/kapalı sayıları yalnızca gösterilen dosyaları kapsar' : ''}. Mod paketi kimliği bu dosyalardan belirlenemez.`;
    $('supportHeartbeat').textContent = `Son launcher bildirimi: ${new Date(device.lastSeen).toLocaleString('tr-TR')} · Mod önerileri: ${device.allowRequests ? 'Açık' : 'Kapalı'}. Otomatik kurulum izni bu kayıttan bilinmiyor.`;
    $('supportModList').replaceChildren(...device.mods.map(mod => node('li', `${mod.name} · ${mod.enabled ? 'Açık' : 'Kapalı'}`)));
    if (!device.mods.length) $('supportModList').append(node('li', 'Bu profilde mod dosyası bildirilmemiş.'));
    $('supportRequestStatus').textContent = !connected ? 'Çevrimdışı; istek gönderilemez.' : !device.active ? 'Kurulumun güncel etkinlik sinyali yok.' : !device.allowRequests ? 'Bu kurulum mod önerilerini kabul etmiyor.' : !requestLoaded ? 'İstek durumu kontrol ediliyor…' : pending !== null ? 'Bekleyen kurulum isteği var. Launcher’ın işlemesi bekleniyor; üzerine yazılmaz.' : 'Yeni istek gönderilebilir. Kullanıcının verdiği izinlere göre otomatik kurulur veya launcher onayı beklenir.';
    actions();
  }
  function render() {
    if (stopped) return;
    rows = readDevices(raw, Math.floor(now()));
    $('supportCount').textContent = `${connected ? rows.filter(item => item.active).length : '—'} aktif · ${rows.length} paylaşan kurulum (en yeni 100)`;
    const body = $('supportRows'); body.replaceChildren();
    for (const device of rows) {
      const tr = document.createElement('tr'), td = document.createElement('td');
      const button = node('button', device.username, 'support-user'); button.type = 'button'; button.setAttribute('aria-pressed', String(selected === device.uid));
      button.onclick = () => select(device.uid); td.append(button); tr.append(td);
      for (const value of [device.active && connected ? 'Son 5 dk aktif' : 'Etkin sinyal yok', device.version, device.modCount]) tr.append(node('td', String(value)));
      body.append(tr);
    }
    if (!rows.length) { const tr = document.createElement('tr'), td = node('td', 'Ayrı destek paylaşımını açmış kurulum henüz yok.'); td.colSpan = 4; tr.append(td); body.append(tr); }
    if (selected && !rows.some(item => item.uid === selected)) { selected = undefined; detailGeneration++; stopRequest(); cancelSearch(); }
    details();
  }
  function select(uid) {
    detailGeneration++; stopRequest(); cancelSearch(); selected = uid; pending = undefined; requestLoaded = false;
    $('supportSearch').value = ''; message('');
    const current = detailGeneration;
    stopRequest = sdk.onValue(sdk.ref(database, `launcherSupport/requests/${uid}`), snapshot => {
      if (stopped || current !== detailGeneration) return;
      pending = snapshot.val(); requestLoaded = true; details();
    }, () => { if (stopped || current !== detailGeneration) return; requestLoaded = false; message('Bu kurulumun istekleri okunamadı. Yönetici yetkisini kontrol et.'); actions(); });
    render();
  }
  $('supportSearchForm').onsubmit = async event => {
    event.preventDefault(); cancelSearch(); actions();
    const device = rows.find(item => item.uid === selected), query = $('supportSearch').value.trim();
    if (!connected || !device?.active || !device.allowRequests || !query || query.length > 100) return;
    const current = searchGeneration, controller = searchAbort = new AbortController();
    const timeout = setTimeout(() => controller.abort(), 10000); message('Modrinth aranıyor…');
    try {
      const params = new URLSearchParams({query, limit:'10', facets:JSON.stringify([['project_type:mod']])});
      const response = await fetch('https://api.modrinth.com/v2/search?' + params, {signal:controller.signal, headers:{Accept:'application/json'}});
      if (!response.ok) throw new Error('search');
      const reader = response.body.getReader(); let bytes = 0, chunks = [];
      while (true) { const next = await reader.read(); if (next.done) break; bytes += next.value.byteLength; if (bytes > 262144) { await reader.cancel(); throw new Error('size'); } chunks.push(next.value); }
      const buffer = new Uint8Array(bytes); let offset = 0; for (const chunk of chunks) { buffer.set(chunk, offset); offset += chunk.length; }
      const projects = searchProjects(JSON.parse(new TextDecoder().decode(buffer)));
      if (stopped || current !== searchGeneration) return;
      for (const item of projects) {
        const button = node('button', item.projectName, 'button button-quiet'); button.type = 'button';
        button.onclick = () => { project = item; for (const sibling of $('supportSearchResults').children) sibling.setAttribute('aria-pressed', String(sibling === button)); message(`${item.projectName} seçildi. Hedef profille uyum launcher tarafından denetlenir.`); actions(); };
        button.setAttribute('aria-pressed', 'false'); $('supportSearchResults').append(button);
      }
      message(projects.length ? 'Göndermek istediğin modu seç.' : 'Eşleşen mod bulunamadı.');
    } catch { if (!stopped && current === searchGeneration) message('Arama tamamlanamadı. Bağlantını kontrol edip yeniden dene.'); }
    finally { clearTimeout(timeout); }
  };
  $('supportSend').onclick = async () => {
    render(); if ($('supportSend').disabled) return;
    const device = rows.find(item => item.uid === selected), current = detailGeneration;
    const id = [...crypto.getRandomValues(new Uint8Array(16))].map(value => value.toString(16).padStart(2, '0')).join('');
    const payload = requestPayload(device, project, id, sdk.serverTimestamp());
    submitting = true; actions();
    try {
      const result = await sdk.runTransaction(sdk.ref(database, `launcherSupport/requests/${device.uid}`), value => value === null ? payload : undefined, {applyLocally:false});
      if (stopped || current !== detailGeneration) return;
      message(result.committed ? 'İstek gönderildi. Kullanıcının verdiği izinlere göre otomatik kurulur veya launcher onayı beklenir. Uyumluluk launcher’da kontrol edilir.' : 'Zaten bekleyen bir istek var; yeni istek gönderilmedi.');
    } catch { if (!stopped && current === detailGeneration) message('İstek gönderilemedi. Yetkini, bağlantıyı ve bekleyen isteği kontrol et.'); }
    finally { submitting = false; if (!stopped) details(); }
  };
  $('supportClose').onclick = () => { selected = undefined; detailGeneration++; stopRequest(); cancelSearch(); render(); };
  const stopDevices = sdk.onValue(sdk.query(sdk.ref(database, 'launcherSupport/devices'), sdk.orderByChild('lastSeen'), sdk.limitToLast(100)), snapshot => { if (stopped) return; raw = snapshot.val(); message(''); render(); }, () => { if (stopped) return; raw = undefined; render(); message('Destek verileri okunamadı. Yönetici yetkisini ve veritabanı kurallarını kontrol et.'); });
  const timer = setInterval(render, 30000);
  return {connection(value) { connected = value; render(); }, refresh:render, stop() {
    stopped = true; stopDevices(); stopRequest(); clearInterval(timer); cancelSearch();
    raw = selected = project = pending = undefined; rows = [];
    $('supportRows').replaceChildren(); $('supportDetail').hidden = true; $('supportModList').replaceChildren();
    $('supportName').textContent = ''; $('supportFacts').textContent = ''; $('supportCount').textContent = ''; message('');
    $('supportSearch').value = ''; $('supportModCount').textContent = ''; $('supportHeartbeat').textContent = ''; $('supportRequestStatus').textContent = '';
    $('supportSearchForm').onsubmit = $('supportSend').onclick = $('supportClose').onclick = null;
  }};
}
