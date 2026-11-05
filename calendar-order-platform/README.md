# Calendar Ordering Platform

Isolated module inside the `calendarGenerator` repository. It does **not** modify the existing .NET WPF Calendar Generator.

## What it is

A local-first web application for ordering personalized family photo calendars:

- Customer multi-step order form (Hungarian UI)
- Secure edit links (hashed tokens)
- Admin dashboard
- SQLite + local filesystem storage
- Storage/repository interfaces ready for Google Drive / Sheets
- Separate Google Apps Script adapter under `google-apps-script/`

## Quick start (local)

Requirements: Node.js 20+

```powershell
cd calendar-order-platform
copy backend\.env.example backend\.env
npm run setup
npm run dev
```

- Customer site: http://localhost:5174/
- Admin: http://localhost:5174/admin  
  Default credentials from `.env`: `admin` / `changeme-admin`
- API: http://localhost:4010/api/health

## Architecture

```
calendar-order-platform/
  backend/               Express + Prisma + SQLite
  frontend/              React + Vite + Tailwind
  storage/orders/        Local photo/order JSON storage
  google-apps-script/    Independent Apps Script deployment
```

Business logic depends on interfaces:

- `IOrderRepository` → `LocalOrderRepository` (default)
- `IPhotoStorage` → `LocalPhotoStorage` (default)
- `IOrderService` → `OrderService`

Google adapters live under `backend/src/google/` and are **not** required for local development.

## Tests

```powershell
cd calendar-order-platform
npm run test
```

Covered automatically (vitest + mocked Google clients):

- Order creation and edit-token flow
- Required field / phone / year validation
- Events (same-day OK; out-of-year rejected)
- Max 6 photos/month; invalid MIME; oversized files
- Photo deletion; admin auth; admin order deletion
- Google Drive folder layout + idempotent JSON upload (in-memory Drive)
- Google Sheets create/update/delete sync (in-memory Sheets)

## Hungarian deployment guide

See [TELEPITESI_UTMUTATO_HU.md](./TELEPITESI_UTMUTATO_HU.md).

## Existing WPF app

Unchanged. Run separately:

```powershell
dotnet run --project ../src/CalendarGenerator.App
```

## Limitations

- Node Google Drive/Sheets adapters need a real Google API client wired for production Node hosting; zero-cost production path is `google-apps-script/`.
- Apps Script HTML UI is a lighter adapter than the full React wizard (photo binary upload via Apps Script has quota/size constraints).
- Photo ZIP download for Drive-backed storage returns null locally; local filesystem ZIP works via admin export where implemented.
