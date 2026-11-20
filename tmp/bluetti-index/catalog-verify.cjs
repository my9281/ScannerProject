const vm = require('node:vm');
const { readFileSync } = require('node:fs');
const assert = require('node:assert/strict');

const values = new Map();
const db = new Map();
const calls = [];
const counters = { open: 0, write: 0, clear: 0 };
let release = 'v1';
let online = true;
const sample = [{ sku: 'AC180', description: 'Portable station', model: 'AC180', searchText: 'AC180 PORTABLE STATION' }];

const indexedDB = {
  open() {
    counters.open++;
    const result = {
      close() {},
      transaction() {
        const tx = { objectStore: () => ({
          get: id => ({ result: db.get(id) }),
          put: (value, id) => { counters.write++; db.set(id, value); return { result: id }; }
        }) };
        queueMicrotask(() => tx.oncomplete());
        return tx;
      }
    };
    const request = { result };
    queueMicrotask(() => request.onsuccess());
    return request;
  },
  deleteDatabase() {
    counters.clear++;
    db.clear();
    const request = {};
    queueMicrotask(() => request.onsuccess());
    return request;
  }
};

class Element {
  constructor() { this.listeners = {}; this.value = ''; this.children = []; }
  addEventListener(type, fn) { this.listeners[type] = fn; }
  append(...children) { this.children.push(...children); }
  replaceChildren(...children) { this.children = children; }
}

function page() {
  const nodes = new Map();
  const node = key => {
    if (!nodes.has(key)) nodes.set(key, new Element());
    return nodes.get(key);
  };
  const browser = {
    document: {
      cookie: '',
      querySelector: () => ({ dataset: { catalog: 'shipping' } }),
      getElementById: key => ['catalog-model', 'catalog-category'].includes(key) ? null : node(key),
      createElement: () => new Element()
    },
    location: { protocol: 'https:', search: '' },
    localStorage: { getItem: key => values.get(key) ?? null, setItem: (k, v) => values.set(k, v) },
    indexedDB,
    AbortController,
    URLSearchParams,
    fetch: async (url, { headers }) => {
      calls.push({ url, headers });
      if (!online) throw Error('offline');
      return headers['If-None-Match'] === release ? { status: 304 } : {
        ok: true, status: 200, json: async () => sample, headers: { get: () => release }
      };
    },
    addEventListener() {}
  };
  browser.window = browser;
  vm.createContext(browser);
  for (const file of ['catalog-storage.js', 'catalog-search.js']) {
    vm.runInContext(readFileSync('Scanner.Web/wwwroot/' + file, 'utf8'), browser, { filename: file });
  }
  return { node, browser };
}
const tick = () => new Promise(resolve => setImmediate(resolve));
(async () => {
  let p = page();
  await tick();
  assert.equal(counters.open, 0);
  assert.equal(p.node('catalog-preferences').open, true);
  p.node('catalog-query').value = 'ＡＣ１８０ portable';
  p.node('catalog-query').listeners.input();
  assert.equal(p.node('catalog-rows').children.length, 1);
  assert.equal(calls.length, 1);
  p.node('allow-cookies').checked = true;
  p.node('allow-local-db').checked = true;
  await p.node('save-catalog-preferences').listeners.click();
  assert.equal(counters.write, 1);
  assert.match(p.browser.document.cookie, /Max-Age=31536000; SameSite=Lax; Secure/);
  p = page();
  await tick();
  assert.equal(p.node('catalog-preferences').open, false);
  assert.equal(counters.write, 1);
  assert.equal(calls.at(-1).headers['If-None-Match'], 'v1');
  online = false;
  p = page();
  await tick();
  p.node('catalog-query').value = 'AC180';
  p.node('catalog-query').listeners.input();
  assert.equal(p.node('catalog-rows').children.length, 1);
  assert.match(p.node('catalog-sync').textContent, /正在使用本地目录/);
  online = true;
  release = 'v2';
  p = page();
  await tick();
  assert.equal(counters.write, 2);
  p.node('allow-local-db').checked = false;
  p.node('allow-cookies').checked = false;
  await p.node('save-catalog-preferences').listeners.click();
  assert.equal(counters.clear, 1);
  assert.equal(db.size, 0);
  assert.match(JSON.stringify([...values]), /localDb\\?":false/);
  console.log('Consent, first write, 304, offline search, version update and cache removal verified');
})().catch(e => { console.error(e); process.exitCode = 1; });
