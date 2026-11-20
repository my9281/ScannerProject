'use strict';
(() => {
  const $ = id => document.getElementById(id);
  const board = new URLSearchParams(location.search).get('view') === 'board';
  if (board) {
    $('heading').textContent = '所属域收货看板';
    $('description').textContent = '按所属域显示收货记录，Bluetti 预报匹配结果仅在 Bluetti 域展示。';
    $('rma').hidden = false;
  }
  $(board ? 'board-link' : 'pool-link').setAttribute('aria-current', 'page');
  let page = 0, busy = false, hasNext = false;
  async function load() {
    if (busy) return; busy = true;
    document.querySelectorAll('.workspace-content button').forEach(b => { b.disabled = true; });
    try {
      const response = await fetch('/api/carrier-receipts' + (board ? '/board' : '') + '?page=' + page + '&search=' + encodeURIComponent($('search').value.trim()),
        { cache: 'no-store' });
      const data = await response.json(); if (!response.ok) throw new Error(data.message || '加载失败，请检查数据库迁移 022–024。');
      $('rows').replaceChildren(); hasNext = data.length === 100;
      for (const row of data) {
        const tr = document.createElement('tr');
        const cells = [row.siteId, row.batchNumber, row.carrier + ' / ' + row.trackingNumber, row.receivedBy,
          new Date(row.scannedAt).toLocaleString(), new Date(row.uploadedAt).toLocaleString()];
        if (board) cells.push(row.rma || '—');
        for (const text of cells) {
          const td = document.createElement('td'); td.textContent = text; tr.append(td);
        }
        $('rows').append(tr);
      }
      $('message').textContent = data.length ? '扫描记录按服务器确认时间排序，显示浏览器当地时间。' : '没有符合条件的记录。';
      $('page').textContent = '第 ' + (page + 1) + ' 页';
    } catch (e) { $('message').textContent = e.message; }
    finally { busy = false; document.querySelectorAll('.workspace-content button').forEach(b => { b.disabled = false; }); $('previous').disabled = page === 0; $('next').disabled = !hasNext; }
  }
  $('filter').addEventListener('submit', e => { e.preventDefault(); if (!busy) { page = 0; load(); } });
  $('previous').addEventListener('click', () => { if (!busy && page > 0) { page--; load(); } });
  $('next').addEventListener('click', () => { if (!busy && hasNext) { page++; load(); } });
  load();
})();
