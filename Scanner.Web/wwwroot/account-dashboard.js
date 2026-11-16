'use strict';
(() => {
  const $ = id => document.getElementById(id), token = sessionStorage.getItem('accountToken') || '';
  const roles = { admin: '管理员', user: '普通用户', viewer: '只读用户' };
  let current = null, usersAfter = '0', nextAfter = '', usersBusy = false, resetUser = null;
  function notice(text, error = false) { $('message').textContent = text; $('message').className = 'notice' + (error ? ' error' : ''); }
  function expire() { sessionStorage.removeItem('accountToken'); location.replace('/login.html'); }
  async function api(path, method = 'GET', body) {
    let response;
    try {
      response = await fetch('/api/' + path, { method, cache: 'no-store', headers: { Authorization: 'Bearer ' + token,
        ...(body ? { 'Content-Type': 'application/json' } : {}) }, ...(body ? { body: JSON.stringify(body) } : {}) });
    } catch { throw new Error('无法连接服务器，请检查网络后重试。'); }
    const data = await response.json().catch(() => ({}));
    if (response.status === 401) { expire(); throw new Error('登录已失效，请重新登录。'); }
    if (!response.ok) {
      const fields = data.errors ? Object.values(data.errors).flat().join(' ') : '';
      throw new Error(data.message || fields || '请求失败（HTTP ' + response.status + '）');
    }
    return data;
  }
  function openDialog(id) {
    const dialog = $(id); dialog.querySelector('form').reset();
    const status = dialog.querySelector('.form-message'); status.textContent = ''; status.className = 'form-message';
    dialog.showModal();
  }
  for (const dialog of document.querySelectorAll('dialog')) {
    dialog.addEventListener('close', () => { dialog.querySelector('form').reset(); });
    dialog.querySelectorAll('[data-close]').forEach(b => b.addEventListener('click', () => {
      if (dialog.dataset.busy !== 'true') dialog.close();
    }));
    dialog.addEventListener('cancel', e => { if (dialog.dataset.busy === 'true') e.preventDefault(); });
  }
  async function submit(form, action) {
    const dialog = form.closest('dialog'), status = form.querySelector('.form-message');
    if (dialog.dataset.busy === 'true') return;
    dialog.dataset.busy = 'true'; form.querySelector('fieldset').disabled = true;
    status.textContent = '正在保存…'; status.className = 'form-message';
    try { await action(status); }
    catch (error) { status.textContent = error.message; status.className = 'form-message error'; }
    finally { dialog.dataset.busy = 'false'; form.querySelector('fieldset').disabled = false; }
  }
  async function load() {
    $('retry').hidden = true;
    try {
      current = await api('account/me');
      const permissions = new Set(current.permissions || []), admin = permissions.has('system.configure');
      $('username').textContent = current.username;
      $('displayName').textContent = current.displayName || current.username;
      $('domain').textContent = current.domain.domainName;
      $('create-domain').textContent = current.domain.domainName;
      $('role').textContent = roles[current.role] || current.role;
      $('sidebar-name').textContent = current.displayName || current.username;
      $('sidebar-role').textContent = roles[current.role] || current.role;
      $('avatar').textContent = [...current.username].slice(0, 2).join('').toUpperCase();
      $('expires').textContent = new Date(current.expiresAt).toLocaleString('zh-CN', { hour12: false });
      $('welcome-text').textContent = '你好，' + (current.displayName || current.username) + '。从这里进入你的业务工具。';
      $('permissions').replaceChildren();
      for (const permission of permissions) { const chip = document.createElement('span'); chip.textContent = permission; $('permissions').append(chip); }
      document.querySelectorAll('[data-admin]').forEach(el => { el.hidden = !admin; });
      document.querySelectorAll('[data-permission]').forEach(el => { el.hidden = !permissions.has(el.dataset.permission); });
      document.querySelectorAll('[data-account]').forEach(el => { el.hidden = false; });
      $('open-password').disabled = false;
      notice(admin ? '管理员工作空间 · 业务入口已就绪' : '工作空间已就绪 · 已展示当前账户可用入口');
    } catch (error) { notice(error.message, true); $('retry').hidden = false; }
  }
  $('retry').addEventListener('click', load);
  async function logout() {
    const buttons = [$('logout'), $('logout-mobile')].filter(Boolean); buttons.forEach(b => { b.disabled = true; });
    try { await api('account/logout', 'POST'); expire(); }
    catch (error) { notice('退出未确认：' + error.message + ' 可重试退出。', true); buttons.forEach(b => { b.disabled = false; }); }
  }
  $('logout').addEventListener('click', logout);
  $('logout-mobile')?.addEventListener('click', logout);
  $('open-password').addEventListener('click', () => openDialog('password-dialog'));
  $('open-create').addEventListener('click', () => openDialog('create-dialog'));
  $('password-form').addEventListener('submit', e => {
    e.preventDefault(); submit(e.currentTarget, async status => {
      const result = await api('account-management/password', 'POST', { currentPassword: $('current-password').value,
        newPassword: $('new-password').value, confirmPassword: $('confirm-password').value });
      sessionStorage.removeItem('accountToken'); e.target.reset(); status.textContent = result.message;
      $('open-password').disabled = true; setTimeout(() => location.replace('/login.html'), 1200);
    });
  });
  $('create-form').addEventListener('submit', e => {
    e.preventDefault(); submit(e.currentTarget, async status => {
      const result = await api('account-management/users', 'POST', { username: $('create-username').value.trim(),
        displayName: $('create-display').value.trim() || null, role: $('create-role').value,
        password: $('create-password').value, confirmPassword: $('create-confirm').value });
      $('create-password').value = $('create-confirm').value = '';
      status.textContent = '已创建用户 ' + result.username + '，可使用初始密码登录。';
      if (!$('user-panel').hidden) { usersAfter = '0'; await loadUsers(); }
    });
  });
  async function loadUsers() {
    if (usersBusy) return; usersBusy = true;
    $('users-next').disabled = $('users-first').disabled = true;
    try {
      const rows = await api('account-management/users?after=' + encodeURIComponent(usersAfter) + '&search=' + encodeURIComponent($('user-search').value.trim()));
      nextAfter = rows.length === 100 ? rows[rows.length - 1].id : '';
      $('user-rows').replaceChildren();
      for (const user of rows) {
        const row = document.createElement('tr');
        for (const text of [user.username, user.displayName || '—', roles[user.role] || user.role,
          ({ active: '正常', locked: '已锁定', disabled: '已停用' })[user.status] || user.status]) {
          const cell = document.createElement('td'); cell.textContent = text; row.append(cell);
        }
        const cell = document.createElement('td');
        if (user.id === String(current.userId)) cell.textContent = '当前账户';
        else {
          const button = document.createElement('button'); button.type = 'button'; button.className = 'text-button'; button.textContent = '重置密码';
          button.addEventListener('click', () => { resetUser = user; $('reset-name').textContent = user.username; openDialog('reset-dialog'); }); cell.append(button);
        }
        row.append(cell); $('user-rows').append(row);
      }
      $('user-message').textContent = rows.length ? '仅显示 ' + current.domain.domainName + ' 域的用户。' : '没有符合条件的用户。';
      $('user-message').className = 'form-message';
    } catch (error) { nextAfter = ''; $('user-message').textContent = error.message; $('user-message').className = 'form-message error'; }
    finally { usersBusy = false; $('users-next').disabled = !nextAfter; $('users-first').disabled = usersAfter === '0'; }
  }
  $('open-users').addEventListener('click', async () => {
    $('user-panel').hidden = !$('user-panel').hidden; $('open-users').setAttribute('aria-expanded', String(!$('user-panel').hidden));
    if (!$('user-panel').hidden) { await loadUsers(); $('user-panel').scrollIntoView({ behavior: 'smooth', block: 'nearest' }); }
  });
  $('close-users').addEventListener('click', () => { $('user-panel').hidden = true; $('open-users').setAttribute('aria-expanded', 'false'); $('open-users').focus(); });
  $('user-search-form').addEventListener('submit', e => { e.preventDefault(); if (!usersBusy) { usersAfter = '0'; loadUsers(); } });
  $('users-first').addEventListener('click', () => { if (!usersBusy) { usersAfter = '0'; loadUsers(); } });
  $('users-next').addEventListener('click', () => { if (!usersBusy && nextAfter) { usersAfter = nextAfter; loadUsers(); } });
  $('reset-form').addEventListener('submit', e => {
    e.preventDefault(); submit(e.currentTarget, async status => {
      if (!resetUser) throw new Error('请重新选择用户。');
      const result = await api('account-management/users/' + encodeURIComponent(resetUser.id) + '/password', 'POST', {
        newPassword: $('reset-password').value, confirmPassword: $('reset-confirm').value });
      e.target.reset(); status.textContent = result.message;
    });
  });
  document.querySelectorAll('.nav-link').forEach(link => link.addEventListener('click', () => {
    document.querySelectorAll('.nav-link').forEach(el => el.classList.toggle('active', el === link));
  }));
  if (!token) { expire(); return; }
  load();
})();
