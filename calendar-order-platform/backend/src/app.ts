import express from "express";
import cors from "cors";
import helmet from "helmet";
import cookieParser from "cookie-parser";
import rateLimit from "express-rate-limit";
import { config } from "./config.js";
import { createOrderService } from "./container.js";
import { customerRoutes } from "./routes/customerRoutes.js";
import { adminRoutes } from "./routes/adminRoutes.js";
import { errorHandler } from "./middleware/errorHandler.js";

export function createApp() {
  const app = express();
  const orders = createOrderService();

  app.use(helmet({ crossOriginResourcePolicy: { policy: "cross-origin" } }));
  app.use(
    cors({
      origin: config.corsOrigin,
      credentials: true,
    })
  );
  app.use(express.json({ limit: "2mb" }));
  app.use(cookieParser());
  app.use(
    rateLimit({
      windowMs: 60_000,
      max: 120,
      standardHeaders: true,
      legacyHeaders: false,
    })
  );

  app.get("/api/health", (_req, res) => {
    res.json({ ok: true, service: "calendar-order-platform", mode: "local" });
  });

  app.use("/api", customerRoutes(orders));
  app.use("/api/admin", adminRoutes(orders));
  app.use(errorHandler);
  return app;
}
