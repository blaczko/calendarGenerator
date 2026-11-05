import type { Server } from "node:http";
import { createApp } from "./app.js";
import { config } from "./config.js";

declare global {
  // Persist across tsx watch reloads in the same process.
  // eslint-disable-next-line no-var
  var __calendarOrderApiServer: Server | undefined;
}

async function start(): Promise<void> {
  if (globalThis.__calendarOrderApiServer) {
    await new Promise<void>((resolve) => {
      globalThis.__calendarOrderApiServer!.close(() => resolve());
    });
    globalThis.__calendarOrderApiServer = undefined;
  }

  const app = createApp();
  await new Promise<void>((resolve, reject) => {
    const server = app.listen(config.port, () => {
      console.log(
        `Calendar Order Platform API listening on http://localhost:${config.port}`
      );
      console.log(`Storage root: ${config.storageRoot}`);
      console.log(`Supported years: ${config.supportedYears.join(", ")}`);
      resolve();
    });
    server.on("error", reject);
    globalThis.__calendarOrderApiServer = server;
  });
}

start().catch((err) => {
  console.error("Failed to start API:", err);
  process.exit(1);
});
