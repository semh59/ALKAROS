// ALKAROS Waiter PWA — the tables screen (V1-WTR-043, step 7/? of
// docs/engineering/garson-refactor-plan.md's Section 2, the plan's own
// first screen module in its 2.4 ordering).
//
// openTable() (plus its own two small helpers, showScreen and
// popHandoffNoteIfAny) moved in here too, in a later step (V1-WTR-048)
// once bill.js and menu.js both existed to resolve what used to orchestrate
// across three not-yet-extracted modules (closeBill/openBill, renderProducts,
// renderBill, persistDraftsByTable) — exactly the point this file's own
// history note above always said it was waiting for.

import { state, el, persistDraftsByTable } from '../state.js';
import { escapeHtml, formatMoney } from '../util.js';
import { apiUrl, api } from '../api.js';
import { toast } from '../toast.js';
import { activeItems, loadOrder, renderBill, openBill, closeBill } from '../sheets/bill.js';
import { renderProducts } from './menu.js';

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
          <span class="table-amount${busy ? '' : ' is-empty'}">${busy ? formatMoney(table.amount) : ''}</span>
        </button>
        ${quick}
      </div>`;
  }).join('');
}

export function showScreen(name) {
  const onMenu = name === 'menu';
  el.tablesScreen.dataset.state = onMenu ? 'behind' : 'on';
  el.menuScreen.dataset.state = onMenu ? 'on' : 'off';
  // V1-RMD-175: found by the 2026-09-10 Garson audit — this used to
  // force-focus the search box on every single menu open (every table
  // tap, every return from the bill), popping the on-screen keyboard even
  // when the waiter only wanted to tap a category or a product. Search is
  // still one tap away; it just is not forced on the waiter anymore.
}

// ══ Opening a table ════════════════════════════════════════════════

export async function openTable(tableId, goStraightToMenu) {
  const table = state.tables.find((candidate) => candidate.id === tableId);
  if (!table) return;

  // An unsent round belongs to the table it was composed for, and it is
  // kept there. The previous version showed a toast saying the round was
  // still held and then deleted it on the very next line — nine items
  // typed for table 5 vanished the moment the waiter glanced at table 7,
  // while the screen claimed otherwise.
  if (state.table && state.table.id !== tableId) {
    if (state.draft.length > 0) {
      state.draftsByTable.set(state.table.id, {
        number: state.table.number,
        lines: state.draft
      });
      toast(`${state.table.number} masasının gönderilmemiş turu saklandı.`, { warning: true });
    } else {
      state.draftsByTable.delete(state.table.id);
    }
  }
  if (!state.table || state.table.id !== tableId) {
    const held = state.draftsByTable.get(tableId);
    state.draft = held ? held.lines : [];
    state.draftsByTable.delete(tableId);
    // V1-WTR-015: a party size typed for the PREVIOUS table must never
    // leak into a different one's fresh draft.
    state.draftPartySize = null;
  }
  persistDraftsByTable();

  state.table = table;
  el.menuTableName.textContent = `${table.number} masası`;
  el.billTitle.textContent = `${table.number} masası`;

  void loadTableSeats(table);
  await loadOrder(tableId);
  // V1-RMD-179: if a second, newer openTable() call for a different table
  // has since taken over (loadOrder() above already refused to write a
  // stale state.order for exactly this reason), this call's own remaining
  // work — rendering, switching screens, popping a hand-off note — belongs
  // to a table that is no longer the one on screen. Stop here.
  if (state.table?.id !== tableId) return;
  renderBill();

  // The locked flow: an occupied table opens its bill, an empty one opens
  // the menu. The bill sheet is never raised over the menu - that covers the
  // very screen the waiter came to use.
  const empty = !state.order || activeItems().length === 0;
  if (empty || goStraightToMenu) {
    closeBill();
    showScreen('menu');
    renderProducts();
  } else {
    showScreen('tables');
    openBill();
  }

  // V1-WTR-013: pops (reads and marks seen, in one server round trip) any
  // pending hand-off note left for this waiter — fires on every table
  // open, but a note only ever exists once, so in practice this shows it
  // exactly the first time a table is opened after receiving it and does
  // nothing on every open after that.
  void popHandoffNoteIfAny();
}

async function popHandoffNoteIfAny() {
  const result = await api(apiUrl('/orders/handoff-note/pop'), { method: 'POST' });
  if (!result.ok || !result.data) return;
  toast(`${result.data.fromDisplayName}'den not: ${result.data.note}`, { warning: true });
}
