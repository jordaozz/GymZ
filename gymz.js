(() => {
  const key = 'backup';
  const recovery = 'prior-backup';
  let connection;
  let app;
  let installPrompt;
  let depth = 0;
  let movingBack = false;
  let pendingScreen;
  const pageKey = Math.random().toString(36).slice(2);
  history.scrollRestoration = 'manual';
  const today = () => {
    const date = new Date();
    const two = (value) => String(value).padStart(2, '0');
    return [date.getFullYear(), two(date.getMonth() + 1), two(date.getDate())].join('-');
  };
  const weekStart = () => {
    const date = new Date();
    date.setDate(date.getDate() - (date.getDay() + 6) % 7);
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  };
  const currentDrafts = value => {
    const byDate = value?.day ? { [value.day]: value.items } : value ?? {};
    const first = weekStart();
    const last = today();
    return Object.fromEntries(Object.entries(byDate).filter(([date]) => date >= first && date <= last));
  };
  const state = () => app?.invokeMethodAsync('DeviceState', today(), !!navigator.serviceWorker?.controller, !!installPrompt);
  const database = () => connection ??= new Promise((resolve, reject) => {
    const request = indexedDB.open('gymz', 1);
    request.onupgradeneeded = () => request.result.createObjectStore('data');
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
    request.onblocked = () => reject(new Error('gymz-storage-blocked'));
  });
  const access = async (key, mode, read) => {
    const db = await database();
    return new Promise((resolve, reject) => {
      const tx = db.transaction('data', mode);
      const store = tx.objectStore('data');
      const request = store.get(key);
      let value;
      request.onsuccess = () => {
        try { value = read(request.result ?? null, store); }
        catch (error) { reject(error); tx.abort(); }
      };
      tx.oncomplete = () => resolve(value);
      tx.onabort = () => reject(tx.error ?? new Error('gymz-save-failed'));
    });
  };
  const get = name => access(name, 'readonly', value => value);
  window.addEventListener('beforeinstallprompt', event => {
    event.preventDefault();
    installPrompt = event;
    state();
  });
  window.addEventListener('appinstalled', () => { installPrompt = null; state(); });
  window.addEventListener('popstate', event => {
    const former = depth;
    depth = event.state?.gymzPage === pageKey ? event.state.gymzDepth : 0;
    if (movingBack) {
      movingBack = false;
      if (pendingScreen) {
        const queued = pendingScreen;
        pendingScreen = null;
        window.gymz.screen(...queued);
      }
    } else if (depth < former) {
      app?.invokeMethodAsync('DeviceBack', depth);
    } else if (depth > former) {
      depth = former;
      history.replaceState({ ...history.state, gymzPage: pageKey, gymzDepth: depth }, '');
    }
  });
  const moveFocus = (id, reset = false) => requestAnimationFrame(() => {
    if (reset && document.activeElement?.matches('input, textarea, select')) document.activeElement.blur();
    const target = id && document.getElementById(id);
    if (target) {
      if (!reset && target.contains(document.activeElement)) return;
      target.scrollIntoView({ block: 'center' });
      target.focus({ preventScroll: true });
    } else if (reset) {
      window.scrollTo(0, 0);
      document.querySelector('main h1')?.focus({ preventScroll: true });
    }
  });
  const sizeForKeyboard = () => {
    const viewport = window.visualViewport;
    const keyboard = viewport && viewport.scale < 1.05 && document.activeElement?.matches('input:not([type=checkbox]),textarea');
    const offset = keyboard ? Math.max(0, window.innerHeight - viewport.height - viewport.offsetTop) : 0;
    document.documentElement.style.setProperty('--keyboard-inset', offset > 60 ? offset + 'px' : '0px');
  };
  window.gymz = {
    today,
    load: () => get(key),
    hasRestorePoint: async () => !!await get(recovery),
    restorePoint: () => get(recovery),
    save(content, expected, restoring, day, clearDraft) {
      return access(key, 'readwrite', (current, store) => {
        if (current !== expected) throw new Error('gymz-conflict');
        if (restoring && current) store.put(current, recovery);
        if (restoring) store.delete('drafts');
        else if (clearDraft) {
          const request = store.get('drafts');
          request.onsuccess = () => {
            const drafts = currentDrafts(request.result);
            if (!drafts[day]) return;
            delete drafts[day][clearDraft];
            if (!Object.keys(drafts[day]).length) delete drafts[day];
            store.put(drafts, 'drafts');
          };
        }
        store.put(content, key);
      });
    },
    async draft(day, id) {
      const drafts = currentDrafts(await get('drafts'));
      return drafts[day]?.[id] ?? null;
    },
    writeDraft(day, id, draft) {
      return access('drafts', 'readwrite', (current, store) => {
        if (day < weekStart() || day > today()) return;
        const byDate = currentDrafts(current);
        const items = byDate[day] ??= {};
        if (draft) items[id] = draft;
        else delete items[id];
        if (!Object.keys(items).length) delete byDate[day];
        store.put(byDate, 'drafts');
      });
    },
    timer: () => localStorage.getItem('gymz-rest'),
    setTimer(value) {
      if (value) localStorage.setItem('gymz-rest', value);
      else localStorage.removeItem('gymz-rest');
    },
    autoRest: () => localStorage.getItem('gymz-auto-rest') === 'true',
    setAutoRest(value) { localStorage.setItem('gymz-auto-rest', String(value)); },
    start(reference) {
      app = reference;
      history.replaceState({ ...history.state, gymzPage: pageKey, gymzDepth: 0 }, '');
      window.visualViewport?.addEventListener('resize', sizeForKeyboard);
      window.visualViewport?.addEventListener('scroll', sizeForKeyboard);
      document.addEventListener('focusin', sizeForKeyboard);
      document.addEventListener('focusout', () => requestAnimationFrame(sizeForKeyboard));
      document.addEventListener('visibilitychange', () => { if (!document.hidden) state(); });
      window.addEventListener('pageshow', state);
      setInterval(() => { if (!document.hidden) state(); }, 20000);
      if ('serviceWorker' in navigator) {
        navigator.serviceWorker.addEventListener('controllerchange', state);
        navigator.serviceWorker.register('./service-worker.js').then(state).catch(() => {});
      }
      state();
    },
    screen(next, id) {
      if (movingBack) { pendingScreen = [next, id]; return; }
      if (next < depth) {
        movingBack = true;
        history.go(next - depth);
      } else if (next > depth) {
        for (let nextDepth = depth + 1; nextDepth <= next; nextDepth++) {
          history.pushState({ ...history.state, gymzPage: pageKey, gymzDepth: nextDepth }, '');
        }
        depth = next;
      }
      moveFocus(id, true);
    },
    focus: id => moveFocus(id),
    confirm: text => window.confirm(text),
    async install() {
      if (!installPrompt) return;
      await installPrompt.prompt();
      installPrompt = null;
      await state();
    },
    async keepData() {
      const stored = await navigator.storage?.persist?.();
      alert(stored ? 'O navegador concedeu armazenamento persistente.' :
        'Este navegador controla o espaço local. Use o backup para guardar uma cópia dos registros.');
    },
    download(name, type, value) {
      const url = URL.createObjectURL(new Blob([value], { type }));
      const anchor = Object.assign(document.createElement('a'), { href: url, download: name });
      document.body.append(anchor);
      anchor.click();
      anchor.remove();
      setTimeout(() => URL.revokeObjectURL(url), 60000);
    }
  };
})();
