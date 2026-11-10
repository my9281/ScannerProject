'use strict';
(() => {
  const byId = id => document.getElementById(id);
  let records = [];
  function render() {
    const pallet = byId('pallet').value, search = byId('search').value.trim().toLowerCase();
    const groups = new Map();
    for (const row of records) {
      if ((pallet && row.palletNumber !== Number(pallet)) || !row.sn.toLowerCase().includes(search)) continue;
      if (!groups.has(row.palletNumber)) groups.set(row.palletNumber, []);
      groups.get(row.palletNumber).push(row);
    }
    byId('groups').replaceChildren();
    let count = 0;
    for (const [number, rows] of [...groups].sort((a, b) => a[0] - b[0])) {
      count += rows.length;
      rows.sort((a, b) => a.scanTime.localeCompare(b.scanTime) || a.sn.localeCompare(b.sn));
      const details = document.createElement('details'), summary = document.createElement('summary');
      summary.textContent = '托盘 ' + number;
      const total = document.createElement('span'); total.textContent = rows.length + ' 条'; summary.append(total);
      const scroll = document.createElement('div'); scroll.className = 'scroll';
      const table = document.createElement('table');
      table.innerHTML = '<thead><tr><th>序号</th><th>SN</th><th>扫描日期</th><th>扫描时间</th></tr></thead>';
      const body = document.createElement('tbody');
      rows.forEach((row, index) => {
        const tr = document.createElement('tr');
        [index + 1, row.sn, row.scanDate.slice(0, 10), row.scanTime.replace('T', ' ')].forEach((value, column) => {
          const td = document.createElement('td'); td.textContent = String(value); if (column === 1) td.className = 'sn'; tr.append(td);
        });
        body.append(tr);
      });
      table.append(body); scroll.append(table); details.append(summary, scroll);
      details.open = groups.size === 1 || Boolean(search); byId('groups').append(details);
    }
    byId('message').className = '';
    byId('message').textContent = count ? `共 ${groups.size} 个托盘，${count} 条记录。` : '没有符合条件的扫描记录。';
  }
  async function load() {
    const token = sessionStorage.getItem('accountToken') || '';
    if (!token) { location.replace('/login.html'); return; }
    byId('refresh').disabled = true;
    byId('message').className = ''; byId('message').textContent = '正在读取数据…';
    try {
      const response = await fetch('/api/tester-pallet-view', { headers: { Authorization: 'Bearer ' + token }, cache: 'no-store' });
      const result = await response.json().catch(() => ({}));
      if (!response.ok) throw new Error(result.message || `查询失败（HTTP ${response.status}）`);
      if (!Array.isArray(result)) throw new Error('服务器返回的数据格式无效。');
      records = result; render();
    } catch (error) {
      records = []; byId('groups').replaceChildren();
      byId('message').className = 'error'; byId('message').textContent = error.message;
    } finally { byId('refresh').disabled = false; }
  }
  byId('filters').addEventListener('submit', event => { event.preventDefault(); render(); });
  byId('refresh').addEventListener('click', load);
  load();
})();
