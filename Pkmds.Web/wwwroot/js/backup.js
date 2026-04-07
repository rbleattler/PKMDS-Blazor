// backup.js — IndexedDB-backed save file backup manager for PKMDS
// Save file bytes are stored as base64 strings to avoid binary limitations on some browsers.

const DB_NAME = "pkmds-backups";
const DB_VERSION = 1;
const STORE = "backups";

// Shared connection promise — reused across operations to avoid opening a new
// connection for every CRUD call and to ensure versionchange events can close
// the connection cleanly so future schema upgrades are not blocked.
let _dbPromise = null;

function openDb() {
    if (_dbPromise) return _dbPromise;

    _dbPromise = new Promise((resolve, reject) => {
        const request = indexedDB.open(DB_NAME, DB_VERSION);

        request.onupgradeneeded = (event) => {
            const db = event.target.result;
            const oldVersion = event.oldVersion;

            // v1: initial schema
            if (oldVersion < 1) {
                const store = db.createObjectStore(STORE, { keyPath: "id", autoIncrement: true });
                store.createIndex("createdAt", "createdAt", { unique: false });
            }

            // Future versions: add migration steps here, e.g.:
            // if (oldVersion < 2) { ... }
        };

        request.onsuccess = (event) => {
            const db = event.target.result;

            // Reset when the DB is closed externally (e.g., browser clears storage).
            db.onclose = () => { _dbPromise = null; };

            // Allow other tabs to upgrade the schema without being blocked by
            // this open connection.
            db.onversionchange = () => {
                db.close();
                _dbPromise = null;
            };

            resolve(db);
        };

        request.onerror = (event) => {
            _dbPromise = null;
            reject(event.target.error);
        };
    });

    return _dbPromise;
}

export async function addBackup(bytesBase64, meta, source) {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const tx = db.transaction(STORE, "readwrite");
        const store = tx.objectStore(STORE);
        const createdAt = new Date().toISOString();
        const request = store.add({ bytesBase64, meta, createdAt, source });
        let newId;
        // Capture the auto-assigned ID from the request, but resolve only after
        // the transaction commits so callers observe durable state.
        request.onsuccess = (event) => { newId = event.target.result; };
        tx.oncomplete = () => resolve(newId);
        tx.onerror = (event) => reject(event.target.error);
        tx.onabort = (event) => reject(event.target.error ?? new Error("Transaction aborted"));
    });
}

// Returns all backup records WITHOUT bytesBase64 — lightweight for list display.
// This avoids transferring potentially hundreds of MB of base64 data to C# just
// to show a table of backup metadata.
export async function getBackupMetadata() {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const tx = db.transaction(STORE, "readonly");
        const store = tx.objectStore(STORE);
        const request = store.getAll();
        request.onsuccess = (event) => {
            const results = event.target.result.map(({ bytesBase64: _bytes, ...rest }) => rest);
            resolve(results);
        };
        request.onerror = (event) => reject(event.target.error);
    });
}

// Returns a single full backup record by ID (including bytesBase64) for restore/export.
export async function getBackup(id) {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const tx = db.transaction(STORE, "readonly");
        const store = tx.objectStore(STORE);
        const request = store.get(id);
        request.onsuccess = (event) => resolve(event.target.result ?? null);
        request.onerror = (event) => reject(event.target.error);
    });
}

export async function deleteBackup(id) {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const tx = db.transaction(STORE, "readwrite");
        const store = tx.objectStore(STORE);
        store.delete(id);
        tx.oncomplete = () => resolve();
        tx.onerror = (event) => reject(event.target.error);
        tx.onabort = (event) => reject(event.target.error ?? new Error("Transaction aborted"));
    });
}

export async function clearAll() {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const tx = db.transaction(STORE, "readwrite");
        const store = tx.objectStore(STORE);
        store.clear();
        tx.oncomplete = () => resolve();
        tx.onerror = (event) => reject(event.target.error);
        tx.onabort = (event) => reject(event.target.error ?? new Error("Transaction aborted"));
    });
}

export async function getCount() {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const tx = db.transaction(STORE, "readonly");
        const store = tx.objectStore(STORE);
        const request = store.count();
        request.onsuccess = (event) => resolve(event.target.result);
        request.onerror = (event) => reject(event.target.error);
    });
}

// Returns the IDs of the N oldest backups ordered by createdAt ascending.
// Used by the retention policy to prune excess backups.
export async function getOldestIds(count) {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const tx = db.transaction(STORE, "readonly");
        const store = tx.objectStore(STORE);
        const index = store.index("createdAt");
        const ids = [];
        const request = index.openCursor();
        request.onsuccess = (event) => {
            const cursor = event.target.result;
            if (cursor && ids.length < count) {
                ids.push(cursor.value.id);
                cursor.continue();
            } else {
                resolve(ids);
            }
        };
        request.onerror = (event) => reject(event.target.error);
    });
}
