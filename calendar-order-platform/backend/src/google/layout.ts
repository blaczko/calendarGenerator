/** Documented Google Drive folder layout for production uploads. */
export const GOOGLE_DRIVE_LAYOUT = {
  root: "Naptar",
  year: "{year}",
  order: "{orderId}_{CustomerName}",
  meta: "00_Megrendelo_adatai",
  metaFile: "megrendeles.json",
  months: [
    "01_Januar",
    "02_Februar",
    "03_Marcius",
    "04_Aprilis",
    "05_Majus",
    "06_Junius",
    "07_Julius",
    "08_Augusztus",
    "09_Szeptember",
    "10_Oktober",
    "11_November",
    "12_December",
  ],
} as const;

export const GOOGLE_SHEETS_COLUMNS = [
  "Order ID",
  "Submission date",
  "Customer name",
  "Phone number",
  "Email",
  "Calendar year",
  "Order status",
  "Number of photographs",
  "Number of events",
  "Google Drive folder URL",
  "Last modification date",
] as const;
