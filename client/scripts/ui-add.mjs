// `npm run ui:add -- dialog tabs`: adds shadcn components, then converts them to the app's tokens
// (see shadcn-8px.mjs). Never run `shadcn add` on its own: its classes assume a 4px spacing unit.

import { spawnSync } from "node:child_process"

const components = process.argv.slice(2)
if (components.length === 0) {
  console.error("Usage: npm run ui:add -- <component> [component...]")
  process.exit(1)
}

const run = (command, args) => {
  const result = spawnSync(command, args, { stdio: "inherit", shell: process.platform === "win32" })
  if (result.status !== 0) process.exit(result.status ?? 1)
}

run("npx", ["shadcn", "add", "--yes", ...components])
run("node", ["scripts/shadcn-8px.mjs"])
