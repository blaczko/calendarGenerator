import { createHash, randomBytes } from "node:crypto";
import { config } from "../config.js";
import type {
  CreateOrderInput,
  OrderListFilter,
  OrderRecord,
  OrderStatus,
  UpdateOrderInput,
} from "../domain/types.js";
import type { IOrderRepository } from "../interfaces/IOrderRepository.js";
import type {
  CreateOrderResult,
  IOrderService,
} from "../interfaces/IOrderService.js";
import type { IPhotoStorage, PhotoUploadPayload } from "../interfaces/IPhotoStorage.js";
import {
  assertEventsInYear,
  assertValidUpload,
  createOrderSchema,
  updateOrderSchema,
} from "./validation.js";

function hashToken(token: string): string {
  return createHash("sha256").update(token).digest("hex");
}

function slugifyName(name: string): string {
  return name
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/[^a-zA-Z0-9]+/g, "_")
    .replace(/^_|_$/g, "")
    .slice(0, 40) || "Customer";
}

function folderName(publicOrderId: string, fullName: string): string {
  return `${publicOrderId}_${slugifyName(fullName)}`;
}

export class OrderService implements IOrderService {
  constructor(
    private readonly orders: IOrderRepository,
    private readonly photos: IPhotoStorage
  ) {}

  private async nextPublicId(year: number): Promise<string> {
    const existing = await this.orders.list({ year });
    const seq = existing.length + 1;
    return `ORD-${year}-${String(seq).padStart(3, "0")}`;
  }

  async createOrder(raw: CreateOrderInput): Promise<CreateOrderResult> {
    const input = createOrderSchema.parse(raw) as CreateOrderInput;
    assertEventsInYear(input.calendarYear, input.events);
    const publicOrderId = await this.nextPublicId(input.calendarYear);
    const editToken = randomBytes(32).toString("hex");
    const order = await this.orders.create(
      {
        ...input,
        email: input.email || null,
      },
      publicOrderId,
      hashToken(editToken)
    );
    const folder = folderName(publicOrderId, order.fullName);
    await this.photos.ensureOrderFolders(folder, order.calendarYear);
    await this.photos.writeOrderJson(folder, order.calendarYear, this.toExport(order));
    return {
      order,
      editToken,
      editUrl: `${config.publicAppUrl}/edit/${editToken}`,
    };
  }

  async getOrderForEdit(editToken: string): Promise<OrderRecord> {
    const order = await this.orders.findByEditTokenHash(hashToken(editToken));
    if (!order) throw Object.assign(new Error("Érvénytelen vagy visszavont szerkesztő link."), { status: 404 });
    return order;
  }

  async updateOrder(editToken: string, raw: UpdateOrderInput): Promise<OrderRecord> {
    const order = await this.getOrderForEdit(editToken);
    const input = updateOrderSchema.parse(raw);
    const year = input.calendarYear ?? order.calendarYear;
    if (input.events) assertEventsInYear(year, input.events);
    const updated = await this.orders.update(order.id, {
      ...input,
      email: input.email === "" ? null : input.email,
    });
    await this.photos.writeOrderJson(
      folderName(updated.publicOrderId, updated.fullName),
      updated.calendarYear,
      this.toExport(updated)
    );
    return updated;
  }

  async uploadPhoto(
    editToken: string,
    month: number,
    payload: PhotoUploadPayload
  ): Promise<OrderRecord> {
    if (month < 1 || month > 12) throw new Error("Érvénytelen hónap.");
    assertValidUpload({
      mimetype: payload.mimeType,
      originalname: payload.originalFilename,
      size: payload.sizeBytes,
    });
    const order = await this.getOrderForEdit(editToken);
    const inMonth = order.photos.filter((p) => p.month === month);
    if (inMonth.length >= config.maxPhotosPerMonth) {
      throw new Error(
        `Havonként 1–${config.maxPhotosPerMonth} fénykép tölthető fel (maximum elérve).`
      );
    }
    const folder = folderName(order.publicOrderId, order.fullName);
    const stored = await this.photos.savePhoto(
      folder,
      order.calendarYear,
      month,
      payload
    );
    const updated = await this.orders.addPhoto(order.id, {
      month,
      sortOrder: inMonth.length,
      ...stored,
    });
    await this.photos.writeOrderJson(folder, order.calendarYear, this.toExport(updated));
    return updated;
  }

  async deletePhoto(editToken: string, photoId: string): Promise<OrderRecord> {
    const order = await this.getOrderForEdit(editToken);
    const photo = order.photos.find((p) => p.id === photoId);
    if (!photo) throw Object.assign(new Error("Fénykép nem található."), { status: 404 });
    await this.photos.deletePhoto(photo.relativePath);
    const updated = await this.orders.deletePhoto(order.id, photoId);
    await this.photos.writeOrderJson(
      folderName(updated.publicOrderId, updated.fullName),
      updated.calendarYear,
      this.toExport(updated)
    );
    return updated;
  }

  async reorderPhotos(
    editToken: string,
    month: number,
    photoIds: string[]
  ): Promise<OrderRecord> {
    const order = await this.getOrderForEdit(editToken);
    const monthPhotos = order.photos.filter((p) => p.month === month);
    if (photoIds.length !== monthPhotos.length) {
      throw new Error("Érvénytelen fénykép-sorrend.");
    }
    for (const id of photoIds) {
      if (!monthPhotos.some((p) => p.id === id)) {
        throw new Error("Érvénytelen fénykép azonosító.");
      }
    }
    return this.orders.reorderPhotos(order.id, month, photoIds);
  }

  adminList(filter?: OrderListFilter): Promise<OrderRecord[]> {
    return this.orders.list(filter);
  }

  async adminGet(orderId: string): Promise<OrderRecord> {
    const order = await this.orders.findById(orderId);
    if (!order) throw Object.assign(new Error("Megrendelés nem található."), { status: 404 });
    return order;
  }

  adminSetStatus(orderId: string, status: OrderStatus): Promise<OrderRecord> {
    return this.orders.updateStatus(orderId, status);
  }

  async adminDelete(orderId: string): Promise<void> {
    const order = await this.adminGet(orderId);
    for (const p of order.photos) {
      await this.photos.deletePhoto(p.relativePath);
    }
    await this.orders.delete(orderId);
  }

  adminRevokeEditLink(orderId: string): Promise<OrderRecord> {
    return this.orders.revokeEditToken(orderId);
  }

  adminCounts(): Promise<Record<OrderStatus, number>> {
    return this.orders.countsByStatus();
  }

  async exportOrderJson(orderId: string): Promise<object> {
    const order = await this.adminGet(orderId);
    return this.toExport(order);
  }

  private toExport(order: OrderRecord): object {
    return {
      publicOrderId: order.publicOrderId,
      fullName: order.fullName,
      phone: order.phone,
      email: order.email,
      calendarYear: order.calendarYear,
      calendarLanguage: order.calendarLanguage,
      calendarFormatPref: order.calendarFormatPref,
      comments: order.comments,
      status: order.status,
      createdAt: order.createdAt,
      updatedAt: order.updatedAt,
      photos: order.photos.map((p) => ({
        month: p.month,
        sortOrder: p.sortOrder,
        originalFilename: p.originalFilename,
        relativePath: p.relativePath,
        mimeType: p.mimeType,
        sizeBytes: p.sizeBytes,
      })),
      events: order.events.map((e) => ({
        eventDate: e.eventDate,
        description: e.description,
      })),
    };
  }
}
