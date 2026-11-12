# Google Apps Script API backend

Owner: `zataipixel@gmail.com`  
UI: **https://naptar-web.pages.dev/** (Cloudflare Pages)  
This folder is **API only** (Drive + Sheets). No HtmlService customer UI.

## What stays here

| File | Role |
|------|------|
| `Code.gs` | `doPost` JSON API + `doGet` ping / preview / redirects |
| `Config.gs` | secrets / folder IDs |
| `appsscript.json` | web app access (`ANYONE_ANONYMOUS`) |

Deleted: `Customer.html`, `Admin.html`, `Lookup.html`, `Review.html`, `social-share.html`.

## API

**POST** `/exec` body:

```json
{ "action": "createOrder", "args": [ { "...payload" } ] }
```

Response: `{ "ok": true, "result": ... }` or `{ "ok": false, "error": "..." }`

**GET** `/exec?api=ping` — health check  
**GET** `/exec?page=previewServe&orderId=…` — preview strip HTML  
Other GET pages redirect to Cloudflare.

Cloudflare Function env: `GAS_EXEC_URL` = this web app `/exec` URL.

## Local sync

```powershell
cd google-apps-script
npm install
npm run login
Copy-Item pipeline.config.example.json pipeline.config.json   # once
npm run doctor
npm run deploy                                                # push + redeploy /exec
```

| Command | What it does |
|--------|----------------|
| `npm run push` | Upload `Code.gs` / `Config.gs` |
| `npm run deploy` | Upload + live redeploy |
| `npm run watch:live` | Upload + redeploy on save |

## Facebook / WhatsApp posts

Post **https://blaczko.github.io/calendarGenerator/share/** (redirects to Cloudflare).  
Do not post the Apps Script `/exec` wizard URL.
