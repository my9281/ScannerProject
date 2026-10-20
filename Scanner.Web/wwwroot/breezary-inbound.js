'use strict';
(() => {
  const byId = id => document.getElementById(id), token = sessionStorage.getItem('accountToken') || '';
  let cursor = '0', busy = false, count = 0, filter = {}, generation = 0;
  for (let number = 1; number <= 100; number++) {
    const option = document.createElement('option'); option.value = String(number); option.textContent = '位置 ' + number; byId('location').append(option);
  }
  async function load(reset) {
    if (busy) return;
    if (!token) { location.replace('/login.html'); return; }
    busy = true; byId('more').disabled = true;
    const current = ++generation;
    try {
      if (reset) {
        cursor = '0'; count = 0; byId('records').replaceChildren();
        filter = { location: byId('location').value, search: byId('search').value.trim() };
      }
      byId('message').textContent = '正在读取数据库…';
      const query = new URLSearchParams({ beforeId: cursor, limit: '100', search: filter.search });
      if (filter.location) query.set('location', filter.location);
      const response = await fetch('/api/breezary-inbound-scans?' + query, { headers: { Authorization: 'Bearer ' + token }, cache: 'no-store' });
      const result = await response.json().catch(() => ({}));
      if (!response.ok) throw new Error(result.message || '查询失败（HTTP ' + response.status + '）');
      if (!Array.isArray(result.records)) throw new Error('服务器返回的记录格式无效。');
      if (current !== generation) return;
      for (const row of result.records) {
        const tr = document.createElement('tr');
        for (const value of [row.cacheLocation, row.barcode, new Date(row.scannedAt).toLocaleString('zh-CN'), new Date(row.uploadedAt).toLocaleString('zh-CN'), row.deviceId, row.scanId]) {
          const td = document.createElement('td'); td.textContent = String(value ?? ''); tr.append(td);
        }
        byId('records').append(tr);
      }
      count += result.records.length; cursor = String(result.nextId || 0);
      byId('message').textContent = count ? '已显示 ' + count + ' 条扫描记录。' : '暂无上传记录。';
      byId('more').disabled = !result.hasMore;
    } catch (error) { byId('message').textContent = error.message; }
    finally { busy = false; }
  }
  byId('filters').addEventListener('submit', event => { event.preventDefault(); load(true); });
  byId('more').addEventListener('click', () => load(false));
  load(true);
})();
