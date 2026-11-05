import type {
  CreateOrderInput,
  OrderListFilter,
  OrderRecord,
  OrderStatus,
  UpdateOrderInput,
} from "../domain/types.js";

export interface IOrderRepository {
  create(
    input: CreateOrderInput,
    publicOrderId: string,
    editTokenHash: string
  ): Promise<OrderRecord>;
  findById(id: string): Promise<OrderRecord | null>;
  findByPublicOrderId(publicOrderId: string): Promise<OrderRecord | null>;
  findByEditTokenHash(hash: string): Promise<OrderRecord | null>;
  list(filter?: OrderListFilter): Promise<OrderRecord[]>;
  update(id: string, input: UpdateOrderInput): Promise<OrderRecord>;
  updateStatus(id: string, status: OrderStatus): Promise<OrderRecord>;
  revokeEditToken(id: string): Promise<OrderRecord>;
  delete(id: string): Promise<void>;
  addPhoto(
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
  ): Promise<OrderRecord>;
  deletePhoto(orderId: string, photoId: string): Promise<OrderRecord>;
  reorderPhotos(
    orderId: string,
    month: number,
    photoIdsInOrder: string[]
  ): Promise<OrderRecord>;
  countsByStatus(): Promise<Record<OrderStatus, number>>;
}
