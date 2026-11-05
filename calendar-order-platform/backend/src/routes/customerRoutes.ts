import { Router } from "express";
import multer from "multer";
import { config } from "../config.js";
import type { IOrderService } from "../interfaces/IOrderService.js";
import { createPhotoStorage } from "../container.js";

const upload = multer({
  storage: multer.memoryStorage(),
  limits: { fileSize: config.maxFileSizeMb * 1024 * 1024, files: 1 },
});

export function customerRoutes(orders: IOrderService): Router {
  const router = Router();
  const storage = createPhotoStorage();

  router.get("/config", (_req, res) => {
    res.json({
      supportedYears: config.supportedYears,
      maxPhotosPerMonth: config.maxPhotosPerMonth,
      maxFileSizeMb: config.maxFileSizeMb,
      maxEventsPerOrder: config.maxEventsPerOrder,
      languages: ["hu"],
      calendarFormats: [
        { value: "portrait", labelHu: "Álló (portrait) – A4" },
        { value: "landscape", labelHu: "Fekvő (landscape) – A4" },
      ],
    });
  });

  router.post("/orders", async (req, res, next) => {
    try {
      const result = await orders.createOrder(req.body);
      res.status(201).json({
        orderId: result.order.publicOrderId,
        internalId: result.order.id,
        editToken: result.editToken,
        editUrl: result.editUrl,
        order: sanitizeCustomerOrder(result.order),
      });
    } catch (e) {
      next(e);
    }
  });

  router.get("/orders/edit/:token", async (req, res, next) => {
    try {
      const order = await orders.getOrderForEdit(req.params.token);
      res.json({ order: sanitizeCustomerOrder(order) });
    } catch (e) {
      next(e);
    }
  });

  router.put("/orders/edit/:token", async (req, res, next) => {
    try {
      const order = await orders.updateOrder(req.params.token, req.body);
      res.json({ order: sanitizeCustomerOrder(order) });
    } catch (e) {
      next(e);
    }
  });

  router.post(
    "/orders/edit/:token/photos/:month",
    upload.single("photo"),
    async (req, res, next) => {
      try {
        if (!req.file) throw new Error("Nincs feltöltött fájl.");
        const month = Number(req.params.month);
        const order = await orders.uploadPhoto(req.params.token, month, {
          buffer: req.file.buffer,
          originalFilename: req.file.originalname,
          mimeType: req.file.mimetype,
          sizeBytes: req.file.size,
        });
        res.status(201).json({ order: sanitizeCustomerOrder(order) });
      } catch (e) {
        next(e);
      }
    }
  );

  router.delete("/orders/edit/:token/photos/:photoId", async (req, res, next) => {
    try {
      const order = await orders.deletePhoto(req.params.token, req.params.photoId);
      res.json({ order: sanitizeCustomerOrder(order) });
    } catch (e) {
      next(e);
    }
  });

  router.put("/orders/edit/:token/photos/:month/reorder", async (req, res, next) => {
    try {
      const month = Number(req.params.month);
      const photoIds = req.body.photoIds as string[];
      const order = await orders.reorderPhotos(req.params.token, month, photoIds);
      res.json({ order: sanitizeCustomerOrder(order) });
    } catch (e) {
      next(e);
    }
  });

  router.get("/orders/edit/:token/photos/file/:photoId", async (req, res, next) => {
    try {
      const order = await orders.getOrderForEdit(req.params.token);
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

function sanitizeCustomerOrder(order: Awaited<ReturnType<IOrderService["getOrderForEdit"]>>) {
  const { editTokenHash, ...rest } = order;
  return rest;
}
