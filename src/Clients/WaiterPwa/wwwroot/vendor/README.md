# Vendored third-party scripts

WaiterPwa is a plain-JS, no-bundler PWA (see `../waiter-app.js`), so
third-party browser scripts are vendored here rather than pulled from a CDN
— an external CDN dependency for a core feature would work against this
app's offline-first design (the service worker, `../sw.js`, can only cache
what it can fetch at install time, and a CDN fetch failure with no cached
fallback would be a worse failure mode than not vendoring at all).

- `signalr.min.js` — `@microsoft/signalr` 10.0.0 (MIT license), the official
  ASP.NET Core SignalR JavaScript client, copied unmodified from
  `node_modules/@microsoft/signalr/dist/browser/signalr.min.js` (built from
  the same PosTerminal `package.json`-pinned version already used for
  `CustomerDisplay.tsx`'s hub connection). Used by V1-WTR-009's order-ready
  notification (`connectOrderReadyHub` in `../waiter-app.js`).
