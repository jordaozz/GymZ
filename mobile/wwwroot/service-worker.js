importScripts('./service-worker-assets.js');

const prefix = 'gymz-offline-';
const cacheName = prefix + self.assetsManifest.version;
const base = new URL('./', self.location.href);
const excluded = /(?:\.br|\.gz|\.map)$|(?:^|\/)service-worker(?:-assets|\.published)?\.js$/;
const urls = self.assetsManifest.assets.filter(asset => !excluded.test(asset.url))
  .map(asset => new URL(asset.url, base).href);
const local = new Set(urls);

self.addEventListener('install', event => {
  event.waitUntil(caches.open(cacheName).then(cache => cache.addAll(urls.map(url => new Request(url, { cache: 'reload' })))));
});

self.addEventListener('activate', event => {
  event.waitUntil((async () => {
    for (const key of await caches.keys()) {
      if (key.startsWith(prefix) && key !== cacheName) await caches.delete(key);
    }
    await self.clients.claim();
  })());
});

self.addEventListener('fetch', event => {
  const request = event.request;
  if (request.method !== 'GET') return;
  const url = new URL(request.url);
  if (url.origin !== base.origin || !url.pathname.startsWith(base.pathname)) return;
  const target = request.mode === 'navigate' ? new URL('index.html', base).href : url.href;
  if (!local.has(target)) return;
  event.respondWith(caches.open(cacheName).then(async cache => (await cache.match(target)) || fetch(request)));
});
