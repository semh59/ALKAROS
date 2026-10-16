// End-to-end proof against the running Docker dev stack (web proxy on :8090, database in alkaros-postgres-1).
// Every injection is a real condition (a table made unreachable for a moment, a table lock, a failed print job row,
// a browser report). Each step writes one JSON line to run.log with what was injected and what the system showed.
// Usage: node prove.mjs <outDir>
import { execFileSync, spawn } from 'node:child_process';
import { createRequire } from 'node:module';
import { appendFileSync, writeFileSync } from 'node:fs';
import { pathToFileURL } from 'node:url';
import { join, resolve } from 'node:path';
import { randomUUID } from 'node:crypto';

const ROOT = resolve(import.meta.dirname, '../..');
const OUT = process.argv[2] ?? import.meta.dirname;
const BASE = 'http://localhost:8090';
const { hashPassword } = await import(pathToFileURL(join(ROOT, 'tests', 'E2E', 'WaiterPwa', 'lib', 'passwordHash.js')).href);
void createRequire;

const USERNAME = 'kanit.540';
const PASSWORD = 'Kanit540Sifre!2026';
const LOG = join(OUT, 'run.log');
writeFileSync(LOG, '');

const log = (step, data) => {
  const line = JSON.stringify({ step, ...data });
  appendFileSync(LOG, line + '\n');
  console.log(line);
};
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

function psql(sql) {
  return execFileSync('docker', ['exec', '-i', 'alkaros-postgres-1', 'psql', '-U', 'alkaros', '-d', 'alkaros', '-v', 'ON_ERROR_STOP=1', '-tA', '-F', '|'], { input: sql, encoding: 'utf8' }).trim();
}

async function http(cookie, method, path, body, headers = {}) {
  const started = performance.now();
  const response = await fetch(`${BASE}${path}`, {
    method,
    headers: { 'Content-Type': 'application/json', Cookie: cookie, ...headers },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const text = await response.text();
  let json = null;
  try { json = text ? JSON.parse(text) : null; } catch { /* not json */ }
  return { status: response.status, elapsedMs: Math.round(performance.now() - started), json, text, headers: response.headers };
}

async function errorList(cookie) {
  const result = await http(cookie, 'GET', '/api/v1/management/observability/errors');
  return result.json?.items ?? [];
}

const eventsOf = (items, category) => items.filter((i) => i.category === category).map((i) => ({ category: i.category, layer: i.layer, severity: i.severity, status: i.status, count: i.occurrenceCount, detail: i.detail }));

// ---- S0 preflight -------------------------------------------------------
const anonymous = await http('', 'POST', '/api/v1/telemetry/offline-queue', { length: 5 });
log('S0-preflight', {
  offlineQueueEndpointWithoutSession: anonymous.status,
  traceIdColumn: psql("select count(*) from information_schema.columns where table_schema='observability' and table_name='raw_ui_interactions' and column_name='trace_id'"),
  eventsBefore: psql('select count(*) from observability.error_events'),
});

// ---- S1 test account (fixture style, manager role, dev stack only) ----------
if (psql(`select count(*) from identity.users where username='${USERNAME}'`) === '0') {
  const userId = randomUUID();
  psql(`insert into identity.users (user_id, username, password_hash, display_name, active) values ('${userId}', '${USERNAME}', '${hashPassword(PASSWORD)}', 'Kanit 540', true);
        insert into identity.user_roles (user_role_id, user_id, role_id) select gen_random_uuid(), '${userId}', role_id from identity.roles where code='manager';`);
}
psql(`update identity.users set active = true where username='${USERNAME}'`);
const terminalId = randomUUID();
const login = await fetch(`${BASE}/api/v1/auth/login`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ terminalId, username: USERNAME, password: PASSWORD }) });
if (!login.ok) throw new Error(`login failed ${login.status}`);
const cookie = login.headers.getSetCookie().map((c) => c.split(';')[0]).join('; ');
log('S1-login', { status: login.status, cookies: login.headers.getSetCookie().map((c) => c.split('=')[0]) });

// ---- S2 browser error ---------------------------------------------------------
const browser = await http(cookie, 'POST', '/api/v1/telemetry/browser-errors', { eventId: randomUUID(), kind: 'js_error', route: '/cashier/orders/42' });
await sleep(3500);
log('S2-browser-error', { postStatus: browser.status, events: eventsOf(await errorList(cookie), 'browser.js_error') });

// ---- S3 exception: the catalog table is unreachable for a moment (the error reaches the shared handler) ----
async function withRenamedTable(table, temp, call) {
  psql(`alter table ${table} rename to ${temp}`);
  try {
    return await call();
  } finally {
    psql(`alter table ${table.split('.')[0]}.${temp} rename to ${table.split('.')[1]}`);
  }
}

const failed = await withRenamedTable('catalog.products', 'products_kanit540', () => http(cookie, 'GET', `/api/v1/terminals/${terminalId}/catalog`));
await sleep(3500);
const afterException = await errorList(cookie);
log('S3-exception', {
  injected: 'catalog.products renamed for one request, then restored',
  httpStatus: failed.status,
  traceId: failed.headers.get('x-correlation-id'),
  events: eventsOf(afterException, 'api.unhandled_exception'),
  restored: psql('select count(*) from catalog.products').length > 0,
  notifications: psql("select kind || '/' || category || '/' || severity from observability.notification_queue where category='api.unhandled_exception'"),
});

// ---- S3b the same kind of failure, but caught by the endpoint itself and answered as 503 --------------------
const swallowed = await withRenamedTable('table_mgmt.zones', 'zones_kanit540', () => http(cookie, 'GET', `/api/v1/terminals/${terminalId}/table-management/zones`));
await sleep(3500);
log('S3b-failure-answered-by-the-endpoint', {
  injected: 'table_mgmt.zones renamed for one request, then restored',
  httpStatus: swallowed.status,
  note: 'the endpoint catches the database error and answers 503 itself, so the shared handler never sees it',
  eventsForThisRoute: (await errorList(cookie)).filter((i) => (i.detail ?? '').includes('table-management/zones')).map((i) => `${i.category}/${i.severity}`),
});

// ---- S4 slow query: a lock held on the tables table ----------------------------------
const holder = spawn('docker', ['exec', '-i', 'alkaros-postgres-1', 'psql', '-U', 'alkaros', '-d', 'alkaros', '-c', "begin; lock table table_mgmt.tables in access exclusive mode; select pg_sleep(2.6); commit;"], { stdio: 'ignore' });
const holderExit = new Promise((r) => holder.on('exit', r));
await sleep(700);
const slow = await http(cookie, 'GET', `/api/v1/terminals/${terminalId}/table-management/tables`);
await holderExit;
await sleep(6000);
const afterSlow = await errorList(cookie);
log('S4-slow-query', {
  injected: 'access exclusive lock on table_mgmt.tables held for about 2.6 s',
  httpStatus: slow.status,
  elapsedMs: slow.elapsedMs,
  slowQuery: eventsOf(afterSlow, 'db.slow_query'),
  slowRequest: eventsOf(afterSlow, 'api.slow_request'),
  plan: eventsOf(afterSlow, 'db.slow_query_plan'),
});

// ---- S5 printer failure: a failed print job on a real kitchen ticket -----------------------
const product = psql("select product_id || '|' || coalesce(current_price, 10) from catalog.products where active and is_available order by display_order limit 1").split('|');
const roundId = randomUUID();
const draft = await http(cookie, 'POST', `/api/v1/terminals/${terminalId}/orders/table-draft`, {
  id: roundId, tableId: 'b0000000-0000-0000-0000-000000000001', tableNumber: 'S1', partySize: 2,
  items: [{ id: randomUUID(), productId: product[0], productName: 'kanit', quantity: 1, unitPrice: Number(product[1]), modifiers: [], specialInstructions: null, seatId: null, courseNumber: null }],
});
const submit = draft.status === 200
  ? await http(cookie, 'POST', `/api/v1/terminals/${terminalId}/orders/${draft.json.orderId}/submit-draft`, { orderId: draft.json.orderId, expectedRowVersion: draft.json.rowVersion, operationId: `${draft.json.orderId}:${roundId}` })
  : draft;
const orderId = draft.json?.orderId;
const ticket = orderId ? psql(`select id from kitchen.kitchen_tickets where order_id='${orderId}' limit 1`) : '';
let printer = {};
if (ticket) {
  const printerId = randomUUID();
  psql(`insert into kitchen.printers (id, name, station_id, ip_address, port, is_active) values ('${printerId}', 'kanit540-yazici', 'kanit540', '10.255.255.1', 9100, true);
        insert into kitchen.print_jobs (id, ticket_id, printer_id, idempotency_key, payload, status, attempt_count, max_attempts, failed_at, last_error, updated_at)
        values (gen_random_uuid(), '${ticket}', '${printerId}', 'kanit540-${roundId}', 'kanit540', 'Failed', 5, 5, now(), 'printer unreachable', now());`);
  await sleep(70_000);
  const list = await errorList(cookie);
  printer = { printerEvent: eventsOf(list, 'printer.job_failed') };
  psql(`update kitchen.print_jobs set status = 'Cancelled', updated_at = now() where idempotency_key = 'kanit540-${roundId}'`);
  await sleep(70_000);
  printer.afterRecovery = eventsOf(await errorList(cookie), 'printer.job_failed');
}
log('S5-printer', { orderStatus: [draft.status, submit.status], ticketCreated: Boolean(ticket), injected: 'failed print job on the real ticket, then cancelled', ...printer });

// ---- S6 button interaction and the journey timeline ---------------------------------------------
const traceId = randomUUID().replaceAll('-', '');
await http(cookie, 'GET', `/api/v1/terminals/${terminalId}/table-management/tables`, undefined, { traceparent: `00-${traceId}-${'a'.repeat(16)}-01` });
const interaction = await http(cookie, 'POST', '/api/v1/telemetry/interactions', { items: [{ key: 'b.kanit540.tables', kind: 'click', ageMs: 400, durationMs: 55, longTasks: 0, shiftMilli: 0, outcome: 'ok', traceId }] });
await sleep(1500);
const journey = await http(cookie, 'GET', `/api/v1/management/observability/journeys/requests/${traceId}`);
log('S6-interaction-and-journey', {
  interactionStatus: interaction.status,
  journeySteps: (journey.json?.steps ?? []).map((s) => `${s.kind}:${s.name}`),
  actions: ((await http(cookie, 'GET', '/api/v1/management/observability/journeys/actions')).json ?? []).filter((a) => a.key === 'b.kanit540.tables'),
});

// ---- S7 counters ---------------------------------------------------------------------------------------
log('S7-counters', {
  errorEvents: psql("select layer || '/' || category || '/' || severity || '/' || status || '/' || occurrence_count from observability.error_events order by first_seen_at").split('\n'),
  notificationQueue: psql("select kind || '/' || category || '/' || severity from observability.notification_queue order by enqueued_at").split('\n'),
  telegramChannelConfigured: (await http(cookie, 'GET', '/api/v1/management/observability/errors/notification-channel')).json?.configured,
});

psql(`update identity.users set active = false where username='${USERNAME}'`);
log('S8-cleanup', { testAccountDeactivated: true, note: 'rows created by the proof (order, ticket, printer, print job) stay in the dev database' });
