// Mock localStorage polyfill for Node 22+ jsdom vitest runner
try {
  delete (globalThis as any).localStorage;
} catch {}

try {
  if (typeof window !== 'undefined') {
    delete (window as any).localStorage;
  }
} catch {}

const store = new Map<string, string>();
const mockStorage: Storage = {
  get length() {
    return store.size;
  },
  clear: () => {
    store.clear();
  },
  getItem: (key: string) => store.get(key) ?? null,
  setItem: (key: string, value: string) => {
    store.set(key, String(value));
  },
  removeItem: (key: string) => {
    store.delete(key);
  },
  key: (index: number) => Array.from(store.keys())[index] ?? null,
};

Object.defineProperty(globalThis, 'localStorage', {
  value: mockStorage,
  writable: true,
  configurable: true,
  enumerable: true,
});

if (typeof window !== 'undefined') {
  Object.defineProperty(window, 'localStorage', {
    value: mockStorage,
    writable: true,
    configurable: true,
    enumerable: true,
  });
}
