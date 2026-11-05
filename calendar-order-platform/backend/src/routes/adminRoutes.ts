import { Router } from "express";
import { config } from "../config.js";
import { createPhotoStorage } from "../container.js";
import type { IOrderService } from "../interfaces/IOrderService.js";
import {
  ADMIN_COOKIE,
  createAdminSessionToken,
  requireAdmin,
} from "../middleware/adminAuth.js";
import type { OrderStatus } from "../domain/types.js";

export function adminRoutes(orders: IOrderService): Router {
  const router = Router();
  const storage = createPhotoStorage();

  router.post("/login", (req, res) => {
    const { username, password } = req.body ?? {};
    if (
      username !== config.adminUsername ||
      password !== config.adminPassword
    ) {
      res.status(401).json({ error: "Hibás felhasználónév vagy jelszó." });
      return;
    }
    const token = createAdminSessionToken(username);
    res.cookie(ADMIN_COOKIE, token, {
      httpOnly: true,
      sameSite: "lax",
      secure: false,
      maxAge: 1000 * 60 * 60 * 12,
    });
    res.json({ ok: true, username });
  });

  router.post("/logout", (_req, res) => {
    res.clearCookie(ADMIN_COOKIE);
    res.json({ ok: true });
  });

  router.get("/me", requireAdmin, (_req, res) => {
    res.json({ username: config.adminUsername });
  });

  router.get("/stats", requireAdmin, async (_req, res, next) => {
    try {
      const counts = await orders.adminCounts();
      const total = Object.values(counts).reduce((a, b) => a + b, 0);
      res.json({ total, counts });
    } catch (e) {
      next(e);
    }
  });

  router.get("/orders", requireAdmin, async (req, res, next) => {
    try {
      const list = await orders.adminList({
        search: req.query.search as string | undefined,
        year: req.query.year ? Number(req.query.year) : undefined,
        status: req.query.status as OrderStatus | undefined,
        sort:
          req.query.sort === "createdAt_asc"
            ? "createdAt_asc"
            : "createdAt_desc",
      });
      res.json({
        orders: list.map((o) => ({
          id: o.id,
          publicOrderId: o.publicOrderId,
          fullName: o.fullName,
          phone: o.phone,
          email: o.email,
          calendarYear: o.calendarYear,
          status: o.status,
          createdAt: o.createdAt,
          updatedAt: o.updatedAt,
          photoCount: o.photos.length,
          eventCount: o.events.length,
          editTokenRevoked: o.editTokenRevoked,
        })),
      });
    } catch (e) {
      next(e);
    }
  });

  router.get("/orders/:id", requireAdmin, async (req, res, next) => {
    try {
      const order = await orders.adminGet(req.params.id);
      const { editTokenHash, ...safe } = order;
      res.json({ order: safe });
    } catch (e) {
      next(e);
    }
  });

  router.patch("/orders/:id/status", requireAdmin, async (req, res, next) => {
    try {
      const order = await orders.adminSetStatus(
        req.params.id,
        req.body.status as OrderStatus
      );
      res.json({ order });
    } catch (e) {
      next(e);
    }
  });

  router.post("/orders/:id/revoke-edit-link", requireAdmin, async (req, res, next) => {
    try {
      const order = await orders.adminRevokeEditLink(req.params.id);
      res.json({ order });
    } catch (e) {
      next(e);
    }
  });

  router.delete("/orders/:id", requireAdmin, async (req, res, next) => {
    try {
      await orders.adminDelete(req.params.id);
      res.json({ ok: true });
    } catch (e) {
      next(e);
    }
  });

  router.get("/orders/:id/export.json", requireAdmin, async (req, res, next) => {
    try {
      const data = await orders.exportOrderJson(req.params.id);
      res.setHeader(
        "Content-Disposition",
        `attachment; filename="${(data as { publicOrderId: string }).publicOrderId}.json"`
      );
      res.json(data);
    } catch (e) {
      next(e);
    }
  });

  router.get("/orders/:id/export.csv", requireAdmin, async (req, res, next) => {
    try {
      const order = await orders.adminGet(req.params.id);
      const lines = [
        "field,value",
        `orderId,${csv(order.publicOrderId)}`,
        `fullName,${csv(order.fullName)}`,
        `phone,${csv(order.phone)}`,
        `email,${csv(order.email ?? "")}`,
        `year,${order.calendarYear}`,
        `status,${order.status}`,
        `photos,${order.photos.length}`,
        `events,${order.events.length}`,
      ];
      for (const e of order.events) {
        lines.push(`event,${csv(e.eventDate.toISOString().slice(0, 10) + " — " + e.description)}`);
      }
      res.setHeader("Content-Type", "text/csv; charset=utf-8");
      res.setHeader(
        "Content-Disposition",
        `attachment; filename="${order.publicOrderId}.csv"`
      );
      res.send(lines.join("\n"));
    } catch (e) {
      next(e);
    }
  });

  router.get("/orders/:id/photos/:photoId", requireAdmin, async (req, res, next) => {
    try {
      const order = await orders.adminGet(req.params.id);
      const photo = order.photos.find((p) => p.id === req.params.photoId);
      if (!photo) {
        res.status(404).json({ error: "Photo not found" });
        return;
      }
      res.sendFile(storage.getAbsolutePath(photo.relativePath));
    } catch (e) {
      next(e);
    }
  });

  return router;
}

function csv(v: string): string {
  if (/[",\n]/.test(v)) return `"${v.replace(/"/g, '""')}"`;
  return v;
}
