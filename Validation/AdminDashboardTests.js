'use strict';
const assert = require('node:assert/strict');
const test = require('node:test');
const {readFileSync} = require('node:fs');
const {resolve} = require('node:path');
const {pathToFileURL} = require('node:url');
const vm = require('node:vm');
const root = resolve(__dirname, '..');

async function dashboard(fetch = async () => { throw new Error("No network fixture configured"); }) {
  const {summarize} = await import(pathToFileURL(resolve(root, 'site/admin/metrics.mjs')));
  class Element {
    constructor() { this.children = []; this.value = ''; this.dataset = {}; this.textContent = ''; }
    append(...children) { this.children.push(...children); }
    replaceChildren(...children) { this.children = children; }
  }
  const elements = new Map();
  const document = {createElement: () => new Element(), getElementById(id) {
    if (!elements.has(id)) elements.set(id, new Element());
    return elements.get(id);
  }};
  const source = readFileSync(resolve(root, 'site/admin/admin.js'), 'utf8')
    .replace(/^import .*?;\r?\n/gm, '')
    .split(/\r?\ntry \{\r?\n  const response = await fetch\('\.\/admin-config\.json'/)[0];
  const context = vm.createContext({document, summarize, Intl, Date, URL, URLSearchParams, AbortSignal, fetch});
  vm.runInContext(source, context);
  return {elements, render: context.render, clear: context.clear, loadLauncherDetails: context.loadLauncherDetails};
}
const now = 2_000_000_000;
const reports = {errors:{a:{java:{code:'MLU-JAVA',version:'6.2.29',lastSeen:now,count:3},memory:{code:'MLU-MEMORY',version:'6.2.28',lastSeen:now-1000,count:2}},b:{java:{code:'MLU-JAVA',version:'6.2.28',lastSeen:now-2000,count:1}}}};

test('admin filters intersect categories and versions, then reset without losing source data', async () => {
  const {elements, render} = await dashboard();
  render(reports, now);
  assert.equal(elements.get('errors').children.length, 3);
  const categories = elements.get('errorCategory'), versions = elements.get('errorVersion');
  categories.value = 'Java sürümü'; categories.onchange();
  assert.equal(elements.get('errors').children.length, 2);
  versions.value = '6.2.29'; versions.onchange();
  assert.equal(elements.get('errors').children.length, 1);
  assert.equal(elements.get('errorCount').textContent, '1 / 3 kayıt gösteriliyor');
  render(reports, now + 30000, now);
  assert.equal(categories.value, 'Java sürümü');
  assert.equal(versions.value, '6.2.29');
  elements.get('resetErrors').onclick();
  assert.equal(elements.get('errors').children.length, 3);
  assert.equal(elements.get('resetErrors').disabled, true);
});

test('periodic refresh preserves the existing filter options when their values are unchanged', async () => {
  const {elements, render} = await dashboard();
  render(reports,now);
  const category = elements.get('errorCategory'), version = elements.get('errorVersion');
  const categories = [...category.children], versions = [...version.children];
  category.value = 'Java sürümü'; version.value = '6.2.29';
  render(reports,now+30000,now);
  assert.deepEqual(category.children,categories);
  assert.deepEqual(version.children,versions);
  assert.equal(category.value,'Java sürümü'); assert.equal(version.value,'6.2.29');
});

async function liveDashboard() {
  const {summarize} = await import(pathToFileURL(resolve(root, 'site/admin/metrics.mjs')));
  const elements = new Map(), listeners = [], timers = new Set(), authCallbacks = {};
  class Element {
    constructor() { this.children=[]; this.value=''; this.dataset={}; this.textContent=''; }
    append(...children) { this.children.push(...children); }
    replaceChildren(...children) { this.children=children; }
  }
  const document = {createElement:()=>new Element(),getElementById(id){
    if(!elements.has(id)) elements.set(id,new Element()); return elements.get(id);
  }};
  const authSdk = {getAuth:()=>({}),setPersistence:async()=>{},browserSessionPersistence:{},getRedirectResult:async()=>null,
    onAuthStateChanged(auth,next,error){Object.assign(authCallbacks,{next,error});}};
  const dbSdk = {getDatabase:()=>({}),ref:(database,path)=>path,onValue(path,next,error){
    const listener={path,next,error,stopped:false}; listeners.push(listener); return ()=>{listener.stopped=true;};
  }};
  let timerId=0;
  const source=readFileSync(resolve(root,'site/admin/admin.js'),'utf8').replace(/^import .*?;\r?\n/gm,'')
    .replace(/const \[\{initializeApp\}, authSdk, dbSdk\] = await Promise\.all\(\[[\s\S]*?\]\);/,
      'const {initializeApp,authSdk,dbSdk}=sdk;');
  const context=vm.createContext({document,summarize,Intl,Date,URL,URLSearchParams,AbortSignal,
    sdk:{initializeApp:()=>({}),authSdk,dbSdk}, mountSupport:()=>({stop(){},connection(){},refresh(){}}),
    fetch:async url=>url==='./admin-config.json'?{ok:true,json:async()=>({apiKey:'fixture',authDomain:'fixture',databaseURL:'https://fixture.firebaseio.com',projectId:'fixture'})}:{ok:false},
    setInterval(callback){const timer={id:++timerId,callback}; timers.add(timer); return timer;},clearInterval(timer){timers.delete(timer);}});
  await vm.runInContext('(async()=>{'+source+'})()',context);
  return {elements,listeners,timers,authCallbacks};
}

test('auth failure stops realtime work and rejects already queued data and timer callbacks', async () => {
  const {elements,listeners,timers,authCallbacks}=await liveDashboard();
  authCallbacks.next({uid:'fixture',emailVerified:true,providerData:[{providerId:'google.com'}]});
  const first=listeners.slice(), timer=[...timers][0];
  first.find(item=>item.path==='.info/connected').next({val:()=>true});
  first.find(item=>item.path==='launcherMetrics').next({val:()=>({})});
  assert.equal(elements.get('dashboard').hidden,false);
  authCallbacks.error(new Error('Controlled auth failure'));
  assert.equal(elements.get('dashboard').hidden,true); assert.equal(timers.size,0);
  assert.ok(first.every(item=>item.stopped));
  first.find(item=>item.path==='launcherMetrics').next({val:()=>reports});
  first.find(item=>item.path==='.info/connected').next({val:()=>true}); timer.callback();
  assert.equal(elements.get('dashboard').hidden,true);
  assert.equal(elements.get('status').textContent,'Google oturumu doğrulanamadı.');
  assert.equal(elements.get('login').hidden,false); assert.equal(elements.get('logout').hidden,true);
});

test('permission error is retained across connection updates and old account callbacks are ignored', async () => {
  const {elements,listeners,authCallbacks}=await liveDashboard();
  authCallbacks.next({uid:'first',emailVerified:true,providerData:[{providerId:'google.com'}]});
  const first=listeners.slice();
  first.find(item=>item.path==='launcherMetrics').error(new Error('Controlled permission failure'));
  const error=elements.get('status').textContent;
  first.find(item=>item.path==='.info/connected').next({val:()=>false});
  first.find(item=>item.path==='.info/connected').next({val:()=>true});
  assert.equal(elements.get('status').textContent,error);
  authCallbacks.next({uid:'second',emailVerified:true,providerData:[{providerId:'google.com'}]});
  first.find(item=>item.path==='launcherMetrics').next({val:()=>reports});
  first.find(item=>item.path==='launcherMetrics').error(new Error('Old account callback'));
  assert.equal(elements.get('dashboard').hidden,true);
  assert.equal(elements.get('status').textContent,'Yetki ve Firebase verileri kontrol ediliyor…');
  const current=listeners.slice(first.length);
  current.find(item=>item.path==='.info/connected').next({val:()=>true});
  current.find(item=>item.path==='launcherMetrics').next({val:()=>({})});
  assert.equal(elements.get('dashboard').hidden,false);
});

test('admin distinguishes unmatched filters from no reports and clears counts on lost access', async () => {
  const {elements, render, clear} = await dashboard();
  render(reports, now);
  elements.get('errorCategory').value = 'Bellek'; elements.get('errorVersion').value = '6.2.29';
  elements.get('errorVersion').onchange();
  assert.equal(elements.get('errors').children[0].children[0].textContent, 'Bu filtrelere uyan bildirim yok');
  render({}, now);
  assert.equal(elements.get('errors').children[0].children[0].textContent, 'Henüz hata bildirilmedi');
  assert.equal(elements.get('errorCategory').value, '');
  clear();
  assert.equal(elements.get('dashboard').hidden, true);
  assert.equal(elements.get('errors').children.length, 0);
  assert.equal(elements.get('errorCount').textContent, '');
});

test('admin last-data time stays tied to the snapshot during activity refreshes', async () => {
  const {elements, render} = await dashboard();
  render(reports, now, now);
  const timestamp = elements.get('lastUpdated').dateTime;
  render(reports, now + 30000, now);
  assert.equal(elements.get('lastUpdated').dateTime, timestamp);
  render(reports, now + 30000, now + 30000);
  assert.notEqual(elements.get('lastUpdated').dateTime, timestamp);
});

test('admin filters all error records before limiting visible rows to fifty', async () => {
  const {elements, render} = await dashboard();
  const errors = Object.fromEntries(Array.from({length:70}, (_,i) => ['u'+i, {java:{
    code:i===0?'MLU-JAVA':'MLU-MEMORY', version:i===0?'6.2.1':'6.2.30', lastSeen:now-70000+i*1000, count:1
  }}]));
  render({errors},now);
  assert.equal(elements.get('errors').children.length,50);
  const categories = elements.get('errorCategory'), versions = elements.get('errorVersion');
  assert.ok(categories.children.some(option=>option.value==='Java sürümü'));
  assert.ok(versions.children.some(option=>option.value==='6.2.1'));
  categories.value = 'Java sürümü'; categories.onchange();
  assert.equal(elements.get('errors').children.length,1);
  assert.equal(elements.get('errors').children[0].children[0].textContent,'Java sürümü');
  versions.value = '6.2.1'; versions.onchange();
  assert.equal(elements.get('errors').children.length,1);
  assert.equal(elements.get('errors').children[0].children[1].textContent,'6.2.1');
  elements.get('resetErrors').onclick();
  assert.equal(elements.get('errors').children.length,50);
  assert.match(elements.get('errorCount').textContent,/70/);
});

function content(element) {
  return [element.textContent, ...element.children.map(content)].join(' ');
}
function descendants(element) {
  return [element, ...element.children.flatMap(descendants)];
}

test('empty metrics explain missing coverage and never claim that no one uses the launcher', async () => {
  const {elements, render} = await dashboard();
  render({},now);
  for (const id of ['installations','active','daily','recentErrors','weekly','monthly','newDaily','newWeekly'])
    assert.equal(elements.get(id).textContent,'—',id);
  assert.match(elements.get('coverageNote').textContent,/Henüz katılım kaydı yok/);
  assert.match(elements.get('coverageNote').textContent,/kimsenin kullanmadığı anlamına gelmez/);
  assert.match(elements.get('coverageNote').textContent,/Toplam kullanıcı sayısı bilinmiyor/);
});

test('richer counters render observed data and installation tables expose no raw identities', async () => {
  const {elements, render, clear} = await dashboard();
  const installations = Object.fromEntries(Array.from({length:105},(_,i)=>['private-uid-'+i,{firstSeen:now-86400001,lastSeen:now-i*1000,version:'6.2.30'}]));
  render({installations,sessions:{'private-uid-0':{lastSeen:now,version:'6.2.30'}},
    errors:{'private-uid-0':{java:{code:'MLU-JAVA',version:'6.2.30',lastSeen:now,count:4}}}},now);
  assert.equal(elements.get('installations').textContent,'105');
  assert.equal(elements.get('weekly').textContent,'105'); assert.equal(elements.get('monthly').textContent,'105');
  assert.equal(elements.get('newDaily').textContent,'0'); assert.equal(elements.get('newWeekly').textContent,'105');
  assert.equal(elements.get('active').textContent,'1'); assert.equal(elements.get('validSessions').textContent,'1');
  const rows = elements.get('installationRows');
  assert.equal(rows.children.length,100);
  assert.equal(rows.children[0].children[0].textContent,'Kayıt 1');
  assert.equal(rows.children[99].children[0].textContent,'Kayıt 100');
  assert.doesNotMatch(content(rows),/private-uid-/);
  assert.match(elements.get('installationCount').textContent,/100 \/ 105/);
  assert.match(elements.get('coverageNote').textContent,/4 birikimli hata olayı/);
  clear();
  for (const id of ['weekly','monthly','newDaily','newWeekly','lastHeartbeat','validSessions','ignoredRecords'])
    assert.equal(elements.get(id).textContent,'—',id);
  assert.equal(rows.children.length,0); assert.equal(elements.get('coverageNote').textContent,'');
});

function releaseFixture() {
  const tag = 'v6.2.30', version = '6.2.30';
  const names = [`MistikSetup-Online-${version}.exe`,`MistikSetup-Offline-${version}.exe`,`MistikRepair-${version}.exe`,`MistikLauncher-${version}-win-x64.zip`,'SHA256SUMS.txt'];
  return {tag_name:tag,draft:false,prerelease:false,published_at:'2026-10-14T10:00:00Z',
    html_url:`https://github.com/gamer3434/MistikLauncherUltra/releases/tag/${tag}`,
    assets:names.map((name,i)=>({name,size:1048576,download_count:i===4?999:i+3,
      browser_download_url:`https://github.com/gamer3434/MistikLauncherUltra/releases/download/${tag}/${name}`}))};
}

test('release downloads count only verified program assets and exclude checksum-file requests', async () => {
  const release = releaseFixture();
  const {elements, loadLauncherDetails} = await dashboard(async (url,options)=> {
    assert.equal(url,'https://api.github.com/repos/gamer3434/MistikLauncherUltra/releases/latest');
    assert.equal(options.cache,'no-store'); assert.ok(options.signal instanceof AbortSignal);
    return {ok:true,json:async()=>release};
  });
  await loadLauncherDetails();
  const panel = elements.get('releaseDetails');
  assert.match(content(panel),/18 program dosyası indirmesi/);
  assert.equal(descendants(panel).filter(item=>item.className==='asset-row').length,4);
  assert.doesNotMatch(content(panel),/999/);
  assert.match(content(panel),/Kullanıcı veya kurulum sayısı değildir/);
});

test('invalid or missing release download counts remain unknown instead of becoming zero', async () => {
  for (const count of [-1, undefined, 1.5, '5']) {
    const release = releaseFixture(); release.assets[0].download_count = count;
    const {elements, loadLauncherDetails} = await dashboard(async()=>({ok:true,json:async()=>release}));
    await loadLauncherDetails();
    assert.match(content(elements.get('releaseDetails')),/İndirme sayısı doğrulanamadı/);
    assert.doesNotMatch(content(elements.get('releaseDetails')),/18 program dosyası indirmesi/);
  }
});

test('partial, duplicated and overflowing program asset totals remain unknown', async () => {
  const incomplete = releaseFixture(); incomplete.assets.splice(1,1);
  const duplicated = releaseFixture(); duplicated.assets.push({...duplicated.assets[0]});
  const overflow = releaseFixture(); overflow.assets[0].download_count = Number.MAX_SAFE_INTEGER;
  for (const release of [incomplete, duplicated, overflow]) {
    const {elements, loadLauncherDetails} = await dashboard(async()=>({ok:true,json:async()=>release}));
    await loadLauncherDetails();
    assert.match(content(elements.get('releaseDetails')),/İndirme sayısı doğrulanamadı/);
    assert.doesNotMatch(content(elements.get('releaseDetails')),/\d+ program dosyası indirmesi/);
  }
});

test('unofficial release links and failed requests cannot retain previously verified release data', async () => {
  const variants = [
    {html_url:'https://github.com/other/project/releases/tag/v6.2.30'},
    {html_url:'https://github.com.attacker.invalid/gamer3434/MistikLauncherUltra/releases/tag/v6.2.30'},
    {draft:true}, {prerelease:true}
  ];
  for (const changes of variants) {
    let result = releaseFixture();
    const {elements, loadLauncherDetails} = await dashboard(async()=>({ok:true,json:async()=>result}));
    await loadLauncherDetails(); assert.match(content(elements.get('releaseDetails')),/v6.2.30/);
    result = {...releaseFixture(),...changes};
    await loadLauncherDetails();
    assert.equal(elements.get('releaseDetails').children.length,1);
    assert.equal(content(elements.get('releaseDetails')).trim(),'Sürüm bilgisi şu anda alınamadı.');
  }
  const {elements, loadLauncherDetails} = await dashboard(async()=>{throw new Error('Controlled offline test');});
  await loadLauncherDetails();
  assert.match(content(elements.get('releaseDetails')),/Sürüm bilgisi şu anda alınamadı/);
});
