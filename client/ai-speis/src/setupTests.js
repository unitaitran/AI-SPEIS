// jest-dom adds custom jest matchers for asserting on DOM nodes.
// allows you to do things like:
// expect(element).toHaveTextContent(/react/i)
// learn more: https://github.com/testing-library/jest-dom
import '@testing-library/jest-dom';

class MemoryStorage {
  constructor() {
    this.store = new Map();
  }

  getItem(key) {
    return this.store.has(key) ? this.store.get(key) : null;
  }

  setItem(key, value) {
    this.store.set(String(key), String(value));
  }

  removeItem(key) {
    this.store.delete(String(key));
  }

  clear() {
    this.store.clear();
  }

  key(index) {
    return Array.from(this.store.keys())[index] ?? null;
  }

  get length() {
    return this.store.size;
  }
}

if (!globalThis.localStorage) {
  globalThis.localStorage = new MemoryStorage();
}

if (!window.localStorage) {
  window.localStorage = globalThis.localStorage;
}

