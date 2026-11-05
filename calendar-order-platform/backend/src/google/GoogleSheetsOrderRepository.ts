import { randomUUID } from "node:crypto";
import type {
  CreateOrderInput,
  OrderListFilter,
  OrderRecord,
  OrderStatus,
  UpdateOrderInput,
} from "../domain/types.js";
import type { IOrderRepository } from "../interfaces/IOrderRepository.js";
import { GOOGLE_SHEETS_COLUMNS } from "./layout.js";
import type { IGoogleSheetsClient } from "./sheetsClient.js";

export { GOOGLE_SHEETS_COLUMNS };

/**
 * Google Sheets–backed order repository (metadata mirror).
 * Not used during local development (LocalOrderRepository + SQLite is default).
 * Inject IGoogleSheetsClient; unit tests use InMemoryGoogleSheetsClient.
 */
export class GoogleSheetsOrderRepository implements IOrderRepository {
  private orders = new Map<string, OrderRecord>();

  constructor(private readonly sheets: IGoogleSheetsClient) {}

  private async sync(order: OrderRecord, driveFolderUrl = ""): Promise<void> {
    await this.sheets.ensureHeaderRow();
    await this.sheets.upsertOrderRow({
      publicOrderId: order.publicOrderId,
      createdAt: order.createdAt.toISOString(),
      fullName: order.fullName,
      phone: order.phone,
      email: order.email ?? "",
      calendarYear: order.calendarYear,
      status: order.status,
      photoCount: order.photos.length,
      eventCount: order.events.length,
      driveFolderUrl,
      updatedAt: order.updatedAt.toISOString(),
    });
  }

  async create(
    input: CreateOrderInput,
    publicOrderId: string,
    editTokenHash: string
  ): Promise<OrderRecord> {
    const now = new Date();
    const order: OrderRecord = {
      id: randomUUID(),
      publicOrderId,
      fullName: input.fullName,
      phone: input.phone,
      email: input.email ?? null,
      calendarYear: input.calendarYear,
      calendarLanguage: input.calendarLanguage ?? "hu",
      calendarFormatPref: input.calendarFormatPref ?? null,
      comments: input.comments ?? null,
      privacyConsent: input.privacyConsent,
      status: "NEW",
      editTokenHash,
      editTokenRevoked: false,
      createdAt: now,
      updatedAt: now,
      events: (input.events ?? []).map((e) => ({
        id: randomUUID(),
        eventDate: new Date(e.eventDate),
        description: e.description,
        createdAt: now,
      })),
      photos: [],
    };
    this.orders.set(order.id, order);
    await this.sync(order);
    return structuredClone(order);
  }

  async findById(id: string): Promise<OrderRecord | null> {
    const o = this.orders.get(id);
    return o ? structuredClone(o) : null;
  }

  async findByPublicOrderId(publicOrderId: string): Promise<OrderRecord | null> {
    for (const o of this.orders.values()) {
      if (o.publicOrderId === publicOrderId) return structuredClone(o);
    }
    return null;
  }

  async findByEditTokenHash(hash: string): Promise<OrderRecord | null> {
    for (const o of this.orders.values()) {
      if (o.editTokenHash === hash && !o.editTokenRevoked) return structuredClone(o);
    }
    return null;
  }

  async list(filter?: OrderListFilter): Promise<OrderRecord[]> {
    let list = [...this.orders.values()];
    if (filter?.year) list = list.filter((o) => o.calendarYear === filter.year);
    if (filter?.status) list = list.filter((o) => o.status === filter.status);
    if (filter?.search) {
      const q = filter.search.toLowerCase();
      list = list.filter(
        (o) =>
          o.fullName.toLowerCase().includes(q) ||
          o.phone.replace(/\s/g, "").includes(q.replace(/\s/g, ""))
      );
    }
    list.sort((a, b) =>
      filter?.sort === "createdAt_asc"
        ? a.createdAt.getTime() - b.createdAt.getTime()
        : b.createdAt.getTime() - a.createdAt.getTime()
    );
    return list.map((o) => structuredClone(o));
  }

  async update(id: string, input: UpdateOrderInput): Promise<OrderRecord> {
    const order = this.orders.get(id);
    if (!order) throw new Error("Order not found");
    Object.assign(order, {
      ...input,
      updatedAt: new Date(),
      events: input.events
        ? input.events.map((e) => ({
            id: randomUUID(),
            eventDate: new Date(e.eventDate),
            description: e.description,
            createdAt: new Date(),
          }))
        : order.events,
    });
    await this.sync(order);
    return structuredClone(order);
  }

  async updateStatus(id: string, status: OrderStatus): Promise<OrderRecord> {
    const order = this.orders.get(id);
    if (!order) throw new Error("Order not found");
    order.status = status;
    order.updatedAt = new Date();
    await this.sync(order);
    return structuredClone(order);
  }

  async revokeEditToken(id: string): Promise<OrderRecord> {
    const order = this.orders.get(id);
    if (!order) throw new Error("Order not found");
    order.editTokenRevoked = true;
    order.updatedAt = new Date();
    await this.sync(order);
    return structuredClone(order);
  }

  async delete(id: string): Promise<void> {
    const order = this.orders.get(id);
    if (!order) return;
    await this.sheets.deleteOrderRow(order.publicOrderId);
    this.orders.delete(id);
  }

  async addPhoto(
    orderId: string,
    photo: {
      month: number;
      sortOrder: number;
      storedFilename: string;
      originalFilename: string;
      mimeType: string;
      sizeBytes: number;
      relativePath: string;
    }
  ): Promise<OrderRecord> {
    const order = this.orders.get(orderId);
    if (!order) throw new Error("Order not found");
    order.photos.push({ id: randomUUID(), createdAt: new Date(), ...photo });
    order.updatedAt = new Date();
    await this.sync(order);
    return structuredClone(order);
  }

  async deletePhoto(orderId: string, photoId: string): Promise<OrderRecord> {
    const order = this.orders.get(orderId);
    if (!order) throw new Error("Order not found");
    order.photos = order.photos.filter((p) => p.id !== photoId);
    order.updatedAt = new Date();
    await this.sync(order);
    return structuredClone(order);
  }

  async reorderPhotos(
    orderId: string,
    month: number,
    photoIdsInOrder: string[]
  ): Promise<OrderRecord> {
    const order = this.orders.get(orderId);
    if (!order) throw new Error("Order not found");
    photoIdsInOrder.forEach((pid, idx) => {
      const p = order.photos.find((x) => x.id === pid && x.month === month);
      if (p) p.sortOrder = idx;
    });
    order.updatedAt = new Date();
    await this.sync(order);
    return structuredClone(order);
  }

  async countsByStatus(): Promise<Record<OrderStatus, number>> {
    const counts: Record<OrderStatus, number> = {
      NEW: 0,
      IN_PROGRESS: 0,
      WAITING_FOR_CUSTOMER: 0,
      COMPLETED: 0,
      CANCELLED: 0,
    };
    for (const o of this.orders.values()) counts[o.status] += 1;
    return counts;
  }
}
