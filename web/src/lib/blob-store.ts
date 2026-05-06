/**
 * IndexedDB blob store for file-imported sounds. Every operation resolves to a
 * no-op when IndexedDB is unavailable, so callers never need to guard.
 */

const DB_NAME = "murmur-app"
const STORE = "custom-audio"

let dbPromise: Promise<IDBDatabase | null> | null = null

function open(): Promise<IDBDatabase | null> {
  if (dbPromise) return dbPromise
  dbPromise = new Promise((resolve) => {
    try {
      const req = indexedDB.open(DB_NAME, 1)
      req.onupgradeneeded = () => {
        if (!req.result.objectStoreNames.contains(STORE))
          req.result.createObjectStore(STORE)
      }
      req.onsuccess = () => resolve(req.result)
      req.onerror = () => resolve(null)
      req.onblocked = () => resolve(null)
    } catch {
      resolve(null)
    }
  })
  return dbPromise
}

/** False when persistence failed; the caller warns. */
export async function putBlob(id: string, blob: Blob): Promise<boolean> {
  const db = await open()
  if (!db) return false
  return new Promise((resolve) => {
    try {
      const tx = db.transaction(STORE, "readwrite")
      tx.objectStore(STORE).put(blob, id)
      tx.oncomplete = () => resolve(true)
      tx.onerror = () => resolve(false)
      tx.onabort = () => resolve(false)
    } catch {
      resolve(false)
    }
  })
}

/** Empty map on any failure. */
export async function getAllBlobs(): Promise<Map<string, Blob>> {
  const db = await open()
  const out = new Map<string, Blob>()
  if (!db) return out
  return new Promise((resolve) => {
    try {
      const tx = db.transaction(STORE, "readonly")
      const store = tx.objectStore(STORE)
      const keysReq = store.getAllKeys()
      const valsReq = store.getAll()
      tx.oncomplete = () => {
        const keys = keysReq.result
        const vals = valsReq.result as Blob[]
        keys.forEach((key, i) => {
          const val = vals[i]
          if (typeof key === "string" && val instanceof Blob) out.set(key, val)
        })
        resolve(out)
      }
      tx.onerror = () => resolve(out)
      tx.onabort = () => resolve(out)
    } catch {
      resolve(out)
    }
  })
}

/** Never rejects. */
export async function deleteBlob(id: string): Promise<void> {
  const db = await open()
  if (!db) return
  await new Promise<void>((resolve) => {
    try {
      const tx = db.transaction(STORE, "readwrite")
      tx.objectStore(STORE).delete(id)
      tx.oncomplete = () => resolve()
      tx.onerror = () => resolve()
      tx.onabort = () => resolve()
    } catch {
      resolve()
    }
  })
}
