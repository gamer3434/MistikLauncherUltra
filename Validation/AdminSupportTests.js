'use strict';
const assert = require('node:assert/strict');
const test = require('node:test');
const {readFileSync} = require('node:fs');
const api = import('../site/admin/support.mjs');
const now = 1800000000000;
const fixture = extra => ({username:'Mustafa', version:'6.2.38', profile:'fabric-loader-0.19.5-1.21.1', lastSeen:now,
  mods:[{name:'voicechat.jar', enabled:true}], modCount:1, truncated:false, allowRequests:true, ...extra});
test('support names are separate opt-in installation records with bounded activity', async () => {
  const {readDevices} = await api;
  const rows = readDevices({active:fixture(), stale:fixture({lastSeen:now-300001}), future:fixture({lastSeen:now+60001})}, now);
  assert.equal(rows.length, 2); assert.equal(rows[0].active, true); assert.equal(rows[1].active, false);
  assert.equal(readDevices(null, now).length, 0);
});
test('support inventory validates count, basename, disabled files and truncation', async () => {
  const {readDevices} = await api;
  const read = value => readDevices({device:fixture(value)}, now);
  assert.equal(read({mods:null, modCount:0}).length, 1);
  assert.equal(read({mods:[{name:'voicechat.jar.disabled', enabled:false}]}).length, 1);
  assert.equal(read({modCount:310, truncated:true}).length, 1);
  assert.equal(read({modCount:310}).length, 0);
  for (const name of ['../mod.jar', 'C:\\mod.jar', '/mod.jar', 'mod.exe', 'bad\n.jar']) assert.equal(read({mods:[{name, enabled:true}]}).length, 0);
  assert.equal(read({mods:Array.from({length:301}, () => ({name:'mod.jar', enabled:true})), modCount:301}).length, 0);
  assert.equal(read({username:'a'.repeat(33)}).length, 0);
});
test('support UI does not interpret user-supplied names as markup', async () => {
  const {readDevices, searchProjects} = await api;
  const attack = '<img src=x onerror=alert(1)>';
  assert.equal(readDevices({device:fixture({username:attack})}, now)[0].username, attack);
  assert.equal(searchProjects({hits:[{project_type:'mod', project_id:'AbCd1234', title:attack}]})[0].projectName, attack);
  const source = readFileSync(require('node:path').join(__dirname, '../site/admin/support.mjs'), 'utf8');
  assert.ok(source.includes('textContent:value'));
  assert.doesNotMatch(source, /innerHTML|outerHTML|insertAdjacentHTML|document\.write\s*\(/);
});
test('Modrinth proposals reject slugs, modpacks, duplicates and oversized results', async () => {
  const {searchProjects} = await api;
  const good = {project_type:'mod', project_id:'AbCd1234', title:'Voice chat'};
  assert.deepEqual(searchProjects({hits:[good, good, {...good, project_id:'../../x'}, {...good, project_type:'modpack'}, {...good, title:'a'.repeat(121)}]}), [{projectId:'AbCd1234', projectName:'Voice chat'}]);
  assert.ok(searchProjects({hits:Array.from({length:20}, (_, i) => ({...good, project_id:String(i).padStart(8,'0')}))}).length <= 10);
});
test('requests carry only canonical project and current profile, never URLs or commands', async () => {
  const {requestPayload, readDevices} = await api;
  const device = readDevices({device:fixture()}, now)[0];
  const project = {projectId:'AbCd1234', projectName:'Voice chat', url:'file:///evil', command:'evil'};
  const payload = requestPayload(device, project, 'a'.repeat(32), {'.sv':'timestamp'});
  assert.deepEqual(Object.keys(payload), ['id','projectId','projectName','profile','createdAt']);
  for (const value of [{...device, active:false}, {...device, allowRequests:false}, {...device, uid:'../other'}, {...device, profile:''}]) assert.throws(() => requestPayload(value, project, 'a'.repeat(32), now));
  assert.throws(() => requestPayload(device, {...project, projectId:'mod-slug'}, 'a'.repeat(32), now));
  assert.throws(() => requestPayload(device, project, 'not-random', now));
});
test('support limits listing and response size, cancels stale work and preserves pending requests', () => {
  const source = readFileSync(require('node:path').join(__dirname, '../site/admin/support.mjs'), 'utf8');
  assert.ok(source.includes('sdk.limitToLast(100)'));
  assert.ok(source.includes('bytes > 262144'));
  assert.ok(source.includes('current !== searchGeneration'));
  assert.ok(source.includes('current !== detailGeneration'));
  assert.ok(source.includes('value => value === null ? payload : undefined'));
  assert.ok(source.includes('applyLocally:false'));
  assert.ok(source.includes('stopDevices(); stopRequest(); clearInterval(timer); cancelSearch();'));
});
async function supportFixture(t) {
  const {mountSupport} = await api;
  class Element {
    constructor(tag) { this.tagName=tag; this.children=[]; this.textContent=''; this.value=''; this.attributes={}; }
    append(...children) { this.children.push(...children); }
    replaceChildren(...children) { this.children=children; }
    setAttribute(key,value) { this.attributes[key]=value; }
  }
  const elements = new Map(), handlers = new Map(), transactions = [], stopped = [];
  const previous = {document:global.document,fetch:global.fetch};
  global.document = {createElement:tag=>new Element(tag), getElementById(id) { if(!elements.has(id))elements.set(id,new Element('div')); return elements.get(id); }};
  const sdk = {ref:(_,key)=>key,query:key=>key,orderByChild:()=>{},limitToLast:()=>{},
    onValue(key,ok,error){handlers.set(key,{ok,error});return()=>stopped.push(key);},serverTimestamp:()=>({'.sv':'timestamp'}),
    async runTransaction(key,update,options){transactions.push({key,payload:update(null),existing:update({id:'pending'}),options});return {committed:true};}};
  const support = mountSupport({database:{},sdk,now:()=>now});
  t.after(()=>{support.stop();global.document=previous.document;global.fetch=previous.fetch;});
  const $ = id => global.document.getElementById(id);
  const data = value => handlers.get('launcherSupport/devices').ok({val:()=>value});
  const select = () => $('supportRows').children[0].children[0].children[0].onclick();
  const pending = value => handlers.get('launcherSupport/requests/device').ok({val:()=>value});
  return {$,elements,handlers,transactions,stopped,support,data,select,pending};
}
test('support DOM fixture renders literal names and bounded inventory, then erases private data on stop', async t => {
  const {$,support,data,select,pending,stopped,handlers} = await supportFixture(t);
  support.connection(true);data({device:fixture({username:'<img src=x onerror=alert(1)>'})});select();pending(null);
  assert.equal($('supportName').textContent,'<img src=x onerror=alert(1)>');assert.equal($('supportName').children.length,0);
  assert.equal($('supportModList').children[0].textContent,'voicechat.jar · Açık');
  const oldHandler=handlers.get('launcherSupport/devices');support.stop();
  oldHandler.ok({val:()=>({device:fixture()})});
  assert.equal($('supportRows').children.length,0);assert.equal($('supportName').textContent,'');assert.equal($('supportDetail').hidden,true);
  assert.ok(stopped.includes('launcherSupport/devices'));assert.ok(stopped.includes('launcherSupport/requests/device'));
});
test('support DOM fixture sends canonical proposal once, blocks pending and offline targets', async t => {
  const {$,support,data,select,pending,transactions} = await supportFixture(t);
  support.connection(true);data({device:fixture()});select();pending(null);
  global.fetch=async()=>new Response(JSON.stringify({hits:[{project_type:'mod',project_id:'AbCd1234',title:'Voice chat'}]}));
  $('supportSearch').value='voice';await $('supportSearchForm').onsubmit({preventDefault(){}});
  $('supportSearchResults').children[0].onclick();assert.equal($('supportSend').disabled,false);
  assert.match($('supportRequestStatus').textContent,/izinlere göre otomatik kurulur veya launcher onayı beklenir/);
  await $('supportSend').onclick();assert.equal(transactions.length,1);assert.equal(transactions[0].key,'launcherSupport/requests/device');
  assert.match($('supportStatus').textContent,/izinlere göre otomatik kurulur veya launcher onayı beklenir/);
  assert.equal(transactions[0].existing,undefined);assert.equal(transactions[0].options.applyLocally,false);
  assert.equal(transactions[0].payload.profile,'fabric-loader-0.19.5-1.21.1');
  pending({id:'pending'});await $('supportSend').onclick();assert.equal(transactions.length,1);
  assert.match($('supportRequestStatus').textContent,/Launcher’ın işlemesi bekleniyor/);
  pending(null);support.connection(false);await $('supportSend').onclick();assert.equal(transactions.length,1);
});
test('support DOM fixture ignores late search results after target disappears', async t => {
  const {$,support,data,select,pending} = await supportFixture(t);
  support.connection(true);data({device:fixture()});select();pending(null);
  let finish;global.fetch=()=>new Promise(resolve=>finish=resolve);
  $('supportSearch').value='voice';const searching=$('supportSearchForm').onsubmit({preventDefault(){}});
  data({});finish(new Response(JSON.stringify({hits:[{project_type:'mod',project_id:'AbCd1234',title:'Old result'}]})));await searching;
  assert.equal($('supportSearchResults').children.length,0);assert.equal($('supportDetail').hidden,true);assert.equal($('supportSend').disabled,true);
});
