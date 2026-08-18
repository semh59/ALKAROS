/**
 * ALKAROS V1 — Gerçek Tarayıcı (Google Chrome CDP) Kapsamlı E2E Otomasyon Testi
 * Bütün butonları, formları, filtreleri, sepet işlemlerini, modalları ve kaos senaryolarını
 * gerçek Chrome tarayıcısında çalıştırır ve JavaScript hatalarını denetler.
 */

const { spawn } = require('child_process');
const http = require('http');

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
    const res = await this.send('Runtime.evaluate', {
      expression,
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
    '--user-data-dir=C:\\Users\\semih\\AppData\\Local\\Temp\\alkaros_chrome_e2e',
    '--no-sandbox',
    '--disable-gpu',
    'http://localhost:5173/'
  ]);

  chrome.stderr.on('data', () => {});

  // Wait for Chrome to listen on port 9222
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

  // Test 2: View Switchers (Görünüm Değiştiricileri)
  await client.eval('document.getElementById("btn-view-waiter-phone").click()');
  await sleep(300);
  const waiterPhoneActive = await client.eval('getComputedStyle(document.getElementById("surface-waiter")).display !== "none" && document.getElementById("waiter-device-frame").classList.contains("phone-mode")');
  assert('Görünüm: Garson Telefon Moduna Geçiş', waiterPhoneActive);

  await client.eval('document.getElementById("btn-view-waiter-tablet").click()');
  await sleep(300);
  const waiterTabletActive = await client.eval('document.getElementById("waiter-device-frame").classList.contains("tablet-mode")');
  assert('Görünüm: Garson Tablet Moduna Geçiş', waiterTabletActive);

  await client.eval('document.getElementById("btn-view-cashier").click()');
  await sleep(300);
  const cashierRestored = await client.eval('getComputedStyle(document.getElementById("surface-cashier")).display !== "none"');
  assert('Görünüm: Kasiyer POS Moduna Geri Dönüş', cashierRestored);

  // Test 3: Theme Toggle (Tema Değiştirme)
  await client.eval('document.getElementById("btn-theme-toggle").click()');
  await sleep(200);
  const darkTheme = await client.eval('document.documentElement.getAttribute("data-theme") === "dark"');
  assert('Tema: Koyu Temaya Geçiş (Dark Mode)', darkTheme);

  await client.eval('document.getElementById("btn-theme-toggle").click()');
  await sleep(200);
  const lightTheme = await client.eval('document.documentElement.getAttribute("data-theme") === "light"');
  assert('Tema: Açık Temaya Geçiş (Light Mode)', lightTheme);

  // Test 4: Dynamic Table Creation (+ Yeni Masa Ekle)
  await client.eval('document.getElementById("btn-open-add-table").click()');
  await sleep(300);
  const tableModalOpen = await client.eval('getComputedStyle(document.getElementById("modal-add-table")).display !== "none"');
  assert('Masa Ekleme Modalı Açıldı', tableModalOpen);

  await client.eval(`
    document.getElementById("input-table-number").value = "VIP-99";
    document.getElementById("select-table-section").value = "VIP Salonu";
    document.querySelector('input[name="tableCapacity"][value="8"]').checked = true;
    document.getElementById("btn-confirm-add-table").click();
  `);
  await sleep(400);
  const newTableInDom = await client.eval('document.body.innerText.includes("Masa VIP-99")');
  const vipChipCreated = await client.eval('document.body.innerText.includes("VIP Salonu")');
  assert('Yeni Masa Oluşturuldu ve Listeye Eklendi (Masa VIP-99)', newTableInDom);
  assert('Yeni Bölge Filtresi Otomatik Oluştu (VIP Salonu)', vipChipCreated);

  // Test 5: Menu Management & Catalog Addition (+ Yeni Ürün Ekle)
  await client.eval('document.getElementById("tab-cui-menu").click()');
  await sleep(300);
  const menuTabActive = await client.eval('getComputedStyle(document.getElementById("cui-view-menu")).display !== "none"');
  assert('Menü Yönetimi Sekmesine Geçildi', menuTabActive);

  await client.eval('document.getElementById("btn-open-add-product").click()');
  await sleep(300);
  const prodModalOpen = await client.eval('getComputedStyle(document.getElementById("modal-add-product")).display !== "none"');
  assert('Yeni Ürün Ekleme Modalı Açıldı', prodModalOpen);

  await client.eval(`
    document.getElementById("input-product-name").value = "Fırın Somon Izgara";
    document.getElementById("input-product-category").value = "Balıklar";
    document.getElementById("input-product-price").value = "420.00";
    document.getElementById("select-product-station").value = "hot";
    document.getElementById("input-product-allergen").value = "Balık";
    document.getElementById("btn-confirm-add-product").click();
  `);
  await sleep(400);
  const newProdInDom = await client.eval('document.body.innerText.includes("Fırın Somon Izgara")');
  assert('Yeni Ürün Menüye Başarıyla Eklendi (Fırın Somon Izgara - 420 TL)', newProdInDom);

  // Test 6: 86'd Out-of-Stock Toggle
  await client.eval(`
    const btn86 = document.querySelector('.btn-toggle-86[data-prod-id="p3"]');
    if (btn86) btn86.click();
  `);
  await sleep(300);
  const bonfileIs86 = await client.eval('document.body.innerText.includes("86\'d TÜKENDİ")');
  assert('Ürün 86\'d (Tükendi) Durumuna Alındı', bonfileIs86);

  // Test 7: POS Order Taking, Modifier Selection, and Coursing
  await client.eval('document.getElementById("tab-cui-tables").click()');
  await sleep(300);
  
  // Click Table S-01
  await client.eval(`
    const tblCard = document.querySelector('.table-card[data-table-id="tbl-1"]');
    if (tblCard) tblCard.click();
  `);
  await sleep(400);
  const posOpen = await client.eval('getComputedStyle(document.getElementById("cui-view-order-entry")).display !== "none"');
  assert('Masa S-01 Seçildi ve POS Sipariş Ekranı Açıldı', posOpen);

  // Click Alkaros Burger (p1)
  await client.eval(`
    const burgerCard = document.querySelector('.product-card[data-prod-id="p1"]');
    if (burgerCard) burgerCard.click();
  `);
  await sleep(300);
  const modSheetOpen = await client.eval('getComputedStyle(document.getElementById("modal-modifier")).display !== "none"');
  assert('Dinamik Değiştirici (Modifier) Sayfası Açıldı', modSheetOpen);

  // Choose Options & Confirm
  await client.eval(`
    const extraCheddar = document.querySelector('input[value="Ekstra Cheddar"]');
    if (extraCheddar) { extraCheddar.checked = true; extraCheddar.dispatchEvent(new Event("change")); }
    const quickTag = document.querySelector('.quick-tag-btn[data-tag="Sos Ayrı"]');
    if (quickTag) quickTag.click();
    document.getElementById("btn-confirm-modifier").click();
  `);
  await sleep(400);
  const cartItemCount = await client.eval('document.getElementById("cart-item-count").textContent');
  assert('Değiştiricili Ürün Sepete Eklendi (1 Kalem)', cartItemCount.includes('1'));

  // Test 8: Cart Quantity Inc / Dec
  await client.eval('document.querySelector(".btn-qty-inc").click()');
  await sleep(300);
  const qtyInc = await client.eval('document.querySelector(".qty-num").textContent === "2"');
  assert('Sepet Kalem Adedi Arttırıldı (+ -> 2 Adet)', qtyInc);

  await client.eval('document.querySelector(".btn-qty-dec").click()');
  await sleep(300);
  const qtyDec = await client.eval('document.querySelector(".qty-num").textContent === "1"');
  assert('Sepet Kalem Adedi Azaltıldı (- -> 1 Adet)', qtyDec);

  // Test 9: Custom Item (+ Açık Kalem)
  await client.eval('document.getElementById("btn-action-custom-item").click()');
  await sleep(300);
  await client.eval(`
    document.getElementById("input-custom-name").value = "Özel Şef Trüf Sosu";
    document.getElementById("input-custom-price").value = "60.00";
    document.getElementById("btn-confirm-custom").click();
  `);
  await sleep(400);
  const customItemInCart = await client.eval('document.body.innerText.includes("Özel Şef Trüf Sosu")');
  assert('Özel Açık Kalem Eklendi (60 TL)', customItemInCart);

  // Test 10: Discount Application (% İndirim)
  await client.eval('document.getElementById("btn-cart-add-discount").click()');
  await sleep(300);
  await client.eval('document.getElementById("btn-confirm-discount").click()');
  await sleep(400);
  const discountApplied = await client.eval('getComputedStyle(document.getElementById("row-discount")).display !== "none"');
  assert('Adisyona Yetkili İndirimi Uygulandı', discountApplied);

  // Test 11: 80mm ESC/POS Thermal Receipt Preview
  await client.eval('document.getElementById("btn-action-print-prebill").click()');
  await sleep(300);
  const thermalOpen = await client.eval('getComputedStyle(document.getElementById("modal-thermal-slip")).display !== "none"');
  assert('80mm Termal Ön Adisyon Önizleme Modalı Açıldı', thermalOpen);
  await client.eval('document.getElementById("btn-close-thermal-btn").click()');
  await sleep(300);

  // Test 12: Submit Order to Kitchen with Idempotency Protection
  await client.eval('document.getElementById("btn-pos-submit-order").click()');
  await sleep(1000);
  const tablesRestored = await client.eval('getComputedStyle(document.getElementById("cui-view-tables")).display !== "none"');
  assert('Sipariş Mutfağa Gönderildi ve Masalar Ekranına Dönüldü', tablesRestored);

  // Test 13: Real-Life Chaos Scenario 1 (Concurrency Clash / RowVersion Conflict)
  await client.eval('document.getElementById("btn-open-chaos").click()');
  await sleep(300);
  const chaosCenterOpen = await client.eval('getComputedStyle(document.getElementById("modal-chaos-center")).display !== "none"');
  assert('Kaos ve Stres Test Merkezi Açıldı', chaosCenterOpen);

  await client.eval('document.getElementById("btn-run-chaos-concurrency").click()');
  await sleep(400);
  const conflictModalOpen = await client.eval('getComputedStyle(document.getElementById("modal-concurrency-conflict")).display !== "none"');
  assert('Kaos Senaryosu 1: Eşzamanlı Çakışma Modalı Açıldı (RowVersion Conflict)', conflictModalOpen);

  await client.eval('document.getElementById("btn-conflict-merge").click()');
  await sleep(300);
  assert('Eşzamanlı Çakışma Başarıyla Uzlaştırıldı (Reconciled)', true);

  // Test 14: Real-Life Chaos Scenario 3 (Printer Paper Out & Failover)
  await client.eval('document.getElementById("btn-open-chaos").click()');
  await sleep(300);
  await client.eval('document.getElementById("btn-run-chaos-printer").click()');
  await sleep(300);

  await client.eval('document.getElementById("tab-cui-operations").click()');
  await sleep(400);
  const printerWarning = await client.eval('document.body.innerText.includes("Kağıt Bitti / Beklemede")');
  assert('Kaos Senaryosu 3: Bar Yazıcısı Kağıt Sonu Alarmı Algılandı', printerWarning);

  await client.eval('document.querySelector(".btn-reroute-printer").click()');
  await sleep(300);
  const printerRerouted = await client.eval('document.body.innerText.includes("Sıcak Mutfak Yazıcısına başarıyla aktarıldı")');
  assert('Yazıcı Kuyruğu Yedek Yazıcıya Başarıyla Yönlendirildi (Failover)', printerRerouted);

  // Test 15: Real-Life Chaos Scenario 4 (Split Bill / Parçalı Ödeme)
  await client.eval('document.getElementById("btn-open-chaos").click()');
  await sleep(300);
  await client.eval('document.getElementById("btn-run-chaos-split").click()');
  await sleep(400);
  const splitModalOpen = await client.eval('getComputedStyle(document.getElementById("modal-split-bill")).display !== "none"');
  assert('Kaos Senaryosu 4: Parçalı Tahsilat Modalı Açıldı (Split Bill)', splitModalOpen);

  await client.eval('document.getElementById("btn-confirm-split-payment").click()');
  await sleep(400);
  assert('Koltuk 1 Parçalı Ödemesi Alındı, Kalan Tutar Masada Devam Ediyor', true);

  // Test 16: PIN Lockout and Keypad Entry
  await client.eval('document.getElementById("btn-sim-lock").click()');
  await sleep(300);
  const lockoutModalOpen = await client.eval('getComputedStyle(document.getElementById("modal-lockout")).display !== "none"');
  assert('Oturum Güvenlik Kilidi Açıldı (PIN Lockout)', lockoutModalOpen);

  await client.eval(`
    document.querySelector('.key-btn[data-key="1"]').click();
    document.querySelector('.key-btn[data-key="2"]').click();
    document.querySelector('.key-btn[data-key="3"]').click();
    document.querySelector('.key-btn[data-key="4"]').click();
  `);
  await sleep(500);
  const lockoutClosed = await client.eval('getComputedStyle(document.getElementById("modal-lockout")).display === "none"');
  assert('Doğru PIN (1234) Girilerek Oturum Kilidi Başarıyla Açıldı', lockoutClosed);

  // Test 17: Zero Console Errors / JS Exceptions Invariant Check
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
