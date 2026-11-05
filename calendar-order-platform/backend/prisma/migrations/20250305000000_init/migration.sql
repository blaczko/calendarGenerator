-- CreateTable
CREATE TABLE "Order" (
    "id" TEXT NOT NULL PRIMARY KEY,
    "publicOrderId" TEXT NOT NULL,
    "editTokenHash" TEXT NOT NULL,
    "editTokenRevoked" BOOLEAN NOT NULL DEFAULT false,
    "fullName" TEXT NOT NULL,
    "phone" TEXT NOT NULL,
    "email" TEXT,
    "calendarYear" INTEGER NOT NULL,
    "calendarLanguage" TEXT NOT NULL DEFAULT 'hu',
    "calendarFormatPref" TEXT,
    "comments" TEXT,
    "privacyConsent" BOOLEAN NOT NULL,
    "status" TEXT NOT NULL DEFAULT 'NEW',
    "createdAt" DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updatedAt" DATETIME NOT NULL
);

CREATE UNIQUE INDEX "Order_publicOrderId_key" ON "Order"("publicOrderId");

CREATE TABLE "Photo" (
    "id" TEXT NOT NULL PRIMARY KEY,
    "orderId" TEXT NOT NULL,
    "month" INTEGER NOT NULL,
    "sortOrder" INTEGER NOT NULL,
    "storedFilename" TEXT NOT NULL,
    "originalFilename" TEXT NOT NULL,
    "mimeType" TEXT NOT NULL,
    "sizeBytes" INTEGER NOT NULL,
    "relativePath" TEXT NOT NULL,
    "createdAt" DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT "Photo_orderId_fkey" FOREIGN KEY ("orderId") REFERENCES "Order" ("id") ON DELETE CASCADE ON UPDATE CASCADE
);

CREATE INDEX "Photo_orderId_month_sortOrder_idx" ON "Photo"("orderId", "month", "sortOrder");

CREATE TABLE "CalendarEvent" (
    "id" TEXT NOT NULL PRIMARY KEY,
    "orderId" TEXT NOT NULL,
    "eventDate" DATETIME NOT NULL,
    "description" TEXT NOT NULL,
    "createdAt" DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT "CalendarEvent_orderId_fkey" FOREIGN KEY ("orderId") REFERENCES "Order" ("id") ON DELETE CASCADE ON UPDATE CASCADE
);

CREATE INDEX "CalendarEvent_orderId_eventDate_idx" ON "CalendarEvent"("orderId", "eventDate");

CREATE TABLE "AdminUser" (
    "id" TEXT NOT NULL PRIMARY KEY,
    "username" TEXT NOT NULL,
    "passwordHash" TEXT NOT NULL,
    "createdAt" DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE UNIQUE INDEX "AdminUser_username_key" ON "AdminUser"("username");
