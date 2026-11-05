import { PrismaClient } from "@prisma/client";
import bcrypt from "bcryptjs";
import { createHash, randomBytes } from "node:crypto";

const prisma = new PrismaClient();

async function main() {
  const username = process.env.ADMIN_USERNAME ?? "admin";
  const password = process.env.ADMIN_PASSWORD ?? "changeme-admin";
  const passwordHash = await bcrypt.hash(password, 10);

  await prisma.adminUser.upsert({
    where: { username },
    update: { passwordHash },
    create: { username, passwordHash },
  });

  const existing = await prisma.order.findUnique({
    where: { publicOrderId: "ORD-2027-001" },
  });
  if (!existing) {
    const token = randomBytes(32).toString("hex");
    await prisma.order.create({
      data: {
        publicOrderId: "ORD-2027-001",
        editTokenHash: createHash("sha256").update(token).digest("hex"),
        fullName: "Kovács Botond",
        phone: "+40 712 345 678",
        email: "boti@example.com",
        calendarYear: 2027,
        calendarLanguage: "hu",
        comments: "Minta megrendelés a helyi fejlesztéshez.",
        privacyConsent: true,
        status: "NEW",
        events: {
          create: [
            {
              eventDate: new Date("2027-05-15"),
              description: "Boti születésnapja",
            },
            {
              eventDate: new Date("2027-06-20"),
              description: "Házassági évforduló",
            },
          ],
        },
      },
    });
    console.log("Seed sample edit token (ORD-2027-001):", token);
  }

  console.log("Seed complete. Admin user:", username);
}

main()
  .catch((e) => {
    console.error(e);
    process.exit(1);
  })
  .finally(async () => {
    await prisma.$disconnect();
  });
