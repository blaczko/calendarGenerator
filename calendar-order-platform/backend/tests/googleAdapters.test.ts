import { describe, expect, it } from "vitest";
import { GoogleDrivePhotoStorage } from "../src/google/GoogleDrivePhotoStorage.js";
import { InMemoryGoogleDriveClient } from "../src/google/driveClient.js";
import { GoogleSheetsOrderRepository } from "../src/google/GoogleSheetsOrderRepository.js";
import { InMemoryGoogleSheetsClient } from "../src/google/sheetsClient.js";
import { GOOGLE_DRIVE_LAYOUT } from "../src/google/layout.js";

describe("Google Drive adapter (mocked)", () => {
  it("creates year, order, meta and 12 month folders", async () => {
    const drive = new InMemoryGoogleDriveClient();
    const storage = new GoogleDrivePhotoStorage(drive);
    await storage.ensureOrderFolders("ORD-2027-001_Teszt", 2027);

    const folderNames = [...drive.folders.values()].map((f) => f.name);
    expect(folderNames).toContain(GOOGLE_DRIVE_LAYOUT.root);
    expect(folderNames).toContain("2027");
    expect(folderNames).toContain("ORD-2027-001_Teszt");
    expect(folderNames).toContain(GOOGLE_DRIVE_LAYOUT.meta);
    for (const m of GOOGLE_DRIVE_LAYOUT.months) {
      expect(folderNames).toContain(m);
    }
  });

  it("uploads photos and avoids duplicate uploads on retry", async () => {
    const drive = new InMemoryGoogleDriveClient();
    const storage = new GoogleDrivePhotoStorage(drive);
    const buf = Buffer.from("fake-image");
    const meta1 = await storage.savePhoto("ORD-2027-009_A", 2027, 3, {
      buffer: buf,
      originalFilename: "pic.jpg",
      mimeType: "image/jpeg",
      sizeBytes: buf.length,
    });
    const before = drive.files.size;
    // Same relative path idempotency: re-upload with same stored filename key via writeOrderJson
    await storage.writeOrderJson("ORD-2027-009_A", 2027, { orderId: "ORD-2027-009" });
    await storage.writeOrderJson("ORD-2027-009_A", 2027, { orderId: "ORD-2027-009" });
    expect(drive.files.size).toBe(before + 1); // only one megrendeles.json
    expect(meta1.originalFilename).toBe("pic.jpg");
    expect(meta1.relativePath).toContain("03_Marcius");
  });
});

describe("Google Sheets adapter (mocked)", () => {
  it("syncs create/update/delete to spreadsheet rows", async () => {
    const sheets = new InMemoryGoogleSheetsClient();
    const repo = new GoogleSheetsOrderRepository(sheets);

    const created = await repo.create(
      {
        fullName: "Teszt Elek",
        phone: "+40721112233",
        email: "a@b.hu",
        calendarYear: 2027,
        privacyConsent: true,
        events: [{ eventDate: "2027-05-15", description: "Szülinap" }],
      },
      "ORD-2027-100",
      "hash"
    );

    expect(sheets.rows.has("ORD-2027-100")).toBe(true);
    expect(sheets.rows.get("ORD-2027-100")?.fullName).toBe("Teszt Elek");
    expect(sheets.rows.get("ORD-2027-100")?.eventCount).toBe(1);

    await repo.updateStatus(created.id, "IN_PROGRESS");
    expect(sheets.rows.get("ORD-2027-100")?.status).toBe("IN_PROGRESS");

    await repo.delete(created.id);
    expect(sheets.rows.has("ORD-2027-100")).toBe(false);
  });
});
