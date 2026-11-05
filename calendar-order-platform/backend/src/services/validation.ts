import { z } from "zod";
import { config } from "../config.js";

/** Romanian numbers only: +40 / 0040 / 0 + 2|3|7 + 8 digits (spaces/dashes allowed). */
export function isRomanianPhone(raw: string): boolean {
  const cleaned = raw.replace(/[\s\-().]/g, "");
  return (
    /^\+40[237]\d{8}$/.test(cleaned) ||
    /^0040[237]\d{8}$/.test(cleaned) ||
    /^0[237]\d{8}$/.test(cleaned)
  );
}

export const createOrderSchema = z.object({
  fullName: z.string().trim().min(1, "A név megadása kötelező."),
  phone: z
    .string()
    .trim()
    .refine(isRomanianPhone, {
      message:
        "Csak román telefonszám fogadható el (pl. +40 712 345 678 vagy 0712 345 678).",
    }),
  email: z
    .string()
    .trim()
    .email("Érvénytelen e-mail cím.")
    .optional()
    .or(z.literal(""))
    .nullable(),
  calendarYear: z.coerce.number().int().refine(
    (y) => config.supportedYears.includes(y),
    "A kiválasztott év nem támogatott."
  ),
  calendarLanguage: z.string().default("hu"),
  calendarFormatPref: z
    .union([z.enum(["portrait", "landscape"]), z.literal(""), z.null()])
    .optional()
    .transform((v) => (v === "" || v == null ? null : v)),
  comments: z.string().optional().nullable(),
  // Draft orders may be created before final consent (e.g. photo uploads).
  // Final customer confirmation must send privacyConsent: true.
  privacyConsent: z.boolean(),
  events: z
    .array(
      z.object({
        eventDate: z.string().min(1),
        description: z.string().trim().min(1, "Az esemény leírása kötelező."),
      })
    )
    .max(config.maxEventsPerOrder)
    .default([]),
});

export const updateOrderSchema = createOrderSchema.partial().extend({
  events: z
    .array(
      z.object({
        eventDate: z.string().min(1),
        description: z.string().trim().min(1),
      })
    )
    .max(config.maxEventsPerOrder)
    .optional(),
});

export function assertEventsInYear(
  year: number,
  events: Array<{ eventDate: string; description: string }>
): void {
  for (const e of events) {
    const d = new Date(e.eventDate);
    if (Number.isNaN(d.getTime())) {
      throw new Error(`Érvénytelen dátum: ${e.eventDate}`);
    }
    if (d.getUTCFullYear() !== year && d.getFullYear() !== year) {
      throw new Error(`Az esemény dátuma (${e.eventDate}) nem esik a ${year} évre.`);
    }
  }
}

export function assertValidUpload(file: {
  mimetype: string;
  originalname: string;
  size: number;
}): void {
  const maxBytes = config.maxFileSizeMb * 1024 * 1024;
  if (file.size > maxBytes) {
    throw new Error(`A fájl túl nagy (max. ${config.maxFileSizeMb} MB).`);
  }
  if (!(config.allowedMimeTypes as readonly string[]).includes(file.mimetype)) {
    throw new Error("Nem támogatott fájltípus. Engedélyezett: JPG, PNG, WebP.");
  }
  const ext = file.originalname.toLowerCase().slice(file.originalname.lastIndexOf("."));
  if (!(config.allowedExtensions as readonly string[]).includes(ext)) {
    throw new Error("Nem támogatott fájlkiterjesztés.");
  }
}
