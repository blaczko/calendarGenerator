import path from "node:path";
import { fileURLToPath } from "node:url";
import dotenv from "dotenv";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
/** backend/ package root (…/calendar-order-platform/backend) */
const backendRoot = path.resolve(__dirname, "..");
dotenv.config({ path: path.join(backendRoot, ".env") });

function intEnv(name: string, fallback: number): number {
  const raw = process.env[name];
  if (!raw) return fallback;
  const n = Number(raw);
  return Number.isFinite(n) ? n : fallback;
}

function listYears(raw: string | undefined): number[] {
  const years = (raw ?? "2026,2027,2028,2029")
    .split(",")
    .map((y) => Number(y.trim()))
    .filter((y) => Number.isInteger(y) && y >= 2000 && y <= 2100);
  return years.length ? years : [new Date().getFullYear()];
}

function resolveStorageRoot(): string {
  const raw = process.env.STORAGE_ROOT ?? "../storage/orders";
  return path.isAbsolute(raw) ? raw : path.resolve(backendRoot, raw);
}

export const config = {
  port: intEnv("PORT", 4010),
  corsOrigin: process.env.CORS_ORIGIN ?? "http://localhost:5174",
  publicAppUrl: process.env.PUBLIC_APP_URL ?? "http://localhost:5174",
  adminUsername: process.env.ADMIN_USERNAME ?? "admin",
  adminPassword: process.env.ADMIN_PASSWORD ?? "changeme-admin",
  adminSessionSecret: process.env.ADMIN_SESSION_SECRET ?? "dev-secret-change-me",
  storageRoot: resolveStorageRoot(),
  maxFileSizeMb: intEnv("MAX_FILE_SIZE_MB", 10),
  maxPhotosPerMonth: intEnv("MAX_PHOTOS_PER_MONTH", 6),
  maxEventsPerOrder: intEnv("MAX_EVENTS_PER_ORDER", 100),
  supportedYears: listYears(process.env.SUPPORTED_YEARS),
  photoStorageAdapter: (process.env.PHOTO_STORAGE_ADAPTER ?? "local") as
    | "local"
    | "google-drive",
  orderRepositoryAdapter: (process.env.ORDER_REPOSITORY_ADAPTER ?? "local") as
    | "local"
    | "google-sheets",
  allowedMimeTypes: ["image/jpeg", "image/png", "image/webp"] as const,
  allowedExtensions: [".jpg", ".jpeg", ".png", ".webp"] as const,
};
