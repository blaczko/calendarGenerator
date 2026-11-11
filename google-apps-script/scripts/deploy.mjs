import { spawnSync } from "node:child_process";
import {
  getRoot,
  loadPipelineConfig,
  printSetupHelp,
  syncClaspJson,
} from "./config.mjs";

const root = getRoot();
process.chdir(root);

function run(cmd, args) {
  const result = spawnSync(cmd, args, {
    stdio: "inherit",
    shell: true,
    cwd: root,
  });
  if (result.status !== 0) {
    process.exit(result.status ?? 1);
  }
}

const loaded = loadPipelineConfig();
if (!loaded.ok || !loaded.config) {
  console.error(loaded.error || "Invalid pipeline config.");
  printSetupHelp();
  process.exit(1);
}

const config = loaded.config;
if (!config.deploymentId) {
  console.error(
    "deploymentId is required for deploy. Set it in pipeline.config.json."
  );
  process.exit(1);
}

syncClaspJson(config);

const description =
  process.env.CLASP_DEPLOY_DESCRIPTION ||
  `${config.deployDescriptionPrefix} ${new Date().toISOString()}`;

console.log("→ clasp push");
run("npx", ["clasp", "push", "-f"]);

// clasp v3: update an existing web app with `redeploy <deploymentId>`
// Use --description=... (one argv) so Windows shell:true does not split on spaces.
console.log(`→ clasp redeploy ${config.deploymentId}`);
run("npx", [
  "clasp",
  "redeploy",
  config.deploymentId,
  `--description=${JSON.stringify(description)}`,
]);
console.log("Live web app updated.");
