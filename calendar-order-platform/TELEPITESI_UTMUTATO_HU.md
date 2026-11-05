# Telepítési útmutató – Naptár rendelési platform

Ez az útmutató a **calendar-order-platform** modulra vonatkozik.  
A meglévő Windows WPF naptárkészítő alkalmazást nem módosítja és nem helyettesíti.

---

## A rész – Helyi fejlesztés

### 1. Szükséges szoftver
- Node.js 20 vagy újabb
- npm (Node-dal érkezik)
- Modern böngésző

### 2. Node.js telepítése
1. Töltse le: https://nodejs.org/
2. Telepítse az LTS verziót.
3. Ellenőrzés PowerShellben:

```powershell
node -v
npm -v
```

### 3. Függőségek telepítése

```powershell
cd c:\MyCode\calendarGenerator\calendar-order-platform
copy backend\.env.example backend\.env
npm run setup
```

A `setup` parancs:
- telepíti a workspace csomagokat,
- inicializálja a SQLite adatbázist,
- seed adatokat tölt (minta megrendelés + admin felhasználó).

### 4. Környezeti változók (`backend/.env`)

Fontosabb kulcsok:

| Kulcs | Példa | Jelentés |
|------|-------|----------|
| `PORT` | `4010` | API port |
| `CORS_ORIGIN` | `http://localhost:5174` | Frontend eredet |
| `PUBLIC_APP_URL` | `http://localhost:5174` | Szerkesztő linkek alapja |
| `ADMIN_USERNAME` | `admin` | Admin belépés |
| `ADMIN_PASSWORD` | `changeme-admin` | Admin jelszó (cserélje le!) |
| `STORAGE_ROOT` | `../storage/orders` | Helyi fájltár |
| `SUPPORTED_YEARS` | `2026,2027,2028,2029` | Engedélyezett évek |
| `PHOTO_STORAGE_ADAPTER` | `local` | Helyi tár |
| `ORDER_REPOSITORY_ADAPTER` | `local` | SQLite |

**Ne** állítsa Google adapterre a helyi fejlesztéshez Google hitelesítő adatok nélkül.

### 5. Adatbázis
SQLite fájl: `backend/prisma/dev.db`  
Migrációk: `backend/prisma/migrations/`

### 6. Indítás

```powershell
npm run dev
```

### 7. Admin felület
- URL: http://localhost:5174/admin
- Alapértelmezett: `admin` / `changeme-admin`

### 8. Teljes megrendelési folyamat tesztelése
1. Nyissa meg: http://localhost:5174/
2. Töltse ki a személyes adatokat.
3. Válasszon évet.
4. Töltsön fel fényképeket (opcionális).
5. Adjon hozzá eseményeket.
6. Fogadja el az adatkezelést, küldje el.
7. Másolja a szerkesztő linket.
8. Adminban ellenőrizze az új rendelést.

---

## B rész – Google Drive előkészítés

> Ne találjon ki saját ID-kat. A Google felületen létrejött értékeket másolja be.

### 1. Google fiók
Használjon dedikált üzleti/üzemeltetői fiókot, ha lehetséges.

### 2. Gyökérmappa a Drive-on
1. Drive → Új mappa
2. Név: `Naptar`
3. Másolja a mappa ID-ját az URL-ből (`.../folders/FOLDER_ID`)

### 3. Google Táblázat
1. Hozzon létre táblázatot: `Orders`
2. Fejlécek (1. sor):

`Order ID | Submission date | Customer name | Phone number | Email | Calendar year | Order status | Number of photographs | Number of events | Google Drive folder URL | Last modification date`

3. Másolja a spreadsheet ID-t az URL-ből.

### 4–7. Apps Script projekt
1. https://script.google.com → Új projekt
2. Másolja be a `google-apps-script/Code.gs`, `Customer.html`, `Admin.html` fájlokat
3. A `CONFIG` objektumban cserélje le:
   - `ROOT_FOLDER_ID`
   - `SPREADSHEET_ID`
   - `ADMIN_PASSWORD`
4. Engedélyezze a szükséges Google szolgáltatásokat (Drive, Sheets – alapból elérhetők DriveApp/SpreadsheetApp-pel)
5. Jogosultságok: a szkript a tulajdonos fiókjaként fusson
6. Telepítés: Közzététel → Webalkalmazásként telepítés

---

## C rész – Éles telepítés (Apps Script)

1. Készítse elő a fenti CONFIG értékeket.
2. Telepítse webalkalmazásként.
3. Futtatás: **Saját magam** (Me)
4. Hozzáférés: a Google fiók szabályai szerint (pl. „Bárki”, ha a szervezet engedi)
5. Admin: külön védje jelszóval / belső folyamattal; ne tegye nyilvánossá a privileged műveleteket
6–7. Drive + Sheets bekötés a CONFIG ID-kkal
8–11. Tesztelje: rendelés, fénykép (Node verzióban teljes), szerkesztő link, admin
12. Ellenőrizze, hogy a fényképek **nem** publikus „anyone with link” Drive fájlokként kerülnek ki

**Fontos:** A Node/Express backend **nem** telepíthető közvetlenül Google Apps Scriptbe.  
Ezért készült a külön `google-apps-script/` adapter.

---

### Google integráció ellenőrzési eljárás (éles után)

1. Hozzon létre egy tesztmegrendelést az Apps Script webalkalmazáson.
2. Ellenőrizze a Drive-on: `Naptar / {év} / ORD-…` mappa, 12 hónap + `00_Megrendelo_adatai`.
3. Ellenőrizze az `Orders` táblázat új sorát és a Drive mappa URL oszlopot.
4. Próbáljon szerkesztő tokennel hozzáférni; érvénytelen tokennel elutasítást várjon.
5. Admin jelszóval ellenőrizze a státuszváltást.
6. Győződjön meg róla, hogy a feltöltött fényképek **nem** „Bárki a linkkel” megosztásúak.

A helyi Node tesztek a Google szolgáltatásokat **mockolják** (`InMemoryGoogleDriveClient`, `InMemoryGoogleSheetsClient`); a fenti lépések az éles Google fiókra vonatkoznak.

---

## D rész – Karbantartás

1. **Frissítés:** húzza be az új kódot, `npm run setup` / migrációk futtatása
2. **Mentés:** másolja a `storage/orders` mappát és a `dev.db` fájlt
3. **Drive tárhely:** rendszeresen archiváljon régi évek mappáit
4. **Régi rendelések törlése:** admin felület „Törlés” + Drive mappa kézi törlés
5. **Gyakori hibák**
   - CORS hiba → `CORS_ORIGIN` egyezzen a frontend URL-lel
   - Feltöltés elutasítva → MIME/méret limit
   - Admin 401 → cookie / jelszó / `ADMIN_SESSION_SECRET`
6. **Apps Script kvóták:** napi URL Fetch / Drive művelet korlátok – lásd Google dokumentáció

---

## Jog / GDPR megjegyzés

A beépített hozzájárulás, törlés, export és link-visszavonás **technikai** eszközök.  
A jogi megfelelőség (tájékoztató szövegek, adatkezelési nyilvántartás, megőrzési politika) az üzemeltető felelőssége.
