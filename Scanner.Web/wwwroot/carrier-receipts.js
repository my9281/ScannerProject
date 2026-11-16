'use strict';
(() => {
  const $ = id => document.getElementById(id), token = sessionStorage.getItem('accountToken') || '';
  let page = 0, busy = false, hasNext = false;
  async function load() {
    if (busy) return; busy = true;
    document.querySelectorAll('button').forEach(b => { b.disabled = true; });
    try {
      const response = await fetch('/api/carrier-receipts?page=' + page + '&search=' + encodeURIComponent($('search').value.trim()),
        { cache: 'no-store', headers: { Authorization: 'Bearer ' + token } });
      const data = await response.json(); if (!response.ok) throw new Error(data.message || '加载失败，请检查数据库迁移 022–024。');
      $('rows').replaceChildren(); hasNext = data.length === 100;
      for (const row of data) {
        const tr = document.createElement('tr'); if (!row.hasForecast) tr.className = 'warning';
        for (const text of [row.siteId, row.batchNumber, row.carrier + ' / ' + row.trackingNumber, row.receivedBy,
          new Date(row.scannedAt).toLocaleString(), new Date(row.uploadedAt).toLocaleString(), row.hasForecast ? '已关联预报' : '待补预报']) {
          const td = document.createElement('td'); td.textContent = text; tr.append(td);
        }
        $('rows').append(tr);
      }
      $('message').textContent = data.length ? '扫描记录按服务器确认时间排序，显示浏览器当地时间。' : '没有符合条件的记录。';
      $('page').textContent = '第 ' + (page + 1) + ' 页';
    } catch (e) { $('message').textContent = e.message; }
    finally { busy = false; document.querySelectorAll('button').forEach(b => { b.disabled = false; }); $('previous').disabled = page === 0; $('next').disabled = !hasNext; }
  }
  $('filter').addEventListener('submit', e => { e.preventDefault(); if (!busy) { page = 0; load(); } });
  $('previous').addEventListener('click', () => { if (!busy && page > 0) { page--; load(); } });
  $('next').addEventListener('click', () => { if (!busy && hasNext) { page++; load(); } });
  if (!token) { location.replace('/login.html'); return; }
  load();
})();
