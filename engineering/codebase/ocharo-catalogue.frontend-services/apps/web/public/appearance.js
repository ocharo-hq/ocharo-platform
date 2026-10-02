// Classic, blocking, same-origin boot script: no inline script or CSP relaxation.
(() => {
  let saved = 'system';
  try {
    saved = localStorage.getItem('ocharo.catalogue.theme') || 'system';
  } catch {
    /* Storage is optional. */
  }
  const dark = saved === 'dark' || (saved !== 'light' && window.matchMedia('(prefers-color-scheme: dark)').matches);
  document.documentElement.classList.toggle('dark', dark);
  document.documentElement.classList.toggle('light', !dark);
})();
