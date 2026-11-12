# Cloudflare Pages frontend (Apps Script backend)

Static order UI on Cloudflare Pages. API calls go to `/api` → Pages Function → Apps Script `doPost`.

## One-time Cloudflare setup

1. Create account: https://dash.cloudflare.com/sign-up  
2. **Workers & Pages** → **Create** → **Pages** → **Connect to Git**  
3. Select repo `blaczko/calendarGenerator`  
4. Build settings:
   - **Root directory:** `web`
   - **Build command:** *(empty)*
   - **Build output directory:** `/`  (or leave blank / `.` — static files in `web/`)
5. Deploy  
6. **Settings → Environment variables** → add:
   - Name: `GAS_EXEC_URL`
   - Value: your Apps Script web-app `/exec` URL  
     (example: `https://script.google.com/macros/s/AKfycby…/exec`)
   - Environment: Production (and Preview if you want)
7. Redeploy after adding the env var  
8. Open `https://YOUR_PROJECT.pages.dev/?page=customer`  
9. Health check: `https://YOUR_PROJECT.pages.dev/api`

## URLs

| Page | URL |
|------|-----|
| Order form | `/?page=customer` |
| Office walk-in | `/?page=customer&officeorder=true` |
| Lookup | `/?page=lookup` |
| Review | `/?page=review&orderId=ORD-…` |

## Notes

- Keep Apps Script deployment access as **Anyone** (anonymous) so the Worker can POST without a Google login.  
- Old `script.google.com` HtmlService UI can stay as fallback during migration.
