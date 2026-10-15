import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {resolve} from 'node:path';
import {createRequire} from 'node:module';
const require = createRequire(resolve('artifacts/firebase-tests/package.json'));
const {initializeTestEnvironment, assertSucceeds, assertFails} = require('@firebase/rules-unit-testing');
const {ref, set, get, remove, update, serverTimestamp} = require('firebase/database');
const rules = readFileSync('analytics/database.rules.template.json', 'utf8').replaceAll('ADMIN_EMAIL', 'admin@example.test');
const [host, port] = (process.env.FIREBASE_DATABASE_EMULATOR_HOST || '127.0.0.1:9199').split(':');
const env = await initializeTestEnvironment({projectId:'demo-mistik-metrics', database:{host, port:Number(port), rules}});
let checks = 0;
const yes = async operation => { await assertSucceeds(operation); checks++; };
const no = async operation => { await assertFails(operation); checks++; };
try {
  await env.clearDatabase();
  const anonymous = {firebase:{sign_in_provider:'anonymous'}};
  const owner = env.authenticatedContext('support-a', anonymous).database();
  const stranger = env.authenticatedContext('support-b', anonymous).database();
  const adminClaims = {email:'admin@example.test', email_verified:true, firebase:{sign_in_provider:'google.com'}};
  const admin = env.authenticatedContext('admin', adminClaims).database();
  const unverified = env.authenticatedContext('unverified', {...adminClaims, email_verified:false}).database();
  const wrongProvider = env.authenticatedContext('password', {...adminClaims, firebase:{sign_in_provider:'password'}}).database();
  const otherAdmin = env.authenticatedContext('other-admin', {...adminClaims, email:'other@example.test'}).database();
  const sameUidWrongProvider = env.authenticatedContext('support-a', {firebase:{sign_in_provider:'password'}}).database();
  const guest = env.unauthenticatedContext().database();
  const devicePath = 'launcherSupport/devices/support-a';
  const requestPath = 'launcherSupport/requests/support-a';
  const device = () => ({username:'Player', version:'6.2.39', profile:'fabric-1.21.1', lastSeen:serverTimestamp(),
    mods:[{name:'fabric-api.jar', enabled:true}, {name:'voicechat.jar.disabled', enabled:false}],
    modCount:2, truncated:false, allowRequests:true});
  const request = () => ({id:'0123456789abcdef0123456789abcdef', projectId:'P7dR8mSH',
    projectName:'Fabric API', profile:'fabric-1.21.1', createdAt:serverTimestamp()});
  await yes(set(ref(owner, devicePath), device()));
  await yes(get(ref(owner, devicePath)));
  await yes(get(ref(admin, 'launcherSupport/devices')));
  await yes(get(ref(admin, requestPath)));
  for (const db of [guest, stranger, unverified, wrongProvider, otherAdmin, sameUidWrongProvider]) {
    await no(get(ref(db, devicePath)));
    await no(get(ref(db, requestPath)));
    await no(set(ref(db, devicePath), device()));
    await no(set(ref(db, requestPath), request()));
  }
  await no(get(ref(owner, 'launcherSupport/devices')));
  await no(get(ref(owner, 'launcherSupport/requests')));
  await no(get(ref(admin, 'launcherSupport')));
  await no(set(ref(admin, devicePath), device()));
  await no(set(ref(owner, 'launcherSupport'), {devices:{'support-a':device()}}));
  for (const fields of [
    {username:''}, {username:'x'.repeat(33)}, {version:'v6.2.39'}, {version:'6.2.39-beta'},
    {profile:''}, {profile:'x'.repeat(161)}, {lastSeen:Date.now()-360000}, {lastSeen:Date.now()+360000},
    {modCount:-1}, {modCount:1.5}, {modCount:100001}, {truncated:'false'}, {allowRequests:1},
    {email:'private@example.test'}, {mods:'not-an-inventory'},
    {mods:{300:{name:'overflow.jar',enabled:true}}}, {mods:{'01':{name:'bad-index.jar',enabled:true}}},
    {mods:[{name:'valid.jar',enabled:true,url:'https://example.test/payload'}]},
    {mods:[{name:'valid.jar'}]}, {mods:[{name:'valid.jar',enabled:'true'}]}
  ]) await no(set(ref(owner, devicePath), {...device(), ...fields}));
  for (const name of ['', 'x'.repeat(181), '../outside.jar', 'dir/mod.jar', 'dir\\mod.jar', 'bad\nname.jar',
    'bad\r\nname.jar', 'bad\0name.jar', 'bad\x7fname.jar', 'bad\u0085name.jar'])
    await no(set(ref(owner, devicePath), {...device(), mods:[{name,enabled:true}]}));
  // Firebase removes empty arrays: an absent inventory remains a valid empty report.
  await yes(set(ref(owner, devicePath), {...device(), mods:[], modCount:0}));
  const empty = (await get(ref(owner, devicePath))).val();
  assert.equal(empty.mods, undefined); checks++;
  await yes(set(ref(owner, devicePath), {...device(), mods:{299:{name:'last.jar',enabled:true}}, modCount:100000, truncated:true}));
  await yes(set(ref(owner, devicePath), {...device(), mods:[{name:'Türkçe-地图-🎮.jar',enabled:true}]}));
  await yes(set(ref(owner, devicePath), {...device(), mods:Array.from({length:300}, (_,i) => ({name:`mod-${i}.jar`,enabled:true})), modCount:300}));
  await no(set(ref(owner, devicePath), {...device(), mods:Array.from({length:301}, (_,i) => ({name:`mod-${i}.jar`,enabled:true})), modCount:301}));
  await no(set(ref(owner, requestPath), request()));
  await yes(set(ref(owner, devicePath), {...device(), allowRequests:false}));
  await no(set(ref(admin, requestPath), request()));
  await yes(set(ref(owner, devicePath), device()));
  for (const fields of [
    {id:'A'.repeat(32)}, {id:'a'.repeat(31)}, {projectId:'fabric-api'}, {projectId:'../x'},
    {projectName:''}, {projectName:'x'.repeat(121)}, {profile:'forge-1.21.1'},
    {createdAt:Date.now()-360000}, {createdAt:Date.now()+360000},
    {url:'https://example.test/payload'}, {path:'mods/payload.jar'}, {command:'run'}
  ]) await no(set(ref(admin, requestPath), {...request(), ...fields}));
  await env.withSecurityRulesDisabled(async context => {
    await set(ref(context.database(), devicePath), {...device(), lastSeen:Date.now()-300001});
  });
  await no(set(ref(admin, requestPath), request()));
  await env.withSecurityRulesDisabled(async context => {
    await set(ref(context.database(), devicePath), {...device(), lastSeen:Date.now()+360000});
  });
  await no(set(ref(admin, requestPath), request()));
  await yes(set(ref(owner, devicePath), device()));
  await yes(set(ref(admin, requestPath), request()));
  await yes(get(ref(owner, requestPath)));
  await yes(get(ref(admin, 'launcherSupport/requests')));
  await no(set(ref(admin, requestPath), {...request(), projectName:'Replacement'}));
  await no(update(ref(admin, requestPath), {projectName:'Replacement'}));
  await no(remove(ref(admin, requestPath)));
  await no(remove(ref(stranger, requestPath)));
  await no(update(ref(owner, requestPath), {projectName:'Forged'}));
  await no(remove(ref(admin, devicePath)));
  await yes(remove(ref(owner, devicePath)));
  await yes(get(ref(owner, requestPath)));
  await yes(remove(ref(owner, requestPath)));
  await no(set(ref(admin, requestPath), request()));
  await no(set(ref(owner, 'launcherSupport/unknown'), {command:'run'}));
  console.log(`Launcher support emulator: ${checks} ownership, schema, consent and request checks passed.`);
} finally { await env.cleanup(); }
