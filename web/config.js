/** Public runtime config for the Cloudflare-hosted frontend. */
window.APP_CONFIG = {
  apiBase: '/api',
  // Public site origin (Cloudflare Pages).
  publicOrigin: 'https://naptar-web.pages.dev',
  // Used only for preview iframe (still served by Apps Script HtmlService).
  gasExecUrl:
    'https://script.google.com/macros/s/AKfycbyYmQGgwFshrVNfoZ4kX0KY8A4VJPndvfwmS9qFssUMi9BpCfsSf4zMNA2YFzms_bib/exec'
};
