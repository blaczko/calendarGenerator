import { existsSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const CONFIG_PATH = join(root, "pipeline.config.json");
const EXAMPLE_PATH = join(root, "pipeline.config.example.json");
const CLASP_PATH = join(root, ".clasp.json");

export function getRoot() {
  return root;
}

export function loadPipelineConfig() {
  if (!existsSync(CONFIG_PATH)) {
    return {
      ok: false,
      error:
        `Missing pipeline.config.json\n` +
        `Copy pipeline.config.example.json → pipeline.config.json and fill scriptId / deploymentId.`,
      config: null,
    };
  }

  let raw;
  try {
    raw = JSON.parse(readFileSync(CONFIG_PATH, "utf8"));
  } catch (err) {
    return {
      ok: false,
      error: `Invalid pipeline.config.json: ${err.message}`,
      config: null,
    };
  }

  const config = {
    scriptId: String(raw.scriptId || process.env.CLASP_SCRIPT_ID || "").trim(),
    deploymentId: String(
      raw.deploymentId || process.env.CLASP_DEPLOYMENT_ID || ""
    ).trim(),
    rootDir: String(raw.rootDir || ".").trim() || ".",
    liveDeploy: raw.liveDeploy !== false,
    debounceMs: Number(
      process.env.GAS_WATCH_DEBOUNCE_MS || raw.debounceMs || 1500
    ),
    deployDescriptionPrefix: String(
      raw.deployDescriptionPrefix || "local-sync"
    ).trim(),
    watch: {
      globs:
        raw.watch?.globs?.length > 0
          ? raw.watch.globs
          : ["**/*.{gs,html}", "appsscript.json"],
      ignore: raw.watch?.ignore || [
        "**/node_modules/**",
        "**/scripts/**",
        "**/social-share.html",
        "**/.git/**",
      ],
    },
  };

  const missing = [];
  if (!config.scriptId) missing.push("scriptId");
  if (config.liveDeploy && !config.deploymentId) missing.push("deploymentId");

  return {
    ok: missing.length === 0,
    missing,
    error:
      missing.length > 0
        ? `pipeline.config.json is incomplete. Fill: ${missing.join(", ")}`
        : null,
    config,
  };
}

/** clasp reads .clasp.json — keep it in sync with pipeline.config.json */
export function syncClaspJson(config = loadPipelineConfig().config) {
  if (!config?.scriptId) {
    throw new Error("Cannot sync .clasp.json without scriptId.");
  }
  const payload = {
    scriptId: config.scriptId,
    rootDir: config.rootDir || ".",
  };
  writeFileSync(CLASP_PATH, JSON.stringify(payload, null, 2) + "\n", "utf8");
  return CLASP_PATH;
}

export function printSetupHelp() {
  console.log(`
Naptar Apps Script pipeline — setup checklist
---------------------------------------------
1) cd google-apps-script
2) npm install
3) npm run login
   (sign in as the Apps Script owner, e.g. zataipixel@gmail.com)

4) Copy config:
   Copy-Item pipeline.config.example.json pipeline.config.json

5) Fill pipeline.config.json:
   - scriptId      → Apps Script → Project Settings → Script ID
   - deploymentId  → npm run list-deployments  (Web app deployment)

6) Validate:
   npm run doctor

7) Run:
   npm run watch:live
`);
}

export function doctor() {
  printSetupHelp();
  console.log("Current status:");
  console.log(`- example config: ${existsSync(EXAMPLE_PATH) ? "ok" : "MISSING"}`);
  console.log(`- pipeline.config.json: ${existsSync(CONFIG_PATH) ? "found" : "MISSING"}`);

  const loaded = loadPipelineConfig();
  if (!loaded.config && loaded.error) {
    console.log(`- config load: FAIL (${loaded.error.split("\n")[0]})`);
    return 1;
  }

  const c = loaded.config;
  console.log(`- scriptId: ${c.scriptId ? "set" : "EMPTY"}`);
  console.log(
    `- deploymentId: ${c.deploymentId ? "set" : c.liveDeploy ? "EMPTY (required for live)" : "optional"}`
  );
  console.log(`- liveDeploy: ${c.liveDeploy}`);
  console.log(`- debounceMs: ${c.debounceMs}`);

  // clasp credentials live in the user profile after `clasp login`
  const home = process.env.USERPROFILE || process.env.HOME || "";
  const clasprc = join(home, ".clasprc.json");
  console.log(`- clasp login (~/.clasprc.json): ${existsSync(clasprc) ? "ok" : "MISSING — run npm run login"}`);

  if (!loaded.ok) {
    console.log(`\nNot runnable yet: ${loaded.error}`);
    return 1;
  }

  try {
    syncClaspJson(c);
    console.log(`- .clasp.json: synced from pipeline.config.json`);
  } catch (err) {
    console.log(`- .clasp.json: FAIL (${err.message})`);
    return 1;
  }

  console.log("\nRunnable. Start with: npm run watch:live");
  return 0;
}

if (process.argv[1] && process.argv[1].endsWith("config.mjs")) {
  const mode = process.argv[2] || "doctor";
  if (mode === "sync") {
    const loaded = loadPipelineConfig();
    if (!loaded.ok) {
      console.error(loaded.error);
      printSetupHelp();
      process.exit(1);
    }
    const path = syncClaspJson(loaded.config);
    console.log(`Synced ${path}`);
  } else {
    process.exit(doctor());
  }
}
