(function () {
  'use strict';

  const repo = 'gamer3434/MistikLauncherUltra';
  const minimumVersion = [6, 1, 3];
  const fallbackVersion = '6.1.3';
  const fallbackNotes = {
    tr: [
      'Launcher girişinden önce e-posta doğrulama bağlantısını aç.',
      'Doğrulanmamış hesaplar launcher ayar yedeğine erişemez.',
      'Yedek yalnızca temel launcher ayarlarını içerir; oyun bilgileri ve parolalar gönderilmez.'
    ],
    en: [
      'Open the email verification link before signing in to the launcher.',
      'Unverified accounts cannot access launcher settings backup.',
      'Backup includes only basic launcher settings; game credentials and passwords stay private.'
    ]
  };

  function releaseNotes(body, language) {
    const heading = language === 'tr' ? 'Türkçe' : 'English';
    const lines = String(body || '').split(/\r?\n/);
    let inside = false;
    const notes = [];
    for (const line of lines) {
      const section = /^##\s+(.+?)\s*$/.exec(line);
      if (section) { inside = section[1] === heading; continue; }
      if (!inside) continue;
      const bullet = /^\s*-\s+(.+)$/.exec(line);
      if (bullet) notes.push(bullet[1].replace(/\[([^\]]+)\]\([^)]*\)/g, '$1').replace(/\*\*|`/g, '').trim().slice(0, 220));
      if (notes.length === 5) break;
    }
    return notes;
  }

  function parseRelease(release) {
    if (!release || release.draft !== false || release.prerelease !== false) return null;
    const match = /^v(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$/.exec(release.tag_name || '');
    if (!match || match.slice(1).some(part => part.length > 16)) return null;
    const components = match.slice(1).map(Number);
    if (!components.every(Number.isSafeInteger)) return null;
    if (components.some((part, i) => part < minimumVersion[i] && components.slice(0, i).every((prior, j) => prior === minimumVersion[j]))) return null;
    const version = match.slice(1).join('.');
    const tag = release.tag_name;
    const releaseUrl = `https://github.com/${repo}/releases/tag/${tag}`;
    if (release.html_url !== releaseUrl) return null;
    const names = {
      online: `MistikSetup-Online-${version}.exe`,
      offline: `MistikSetup-Offline-${version}.exe`,
      portable: `MistikLauncher-${version}-win-x64.zip`
    };
    const assets = {};
    for (const [kind, name] of Object.entries(names)) {
      const asset = Array.isArray(release.assets) && release.assets.find(item => item.name === name);
      const url = `https://github.com/${repo}/releases/download/${tag}/${name}`;
      if (!asset || asset.browser_download_url !== url) return null;
      assets[kind] = { url, hash: /^sha256:[a-f0-9]{64}$/i.test(asset.digest || '') ? asset.digest.slice(7).toLowerCase() : null };
    }
    return { version, releaseUrl, assets, notes: { tr: releaseNotes(release.body, 'tr'), en: releaseNotes(release.body, 'en') } };
  }

  if (typeof module !== 'undefined') module.exports = { parseRelease, releaseNotes };
  if (typeof document === 'undefined') return;

  let current = null;
  function render() {
    const language = document.documentElement.lang === 'en' ? 'en' : 'tr';
    const version = current ? current.version : fallbackVersion;
    const notes = current && current.notes[language].length ? current.notes[language] : fallbackNotes[language];
    const label = document.querySelector('[data-release-version-label]');
    if (label) label.textContent = `v${version} · Windows x64`;
    document.querySelectorAll('[data-release-version]').forEach(node => { node.textContent = `v${version}`; });
    document.querySelectorAll('[data-release-version-plain]').forEach(node => { node.textContent = version; });
    document.querySelectorAll('[data-release-asset]').forEach(link => {
      if (current) link.href = current.assets[link.dataset.releaseAsset].url;
    });

    const card = document.querySelector('[data-release-announcements]');
    if (card) {
      document.querySelector('[data-release-kicker]').textContent = language === 'tr'
        ? (current ? 'EN YENİ KARARLI SÜRÜM' : 'SON DOĞRULANAN SÜRÜM')
        : (current ? 'LATEST STABLE RELEASE' : 'LAST VERIFIED RELEASE');
      document.querySelector('[data-release-heading]').textContent = language === 'tr' ? "Launcher'dan haberler" : 'Launcher news';
      card.replaceChildren(...notes.slice(0, 3).map(note => {
        const item = document.createElement('li');
        item.textContent = note;
        return item;
      }));
      const link = document.querySelector('[data-release-link]');
      link.textContent = language === 'tr' ? 'Sürüm notları ↗' : 'Release notes ↗';
      if (current) link.href = current.releaseUrl;
    }

    const summary = document.querySelector('[data-release-summary]');
    if (!summary || !current) return;
    const hashes = document.querySelector('[data-release-hashes]');
    const hasHashes = Object.values(current.assets).every(asset => asset.hash);
    hashes.hidden = !hasHashes;
    if (hasHashes) document.querySelectorAll('[data-release-hash]').forEach(row => {
      const hash = current.assets[row.dataset.releaseHash].hash;
      row.querySelector('code').textContent = hash;
      row.querySelector('[data-copy-hash]').dataset.copyHash = hash;
    });
    if (version === fallbackVersion) return;
    summary.textContent = language === 'tr' ? `${version} yenilikleri${hasHashes ? ' ve SHA-256 değerleri' : ''}` : `Version ${version} notes${hasHashes ? ' and SHA-256 hashes' : ''}`;
    document.querySelector('[data-release-title]').textContent = language === 'tr' ? `${version} yenilikleri` : `What's new in ${version}`;
    document.querySelector('[data-release-intro]').textContent = language === 'tr' ? 'En yeni kararlı sürümün duyuruları:' : 'Announcements for the latest stable release:';
    const list = document.querySelector('[data-release-notes]');
    list.replaceChildren(...notes.map(note => {
      const item = document.createElement('li');
      item.textContent = note;
      return item;
    }));
  }

  document.addEventListener('mistik:languagechange', render);
  render();
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), 8000);
  fetch(`https://api.github.com/repos/${repo}/releases/latest`, {
    headers: { Accept: 'application/vnd.github+json' }, signal: controller.signal
  }).then(response => {
    if (!response.ok) throw new Error('release unavailable');
    return response.json();
  }).then(release => {
    current = parseRelease(release);
    if (current) render();
  }).catch(() => { /* The verified 6.1.3 fallback remains usable offline. */ })
    .finally(() => clearTimeout(timeout));
}());
