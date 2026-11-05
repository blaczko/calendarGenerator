import { GOOGLE_DRIVE_LAYOUT } from "./layout.js";

/** Minimal Drive API surface used by GoogleDrivePhotoStorage (injectable for tests). */
export interface DriveFolderRef {
  id: string;
  name: string;
  url: string;
}

export interface DriveFileRef {
  id: string;
  name: string;
  url: string;
}

export interface IGoogleDriveClient {
  getOrCreateFolder(parentId: string | null, name: string): Promise<DriveFolderRef>;
  ensureChildFolders(parentId: string, names: string[]): Promise<Record<string, DriveFolderRef>>;
  uploadFile(
    folderId: string,
    name: string,
    mimeType: string,
    content: Buffer | string,
    idempotencyKey?: string
  ): Promise<DriveFileRef>;
  deleteFile(fileId: string): Promise<void>;
  findFileByName(folderId: string, name: string): Promise<DriveFileRef | null>;
}

/** In-memory Drive client for unit tests (no Google credentials). */
export class InMemoryGoogleDriveClient implements IGoogleDriveClient {
  folders = new Map<string, { id: string; name: string; parentId: string | null }>();
  files = new Map<
    string,
    { id: string; name: string; folderId: string; mimeType: string; content: Buffer; key?: string }
  >();
  private seq = 0;

  private nextId(prefix: string): string {
    this.seq += 1;
    return `${prefix}_${this.seq}`;
  }

  async getOrCreateFolder(parentId: string | null, name: string): Promise<DriveFolderRef> {
    for (const f of this.folders.values()) {
      if (f.parentId === parentId && f.name === name) {
        return { id: f.id, name: f.name, url: `memory://folder/${f.id}` };
      }
    }
    const id = this.nextId("folder");
    this.folders.set(id, { id, name, parentId });
    return { id, name, url: `memory://folder/${id}` };
  }

  async ensureChildFolders(
    parentId: string,
    names: string[]
  ): Promise<Record<string, DriveFolderRef>> {
    const out: Record<string, DriveFolderRef> = {};
    for (const name of names) {
      out[name] = await this.getOrCreateFolder(parentId, name);
    }
    return out;
  }

  async uploadFile(
    folderId: string,
    name: string,
    mimeType: string,
    content: Buffer | string,
    idempotencyKey?: string
  ): Promise<DriveFileRef> {
    if (idempotencyKey) {
      for (const f of this.files.values()) {
        if (f.key === idempotencyKey) {
          return { id: f.id, name: f.name, url: `memory://file/${f.id}` };
        }
      }
    }
    const id = this.nextId("file");
    const buf = typeof content === "string" ? Buffer.from(content, "utf8") : content;
    this.files.set(id, { id, name, folderId, mimeType, content: buf, key: idempotencyKey });
    return { id, name, url: `memory://file/${id}` };
  }

  async deleteFile(fileId: string): Promise<void> {
    this.files.delete(fileId);
  }

  async findFileByName(folderId: string, name: string): Promise<DriveFileRef | null> {
    for (const f of this.files.values()) {
      if (f.folderId === folderId && f.name === name) {
        return { id: f.id, name: f.name, url: `memory://file/${f.id}` };
      }
    }
    return null;
  }
}

export function driveMonthFolderNames(): string[] {
  return [...GOOGLE_DRIVE_LAYOUT.months];
}
