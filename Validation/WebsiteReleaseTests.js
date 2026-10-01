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

test('keeps a working v6.1.5 fallback and matching published digests', () => {
  const home = readFileSync(require.resolve('../site/index.html'), 'utf8');
  const download = readFileSync(require.resolve('../site/indir/index.html'), 'utf8');
  const releaseScript = readFileSync(require.resolve('../site/assets/release.js'), 'utf8');
  assert.match(releaseScript, /fallbackVersion = '6\.1\.5'/);
  assert.match(home, /data-release-version>v6\.1\.5/);
  for (const [kind, name] of Object.entries({
    online: 'MistikSetup-Online-6.1.5.exe',
    offline: 'MistikSetup-Offline-6.1.5.exe',
    portable: 'MistikLauncher-6.1.5-win-x64.zip'
  })) {
    assert.match(download, new RegExp(`data-release-asset="${kind}" href="https://github\\.com/${repo}/releases/download/v6\\.1\\.5/${name.replaceAll('.', '\\.')}"`));
    const row = new RegExp(`data-release-hash="${kind}">[\\s\\S]*?<code>([a-f0-9]{64})</code>[\\s\\S]*?data-copy-hash="([a-f0-9]{64})"`).exec(download);
    assert.ok(row, `${kind} fallback hash exists`);
    assert.equal(row[1], {
      online: 'ec2f62878ae63086a6f6d0f524b4db25cc55f7e869d87ef302f0d48d632bacf4',
      offline: 'fc686ab0d91045b0acafb387cd1427de62c3d759352c8afd47b19fd7cd36ccf1',
      portable: 'ad86ea9a1f1488c776b377bd187511bcdfce34c294f37f83935401baa9b0f139'
    }[kind]);
    assert.equal(row[1], row[2]);
  }
});

test('preview sample labels translate and decorative switches stay silent', () => {
  const preview = readFileSync(require.resolve('../site/onizleme/index.html'), 'utf8');
  const script = readFileSync(require.resolve('../site/assets/site.js'), 'utf8');
  const context = { window: {} };
  runInNewContext(script.slice(0, script.indexOf('let lang=')), context);
  assert.equal(context.window.mistikWords.en['screen.versions.sampleProfile'], 'Modded world');
  assert.equal(context.window.mistikWords.en['screen.settings.currentLanguage'], 'English ⌄');
  assert.match(preview, /data-i18n="screen\.versions\.sampleProfile"/);
  assert.match(preview, /data-i18n="screen\.settings\.currentLanguage"/);
  assert.doesNotMatch(preview, /class="toggle-demo" aria-label=/);
});
