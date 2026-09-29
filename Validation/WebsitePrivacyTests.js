'use strict';

const assert = require('node:assert/strict');
const { existsSync, readFileSync } = require('node:fs');
const { resolve } = require('node:path');
const test = require('node:test');

const read = path => readFileSync(require.resolve(path), 'utf8');

test('active sessions are closed and launcher profiles require verified account owners', () => {
  const { rules } = JSON.parse(read('../firebase/database.rules.json'));
  assert.equal(rules['.read'], false);
  assert.equal(rules['.write'], false);
  assert.equal(rules.activeSessions['.read'], false);
  assert.equal(rules.activeSessions['.write'], false);
  assert.equal(rules.activeSessions.$uid, undefined);
  const verifiedOwner = "auth != null && auth.uid === $uid && auth.token.firebase.sign_in_provider !== 'anonymous' && auth.token.email_verified === true";
  assert.equal(rules.launcherProfiles.$uid['.read'], verifiedOwner);
  assert.equal(rules.launcherProfiles.$uid['.write'], verifiedOwner);
  const firebase = JSON.parse(read('../firebase.json'));
  assert.equal(firebase.database.rules, 'firebase/database.rules.json');
  assert.equal(firebase.auth.providers.emailPassword, true);
  assert.equal(firebase.auth.providers.anonymous, true);
  assert.equal(JSON.parse(read('../firebase/firebase.json')).auth.providers.anonymous, true);
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
  assert.match(words, /'fact\.local':'Ayarların cihazında; bulut yedeği isteğe bağlı'/);
  assert.match(words, /'fact\.local':'Settings stay local; cloud backup is optional'/);
  assert.doesNotMatch(words, /'fact\.active'|'presence\./);
  assert.equal(existsSync(resolve(__dirname, '../site/assets/presence.js')), false);
});
