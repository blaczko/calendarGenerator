import type { NextFunction, Request, Response } from "express";
import { createHmac, timingSafeEqual } from "node:crypto";
import { config } from "../config.js";

const COOKIE = "cop_admin_session";

function sign(payload: string): string {
  return createHmac("sha256", config.adminSessionSecret).update(payload).digest("hex");
}

export function createAdminSessionToken(username: string): string {
  const payload = `${username}:${Date.now()}`;
  return `${Buffer.from(payload).toString("base64url")}.${sign(payload)}`;
}

export function verifyAdminSessionToken(token: string | undefined): boolean {
  if (!token) return false;
  const [encoded, signature] = token.split(".");
  if (!encoded || !signature) return false;
  const payload = Buffer.from(encoded, "base64url").toString("utf8");
  const expected = sign(payload);
  try {
    const a = Buffer.from(signature);
    const b = Buffer.from(expected);
    if (a.length !== b.length) return false;
    if (!timingSafeEqual(a, b)) return false;
  } catch {
    return false;
  }
  const [username, ts] = payload.split(":");
  if (username !== config.adminUsername) return false;
  const age = Date.now() - Number(ts);
  return Number.isFinite(age) && age < 1000 * 60 * 60 * 12;
}

export function requireAdmin(req: Request, res: Response, next: NextFunction): void {
  const token = req.cookies?.[COOKIE] as string | undefined;
  if (!verifyAdminSessionToken(token)) {
    res.status(401).json({ error: "Admin authentication required." });
    return;
  }
  next();
}

export { COOKIE as ADMIN_COOKIE };
