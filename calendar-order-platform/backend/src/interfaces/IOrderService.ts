import type {
  CreateOrderInput,
  OrderListFilter,
  OrderRecord,
  OrderStatus,
  UpdateOrderInput,
} from "../domain/types.js";
import type { PhotoUploadPayload } from "./IPhotoStorage.js";

export interface CreateOrderResult {
  order: OrderRecord;
  editToken: string;
  editUrl: string;
}

export interface IOrderService {
  createOrder(input: CreateOrderInput): Promise<CreateOrderResult>;
  getOrderForEdit(editToken: string): Promise<OrderRecord>;
  updateOrder(editToken: string, input: UpdateOrderInput): Promise<OrderRecord>;
  uploadPhoto(
    editToken: string,
    month: number,
    payload: PhotoUploadPayload
  ): Promise<OrderRecord>;
  deletePhoto(editToken: string, photoId: string): Promise<OrderRecord>;
  reorderPhotos(
    editToken: string,
    month: number,
    photoIds: string[]
  ): Promise<OrderRecord>;
  adminList(filter?: OrderListFilter): Promise<OrderRecord[]>;
  adminGet(orderId: string): Promise<OrderRecord>;
  adminSetStatus(orderId: string, status: OrderStatus): Promise<OrderRecord>;
  adminDelete(orderId: string): Promise<void>;
  adminRevokeEditLink(orderId: string): Promise<OrderRecord>;
  adminCounts(): Promise<Record<OrderStatus, number>>;
  exportOrderJson(orderId: string): Promise<object>;
}
