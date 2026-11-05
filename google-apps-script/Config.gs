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
  WEBAPP_ACCESS: 'ANYONE_WITH_LINK'
};

function getAppConfig() {
  if (!APP_CONFIG.ADMIN_PASSWORD) {
    Logger.log('WARNING: APP_CONFIG.ADMIN_PASSWORD is empty. Set it in Config.gs before production use.');
  }
  return APP_CONFIG;
}
