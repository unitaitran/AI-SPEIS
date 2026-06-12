export const NAVIGATION_EVENT = 'app:navigate';

export function navigate(path, { replace = false } = {}) {
  const method = replace ? 'replaceState' : 'pushState';
  window.history[method]({}, '', path);
  window.dispatchEvent(new Event(NAVIGATION_EVENT));
}
