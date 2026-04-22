// Force a clean reload: unregister all service workers and delete every cache,
// then hard-reload the page. Used by the "Clear App Cache" button in Settings as
// an escape hatch when a deploy's service-worker update fails to invalidate the
// previous cache (stale JSON data, old WASM, etc.).
window.clearAppCacheAndReload = async function () {
    try {
        if ('serviceWorker' in navigator) {
            const registrations = await navigator.serviceWorker.getRegistrations();
            await Promise.all(registrations.map(r => r.unregister()));
        }
        if ('caches' in window) {
            const keys = await caches.keys();
            await Promise.all(keys.map(k => caches.delete(k)));
        }
    } catch (err) {
        console.error('clearAppCacheAndReload: cleanup failed', err);
    } finally {
        // location.reload(true) was removed from the spec; use a cache-busting
        // replace() to force a fresh page fetch.
        const bust = Date.now();
        const sep = window.location.href.includes('?') ? '&' : '?';
        window.location.replace(`${window.location.href.replace(/[?&]_cb=\d+/g, '')}${sep}_cb=${bust}`);
    }
};
