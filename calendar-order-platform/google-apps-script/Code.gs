/**
 * Google Apps Script multi-step order API (Drive + Sheets).
 * Requires Config.gs. Redeploy (new version) after updating this file.
 */

var MONTH_FOLDERS = [
  '01_Januar', '02_Februar', '03_Marcius', '04_Aprilis', '05_Majus', '06_Junius',
  '07_Julius', '08_Augusztus', '09_Szeptember', '10_Oktober', '11_November', '12_December'
];
var MAX_PHOTOS_PER_MONTH = 6;

function cfg_() {
  return getAppConfig();
}

function doGet(e) {
  var page = (e && e.parameter && e.parameter.page) || 'customer';
  var template = HtmlService.createTemplateFromFile(page === 'admin' ? 'Admin' : 'Customer');
  var config = cfg_();
  template.config = {
    rootFolderName: config.ROOT_FOLDER_NAME,
    spreadsheetName: config.SPREADSHEET_NAME,
    ownerEmail: config.OWNER_EMAIL,
    maxPhotosPerMonth: MAX_PHOTOS_PER_MONTH
  };
  return template
    .evaluate()
    .setTitle('Családi naptár rendelés')
    .setXFrameOptionsMode(HtmlService.XFrameOptionsMode.ALLOWALL);
}

function getPublicConfig() {
  return {
    supportedYears: [2026, 2027, 2028, 2029],
    maxPhotosPerMonth: MAX_PHOTOS_PER_MONTH,
    calendarFormats: [
      { value: 'portrait', labelHu: 'Álló (portrait) – A4' },
      { value: 'landscape', labelHu: 'Fekvő (landscape) – A4' }
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
    comments: payload.comments || '',
    events: payload.events || [],
    photos: [],
    status: 'NEW',
    privacyConsent: !!payload.privacyConsent,
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
  return { order: publicOrder_(ctx.record) };
}

function updateOrder(editToken, payload) {
  var ctx = loadOrderCtx_(editToken);
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
  if (payload.calendarLanguage != null) record.calendarLanguage = payload.calendarLanguage;
  if (payload.comments != null) record.comments = payload.comments;
  if (payload.events) {
    assertEventsInYear_(record.calendarYear, payload.events);
    record.events = payload.events;
  }
  if (payload.privacyConsent === true) record.privacyConsent = true;
  record.updatedAt = new Date().toISOString();
  writeRecord_(ctx.metaFolder, record);
  updateSheetRow_(record);
  return { order: publicOrder_(record) };
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
  var lock = LockService.getScriptLock();
  lock.waitLock(30000);
  try {
    return uploadPhotosBatchUnlocked_(editToken, month, filePayloads);
  } finally {
    lock.releaseLock();
  }
}

function uploadPhotosBatchUnlocked_(editToken, month, filePayloads) {
  month = Number(month);
  if (month < 1 || month > 12) throw new Error('Érvénytelen hónap.');
  if (!filePayloads || !filePayloads.length) throw new Error('Nincs feltöltött fájl.');

  var ctx = loadOrderCtx_(editToken);
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
  return { order: publicOrder_(ctx.record) };
}

function deletePhoto(editToken, photoId) {
  var lock = LockService.getScriptLock();
  lock.waitLock(30000);
  try {
    return deletePhotoUnlocked_(editToken, photoId);
  } finally {
    lock.releaseLock();
  }
}

function deletePhotoUnlocked_(editToken, photoId) {
  var ctx = loadOrderCtx_(editToken);
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
  return { order: publicOrder_(ctx.record) };
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
      updatedAt: String(row[10] || '')
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

function publicOrder_(record) {
  return {
    orderId: record.orderId,
    fullName: record.fullName,
    phone: record.phone,
    email: record.email,
    calendarYear: record.calendarYear,
    calendarLanguage: record.calendarLanguage,
    calendarFormatPref: record.calendarFormatPref,
    comments: record.comments,
    events: record.events || [],
    photos: record.photos || [],
    status: record.status,
    privacyConsent: record.privacyConsent,
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

function appendSheetRow_(record) {
  var sheet = openOrdersSheet_();
  if (sheet.getLastRow() === 0) {
    sheet.appendRow([
      'Order ID', 'Submission date', 'Customer name', 'Phone number', 'Email', 'Calendar year',
      'Order status', 'Number of photographs', 'Number of events', 'Google Drive folder URL', 'Last modification date'
    ]);
  }
  sheet.appendRow([
    record.orderId, record.createdAt, record.fullName, record.phone, record.email, record.calendarYear,
    record.status, (record.photos || []).length, (record.events || []).length, record.driveFolderUrl, record.updatedAt || record.createdAt
  ]);
}

function updateSheetRow_(record) {
  var sheet = openOrdersSheet_();
  var data = sheet.getDataRange().getValues();
  for (var r = 1; r < data.length; r++) {
    if (String(data[r][0]) === String(record.orderId)) {
      sheet.getRange(r + 1, 1, 1, 11).setValues([[
        record.orderId, record.createdAt, record.fullName, record.phone, record.email, record.calendarYear,
        record.status, (record.photos || []).length, (record.events || []).length, record.driveFolderUrl, record.updatedAt || record.createdAt
      ]]);
      return;
    }
  }
  appendSheetRow_(record);
}
