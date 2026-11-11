import { spawn } from "node:child_process";
import chokidar from "chokidar";
import {
  getRoot,
  loadPipelineConfig,
  printSetupHelp,
  syncClaspJson,
} from "./config.mjs";

const root = getRoot();
process.chdir(root);

const loaded = loadPipelineConfig();
if (!loaded.config) {
  console.error(loaded.error || "Invalid pipeline config.");
  printSetupHelp();
  process.exit(1);
}

const config = loaded.config;
if (!config.scriptId) {
  console.error("scriptId is required in pipeline.config.json.");
  printSetupHelp();
  process.exit(1);
}

syncClaspJson(config);

const live = Boolean(config.liveDeploy && config.deploymentId);
if (config.liveDeploy && !config.deploymentId) {
  console.warn(
    "liveDeploy=true but deploymentId is empty — watch will PUSH only.\n" +
      "Fill deploymentId in pipeline.config.json to update /exec."
  );
}

let timer = null;
let running = false;
let queued = false;

function run(cmd, args) {
  return new Promise((resolve, reject) => {
    const child = spawn(cmd, args, {
      cwd: root,
      shell: true,
      stdio: "inherit",
    });
    child.on("exit", (code) => {
      if (code === 0) resolve();
      else reject(new Error(`${cmd} ${args.join(" ")} exited ${code}`));
    });
  });
}

async function syncOnce(reason) {
  if (running) {
    queued = true;
    return;
  }
  running = true;
  const started = new Date().toISOString();
  console.log(`\n[${started}] sync (${reason})`);
  try {
    await run("npx", ["clasp", "push"]);
    if (live) {
      const description = `${config.deployDescriptionPrefix} ${started}`;
      // clasp v3: --description=... keeps spaces intact under Windows shell:true
      await run("npx", [
        "clasp",
        "redeploy",
        config.deploymentId,
        `--description=${JSON.stringify(description)}`,
      ]);
      console.log("Pushed + live /exec redeployed.");
    } else {
      console.log("Pushed. (live redeploy skipped)");
    }
  } catch (err) {
    console.error(String(err && err.message ? err.message : err));
  } finally {
    running = false;
    if (queued) {
      queued = false;
      schedule("queued changes");
    }
  }
}

function schedule(reason) {
  clearTimeout(timer);
  timer = setTimeout(() => syncOnce(reason), config.debounceMs);
}

const watcher = chokidar.watch(config.watch.globs, {
  cwd: root,
  ignored: config.watch.ignore,
  ignoreInitial: true,
  awaitWriteFinish: { stabilityThreshold: 400, pollInterval: 100 },
});

watcher
  .on("ready", () => {
    console.log("Watching google-apps-script for changes…");
    console.log(live ? "Mode: push + live deploy" : "Mode: push only");
    console.log(`Debounce: ${config.debounceMs}ms`);
    syncOnce("startup");
  })
  .on("add", (p) => schedule(`add ${p}`))
  .on("change", (p) => schedule(`change ${p}`))
  .on("unlink", (p) => schedule(`unlink ${p}`));

process.on("SIGINT", () => {
  watcher.close().then(() => process.exit(0));
});
