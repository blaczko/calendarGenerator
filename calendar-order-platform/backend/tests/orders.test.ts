import fs from "node:fs";
import path from "node:path";
import { afterAll, beforeAll, describe, expect, it } from "vitest";
import request from "supertest";
import { PrismaClient } from "@prisma/client";

process.env.DATABASE_URL = "file:./test.db";
process.env.STORAGE_ROOT = path.resolve("..", "storage", "test-orders");
process.env.ADMIN_USERNAME = "admin";
process.env.ADMIN_PASSWORD = "test-admin";
process.env.ADMIN_SESSION_SECRET = "test-secret";
process.env.PUBLIC_APP_URL = "http://localhost:5174";
process.env.CORS_ORIGIN = "http://localhost:5174";
process.env.SUPPORTED_YEARS = "2027,2028";

const prisma = new PrismaClient();

describe("Calendar Order Platform API", () => {
  let app: Awaited<ReturnType<typeof import("../src/app.js").createApp>>;

  beforeAll(async () => {
    fs.mkdirSync(process.env.STORAGE_ROOT!, { recursive: true });
    // Isolated throwaway SQLite file for tests (not production).
    const dbPath = path.resolve("prisma", "test.db");
    if (fs.existsSync(dbPath)) fs.unlinkSync(dbPath);
    const { execSync } = await import("node:child_process");
    execSync("npx prisma migrate deploy", {
      cwd: path.resolve("."),
      env: { ...process.env, DATABASE_URL: "file:./test.db" },
      stdio: "inherit",
    });
    const mod = await import("../src/app.js");
    app = mod.createApp();
  });

  afterAll(async () => {
    await prisma.$disconnect();
  });

  it("rejects empty name and invalid phone", async () => {
    const res = await request(app).post("/api/orders").send({
      fullName: "",
      phone: "abc",
      calendarYear: 2027,
      privacyConsent: true,
      events: [],
    });
    expect(res.status).toBe(400);
  });

  it("rejects unsupported year", async () => {
    const res = await request(app).post("/api/orders").send({
      fullName: "Teszt Elek",
      phone: "+40712345678",
      calendarYear: 1999,
      privacyConsent: true,
      events: [],
    });
    expect(res.status).toBe(400);
  });

  it("rejects non-Romanian phone numbers", async () => {
    const res = await request(app).post("/api/orders").send({
      fullName: "Teszt Elek",
      phone: "+36 30 123 4567",
      calendarYear: 2027,
      privacyConsent: true,
      events: [],
    });
    expect(res.status).toBe(400);
    expect(JSON.stringify(res.body)).toMatch(/román|Romanian|telefonszám/i);
  });

  it("creates order, returns edit token, allows edit and events", async () => {
    const create = await request(app).post("/api/orders").send({
      fullName: "Teszt Elek",
      phone: "+40 712 345 678",
      email: "teszt@example.com",
      calendarYear: 2027,
      privacyConsent: true,
      events: [
        { eventDate: "2027-05-15", description: "Boti születésnapja" },
        { eventDate: "2027-05-15", description: "Családi ebéd" },
      ],
    });
    expect(create.status).toBe(201);
    expect(create.body.orderId).toMatch(/^ORD-2027-/);
    expect(create.body.editToken).toHaveLength(64);

    const token = create.body.editToken as string;
    const get = await request(app).get(`/api/orders/edit/${token}`);
    expect(get.status).toBe(200);
    expect(get.body.order.events).toHaveLength(2);

    const badToken = await request(app).get("/api/orders/edit/not-a-real-token");
    expect(badToken.status).toBe(404);

    const outOfYear = await request(app).put(`/api/orders/edit/${token}`).send({
      events: [{ eventDate: "2026-01-01", description: "Rossz év" }],
    });
    expect(outOfYear.status).toBe(400);
  });

  it("enforces max 6 photos per month and rejects bad mime", async () => {
    const create = await request(app).post("/api/orders").send({
      fullName: "Foto Teszt",
      phone: "0712111223",
      calendarYear: 2027,
      privacyConsent: true,
      events: [],
    });
    const token = create.body.editToken as string;
    const tinyPng = Buffer.from(
      "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==",
      "base64"
    );

    for (let i = 0; i < 6; i++) {
      const up = await request(app)
        .post(`/api/orders/edit/${token}/photos/1`)
        .attach("photo", tinyPng, { filename: `a${i}.png`, contentType: "image/png" });
      expect(up.status).toBe(201);
    }
    const seventh = await request(app)
      .post(`/api/orders/edit/${token}/photos/1`)
      .attach("photo", tinyPng, { filename: "x.png", contentType: "image/png" });
    expect(seventh.status).toBe(400);

    const bad = await request(app)
      .post(`/api/orders/edit/${token}/photos/2`)
      .attach("photo", Buffer.from("%PDF"), {
        filename: "x.pdf",
        contentType: "application/pdf",
      });
    expect(bad.status).toBe(400);
  });

  it("admin login and list orders", async () => {
    const agent = request.agent(app);
    const fail = await agent.post("/api/admin/login").send({
      username: "admin",
      password: "wrong",
    });
    expect(fail.status).toBe(401);

    const ok = await agent.post("/api/admin/login").send({
      username: "admin",
      password: "test-admin",
    });
    expect(ok.status).toBe(200);

    const list = await agent.get("/api/admin/orders");
    expect(list.status).toBe(200);
    expect(Array.isArray(list.body.orders)).toBe(true);
  });

  it("deletes photos and orders; rejects oversized files", async () => {
    const create = await request(app).post("/api/orders").send({
      fullName: "Torles Teszt",
      phone: "+40739998877",
      calendarYear: 2027,
      privacyConsent: true,
      events: [],
    });
    expect(create.status).toBe(201);
    const token = create.body.editToken as string;
    const orderDbId = (
      await request(app).get(`/api/orders/edit/${token}`)
    ).body.order.id as string;

    const tinyPng = Buffer.from(
      "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==",
      "base64"
    );
    const up = await request(app)
      .post(`/api/orders/edit/${token}/photos/5`)
      .attach("photo", tinyPng, { filename: "ok.png", contentType: "image/png" });
    expect(up.status).toBe(201);
    const photoId = up.body.order.photos[0].id as string;

    const delPhoto = await request(app).delete(
      `/api/orders/edit/${token}/photos/${photoId}`
    );
    expect(delPhoto.status).toBe(200);
    expect(delPhoto.body.order.photos).toHaveLength(0);

    const huge = Buffer.alloc(11 * 1024 * 1024, 1);
    const oversized = await request(app)
      .post(`/api/orders/edit/${token}/photos/5`)
      .attach("photo", huge, { filename: "big.jpg", contentType: "image/jpeg" });
    expect(oversized.status).toBe(400);

    const agent = request.agent(app);
    await agent.post("/api/admin/login").send({
      username: "admin",
      password: "test-admin",
    });
    const delOrder = await agent.delete(`/api/admin/orders/${orderDbId}`);
    expect(delOrder.status).toBe(200);

    const gone = await request(app).get(`/api/orders/edit/${token}`);
    expect(gone.status).toBe(404);
  });
});
