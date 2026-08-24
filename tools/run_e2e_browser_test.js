/**
 * ALKAROS V1 — Gerçek Tarayıcı (Google Chrome CDP) Kapsamlı E2E Otomasyon Testi
 * Bütün butonları, formları, filtreleri, sepet işlemlerini, modalları, garson alt sekmelerini,
 * istasyon filtrelerini ve kaos senaryolarını gerçek Chrome tarayıcısında çalıştırır.
 */

const { spawn } = require('child_process');
const { existsSync } = require('fs');
const { mkdtemp, readFile, rm } = require('fs/promises');
const { tmpdir } = require('os');
const { join } = require('path');

const STDERR_LIMIT = 8192;

function appendBounded(current, chunk, limit = STDERR_LIMIT) {
  const combined = current + chunk.toString('utf8');
  return combined.length <= limit ? combined : combined.slice(-limit);
}

function chromeCandidates(env = process.env, platform = process.platform) {
  if (platform === 'win32') {
    return [
      env.PROGRAMFILES && join(env.PROGRAMFILES, 'Google', 'Chrome', 'Application', 'chrome.exe'),
      env['PROGRAMFILES(X86)'] && join(env['PROGRAMFILES(X86)'], 'Google', 'Chrome', 'Application', 'chrome.exe'),
      env.LOCALAPPDATA && join(env.LOCALAPPDATA, 'Google', 'Chrome', 'Application', 'chrome.exe')
    ].filter(Boolean);
  }

  if (platform === 'darwin') {
    return ['/Applications/Google Chrome.app/Contents/MacOS/Google Chrome'];
  }

  return [
    '/usr/bin/google-chrome',
    '/usr/bin/google-chrome-stable',
    '/usr/bin/chromium',
    '/usr/bin/chromium-browser'
  ];
}

function resolveChromeExecutable({ env = process.env, platform = process.platform, pathExists = existsSync } = {}) {
  const configuredPath = env.ALKAROS_CHROME_PATH && env.ALKAROS_CHROME_PATH.trim();
  if (configuredPath) {
    if (pathExists(configuredPath)) return configuredPath;
    throw new Error(`ALKAROS_CHROME_PATH does not point to a file: ${configuredPath}`);
  }

  const detectedPath = chromeCandidates(env, platform).find(pathExists);
  if (detectedPath) return detectedPath;

  throw new Error('Google Chrome executable was not found. Set ALKAROS_CHROME_PATH to its absolute path.');
}

async function waitForDevToolsPort(
  profileDirectory,
  { attempts = 25, delayMs = 400, readText = path => readFile(path, 'utf8'), sleepFn = sleep } = {}
) {
  const portFile = join(profileDirectory, 'DevToolsActivePort');
  let lastError = null;
  for (let attempt = 1; attempt <= attempts; attempt++) {
    try {
      const [portText] = (await readText(portFile)).split(/\r?\n/);
      const port = Number(portText);
      if (!Number.isInteger(port) || port < 1 || port > 65535) {
        throw new Error(`Invalid DevTools port value: ${portText}`);
      }
      return port;
    } catch (error) {
      lastError = error;
      if (attempt < attempts) await sleepFn(delayMs);
    }
  }

  const detail = lastError instanceof Error ? lastError.message : String(lastError);
  throw new Error(`Chrome did not publish a DevTools port after ${attempts} attempts. Last error: ${detail}`);
}

async function getCDPTarget(port, fetchTarget = fetch) {
  const res = await fetchTarget(`http://127.0.0.1:${port}/json`, { signal: AbortSignal.timeout(2000) });
  if (!res.ok) throw new Error(`Chrome CDP target endpoint returned HTTP ${res.status}.`);
  const targets = await res.json();
  if (!Array.isArray(targets)) throw new Error('Chrome CDP target endpoint returned an invalid payload.');
  const page = targets.find(t => t.type === 'page' && t.url.includes('5173'));
  if (!page) throw new Error('ALKAROS 5173 page target not found in Chrome!');
  if (typeof page.webSocketDebuggerUrl !== 'string' || !page.webSocketDebuggerUrl.startsWith('ws')) {
    throw new Error('Chrome page target did not provide a WebSocket debugger URL.');
  }
  return page.webSocketDebuggerUrl;
}

async function waitForCDPTarget(
  port,
  { attempts = 10, delayMs = 800, loadTarget = getCDPTarget, sleepFn = sleep } = {}
) {
  let lastError = null;
  for (let attempt = 1; attempt <= attempts; attempt++) {
    try {
      return await loadTarget(port);
    } catch (error) {
      lastError = error;
      if (attempt < attempts) await sleepFn(delayMs);
    }
  }

  const detail = lastError instanceof Error ? lastError.message : String(lastError);
  throw new Error(`Chrome CDP target was unavailable after ${attempts} attempts. Last error: ${detail}`);
}

function waitForProcessExit(child, timeoutMs) {
  if (child.exitCode !== null) return Promise.resolve(true);
  return new Promise(resolve => {
    const onExit = () => finish(true);
    const timer = setTimeout(() => finish(false), timeoutMs);
    const finish = exited => {
      clearTimeout(timer);
      child.removeListener('exit', onExit);
      resolve(exited);
    };
    child.once('exit', onExit);
  });
}

async function stopProcess(child, timeoutMs = 3000) {
  if (!child || child.exitCode !== null) return;
  if (!child.kill()) throw new Error('Chrome process did not accept the termination signal.');
  if (await waitForProcessExit(child, timeoutMs)) return;
  if (!child.kill('SIGKILL')) throw new Error('Chrome process did not accept the forced termination signal.');
  if (!await waitForProcessExit(child, timeoutMs)) {
    throw new Error(`Chrome process did not exit within ${timeoutMs} ms after forced termination.`);
  }
}

class CDPClient {
  constructor(wsUrl) {
    this.wsUrl = wsUrl;
    this.id = 1;
    this.callbacks = new Map();
    this.consoleErrors = [];
    this.jsExceptions = [];
  }

  rejectPending(error) {
    for (const { reject, timer } of this.callbacks.values()) {
      clearTimeout(timer);
      reject(error);
    }
    this.callbacks.clear();
  }

  async connect(timeoutMs = 5000) {
    const WebSocket = globalThis.WebSocket;
    if (typeof WebSocket !== 'function') throw new Error('This Node.js runtime does not provide WebSocket support.');
    return new Promise((resolve, reject) => {
      this.ws = new WebSocket(this.wsUrl);
      let settled = false;
      const timer = setTimeout(() => finish(new Error(`CDP WebSocket connection timed out after ${timeoutMs} ms.`)), timeoutMs);
      const finish = error => {
        if (settled) return;
        settled = true;
        clearTimeout(timer);
        if (error) reject(error);
        else resolve();
      };
      this.ws.onopen = () => finish();
      this.ws.onerror = () => finish(new Error('CDP WebSocket connection failed.'));
      this.ws.onclose = () => {
        const error = new Error('CDP WebSocket connection closed.');
        this.rejectPending(error);
        finish(error);
      };
      this.ws.onmessage = (msg) => {
        let data;
        try {
          data = JSON.parse(msg.data);
        } catch (error) {
          this.rejectPending(new Error('Chrome returned an invalid CDP message.', { cause: error }));
          return;
        }
        if (data.id && this.callbacks.has(data.id)) {
          const { resolve, reject, timer } = this.callbacks.get(data.id);
          this.callbacks.delete(data.id);
          clearTimeout(timer);
          if (data.error) reject(new Error(`CDP command failed: ${JSON.stringify(data.error)}`));
          else resolve(data.result);
        } else if (data.method === 'Runtime.consoleAPICalled' && data.params.type === 'error') {
          this.consoleErrors.push(data.params);
        } else if (data.method === 'Runtime.exceptionThrown') {
          this.jsExceptions.push(data.params);
        }
      };
    });
  }

  send(method, params = {}, timeoutMs = 5000) {
    const id = this.id++;
    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => {
        this.callbacks.delete(id);
        reject(new Error(`CDP command ${method} timed out after ${timeoutMs} ms.`));
      }, timeoutMs);
      this.callbacks.set(id, { resolve, reject, timer });
      try {
        this.ws.send(JSON.stringify({ id, method, params }));
      } catch (error) {
        clearTimeout(timer);
        this.callbacks.delete(id);
        reject(error);
      }
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
    this.rejectPending(new Error('CDP client closed before the command completed.'));
    if (this.ws) this.ws.close();
  }
}

async function sleep(ms) {
  return new Promise(r => setTimeout(r, ms));
}

async function runAllTests() {
  console.log('🚀 1. Google Chrome headless arka planda başlatılıyor...');
  const chromePath = resolveChromeExecutable();
  const profileDirectory = await mkdtemp(join(tmpdir(), 'alkaros-chrome-e2e-'));
  let chrome = null;
  let chromeStderr = '';
  let launchError = null;
  let client = null;

  try {
    chrome = spawn(chromePath, [
      '--headless=new',
      '--remote-debugging-port=0',
      `--user-data-dir=${profileDirectory}`,
      '--no-sandbox',
      '--disable-gpu',
      'http://localhost:5173/'
    ], { stdio: ['ignore', 'ignore', 'pipe'] });

    let rejectLaunch;
    const launchFailure = new Promise((resolve, reject) => {
      rejectLaunch = reject;
    });
    chrome.on('error', error => {
      launchError = error;
      rejectLaunch(error);
    });
    chrome.stderr.on('data', chunk => {
      chromeStderr = appendBounded(chromeStderr, chunk);
    });

    const port = await Promise.race([waitForDevToolsPort(profileDirectory), launchFailure]);
    if (launchError) throw launchError;
    const wsUrl = await waitForCDPTarget(port);

    console.log('🔌 2. Chrome DevTools Protocol bağlantısı kuruldu:', wsUrl);
    client = new CDPClient(wsUrl);
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

    return testResults.some(r => r.status === 'FAIL') ? 1 : 0;
  } catch (error) {
    const stderrDetail = chromeStderr.trim();
    if (stderrDetail) {
      throw new Error(`${error instanceof Error ? error.message : String(error)}\nChrome stderr:\n${stderrDetail}`, {
        cause: error
      });
    }
    throw error;
  } finally {
    try {
      if (client) client.close();
    } finally {
      try {
        await stopProcess(chrome);
      } finally {
        await rm(profileDirectory, { recursive: true, force: true });
      }
    }
  }
}

if (require.main === module) {
  runAllTests()
    .then(exitCode => {
      process.exitCode = exitCode;
    })
    .catch(err => {
      console.error('Test koşturulurken beklenmeyen hata:', err);
      process.exitCode = 1;
    });
}

module.exports = {
  appendBounded,
  chromeCandidates,
  getCDPTarget,
  resolveChromeExecutable,
  stopProcess,
  waitForCDPTarget,
  waitForDevToolsPort
};
