'use strict';
(() => {
  const $ = id => document.getElementById(id), token = sessionStorage.getItem('accountToken') || '';
  let revision = 0, before = 0, next = 0, busy = false, loadedCarrier = '';
  const message = text => { $('message').textContent = text; };
  const date = value => value ? new Date(value).toLocaleString() : '—';
  async function api(path, method = 'GET', body) {
    const response = await fetch('/api/carrier-receiving/' + path, { method, cache: 'no-store',
      headers: { Authorization: 'Bearer ' + token, ...(body ? { 'Content-Type': 'application/json' } : {}) },
      ...(body ? { body: JSON.stringify(body) } : {}) });
    const data = await response.json().catch(() => ({}));
    if (!response.ok) throw new Error(data.message || '请求失败（HTTP ' + response.status + '）');
    return data;
  }
  async function run(action) {
    if (busy) return;
    busy = true;
    document.querySelectorAll('button, select').forEach(el => { el.disabled = true; });
    try { await action(); } catch (error) { message(error.message); }
    finally {
      busy = false; document.querySelectorAll('button, select').forEach(el => { el.disabled = false; });
      $('next').disabled = !next;
    }
  }
  async function settings() {
    loadedCarrier = ''; $('settingFields').disabled = true; $('secret').value = '';
    const carrier = $('settingCarrier').value;
    const data = await api('settings/' + carrier);
    revision = data.revision; $('enabled').checked = data.enabled; $('sandbox').checked = data.sandbox;
    $('clientId').value = data.clientId; $('clearSecret').checked = false;
    $('secretState').textContent = data.hasSecret ? '已保存密钥' : '尚未保存密钥';
    loadedCarrier = carrier; $('settingFields').disabled = false;
  }
  async function list() {
    const rows = await api('items?before=' + before + '&search=' + encodeURIComponent($('search').value.trim()));
    $('rows').replaceChildren(); next = rows.length === 100 ? rows[rows.length - 1].id : 0;
    for (const row of rows) {
      const tr = document.createElement('tr');
      if (row.delivered && !row.receivedAt) tr.className = 'warning';
      for (const text of [row.carrier + ' / ' + row.trackingNumber, row.reference, (row.carrierStatus || '尚未查询') + (row.lastError ? '\n最近错误：' + row.lastError : ''), date(row.checkedAt),
        date(row.receivedAt) + (row.receivedBy ? ' / ' + row.receivedBy : ''), row.reconciliation]) {
        const td = document.createElement('td'); td.textContent = text; tr.append(td);
      }
      const actions = document.createElement('td');
      const button = (label, action) => { const b = document.createElement('button'); b.type = 'button'; b.textContent = label; b.addEventListener('click', () => run(action)); actions.append(b); };
      const payload = { carrier: row.carrier, trackingNumber: row.trackingNumber };
      button('查询物流', async () => { await api('track', 'POST', payload); await list(); message('物流状态已更新。'); });
      if (!row.receivedAt) button('登记实收', async () => {
        if (!window.confirm('确认仓库已实际收到包裹 ' + row.trackingNumber + '？')) return;
        await api('items', 'POST', { ...payload, receive: true }); await list(); message('已登记实收。');
      });
      tr.append(actions); $('rows').append(tr);
    }
    if (!rows.length) message('当前条件下没有记录。');
  }
  $('settingCarrier').addEventListener('change', () => run(settings));
  $('reloadSettings').addEventListener('click', () => run(settings));
  $('settings').addEventListener('submit', event => { event.preventDefault(); run(async () => {
    if (loadedCarrier !== $('settingCarrier').value) throw new Error('请先重新加载配置。');
    await api('settings/' + loadedCarrier, 'PUT', { enabled: $('enabled').checked, sandbox: $('sandbox').checked,
      clientId: $('clientId').value.trim(), secret: $('secret').value || null, clearSecret: $('clearSecret').checked, revision });
    $('secret').value = ''; await settings(); message('配置已保存，下一次查询立即生效。');
  }); });
  $('scan').addEventListener('submit', event => { event.preventDefault(); run(async () => {
    const result = await api('items', 'POST', { carrier: $('carrier').value, trackingNumber: $('number').value.trim(),
      reference: $('reference').value.trim(), receive: $('mode').value === 'receive' });
    $('number').value = ''; $('reference').value = ''; before = 0; await list(); message(result.message); $('number').focus();
  }); });
  $('searchForm').addEventListener('submit', event => { event.preventDefault(); run(async () => { before = 0; await list(); }); });
  $('next').addEventListener('click', () => run(async () => { before = next; await list(); }));
  if (!token) { location.replace('/login.html'); return; }
  run(async () => { await settings(); await list(); });
})();
