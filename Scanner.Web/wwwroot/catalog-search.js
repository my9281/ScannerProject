'use strict';
(() => {
  const $ = id => document.getElementById(id);
  const id = document.querySelector('[data-catalog]').dataset.catalog;
  const manual = id === 'manual';
  const endpoint = manual ? '/manual-accessories-data.json' : '/api/catalogs/shipping-skus';
  const storage = window.catalogStorage;
  const query = $('catalog-query');
  const model = $('catalog-model');
  const category = $('catalog-category');
  const preferences = $('catalog-preferences');
  const syncStatus = $('catalog-sync');
  const retry = $('retry-catalog-sync');
  const pageSize = 50;
  const normalize = value => String(value ?? '').normalize('NFKC').toUpperCase();
  const params = new URLSearchParams(location.search);
  let page = Math.max(1, Number.parseInt(params.get('page'), 10) || 1);
  let records;
  let pending;
  let memory;

  query.value = (params.get('q') ?? '').slice(0, 120);

  function showPreferences() {
    $('allow-cookies').checked = storage.choice?.cookies ?? false;
    $('allow-local-db').checked = storage.choice?.localDb ?? false;
    if (!preferences.open) preferences.showModal();
  }

  function fillOptions(select, values) {
    const selected = select.value;
    select.options.length = 1;
    for (const value of [...new Set(values)].filter(Boolean).sort()) select.add(new Option(value, value));
    select.value = [...select.options].some(option => option.value === selected) ? selected : '';
  }

  function loadRows(rows) {
    records = rows.map(row => ({
      row,
      text: manual ? normalize([row.model, row.name, row.quantity, row.category, row.source].join(' ')) : row.searchText
    }));
    if (manual) {
      fillOptions(model, rows.map(row => row.model));
      fillOptions(category, rows.map(row => row.category));
    }
    render();
  }

  async function sync() {
    pending?.abort();
    const controller = new AbortController();
    pending = controller;
    const { signal } = controller;
    const allowed = storage.choice?.localDb;
    retry.hidden = true;
    syncStatus.textContent = '正在加载目录…';
    let cached;
    let dbFailed = false;
    if (allowed) {
      try { cached = await storage.get(id); } catch { dbFailed = true; }
      if (signal.aborted) return;
      if (cached?.rows) {
        loadRows(cached.rows);
        syncStatus.textContent = '正在使用本地目录，后台检查更新…';
      }
    }
    const previous = cached ?? memory;
    try {
      const headers = previous?.version ? { 'If-None-Match': previous.version } : {};
      const response = await fetch(endpoint, {
        headers, signal: AbortSignal.any([signal, AbortSignal.timeout(10000)]), cache: 'no-store'
      });
      let snapshot = previous;
      if (response.status !== 304) {
        if (!response.ok) throw new Error('目录加载失败。');
        const rows = await response.json();
        if (!Array.isArray(rows) || !rows.length || rows.some(row =>
          manual ? typeof row?.name !== 'string' : typeof row?.searchText !== 'string')) {
          throw new Error('目录数据格式无效。');
        }
        snapshot = { version: response.headers.get('ETag'), rows };
      }
      if (signal.aborted) return;
      if (!snapshot?.rows) throw new Error('目录缓存缺失。');
      memory = snapshot;
      loadRows(snapshot.rows);
      if (allowed && !dbFailed && (!cached || response.status !== 304)) {
        try { await storage.put(id, snapshot); } catch { dbFailed = true; }
      }
      if (signal.aborted) return;
      syncStatus.textContent = allowed && !dbFailed
        ? '本地目录已同步。'
        : allowed ? '本地数据库不可用，本页仍可搜索。' : '目录已加载，仅在本页使用。';
    } catch {
      if (signal.aborted) return;
      if (previous?.rows) loadRows(previous.rows);
      syncStatus.textContent = cached?.rows
        ? '暂时无法同步，正在使用本地目录。'
        : previous?.rows ? '暂时无法同步，正在使用已加载的目录。' : '目录加载失败，请重试。';
      retry.hidden = false;
    }
  }

  function element(tag, content) {
    const node = document.createElement(tag);
    node.textContent = content;
    return node;
  }

  function display(value) { return value == null || value === '' ? '—' : value; }

  function makeRow(row) {
    const tr = document.createElement('tr');
    const key = element('th', display(manual ? row.model : row.sku));
    key.scope = 'row';
    tr.append(key);
    if (manual) {
      for (const value of [row.name, row.quantity, row.category]) tr.append(element('td', display(value)));
      const label = row.source + ' · 第 ' + row.sourcePage + ' 页';
      const cell = element('td', label);
      if (row.sourceUrl?.startsWith('https://')) {
        const link = element('a', label);
        link.href = row.sourceUrl + '#page=' + row.sourcePage;
        link.target = '_blank';
        link.rel = 'noopener';
        cell.replaceChildren(link);
      }
      tr.append(cell);
    } else {
      const measures = (...values) => values.map(display).join(' × ');
      for (const value of [row.description, row.category, row.model,
        measures(row.lengthInches, row.widthInches, row.heightInches), row.weightPounds,
        measures(row.length, row.width, row.height) + ' / ' + display(row.weight)]) {
        tr.append(element('td', display(value)));
      }
    }
    return tr;
  }

  function render() {
    if (!records) return;
    const q = query.value.trim();
    const terms = normalize(q).split(/\s+/).filter(Boolean);
    const selectedModel = model?.value;
    const selectedCategory = category?.value;
    const matches = manual || terms.length ? records.filter(({ row, text }) =>
      (!selectedModel || row.model === selectedModel) &&
      (!selectedCategory || row.category === selectedCategory) &&
      terms.every(term => text.includes(term))) : [];
    if (!manual && q) {
      matches.sort((a, b) => Number(b.row.sku.toUpperCase() === q.toUpperCase()) -
        Number(a.row.sku.toUpperCase() === q.toUpperCase()));
    }
    const pageCount = Math.max(1, Math.ceil(matches.length / pageSize));
    page = Math.min(page, pageCount);
    $('catalog-count').textContent = !manual && !q ? '请输入搜索词。'
      : matches.length ? '匹配 ' + matches.length.toLocaleString() + ' 条。' : '没有匹配的条目。';
    $('catalog-results').hidden = matches.length === 0;
    $('catalog-rows').replaceChildren(...matches.slice((page - 1) * pageSize, page * pageSize)
      .map(({ row }) => makeRow(row)));
    $('catalog-pagination').hidden = pageCount === 1;
    $('catalog-page').textContent = '第 ' + page + ' / ' + pageCount + ' 页';
    $('catalog-prev').disabled = page === 1;
    $('catalog-next').disabled = page === pageCount;
  }

  function update(reset) {
    if (reset) page = 1;
    render();
  }

  $('catalog-form').addEventListener('submit', event => { event.preventDefault(); update(true); });
  query.addEventListener('input', () => update(true));
  model?.addEventListener('change', () => update(true));
  category?.addEventListener('change', () => update(true));
  $('catalog-prev').addEventListener('click', () => { page = Math.max(1, page - 1); update(false); });
  $('catalog-next').addEventListener('click', () => { page++; update(false); });
  async function savePreferences(value) {
    pending?.abort();
    const previous = storage.choice;
    const remembered = storage.save(value);
    preferences.close();
    const cleanup = previous?.localDb && !value.localDb
      ? storage.clear().then(() => true, () => false) : Promise.resolve(true);
    await sync();
    if (!remembered) syncStatus.textContent += ' 浏览器未能保存许可设置。';
    if (!(await cleanup)) syncStatus.textContent += ' 本地目录清理尚未完成。';
  }
  const decline = () => savePreferences({ cookies: false, localDb: false });
  $('open-catalog-preferences').addEventListener('click', showPreferences);
  $('save-catalog-preferences').addEventListener('click', () => savePreferences({
    cookies: $('allow-cookies').checked, localDb: $('allow-local-db').checked
  }));
  $('decline-catalog-preferences').addEventListener('click', decline);
  preferences.addEventListener('cancel', event => {
    if (storage.choice) return;
    event.preventDefault();
    void decline();
  });
  retry.addEventListener('click', sync);
  window.addEventListener('storage', async event => {
    if (event.key !== storage.preferenceKey && event.key !== null) return;
    const previous = storage.choice;
    storage.refreshChoice();
    pending?.abort();
    if (storage.choice && preferences.open) preferences.close();
    if (!storage.choice) showPreferences();
    if (previous?.localDb && !storage.choice?.localDb) {
      try { await storage.clear(); } catch { /* The tab that changed the choice also clears the cache. */ }
    }
    void sync();
  });
  if (!storage.choice) showPreferences();
  void sync();
})();
