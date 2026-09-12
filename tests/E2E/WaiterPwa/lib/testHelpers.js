import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { expect } from '@playwright/test';

const HERE = dirname(fileURLToPath(import.meta.url));
const STATE_FILE = join(HERE, '..', '.e2e-state.json');

export function readSeed() {
  return JSON.parse(readFileSync(STATE_FILE, 'utf8')).seed;
}

/** Logs the real waiter user in through the actual login screen. */
export async function login(page, seed) {
  await page.goto('/');
  await page.locator('#loginUsername').fill(seed.waiterUsername);
  await page.locator('#loginPassword').fill(seed.waiterPassword);
  await page.locator('#loginSubmit').click();
  await expect(page.locator('#tablesGrid [data-table]').first()).toBeVisible({ timeout: 15_000 });
}

/** Opens the seeded table straight onto the menu screen. */
export async function openSeedTable(page, seed) {
  await page.locator(`[data-table="${seed.tableId}"]`).click();
  await expect(page.locator('#productList [data-product]').first()).toBeVisible();
}

/**
 * Adds the seeded modifier-bearing product via the full options sheet, so
 * the seat/course pickers and the modifier itself are all exercised.
 * Leaves the sheet open at the point of confirming — caller clicks
 * #optionsConfirm once any extra picks (seat/course) are made.
 */
export async function openModifierProductSheet(page, seed) {
  await page.locator(`[data-product="${seed.modifierProductId}"]`).click();
  await expect(page.locator('#optionsSheet')).toHaveClass(/is-open/);
}
