import { randomUUID } from "node:crypto";
import path from "node:path";
import type { StoredPhotoMeta } from "../domain/types.js";
import type { IPhotoStorage, PhotoUploadPayload } from "../interfaces/IPhotoStorage.js";
import type { IGoogleDriveClient } from "./driveClient.js";
import { driveMonthFolderNames } from "./driveClient.js";
import { GOOGLE_DRIVE_LAYOUT } from "./layout.js";

export { GOOGLE_DRIVE_LAYOUT };

/**
 * Google Drive photo storage adapter.
 * Requires an injected IGoogleDriveClient. Local development uses LocalPhotoStorage instead.
 * Activate via PHOTO_STORAGE_ADAPTER=google-drive only when a real client is wired.
 */
export class GoogleDrivePhotoStorage implements IPhotoStorage {
  private orderFolderIds = new Map<string, string>();
  private monthFolderIds = new Map<string, string>();
  private fileIdsByRelative = new Map<string, string>();

  constructor(
    private readonly drive: IGoogleDriveClient,
    private readonly rootFolderId: string | null = null
  ) {}

  private key(orderFolderName: string, year: number): string {
    return `${year}/${orderFolderName}`;
  }

  async ensureOrderFolders(orderFolderName: string, year: number): Promise<string> {
    const cacheKey = this.key(orderFolderName, year);
    if (this.orderFolderIds.has(cacheKey)) {
      return this.orderFolderIds.get(cacheKey)!;
    }

    const root = await this.drive.getOrCreateFolder(
      this.rootFolderId,
      GOOGLE_DRIVE_LAYOUT.root
    );
    const yearFolder = await this.drive.getOrCreateFolder(root.id, String(year));
    const orderFolder = await this.drive.getOrCreateFolder(yearFolder.id, orderFolderName);
    await this.drive.getOrCreateFolder(orderFolder.id, GOOGLE_DRIVE_LAYOUT.meta);
    const months = await this.drive.ensureChildFolders(
      orderFolder.id,
      driveMonthFolderNames()
    );

    this.orderFolderIds.set(cacheKey, orderFolder.id);
    for (const [name, ref] of Object.entries(months)) {
      this.monthFolderIds.set(`${cacheKey}/${name}`, ref.id);
    }
    return orderFolder.id;
  }

  async savePhoto(
    orderFolderName: string,
    year: number,
    month: number,
    payload: PhotoUploadPayload
  ): Promise<StoredPhotoMeta> {
    await this.ensureOrderFolders(orderFolderName, year);
    const monthName = driveMonthFolderNames()[month - 1];
    if (!monthName) throw new Error("Invalid month");
    const cacheKey = this.key(orderFolderName, year);
    const monthFolderId = this.monthFolderIds.get(`${cacheKey}/${monthName}`);
    if (!monthFolderId) throw new Error("Month folder missing");

    const ext = path.extname(payload.originalFilename).toLowerCase() || ".jpg";
    const storedFilename = `${randomUUID()}${ext}`;
    const idempotencyKey = `${cacheKey}/${monthName}/${storedFilename}`;
    const uploaded = await this.drive.uploadFile(
      monthFolderId,
      storedFilename,
      payload.mimeType,
      payload.buffer,
      idempotencyKey
    );

    const relativePath = `${year}/${orderFolderName}/${monthName}/${storedFilename}`;
    this.fileIdsByRelative.set(relativePath, uploaded.id);
    return {
      relativePath,
      storedFilename,
      originalFilename: payload.originalFilename,
      mimeType: payload.mimeType,
      sizeBytes: payload.sizeBytes,
    };
  }

  async deletePhoto(relativePath: string): Promise<void> {
    const id = this.fileIdsByRelative.get(relativePath);
    if (id) {
      await this.drive.deleteFile(id);
      this.fileIdsByRelative.delete(relativePath);
    }
  }

  getAbsolutePath(relativePath: string): string {
    // Drive files are not on the local filesystem; return a virtual URI for diagnostics.
    return `gdrive://${relativePath}`;
  }

  async writeOrderJson(
    orderFolderName: string,
    year: number,
    data: unknown
  ): Promise<string> {
    const orderFolderId = await this.ensureOrderFolders(orderFolderName, year);
    const meta = await this.drive.getOrCreateFolder(orderFolderId, GOOGLE_DRIVE_LAYOUT.meta);
    const content = JSON.stringify(data, null, 2);
    const idempotencyKey = `${year}/${orderFolderName}/megrendeles.json`;
    await this.drive.uploadFile(
      meta.id,
      GOOGLE_DRIVE_LAYOUT.metaFile,
      "application/json",
      content,
      idempotencyKey
    );
    return `${year}/${orderFolderName}/${GOOGLE_DRIVE_LAYOUT.meta}/${GOOGLE_DRIVE_LAYOUT.metaFile}`;
  }

  async createZipOfOrder(): Promise<Buffer | null> {
    // ZIP packaging for Drive-backed orders is handled via Apps Script / admin export offline.
    return null;
  }
}
