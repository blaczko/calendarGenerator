import type { NextFunction, Request, Response } from "express";
import { ZodError } from "zod";

export function errorHandler(
  err: unknown,
  _req: Request,
  res: Response,
  _next: NextFunction
): void {
  if (err instanceof ZodError) {
    res.status(400).json({
      error: "Validation failed",
      details: err.errors.map((e) => ({ path: e.path.join("."), message: e.message })),
    });
    return;
  }
  const multerCode = (err as { code?: string }).code;
  if (multerCode === "LIMIT_FILE_SIZE") {
    res.status(400).json({ error: "A fájl túl nagy." });
    return;
  }
  const status = (err as { status?: number }).status ?? 400;
  const message = err instanceof Error ? err.message : "Unexpected error";
  if (status >= 500) console.error(err);
  res.status(status).json({ error: message });
}
