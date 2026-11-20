'use strict';
(() => {
  const menu = document.querySelector('.workspace-menu');
  const greeting = document.getElementById('home-account-greeting');
  const login = document.getElementById('home-login');
  if (!menu && !greeting) return;
  try { sessionStorage.removeItem('accountToken'); } catch { /* Legacy storage may be disabled. */ }

  const groups = [
    ['工作台', [
      ['/account.html', '账户概览'],
      ['/documents.html', '操作文档', 'checklist.read']
    ]],
    ['产品与配件', [
      ['/bluetti-products', 'BLUETTI 产品查询'],
      ['/manual-accessories', '手册配件查询'],
      ['/shipping-skus', '配件与发货 SKU']
    ]],
    ['收货与仓储', [
      ['/carrier-receipts.html', 'PDA 公共池', 'receiving.read'],
      ['/carrier-receipts.html?view=board', '所属域收货看板', 'receiving.read'],
      ['/carrier-receiving.html', '收货对账', 'system.configure'],
      ['/breezary-inbound.html', 'BreezaryStar 入库', 'system.configure'],
      ['/tester-pallets.html', '扫描托盘数据', 'system.configure']
    ]],
    ['系统管理', [
      ['/receiving-sync.html', 'BLUETTI 预报同步', 'system.configure'],
      ['/tencent-docs.html', '腾讯文档', 'system.configure'],
      ['/wpf-updates.html', 'WPF 更新发布', 'system.configure']
    ]]
  ];

  const current = location.pathname + (location.pathname === '/carrier-receipts.html'
    && new URLSearchParams(location.search).get('view') === 'board' ? '?view=board' : '');
  const roles = { admin: '管理员', user: '普通用户', viewer: '只读用户' };
  let nav;
  if (menu) {
    menu.innerHTML =
      '<a class="cow-brand" href="/"><img src="/shipping-cow-icon.png" width="145" height="138" alt="">' +
      '<span>维修管理平台<small>ShippingCow</small></span></a>' +
      '<details open><summary>菜单</summary><nav aria-label="管理系统导航"></nav></details>';
    const disclosure = menu.querySelector('details');
    nav = menu.querySelector('nav');
    const compact = matchMedia('(max-width: 760px)');
    const syncMenu = () => { disclosure.open = !compact.matches; };
    compact.addEventListener('change', syncMenu);
    syncMenu();
    menu.addEventListener('keydown', event => {
      if (event.key === 'Escape' && compact.matches && disclosure.open) {
        disclosure.open = false;
        disclosure.querySelector('summary').focus();
      }
    });
  }

  function element(tag, text, cssClass) {
    const node = document.createElement(tag);
    node.textContent = text;
    if (cssClass) node.className = cssClass;
    return node;
  }

  function render(account) {
    const permissions = new Set(account?.permissions ?? []);
    if (nav) {
      nav.replaceChildren();
      for (const [name, entries] of groups) {
        const visible = entries.filter(([, , permission]) => !permission || permissions.has(permission));
        if (!visible.length) continue;
        const section = document.createElement('section');
        section.append(element('h2', name, 'workspace-menu-label'));
        for (const [path, label] of visible) {
          const link = element('a', label);
          link.href = path;
          if (path === current) link.setAttribute('aria-current', 'page');
          section.append(link);
        }
        nav.append(section);
      }
      const footer = document.createElement('div');
      footer.className = 'workspace-menu-account';
      if (account) {
        footer.append(element('strong', account.displayName || account.username),
          element('span', account.domain.domainName + ' · ' + (roles[account.role] || account.role)));
        const logout = element('button', '退出登录');
        logout.type = 'button';
        const status = element('span', '');
        status.setAttribute('role', 'status');
        logout.addEventListener('click', async () => {
          logout.disabled = true;
          status.textContent = '';
          try {
            const response = await fetch('/api/account/logout', { method: 'POST', cache: 'no-store' });
            if (!response.ok) throw new Error();
            location.replace('/login.html');
          } catch {
            status.textContent = '退出失败，请重试。';
            logout.disabled = false;
          }
        });
        footer.append(logout, status);
      } else {
        const link = element('a', '登录');
        link.href = '/login.html';
        footer.append(link);
      }
      nav.append(footer);
    }

    if (greeting && account) {
      const hour = new Date().getHours();
      const phrase = hour < 5 || hour >= 23 ? '夜间好' : hour < 12 ? '早上好' : hour < 18 ? '下午好' : '晚上好';
      const domain = account.domain.domainName;
      greeting.textContent = (account.displayName || account.username) + '（' + domain + (domain.endsWith('域') ? '' : '域') + '），' + phrase;
      greeting.hidden = false;
      login.textContent = '进入工作台';
      login.href = '/account.html';
    }
  }

  render(null);
  (async () => {
    try {
      const response = await fetch('/api/account/me', { cache: 'no-store' });
      if (response.ok) {
        if (current === '/login.html') location.replace('/account.html');
        else render(await response.json());
      } else if (response.status === 401 && menu &&
        (current === '/account.html' || groups.some(([, entries]) =>
          entries.some(([path, , permission]) => path === current && permission)))) {
        location.replace('/login.html');
      }
    } catch { /* Public menu remains available if account verification fails. */ }
  })();
})();
