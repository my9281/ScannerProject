'use strict';
(() => {
  const token = sessionStorage.getItem('accountToken') || '';
  const byId = id => document.getElementById(id);
  const buttons = ['connect', 'sheets', 'load', 'pull', 'push'];
  let state = null, busy = false, abort = null, editing = null;
  const notify = (text, error = false) => {
    byId('message').textContent = text;
    byId('message').className = 'message' + (error ? ' error' : '');
  };
  const controls = () => {
    byId('connect').disabled = busy || !state?.configured || state?.authMode === 'DirectToken';
    for (const id of ['sheets', 'pull', 'push']) byId(id).disabled = busy || !state?.connected;
    byId('load').disabled = busy;
    byId('cancel').disabled = !abort;
  };
  async function api(path, options = {}) {
    const response = await fetch('/api/tencent-docs/' + path, {
      ...options, cache: 'no-store', headers: { Authorization: 'Bearer ' + token, ...(options.body ? { 'Content-Type': 'application/json' } : {}) }
    });
    const result = await response.json().catch(() => ({}));
    if (response.status === 401) throw new Error('登录已失效，请重新登录 Scanner。');
    if (!response.ok) throw new Error(result.message || '请求失败（HTTP ' + response.status + '）');
    return result;
  }
  async function status() {
    state = await api('status');
    byId('configured').textContent = state.configured ? '已配置' : '未配置：请填写服务器 TencentDocs 配置并重启服务';
    const direct = state.authMode === 'DirectToken';
    byId('connected').textContent = state.connected ? (direct ? '已配置直接凭据' : '已连接') + (state.expiresAt ? ' · 访问令牌到期时间 ' + new Date(state.expiresAt).toLocaleString() + (direct ? '（到期后手动更新服务器配置）' : '（读取时自动刷新）') : '') : (direct ? '直接凭据未配置或已过期' : '尚未授权');
    byId('callback').textContent = state.redirectUri || '未配置';
    byId('target').textContent = state.encodedId + ' / ' + state.sheetId;
    controls();
  }
  async function run(action) {
    if (busy) return;
    busy = true; controls();
    try { await action(); }
    catch (error) { notify(error.name === 'AbortError' ? '读取已取消，未展示不完整结果。' : error.message, true); }
    finally { busy = false; abort = null; controls(); }
  }
  function cell(value) {
    if (value == null) return '';
    if (Array.isArray(value)) return value.map(cell).join(' / ');
    if (typeof value === 'object') {
      for (const key of ['text', 'label', 'name', 'title', 'value']) if (value[key] != null) return cell(value[key]);
      return JSON.stringify(value);
    }
    return String(value);
  }
  function render(records) {
    const fields = [...new Set(records.flatMap(record => Object.keys(record.values || {})))];
    const head = document.createElement('thead'), row = document.createElement('tr');
    for (const name of ['操作', '同步状态', '记录 ID', ...fields]) { const th = document.createElement('th'); th.textContent = name; row.append(th); }
    head.append(row);
    const body = document.createElement('tbody');
    for (const record of records) {
      const tr = document.createElement('tr');
      const action = document.createElement('td'), edit = document.createElement('button');
      edit.textContent = '编辑本地'; edit.addEventListener('click', () => openEdit(record)); action.append(edit); tr.append(action);
      const sync = { clean: '已同步', pending: '待推送', error: '推送失败', conflict: '存在冲突' }[record.syncStatus] || record.syncStatus || '—';
      const statusCell = document.createElement('td'); statusCell.textContent = sync; statusCell.title = record.lastError || ''; tr.append(statusCell);
      for (const [name, value] of [['记录 ID', record.recordID], ...fields.map(name => [name, record.values?.[name]])]) {
        const td = document.createElement('td');
        const ms = ['发货日期', '签收日期'].includes(name) ? Number(value) : NaN;
        td.textContent = Number.isFinite(ms) && ms > 0 && !Number.isNaN(new Date(ms).getTime()) ? new Date(ms).toLocaleDateString('zh-CN', { timeZone: 'Asia/Shanghai' }) : cell(value);
        tr.append(td);
      }
      body.append(tr);
    }
    if (!records.length) { const tr = document.createElement('tr'), td = document.createElement('td'); td.textContent = '暂无记录'; tr.append(td); body.append(tr); }
    byId('records').replaceChildren(head, body);
  }
  byId('connect').addEventListener('click', () => run(async () => {
    const result = await api('oauth/start', { method: 'POST' });
    const url = new URL(result.authorizationUrl);
    if (url.origin !== 'https://docs.qq.com' || url.pathname !== '/oauth/v2/authorize') throw new Error('授权地址无效。');
    location.assign(url.href);
  }));
  byId('sheets').addEventListener('click', () => run(async () => {
    notify('正在检查工作表…');
    const result = await api('sheets');
    byId('sheet-result').textContent = JSON.stringify(result, null, 2);
    notify('工作表查询成功。');
  }));
  async function loadLocal() {
    abort = new AbortController(); controls(); render([]);
    const records = [], ids = new Set(); let offset = 0;
    while (true) {
      notify('正在读取本地数据库，已获取 ' + records.length + ' 条记录…');
      const page = await api('records?offset=' + offset + '&limit=100', { signal: abort.signal });
      if (!Array.isArray(page.records)) throw new Error('服务器未返回记录列表。');
      for (const record of page.records) {
        if (!record.recordID || ids.has(record.recordID)) throw new Error('记录 ID 缺失或重复，请重新读取。');
        ids.add(record.recordID); records.push(record);
      }
      if (!page.hasMore) break;
      if (records.length >= 10000) throw new Error('测试页最多读取 10000 条，请缩小数据范围。');
      if (page.next <= offset) throw new Error('分页位置无效。');
      offset = page.next;
    }
    render(records); notify('本地数据共 ' + records.length + ' 条；查看过程未调用腾讯接口。');
    abort = null;
  }
  byId('load').addEventListener('click', () => run(loadLocal));
  byId('pull').addEventListener('click', () => run(async () => {
    notify('正在从腾讯同步入库…');
    const result = await api('sync/pull', { method: 'POST' });
    await loadLocal(); notify('同步入库完成，共 ' + result.pulled + ' 条；本地待推送改动已保留。');
  }));
  byId('push').addEventListener('click', () => run(async () => {
    notify('正在推送本地改动…');
    const result = await api('sync/push', { method: 'POST' });
    await loadLocal(); notify('已推送 ' + result.pushed + ' 条，冲突 ' + result.conflicts + ' 条，失败 ' + result.failed + ' 条。', result.conflicts + result.failed > 0);
  }));
  function openEdit(record) {
    if (busy) return;
    editing = record; byId('edit-values').value = JSON.stringify(record.values, null, 2);
    byId('edit-message').textContent = record.lastError || '';
    byId('conflict-detail').hidden = byId('rebase-label').hidden = record.syncStatus !== 'conflict';
    byId('remote-values').textContent = JSON.stringify(record.remoteValues || {}, null, 2);
    byId('rebase').checked = false;
    byId('edit-dialog').showModal();
  }
  byId('close-edit').addEventListener('click', () => byId('edit-dialog').close());
  byId('edit-form').addEventListener('submit', async event => {
    event.preventDefault(); if (!editing || busy) return;
    byId('save-edit').disabled = true;
    try {
      const values = JSON.parse(byId('edit-values').value);
      if (!values || Array.isArray(values) || typeof values !== 'object') throw new Error('字段值必须是 JSON 对象。');
      await api('records/' + encodeURIComponent(editing.recordID), { method: 'PATCH', body: JSON.stringify({ version: editing.version, values, rebase: byId('rebase').checked }) });
      byId('edit-dialog').close(); await run(loadLocal); notify('已保存到本地，等待推送。');
    } catch (error) { byId('edit-message').textContent = error.message; }
    finally { byId('save-edit').disabled = false; }
  });
  byId('cancel').addEventListener('click', () => abort?.abort());
  (async () => {
    if (!token) { location.replace('/login.html'); return; }
    try {
      const result = new URLSearchParams(location.search).get('oauth');
      history.replaceState(null, '', location.pathname);
      await status();
      await run(loadLocal);
      if (result === 'success') notify('腾讯文档授权成功，可以拉取数据。');
      else if (result === 'denied') notify('腾讯文档授权已取消，请重新连接。', true);
      else if (result === 'failed') notify('授权换取令牌失败，请核对应用配置、回调地址及服务器日志后重试。', true);
    } catch (error) { for (const id of buttons) byId(id).disabled = true; notify(error.message, true); }
  })();
})();
