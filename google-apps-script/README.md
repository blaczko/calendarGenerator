# Google Apps Script deployment

Owner account: `zataipixel@gmail.com`  
Drive folder: **Naptar** (`15rbvA2819MJelitvQZzpYxlcrt_7VsZN`)  
Spreadsheet: **Orders** (`1r9uaZ0LUZkKplQ5iSdQQIt0ePqrf01DIqgH8N-H_F1k`)  
Access: **Anyone with the link**

## Local sync pipeline (recommended)

Local edits under `google-apps-script/` can be pushed automatically with [clasp](https://github.com/google/clasp).

### One-time setup

```powershell
cd google-apps-script
npm install
npm run login
Copy-Item pipeline.config.example.json pipeline.config.json
```

Edit **`pipeline.config.json`** (single config file for the pipeline):

| Field | Where to get it |
|--------|------------------|
| `scriptId` | Apps Script → **Project Settings** → Script ID |
| `deploymentId` | `npm run list-deployments` → Web app deployment id |
| `liveDeploy` | `true` to update `/exec` on each save |
| `debounceMs` | delay before upload (default `1500`) |

Then validate:

```powershell
npm run doctor
```

If the project is empty/new: set `scriptId`, `npm run push`, create a Web app deployment in the UI (Execute as: Me, Anyone), put that deployment id into `pipeline.config.json`.

### Day-to-day

| Command | What it does |
|--------|----------------|
| `npm run doctor` | Checks config + clasp login |
| `npm run watch` | Upload on save — project / `@HEAD` only |
| `npm run watch:live` | Upload on save **and** redeploy live `/exec` |
| `npm run push` | One-shot upload |
| `npm run deploy` | One-shot upload + live redeploy |

`watch:live` is what you want while iterating on the customer web app: save a file → pushed → `/exec` updated (debounced ~1.5s).

### Manual UI deploy (fallback)

1. Open https://script.google.com while signed in as `zataipixel@gmail.com`
2. Edit files or `npm run push`
3. **Deploy → Manage deployments → Edit → Version: New version**
4. Authorize Drive + Sheets when prompted

Orders land under Drive `Naptar/{year}/ORD-…` and a new row in spreadsheet **Orders**.

## Multi-step customer UI

`Customer.html` includes the same steps as the local React app:

1. Personal data  
2. Calendar year + portrait/landscape  
3. Monthly photo upload (1–6 / month)  
4. Events  
5. Review + consent  

After editing files in Apps Script, create a **new deployment version**  
(Deploy → Manage deployments → Edit → Version: New version), otherwise the old UI stays live.

## Facebook / WhatsApp link preview

**Do not post the plain `/exec` wizard URL.** Google wraps HtmlService pages in an iframe, so Facebook never sees `og:*` tags.

### Reliable option (recommended)
**Do not post `script.google.com/…/exec` on Facebook.** The Facebook mobile in-app browser
cannot load Apps Script (blank white page). Post this landing page instead:

1. Host `docs/share/` (GitHub → Settings → Pages → Source: `/docs`).
2. Post: `https://blaczko.github.io/calendarGenerator/share/`
3. That page detects Facebook’s browser and opens Chrome / offers copy-link.
4. Scrape/refresh in [Facebook Sharing Debugger](https://developers.facebook.com/tools/debug/).

### Apps Script share landing
1. Set `SHARE_IMAGE_PUBLIC_URL` in `Config.gs` to a **public** JPG URL (GitHub raw/Pages/CDN).  
   Drive `uc?export=view` and `?page=shareImage` usually fail for Facebook.
2. Redeploy, then post: `…/exec?page=share` (served via ContentService so OG tags are top-level).
3. Refresh the Facebook debugger scrape.
