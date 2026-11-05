import fs from "node:fs/promises";
import path from "node:path";
import { randomUUID } from "node:crypto";
import { config } from "../config.js";
import type { StoredPhotoMeta } from "../domain/types.js";
import type { IPhotoStorage, PhotoUploadPayload } from "../interfaces/IPhotoStorage.js";

const MONTH_DIRS = [
  "01_January",
  "02_February",
  "03_March",
  "04_April",
  "05_May",
  "06_June",
  "07_July",
  "08_August",
  "09_September",
  "10_October",
  "11_November",
  "12_December",
];

function safeExt(originalFilename: string, mimeType: string): string {
  const ext = path.extname(originalFilename).toLowerCase();
  if ((config.allowedExtensions as readonly string[]).includes(ext)) return ext;
  if (mimeType === "image/png") return ".png";
  if (mimeType === "image/webp") return ".webp";
  return ".jpg";
}

export class LocalPhotoStorage implements IPhotoStorage {
  constructor(private readonly root = config.storageRoot) {}

  private orderDir(orderFolderName: string, year: number): string {
    return path.join(this.root, String(year), orderFolderName);
  }

  async ensureOrderFolders(orderFolderName: string, year: number): Promise<string> {
    const base = this.orderDir(orderFolderName, year);
    await fs.mkdir(path.join(base, "photos"), { recursive: true });
    for (const m of MONTH_DIRS) {
      await fs.mkdir(path.join(base, "photos", m), { recursive: true });
    }
    return base;
  }

  async savePhoto(
    orderFolderName: string,
    year: number,
    month: number,
    payload: PhotoUploadPayload
  ): Promise<StoredPhotoMeta> {
    await this.ensureOrderFolders(orderFolderName, year);
    const monthDir = MONTH_DIRS[month - 1];
    if (!monthDir) throw new Error("Invalid month");
    const storedFilename = `${randomUUID()}${safeExt(payload.originalFilename, payload.mimeType)}`;
    const relativePath = path
      .join(String(year), orderFolderName, "photos", monthDir, storedFilename)
      .replace(/\\/g, "/");
    const abs = path.join(this.root, relativePath);
    await fs.mkdir(path.dirname(abs), { recursive: true });
    await fs.writeFile(abs, payload.buffer);
    return {
      relativePath,
      storedFilename,
      originalFilename: payload.originalFilename,
      mimeType: payload.mimeType,
      sizeBytes: payload.sizeBytes,
    };
  }

  async deletePhoto(relativePath: string): Promise<void> {
    const abs = this.getAbsolutePath(relativePath);
    try {
      await fs.unlink(abs);
    } catch {
      // ignore missing
    }
  }

  getAbsolutePath(relativePath: string): string {
    const normalized = relativePath.replace(/\\/g, "/");
    if (normalized.includes("..")) throw new Error("Invalid path");
    return path.join(this.root, normalized);
  }

  async writeOrderJson(
    orderFolderName: string,
    year: number,
    data: unknown
  ): Promise<string> {
    await this.ensureOrderFolders(orderFolderName, year);
    const relativePath = path
      .join(String(year), orderFolderName, "order.json")
      .replace(/\\/g, "/");
    const abs = path.join(this.root, relativePath);
    await fs.writeFile(abs, JSON.stringify(data, null, 2), "utf8");
    return relativePath;
  }

  async createZipOfOrder(): Promise<Buffer | null> {
    // Optional enhancement — admin can download files individually.
    return null;
  }
}

export { MONTH_DIRS };
