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
  length: 0,
  clear: () => {
    store.clear();
    mockStorage.length = 0;
  },
  getItem: (key: string) => store.get(key) ?? null,
  setItem: (key: string, value: string) => {
    store.set(key, String(value));
    mockStorage.length = store.size;
  },
  removeItem: (key: string) => {
    store.delete(key);
    mockStorage.length = store.size;
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
