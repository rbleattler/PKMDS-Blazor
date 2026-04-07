// bank.js — IndexedDB-backed Pokemon Bank for PKMDS
// PKM bytes are stored as base64 strings to avoid binary limitations on some browsers.

const DB_NAME = "pkmds-bank";
const DB_VERSION = 1;
const STORE = "pokemon";

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
                store.createIndex("species", "meta.species", { unique: false });
                store.createIndex("isShiny", "meta.isShiny", { unique: false });
                store.createIndex("tag", "meta.tag", { unique: false });
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

export async function addPokemon(bytesBase64, meta) {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const tx = db.transaction(STORE, "readwrite");
        const store = tx.objectStore(STORE);
        const addedAt = new Date().toISOString();
        const request = store.add({ bytesBase64, meta, addedAt });
        request.onsuccess = (event) => resolve(event.target.result);
        request.onerror = (event) => reject(event.target.error);
    });
}

export async function getAllPokemon() {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const tx = db.transaction(STORE, "readonly");
        const store = tx.objectStore(STORE);
        const request = store.getAll();
        request.onsuccess = (event) => resolve(event.target.result);
        request.onerror = (event) => reject(event.target.error);
    });
}

export async function deletePokemon(id) {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const tx = db.transaction(STORE, "readwrite");
        const store = tx.objectStore(STORE);
        const request = store.delete(id);
        request.onsuccess = () => resolve();
        request.onerror = (event) => reject(event.target.error);
    });
}

export async function clearAll() {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const tx = db.transaction(STORE, "readwrite");
        const store = tx.objectStore(STORE);
        const request = store.clear();
        request.onsuccess = () => resolve();
        request.onerror = (event) => reject(event.target.error);
    });
}

export async function exportAll() {
    const entries = await getAllPokemon();
    const json = JSON.stringify(entries);
    const encoder = new TextEncoder();
    // Return Uint8Array directly — .NET marshals this to byte[] without an extra
    // Array.from() copy, which halves the memory needed for large exports.
    return encoder.encode(json);
}

export async function importAll(jsonBytes) {
    const decoder = new TextDecoder();
    const json = decoder.decode(new Uint8Array(jsonBytes));
    const entries = JSON.parse(json);

    const db = await openDb();
    return new Promise((resolve, reject) => {
        const tx = db.transaction(STORE, "readwrite");
        const store = tx.objectStore(STORE);

        for (const entry of entries) {
            // Strip the id so IndexedDB auto-assigns a new one (avoids conflicts).
            const { id: _id, ...entryWithoutId } = entry;
            store.add(entryWithoutId);
        }

        tx.oncomplete = () => resolve(entries.length);
        tx.onerror = (event) => reject(event.target.error);
    });
}
