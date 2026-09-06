import { readFileSync } from "node:fs";
import { pathToFileURL } from "node:url";

/**
 * Loads a static client app's real index.html body (so the DOM element ids
 * the app's script queries via document.getElementById always match the
 * shipped markup — no hand-maintained skeleton to drift out of sync) and
 * then evaluates its real wwwroot script in the current jsdom document.
 *
 * The script is a plain browser IIFE (no import/export), so Vitest's ESM
 * loader just executes its top-level side effects once per unique module
 * specifier — the cache-busting query param lets each test re-import a
 * fresh instance (fresh closure-scoped `state`) instead of reusing one
 * still wired to a previous test's DOM/mocks.
 *
 * @param {string} htmlPath absolute path to the app's index.html
 * @param {string} scriptPath absolute path to the app's own <script src> file
 */
export async function loadApp(htmlPath, scriptPath) {
  const html = readFileSync(htmlPath, "utf-8");
  const bodyMatch = html.match(/<body>([\s\S]*)<\/body>/);
  if (!bodyMatch) throw new Error(`No <body> found in ${htmlPath}`);
  // Drop every <script> tag from the markup - it is the app's OWN script
  // (and, for WaiterPwa, the vendored SignalR client) that we load and
  // stub out separately below.
  const bodyWithoutScripts = bodyMatch[1].replace(/<script[\s\S]*?<\/script>/g, "");
  document.body.innerHTML = bodyWithoutScripts;

  const cacheBust = `${pathToFileURL(scriptPath).href}?t=${Date.now()}-${Math.random()}`;
  await import(cacheBust);
}
