import { GOOGLE_SHEETS_COLUMNS } from "./layout.js";

export interface SheetOrderRow {
  publicOrderId: string;
  createdAt: string;
  fullName: string;
  phone: string;
  email: string;
  calendarYear: number;
  status: string;
  photoCount: number;
  eventCount: number;
  driveFolderUrl: string;
  updatedAt: string;
}

export interface IGoogleSheetsClient {
  ensureHeaderRow(): Promise<void>;
  upsertOrderRow(row: SheetOrderRow): Promise<void>;
  deleteOrderRow(publicOrderId: string): Promise<void>;
  listOrderIds(): Promise<string[]>;
}

/** In-memory Sheets client for unit tests. */
export class InMemoryGoogleSheetsClient implements IGoogleSheetsClient {
  header = [...GOOGLE_SHEETS_COLUMNS];
  rows = new Map<string, SheetOrderRow>();

  async ensureHeaderRow(): Promise<void> {
    // no-op; header always present
  }

  async upsertOrderRow(row: SheetOrderRow): Promise<void> {
    this.rows.set(row.publicOrderId, { ...row });
  }

  async deleteOrderRow(publicOrderId: string): Promise<void> {
    this.rows.delete(publicOrderId);
  }

  async listOrderIds(): Promise<string[]> {
    return [...this.rows.keys()];
  }
}

export function toSheetValues(row: SheetOrderRow): string[] {
  return [
    row.publicOrderId,
    row.createdAt,
    row.fullName,
    row.phone,
    row.email,
    String(row.calendarYear),
    row.status,
    String(row.photoCount),
    String(row.eventCount),
    row.driveFolderUrl,
    row.updatedAt,
  ];
}
