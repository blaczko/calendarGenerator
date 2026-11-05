# Google Apps Script deployment

Owner account: `zataipixel@gmail.com`  
Drive folder: **Naptar** (`15rbvA2819MJelitvQZzpYxlcrt_7VsZN`)  
Spreadsheet: **Orders** (`1r9uaZ0LUZkKplQ5iSdQQIt0ePqrf01DIqgH8N-H_F1k`)  
Access: **Anyone with the link**

## Files to copy into Apps Script

1. `Config.gs` — set `ADMIN_PASSWORD` here
2. `Code.gs`
3. `Customer.html`
4. `Admin.html`

## Deploy steps

1. Open https://script.google.com while signed in as `zataipixel@gmail.com`
2. **New project** → rename e.g. `Naptar Orders`
3. Replace `Code.gs` contents; create files for `Config.gs`, `Customer.html`, `Admin.html`
4. In `Config.gs`, set `ADMIN_PASSWORD: 'your-secret'`
5. **Deploy → New deployment → Web app**
   - Execute as: **Me**
   - Who has access: **Anyone** (anyone with the link)
6. Authorize Drive + Sheets when prompted
7. Copy the web app URL and open it to test an order

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
