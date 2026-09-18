// @ts-check
import { defineConfig, devices } from '@playwright/test';

export const BASE_URL = 'http://127.0.0.1:5098';

export default defineConfig({
  testDir: './specs',
  timeout: 30_000,
  expect: { timeout: 8_000 },
  fullyParallel: false,
  // Every scenario shares the same seeded terminal/session and the one
  // fixed KASA-1 table the vanilla Cashier client hardcodes - a second
  // worker racing the same table/order would corrupt the other's
  // assertions, same reasoning as tests/E2E/WaiterPwa's own config.
  workers: 1,
  retries: 0,
  reporter: [['list'], ['html', { open: 'never', outputFolder: 'report' }]],
  globalSetup: './global-setup.js',
  use: {
    baseURL: BASE_URL,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },
  projects: [
    // PosTerminal/Cashier is a fixed till screen, not a handheld app -
    // exercised at a real desktop-class viewport (its CSS grid layout
    // assumes room for a category rail + product grid + a fixed ticket
    // column side by side).
    {
      name: 'chromium',
      use: {
        ...devices['Desktop Chrome'],
        viewport: { width: 1440, height: 900 },
      },
    },
  ],
});
