// V1-RMD-206: jsdom does not implement CSS.escape (confirmed: querying it
// under jsdom returns undefined), so any production code that calls it
// (waiter-app.js's own re-find-the-fresh-button-by-id logic) throws as an
// unhandled exception mid-click in every test that exercises that path -
// tests can still pass around the throw (it happens inside a DOM event
// listener, which the DOM spec swallows rather than propagating to the
// caller), but Vitest still reports it as an unhandled error and it would
// mask a real one. A real browser always has CSS.escape; this only fills
// the gap in the test environment.
if (typeof globalThis.CSS === "undefined" || typeof globalThis.CSS.escape !== "function") {
  globalThis.CSS = globalThis.CSS || {};
  globalThis.CSS.escape = (value) =>
    String(value).replace(/[^a-zA-Z0-9_-]/g, (ch) => `\\${ch}`);
}
