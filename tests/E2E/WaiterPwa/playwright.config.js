// @ts-check
import { defineConfig, devices } from '@playwright/test';

export const BASE_URL = 'http://127.0.0.1:5099';

export default defineConfig({
  testDir: './specs',
  timeout: 30_000,
  expect: { timeout: 8_000 },
  fullyParallel: false,
  // Every scenario shares one seeded table/order - a second worker racing
  // the same table would corrupt the other's assertions.
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
    // WaiterPwa is a handheld app (waiter-app.css's own tablet breakpoint
    // is 820px - at or above that width the bill sheet becomes a fixed
    // always-present side column by design, "on a tablet the bill is a
    // fixed column" per that CSS's own comment). A desktop-width viewport
    // put that column's real estate over part of the tables grid and every
    // click there failed with "<body> intercepts pointer events". A phone
    // viewport is what this app is actually built for.
    {
      name: 'chromium',
      use: {
        ...devices['Desktop Chrome'],
        viewport: { width: 390, height: 844 },
      },
    },
  ],
});
