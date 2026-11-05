import { config } from "./config.js";
import { LocalOrderRepository } from "./repositories/LocalOrderRepository.js";
import { OrderService } from "./services/OrderService.js";
import { LocalPhotoStorage } from "./storage/LocalPhotoStorage.js";
import type { IOrderRepository } from "./interfaces/IOrderRepository.js";
import type { IPhotoStorage } from "./interfaces/IPhotoStorage.js";
import type { IOrderService } from "./interfaces/IOrderService.js";

export function createPhotoStorage(): IPhotoStorage {
  if (config.photoStorageAdapter === "google-drive") {
    // Dynamic import kept out of default path so local runs need no Google deps.
    throw new Error(
      "PHOTO_STORAGE_ADAPTER=google-drive is not enabled in this local build path. Use local."
    );
  }
  return new LocalPhotoStorage();
}

export function createOrderRepository(): IOrderRepository {
  if (config.orderRepositoryAdapter === "google-sheets") {
    throw new Error(
      "ORDER_REPOSITORY_ADAPTER=google-sheets is not enabled for local development. Use local."
    );
  }
  return new LocalOrderRepository();
}

export function createOrderService(): IOrderService {
  return new OrderService(createOrderRepository(), createPhotoStorage());
}
