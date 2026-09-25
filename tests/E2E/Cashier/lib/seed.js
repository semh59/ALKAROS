// Seeds the minimal, realistic data every Kasa (Cashier/PosTerminal/
// CustomerDisplay) scenario in this suite needs: one cashier user with a
// REAL password (the login screen itself is exercised, not bypassed with a
// cookie), the fixed "KASA-1" over-the-counter table the vanilla
// `src/Clients/Cashier` client hardcodes (tableId
// 00000000-0000-0000-0000-000000000001 - see cashier-app.js's own
// dispatchOrderToKitchen), and two catalog products - one with a LOW
// remaining stock count (to exercise the `.is-low` badge class) and one
// with a normal count.
//
// The cashier role is granted EVERY permission in the catalog, same
// precedent as tests/E2E/WaiterPwa/lib/seed.js: this suite exercises real
// screens end to end, not the authorization model itself (already covered
// by tests/Modules/Identity/Authorization).
import { randomUUID } from 'node:crypto';
import { hashPassword } from './passwordHash.js';

export const CASHIER_USERNAME = 'e2e.kasiyer';
export const CASHIER_PASSWORD = 'E2eKasiyerSifre!42';
export const LIMITED_USERNAME = 'e2e.kasiyer.sinirli';
export const SECOND_MANAGER_USERNAME = 'e2e.mudur.ikinci';
export const KASA_1_TABLE_ID = '00000000-0000-0000-0000-000000000001';
export const KASA_1_TABLE_NUMBER = 'KASA-1';

export async function seedDatabase(client) {
  const userId = randomUUID();
  const roleId = randomUUID();
  await client.query(
    `INSERT INTO identity.users (user_id, username, password_hash, display_name, active)
     VALUES ($1, $2, $3, 'E2E Kasiyer', true)`,
    [userId, CASHIER_USERNAME, hashPassword(CASHIER_PASSWORD)],
  );
  await client.query(
    `INSERT INTO identity.roles (role_id, code, name) VALUES ($1, 'e2e-cashier-role', 'E2E Kasiyer Rolü')`,
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

  // A cashier who holds every permission EXCEPT reconciliation.manage: proves
  // the manual card-payment resolution (V1-RMD-264) is manager-only.
  const limitedUserId = randomUUID();
  const limitedRoleId = randomUUID();
  await client.query(
    `INSERT INTO identity.users (user_id, username, password_hash, display_name, active)
     VALUES ($1, $2, $3, 'E2E Sınırlı Kasiyer', true)`,
    [limitedUserId, LIMITED_USERNAME, hashPassword(CASHIER_PASSWORD)],
  );
  await client.query(
    `INSERT INTO identity.roles (role_id, code, name) VALUES ($1, 'e2e-limited-cashier-role', 'E2E Sınırlı Kasiyer Rolü')`,
    [limitedRoleId],
  );
  const { rows: limitedRows } = await client.query(
    "SELECT permission_id FROM identity.permissions WHERE code <> 'reconciliation.manage'",
  );
  for (const row of limitedRows) {
    await client.query(
      'INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id) VALUES ($1, $2, $3)',
      [randomUUID(), limitedRoleId, row.permission_id],
    );
  }
  await client.query(
    'INSERT INTO identity.user_roles (user_role_id, user_id, role_id) VALUES ($1, $2, $3)',
    [randomUUID(), limitedUserId, limitedRoleId],
  );

  // A SECOND person holding every permission: the manual card confirmation is two-person (one claims the
  // charge with the slip number, a DIFFERENT one approves), so a browser test needs two real users.
  const secondManagerId = randomUUID();
  const secondManagerRoleId = randomUUID();
  await client.query(
    `INSERT INTO identity.users (user_id, username, password_hash, display_name, active)
     VALUES ($1, $2, $3, 'E2E İkinci Müdür', true)`,
    [secondManagerId, SECOND_MANAGER_USERNAME, hashPassword(CASHIER_PASSWORD)],
  );
  await client.query(
    `INSERT INTO identity.roles (role_id, code, name) VALUES ($1, 'e2e-second-manager-role', 'E2E İkinci Müdür Rolü')`,
    [secondManagerRoleId],
  );
  for (const row of permissionRows) {
    await client.query(
      'INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id) VALUES ($1, $2, $3)',
      [randomUUID(), secondManagerRoleId, row.permission_id],
    );
  }
  await client.query(
    'INSERT INTO identity.user_roles (user_role_id, user_id, role_id) VALUES ($1, $2, $3)',
    [randomUUID(), secondManagerId, secondManagerRoleId],
  );

  // The vanilla Cashier client (src/Clients/Cashier/wwwroot/cashier-app.js)
  // hardcodes this exact table id/number as its own single over-the-counter
  // "table" - migration 098 (V1-RMD-157) already provisions this exact row
  // on every environment (a genuinely fresh deployment used to 503 without
  // it), so this seed does not need to create it, only rely on it existing.

  const lowStockProductId = randomUUID();
  const normalStockProductId = randomUUID();
  await client.query(
    `INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, active, is_available, current_price)
     VALUES ($1, 'E2E-KASA-DUSUK', 'E2E Kasa Düşük Stok', 1, 1, true, true, 190.00)`,
    [lowStockProductId],
  );
  await client.query(
    `INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, active, is_available, current_price)
     VALUES ($1, 'E2E-KASA-NORMAL', 'E2E Kasa Normal Stok', 1, 1, true, true, 220.00)`,
    [normalStockProductId],
  );

  // A dedicated, round-priced, deep-stock product for the split-payment
  // specs (07+): every scenario there builds its own bill from it, and a
  // 100,00 unit price makes equal-split rounding cases (100/3, 300/7...)
  // exact to reason about. Kept separate from the two products above so
  // consuming its stock never disturbs 02's exact "Kalan 3"/"Kalan 500"
  // badge assertions.
  // A zone with untouched tables for the payment-aware transfer/merge specs
  // (11+): each scenario takes its own pair so none inherits an order, bill or
  // payment left on another scenario's table.
  const transferZoneId = randomUUID();
  await client.query(
    `INSERT INTO table_mgmt.zones (zone_id, code, name) VALUES ($1, 'E2E-TRF', 'E2E Devir Salonu')`,
    [transferZoneId],
  );
  const transferTables = [];
  for (let i = 1; i <= 12; i++) {
    const tableId = randomUUID();
    await client.query(
      `INSERT INTO table_mgmt.tables (table_id, zone_id, table_number, capacity, current_status)
       VALUES ($1, $2, $3, 4, 'Available')`,
      [tableId, transferZoneId, `TRF-${i}`],
    );
    transferTables.push({ tableId, tableNumber: `TRF-${i}` });
  }

  const paymentProductId = randomUUID();
  await client.query(
    `INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, active, is_available, current_price)
     VALUES ($1, 'E2E-ODEME-URUN', 'E2E Ödeme Ürünü', 1, 1, true, true, 100.00)`,
    [paymentProductId],
  );

  const stockPlan = [
    { productId: lowStockProductId, onHand: 3 },
    { productId: normalStockProductId, onHand: 500 },
    { productId: paymentProductId, onHand: 5000 },
  ];
  for (const { productId, onHand } of stockPlan) {
    const locationId = randomUUID();
    const stockItemId = randomUUID();
    const suffix = stockItemId.replace(/-/g, '').slice(0, 8);
    await client.query(
      `INSERT INTO inventory.stock_locations (id, code, name, location_type)
       VALUES ($1, $2, 'E2E Kasa Stock Location', 'Counter')`,
      [locationId, `E2EKASALOC-${suffix}`],
    );
    await client.query(
      `INSERT INTO inventory.stock_items (id, code, name, item_type, tracking_unit_code, default_location_id)
       VALUES ($1, $2, 'E2E Kasa Stock Item', 'Portion', 'adet', $3)`,
      [stockItemId, `E2EKASASTK-${suffix}`, locationId],
    );
    await client.query(
      `INSERT INTO inventory.product_stock_mappings (product_id, stock_item_id, quantity_multiplier)
       VALUES ($1, $2, 1.0)`,
      [productId, stockItemId],
    );
    await client.query(
      `INSERT INTO inventory.stock_balances (stock_balance_id, stock_item_id, stock_location_id, on_hand_quantity, reserved_quantity, available_quantity)
       VALUES ($1, $2, $3, $4, 0, $4)`,
      [randomUUID(), stockItemId, locationId, onHand],
    );
  }

  return {
    cashierUsername: CASHIER_USERNAME,
    cashierPassword: CASHIER_PASSWORD,
    limitedUsername: LIMITED_USERNAME,
    secondManagerUsername: SECOND_MANAGER_USERNAME,
    kasa1TableId: KASA_1_TABLE_ID,
    kasa1TableNumber: KASA_1_TABLE_NUMBER,
    lowStockProductId,
    normalStockProductId,
    paymentProductId,
    transferTables,
  };
}
