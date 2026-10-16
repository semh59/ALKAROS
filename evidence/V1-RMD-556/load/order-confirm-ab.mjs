// Alternating on/off measurement of the order confirmation (submit-draft) latency against the real Host binary.
// Usage: node order-confirm-ab.mjs <rounds> <ordersPerRound> <warmup> <outFile>
// Each round: fresh database (migrations + E2E seed), fresh Host process with Monitoring__Enabled on or off.
import { spawn } from 'node:child_process';
import { createRequire } from 'node:module';
import { writeFileSync } from 'node:fs';
import { pathToFileURL } from 'node:url';
import { join, resolve } from 'node:path';
import { randomUUID } from 'node:crypto';

const ROOT = resolve(import.meta.dirname, '../../..');
const E2E = join(ROOT, 'tests', 'E2E', 'WaiterPwa');
const require = createRequire(join(E2E, 'package.json'));
const pg = require('pg');
const { applyAllMigrations } = await import(pathToFileURL(join(E2E, 'lib', 'migrate.js')).href);
const { seedDatabase } = await import(pathToFileURL(join(E2E, 'lib', 'seed.js')).href);

const [rounds = 6, orders = 300, warmup = 30, outFile = 'ab-result.json'] = process.argv.slice(2);
const PG = {
  host: process.env.ALKAROS_TEST_PG_HOST || 'localhost',
  port: Number(process.env.ALKAROS_TEST_PG_PORT || 55432),
  user: process.env.ALKAROS_TEST_PG_USER || 'postgres',
  password: process.env.ALKAROS_TEST_PG_PASSWORD || 'postgres',
};
const DOTNET = process.env.DOTNET_EXE || 'C:\\Users\\semih\\.dotnet\\dotnet.exe';
const HOST_DLL = join(ROOT, 'src', 'Host', 'bin', 'Debug', 'net8.0', 'ALKAROS.Host.dll');
const PORT = 5199;
const BASE = `http://127.0.0.1:${PORT}`;

const percentile = (values, p) => {
  const sorted = [...values].sort((a, b) => a - b);
  return sorted[Math.min(sorted.length - 1, Math.ceil(p * sorted.length) - 1)];
};

async function prepareDatabase(name) {
  const admin = new pg.Client({ ...PG, database: 'postgres' });
  await admin.connect();
  await admin.query(`DROP DATABASE IF EXISTS ${name} WITH (FORCE);`);
  await admin.query(`CREATE DATABASE ${name};`);
  await admin.end();
  const client = new pg.Client({ ...PG, database: name });
  await client.connect();
  await applyAllMigrations(client, join(ROOT, 'database', 'migrations'));
  const seed = await seedDatabase(client);
  await client.end();
  return seed;
}

async function startHost(name, enabled) {
  const host = spawn(DOTNET, [
    HOST_DLL, 'serve',
    '--db-url', `postgresql://${PG.user}@${PG.host}:${PG.port}/${name}`,
    '--web-root', join(ROOT, 'src', 'Clients', 'WaiterPwa', 'wwwroot'),
    '--urls', BASE,
    '--allow-insecure-loopback-development',
  ], {
    env: {
      ...process.env,
      ALKAROS_DB_PASSWORD: PG.password,
      ALKAROS_KITCHEN_STATION_ID: 'load-station',
      ASPNETCORE_ENVIRONMENT: 'Development',
      ALKAROS_LOGIN_RATE_LIMIT_PERMITS: '1000',
      Monitoring__Enabled: String(enabled),
    },
    stdio: ['ignore', 'pipe', 'pipe'],
  });
  let output = '';
  host.stdout.on('data', (c) => { output += c; });
  host.stderr.on('data', (c) => { output += c; });
  const deadline = Date.now() + 60_000;
  for (;;) {
    try {
      if ((await fetch(`${BASE}/health/ready`)).ok) break;
    } catch {
      // The host is still starting.
    }
    if (Date.now() > deadline) throw new Error(`Host not ready:\n${output}`);
    await new Promise((r) => setTimeout(r, 300));
  }
  return host;
}

async function call(cookie, method, path, body) {
  const started = performance.now();
  const response = await fetch(`${BASE}${path}`, {
    method,
    headers: { 'Content-Type': 'application/json', Cookie: cookie },
    body: body ? JSON.stringify(body) : undefined,
  });
  const elapsed = performance.now() - started;
  const text = await response.text();
  return { status: response.status, elapsed, json: text ? JSON.parse(text) : null };
}

async function runRound(index, enabled) {
  const name = `alkaros_load556_${index}`;
  const seed = await prepareDatabase(name);
  const host = await startHost(name, enabled);
  try {
    // The server limits writes per terminal (120 per minute), so the load is spread over a pool of logged-in terminals.
    const sessions = [];
    for (let k = 0; k < 20; k++) {
      const terminalId = randomUUID();
      const login = await fetch(`${BASE}/api/v1/auth/login`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ terminalId, username: seed.waiterUsername, password: seed.waiterPassword }),
      });
      if (!login.ok) throw new Error(`login ${login.status} ${await login.text()}`);
      sessions.push({ terminalId, cookie: login.headers.getSetCookie().map((c) => c.split(';')[0]).join('; ') });
    }
    const catalog = await call(sessions[0].cookie, 'GET', `/api/v1/terminals/${sessions[0].terminalId}/catalog`);
    const list = catalog.json.products ?? catalog.json.items ?? catalog.json;
    const product = (Array.isArray(list) ? list : []).find((p) => (p.id ?? p.productId) === seed.plainProductId);
    const unitPrice = product?.price ?? product?.unitPrice ?? 10;
    const tables = [...seed.loadTableIds, ...seed.sendTableIds];
    const confirm = [];
    const draft = [];
    let failures = 0;
    for (let i = 0; i < Number(warmup) + Number(orders); i++) {
      const tableId = tables[i % tables.length];
      const { terminalId, cookie } = sessions[i % sessions.length];
      const roundId = randomUUID();
      const created = await call(cookie, 'POST', `/api/v1/terminals/${terminalId}/orders/table-draft`, {
        id: roundId, tableId, tableNumber: 'L', partySize: 2,
        items: [{ id: randomUUID(), productId: seed.plainProductId, productName: 'urun', quantity: 1, unitPrice, modifiers: [], specialInstructions: null, seatId: null, courseNumber: null }],
      });
      if (created.status !== 200) {
        failures++;
        if (failures < 3) console.error('draft', created.status, JSON.stringify(created.json));
        continue;
      }
      const submitted = await call(cookie, 'POST', `/api/v1/terminals/${terminalId}/orders/${created.json.orderId}/submit-draft`, {
        orderId: created.json.orderId, expectedRowVersion: created.json.rowVersion, operationId: `${created.json.orderId}:${roundId}`,
      });
      if (submitted.status !== 200) {
        failures++;
        if (failures < 3) console.error('submit', submitted.status, JSON.stringify(submitted.json));
        continue;
      }
      if (i >= Number(warmup)) {
        confirm.push(submitted.elapsed);
        draft.push(created.elapsed);
      }
    }
    return {
      round: index, monitoring: enabled, orders: confirm.length, failures,
      confirmP50: percentile(confirm, 0.5), confirmP95: percentile(confirm, 0.95), confirmP99: percentile(confirm, 0.99), draftP95: percentile(draft, 0.95),
    };
  } finally {
    host.kill();
    await new Promise((r) => setTimeout(r, 1500));
  }
}

const results = [];
for (let r = 0; r < Number(rounds); r++) {
  const enabled = r % 2 === 0;
  const result = await runRound(r, enabled);
  console.log(JSON.stringify(result));
  results.push(result);
}
const median = (v) => percentile(v, 0.5);
const on = results.filter((r) => r.monitoring).map((r) => r.confirmP95);
const off = results.filter((r) => !r.monitoring).map((r) => r.confirmP95);
const summary = { rounds: results, medianP95On: median(on), medianP95Off: median(off), ratio: median(on) / median(off) };
console.log(JSON.stringify({ medianP95On: summary.medianP95On, medianP95Off: summary.medianP95Off, ratio: summary.ratio }));
writeFileSync(outFile, JSON.stringify(summary, null, 2));
