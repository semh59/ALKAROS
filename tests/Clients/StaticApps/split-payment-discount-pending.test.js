import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import { loadApp } from "./support/loadApp.js";
import { installFetchRouter } from "./support/fetchRouter.js";

const here = dirname(fileURLToPath(import.meta.url));
const pageDir = resolve(here, "..", "..", "..", "src/Clients/Cashier/wwwroot/payments/split-payment");
const htmlPath = resolve(pageDir, "index.html");
const scriptPath = resolve(pageDir, "split-payment.js");

const TERMINAL_ID = "11111111-1111-1111-1111-111111111111";
const BILL_ID = "22222222-2222-2222-2222-222222222222";

const unpaid = { billId: BILL_ID, payableAmount: 100, allocatedTotal: 0, remainingAmount: 100, allocations: [], unsettledPayment: null };
const applied = { status: "Applied", billId: BILL_ID, adjustmentId: "a1", summary: {}, grantId: null };
const pending = { status: "Pending", billId: BILL_ID, adjustmentId: null, summary: null, grantId: "g1" };

// Each element answers one POST .../discount in order.
let approveDelayMs = 0;
async function open(discountAnswers, approveAnswers = []) {
  const answers = [...discountAnswers];
  const approvals = [...approveAnswers];
  const routeSet = [
    { test: (url) => url.endsWith("/api/v1/auth/session/current"), body: { terminalId: TERMINAL_ID, userId: "u1" } },
    { test: (url, init) => url.endsWith("/tenders/") && (init?.method ?? "GET") === "GET", body: unpaid },
    { test: (url) => url.includes("/cash-sessions/active"), body: { cashSessionId: "s1" } },
    { test: (url) => url.endsWith("/adjustments"), body: { adjustments: [], summary: null } },
    { test: (url) => url.endsWith("/refund-requests"), body: [] },
    {
      test: (url, init) => url.endsWith("/approve") && init?.method === "POST",
      delayMs: approveDelayMs,
      get status() { return approvals[0].status; },
      get body() { return approvals.shift().body; },
    },
    {
      test: (url, init) => url.endsWith("/discount") && init?.method === "POST",
      get status() { return answers[0].status; },
      get body() { return answers.shift().body; },
    },
  ];
  window.history.replaceState({}, "", `/?billId=${BILL_ID}`);
  const fetchMock = installFetchRouter(routeSet);
  await loadApp(htmlPath, scriptPath);
  await vi.waitFor(() => expect(document.getElementById("submit-discount")).not.toBeNull());
  return fetchMock;
}

const discountCalls = (fetchMock) => fetchMock.mock.calls
  .filter(([url, init]) => url.endsWith("/discount") && init?.method === "POST")
  .map(([, init]) => JSON.parse(init.body));
const type = (id, value) => {
  const input = document.getElementById(id);
  input.value = value;
  input.dispatchEvent(new Event("input", { bubbles: true }));
};
const submit = () => document.getElementById("submit-discount");
const text = () => document.getElementById("app").textContent;
// The submit button is disabled from the click until the answer is handled, so this waits for the answer without a sleep.
const answered = () => vi.waitFor(() => expect(submit().disabled).toBe(false));
// Lets the first `free` discount requests through at once and holds the rest until the returned release() is called,
// so a test decides when the server answers instead of racing a timer.
function holdDiscountAnswers(fetchMock, free) {
  const inner = fetchMock.getMockImplementation();
  let release;
  const gate = new Promise((resolve) => { release = resolve; });
  let seen = 0;
  fetchMock.mockImplementation(async (url, init) => {
    if (String(url).endsWith("/discount") && init?.method === "POST" && ++seen > free) await gate;
    return inner(url, init);
  });
  return release;
}

describe("split-payment.js discount waiting for a manager", () => {
  beforeEach(() => vi.resetModules());
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  it("keeps the request and its key while a manager decides and applies the discount when the check finds it approved", async () => {
    const fetchMock = await open([{ status: 202, body: pending }, { status: 202, body: pending }, { status: 200, body: applied }]);

    type("discount-value", "20");
    submit().click();
    await vi.waitFor(() => expect(text()).toContain("yönetici onayına gönderildi"));
    expect(submit().textContent).toBe("Onay durumunu kontrol et");
    expect(document.getElementById("discount-value").value).toBe("20");

    submit().click();
    await vi.waitFor(() => expect(discountCalls(fetchMock)).toHaveLength(2));
    await answered();
    submit().click();
    await vi.waitFor(() => expect(text()).toContain("İndirim uygulandı ve kaydedildi."));

    const calls = discountCalls(fetchMock);
    expect(calls).toHaveLength(3);
    expect(new Set(calls.map((c) => c.IdempotencyKey)).size).toBe(1);
    expect(new Set(calls.map((c) => c.Value)).size).toBe(1);
  });

  it("checks a waiting discount by itself every few seconds with the same key and stops once it is applied", async () => {
    vi.useFakeTimers({ toFake: ["setInterval", "clearInterval"] });
    try {
      const fetchMock = await open([{ status: 202, body: pending }, { status: 202, body: pending }, { status: 200, body: applied }]);
      type("discount-value", "20");
      submit().click();
      await vi.waitFor(() => expect(text()).toContain("yönetici onayına gönderildi"));

      await vi.advanceTimersByTimeAsync(5000);
      await vi.waitFor(() => expect(discountCalls(fetchMock)).toHaveLength(2));
      await vi.waitFor(() => expect(text()).toContain("yönetici onayına gönderildi"));
      await vi.advanceTimersByTimeAsync(5000);
      await vi.waitFor(() => expect(text()).toContain("İndirim uygulandı ve kaydedildi."));
      await vi.advanceTimersByTimeAsync(20000);

      const calls = discountCalls(fetchMock);
      expect(calls).toHaveLength(3);
      expect(new Set(calls.map((c) => c.IdempotencyKey)).size).toBe(1);
    } finally {
      vi.useRealTimers();
    }
  });

  it("sends no second check while the previous one is still unanswered", async () => {
    vi.useFakeTimers({ toFake: ["setInterval", "clearInterval"] });
    try {
      const fetchMock = await open([{ status: 202, body: pending }, { status: 202, body: pending }]);
      const release = holdDiscountAnswers(fetchMock, 1);
      type("discount-value", "20");
      submit().click();
      await vi.waitFor(() => expect(text()).toContain("yönetici onayına gönderildi"));

      await vi.advanceTimersByTimeAsync(5000);
      await vi.waitFor(() => expect(discountCalls(fetchMock)).toHaveLength(2));
      await vi.advanceTimersByTimeAsync(10000);

      expect(discountCalls(fetchMock)).toHaveLength(2);
      release();
    } finally {
      vi.useRealTimers();
    }
  });

  it("checks silently while the manager has not decided or the server fails: nothing is redrawn and no error shows", async () => {
    vi.useFakeTimers({ toFake: ["setInterval", "clearInterval"] });
    try {
      const fetchMock = await open([{ status: 202, body: pending }, { status: 202, body: pending }, { status: 503, body: {} }, { status: 202, body: pending }]);
      type("discount-value", "20");
      submit().click();
      await vi.waitFor(() => expect(text()).toContain("yönetici onayına gönderildi"));
      const input = document.getElementById("discount-value");

      for (let tick = 1; tick <= 3; tick += 1) {
        await vi.advanceTimersByTimeAsync(5000);
        await vi.waitFor(() => expect(discountCalls(fetchMock)).toHaveLength(tick + 1));
      }

      expect(document.getElementById("discount-value")).toBe(input);
      expect(submit().textContent).toBe("Onay durumunu kontrol et");
      expect(text()).toContain("yönetici onayına gönderildi");
      expect(text()).not.toContain("Bağlantı");
    } finally {
      vi.useRealTimers();
    }
  });

  it("does not check while the tab is hidden and resumes when it is shown", async () => {
    vi.useFakeTimers({ toFake: ["setInterval", "clearInterval"] });
    let hidden = true;
    Object.defineProperty(document, "hidden", { configurable: true, get: () => hidden });
    try {
      const fetchMock = await open([{ status: 202, body: pending }, { status: 202, body: pending }]);
      type("discount-value", "20");
      submit().click();
      await vi.waitFor(() => expect(text()).toContain("yönetici onayına gönderildi"));

      await vi.advanceTimersByTimeAsync(20000);
      expect(discountCalls(fetchMock)).toHaveLength(1);

      hidden = false;
      await vi.advanceTimersByTimeAsync(5000);
      await vi.waitFor(() => expect(discountCalls(fetchMock)).toHaveLength(2));
    } finally {
      delete document.hidden;
      vi.useRealTimers();
    }
  });

  it("stops checking by itself once the waiting discount is refused", async () => {
    vi.useFakeTimers({ toFake: ["setInterval", "clearInterval"] });
    try {
      const fetchMock = await open([{ status: 202, body: pending }, { status: 403, body: { error: { code: "GRANT_DENIED", message: "x" } } }]);
      type("discount-value", "20");
      submit().click();
      await vi.waitFor(() => expect(text()).toContain("yönetici onayına gönderildi"));

      await vi.advanceTimersByTimeAsync(5000);
      await vi.waitFor(() => expect(text()).toContain("reddedildi"));
      await vi.advanceTimersByTimeAsync(20000);

      expect(discountCalls(fetchMock)).toHaveLength(2);
    } finally {
      vi.useRealTimers();
    }
  });

  it("does not check by itself once the waiting form was edited", async () => {
    vi.useFakeTimers({ toFake: ["setInterval", "clearInterval"] });
    try {
      const fetchMock = await open([{ status: 202, body: pending }]);
      type("discount-value", "20");
      submit().click();
      await vi.waitFor(() => expect(text()).toContain("yönetici onayına gönderildi"));
      type("discount-value", "25");

      await vi.advanceTimersByTimeAsync(20000);

      expect(discountCalls(fetchMock)).toHaveLength(1);
    } finally {
      vi.useRealTimers();
    }
  });

  it("checks a waiting full-bill discount without asking for the confirmation again", async () => {
    const fetchMock = await open([{ status: 202, body: pending }, { status: 200, body: applied }]);

    type("discount-value", "100");
    submit().click();
    expect(discountCalls(fetchMock)).toHaveLength(0);
    submit().click();
    await vi.waitFor(() => expect(text()).toContain("yönetici onayına gönderildi"));
    submit().click();
    await vi.waitFor(() => expect(discountCalls(fetchMock)).toHaveLength(2));
  });

  it("asks for the full-bill confirmation when a waiting discount is edited to take the whole bill", async () => {
    const fetchMock = await open([{ status: 202, body: pending }]);

    type("discount-value", "20");
    submit().click();
    await vi.waitFor(() => expect(text()).toContain("yönetici onayına gönderildi"));
    type("discount-value", "100");
    submit().click();

    expect(discountCalls(fetchMock)).toHaveLength(1);
    expect(text()).toContain("kapatır");
  });

  it("ignores a waiting answer for a form that was edited while the request was in flight", async () => {
    const fetchMock = await open([{ status: 202, body: pending }]);
    const release = holdDiscountAnswers(fetchMock, 0);

    type("discount-value", "20");
    submit().click();
    type("discount-value", "100");
    release();
    await answered();

    expect(text()).not.toContain("yönetici onayına gönderildi");
    expect(text()).not.toContain("İndirim uygulandı");
    expect(submit().textContent).toBe("İndirim uygula");
    submit().click();
    expect(discountCalls(fetchMock)).toHaveLength(1);
    expect(text()).toContain("kapatır");
  });

  it("keeps what was typed while the request was in flight when the discount is applied", async () => {
    const release = holdDiscountAnswers(await open([{ status: 200, body: applied }]), 0);

    type("discount-value", "20");
    submit().click();
    type("discount-value", "7");
    release();
    await vi.waitFor(() => expect(text()).toContain("İndirim uygulandı ve kaydedildi."));

    expect(document.getElementById("discount-value").value).toBe("7");
  });

  it.each([
    ["discount-reason", "change", "VIPGuest"],
    ["discount-calc-type", "change", "FixedAmount"],
    ["discount-note", "input", "başka not"],
  ])("starts a new request when %s of the waiting discount is edited", async (id, eventName, value) => {
    const fetchMock = await open([{ status: 202, body: pending }, { status: 202, body: pending }]);

    type("discount-value", "20");
    submit().click();
    await vi.waitFor(() => expect(text()).toContain("yönetici onayına gönderildi"));
    const field = document.getElementById(id);
    field.value = field.tagName === "SELECT" ? [...field.options].find((o) => o.value !== field.value).value : value;
    field.dispatchEvent(new Event(eventName, { bubbles: true }));
    expect(submit().textContent).toBe("İndirim uygula");
    submit().click();
    await vi.waitFor(() => expect(discountCalls(fetchMock)).toHaveLength(2));

    const [first, second] = discountCalls(fetchMock);
    expect(second.IdempotencyKey).not.toBe(first.IdempotencyKey);
  });

  it("keeps the waiting state when checking the approval fails on the server", async () => {
    const fetchMock = await open([{ status: 202, body: pending }, { status: 503, body: { error: { code: "X", message: "x" } } }]);

    type("discount-value", "20");
    submit().click();
    await vi.waitFor(() => expect(text()).toContain("yönetici onayına gönderildi"));
    submit().click();
    await vi.waitFor(() => expect(discountCalls(fetchMock)).toHaveLength(2));
    await answered();

    expect(submit().textContent).toBe("Onay durumunu kontrol et");
    const [first, second] = discountCalls(fetchMock);
    expect(second.IdempotencyKey).toBe(first.IdempotencyKey);
  });

  it("shows no error for a refusal that belongs to a form edited in the meantime", async () => {
    const release = holdDiscountAnswers(await open([{ status: 403, body: { error: { code: "GRANT_DENIED", message: "x" } } }]), 0);

    type("discount-value", "20");
    submit().click();
    type("discount-value", "7");
    release();
    await answered();

    expect(text()).not.toContain("reddedildi");
  });

  it("starts a new request when the waiting discount is edited", async () => {
    const fetchMock = await open([{ status: 202, body: pending }, { status: 202, body: pending }]);

    type("discount-value", "20");
    submit().click();
    await vi.waitFor(() => expect(text()).toContain("yönetici onayına gönderildi"));
    type("discount-value", "15");
    expect(submit().textContent).toBe("İndirim uygula");
    expect(document.getElementById("discount-notice")).toBeNull();
    submit().click();
    await vi.waitFor(() => expect(discountCalls(fetchMock)).toHaveLength(2));

    const [first, second] = discountCalls(fetchMock);
    expect(second.IdempotencyKey).not.toBe(first.IdempotencyKey);
  });

  it("does not reuse the key of a refused discount", async () => {
    const refused = { error: { code: "GRANT_DENIED", message: "x" } };
    const fetchMock = await open([{ status: 403, body: refused }, { status: 200, body: applied }]);

    type("discount-value", "20");
    submit().click();
    await vi.waitFor(() => expect(text()).toContain("İndirim talebiniz reddedildi."));
    submit().click();
    await vi.waitFor(() => expect(discountCalls(fetchMock)).toHaveLength(2));

    const [first, second] = discountCalls(fetchMock);
    expect(second.IdempotencyKey).not.toBe(first.IdempotencyKey);
  });

  it("tells a discount refused because of the bill state with the server's sentence for that action", async () => {
    await open([{ status: 409, body: { error: { code: "UNSUPPORTED_BILL_STATE", message: "Bu hesap durumunda indirim uygulanamaz." } } }]);

    type("discount-value", "20");
    submit().click();

    await vi.waitFor(() => expect(text()).toContain("Bu hesap durumunda indirim uygulanamaz."));
    expect(text()).not.toContain("bölme tasarımı");
  });
});

const approvedByManager = { status: 200, body: { grantId: "g1", approverUserId: "m1", approverName: "Mehmet Yılmaz" } };
const approveCalls = (fetchMock) => fetchMock.mock.calls
  .filter(([url, init]) => url.endsWith("/approve") && init?.method === "POST")
  .map(([url, init]) => ({ url, ...JSON.parse(init.body) }));
const approveFields = () => ({
  name: document.getElementById("discount-approver-name"),
  secret: document.getElementById("discount-approver-secret"),
  button: document.getElementById("discount-approve-here"),
});
const fillApprover = (name, secret) => {
  const fields = approveFields();
  fields.name.value = name;
  fields.name.dispatchEvent(new Event("input", { bubbles: true }));
  fields.secret.value = secret;
};
async function waitForManager(fetchMock) {
  type("discount-value", "20");
  submit().click();
  await vi.waitFor(() => expect(approveFields().button).not.toBeNull());
  return fetchMock;
}

describe("split-payment.js manager approving a waiting discount at the till", () => {
  beforeEach(() => vi.resetModules());
  afterEach(() => {
    approveDelayMs = 0;
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
    window.sessionStorage.clear();
    window.localStorage.clear();
  });

  it("shows no manager fields until a request is waiting, then password-typed fields", async () => {
    await open([{ status: 202, body: pending }]);
    expect(approveFields().button).toBeNull();

    type("discount-value", "20");
    submit().click();
    await vi.waitFor(() => expect(approveFields().button).not.toBeNull());

    expect(approveFields().secret.type).toBe("password");
    expect(approveFields().secret.getAttribute("autocomplete")).toBe("off");
    expect(approveFields().button.textContent).toBe("Yönetici onayı ver");
  });

  it("sends the manager's name and password with the waiting request's own key, then applies the discount under the manager's name and keeps the password nowhere", async () => {
    const fetchMock = await open([{ status: 202, body: pending }, { status: 200, body: applied }], [approvedByManager]);
    await waitForManager(fetchMock);

    fillApprover("mudur1", "Mudur-Sifre-1");
    approveFields().button.click();
    await vi.waitFor(() => expect(text()).toContain("Mehmet Yılmaz onayıyla uygulandı."));

    const [approval] = approveCalls(fetchMock);
    const discount = discountCalls(fetchMock);
    expect(approval.url).toContain("/approvals/g1/approve");
    expect(approval).toMatchObject({ Username: "mudur1", Password: "Mudur-Sifre-1", IdempotencyKey: discount[0].IdempotencyKey });
    expect(discount.at(-1).IdempotencyKey).toBe(discount[0].IdempotencyKey);
    const everywhere = JSON.stringify([window.location.href, { ...window.sessionStorage }, { ...window.localStorage }]);
    expect(everywhere).not.toContain("Mudur-Sifre-1");
    expect(document.body.innerHTML).not.toContain("Mudur-Sifre-1");
  });

  it.each([
    ["MANAGER_CREDENTIAL_INVALID", 403, "Yönetici adı veya şifre hatalı."],
    ["MANAGER_LOCKED", 403, "Bu yönetici hesabı geçici olarak kilitlendi; en geç 15 dakika sonra açılır. Yönetici kendi ekranından onaylayabilir."],
    ["TOO_MANY_ATTEMPTS", 429, "Çok fazla hatalı deneme yapıldı. En geç 15 dakika sonra tekrar deneyin ya da yönetici kendi ekranından onaylasın."],
    ["SELF_APPROVAL_NOT_ALLOWED", 403, "Kendi talebinizi onaylayamazsınız; yöneticinin kendi hesabıyla girmesi gerekir."],
    ["APPROVER_LACKS_PERMISSION", 403, "Bu yönetici bu işlemi onaylama yetkisine sahip değil."],
  ])("says %s in a fixed Turkish sentence, empties the password and keeps the request waiting", async (code, status, sentence) => {
    const fetchMock = await open([{ status: 202, body: pending }], [{ status, body: { error: { code, message: "raw english" } } }]);
    await waitForManager(fetchMock);

    fillApprover("mudur1", "yanlis-sifre");
    approveFields().button.click();
    await vi.waitFor(() => expect(text()).toContain(sentence));

    expect(text()).not.toContain("raw english");
    expect(approveFields().secret.value).toBe("");
    expect(submit().textContent).toBe("Onay durumunu kontrol et");
    expect(discountCalls(fetchMock)).toHaveLength(1);
  });

  it("asks for both fields and sends nothing when one is empty", async () => {
    const fetchMock = await open([{ status: 202, body: pending }]);
    await waitForManager(fetchMock);

    fillApprover("mudur1", "");
    approveFields().button.click();

    await vi.waitFor(() => expect(text()).toContain("Yönetici adı ve şifresini girin."));
    expect(approveCalls(fetchMock)).toHaveLength(0);
  });

  it("checks the request again when the manager had already decided it elsewhere", async () => {
    const fetchMock = await open(
      [{ status: 202, body: pending }, { status: 200, body: applied }],
      [{ status: 409, body: { error: { code: "ALREADY_RESOLVED", message: "x" } } }]);
    await waitForManager(fetchMock);

    fillApprover("mudur1", "Mudur-Sifre-1");
    approveFields().button.click();

    await vi.waitFor(() => expect(text()).toContain("İndirim uygulandı ve kaydedildi."));
    expect(discountCalls(fetchMock)).toHaveLength(2);
  });

  it("approves with Enter in the password field", async () => {
    const fetchMock = await open([{ status: 202, body: pending }, { status: 200, body: applied }], [approvedByManager]);
    await waitForManager(fetchMock);

    fillApprover("mudur1", "Mudur-Sifre-1");
    approveFields().secret.dispatchEvent(new KeyboardEvent("keydown", { key: "Enter", bubbles: true }));

    await vi.waitFor(() => expect(text()).toContain("Mehmet Yılmaz onayıyla uygulandı."));
  });

  it("does not re-send the old request when the form was edited while the approval was on its way", async () => {
    approveDelayMs = 150;
    const fetchMock = await open([{ status: 202, body: pending }, { status: 200, body: applied }], [approvedByManager]);
    await waitForManager(fetchMock);

    fillApprover("mudur1", "Mudur-Sifre-1");
    approveFields().button.click();
    await vi.waitFor(() => expect(approveCalls(fetchMock)).toHaveLength(1));
    type("discount-value", "50");
    await new Promise((done) => setTimeout(done, 400));

    expect(discountCalls(fetchMock)).toHaveLength(1);
    expect(text()).not.toContain("onayıyla uygulandı");
    expect(approveFields().button).toBeNull();
  });

  it("says the connection failed when the approval request cannot be sent", async () => {
    const fetchMock = await open([{ status: 202, body: pending }], []);
    await waitForManager(fetchMock);

    fillApprover("mudur1", "Mudur-Sifre-1");
    approveFields().button.click();

    await vi.waitFor(() => expect(text()).toContain("Bağlantı kurulamadı. Tekrar deneyin."), { timeout: 4000 });
  });
});
