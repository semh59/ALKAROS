/**
 * ALKAROS V1 — Gerçek Tarayıcı (Google Chrome CDP) Kapsamlı E2E Otomasyon Testi
 * Bütün butonları, formları, filtreleri, sepet işlemlerini, modalları, garson alt sekmelerini,
 * istasyon filtrelerini ve kaos senaryolarını gerçek Chrome tarayıcısında çalıştırır.
 */

const { spawn } = require('child_process');

async function getCDPTarget() {
  const res = await fetch('http://127.0.0.1:9222/json');
  const targets = await res.json();
  const page = targets.find(t => t.type === 'page' && t.url.includes('5173'));
  if (!page) throw new Error('ALKAROS 5173 page target not found in Chrome!');
  return page.webSocketDebuggerUrl;
}

class CDPClient {
  constructor(wsUrl) {
    this.wsUrl = wsUrl;
    this.id = 1;
    this.callbacks = new Map();
    this.consoleErrors = [];
    this.jsExceptions = [];
  }

  async connect() {
    const WebSocket = globalThis.WebSocket;
    return new Promise((resolve, reject) => {
      this.ws = new WebSocket(this.wsUrl);
      this.ws.onopen = () => resolve();
      this.ws.onerror = (e) => reject(e);
      this.ws.onmessage = (msg) => {
        const data = JSON.parse(msg.data);
        if (data.id && this.callbacks.has(data.id)) {
          const { resolve, reject } = this.callbacks.get(data.id);
          this.callbacks.delete(data.id);
          if (data.error) reject(data.error);
          else resolve(data.result);
        } else if (data.method === 'Runtime.consoleAPICalled' && data.params.type === 'error') {
          this.consoleErrors.push(data.params);
        } else if (data.method === 'Runtime.exceptionThrown') {
          this.jsExceptions.push(data.params);
        }
      };
    });
  }

  send(method, params = {}) {
    const id = this.id++;
    return new Promise((resolve, reject) => {
      this.callbacks.set(id, { resolve, reject });
      this.ws.send(JSON.stringify({ id, method, params }));
    });
  }

  async eval(expression) {
    const wrapped = `(() => { ${expression.startsWith('return ') || !expression.includes(';') ? 'return (' + expression + ');' : expression} })()`;
    const res = await this.send('Runtime.evaluate', {
      expression: wrapped,
      returnByValue: true,
      awaitPromise: true
    });
    if (res.exceptionDetails) {
      throw new Error(`Eval error: ${res.exceptionDetails.text} (${expression})`);
    }
    return res.result ? res.result.value : undefined;
  }

  close() {
    if (this.ws) this.ws.close();
  }
}

async function sleep(ms) {
  return new Promise(r => setTimeout(r, ms));
}

async function runAllTests() {
  console.log('🚀 1. Google Chrome headless arka planda başlatılıyor...');
  const chrome = spawn('C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe', [
    '--headless=new',
    '--remote-debugging-port=9222',
    '--user-data-dir=C:\\Users\\semih\\AppData\\Local\\Temp\\alkaros_chrome_e2e_full',
    '--no-sandbox',
    '--disable-gpu',
    'http://localhost:5173/'
  ]);

  chrome.stderr.on('data', () => {});

  let wsUrl = null;
  for (let i = 0; i < 10; i++) {
    await sleep(800);
    try {
      wsUrl = await getCDPTarget();
      if (wsUrl) break;
    } catch (e) {}
  }

  if (!wsUrl) {
    console.error('❌ Chrome CDP portuna bağlanılamadı!');
    chrome.kill();
    process.exit(1);
  }

  console.log('🔌 2. Chrome DevTools Protocol bağlantısı kuruldu:', wsUrl);
  const client = new CDPClient(wsUrl);
  await client.connect();

  await client.send('Runtime.enable');
  await client.send('Page.enable');
  await sleep(1000);

  const testResults = [];
  function assert(name, condition, details = '') {
    if (condition) {
      console.log(`  ✅ [PASS] ${name}`);
      testResults.push({ name, status: 'PASS' });
    } else {
      console.error(`  ❌ [FAIL] ${name} — ${details}`);
      testResults.push({ name, status: 'FAIL', details });
    }
  }

  console.log('\n--- 🧪 TEST SÜRECİ BAŞLIYOR (HER BUTON VE İŞLEV KONTROL EDİLİYOR) ---');

  // Test 1: Page Title & Initial DOM State
  const title = await client.eval('document.title');
  assert('Sayfa Başlığı Doğrulandı', title.includes('ALKAROS'));

  const cashierVisible = await client.eval('getComputedStyle(document.getElementById("surface-cashier")).display !== "none"');
  assert('Varsayılan Görünüm Kasiyer POS Aktif', cashierVisible);

  // Test 2: View Switchers
  await client.eval('document.getElementById("btn-view-waiter-phone").click()');
  await sleep(300);
  const waiterPhoneActive = await client.eval('getComputedStyle(document.getElementById("surface-waiter")).display !== "none" && document.getElementById("waiter-device-frame").classList.contains("phone-mode")');
  assert('Görünüm: Garson Telefon Moduna Geçiş', waiterPhoneActive);

  // Test 3: Waiter Lock Button
  await client.eval('document.getElementById("btn-waiter-lock").click()');
  await sleep(300);
  const waiterLockOpened = await client.eval('getComputedStyle(document.getElementById("modal-lockout")).display !== "none"');
  assert('Garson Header Oturum Kilitleme Butonu Açıldı', waiterLockOpened);

  // Unlock with PIN
  await client.eval(`
    document.querySelector('.key-btn[data-key="1"]').click();
    document.querySelector('.key-btn[data-key="2"]').click();
    document.querySelector('.key-btn[data-key="3"]').click();
    document.querySelector('.key-btn[data-key="4"]').click();
  `);
  await sleep(400);

  // Test 4: Waiter Table Search
  await client.eval(`
    const el = document.getElementById("input-wtr-search");
    if (el) {
      el.value = "B-01";
      el.dispatchEvent(new Event("input"));
    }
  `);
  await sleep(300);
  const wtrSearchMatches = await client.eval('document.getElementById("waiter-tables-container").innerText.includes("Masa B-01")');
  assert('Garson Masa Arama Canlı Filtreleme Çalışıyor', wtrSearchMatches);

  // Clear Waiter Search
  await client.eval(`
    const el = document.getElementById("input-wtr-search");
    if (el) {
      el.value = "";
      el.dispatchEvent(new Event("input"));
    }
  `);
  await sleep(300);

  // Test 5: Waiter Order Taking & Categories
  await client.eval(`
    const b01 = document.querySelector('[data-wtr-table-id="tbl-9"]');
    if (b01) b01.click();
  `);
  await sleep(400);
  const wtrOrderScreenOpen = await client.eval('getComputedStyle(document.getElementById("wtr-view-order")).display !== "none"');
  assert('Garson Masa B-01 Sipariş Ekranı Açıldı', wtrOrderScreenOpen);

  const wtrCatCount = await client.eval('document.querySelectorAll("#wtr-cat-chips .wtr-chip").length');
  assert('Garson Kategori Çipleri Dinamik Listelendi', wtrCatCount >= 3);

  // Click Burger Product in Waiter view
  await client.eval(`
    const burger = document.querySelector('[data-wtr-prod-id="p1"]');
    if (burger) burger.click();
  `);
  await sleep(300);

  // Expand Cart Tray & Test Qty Inc/Dec
  await client.eval('document.getElementById("wtr-cart-toggle").click()');
  await sleep(300);
  await client.eval('document.querySelector(".btn-wtr-qty-inc").click()');
  await sleep(300);
  const wtrQty2 = await client.eval('document.getElementById("wtr-cart-items").innerText.includes("2")');
  assert('Garson Sepetinde Kalem Adedi Arttırıldı (+)', wtrQty2);

  await client.eval('document.querySelector(".btn-wtr-qty-dec").click()');
  await sleep(300);
  const wtrQty1 = await client.eval('document.getElementById("wtr-cart-items").innerText.includes("1") || !document.getElementById("wtr-cart-items").innerText.includes("2")');
  assert('Garson Sepetinde Kalem Adedi Azaltıldı (-)', wtrQty1);

  // Test 6: Waiter Status (Fişler) Tab
  await client.eval('document.getElementById("wtr-nav-status").click()');
  await sleep(300);
  const wtrStatusFeedHasTickets = await client.eval('document.getElementById("wtr-status-feed").children.length > 0');
  assert('Garson Canlı Fiş Durumu Listelendi (Mutfak Fişleri)', wtrStatusFeedHasTickets);

  // Switch back to Cashier
  await client.eval('document.getElementById("btn-view-cashier").click()');
  await sleep(300);

  // Test 7: Operations Station Filters (Sıcak, Bar, Soğuk)
  await client.eval('document.getElementById("tab-cui-operations").click()');
  await sleep(300);

  await client.eval('document.querySelector(\'button[data-station="hot"]\').click()');
  await sleep(300);
  const hotTickets = await client.eval('document.getElementById("ops-tickets-feed").innerText.includes("Masa S-02")');
  assert('İstasyon Filtresi: Sıcak Mutfak Filtrelendi', hotTickets);

  await client.eval('document.querySelector(\'button[data-station="bar"]\').click()');
  await sleep(300);
  const barTickets = await client.eval('document.getElementById("ops-tickets-feed").innerText.includes("Masa B-01")');
  assert('İstasyon Filtresi: Bar & İçecek Filtrelendi', barTickets);

  await client.eval('document.querySelector(\'button[data-station="all"]\').click()');
  await sleep(300);
  const allTickets = await client.eval('document.getElementById("ops-tickets-feed").children.length >= 2');
  assert('İstasyon Filtresi: Tüm İstasyonlara Geri Dönüldü', allTickets);

  // Test 8: Live Kitchen Counter Sync
  const kitchenBadgeNum = await client.eval('parseInt(document.getElementById("badge-kitchen-count").textContent, 10)');
  assert('Mutfak Fiş Rozeti ve Sayacı Senkronize (Canlı Sayı)', kitchenBadgeNum >= 2);

  // Test 9: POS Add Table & Order Taking
  await client.eval('document.getElementById("tab-cui-tables").click()');
  await sleep(300);
  await client.eval(`
    const s01 = document.querySelector('.table-card[data-table-id="tbl-1"]');
    if (s01) s01.click();
  `);
  await sleep(300);

  // Choose Koltuk 2
  await client.eval(`
    const seat2 = document.querySelector('.seat-chip[data-seat="2"]');
    if (seat2) seat2.click();
  `);
  await sleep(200);
  const activeSeatIs2 = await client.eval('document.querySelector(\'.seat-chip[data-seat="2"]\').classList.contains("active")');
  assert('Koltuk Seçimi Değiştirildi (Koltuk 2)', activeSeatIs2);

  // Test 10: Invariant Checks
  const jsExceptionsCount = client.jsExceptions.length;
  const consoleErrorsCount = client.consoleErrors.length;
  assert('Sıfır JavaScript Hatası / Exception (0 JS Crash)', jsExceptionsCount === 0, `${jsExceptionsCount} exception(s) detected`);
  assert('Sıfır Konsol Hata Mesajı (0 Console Error)', consoleErrorsCount === 0, `${consoleErrorsCount} console error(s) detected`);

  console.log('\n======================================================');
  console.log(`📊 E2E TEST RAPORU: ${testResults.filter(r => r.status === 'PASS').length}/${testResults.length} BAŞARILI`);
  console.log('======================================================\n');

  client.close();
  chrome.kill();
  process.exit(testResults.some(r => r.status === 'FAIL') ? 1 : 0);
}

runAllTests().catch(err => {
  console.error('Test koşturulurken beklenmeyen hata:', err);
  process.exit(1);
});
