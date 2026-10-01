// Critical-path load run against a REAL Host and Postgres (same boot as tests/E2E/Cashier).
// Usage: node run.mjs <output.json>   (needs the Cashier E2E node_modules, the built Host and PosTerminal dist)
// Scenarios: 20 concurrent terminals in a busy-hour profile (menu + order submit + bill + payment), a last-unit
// stock race, and a webhook burst with duplicates. Ends with financial-integrity SQL checks.
import { spawn } from "node:child_process";
import { randomBytes, randomUUID } from "node:crypto";
import { writeFileSync } from "node:fs";
import { createRequire } from "node:module";
import { pathToFileURL } from "node:url";
import os from "node:os";

const ROOT = "D:/PROJECT/ALKAROS";
const E2E = `${ROOT}/tests/E2E/Cashier`;
const require = createRequire(`${E2E}/package.json`);
const pg = require("pg");
const { applyAllMigrations } = await import(pathToFileURL(`${E2E}/lib/migrate.js`).href);
const { seedDatabase } = await import(pathToFileURL(`${E2E}/lib/seed.js`).href);

const TERMINALS = Number(process.env.PERF_TERMINALS ?? 20);
const ROUNDS = Number(process.env.PERF_ROUNDS ?? 40);
// The Host allows 120 writes per terminal per minute; a till takes a new order every few seconds, never back to back.
const THINK_MS = Number(process.env.PERF_THINK_MS ?? 1500);
const PORT = 5207, BASE = `http://127.0.0.1:${PORT}`, DB = "alkaros_perf";
const cfg = { host: "localhost", port: 55432, user: "postgres", password: "postgres" };
const OUT = process.argv[2];
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

const samples = {};
const failures = [];
const record = (name, ms, ok) => { (samples[name] ??= []).push({ ms, ok }); };
const timed = async (name, fn) => {
  const start = performance.now();
  try {
    const res = await fn();
    record(name, performance.now() - start, res.ok);
    if (!res.ok) failures.push({ name, status: res.status, body: (await res.clone().text()).slice(0, 200) });
    return res;
  } catch (e) {
    record(name, performance.now() - start, false);
    failures.push({ name, error: String(e) });
    throw e;
  }
};
const pct = (xs, p) => xs.length ? xs[Math.min(xs.length - 1, Math.ceil(p / 100 * xs.length) - 1)] : null;
const summarize = () => Object.fromEntries(Object.entries(samples).map(([name, list]) => {
  const ms = list.filter((s) => s.ok).map((s) => s.ms).sort((a, b) => a - b);
  return [name, { count: list.length, failed: list.filter((s) => !s.ok).length, p50: pct(ms, 50), p95: pct(ms, 95), p99: pct(ms, 99), max: ms.at(-1) ?? null }];
}));

const admin = new pg.Client({ ...cfg, database: "postgres" }); await admin.connect();
await admin.query(`DROP DATABASE IF EXISTS ${DB} WITH (FORCE)`); await admin.query(`CREATE DATABASE ${DB}`); await admin.end();
const db = new pg.Client({ ...cfg, database: DB }); await db.connect();
await applyAllMigrations(db, `${ROOT}/database/migrations`);
const seed = await seedDatabase(db);

const zoneId = randomUUID();
await db.query("INSERT INTO table_mgmt.zones (zone_id, code, name) VALUES ($1, 'PERF', 'Yük Salonu')", [zoneId]);
const tables = [];
for (let i = 1; i <= TERMINALS * 2 + 5; i++) {
  const tableId = randomUUID();
  await db.query("INSERT INTO table_mgmt.tables (table_id, zone_id, table_number, capacity, current_status) VALUES ($1, $2, $3, 4, 'Available')", [tableId, zoneId, `PERF-${i}`]);
  tables.push({ tableId, tableNumber: `PERF-${i}` });
}
await db.query("INSERT INTO catalog.tax_profiles (tax_profile_id, code, name, vat_rate, active) VALUES ('11111111-1111-1111-1111-111111111111','KDV10','KDV %10',10,true)");
await db.query("UPDATE catalog.products SET tax_profile_id = '11111111-1111-1111-1111-111111111111' WHERE product_id = $1", [seed.paymentProductId]);
await db.query("INSERT INTO online_ordering.provider_product_mappings (mapping_id, provider, external_sku, product_id, effective_from, created_by) VALUES (gen_random_uuid(), 'yemeksepeti', 'ys-perf', $1, '2026-01-01', gen_random_uuid())", [seed.paymentProductId]);

const host = spawn("C:/Users/semih/.dotnet/dotnet.exe", [`${ROOT}/src/Host/bin/Debug/net8.0/ALKAROS.Host.dll`, "serve",
  "--db-url", `postgresql://postgres@localhost:55432/${DB}`, "--web-root", `${ROOT}/src/Clients/PosTerminal/dist`,
  "--urls", BASE, "--allow-insecure-loopback-development"], {
  env: { ...process.env, ALKAROS_DB_PASSWORD: "postgres", ALKAROS_KITCHEN_STATION_ID: "perf-station", ASPNETCORE_ENVIRONMENT: "Development",
    ALKAROS_LOGIN_RATE_LIMIT_PERMITS: "1000", ALKAROS_SECRET_ENVELOPE_MASTER_KEY: randomBytes(32).toString("base64"),
    ALKAROS_SECRET_YEMEKSEPETI_WEBHOOK_SECRET: "Bearer static-portal-token" },
  stdio: ["ignore", "pipe", "pipe"],
});
let hostOut = ""; host.stdout.on("data", (c) => hostOut += c); host.stderr.on("data", (c) => hostOut += c);

class Terminal {
  constructor(index) { this.index = index; this.terminalId = randomUUID(); this.cookie = ""; }
  async call(name, path, method = "GET", body) {
    return timed(name, () => fetch(BASE + path, { method, headers: { "Content-Type": "application/json", Cookie: this.cookie }, body: body ? JSON.stringify(body) : undefined }));
  }
  async login() {
    const res = await fetch(BASE + "/api/v1/auth/login", { method: "POST", headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ Username: seed.cashierUsername, Password: seed.cashierPassword, TerminalId: this.terminalId }) });
    if (!res.ok) throw new Error("login " + res.status);
    this.cookie = res.headers.getSetCookie().map((c) => c.split(";")[0]).join("; ");
    this.terminalId = (await res.json()).terminalId;
  }
  get base() { return `/api/v1/terminals/${this.terminalId}`; }
  async submitOrder(table, productId, quantity) {
    const draftRes = await this.call("order.table-draft", `${this.base}/orders/table-draft`, "POST", {
      id: randomUUID(), tableId: table.tableId, tableNumber: table.tableNumber,
      items: [{ id: randomUUID(), productId, name: "Yük ürünü", productName: "Yük ürünü", quantity, unitPrice: 100, specialInstructions: null }] });
    if (!draftRes.ok) return null;
    const draft = await draftRes.json();
    const t0 = performance.now();
    const submit = await this.call("order.submit-draft", `${this.base}/orders/${draft.orderId}/submit-draft`, "POST",
      { orderId: draft.orderId, expectedRowVersion: draft.rowVersion, operationId: `${draft.orderId}:submit` });
    record("order.submit-total", performance.now() - t0, submit.ok);
    return submit.ok ? draft.orderId : null;
  }
}

const stop = async () => { host.kill(); const a = new pg.Client({ ...cfg, database: "postgres" }); await a.connect(); await a.query(`DROP DATABASE IF EXISTS ${DB} WITH (FORCE)`); await a.end(); };
const result = { startedAt: new Date().toISOString(), environment: { cpus: os.cpus().length, cpuModel: os.cpus()[0].model, totalMemGB: Math.round(os.totalmem() / 2 ** 30), platform: os.platform(), node: process.version }, terminals: TERMINALS, rounds: ROUNDS, thinkMs: THINK_MS };

try {
  for (let i = 0; i < 100; i++) { try { if ((await fetch(BASE + "/health/ready")).ok) break; } catch (e) { hostOut += `waiting: ${e.message}; `; } await sleep(300); }
  const terms = Array.from({ length: TERMINALS }, (_, i) => new Terminal(i));
  await Promise.all(terms.map((t) => t.login()));

  // 1) Busy hour: every terminal runs menu -> order -> bill -> payment, all terminals at once.
  const hotStart = performance.now();
  await Promise.all(terms.map(async (t) => {
    const table = tables[t.index];
    for (let r = 0; r < ROUNDS; r++) {
      await t.call("menu.catalog", `${t.base}/catalog`);
      const orderId = await t.submitOrder(table, seed.paymentProductId, 1);
      if (!orderId) continue;
      const billRes = await t.call("bill.from-order", `${t.base}/billing/bills/from-order/${orderId}`, "POST");
      if (!billRes.ok) continue;
      const bill = await billRes.json();
      const billId = bill.billId ?? bill.bill?.id ?? bill.id;
      const t0 = performance.now();
      const tender = await t.call("payment.tender", `${t.base}/billing/bills/${billId}/tenders/`, "POST",
        { Method: "Eft", Amount: 100, IdempotencyKey: randomUUID(), Note: null });
      record("payment.close-total", performance.now() - t0, tender.ok);
      await t.call("menu.active-order", `${t.base}/orders/active`);
      await sleep(THINK_MS);
    }
  }));
  result.busyHourSeconds = (performance.now() - hotStart) / 1000;
  result.busyHourSamples = summarize();
  for (const k of Object.keys(samples)) delete samples[k];

  // 2) Last unit: stock of 3, all terminals try to order 1 at the same moment on their own table.
  const failuresBefore = failures.length;
  const freeTables = tables.slice(TERMINALS);
  const outcomes = await Promise.all(terms.map((t, i) => t.submitOrder(freeTables[i], seed.lowStockProductId, 1)));
  const stock = (await db.query("SELECT on_hand_quantity, reserved_quantity, available_quantity FROM inventory.stock_balances b JOIN inventory.product_stock_mappings m ON m.stock_item_id = b.stock_item_id WHERE m.product_id = $1", [seed.lowStockProductId])).rows[0];
  const isStock = (f) => f.status === 409 && f.body.includes("INSUFFICIENT_STOCK");
  const rejected = failures.slice(failuresBefore).filter(isStock).length;
  failures.splice(failuresBefore, failures.length, ...failures.slice(failuresBefore).filter((f) => !isStock(f)));
  result.lastUnit = { stockOnHandAtStart: 3, rejectedForStock: rejected, concurrentOrders: TERMINALS, accepted: outcomes.filter(Boolean).length, stockAfter: stock, samples: summarize() };
  for (const k of Object.keys(samples)) delete samples[k];

  // 3) Webhook burst: 20 distinct orders, each delivered 3 times at once (platform redelivery).
  const ids = Array.from({ length: TERMINALS }, () => randomUUID());
  const deliveries = ids.flatMap((id) => [0, 1, 2].map(() => id));
  await Promise.all(deliveries.map((externalId) => timed("webhook.intake", () => fetch(BASE + "/api/v1/integrations/yemeksepeti/orders/webhook", {
    method: "POST", headers: { "Content-Type": "application/json", Authorization: "Bearer static-portal-token" },
    body: JSON.stringify({ order_id: externalId, external_order_id: "YS-" + externalId.slice(0, 4), status: "RECEIVED", transport_type: "LOGISTICS_DELIVERY",
      items: [{ _id: "i1", sku: "ys-perf", pricing: { pricing_type: "UNIT", quantity: 1, unit_price: 100 } }], sys: { updated_at: "t1" } }) }))));
  let created = 0;
  for (let i = 0; i < 60 && created < ids.length; i++) {
    await sleep(1000);
    created = (await db.query("SELECT count(DISTINCT order_id)::int AS n FROM online_ordering.provider_inbox WHERE external_order_id = ANY($1) AND order_id IS NOT NULL", [ids])).rows[0].n;
  }
  const orders = (await db.query("SELECT count(*)::int AS n FROM online_ordering.online_orders WHERE external_order_id = ANY($1)", [ids])).rows[0].n;
  result.webhook = { distinctOrders: ids.length, deliveries: deliveries.length, ordersCreated: orders, samples: summarize() };
  for (const k of Object.keys(samples)) delete samples[k];

  // 4) Financial integrity after all the load.
  const one = async (sql) => (await db.query(sql)).rows[0].n;
  result.integrity = {
    ordersWithMoreThanOneBill: await one("SELECT count(*)::int AS n FROM (SELECT order_id FROM billing.bills GROUP BY order_id HAVING count(*) > 1) x"),
    negativeStockBalances: await one("SELECT count(*)::int AS n FROM inventory.stock_balances WHERE on_hand_quantity < 0 OR available_quantity < 0 OR reserved_quantity < 0"),
    billsPaidOverPayable: await one("SELECT count(*)::int AS n FROM billing.bills WHERE paid_amount > payable_amount OR allocated_amount > payable_amount"),
    negativeBillAmounts: await one("SELECT count(*)::int AS n FROM billing.bills WHERE paid_amount < 0 OR payable_amount < 0 OR allocated_amount < 0"),
    paidBillsNotFullyPaid: await one("SELECT count(*)::int AS n FROM billing.bills WHERE status = 'Paid' AND paid_amount < payable_amount"),
    billsPaid: await one("SELECT count(*)::int AS n FROM billing.bills WHERE status = 'Paid'"),
  };
  result.failures = failures.slice(0, 20);
  result.failureCount = failures.length;
} catch (e) {
  result.error = String(e); result.hostTail = hostOut.slice(-800);
} finally {
  result.finishedAt = new Date().toISOString();
  writeFileSync(OUT, JSON.stringify(result, null, 2));
  await db.end().catch(() => {});
  await stop();
}
const critical = ["menu.catalog", "order.submit-total", "bill.from-order", "payment.close-total"];
const slow = critical.filter((n) => { const m = result.busyHourSamples?.[n]; return !m || m.failed > 0 || m.p95 >= 500 || m.p99 >= 1000; });
const dirty = Object.entries(result.integrity ?? {}).filter(([k, v]) => k !== "billsPaid" && v !== 0);
const verdict = { slowPaths: slow, integrityViolations: dirty.map(([k]) => k), lastUnitOverSold: result.lastUnit?.accepted !== 3, webhookDuplicates: result.webhook?.ordersCreated !== result.webhook?.distinctOrders, unexpectedFailures: result.failureCount };
result.pass = !result.error && slow.length === 0 && dirty.length === 0 && !verdict.lastUnitOverSold && !verdict.webhookDuplicates && result.failureCount === 0;
result.verdict = verdict;
writeFileSync(OUT, JSON.stringify(result, null, 2));
console.log(JSON.stringify({ pass: result.pass, verdict }, null, 2));
process.exitCode = result.pass ? 0 : 1;
