'use strict';

const assert = require('node:assert/strict');
const { existsSync, readFileSync } = require('node:fs');
const { resolve } = require('node:path');
const test = require('node:test');

const read = path => readFileSync(require.resolve(path), 'utf8');

test('retired account data and active sessions deny access', () => {
  const { rules } = JSON.parse(read('../firebase/database.rules.json'));
  assert.equal(rules['.read'], false);
  assert.equal(rules['.write'], false);
  assert.equal(rules.activeSessions['.read'], false);
  assert.equal(rules.activeSessions['.write'], false);
  assert.equal(rules.activeSessions.$uid, undefined);
  assert.deepEqual(rules.launcherProfiles, { '.read': false, '.write': false });
  const firebase = JSON.parse(read('../firebase.json'));
  assert.equal(firebase.database.rules, 'firebase/database.rules.json');
  assert.equal(firebase.auth, undefined);
  assert.equal(JSON.parse(read('../firebase/firebase.json')).auth, undefined);
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
