'use strict';

const assert = require('node:assert/strict');
const { existsSync, readFileSync } = require('node:fs');
const { resolve } = require('node:path');
const test = require('node:test');
const { execFileSync } = require('node:child_process');

const read = path => readFileSync(require.resolve(path), 'utf8');

test('tracked source excludes deployment credentials and generated outputs', () => {
  const root = resolve(__dirname, '..');
  const paths = execFileSync('git', ['ls-files', '-z'], { cwd: root, encoding: 'utf8' }).split('\0').filter(Boolean);
  const forbiddenPath = /(?:^|\/)(?:\.firebaserc|\.env(?:\..*)?|credentials\.json|service-account[^/]*\.json|[^/]*firebase-adminsdk[^/]*\.json)$|\.(?:pfx|p12|pem|key|pdb|bundle|jks|keystore)$|^publish\//i;
  const forbiddenValues = [
    /AIza[0-9A-Za-z_-]{35}/,
    /(?:gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{40,})/,
    /-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----/,
    /(?:AdminPassword\s*=|(?:pwdBox|passwordBox)\.Password\s*==)\s*"[^"\r\n]+"/,
    /"private_key"\s*:\s*"[^"\r\n]+"/
  ];
  for (const path of paths) {
    assert.equal(forbiddenPath.test(path), false, `Private/generated file tracked: ${path}`);
    const content = readFileSync(resolve(root, path), 'utf8');
    // Report only the path; never echo a matched credential in CI logs.
    assert.equal(forbiddenValues.some(pattern => pattern.test(content)), false, `Credential pattern found: ${path}`);
  }
  assert.equal(JSON.parse(read('../firebase.json')).hosting.site, undefined);
});

test('retired account data and active sessions deny access', () => {
  const { rules } = JSON.parse(read('../database.rules.json'));
  assert.equal(rules['.read'], false);
  assert.equal(rules['.write'], false);
  assert.equal(rules.activeSessions['.read'], false);
  assert.equal(rules.activeSessions['.write'], false);
  assert.equal(rules.activeSessions.$uid, undefined);
  assert.deepEqual(rules.launcherProfiles, { '.read': false, '.write': false });
  const firebase = JSON.parse(read('../firebase.json'));
  assert.equal(firebase.database.rules, 'database.rules.json');
  assert.equal(firebase.auth, undefined);
  assert.doesNotMatch(read('../MistikLauncher/MainWindow.xaml.cs'), /MistikPresence/);
  assert.equal(existsSync(resolve(__dirname, '../MistikLauncher/MistikPresence.cs')), false);
});

test('homepage has no public presence counter and includes privacy and keyboard access', () => {
  const home = read('../site/index.html');
  assert.match(home, /data-i18n="fact\.privacy"/);
  assert.match(home, /data-i18n="fact\.local"/);
  assert.match(home, /data-i18n="release\.recovery"/);
  assert.match(home, /class="skip-link" href="#main-content" data-i18n="a11y\.skip"/);
  assert.match(home, /<main\b[^>]*id="main-content"[^>]*tabindex="-1"/);
  assert.doesNotMatch(home, /presence\.js|active-count|active-status|activeSessions/);
  const words = read('../site/assets/site.js');
  assert.match(words, /'fact\.local':'Ayarların cihazında saklanır'/);
  assert.match(words, /'fact\.local':'Settings stay on your device'/);
  assert.doesNotMatch(words, /'fact\.active'|'presence\./);
  assert.equal(existsSync(resolve(__dirname, '../site/assets/presence.js')), false);
  assert.equal(existsSync(resolve(__dirname, '../site/hesap/index.html')), false);
  assert.equal(existsSync(resolve(__dirname, '../site/assets/account.js')), false);
  assert.equal(existsSync(resolve(__dirname, '../site/assets/account.css')), false);
  assert.doesNotMatch(home, /href="\/hesap\/"|nav\.account/);
  assert.doesNotMatch(words, /nav\.account|bulut yedeği|cloud backup/);
});
