// Service worker registration
if ('serviceWorker' in navigator) {
    window._swRegistrationPromise = navigator.serviceWorker.register('service-worker.js', {updateViaCache: 'none'}).then(registration => {
        console.info('Service worker registered, scope:', registration.scope);
        setInterval(() => registration.update().catch(err => {
            // Safari may throw "newestWorker is null" — this is benign
            if (!(err.name === 'InvalidStateError' || (err.message && err.message.includes('newestWorker is null')))) {
                console.warn('Periodic registration.update() failed:', err);
            }
        }), 60 * 60 * 1000); // check for updates every hour
        registration.onupdatefound = () => {
            const installingWorker = registration.installing;
            installingWorker.onstatechange = () => {
                if (installingWorker.state === 'installed' && navigator.serviceWorker.controller) {
                    // Notify Blazor about the update
                    window.dispatchEvent(new CustomEvent('updateAvailable'));
                }
            };
        };
        return registration;
    }).catch(err => {
        console.error('Service worker registration failed:', err);
        return null;
    });
} else {
    console.warn('Service workers are not supported in this browser.');
    window._swRegistrationPromise = Promise.resolve(null);
}

// Listen for update events and forward to Blazor
window.addUpdateListener = () => {
    window.addEventListener('updateAvailable', () => {
        DotNet.invokeMethodAsync('Pkmds.Web', 'ShowUpdateMessage');
    });
};

// Proactively check for a service worker update.
// Returns: 'found' (update ready), 'none' (up to date), 'no-sw' (SW unavailable), 'error' (check/install failed)
window.checkForUpdates = async () => {
    const registration = await window._swRegistrationPromise;
    if (!registration) return 'no-sw';

    // A waiting worker was already downloaded but not yet activated — notify immediately.
    if (registration.waiting) {
        window.dispatchEvent(new CustomEvent('updateAvailable'));
        return 'found';
    }

    let updated;
    try {
        updated = await registration.update();
    } catch (err) {
        if (!(err.name === 'InvalidStateError' || (err.message && err.message.includes('newestWorker is null')))) {
            console.warn('Manual update check failed:', err);
        }
        return 'error';
    }

    if (updated.waiting) {
        window.dispatchEvent(new CustomEvent('updateAvailable'));
        return 'found';
    }

    if (!updated.installing) {
        return 'none';
    }

    // New SW is installing — wait for it to fully succeed or fail before returning.
    // This prevents a silent no-feedback state when the install errors (e.g. SRI hash mismatch
    // during a fresh deployment before CDN has fully propagated the new asset files).
    const installing = updated.installing;
    return new Promise((resolve) => {
        const timeoutId = setTimeout(() => resolve('error'), 30000);
        installing.addEventListener('statechange', function () {
            if (installing.state === 'installed') {
                clearTimeout(timeoutId);
                window.dispatchEvent(new CustomEvent('updateAvailable'));
                resolve('found');
            } else if (installing.state === 'redundant') {
                clearTimeout(timeoutId);
                resolve('error');
            }
        });
    });
};

