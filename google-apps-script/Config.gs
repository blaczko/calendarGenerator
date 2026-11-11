/**
 * Local / deploy configuration for Google Apps Script.
 *
 * 1. Set ADMIN_PASSWORD below (do not share this value publicly).
 * 2. Copy this file into your Apps Script project together with Code.gs,
 *    Customer.html and Admin.html.
 * 3. Deploy as Web app: Execute as Me, Who has access: Anyone with the link.
 *
 * Account: zataipixel@gmail.com
 */
var APP_CONFIG = {
  ROOT_FOLDER_NAME: 'Naptar',
  SPREADSHEET_NAME: 'Orders',

  // From your Google Drive / Sheets URLs:
  ROOT_FOLDER_ID: '15rbvA2819MJelitvQZzpYxlcrt_7VsZN',
  SPREADSHEET_ID: '1r9uaZ0LUZkKplQ5iSdQQIt0ePqrf01DIqgH8N-H_F1k',

  // >>> Add your admin password here before deploying <<<
  ADMIN_PASSWORD: '',

  OWNER_EMAIL: 'zataipixel@gmail.com',
  // Manifest access must be ANYONE_ANONYMOUS (not ANYONE = Google login required).
  WEBAPP_ACCESS: 'ANYONE_ANONYMOUS',

  // Facebook / WhatsApp / Messenger link preview (Open Graph)
  // CRITICAL: Facebook cannot read HtmlService iframe pages. Post:
  //   .../exec?page=share
  // Prefer a public image URL (GitHub raw / Pages / CDN). Drive "uc?export=view"
  // and Apps Script ?page=shareImage usually fail for Facebook.
  // Public JPG for Facebook (NOT a Drive link). GitHub Pages:
  SHARE_IMAGE_PUBLIC_URL: 'https://blaczko.github.io/calendarGenerator/share/og.jpg',
  // Optional: upload share-preview.jpg into the Naptar Drive root, or set ID
  // (used only by ?page=shareImage, never as a Drive open-in-browser link):
  SHARE_IMAGE_FILE_ID: '',
  OG_TITLE: 'Családi naptár rendelés',
  OG_DESCRIPTION: 'Személyre szabott, nyomtatott 12 oldalas A4 naptár a saját fényképeiddel — 60 RON.'
};

function getAppConfig() {
  if (!APP_CONFIG.ADMIN_PASSWORD) {
    Logger.log('WARNING: APP_CONFIG.ADMIN_PASSWORD is empty. Set it in Config.gs before production use.');
  }
  return APP_CONFIG;
}
