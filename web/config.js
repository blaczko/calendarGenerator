/** Public runtime config for the Cloudflare-hosted frontend. */
window.APP_CONFIG = {
  apiBase: '/api',
  // Public site origin (Cloudflare Pages).
  publicOrigin: 'https://naptar-web.pages.dev',
  // Used only for preview iframe (still served by Apps Script HtmlService).
  gasExecUrl:
    'https://script.google.com/macros/s/AKfycbwx0WrFHBjckGTiAsfqPCZbgBfCyPQeT64Xv1cuoC7Tz-oc0RlEn-h6I6j0-hnrOSwa/exec'
};
