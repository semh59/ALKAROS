// ALKAROS Waiter PWA — the tables screen (V1-WTR-043, step 7/? of
// docs/engineering/garson-refactor-plan.md's Section 2, the plan's own
// first screen module in its 2.4 ordering).
//
// Deliberately does NOT include openTable() here, even though it is the
// action that opens this very screen's own table cells: on inspection it
// orchestrates across tables/bill/menu (closeBill/openBill, showScreen,
// renderProducts, renderBill, persistDraftsByTable, popHandoffNoteIfAny) —
// none of bill.js or menu.js exist as modules yet, so pulling it in now
// would mean this module importing back from waiter-app.js itself. It
// stays in waiter-app.js until those two exist (the plan's own 2.4
// ordering: tables.js -> party-size.js -> bill.js -> menu.js/
// product-sheet.js -> the rest), then moves wherever it actually belongs.

import { state, el } from '../state.js';
import { escapeHtml, formatMoney } from '../util.js';
import { apiUrl, api } from '../api.js';

export async function loadZones() {
  const result = await api(apiUrl('/table-management/zones'));
  const list = result.ok ? (Array.isArray(result.data) ? result.data : result.data.zones || []) : [];
  state.zones = [{ id: 'all', name: 'Tümü' }].concat(
    list.map((zone) => ({ id: zone.zoneId || zone.id, name: zone.zoneName || zone.name })));
}

export async function loadTables() {
  const result = await api(apiUrl('/table-management/tables'));
  const list = result.ok ? (Array.isArray(result.data) ? result.data : result.data.tables || []) : [];
  state.tables = list.map((table) => ({
    id: table.tableId,
    number: table.tableNumber,
    seats: table.capacity || 0,
    zoneId: table.zoneId,
    status: (table.status || 'Available').toLowerCase(),
    rowVersion: table.rowVersion,
    // V1-RMD-135: the table's real running total.
    amount: table.currentOrderTotal || 0,
    // V1-WTR-019: when the table's current order was opened
    // (orders.orders.created_at) - null when there is no current order.
    // The "masa yaşlanma" (table ageing) badge's own source of truth.
    openedAt: table.currentOrderOpenedAt || null,
    // foundations §0.2: which actions are valid is the server's answer.
    allowedCommands: table.allowedCommands || []
  }));
  renderTables();
}

// V1-WTR-022: the table's floor-plan seats, for the "koltuk bazlı atama"
// idea - a table nobody has ever laid out visually simply has none
// (404), which is not an error, just "this table offers no seat
// picker". Fire-and-forget from openTable(): nothing blocks on this,
// the product sheet just has no seat picker until it resolves.
export async function loadTableSeats(table) {
  state.tableSeats = [];
  if (!table.zoneId) return;
  const result = await api(apiUrl(`/table-management/floor-plans/${table.zoneId}`));
  if (!result.ok || !result.data || !result.data.tables) return;
  const floorTable = result.data.tables.find((candidate) => candidate.tableId === table.id);
  if (!floorTable || !floorTable.seats) return;
  state.tableSeats = floorTable.seats
    .map((seat) => ({ id: seat.seatId, number: seat.number, label: seat.label }))
    .sort((a, b) => a.number - b.number);
}

export function renderZones() {
  el.zoneChips.innerHTML = state.zones.map((zone) => `
    <button type="button" class="chip" data-zone="${escapeHtml(zone.id)}"
            aria-pressed="${state.activeZone === zone.id}">${escapeHtml(zone.name)}</button>`).join('');
}

// V1-TBL-010 (Semih's catch, 2026-09-12): the previous label read as a
// hard "don't touch yet" while the actual behaviour is the opposite for
// the common case - a check sent to the cashier frees the table for a
// new party IMMEDIATELY (SendCheckToCashierAsync, V1-ORD-006), openTable()
// has no status gate at all. The new label reads as informational rather
// than prohibitive.
const TABLE_STATUS = {
  available: { label: 'Boş', cls: 'is-available' },
  occupied: { label: 'Dolu', cls: 'is-occupied' },
  reserved: { label: 'Rezerve', cls: 'is-reserved' },
  cleaning: { label: 'Toplanıyor', cls: 'is-cleaning' },
  outofservice: { label: 'Servis dışı', cls: 'is-cleaning' }
};

// V1-WTR-019: garson-karsilastirma idea #7, "masa yaşlanma göstergesi" -
// how long a table has carried an open tab, so a waiter can spot the one
// that has sat forgotten. Thresholds are a reasonable default (not a
// business-policy number like idea #8's service charge), tuned to a
// normal Turkish restaurant turn time: under 45 dk is unremarkable, 45-90
// is worth a glance, 90+ is worth walking over for.
const TABLE_AGE_WARNING_MINUTES = 45;
const TABLE_AGE_DANGER_MINUTES = 90;

function tableAgeMinutes(openedAt) {
  if (!openedAt) return null;
  const opened = new Date(openedAt).getTime();
  if (Number.isNaN(opened)) return null;
  return Math.max(0, Math.floor((Date.now() - opened) / 60000));
}

function tableAgeBadgeHtml(minutes) {
  if (minutes == null) return '';
  const tier = minutes >= TABLE_AGE_DANGER_MINUTES ? 'tag-age-danger'
    : minutes >= TABLE_AGE_WARNING_MINUTES ? 'tag-age-warning' : 'tag-age-normal';
  return `<span class="tag ${tier}">${minutes} dk</span>`;
}

export function renderTables() {
  const visible = state.activeZone === 'all'
    ? state.tables
    : state.tables.filter((table) => table.zoneId === state.activeZone);

  if (visible.length === 0) {
    el.tablesGrid.innerHTML = '<div class="empty">Bu bölgede masa yok.</div>';
    return;
  }

  el.tablesGrid.innerHTML = visible.map((table) => {
    const status = TABLE_STATUS[table.status] || { label: table.status, cls: '' };
    // V1-RMD-169: found by the 2026-09-10 Garson audit (foundations.md
    // §0.2) — this used to read `table.amount > 0`, deriving whether a
    // table is occupied from money instead of the server's own status
    // field. A table with an open check whose current total happens to
    // be zero (every line comped, or a Draft round not yet priced) would
    // wrongly look empty; the server already says "Occupied" directly
    // (set when an order attaches to the table, V1-ORD-006) and that is
    // what "does this table already have an order to add to" actually
    // means, not its running total.
    const busy = table.status === 'occupied';
    // A table already carrying an order gets a shortcut straight to the
    // menu; tapping the table itself opens its bill.
    const quick = busy
      ? `<button type="button" class="table-quick" data-quick="${escapeHtml(table.id)}"
                 aria-label="${escapeHtml(table.number)} masasına ürün ekle">
           <svg class="icon" aria-hidden="true"><use href="#ico-add"/></svg>
         </button>`
      : '';
    return `
      <div class="table-cell${busy ? ' has-quick' : ''}">
        <button type="button" class="table ${status.cls}" data-table="${escapeHtml(table.id)}">
          <span class="table-top">
            <span class="table-number">${escapeHtml(table.number)}</span>
            <span class="table-seats">
              <svg class="icon" aria-hidden="true"><use href="#ico-seats"/></svg>${escapeHtml(table.seats)}
            </span>
          </span>
          <span class="tagrow"><span class="tag tag-status">${escapeHtml(status.label)}</span>${busy ? tableAgeBadgeHtml(tableAgeMinutes(table.openedAt)) : ''}</span>
          <span class="table-amount${busy ? '' : ' is-empty'}">${busy ? formatMoney(table.amount) : 'Boş'}</span>
        </button>
        ${quick}
      </div>`;
  }).join('');
}
