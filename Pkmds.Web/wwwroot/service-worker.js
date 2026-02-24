// In development, always fetch from the network and do not enable offline support.
// This is because caching would make development more difficult (changes would not
// be reflected on the first load after each change).

self.addEventListener('install', event => {
    // Skip waiting to ensure immediate activation in development
    self.skipWaiting();
});

self.addEventListener('activate', event => {
    // Claim all clients immediately in development
    event.waitUntil(self.clients.claim());
});

self.addEventListener('fetch', event => {
    // In development, always fetch from network
    // This ensures changes are reflected immediately
    event.respondWith(fetch(event.request));
});
