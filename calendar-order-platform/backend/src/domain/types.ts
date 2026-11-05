export type OrderStatus =
  | "NEW"
  | "IN_PROGRESS"
  | "WAITING_FOR_CUSTOMER"
  | "COMPLETED"
  | "CANCELLED";

export interface OrderPhoto {
  id: string;
  month: number;
  sortOrder: number;
  storedFilename: string;
  originalFilename: string;
  mimeType: string;
  sizeBytes: number;
  relativePath: string;
  createdAt: Date;
}

export interface OrderEvent {
  id: string;
  eventDate: Date;
  description: string;
  createdAt: Date;
}

export interface OrderRecord {
  id: string;
  publicOrderId: string;
  editTokenHash: string;
  editTokenRevoked: boolean;
  fullName: string;
  phone: string;
  email: string | null;
  calendarYear: number;
  calendarLanguage: string;
  calendarFormatPref: string | null;
  comments: string | null;
  privacyConsent: boolean;
  status: OrderStatus;
  createdAt: Date;
  updatedAt: Date;
  photos: OrderPhoto[];
  events: OrderEvent[];
}

export interface CreateOrderInput {
  fullName: string;
  phone: string;
  email?: string | null;
  calendarYear: number;
  calendarLanguage?: string;
  calendarFormatPref?: string | null;
  comments?: string | null;
  privacyConsent: boolean;
  events: Array<{ eventDate: string; description: string }>;
}

export interface UpdateOrderInput {
  fullName?: string;
  phone?: string;
  email?: string | null;
  calendarYear?: number;
  calendarLanguage?: string;
  calendarFormatPref?: string | null;
  comments?: string | null;
  privacyConsent?: boolean;
  events?: Array<{ eventDate: string; description: string }>;
}

export interface OrderListFilter {
  search?: string;
  year?: number;
  status?: OrderStatus;
  sort?: "createdAt_asc" | "createdAt_desc";
}

export interface StoredPhotoMeta {
  relativePath: string;
  storedFilename: string;
  originalFilename: string;
  mimeType: string;
  sizeBytes: number;
}
