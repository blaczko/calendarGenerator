/**
 * Google Apps Script multi-step order API (Drive + Sheets).
 * Requires Config.gs. Redeploy (new version) after updating this file.
 *
 * Pages (?page=):
 *   customer (default) – order wizard
 *   admin              – admin dashboard
 *   review             – order review / edit (token or orderId)
 *   lookup             – enter order number → opens review
 *
 * Office walk-in flag (customer page):
 *   ?officeorder=true
 * Marks the order as directFromOffice in megrendeles.json + Sheets.
 * Office UI is shown only when this flag is present.
 */

var MONTH_FOLDERS = [
  '01_Januar', '02_Februar', '03_Marcius', '04_Aprilis', '05_Majus', '06_Junius',
  '07_Julius', '08_Augusztus', '09_Szeptember', '10_Oktober', '11_November', '12_December'
];
var MAX_PHOTOS_PER_MONTH = 6;
var PREVIEW_FILE_NAME = 'preview_strip.jpg';

function cfg_() {
  return getAppConfig();
}

/** Tracking/noise query keys that must not affect routing or app state. */
var IGNORED_QUERY_KEYS_ = {
  fbclid: true,
  gclid: true,
  dclid: true,
  msclkid: true,
  twclid: true,
  mc_cid: true,
  mc_eid: true
};

function isIgnoredQueryKey_(key) {
  var k = String(key || '').toLowerCase();
  if (IGNORED_QUERY_KEYS_[k]) return true;
  if (k.indexOf('utm_') === 0) return true;
  return false;
}

/** Return request params without Facebook/ads tracking noise (fbclid, utm_*, …). */
function cleanRequestParams_(e) {
  var raw = (e && e.parameter) || {};
  var out = {};
  for (var key in raw) {
    if (!Object.prototype.hasOwnProperty.call(raw, key)) continue;
    if (isIgnoredQueryKey_(key)) continue;
    out[key] = raw[key];
  }
  return out;
}

/**
 * JSON API for external frontends (Cloudflare Pages → Worker/Pages Function → here).
 * Body: { "action": "createOrder", "args": [ payload ] }
 * Response: { "ok": true, "result": ... } or { "ok": false, "error": "..." }
 */
function doPost(e) {
  return apiJsonRespond_(function () {
    if (!e || !e.postData || !e.postData.contents) {
      throw new Error('Hiányzó kérés törzs.');
    }
    var body = JSON.parse(e.postData.contents);
    var action = String((body && body.action) || '');
    var args = (body && body.args) || [];
    if (!Array.isArray(args)) args = [args];
    return dispatchExternalApi_(action, args);
  });
}

function apiJsonRespond_(fn) {
  try {
    var result = fn();
    return ContentService
      .createTextOutput(JSON.stringify({ ok: true, result: result }))
      .setMimeType(ContentService.MimeType.JSON);
  } catch (err) {
    return ContentService
      .createTextOutput(JSON.stringify({
        ok: false,
        error: String((err && err.message) || err || 'Ismeretlen hiba')
      }))
      .setMimeType(ContentService.MimeType.JSON);
  }
}

function dispatchExternalApi_(action, args) {
  switch (action) {
    case 'getPublicConfig':
      return getPublicConfig();
    case 'createOrder':
      return createOrder(args[0]);
    case 'submitOrder':
      return submitOrder(args[0]);
    case 'getOrderByToken':
      return getOrderByToken(args[0]);
    case 'getOrderReviewByToken':
      return getOrderReviewByToken(args[0]);
    case 'getOrderReviewByOrderId':
      return getOrderReviewByOrderId(args[0]);
    case 'updateOrder':
      return updateOrder(args[0], args[1]);
    case 'updateOrderByOrderId':
      return updateOrderByOrderId(args[0], args[1]);
    case 'finalizeOrder':
      return finalizeOrder(args[0], args[1]);
    case 'uploadPhoto':
      return uploadPhoto(args[0], args[1], args[2]);
    case 'uploadPhotosBatch':
      return uploadPhotosBatch(args[0], args[1], args[2]);
    case 'uploadPhotosBatchByOrderId':
      return uploadPhotosBatchByOrderId(args[0], args[1], args[2]);
    case 'deletePhoto':
      return deletePhoto(args[0], args[1]);
    case 'deletePhotoByOrderId':
      return deletePhotoByOrderId(args[0], args[1]);
    case 'adminLogin':
      return adminLogin(args[0]);
    case 'adminListOrders':
      return adminListOrders(args[0], args[1]);
    case 'adminSetStatus':
      return adminSetStatus(args[0], args[1], args[2]);
    default:
      throw new Error('Ismeretlen művelet: ' + action);
  }
}

function doGet(e) {
  var params = cleanRequestParams_(e);
  var page = params.page || 'customer';
  // Reuse cleaned params for downstream helpers that read e.parameter.
  e = e || {};
  e.parameter = params;

  // Lightweight JSON ping for the Cloudflare proxy health check.
  if (params.api === 'ping') {
    return apiJsonRespond_(function () {
      return { ok: true, service: 'naptar-api', ts: new Date().toISOString() };
    });
  }

  // Serve preview image as HTML (Drive hotlinks often break in browsers).
  if (page === 'previewServe') {
    return servePreviewStrip_(e);
  }

  // Open Graph / social crawlers need a raw image URL (not an HTML page).
  if (page === 'shareImage') {
    return serveShareImage_();
  }

  // Lightweight share landing (better link preview target than deep wizard URLs).
  if (page === 'share') {
    return serveShareLanding_();
  }

  // Review + lookup live inside Customer.html so only Customer/Admin need to exist in the project.
  var file = page === 'admin' ? 'Admin' : 'Customer';

  var template = HtmlService.createTemplateFromFile(file);
  var config = cfg_();
  var webAppUrl = '';
  try {
    webAppUrl = ScriptApp.getService().getUrl() || '';
  } catch (err) {
    webAppUrl = '';
  }

  var share = buildShareMeta_(webAppUrl, page);

  // Everything the HTML needs lives under `config` (avoid bare template vars — undeclared ones throw).
  template.config = {
    rootFolderName: config.ROOT_FOLDER_NAME,
    spreadsheetName: config.SPREADSHEET_NAME,
    ownerEmail: config.OWNER_EMAIL,
    maxPhotosPerMonth: MAX_PHOTOS_PER_MONTH,
    webAppUrl: webAppUrl,
    page: page,
    orderId: params.orderId || '',
    token: params.token || '',
    office: isOfficeParam_(e),
    ogTitle: share.title,
    ogDescription: share.description,
    ogImageUrl: share.imageUrl,
    ogUrl: share.url
  };

  return template
    .evaluate()
    .setTitle(share.title)
    .addMetaTag('viewport', 'width=device-width, initial-scale=1, viewport-fit=cover')
    .setXFrameOptionsMode(HtmlService.XFrameOptionsMode.ALLOWALL);
}

function buildShareMeta_(webAppUrl, page) {
  var config = cfg_();
  var title = config.OG_TITLE || 'Családi naptár rendelés';
  var description = config.OG_DESCRIPTION ||
    'Személyre szabott, nyomtatott 12 oldalas A4 naptár a saját fényképeiddel — 60 RON.';
  if (page === 'lookup') title = 'Rendelés keresése';
  if (page === 'review') title = 'Rendelés áttekintése';
  if (page === 'admin') title = 'Naptár admin';

  var url = webAppUrl || '';
  // Prefer the dedicated share landing for social posts.
  if (url) {
    url = url + (url.indexOf('?') >= 0 ? '&' : '?') + 'page=share';
  }
  var imageUrl = resolveShareImagePublicUrl_(webAppUrl);

  return {
    title: title,
    description: description,
    url: url,
    imageUrl: imageUrl,
    orderUrl: webAppUrl || ''
  };
}

function ensurePublicShareImage_(file) {
  if (!file) return null;
  try {
    file.setSharing(DriveApp.Access.ANYONE_WITH_LINK, DriveApp.Permission.VIEW);
  } catch (e) {
    // Owner may already have organization-only sharing policies.
  }
  return file;
}

function resolveShareImageFile_() {
  var config = cfg_();
  if (config.SHARE_IMAGE_FILE_ID) {
    try {
      return ensurePublicShareImage_(DriveApp.getFileById(config.SHARE_IMAGE_FILE_ID));
    } catch (e) {}
  }

  try {
    var root = getOrCreateRootFolder_();
    var names = ['share-preview.jpg', 'share-preview.jpeg', 'share-preview.png', 'og-image.jpg'];
    for (var i = 0; i < names.length; i++) {
      var it = root.getFilesByName(names[i]);
      if (it.hasNext()) return ensurePublicShareImage_(it.next());
    }
  } catch (e2) {}
  return null;
}

/**
 * Prefer a stable public HTTPS image URL (GitHub Pages / CDN).
 * Never use drive.google.com / lh3 links here — Facebook may open them when
 * the scrape fails, and they show "file cannot be opened" for visitors.
 */
function resolveShareImagePublicUrl_(webAppUrl) {
  var config = cfg_();
  if (config.SHARE_IMAGE_PUBLIC_URL) {
    return String(config.SHARE_IMAGE_PUBLIC_URL).trim();
  }
  // Optional: still allow serving the Drive file through Apps Script (not Drive UI).
  if (webAppUrl && resolveShareImageFile_()) {
    return webAppUrl + (webAppUrl.indexOf('?') >= 0 ? '&' : '?') + 'page=shareImage';
  }
  if (webAppUrl) {
    return webAppUrl + (webAppUrl.indexOf('?') >= 0 ? '&' : '?') + 'page=shareImage';
  }
  return '';
}

function serveShareImage_() {
  var file = resolveShareImageFile_();
  if (!file) {
    return HtmlService.createHtmlOutput(
      '<!DOCTYPE html><html><head><meta charset="utf-8" /><title>Hiányzó megosztási kép</title></head><body style="font-family:sans-serif;padding:24px">' +
      '<h1>Hiányzik a megosztási kép</h1>' +
      '<p>Töltse fel a <strong>share-preview.jpg</strong> fájlt a Naptar Drive gyökérmappába ' +
      '(ajánlott méret kb. 1200×630), vagy állítsa be a <code>SHARE_IMAGE_FILE_ID</code> értéket a Config.gs-ben.</p>' +
      '</body></html>'
    ).setTitle('Hiányzó megosztási kép');
  }
  // doGet cannot return a raw Blob — must be HtmlOutput / TextOutput.
  return htmlImageOutput_(file, 'Megosztási kép');
}

/**
 * Standalone HTML (not the HtmlService iframe wrapper of a template file) so
 * Open Graph crawlers can read title/description/image more reliably.
 * Post this URL on Facebook: .../exec?page=share
 */
function serveShareLanding_() {
  var webAppUrl = '';
  try {
    webAppUrl = ScriptApp.getService().getUrl() || '';
  } catch (err) {
    webAppUrl = '';
  }
  var share = buildShareMeta_(webAppUrl, 'share');
  var orderUrl = share.orderUrl || webAppUrl || '#';
  var title = escapeHtml_(share.title);
  var description = escapeHtml_(share.description);
  var imageUrl = escapeHtml_(share.imageUrl);
  var pageUrl = escapeHtml_(webAppUrl ? (webAppUrl + (webAppUrl.indexOf('?') >= 0 ? '&' : '?') + 'page=share') : '');

  // ContentService (not HtmlService): Facebook crawlers must see OG tags in the
  // top-level HTML. HtmlService buries them inside an iframe sandbox string.
  var html =
    '<!DOCTYPE html><html lang="hu"><head>' +
    '<meta charset="utf-8" />' +
    '<meta name="viewport" content="width=device-width, initial-scale=1" />' +
    '<title>' + title + '</title>' +
    '<meta name="description" content="' + description + '" />' +
    '<meta property="og:type" content="website" />' +
    '<meta property="og:site_name" content="Családi naptár" />' +
    '<meta property="og:title" content="' + title + '" />' +
    '<meta property="og:description" content="' + description + '" />' +
    '<meta property="og:url" content="' + pageUrl + '" />' +
    (imageUrl
      ? '<meta property="og:image" content="' + imageUrl + '" />' +
        '<meta property="og:image:secure_url" content="' + imageUrl + '" />' +
        '<meta property="og:image:width" content="1200" />' +
        '<meta property="og:image:height" content="630" />' +
        '<meta name="twitter:image" content="' + imageUrl + '" />' +
        '<link rel="image_src" href="' + imageUrl + '" />'
      : '') +
    '<meta name="twitter:card" content="summary_large_image" />' +
    '<meta name="twitter:title" content="' + title + '" />' +
    '<meta name="twitter:description" content="' + description + '" />' +
    '<style>' +
    'body{margin:0;font-family:Georgia,serif;background:linear-gradient(160deg,#f7f1e8,#efe4d6);color:#2a241c}' +
    '.wrap{max-width:640px;margin:0 auto;padding:28px 16px 48px}' +
    '.card{background:#fffaf4;border:1px solid #e4d8c8;border-radius:24px;overflow:hidden;box-shadow:0 18px 50px rgba(80,50,20,.08)}' +
    '.hero{background:linear-gradient(120deg,#3d2a1f,#8a3c1c 45%,#c47a3a);color:#fff8f1;padding:28px 24px}' +
    '.hero h1{margin:0;font-size:1.8rem;line-height:1.2}' +
    '.hero p{margin:10px 0 0;opacity:.92}' +
    '.body{padding:22px 24px 28px}' +
    '.price{font-size:1.6rem;font-weight:700;margin:0 0 8px}' +
    '.muted{color:#6b6258;margin:0 0 18px}' +
    '.btn{display:inline-block;background:#b4532a;color:#fff;text-decoration:none;padding:12px 18px;border-radius:12px;font-weight:700}' +
    '.preview{display:block;width:100%;height:auto;border-bottom:1px solid #e4d8c8}' +
    '</style></head><body><div class="wrap"><div class="card">' +
    (imageUrl ? '<img class="preview" src="' + imageUrl + '" alt="' + title + '" />' : '') +
    '<div class="hero"><h1>' + title + '</h1><p>' + description + '</p></div>' +
    '<div class="body">' +
    '<p class="price">60 RON</p>' +
    '<p class="muted">12 oldalas nyomtatott A4 naptár · saját fényképek · névnapok és események</p>' +
    '<a class="btn" href="' + escapeHtml_(orderUrl) + '">Megrendelés indítása</a>' +
    '</div></div></div></body></html>';

  return ContentService.createTextOutput(html).setMimeType(ContentService.MimeType.HTML);
}

function escapeHtml_(s) {
  return String(s == null ? '' : s)
    .replace(/&/g, '&amp;')
    .replace(/"/g, '&quot;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;');
}

function getPublicConfig() {
  return {
    supportedYears: [2026, 2027, 2028, 2029],
    maxPhotosPerMonth: MAX_PHOTOS_PER_MONTH,
    calendarFormats: [
      { value: 'portrait', labelHu: 'Álló (portrait)' },
      { value: 'landscape', labelHu: 'Fekvő (landscape)' }
    ],
    paperSizes: [
      { value: 'A4', labelHu: 'A4' },
      { value: 'A3', labelHu: 'A3' }
    ],
    months: [
      'Január', 'Február', 'Március', 'Április', 'Május', 'Június',
      'Július', 'Augusztus', 'Szeptember', 'Október', 'November', 'December'
    ]
  };
}

/**
 * Create order (called when leaving step 1 or on first photo upload).
 * privacyConsent may be false for draft; finalizeOrder requires true.
 */
function createOrder(payload) {
  if (!payload || !payload.fullName || !payload.phone || !payload.calendarYear) {
    throw new Error('Hiányzó kötelező mezők (név, telefon, év).');
  }
  if (!isRomanianPhone_(payload.phone)) {
    throw new Error('Csak román telefonszám fogadható el (pl. +40 712 345 678 vagy 0712 345 678).');
  }

  var year = String(payload.calendarYear);
  var root = getOrCreateRootFolder_();
  var yearFolder = getOrCreateChild_(root, year);
  var orderId = nextOrderId_(year);
  var safeName = String(payload.fullName).replace(/[^\w\- ]+/g, '').replace(/\s+/g, '_');
  var orderFolder = yearFolder.createFolder(orderId + '_' + safeName);

  for (var i = 0; i < MONTH_FOLDERS.length; i++) {
    orderFolder.createFolder(MONTH_FOLDERS[i]);
  }
  var meta = orderFolder.createFolder('00_Megrendelo_adatai');

  var editToken = Utilities.getUuid().replace(/-/g, '') + Utilities.getUuid().replace(/-/g, '');
  var tokenHash = hashToken_(editToken);
  var now = new Date().toISOString();
  var record = {
    orderId: orderId,
    fullName: payload.fullName,
    phone: payload.phone,
    email: payload.email || '',
    calendarYear: Number(payload.calendarYear),
    calendarLanguage: payload.calendarLanguage || 'hu',
    calendarFormatPref: payload.calendarFormatPref || 'portrait',
    paperSize: normalizePaperSize_(payload.paperSize),
    comments: payload.comments || '',
    events: sanitizeEvents_(payload.events || [], Number(payload.calendarYear)),
    photos: [],
    status: 'NEW',
    privacyConsent: !!payload.privacyConsent,
    directFromOffice: !!payload.directFromOffice,
    createdAt: now,
    updatedAt: now,
    editTokenHash: tokenHash,
    driveFolderUrl: orderFolder.getUrl(),
    driveFolderId: orderFolder.getId()
  };

  writeRecord_(meta, record);
  PropertiesService.getScriptProperties().setProperty('tok_' + tokenHash, orderFolder.getId());
  appendSheetRow_(record);

  return {
    orderId: orderId,
    editToken: editToken,
    driveFolderUrl: orderFolder.getUrl(),
    order: publicOrder_(record)
  };
}

/** Backward-compatible alias used by older Customer.html */
function submitOrder(payload) {
  if (!payload.privacyConsent) {
    throw new Error('Adatkezelési hozzájárulás kötelező.');
  }
  return createOrder(payload);
}

function getOrderByToken(editToken) {
  var ctx = loadOrderCtx_(editToken);
  return { order: enrichOrder_(ctx) };
}

/**
 * Review payload by edit token (preferred after checkout).
 */
function getOrderReviewByToken(editToken) {
  var ctx = loadOrderCtx_(editToken);
  return buildReviewPayload_(ctx, editToken);
}

/**
 * Review payload by public order number (lookup page).
 */
function getOrderReviewByOrderId(orderId) {
  var ctx = loadOrderCtxByOrderId_(orderId);
  return buildReviewPayload_(ctx, '');
}

function updateOrder(editToken, payload) {
  return updateOrderResolved_(loadOrderCtx_(editToken), payload);
}

/** Update by order id while status is NEW (lookup/review without token). */
function updateOrderByOrderId(orderId, payload) {
  return updateOrderResolved_(loadOrderCtxByOrderId_(orderId), payload);
}

function updateOrderResolved_(ctx, payload) {
  assertEditable_(ctx.record);
  var record = ctx.record;
  if (payload.fullName != null) record.fullName = payload.fullName;
  if (payload.phone != null) {
    if (!isRomanianPhone_(payload.phone)) {
      throw new Error('Csak román telefonszám fogadható el.');
    }
    record.phone = payload.phone;
  }
  if (payload.email != null) record.email = payload.email;
  if (payload.calendarYear != null) record.calendarYear = Number(payload.calendarYear);
  if (payload.calendarFormatPref != null) record.calendarFormatPref = payload.calendarFormatPref;
  if (payload.paperSize != null) record.paperSize = normalizePaperSize_(payload.paperSize);
  if (payload.calendarLanguage != null) record.calendarLanguage = payload.calendarLanguage;
  if (payload.comments != null) record.comments = payload.comments;
  if (payload.events) {
    record.events = sanitizeEvents_(payload.events, record.calendarYear);
  }
  if (payload.privacyConsent === true) record.privacyConsent = true;
  record.updatedAt = new Date().toISOString();
  writeRecord_(ctx.metaFolder, record);
  updateSheetRow_(record);
  return { order: enrichOrder_(ctx) };
}

function finalizeOrder(editToken, payload) {
  if (!payload || !payload.privacyConsent) {
    throw new Error('Adatkezelési hozzájárulás kötelező a véglegesítéshez.');
  }
  payload.privacyConsent = true;
  return updateOrder(editToken, payload);
}

/**
 * Upload one photo. filePayload = { name, mimeType, dataBase64 }
 */
function uploadPhoto(editToken, month, filePayload) {
  return uploadPhotosBatch(editToken, month, [filePayload]);
}

/**
 * Upload many photos for one month in a single server round-trip.
 * filePayloads = [{ name, mimeType, dataBase64 }, ...]
 * Uses a script lock so parallel client uploads cannot corrupt megrendeles.json.
 */
function uploadPhotosBatch(editToken, month, filePayloads) {
  var ctx = loadOrderCtx_(editToken);
  var lock = LockService.getScriptLock();
  lock.waitLock(60000);
  try {
    uploadPhotosBatchUnlocked_(ctx, month, filePayloads);
  } finally {
    lock.releaseLock();
  }
  // Enrich (URL building) outside the lock — never block other uploads on Drive sharing.
  return { order: enrichOrder_(ctx) };
}

function uploadPhotosBatchByOrderId(orderId, month, filePayloads) {
  var ctx = loadOrderCtxByOrderId_(orderId);
  var lock = LockService.getScriptLock();
  lock.waitLock(60000);
  try {
    uploadPhotosBatchUnlocked_(ctx, month, filePayloads);
  } finally {
    lock.releaseLock();
  }
  return { order: enrichOrder_(ctx) };
}

function uploadPhotosBatchUnlocked_(ctx, month, filePayloads) {
  assertEditable_(ctx.record);
  month = Number(month);
  if (month < 1 || month > 12) throw new Error('Érvénytelen hónap.');
  if (!filePayloads || !filePayloads.length) throw new Error('Nincs feltöltött fájl.');

  var inMonth = 0;
  var i;
  for (i = 0; i < (ctx.record.photos || []).length; i++) {
    if (ctx.record.photos[i].month === month) inMonth++;
  }
  var remaining = MAX_PHOTOS_PER_MONTH - inMonth;
  if (remaining <= 0) {
    throw new Error('Havonként maximum ' + MAX_PHOTOS_PER_MONTH + ' fénykép.');
  }

  var toSave = filePayloads.slice(0, remaining);
  var monthFolder = getChildFolder_(ctx.orderFolder, MONTH_FOLDERS[month - 1]);
  ctx.record.photos = ctx.record.photos || [];

  for (i = 0; i < toSave.length; i++) {
    var filePayload = toSave[i];
    if (!filePayload || !filePayload.dataBase64) continue;
    var mime = filePayload.mimeType || 'image/jpeg';
    if (mime !== 'image/jpeg' && mime !== 'image/png' && mime !== 'image/webp') {
      throw new Error('Csak JPG, PNG vagy WebP engedélyezett: ' + (filePayload.name || ''));
    }
    var bytes = Utilities.base64Decode(filePayload.dataBase64);
    if (bytes.length > 10 * 1024 * 1024) {
      throw new Error('A fájl túl nagy (max. 10 MB): ' + (filePayload.name || ''));
    }
    var safeName = String(filePayload.name || 'photo.jpg').replace(/[\\\/]+/g, '_');
    var stored = Utilities.getUuid() + '_' + safeName;
    var blob = Utilities.newBlob(bytes, mime, stored);
    var file = monthFolder.createFile(blob);
    try {
      file.setSharing(DriveApp.Access.ANYONE_WITH_LINK, DriveApp.Permission.VIEW);
    } catch (shareErr) {}
    ctx.record.photos.push({
      id: file.getId(),
      month: month,
      sortOrder: inMonth + i,
      originalFilename: safeName,
      mimeType: mime,
      sizeBytes: bytes.length
    });
  }

  ctx.record.updatedAt = new Date().toISOString();
  writeRecord_(ctx.metaFolder, ctx.record);
  updateSheetRow_(ctx.record);
}

function deletePhoto(editToken, photoId) {
  var ctx = loadOrderCtx_(editToken);
  var lock = LockService.getScriptLock();
  lock.waitLock(60000);
  try {
    deletePhotoUnlocked_(ctx, photoId);
  } finally {
    lock.releaseLock();
  }
  return { order: enrichOrder_(ctx) };
}

function deletePhotoByOrderId(orderId, photoId) {
  var ctx = loadOrderCtxByOrderId_(orderId);
  var lock = LockService.getScriptLock();
  lock.waitLock(60000);
  try {
    deletePhotoUnlocked_(ctx, photoId);
  } finally {
    lock.releaseLock();
  }
  return { order: enrichOrder_(ctx) };
}

function deletePhotoUnlocked_(ctx, photoId) {
  assertEditable_(ctx.record);
  var photos = ctx.record.photos || [];
  var kept = [];
  var found = false;
  for (var i = 0; i < photos.length; i++) {
    if (photos[i].id === photoId) {
      found = true;
      try {
        DriveApp.getFileById(photoId).setTrashed(true);
      } catch (e) {}
    } else {
      kept.push(photos[i]);
    }
  }
  if (!found) throw new Error('Fénykép nem található.');
  ctx.record.photos = kept;
  ctx.record.updatedAt = new Date().toISOString();
  writeRecord_(ctx.metaFolder, ctx.record);
  updateSheetRow_(ctx.record);
}

function assertAdminPassword_(password) {
  var expected = cfg_().ADMIN_PASSWORD;
  if (!expected) throw new Error('Admin jelszó nincs beállítva a Config.gs fájlban.');
  if (password !== expected) throw new Error('Hibás admin jelszó.');
  return true;
}

/** Public admin API: check password (does not return the password). */
function adminLogin(password) {
  assertAdminPassword_(password);
  return {
    ok: true,
    spreadsheetName: cfg_().SPREADSHEET_NAME,
    rootFolderName: cfg_().ROOT_FOLDER_NAME,
    spreadsheetUrl: 'https://docs.google.com/spreadsheets/d/' + cfg_().SPREADSHEET_ID + '/edit',
    driveRootUrl: 'https://drive.google.com/drive/folders/' + cfg_().ROOT_FOLDER_ID
  };
}

/** List orders from the Orders spreadsheet. */
function adminListOrders(password, filters) {
  assertAdminPassword_(password);
  filters = filters || {};
  var sheet = openOrdersSheet_();
  var data = sheet.getDataRange().getValues();
  var orders = [];
  var counts = { NEW: 0, IN_PROGRESS: 0, WAITING_FOR_CUSTOMER: 0, COMPLETED: 0, CANCELLED: 0, OTHER: 0 };

  for (var r = 1; r < data.length; r++) {
    var row = data[r];
    if (!row[0]) continue;
    var officeRaw = String(row[11] || '').toUpperCase();
    var order = {
      orderId: String(row[0]),
      createdAt: String(row[1] || ''),
      fullName: String(row[2] || ''),
      phone: String(row[3] || ''),
      email: String(row[4] || ''),
      calendarYear: row[5],
      status: String(row[6] || 'NEW'),
      photoCount: Number(row[7] || 0),
      eventCount: Number(row[8] || 0),
      driveFolderUrl: String(row[9] || ''),
      updatedAt: String(row[10] || ''),
      directFromOffice: officeRaw === 'YES' || officeRaw === 'TRUE' || officeRaw === '1'
    };

    if (filters.search) {
      var q = String(filters.search).toLowerCase();
      var blob = (order.fullName + ' ' + order.phone + ' ' + order.orderId + ' ' + order.email).toLowerCase();
      if (blob.indexOf(q) === -1) continue;
    }
    if (filters.year && String(order.calendarYear) !== String(filters.year)) continue;
    if (filters.status && order.status !== filters.status) continue;

    if (counts[order.status] != null) counts[order.status]++;
    else counts.OTHER++;
    orders.push(order);
  }

  orders.sort(function (a, b) {
    return String(b.createdAt).localeCompare(String(a.createdAt));
  });

  return { orders: orders, total: orders.length, counts: counts };
}

/** Update order status in Sheets (+ megrendeles.json when Drive folder is known). */
function adminSetStatus(password, orderId, status) {
  assertAdminPassword_(password);
  var allowed = {
    NEW: 1,
    IN_PROGRESS: 1,
    WAITING_FOR_CUSTOMER: 1,
    COMPLETED: 1,
    CANCELLED: 1
  };
  if (!allowed[status]) throw new Error('Érvénytelen státusz.');

  var sheet = openOrdersSheet_();
  var data = sheet.getDataRange().getValues();
  var found = false;
  var driveUrl = '';
  for (var r = 1; r < data.length; r++) {
    if (String(data[r][0]) === String(orderId)) {
      sheet.getRange(r + 1, 7).setValue(status);
      sheet.getRange(r + 1, 11).setValue(new Date().toISOString());
      driveUrl = String(data[r][9] || '');
      found = true;
      break;
    }
  }
  if (!found) throw new Error('Megrendelés nem található: ' + orderId);

  if (driveUrl) {
    try {
      var folderId = extractDriveFolderId_(driveUrl);
      if (folderId) {
        var orderFolder = DriveApp.getFolderById(folderId);
        var meta = getChildFolder_(orderFolder, '00_Megrendelo_adatai');
        var record = readRecord_(meta);
        record.status = status;
        record.updatedAt = new Date().toISOString();
        writeRecord_(meta, record);
      }
    } catch (e) {
      // Sheet update already succeeded; Drive JSON sync is best-effort.
    }
  }
  return { ok: true, orderId: orderId, status: status };
}

function extractDriveFolderId_(url) {
  var m = String(url).match(/\/folders\/([a-zA-Z0-9_-]+)/);
  return m ? m[1] : '';
}

function isRomanianPhone_(raw) {
  var cleaned = String(raw || '').replace(/[\s\-().]/g, '');
  return (
    /^\+40[237]\d{8}$/.test(cleaned) ||
    /^0040[237]\d{8}$/.test(cleaned) ||
    /^0[237]\d{8}$/.test(cleaned)
  );
}

function assertEventsInYear_(year, events) {
  for (var i = 0; i < (events || []).length; i++) {
    var d = events[i].eventDate;
    if (!d || !events[i].description) throw new Error('Az esemény dátuma és leírása kötelező.');
    if (String(d).slice(0, 4) !== String(year)) {
      throw new Error('Az esemény dátuma nem esik a ' + year + ' évre: ' + d);
    }
  }
}

/** Drop blank rows; keep only dated + described events in the calendar year. */
function sanitizeEvents_(events, year) {
  var out = [];
  for (var i = 0; i < (events || []).length; i++) {
    var raw = events[i] || {};
    var d = raw.eventDate || raw.EventDate || '';
    var desc = String(raw.description || raw.Description || '').trim();
    if (!d || !desc) continue;
    if (year && String(d).slice(0, 4) !== String(year)) {
      throw new Error('Az esemény dátuma nem esik a ' + year + ' évre: ' + d);
    }
    out.push({ eventDate: d, description: desc });
  }
  return out;
}

function assertEditable_(record) {
  if (!record || record.status !== 'NEW') {
    throw new Error(
      'A megrendelés már nem szerkeszthető' +
        (record && record.status ? ' (státusz: ' + record.status + ')' : '') +
        '.'
    );
  }
}

function hashToken_(token) {
  return Utilities.base64EncodeWebSafe(
    Utilities.computeDigest(Utilities.DigestAlgorithm.SHA_256, token)
  );
}

function loadOrderCtx_(editToken) {
  if (!editToken) throw new Error('Hiányzó szerkesztő token.');
  var hash = hashToken_(editToken);
  var folderId = PropertiesService.getScriptProperties().getProperty('tok_' + hash);
  if (!folderId) throw new Error('Érvénytelen vagy lejárt szerkesztő link.');
  var orderFolder = DriveApp.getFolderById(folderId);
  var metaFolder = getChildFolder_(orderFolder, '00_Megrendelo_adatai');
  var record = readRecord_(metaFolder);
  return { orderFolder: orderFolder, metaFolder: metaFolder, record: record };
}

function loadOrderCtxByOrderId_(orderId) {
  if (!orderId) throw new Error('Hiányzó rendelésszám.');
  var sheet = openOrdersSheet_();
  var data = sheet.getDataRange().getValues();
  var driveUrl = '';
  for (var r = 1; r < data.length; r++) {
    if (String(data[r][0]) === String(orderId).trim()) {
      driveUrl = String(data[r][9] || '');
      break;
    }
  }
  if (!driveUrl) throw new Error('Megrendelés nem található: ' + orderId);
  var folderId = extractDriveFolderId_(driveUrl);
  if (!folderId) throw new Error('Drive mappa hiányzik a megrendeléshez.');
  var orderFolder = DriveApp.getFolderById(folderId);
  var metaFolder = getChildFolder_(orderFolder, '00_Megrendelo_adatai');
  var record = readRecord_(metaFolder);
  // Prefer live sheet status if JSON lags.
  try {
    for (var i = 1; i < data.length; i++) {
      if (String(data[i][0]) === String(orderId).trim()) {
        if (data[i][6]) record.status = String(data[i][6]);
        break;
      }
    }
  } catch (e) {}
  return { orderFolder: orderFolder, metaFolder: metaFolder, record: record };
}

function buildReviewPayload_(ctx, editToken) {
  var order = enrichOrder_(ctx);
  return {
    order: order,
    editable: order.status === 'NEW',
    editToken: editToken || '',
    previewUrl: order.previewUrl || '',
    webAppUrl: (function () {
      try { return ScriptApp.getService().getUrl() || ''; } catch (e) { return ''; }
    })()
  };
}

function enrichOrder_(ctx) {
  var order = publicOrder_(ctx.record);
  order.photos = enrichPhotos_(ctx.record.photos || []);
  var previewFile = findPreviewFile_(ctx.orderFolder, ctx.metaFolder);
  order.hasPreview = !!previewFile;
  order.previewUrl = previewFile ? findPreviewUrl_(ctx.orderFolder, ctx.metaFolder) : '';
  order.editable = order.status === 'NEW';
  return order;
}

function enrichPhotos_(photos) {
  var out = [];
  for (var i = 0; i < photos.length; i++) {
    var p = photos[i];
    var copy = {
      id: p.id,
      month: p.month,
      sortOrder: p.sortOrder,
      originalFilename: p.originalFilename,
      mimeType: p.mimeType,
      sizeBytes: p.sizeBytes,
      viewUrl: '',
      thumbUrl: ''
    };
    // URLs only — sharing is set once at upload time (setSharing here caused lock timeouts).
    if (p.id) {
      copy.viewUrl = 'https://drive.google.com/uc?export=view&id=' + p.id;
      copy.thumbUrl = 'https://drive.google.com/thumbnail?id=' + p.id + '&sz=w400';
    }
    out.push(copy);
  }
  return out;
}

function findPreviewFile_(orderFolder, metaFolder) {
  var file = findNamedFile_(metaFolder, PREVIEW_FILE_NAME);
  if (!file) file = findNamedFile_(orderFolder, PREVIEW_FILE_NAME);
  return file;
}

function findPreviewUrl_(orderFolder, metaFolder) {
  var file = findPreviewFile_(orderFolder, metaFolder);
  if (!file) return '';
  try {
    file.setSharing(DriveApp.Access.ANYONE_WITH_LINK, DriveApp.Permission.VIEW);
  } catch (e) {}
  // Prefer googleusercontent host — uc?export=view is frequently blocked.
  return 'https://lh3.googleusercontent.com/d/' + file.getId();
}

function findNamedFile_(folder, name) {
  try {
    var it = folder.getFilesByName(name);
    if (it.hasNext()) return it.next();
  } catch (e) {}
  return null;
}

/**
 * Serves the watermarked preview strip for the review page iframe
 * (?page=previewServe&orderId=... / token=...).
 * Web apps cannot return a raw Blob from doGet — wrap as HtmlOutput.
 */
function servePreviewStrip_(e) {
  try {
    var token = (e && e.parameter && e.parameter.token) || '';
    var orderId = (e && e.parameter && e.parameter.orderId) || '';
    if (!token && !orderId) {
      return htmlMessageOutput_('Hiányzik a rendelésszám vagy a token.', 'Előnézet');
    }
    var ctx = token ? loadOrderCtx_(token) : loadOrderCtxByOrderId_(orderId);
    var file = findPreviewFile_(ctx.orderFolder, ctx.metaFolder);
    if (!file) {
      return htmlMessageOutput_(
        'Az előnézet még nem készült el ehhez a rendeléshez.',
        'Előnézet'
      );
    }
    return htmlImageOutput_(file, 'Naptár előnézet');
  } catch (err) {
    return htmlMessageOutput_(
      (err && err.message) ? err.message : String(err),
      'Előnézet hiba'
    );
  }
}

/**
 * HtmlOutput page that displays a Drive image.
 * Prefer a public Drive URL (large previews blow HtmlService base64 limits).
 * Never return a raw Blob from doGet — that yields
 * "returned value is not a supported return type".
 */
function htmlImageOutput_(file, title) {
  try {
    file.setSharing(DriveApp.Access.ANYONE_WITH_LINK, DriveApp.Permission.VIEW);
  } catch (eShare) {}

  var id = file.getId();
  var primary = 'https://lh3.googleusercontent.com/d/' + id + '=w2400';
  var fallback = 'https://drive.google.com/thumbnail?id=' + id + '&sz=w2400';
  var html =
    '<!DOCTYPE html><html><head><meta charset="utf-8" />' +
    '<meta name="viewport" content="width=device-width, initial-scale=1" />' +
    '<title>' + escapeHtml_(title || 'Előnézet') + '</title>' +
    '<style>html,body{margin:0;background:#1a1510;min-height:100%}' +
    'img{display:block;width:100%;height:auto}</style></head><body>' +
    '<img alt="' + escapeHtml_(title || 'előnézet') + '" src="' + primary + '" ' +
    'onerror="this.onerror=null;this.src=\'' + fallback + '\'" />' +
    '</body></html>';
  return HtmlService.createHtmlOutput(html)
    .setTitle(title || 'Előnézet')
    .setXFrameOptionsMode(HtmlService.XFrameOptionsMode.ALLOWALL);
}

function htmlMessageOutput_(message, title) {
  var html =
    '<!DOCTYPE html><html><head><meta charset="utf-8" />' +
    '<meta name="viewport" content="width=device-width, initial-scale=1" />' +
    '<title>' + escapeHtml_(title || 'Üzenet') + '</title>' +
    '<style>body{margin:0;font-family:Georgia,serif;background:#f7f1e8;color:#2a241c;' +
    'padding:24px;line-height:1.45}</style></head><body><p>' +
    escapeHtml_(message) + '</p></body></html>';
  return HtmlService.createHtmlOutput(html)
    .setTitle(title || 'Üzenet')
    .setXFrameOptionsMode(HtmlService.XFrameOptionsMode.ALLOWALL);
}

function readRecord_(metaFolder) {
  var files = metaFolder.getFilesByName('megrendeles.json');
  if (!files.hasNext()) throw new Error('Megrendelés adatfájl hiányzik.');
  return JSON.parse(files.next().getBlob().getDataAsString());
}

function writeRecord_(metaFolder, record) {
  var content = JSON.stringify(record, null, 2);
  var files = metaFolder.getFilesByName('megrendeles.json');
  if (files.hasNext()) {
    files.next().setContent(content);
    return;
  }
  metaFolder.createFile('megrendeles.json', content, MimeType.PLAIN_TEXT);
}

function isOfficeParam_(e) {
  var p = (e && e.parameter) || {};
  var officeOrder = String(p.officeorder || '').toLowerCase();
  return officeOrder === 'true' || officeOrder === '1' || officeOrder === 'yes';
}

function publicOrder_(record) {
  return {
    orderId: record.orderId,
    fullName: record.fullName,
    phone: record.phone,
    email: record.email,
    calendarYear: record.calendarYear,
    calendarLanguage: record.calendarLanguage,
    calendarFormatPref: record.calendarFormatPref,
    paperSize: normalizePaperSize_(record.paperSize),
    comments: record.comments,
    events: record.events || [],
    photos: record.photos || [],
    status: record.status,
    privacyConsent: record.privacyConsent,
    directFromOffice: !!record.directFromOffice,
    driveFolderUrl: record.driveFolderUrl,
    createdAt: record.createdAt,
    updatedAt: record.updatedAt
  };
}

function getOrCreateRootFolder_() {
  var config = cfg_();
  if (config.ROOT_FOLDER_ID) return DriveApp.getFolderById(config.ROOT_FOLDER_ID);
  var it = DriveApp.getFoldersByName(config.ROOT_FOLDER_NAME);
  return it.hasNext() ? it.next() : DriveApp.createFolder(config.ROOT_FOLDER_NAME);
}

function getOrCreateChild_(parent, name) {
  var it = parent.getFoldersByName(name);
  return it.hasNext() ? it.next() : parent.createFolder(name);
}

function getChildFolder_(parent, name) {
  var it = parent.getFoldersByName(name);
  if (!it.hasNext()) throw new Error('Mappa nem található: ' + name);
  return it.next();
}

function nextOrderId_(year) {
  return 'ORD-' + year + '-' + Utilities.formatDate(new Date(), Session.getScriptTimeZone(), 'HHmmss');
}

function openOrdersSheet_() {
  var config = cfg_();
  if (config.SPREADSHEET_ID) return SpreadsheetApp.openById(config.SPREADSHEET_ID).getSheets()[0];
  var files = DriveApp.getFilesByName(config.SPREADSHEET_NAME);
  var ss = files.hasNext() ? SpreadsheetApp.open(files.next()) : SpreadsheetApp.create(config.SPREADSHEET_NAME);
  return ss.getSheets()[0];
}

function normalizePaperSize_(value) {
  return String(value || '').toUpperCase() === 'A3' ? 'A3' : 'A4';
}

function sheetRowValues_(record) {
  return [
    record.orderId,
    record.createdAt,
    record.fullName,
    record.phone,
    record.email,
    record.calendarYear,
    record.status,
    (record.photos || []).length,
    (record.events || []).length,
    record.driveFolderUrl,
    record.updatedAt || record.createdAt,
    record.directFromOffice ? 'YES' : 'NO',
    normalizePaperSize_(record.paperSize),
    record.calendarFormatPref || 'portrait'
  ];
}

function appendSheetRow_(record) {
  var sheet = openOrdersSheet_();
  if (sheet.getLastRow() === 0) {
    sheet.appendRow([
      'Order ID', 'Submission date', 'Customer name', 'Phone number', 'Email', 'Calendar year',
      'Order status', 'Number of photographs', 'Number of events', 'Google Drive folder URL', 'Last modification date',
      'Direct from office', 'Paper size', 'Orientation'
    ]);
  } else {
    // Backfill header for older sheets that only had 11/12 columns.
    var headers = sheet.getRange(1, 1, 1, Math.max(14, sheet.getLastColumn())).getValues()[0];
    if (!headers[11] || String(headers[11]).indexOf('office') < 0) {
      sheet.getRange(1, 12).setValue('Direct from office');
    }
    if (!headers[12] || String(headers[12]).toLowerCase().indexOf('paper') < 0) {
      sheet.getRange(1, 13).setValue('Paper size');
    }
    if (!headers[13] || String(headers[13]).toLowerCase().indexOf('orient') < 0) {
      sheet.getRange(1, 14).setValue('Orientation');
    }
  }
  sheet.appendRow(sheetRowValues_(record));
}

function updateSheetRow_(record) {
  var sheet = openOrdersSheet_();
  var data = sheet.getDataRange().getValues();
  for (var r = 1; r < data.length; r++) {
    if (String(data[r][0]) === String(record.orderId)) {
      sheet.getRange(r + 1, 1, 1, 14).setValues([sheetRowValues_(record)]);
      return;
    }
  }
  appendSheetRow_(record);
}
