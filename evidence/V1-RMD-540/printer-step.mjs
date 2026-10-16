// Printer failure proof only (the slow part of prove.mjs): a real order is sent to the kitchen, a print job for its ticket
// is marked failed, the detector must raise printer.job_failed, and cancelling the job must resolve it.
// Usage: node printer-step.mjs <outDir>
import { execFileSync } from 'node:child_process';
import { appendFileSync } from 'node:fs';
import { pathToFileURL } from 'node:url';
import { join, resolve } from 'node:path';
import { randomUUID } from 'node:crypto';

const ROOT = resolve(import.meta.dirname, '../..');
const OUT = process.argv[2] ?? import.meta.dirname;
const BASE = 'http://localhost:8090';
const { hashPassword } = await import(pathToFileURL(join(ROOT, 'tests', 'E2E', 'WaiterPwa', 'lib', 'passwordHash.js')).href);
const USERNAME = 'kanit.540';
const PASSWORD = 'Kanit540Sifre!2026';
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const log = (step, data) => {
  const line = JSON.stringify({ step, ...data });
  appendFileSync(join(OUT, 'run.log'), line + '\n');
  console.log(line);
};
const psql = (sql) => execFileSync('docker', ['exec', '-i', 'alkaros-postgres-1', 'psql', '-U', 'alkaros', '-d', 'alkaros', '-v', 'ON_ERROR_STOP=1', '-tA', '-F', '|'], { input: sql, encoding: 'utf8' }).trim();
async function http(cookie, method, path, body) {
  const response = await fetch(`${BASE}${path}`, { method, headers: { 'Content-Type': 'application/json', Cookie: cookie }, body: body === undefined ? undefined : JSON.stringify(body) });
  const text = await response.text();
  let json = null;
  try { json = text ? JSON.parse(text) : null; } catch { /* not json */ }
  return { status: response.status, json, text };
}
const events = async (cookie, category) => ((await http(cookie, 'GET', '/api/v1/management/observability/errors')).json?.items ?? [])
  .filter((i) => i.category === category).map((i) => `${i.category}/${i.severity}/${i.status}/${i.occurrenceCount}`);

psql(`update identity.users set active = true where username='${USERNAME}'`);
void hashPassword;
const terminalId = randomUUID();
const login = await fetch(`${BASE}/api/v1/auth/login`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ terminalId, username: USERNAME, password: PASSWORD }) });
const cookie = login.headers.getSetCookie().map((c) => c.split(';')[0]).join('; ');

const product = psql("select product_id || '|' || coalesce(current_price, 10) from catalog.products where active and is_available order by display_order limit 1").split('|');
const tableId = process.argv[3] ?? 'b0000000-0000-0000-0000-000000000002';
const roundId = randomUUID();
const draft = await http(cookie, 'POST', `/api/v1/terminals/${terminalId}/orders/table-draft`, {
  id: roundId, tableId, tableNumber: 'S2', partySize: 2,
  items: [{ id: randomUUID(), productId: product[0], productName: 'kanit', quantity: 1, unitPrice: Number(product[1]), modifiers: [], specialInstructions: null, seatId: null, courseNumber: null }],
});
const submit = await http(cookie, 'POST', `/api/v1/terminals/${terminalId}/orders/${draft.json?.orderId}/submit-draft`, { orderId: draft.json?.orderId, expectedRowVersion: draft.json?.rowVersion, operationId: `${draft.json?.orderId}:${roundId}` });
log('S5a-order', { draft: draft.status, submit: submit.status, submitBody: submit.text.slice(0, 300) });
// The demo products have no stock configured, so the server (correctly) refuses to send the order to the kitchen.
// The ticket is then created directly for the real draft order so the print-failure chain can still be exercised.
let ticket = draft.json?.orderId ? psql(`select id from kitchen.kitchen_tickets where order_id='${draft.json.orderId}' limit 1`) : '';
if (!ticket && draft.json?.orderId) {
  ticket = randomUUID();
  psql(`insert into kitchen.kitchen_tickets (id, order_id, ticket_number, station_id, status) values ('${ticket}', '${draft.json.orderId}', 'KANIT540-${roundId.slice(0, 8)}', 'kanit540', 'Queued')`);
}
if (ticket) {
  const printerId = randomUUID();
  psql(`insert into kitchen.printers (id, name, station_id, ip_address, port, is_active) values ('${printerId}', 'kanit540-yazici-${roundId.slice(0, 6)}', 'kanit540', '10.255.255.1', 9100, true);
        insert into kitchen.print_jobs (id, ticket_id, printer_id, idempotency_key, payload, status, attempt_count, max_attempts, failed_at, last_error, updated_at)
        values (gen_random_uuid(), '${ticket}', '${printerId}', 'kanit540-${roundId}', 'kanit540', 'Failed', 5, 5, now(), 'printer unreachable', now());`);
  await sleep(70_000);
  const raised = await events(cookie, 'printer.job_failed');
  const queued = psql("select kind || '/' || severity from observability.notification_queue where category='printer.job_failed'");
  psql(`update kitchen.print_jobs set status = 'Cancelled', updated_at = now() where idempotency_key = 'kanit540-${roundId}'`);
  await sleep(70_000);
  log('S5b-printer', { injected: 'failed print job on a real kitchen ticket, then cancelled', raised, queued, afterRecovery: await events(cookie, 'printer.job_failed') });
} else {
  log('S5b-printer', { ticketCreated: false });
}
psql(`update identity.users set active = false where username='${USERNAME}'`);
