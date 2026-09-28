'use strict';

const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const test = require('node:test');
const { parseRelease } = require('../site/assets/release.js');

const repo = 'gamer3434/MistikLauncherUltra';
const names = {
  online: 'MistikSetup-Online-6.2.0.exe',
  offline: 'MistikSetup-Offline-6.2.0.exe',
  portable: 'MistikLauncher-6.2.0-win-x64.zip'
};

function release(overrides = {}) {
  const tag = 'v6.2.0';
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
  assert.deepEqual(Object.keys(parsed.assets).sort(), ['offline', 'online', 'portable']);
  assert.equal(parsed.assets.online.hash, '1'.repeat(64));
  assert.equal(parsed.notes.tr[0], 'Güvenli <img src=x onerror=alert(1)> güncelleme');
  assert.equal(parsed.notes.en[0], 'Safe verified update');
});

test('rejects prereleases, unofficial links, and incomplete assets', () => {
  assert.equal(parseRelease(release({ prerelease: true })), null);
  assert.equal(parseRelease(release({ html_url: 'https://example.com/fake' })), null);
  assert.equal(parseRelease(release({ assets: release().assets.slice(1) })), null);
});

test('keeps a working v6.1.1 fallback and matching published digests', () => {
  const home = readFileSync(require.resolve('../site/index.html'), 'utf8');
  const download = readFileSync(require.resolve('../site/indir/index.html'), 'utf8');
  assert.match(home, /data-release-version>v6\.1\.1/);
  for (const [kind, name] of Object.entries({
    online: 'MistikSetup-Online-6.1.1.exe',
    offline: 'MistikSetup-Offline-6.1.1.exe',
    portable: 'MistikLauncher-6.1.1-win-x64.zip'
  })) {
    assert.match(download, new RegExp(`data-release-asset="${kind}" href="https://github\\.com/${repo}/releases/download/v6\\.1\\.1/${name.replaceAll('.', '\\.')}"`));
    const row = new RegExp(`data-release-hash="${kind}">[\\s\\S]*?<code>([a-f0-9]{64})</code>[\\s\\S]*?data-copy-hash="([a-f0-9]{64})"`).exec(download);
    assert.ok(row, `${kind} fallback hash exists`);
    assert.equal(row[1], {
      online: '03ba2d8f0ce08f4c4362a06101d7ec8a566559409ec3ea5f0f23d4a08ecd5a71',
      offline: '9bc664e2053e899b4ab317897501e222b80ba5ef7160dc0fb922052b2c831292',
      portable: '033fae9084eb4248682bcb25c2ae32f1fec029f3a36b97e9cbfbffd5e7f3f51b'
    }[kind]);
    assert.equal(row[1], row[2]);
  }
});
