import { test, expect } from '@playwright/test';

// V1-RMD-349 (independent 2026-09-26 audit, düşük seviye bulgu): the Cashier
// PWA manifest referenced icon-192.png/icon-512.png, but neither file
// existed under src/Clients/Cashier/wwwroot/ - a browser's own "add to home
// screen" install prompt would silently show a broken/generic icon (Chrome
// requires at least one real ≥192px icon to consider a manifest installable
// at all). Fetches the manifest exactly as a real browser would, then every
// icon it lists, against the real built Cashier shell this whole suite
// already serves - not a static file-existence check on disk.
test.describe('Kasa PWA manifest', () => {
  test('manifest.json geçerlidir ve listelediği her ikon gerçekten yükleniyor', async ({ page, baseURL }) => {
    const manifestUrl = new URL('/cashier/manifest.json', baseURL).toString();
    const manifestResponse = await page.request.get(manifestUrl);
    expect(manifestResponse.ok(), `manifest.json -> ${manifestResponse.status()}`).toBeTruthy();
    const manifest = await manifestResponse.json();

    expect(Array.isArray(manifest.icons)).toBeTruthy();
    expect(manifest.icons.length).toBeGreaterThan(0);

    for (const icon of manifest.icons) {
      const iconUrl = new URL(icon.src, manifestUrl).toString();
      const iconResponse = await page.request.get(iconUrl);
      expect(iconResponse.ok(), `${icon.src} -> ${iconResponse.status()}`).toBeTruthy();
      expect(iconResponse.headers()['content-type']).toContain('image/png');
      const bytes = await iconResponse.body();
      expect(bytes.length).toBeGreaterThan(0);
    }
  });
});
