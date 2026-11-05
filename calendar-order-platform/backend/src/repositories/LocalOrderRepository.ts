import type { Order, Photo, CalendarEvent, OrderStatus as PrismaStatus } from "@prisma/client";
import { prisma } from "../db.js";
import type {
  CreateOrderInput,
  OrderListFilter,
  OrderRecord,
  OrderStatus,
  UpdateOrderInput,
} from "../domain/types.js";
import type { IOrderRepository } from "../interfaces/IOrderRepository.js";

type FullOrder = Order & { photos: Photo[]; events: CalendarEvent[] };

function mapStatus(s: PrismaStatus): OrderStatus {
  return s as OrderStatus;
}

function mapOrder(o: FullOrder): OrderRecord {
  return {
    id: o.id,
    publicOrderId: o.publicOrderId,
    editTokenHash: o.editTokenHash,
    editTokenRevoked: o.editTokenRevoked,
    fullName: o.fullName,
    phone: o.phone,
    email: o.email,
    calendarYear: o.calendarYear,
    calendarLanguage: o.calendarLanguage,
    calendarFormatPref: o.calendarFormatPref,
    comments: o.comments,
    privacyConsent: o.privacyConsent,
    status: mapStatus(o.status),
    createdAt: o.createdAt,
    updatedAt: o.updatedAt,
    photos: o.photos.map((p) => ({
      id: p.id,
      month: p.month,
      sortOrder: p.sortOrder,
      storedFilename: p.storedFilename,
      originalFilename: p.originalFilename,
      mimeType: p.mimeType,
      sizeBytes: p.sizeBytes,
      relativePath: p.relativePath,
      createdAt: p.createdAt,
    })),
    events: o.events.map((e) => ({
      id: e.id,
      eventDate: e.eventDate,
      description: e.description,
      createdAt: e.createdAt,
    })),
  };
}

const include = { photos: { orderBy: [{ month: "asc" as const }, { sortOrder: "asc" as const }] }, events: { orderBy: { eventDate: "asc" as const } } };

export class LocalOrderRepository implements IOrderRepository {
  async create(
    input: CreateOrderInput,
    publicOrderId: string,
    editTokenHash: string
  ): Promise<OrderRecord> {
    const created = await prisma.order.create({
      data: {
        publicOrderId,
        editTokenHash,
        fullName: input.fullName,
        phone: input.phone,
        email: input.email ?? null,
        calendarYear: input.calendarYear,
        calendarLanguage: input.calendarLanguage ?? "hu",
        calendarFormatPref: input.calendarFormatPref ?? null,
        comments: input.comments ?? null,
        privacyConsent: input.privacyConsent,
        events: {
          create: input.events.map((e) => ({
            eventDate: new Date(e.eventDate),
            description: e.description.trim(),
          })),
        },
      },
      include,
    });
    return mapOrder(created);
  }

  async findById(id: string): Promise<OrderRecord | null> {
    const o = await prisma.order.findUnique({ where: { id }, include });
    return o ? mapOrder(o) : null;
  }

  async findByPublicOrderId(publicOrderId: string): Promise<OrderRecord | null> {
    const o = await prisma.order.findUnique({ where: { publicOrderId }, include });
    return o ? mapOrder(o) : null;
  }

  async findByEditTokenHash(hash: string): Promise<OrderRecord | null> {
    const o = await prisma.order.findFirst({
      where: { editTokenHash: hash, editTokenRevoked: false },
      include,
    });
    return o ? mapOrder(o) : null;
  }

  async list(filter: OrderListFilter = {}): Promise<OrderRecord[]> {
    const where: Record<string, unknown> = {};
    if (filter.year) where.calendarYear = filter.year;
    if (filter.status) where.status = filter.status;
    if (filter.search?.trim()) {
      const q = filter.search.trim();
      where.OR = [
        { fullName: { contains: q } },
        { phone: { contains: q } },
        { publicOrderId: { contains: q } },
        { email: { contains: q } },
      ];
    }
    const sort = filter.sort === "createdAt_asc" ? "asc" : "desc";
    const rows = await prisma.order.findMany({
      where,
      include,
      orderBy: { createdAt: sort },
    });
    return rows.map(mapOrder);
  }

  async update(id: string, input: UpdateOrderInput): Promise<OrderRecord> {
    await prisma.$transaction(async (tx) => {
      await tx.order.update({
        where: { id },
        data: {
          fullName: input.fullName,
          phone: input.phone,
          email: input.email === undefined ? undefined : input.email,
          calendarYear: input.calendarYear,
          calendarLanguage: input.calendarLanguage,
          calendarFormatPref:
            input.calendarFormatPref === undefined
              ? undefined
              : input.calendarFormatPref,
          comments: input.comments === undefined ? undefined : input.comments,
          privacyConsent:
            input.privacyConsent === undefined ? undefined : input.privacyConsent,
        },
      });
      if (input.events) {
        await tx.calendarEvent.deleteMany({ where: { orderId: id } });
        if (input.events.length) {
          await tx.calendarEvent.createMany({
            data: input.events.map((e) => ({
              orderId: id,
              eventDate: new Date(e.eventDate),
              description: e.description.trim(),
            })),
          });
        }
      }
    });
    const o = await this.findById(id);
    if (!o) throw new Error("Order not found after update");
    return o;
  }

  async updateStatus(id: string, status: OrderStatus): Promise<OrderRecord> {
    const o = await prisma.order.update({
      where: { id },
      data: { status },
      include,
    });
    return mapOrder(o);
  }

  async revokeEditToken(id: string): Promise<OrderRecord> {
    const o = await prisma.order.update({
      where: { id },
      data: { editTokenRevoked: true },
      include,
    });
    return mapOrder(o);
  }

  async delete(id: string): Promise<void> {
    await prisma.order.delete({ where: { id } });
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
    await prisma.photo.create({ data: { orderId, ...photo } });
    const o = await this.findById(orderId);
    if (!o) throw new Error("Order not found");
    return o;
  }

  async deletePhoto(orderId: string, photoId: string): Promise<OrderRecord> {
    await prisma.photo.deleteMany({ where: { id: photoId, orderId } });
    const o = await this.findById(orderId);
    if (!o) throw new Error("Order not found");
    return o;
  }

  async reorderPhotos(
    orderId: string,
    month: number,
    photoIdsInOrder: string[]
  ): Promise<OrderRecord> {
    await prisma.$transaction(
      photoIdsInOrder.map((id, index) =>
        prisma.photo.updateMany({
          where: { id, orderId, month },
          data: { sortOrder: index },
        })
      )
    );
    const o = await this.findById(orderId);
    if (!o) throw new Error("Order not found");
    return o;
  }

  async countsByStatus(): Promise<Record<OrderStatus, number>> {
    const groups = await prisma.order.groupBy({
      by: ["status"],
      _count: { _all: true },
    });
    const base: Record<OrderStatus, number> = {
      NEW: 0,
      IN_PROGRESS: 0,
      WAITING_FOR_CUSTOMER: 0,
      COMPLETED: 0,
      CANCELLED: 0,
    };
    for (const g of groups) base[mapStatus(g.status)] = g._count._all;
    return base;
  }
}
