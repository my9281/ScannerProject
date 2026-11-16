'use strict';
(() => {
  const $ = id => document.getElementById(id), token = sessionStorage.getItem('accountToken') || '';
  let revision = null, enabled = false, busy = false, before = 0, next = 0;
  const date = value => value ? new Date(value).toLocaleString() : '—';
  function message(text, error = false) { $('message').textContent = text; $('message').className = error ? 'error' : ''; }
  async function api(path, method = 'GET', body) {
    const response = await fetch('/api/receiving-sync/' + path, { method, cache: 'no-store', headers: {
      Authorization: 'Bearer ' + token, ...(body ? { 'Content-Type': 'application/json' } : {}) }, ...(body ? { body: JSON.stringify(body) } : {}) });
    const result = await response.json().catch(() => ({}));
    if (!response.ok) throw new Error(result.message || '请求失败（HTTP ' + response.status + '），请检查数据库迁移 022/023。');
    return result;
  }
  function controls() {
    document.querySelectorAll('button').forEach(b => { b.disabled = busy; });
    $('fields').disabled = busy || revision === null; $('run').disabled = busy || !enabled; $('next').disabled = busy || !next;
  }
  async function run(action) { if (busy) return; busy = true; controls(); try { await action(); } catch (e) { message(e.message, true); } finally { busy = false; controls(); } }
  async function status(fill = false) {
    const s = await api('settings'); enabled = s.enabled;
    if (fill) {
      revision = s.revision;
      for (const id of ['username', 'siteId', 'intervalSeconds', 'trackIntervalSeconds']) $(id).value = s[id];
      for (const id of ['enabled', 'autoTrack']) $(id).checked = s[id];
      $('password').value = ''; $('clearPassword').checked = false; $('hasPassword').textContent = s.hasPassword ? '密码已保存' : '尚无密码';
    }
    $('status').textContent = '客户同步：' + (s.enabled ? '已启用' : '已关闭') + '\n下次到期：' + date(s.nextRunAt)
      + '\n最近尝试：' + date(s.lastAttemptAt) + '\n最近成功：' + date(s.lastSuccessAt)
      + '\n最近成功快照：' + s.lastRowCount + ' 条关联记录，其中资料异常 ' + s.lastIssueCount + ' 条'
      + '\n最近错误：' + (s.lastError || '无') + '\n当前数据库配置版本：' + s.revision;
  }
  async function rows() {
    const data = await api('forecasts?before=' + before); $('rows').replaceChildren();
    next = data.length === 100 ? data[data.length - 1].id : 0;
    for (const row of data) {
      const tr = document.createElement('tr');
      for (const text of [row.rma, row.carrier, row.trackingNumber, row.issue || '—', date(row.lastSeenAt)]) {
        const td = document.createElement('td'); td.textContent = text; tr.append(td);
      }
      $('rows').append(tr);
    }
  }
  $('settings').addEventListener('submit', event => { event.preventDefault(); run(async () => {
    const body = { revision, password: $('password').value || null, clearPassword: $('clearPassword').checked };
    for (const id of ['username', 'siteId']) body[id] = $(id).value.trim();
    for (const id of ['intervalSeconds', 'trackIntervalSeconds']) body[id] = Number($(id).value);
    for (const id of ['enabled', 'autoTrack']) body[id] = $(id).checked;
    const result = await api('settings', 'PUT', body); $('password').value = ''; await status(true); message(result.message);
  }); });
  $('reload').addEventListener('click', () => run(() => status(true)));
  $('run').addEventListener('click', () => run(async () => { const result = await api('run', 'POST'); await status(); message(result.message); }));
  $('first').addEventListener('click', () => run(async () => { before = 0; await rows(); await status(); }));
  $('next').addEventListener('click', () => run(async () => { before = next; await rows(); }));
  if (!token) { location.replace('/login.html'); return; }
  run(async () => { await status(true); await rows(); });
  setInterval(() => { if (!document.hidden) run(async () => { await status(); await rows(); }); }, 15000);
})();
