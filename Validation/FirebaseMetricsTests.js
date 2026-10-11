'use strict';
const assert = require('node:assert/strict');
const test = require('node:test');
const {readFileSync} = require('node:fs');
const {resolve} = require('node:path');
const {pathToFileURL} = require('node:url');
const root = resolve(__dirname, '..');
const read = path => readFileSync(resolve(root, path), 'utf8').replace(/^\uFEFF/, '');
const model = import(pathToFileURL(resolve(root, 'site/admin/metrics.mjs')));
test('metrics count opt-in installations, distinct recent sessions and bounded category summaries', async () => {
  const {summarize} = await model;
  const now = 2_000_000_000;
  const data = {installations: {a:{firstSeen:1,lastSeen:now,version:'6.2.14'}, b:{firstSeen:2,lastSeen:now-86400001,version:'6.2.13'}},
    sessions:{a:{lastSeen:now-299999,version:'6.2.14'},b:{lastSeen:now-300001,version:'6.2.13'},future:{lastSeen:now+60001,version:'6.2.14'}},
    errors:{a:{'MLU-JAVA':{lastSeen:now,version:'6.2.14',code:'MLU-JAVA',count:3}},b:{'MLU-JAVA':{lastSeen:now-86400001,version:'6.2.13',code:'MLU-JAVA',count:2}}}};
  const value = summarize(data, now);
  assert.equal(value.installations,2); assert.equal(value.active,1); assert.equal(value.daily,1); assert.equal(value.recentErrors,1);
  assert.deepEqual(value.categories, [['Java sürümü',5]]); assert.equal(value.errors[0].version,'6.2.14');
});
test('metrics reject unexpected raw data and cap displayed errors', async () => {
  const {summarize} = await model;
  assert.equal(summarize(null, Date.now()).installations,0);
  const invalid = {errors:{a:{unknown:{code:'<script>',version:'6.2.14',lastSeen:1,count:1},bad:{code:'MLU-JAVA',version:'secret',lastSeen:1,count:1}}}};
  assert.equal(summarize(invalid,2).errors.length,0);
  const many = {errors:Object.fromEntries(Array.from({length:70}, (_,i)=>[i,{java:{code:'MLU-JAVA',version:'6.2.14',lastSeen:i,count:1}}]))};
  assert.equal(summarize(many,100).errors.length,50);
});
test('dashboard never injects metric strings into HTML and failures clear previous counts', () => {
  const source=read('site/admin/admin.js');
  assert.doesNotMatch(source,/innerHTML|insertAdjacentHTML/);
  assert.match(source,/td\.textContent = String\(value\)/);
  assert.match(source,/data = undefined; clear\(\); status\('Veriler okunamadı/);
  assert.match(source,/browserSessionPersistence/);
  assert.match(source,/signInWithPopup/);
});
test('deployment defaults stay deny and admin CSP is scoped', () => {
  const rules=JSON.parse(read('analytics/database.rules.template.json')).rules;
  assert.equal(rules['.read'],false); assert.equal(rules['.write'],false);
  assert.equal(rules.activeSessions['.write'],false); assert.equal(rules.launcherProfiles['.read'],false);
  assert.match(rules.launcherMetrics['.read'],/email_verified == true/);
  assert.match(rules.launcherMetrics['.read'],/google.com/);
  assert.match(rules.launcherMetrics['.read'],/ADMIN_EMAIL/);
  const config=JSON.parse(read('firebase.json'));
  assert.equal(config.database.rules,'database.rules.json');
  const general=config.hosting.headers.find(item=>item.source==='**');
  assert.doesNotMatch(general.headers.find(item=>item.key==='Content-Security-Policy').value,/gstatic/);
  const admin=config.hosting.headers.find(item=>item.source==='/admin/**');
  assert.match(admin.headers.find(item=>item.key==='Content-Security-Policy').value,/gstatic/);
  assert.match(read('.gitignore'),/\/site\/admin\/admin-config\.json/);
});
