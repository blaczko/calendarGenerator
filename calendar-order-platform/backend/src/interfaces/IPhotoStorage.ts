import type { StoredPhotoMeta } from "../domain/types.js";

export interface PhotoUploadPayload {
  buffer: Buffer;
  originalFilename: string;
  mimeType: string;
  sizeBytes: number;
}

export interface IPhotoStorage {
  savePhoto(
    orderFolderName: string,
    year: number,
    month: number,
    payload: PhotoUploadPayload
  ): Promise<StoredPhotoMeta>;
  deletePhoto(relativePath: string): Promise<void>;
  getAbsolutePath(relativePath: string): string;
  ensureOrderFolders(orderFolderName: string, year: number): Promise<string>;
  writeOrderJson(
    orderFolderName: string,
    year: number,
    data: unknown
  ): Promise<string>;
  createZipOfOrder(
    orderFolderName: string,
    year: number
  ): Promise<Buffer | null>;
}
