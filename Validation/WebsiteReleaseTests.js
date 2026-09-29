'use strict';

const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const test = require('node:test');
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
  assert.equal(parseRelease(release({ tag_name: 'v6.1.3' })).version, '6.1.3');
  assert.deepEqual(Object.keys(parsed.assets).sort(), ['offline', 'online', 'portable']);
  assert.equal(parsed.assets.online.hash, '1'.repeat(64));
  assert.equal(parsed.notes.tr[0], 'Güvenli <img src=x onerror=alert(1)> güncelleme');
  assert.equal(parsed.notes.en[0], 'Safe verified update');
});

test('rejects prereleases, unofficial links, and incomplete assets', () => {
  assert.equal(parseRelease(release({ prerelease: true })), null);
  assert.equal(parseRelease(release({ html_url: 'https://example.com/fake' })), null);
  assert.equal(parseRelease(release({ assets: release().assets.slice(1) })), null);
  assert.equal(parseRelease(release({ tag_name: 'v6.1.2' })), null);
});

test('rejects leading zeroes and numeric components outside JavaScript safe integers', () => {
  for (const tag of [
    'v06.2.0', 'v6.02.0', 'v6.2.00',
    'v9007199254740992.0.0', 'v6.9007199254740992.0', 'v6.2.9007199254740992',
    'v99999999999999999.0.0'
  ]) assert.equal(parseRelease(release({ tag_name: tag })), null, tag);
});

test('keeps a working v6.1.3 fallback and matching published digests', () => {
  const home = readFileSync(require.resolve('../site/index.html'), 'utf8');
  const download = readFileSync(require.resolve('../site/indir/index.html'), 'utf8');
  const releaseScript = readFileSync(require.resolve('../site/assets/release.js'), 'utf8');
  assert.match(releaseScript, /fallbackVersion = '6\.1\.3'/);
  assert.match(home, /data-release-version>v6\.1\.3/);
  for (const [kind, name] of Object.entries({
    online: 'MistikSetup-Online-6.1.3.exe',
    offline: 'MistikSetup-Offline-6.1.3.exe',
    portable: 'MistikLauncher-6.1.3-win-x64.zip'
  })) {
    assert.match(download, new RegExp(`data-release-asset="${kind}" href="https://github\\.com/${repo}/releases/download/v6\\.1\\.3/${name.replaceAll('.', '\\.')}"`));
    const row = new RegExp(`data-release-hash="${kind}">[\\s\\S]*?<code>([a-f0-9]{64})</code>[\\s\\S]*?data-copy-hash="([a-f0-9]{64})"`).exec(download);
    assert.ok(row, `${kind} fallback hash exists`);
    assert.equal(row[1], {
      online: '16cd85ff1a54caf67d8d600970a0f8636d6d3c85fb8831babac2b345c69b6e7b',
      offline: '09f848ecade8757b83e51e8c45ae6267b5404f3e6766c07f456a3c7de1386aef',
      portable: '70cfe05b989da3ae25fdb32a74af3620225f86ff4b3152c96da7ca2db6f218b2'
    }[kind]);
    assert.equal(row[1], row[2]);
  }
});
