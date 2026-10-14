'use strict';

const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const test = require('node:test');
const { runInNewContext } = require('node:vm');
const { parseRelease } = require('../site/assets/release.js');

const repo = 'gamer3434/MistikLauncherUltra';

function release(overrides = {}) {
  const tag = overrides.tag_name || 'v6.2.0';
  const version = tag.slice(1);
  const names = {
    online: `MistikSetup-Online-${version}.exe`,
    offline: `MistikSetup-Offline-${version}.exe`,
    portable: `MistikLauncher-${version}-win-x64.zip`
  };
  return {
    tag_name: tag,
    html_url: `https://github.com/${repo}/releases/tag/${tag}`,
    draft: false,
    prerelease: false,
    body: '## Türkçe\n\n- Güvenli <img src=x onerror=alert(1)> güncelleme\n\n## English\n\n- Safe **verified** update',
    assets: Object.entries(names).map(([kind, name]) => ({
      name,
      browser_download_url: `https://github.com/${repo}/releases/download/${tag}/${name}`,
      digest: `sha256:${{ online: '1', offline: '2', portable: '3' }[kind].repeat(64)}`
    })),
    ...overrides
  };
}

test('accepts stable official release with exactly the three required assets and hashes', () => {
  const parsed = parseRelease(release());
  assert.equal(parsed.version, '6.2.0');
  assert.equal(parseRelease(release({ tag_name: 'v6.1.5' })).version, '6.1.5');
  assert.deepEqual(Object.keys(parsed.assets).sort(), ['offline', 'online', 'portable']);
  assert.equal(parsed.assets.online.hash, '1'.repeat(64));
  assert.equal(parsed.notes.tr[0], 'Güvenli <img src=x onerror=alert(1)> güncelleme');
  assert.equal(parsed.notes.en[0], 'Safe verified update');
});

test('rejects prereleases, unofficial links, and incomplete assets', () => {
  assert.equal(parseRelease(release({ prerelease: true })), null);
  assert.equal(parseRelease(release({ html_url: 'https://example.com/fake' })), null);
  assert.equal(parseRelease(release({ assets: release().assets.slice(1) })), null);
  assert.equal(parseRelease(release({ tag_name: 'v6.1.4' })), null);
});

test('rejects leading zeroes and numeric components outside JavaScript safe integers', () => {
  for (const tag of [
    'v06.2.0', 'v6.02.0', 'v6.2.00',
    'v9007199254740992.0.0', 'v6.9007199254740992.0', 'v6.2.9007199254740992',
    'v99999999999999999.0.0'
  ]) assert.equal(parseRelease(release({ tag_name: tag })), null, tag);
});

test('keeps a working v6.2.30 fallback and matching published digests', () => {
  const home = readFileSync(require.resolve('../site/index.html'), 'utf8');
  const download = readFileSync(require.resolve('../site/indir/index.html'), 'utf8');
  const releaseScript = readFileSync(require.resolve('../site/assets/release.js'), 'utf8');
  assert.match(releaseScript, /fallbackVersion = '6\.2\.30'/);
  assert.match(home, /data-release-version>v6\.2\.30/);
  for (const [kind, name] of Object.entries({
    online: 'MistikSetup-Online-6.2.30.exe',
    offline: 'MistikSetup-Offline-6.2.30.exe',
    portable: 'MistikLauncher-6.2.30-win-x64.zip'
  })) {
    assert.match(download, new RegExp(`data-release-asset="${kind}" href="https://github\\.com/${repo}/releases/download/v6\\.2\\.30/${name.replaceAll('.', '\\.')}"`));
    const row = new RegExp(`data-release-hash="${kind}">[\\s\\S]*?<code>([a-f0-9]{64})</code>[\\s\\S]*?data-copy-hash="([a-f0-9]{64})"`).exec(download);
    assert.ok(row, `${kind} fallback hash exists`);
    assert.equal(row[1], {
      online: 'e0bf9d791ddf154df659143e85208caf02b3cd6749d5175af368ff99f89a8099',
      offline: 'c97af54c339c85df86529215949d40f238896da6ec8fb3dd7235f44e1f7af09e',
      portable: 'b89283931440e8a121fb30e72f8a7e0c1ef168da4a131876d0b1444dc25bd32f'
    }[kind]);
    assert.equal(row[1], row[2]);
  }
});

test('real client preview has translated descriptions, matching panels and local images', () => {
  const preview = readFileSync(require.resolve('../site/onizleme/index.html'), 'utf8');
  const script = readFileSync(require.resolve('../site/assets/site.js'), 'utf8');
  const context = { window: {} };
  runInNewContext(script.slice(0, script.indexOf('let lang=')), context);
  assert.equal(context.window.mistikWords.en['demo.home.alt'], 'Mistik Launcher home: profile, version and launch readiness.');
  assert.match(context.window.mistikWords.en['demo.sectionIntro'], /sample profile and mod data/);
  for (const screen of ['home', 'mods']) {
    assert.match(preview, new RegExp(`id="tab-${screen}"[^>]+aria-controls="panel-${screen}"`));
    assert.match(preview, new RegExp(`id="panel-${screen}"[^>]+aria-labelledby="tab-${screen}"`));
  }
  for (const image of ['client-home-tr.png','client-home-en.png','client-mods.png']) {
    assert.ok(readFileSync(require.resolve(`../site/assets/${image}`)).length > 1000);
  }
  assert.doesNotMatch(preview, /tab-updates|panel-updates|6\.1\.5/);
});

test('public pages have complete TR/EN labels and do not advertise a removed update menu', () => {
  const script = readFileSync(require.resolve('../site/assets/site.js'), 'utf8');
  const context = { window: {} };
  runInNewContext(script.slice(0, script.indexOf('let lang=')), context);
  for (const page of ['index.html','indir/index.html','ozellikler/index.html','onizleme/index.html']) {
    const html = readFileSync(require.resolve(`../site/${page}`), 'utf8');
    for (const [,key] of html.matchAll(/data-i18n(?:-alt|-aria)?="([^"]+)"/g)) {
      for (const language of ['tr','en']) assert.ok(context.window.mistikWords[language][key], `${page}: ${language} ${key}`);
    }
    assert.doesNotMatch(html, /Güncellemeler menüsünden|6\.1\.5/);
  }
  assert.match(context.window.mistikWords.en['feature.versions.copy'], /Fabric, Forge, NeoForge and Quilt/);
  assert.match(context.window.mistikWords.en['feature.updates.copy'], /Settings/);
});

test('preview switching keeps pressed states, visible panels and translated screenshot in sync', () => {
  const element = dataset => ({
    dataset, attributes: {}, listeners: {},
    addEventListener(name, handler) { this.listeners[name] = handler; },
    setAttribute(name, value) { this.attributes[name] = value; }
  });
  const language = element({}), theme = element({}), year = element({});
  const tabs = ['home','mods'].map(screen => element({screen}));
  const panels = ['home','mods'].map(panel => element({panel}));
  const image = element({srcTr:'/assets/client-home-tr.png',srcEn:'/assets/client-home-en.png',i18nAlt:'demo.home.alt'});
  const previewMenu = element({i18nAria:'demo.screens'});
  const nodes = {
    '[data-screen]':tabs, '[data-panel]':panels, '[data-preview-image]':[image],
    '[data-i18n-alt]':[image], '[data-i18n-aria]':[previewMenu]
  };
  const document = {
    documentElement: element({}), body: element({}),
    getElementById(id) { return {language,theme,year}[id]; },
    querySelectorAll(selector) { return nodes[selector] || []; },
    querySelector() { return element({}); },
    dispatchEvent() {}
  };
  document.documentElement.removeAttribute = () => {};
  runInNewContext(readFileSync(require.resolve('../site/assets/site.js'), 'utf8'), {
    window:{}, document, Event:class {},
    localStorage:{getItem(){return null;},setItem(){}},
    getComputedStyle(){return {getPropertyValue(){return '#111315';}};},
    matchMedia(){return {addEventListener(){}};}
  });
  tabs[1].listeners.click();
  assert.equal(panels[0].hidden,true);
  assert.equal(panels[1].hidden,false);
  assert.equal(tabs[0].attributes['aria-pressed'],'false');
  assert.equal(tabs[1].attributes['aria-pressed'],'true');
  language.listeners.click();
  assert.equal(document.documentElement.lang,'en');
  assert.equal(image.src,'/assets/client-home-en.png');
  assert.equal(image.alt,'Mistik Launcher home: profile, version and launch readiness.');
  assert.equal(previewMenu.attributes['aria-label'],'Launcher screens');
  tabs[0].listeners.click();
  assert.equal(panels[0].hidden,false);
  assert.equal(panels[1].hidden,true);
});

test('public guides explain compatibility, backup scope, privacy consent and real installation paths', () => {
  const script = readFileSync(require.resolve('../site/assets/site.js'), 'utf8');
  const context = {window:{}};
  runInNewContext(script.slice(0,script.indexOf('let lang=')),context);
  const words = context.window.mistikWords.en;
  assert.match(words['workflow.backup.copy'], /does not overwrite existing mods/);
  assert.match(words['workflow.safety'], /does not include worlds/);
  assert.match(words['workflow.migrate.copy'], /cannot convert mods/);
  assert.match(words['privacy.metrics.copy'], /off by default/);
  assert.match(words['privacy.metrics.copy'], /mod lists are not sent/);
  assert.match(words['faq.account.a'], /Microsoft sign-in is not supported/);
  assert.match(words['maintenance.update.copy'], /in Settings/);
  assert.match(words['maintenance.repair.copy'], /run the installer and choose Repair/);
  const features = readFileSync(require.resolve('../site/ozellikler/index.html'),'utf8');
  for (const loader of ['Vanilla','Fabric','Forge','NeoForge','Quilt']) {
    assert.ok(features.includes(`<th scope="row">${loader}</th>`),loader);
  }
  const home = readFileSync(require.resolve('../site/index.html'),'utf8');
  assert.equal((home.match(/class="faq-item"/g)||[]).length,6);
  for (const section of ['baslangic','gizlilik','sorular']) assert.ok(home.includes(`id="${section}"`));
  const downloads = readFileSync(require.resolve('../site/indir/index.html'),'utf8');
  assert.match(downloads,/class="package-guide"/);
  assert.match(downloads,/id="guncelleme"/);
});
