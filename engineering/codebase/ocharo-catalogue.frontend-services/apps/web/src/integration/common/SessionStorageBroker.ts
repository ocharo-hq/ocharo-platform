import type { StorageBroker } from '@wow-two-beta/fe/foundation/storage';

/** Resolves this tab's `sessionStorage`, or null when the browser withholds it (private mode, a security exception). */
function resolveSessionStorage(): Storage | null {
  if (typeof window === 'undefined') return null;
  try {
    return window.sessionStorage;
  } catch {
    return null;
  }
}

/**
 * The SDK storage seam over this tab's `sessionStorage`, for values that must not outlive the tab: JSON as the wire
 * format, every failure a miss, the store resolved per call. It mirrors the SDK's `localStorageStorageBroker`, which
 * ships no session-scoped sibling yet; it moves to the SDK when one lands.
 */
export const sessionStorageBroker: StorageBroker = {
  read<T>(key: string): T | null {
    const store = resolveSessionStorage();
    if (store === null) return null;
    try {
      const raw = store.getItem(key);
      return raw === null ? null : (JSON.parse(raw) as T);
    } catch {
      return null;
    }
  },
  write<T>(key: string, value: T): boolean {
    const store = resolveSessionStorage();
    if (store === null) return false;
    try {
      const serialized = JSON.stringify(value);
      if (serialized === undefined) return false;
      store.setItem(key, serialized);
      return true;
    } catch {
      return false;
    }
  },
  remove(key: string): boolean {
    const store = resolveSessionStorage();
    if (store === null) return false;
    try {
      store.removeItem(key);
      return true;
    } catch {
      return false;
    }
  },
};
