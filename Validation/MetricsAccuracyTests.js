'use strict';
const assert = require('node:assert/strict');
const test = require('node:test');
const {resolve} = require('node:path');
const {pathToFileURL} = require('node:url');
const model = import(pathToFileURL(resolve(__dirname, '../site/admin/metrics.mjs')));
const now = 20_000_000_000, day = 86_400_000;
const installation = (firstSeen = now - 1000, lastSeen = now, version = '6.2.30') => ({firstSeen, lastSeen, version});
const error = (code = 'MLU-JAVA', count = 1, lastSeen = now, version = '6.2.30') => ({code, count, lastSeen, version});

test('activity windows, new installations and installation details use distinct identities', async () => {
  const {summarize} = await model;
  const data = {installations: {
    a: installation(now - 3600000), b: installation(now - 10 * day, now - 6 * day),
    c: installation(now - 6 * day, now - 2 * day), d: installation(now - 40 * day, now - 29 * day),
    e: installation(now - 60 * day, now - 31 * day)
  }, sessions: {a:{lastSeen:now, version:'6.2.30'}, b:{lastSeen:now - 6 * day, version:'6.2.30'}, orphan:{lastSeen:now, version:'6.2.30'}},
  errors: {a:{java:error('MLU-JAVA',3), memory:error('MLU-MEMORY',2)}, b:{java:error('MLU-JAVA',1,now - 2 * day)}}};
  const result = summarize(data, now);
  assert.equal(result.installations,5); assert.equal(result.active,1); assert.equal(result.daily,1);
  assert.equal(result.weekly,3); assert.equal(result.monthly,4); assert.equal(result.newDaily,1); assert.equal(result.newWeekly,2);
  assert.equal(result.validSessions,2); assert.equal(result.orphanSessions,1);
  assert.equal(result.lastHeartbeat,now); assert.equal(result.totalErrors,6); assert.equal(result.errorInstallations,2);
  assert.equal(result.errorRows,3); assert.equal(result.recentErrors,2);
  assert.equal(result.installationRows.length,5);
  assert.equal(new Set(result.installationRows.map(row => row.ordinal)).size,5);
  assert.equal(result.installationRows.filter(row => row.active).length,1);
  assert.equal(result.installationRows.find(row => row.firstSeen === data.installations.a.firstSeen).errors,5);
});

test('invalid timestamps and malformed records cannot inflate totals or displace real errors', async () => {
  const {summarize} = await model;
  const data = {installations:{good:installation(), negative:installation(-1), future:installation(now,now+60001),
    reversed:installation(now,now-1), fractional:installation(1.5), nan:installation(NaN), infinity:installation(1,Infinity),
    wrongVersion:installation(1,now,'private value'), array:[1,2,3]},
    sessions:{good:{lastSeen:now,version:'6.2.30'}, negative:{lastSeen:-1,version:'6.2.30'},
      future:{lastSeen:now+60001,version:'6.2.30'}, fraction:{lastSeen:now-0.5,version:'6.2.30'}},
    errors:{good:{java:error()}, invalid:{negative:error('MLU-JAVA',20,-1), future:error('MLU-JAVA',30,now+60001),
      fractional:error('MLU-JAVA',40,now-0.5), infinite:error('MLU-JAVA',50,Infinity), count:error('MLU-JAVA',1.5),
      countLimit:error('MLU-JAVA',1000000001), unknown:error('<script>'), wrongVersion:error('MLU-JAVA',1,now,'secret')}}};
  const result = summarize(data, now);
  assert.equal(result.installations,1); assert.equal(result.active,1); assert.equal(result.totalErrors,1);
  assert.equal(result.errors.length,1); assert.deepEqual(result.categories,[['Java sürümü',1]]);
  assert.ok(result.ignoredRecords > 0);
});

test('all valid error rows survive aggregation so filters can find records beyond the first fifty', async () => {
  const {summarize} = await model;
  const installations = Object.fromEntries(Array.from({length:70}, (_,i) => ['u'+i, installation()]));
  const errors = Object.fromEntries(Array.from({length:70}, (_,i) => ['u'+i, {legacyAlias:error(i===0?'MLU-JAVA':'MLU-MEMORY',1,now-70000+i*1000)}]));
  const result = summarize({installations, errors},now);
  assert.equal(result.errors.length,70); assert.equal(result.errorRows,70); assert.equal(result.totalErrors,70);
  assert.equal(result.errorInstallations,70); assert.equal(result.errors.filter(item=>item.code==='MLU-JAVA').length,1);
  assert.ok(result.errors[0].lastSeen >= result.errors.at(-1).lastSeen);
});

test('five-minute activity expires without a new snapshot and orphan presence is excluded', async () => {
  const {summarize} = await model;
  const data = {installations:{a:installation()},sessions:{a:{version:'6.2.30',lastSeen:now},orphan:{version:'6.2.30',lastSeen:now}}};
  assert.equal(summarize(data,now+300000).active,1);
  assert.equal(summarize(data,now+300001).active,0);
  assert.equal(summarize({sessions:data.sessions},now).active,0);
  const orphanErrors = summarize({errors:{missing:{java:error('MLU-JAVA',4),memory:error('MLU-MEMORY',2)}}},now);
  assert.equal(orphanErrors.errorInstallations,1); assert.equal(orphanErrors.unlinkedErrorRows,2);
  assert.equal(orphanErrors.totalErrors,6);
});

test('reporting windows have inclusive boundaries and tolerate only the documented clock allowance', async () => {
  const {summarize} = await model;
  const data = {installations:{boundary:installation(now-day,now-day),grace:installation(now,now+60000),tooFar:installation(now,now+60001)},
    sessions:{grace:{lastSeen:now+60000,version:'6.2.30'}},errors:{boundary:{java:error('MLU-JAVA',1,now-day)}}};
  const result = summarize(data,now);
  assert.equal(result.installations,2); assert.equal(result.daily,2); assert.equal(result.recentErrors,1); assert.equal(result.active,1);
});

test('aggregation keeps raw records immutable and rejects container arrays', async () => {
  const {summarize} = await model;
  const record = Object.freeze(installation());
  const data = Object.freeze({installations:Object.freeze({a:record}),errors:Object.freeze({a:Object.freeze({java:Object.freeze(error())})})});
  const before = JSON.stringify(data);
  summarize(data,now);
  assert.equal(JSON.stringify(data),before);
  const invalid = summarize({installations:[installation()],sessions:[{lastSeen:now,version:'6.2.30'}],errors:[{java:error()}]},now);
  assert.equal(invalid.installations,0); assert.equal(invalid.active,0); assert.equal(invalid.errors.length,0);
});
