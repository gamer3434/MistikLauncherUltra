'use strict';
const assert = require('node:assert/strict');
const test = require('node:test');
const {readFileSync} = require('node:fs');
const {resolve} = require('node:path');
const {pathToFileURL} = require('node:url');
const vm = require('node:vm');
const root = resolve(__dirname, '..');

async function dashboard() {
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
    .replace(/^import .*?;\r?\n/, '')
    .split(/\r?\ntry \{\r?\n  const response = await fetch\('\.\/admin-config\.json'/)[0];
  const context = vm.createContext({document, summarize, Intl, Date, URL, URLSearchParams});
  vm.runInContext(source, context);
  return {elements, render: context.render, clear: context.clear};
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
