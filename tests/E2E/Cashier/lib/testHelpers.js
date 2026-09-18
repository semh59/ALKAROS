import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { expect } from '@playwright/test';

const HERE = dirname(fileURLToPath(import.meta.url));
const STATE_FILE = join(HERE, '..', '.e2e-state.json');

export function readSeed() {
  return JSON.parse(readFileSync(STATE_FILE, 'utf8')).seed;
}

/** Logs the real cashier user in through PosTerminal's actual Cashier.tsx login screen. */
export async function loginCashier(page, seed) {
  await page.goto('/');
  await page.getByLabel('Kullanıcı adı').fill(seed.cashierUsername);
  await page.getByLabel('Parola').fill(seed.cashierPassword);
  await page.getByRole('button', { name: /Giriş yap/ }).click();
  await expect(page.getByRole('button', { name: 'Yeni sipariş aç' })).toBeVisible({ timeout: 15_000 });
}

/** Starts a new sale (PosTerminal's own "walk-in" order, no table picker). */
export async function startOrder(page) {
  await page.getByRole('button', { name: 'Yeni sipariş aç' }).click();
  await expect(page.getByText('Soldaki katalogdan ürün seçin.')).toBeVisible();
}
