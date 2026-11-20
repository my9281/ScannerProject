'use strict';
(() => {
  const key = 'shippingCow.catalogConsent.v1';
  const cookie = 'shippingCowCatalogConsent';
  const database = 'shipping-cow-catalogs';
  const store = 'snapshots';
  let choice = read();
  let connection;

  function read() {
    try {
      const value = JSON.parse(localStorage.getItem(key));
      if (typeof value?.cookies === 'boolean' && typeof value.localDb === 'boolean') return value;
    } catch { /* Storage may be unavailable. */ }
    return null;
  }

  function save(value) {
    choice = value;
    let remembered = true;
    try { localStorage.setItem(key, JSON.stringify(value)); } catch { remembered = false; }
    const secure = location.protocol === 'https:' ? '; Secure' : '';
    try {
      if (value.cookies) {
        document.cookie = cookie + '=allowed; Path=/; Max-Age=31536000; SameSite=Lax' + secure;
      } else if (document.cookie.split(';').some(part => part.trim().startsWith(cookie + '='))) {
        document.cookie = cookie + '=; Path=/; Max-Age=0; SameSite=Lax' + secure;
      }
    } catch { /* Cookie restrictions must not interrupt catalog loading. */ }
    return remembered;
  }

  function open() {
    if (!connection) {
      connection = new Promise((resolve, reject) => {
        const request = indexedDB.open(database, 1);
        let expired = false;
        const fail = error => {
          clearTimeout(timer);
          expired = true;
          reject(error);
        };
        const timer = setTimeout(() => fail(new Error('本地数据库打开超时。')), 2000);
        request.onupgradeneeded = () => request.result.createObjectStore(store);
        request.onerror = () => fail(request.error);
        request.onblocked = () => fail(new Error('其他标签页正在使用本地数据库。'));
        request.onsuccess = () => {
          const db = request.result;
          clearTimeout(timer);
          if (expired) {
            db.close();
            return;
          }
          db.onversionchange = () => {
            db.close();
            connection = undefined;
          };
          resolve(db);
        };
      }).catch(error => {
        connection = undefined;
        throw error;
      });
    }
    return connection;
  }

  async function access(mode, operation) {
    const db = await open();
    return new Promise((resolve, reject) => {
      const tx = db.transaction(store, mode);
      const request = operation(tx.objectStore(store));
      const timer = setTimeout(() => {
        try { tx.abort(); } catch { /* The transaction may already be complete. */ }
        reject(new Error('本地数据库读写超时。'));
      }, 2000);
      tx.oncomplete = () => {
        clearTimeout(timer);
        resolve(request.result);
      };
      tx.onabort = () => {
        clearTimeout(timer);
        reject(tx.error || new Error('本地数据库事务已取消。'));
      };
    });
  }

  window.catalogStorage = {
    get choice() { return choice; },
    save,
    refreshChoice() { choice = read(); },
    preferenceKey: key,
    get: id => choice?.localDb ? access('readonly', table => table.get(id)) : Promise.resolve(),
    put: (id, snapshot) => choice?.localDb ? access('readwrite', table =>
      choice?.localDb ? table.put(snapshot, id) : table.get(id)) : Promise.resolve(),
    async clear() {
      const pending = connection;
      connection = undefined;
      if (pending) {
        try { (await pending).close(); } catch { /* No open connection to close. */ }
      }
      return new Promise((resolve, reject) => {
        const request = indexedDB.deleteDatabase(database);
        const timer = setTimeout(() => reject(new Error('本地数据库清理超时。')), 2000);
        request.onsuccess = () => { clearTimeout(timer); resolve(); };
        request.onerror = () => { clearTimeout(timer); reject(request.error); };
        request.onblocked = () => {
          clearTimeout(timer);
          reject(new Error('其他标签页正在使用本地数据库。'));
        };
      });
    }
  };
})();
