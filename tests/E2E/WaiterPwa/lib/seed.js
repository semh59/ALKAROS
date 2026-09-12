// Seeds the minimal, realistic data every WaiterPwa scenario in this suite
// needs: one waiter user with a REAL password (the login screen itself is
// exercised, not bypassed with a cookie), a table with two floor-plan
// seats (for the seat picker), and two catalog products - one plain, one
// carrying a modifier group (for the modifier picker).
//
// The waiter role is granted EVERY permission in the catalog. This is
// deliberately not least-privilege: the goal here is exercising every
// waiter-facing action (void, comp, transfer-server-any, ...) in one
// browser session, not re-testing the authorization model itself - that is
// already covered by tests/Modules/Identity/Authorization's own suite.
//
// node-postgres's parameterized query path (the extended protocol) accepts
// exactly one statement per call, so every INSERT here is issued as its own
// single-statement call - a multi-statement string with placeholders would
// fail outright, unlike the plain multi-statement SQL migrate.js runs.
import { randomUUID } from 'node:crypto';
import { hashPassword } from './passwordHash.js';

export const WAITER_USERNAME = 'e2e.garson';
export const WAITER_PASSWORD = 'E2eGarsonSifre!42';
export const COLLEAGUE_DISPLAY_NAME = 'E2E Meslektaş';

export async function seedDatabase(client) {
  const userId = randomUUID();
  const roleId = randomUUID();
  await client.query(
    `INSERT INTO identity.users (user_id, username, password_hash, display_name, active)
     VALUES ($1, $2, $3, 'E2E Garson', true)`,
    [userId, WAITER_USERNAME, hashPassword(WAITER_PASSWORD)],
  );
  await client.query(
    `INSERT INTO identity.roles (role_id, code, name) VALUES ($1, 'e2e-waiter-role', 'E2E Garson Rolü')`,
    [roleId],
  );
  const { rows: permissionRows } = await client.query('SELECT permission_id FROM identity.permissions');
  for (const row of permissionRows) {
    await client.query(
      'INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id) VALUES ($1, $2, $3)',
      [randomUUID(), roleId, row.permission_id],
    );
  }
  await client.query(
    'INSERT INTO identity.user_roles (user_role_id, user_id, role_id) VALUES ($1, $2, $3)',
    [randomUUID(), userId, roleId],
  );

  // A second active user - orders/staff lists every OTHER active user, so
  // the transfer-server sheet (profile menu -> "Açık masaları devret") has
  // someone to hand off to.
  const colleagueId = randomUUID();
  await client.query(
    `INSERT INTO identity.users (user_id, username, password_hash, display_name, active)
     VALUES ($1, 'e2e.meslektas', 'not-used', $2, true)`,
    [colleagueId, COLLEAGUE_DISPLAY_NAME],
  );

  const zoneId = randomUUID();
  const tableId = randomUUID();
  const seatOneId = randomUUID();
  const seatTwoId = randomUUID();
  const tableNumber = 'E2E-1';
  await client.query(
    `INSERT INTO table_mgmt.zones (zone_id, code, name) VALUES ($1, 'E2E-ZONE', 'E2E Salon')`,
    [zoneId],
  );
  await client.query(
    `INSERT INTO table_mgmt.tables (table_id, zone_id, table_number, capacity, current_status)
     VALUES ($1, $2, $3, 4, 'Available')`,
    [tableId, zoneId, tableNumber],
  );
  await client.query(
    `INSERT INTO table_mgmt.table_seats (seat_id, table_id, seat_number, label, x, y)
     VALUES ($1, $2, 1, 'Koltuk 1', 0, 0)`,
    [seatOneId, tableId],
  );
  await client.query(
    `INSERT INTO table_mgmt.table_seats (seat_id, table_id, seat_number, label, x, y)
     VALUES ($1, $2, 2, 'Koltuk 2', 40, 0)`,
    [seatTwoId, tableId],
  );

  // The WaiterPwa client discovers a table's seats only through the
  // floor-plan endpoint (GET .../floor-plans/{zoneId}), which 404s without
  // an actual designed layout - a bare zones/tables/table_seats seed (the
  // shape the server's own seat-id VALIDATION query is content with) is
  // not enough to make the seat picker appear at all. Found by this suite
  // itself: the first run seeded no layout and the "Koltuk" optgroup never
  // rendered.
  await client.query(
    `INSERT INTO table_mgmt.zone_floor_plans (zone_id, canvas_width, canvas_height)
     VALUES ($1, 800, 600)`,
    [zoneId],
  );
  await client.query(
    `INSERT INTO table_mgmt.table_layouts (table_id, zone_id, x, y, width, height, shape, rotation_degrees)
     VALUES ($1, $2, 40, 40, 120, 120, 'Rectangle', 0)`,
    [tableId, zoneId],
  );

  // A second, seatless table that stays Available - the target the
  // table-transfer sheet needs (its own list only offers Available tables).
  const secondTableId = randomUUID();
  const secondTableNumber = 'E2E-2';
  await client.query(
    `INSERT INTO table_mgmt.tables (table_id, zone_id, table_number, capacity, current_status)
     VALUES ($1, $2, $3, 2, 'Available')`,
    [secondTableId, zoneId, secondTableNumber],
  );

  const plainProductId = randomUUID();
  const modifierProductId = randomUUID();
  const modifierGroupId = randomUUID();
  const modifierId = randomUUID();
  await client.query(
    `INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, active, is_available, current_price)
     VALUES ($1, 'E2E-KOFTE', 'E2E Köfte', 1, 1, true, true, 280.00)`,
    [plainProductId],
  );
  await client.query(
    `INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, active, is_available, current_price)
     VALUES ($1, 'E2E-IZGARA', 'E2E Izgara Tabağı', 1, 1, true, true, 350.00)`,
    [modifierProductId],
  );
  await client.query(
    `INSERT INTO catalog.modifier_groups (modifier_group_id, code, name, selection_type, min_selections, max_selections, active)
     VALUES ($1, 'E2E-EXTRA', 'Ekstralar', 2, 0, 2, true)`,
    [modifierGroupId],
  );
  await client.query(
    `INSERT INTO catalog.modifiers (modifier_id, modifier_group_id, code, name, price_delta, active)
     VALUES ($1, $2, 'E2E-PILAV', 'Ekstra pilav', 40.00, true)`,
    [modifierId, modifierGroupId],
  );
  await client.query(
    `INSERT INTO catalog.product_modifier_groups (product_modifier_group_id, product_id, modifier_group_id)
     VALUES ($1, $2, $3)`,
    [randomUUID(), modifierProductId, modifierGroupId],
  );

  // V1-RMD-144: submit-draft consumes stock; both products need a mapping
  // or the whole send fails.
  for (const productId of [plainProductId, modifierProductId]) {
    const locationId = randomUUID();
    const stockItemId = randomUUID();
    const suffix = stockItemId.replace(/-/g, '').slice(0, 8);
    await client.query(
      `INSERT INTO inventory.stock_locations (id, code, name, location_type)
       VALUES ($1, $2, 'E2E Stock Location', 'Counter')`,
      [locationId, `E2ELOC-${suffix}`],
    );
    await client.query(
      `INSERT INTO inventory.stock_items (id, code, name, item_type, tracking_unit_code, default_location_id)
       VALUES ($1, $2, 'E2E Stock Item', 'Portion', 'adet', $3)`,
      [stockItemId, `E2ESTK-${suffix}`, locationId],
    );
    await client.query(
      `INSERT INTO inventory.product_stock_mappings (product_id, stock_item_id, quantity_multiplier)
       VALUES ($1, $2, 1.0)`,
      [productId, stockItemId],
    );
    await client.query(
      `INSERT INTO inventory.stock_balances (stock_balance_id, stock_item_id, stock_location_id, on_hand_quantity, reserved_quantity, available_quantity)
       VALUES ($1, $2, $3, 50, 0, 50)`,
      [randomUUID(), stockItemId, locationId],
    );
  }

  return {
    waiterUsername: WAITER_USERNAME,
    waiterPassword: WAITER_PASSWORD,
    colleagueDisplayName: COLLEAGUE_DISPLAY_NAME,
    tableId,
    tableNumber,
    secondTableId,
    secondTableNumber,
    zoneId,
    seatOneId,
    seatTwoId,
    plainProductId,
    modifierProductId,
  };
}
