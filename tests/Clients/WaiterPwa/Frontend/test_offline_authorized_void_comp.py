"""V1-RMD-297: the waiter client's offline authority budget check, run for real in Node.

state.js is imported as the ES module the browser loads, with the few browser
globals it touches at import time stubbed; the checks call the exported
functions and read the persisted localStorage, so a change that makes the
local check approve something the budget does not cover fails here.
"""

import json
import shutil
import subprocess
import textwrap
from pathlib import Path

import pytest

WORKSPACE = Path(__file__).resolve().parents[4]
WWWROOT = WORKSPACE / "src" / "Clients" / "WaiterPwa" / "wwwroot"

HARNESS = textwrap.dedent(
    """
    const storage = new Map();
    globalThis.localStorage = {
      getItem: (key) => (storage.has(key) ? storage.get(key) : null),
      setItem: (key, value) => storage.set(key, String(value)),
      removeItem: (key) => storage.delete(key),
    };
    globalThis.document = { getElementById: () => null };
    globalThis.window = globalThis;
    Object.defineProperty(globalThis, 'navigator', { value: { onLine: true }, configurable: true });

    const stateModule = await import(`${process.argv[2]}/state.js`);
    const { state, rememberOfflineBudget, forgetOfflineBudget, offlineAuthorityFor, persistOfflineActions } = stateModule;
    const now = Date.parse('2026-09-29T10:00:00Z');
    const results = {};

    results.noBudget = offlineAuthorityFor('bills.comp', 10, now);

    rememberOfflineBudget({
      budgetId: 'b1', roleCode: 'waiter', expiresAt: '2026-09-29T14:00:00Z',
      lines: [
        { permissionCode: 'bills.comp', limitAmount: 50, maxCount: 2 },
        { permissionCode: 'bills.void', limitAmount: null, maxCount: 1 },
      ],
    }, 'u1');
    results.stored = JSON.parse(localStorage.getItem('alkaros_waiter_offline_budget'));

    results.withinLimit = offlineAuthorityFor('bills.comp', 50, now);
    results.overLimit = offlineAuthorityFor('bills.comp', 50.01, now);
    results.negative = offlineAuthorityFor('bills.comp', -1, now);
    results.notInBudget = offlineAuthorityFor('bills.discount', 1, now);
    results.voidNoLimit = offlineAuthorityFor('bills.void', 0, now);
    results.expired = offlineAuthorityFor('bills.comp', 1, Date.parse('2026-09-29T14:00:00Z'));

    state.offlineBudget.used = { 'bills.comp': 2 };
    persistOfflineActions();
    results.countSpent = offlineAuthorityFor('bills.comp', 1, now);
    results.usedPersisted = JSON.parse(localStorage.getItem('alkaros_waiter_offline_budget')).used;

    rememberOfflineBudget({ budgetId: 'b2', expiresAt: '2026-09-29T14:00:00Z', lines: [] }, 'u1');
    results.withoutRoleCode = { budget: state.offlineBudget, stored: localStorage.getItem('alkaros_waiter_offline_budget') };

    rememberOfflineBudget({ budgetId: 'b3', roleCode: 'waiter', expiresAt: '2026-09-29T14:00:00Z', lines: [] }, 'u1');
    forgetOfflineBudget();
    results.forgotten = { budget: state.offlineBudget, stored: localStorage.getItem('alkaros_waiter_offline_budget') };

    console.log(JSON.stringify(results));
    """
)


CONFIRM_HARNESS = textwrap.dedent(
    """
    const storage = new Map();
    globalThis.localStorage = {
      getItem: (key) => (storage.has(key) ? storage.get(key) : null),
      setItem: (key, value) => storage.set(key, String(value)),
      removeItem: (key) => storage.delete(key),
    };
    const fakeElement = () => {
      const element = {
        children: [], dataset: {}, style: {}, disabled: false, innerHTML: '', textContent: '',
        classList: { add() {}, remove() {}, toggle() {}, contains: () => false },
        appendChild: (child) => {
          child.remove = () => { const index = element.children.indexOf(child); if (index >= 0) element.children.splice(index, 1); };
          element.children.push(child);
        },
        append() {}, setAttribute() {}, removeAttribute() {}, addEventListener() {}, focus() {}, remove() {},
        querySelector: () => null, querySelectorAll: () => [],
      };
      return element;
    };
    globalThis.document = { getElementById: () => fakeElement(), createElement: fakeElement, addEventListener() {} };
    globalThis.window = globalThis;
    Object.defineProperty(globalThis, 'navigator', { value: { onLine: true }, configurable: true });

    const requests = [];
    let serverReachable = false;
    globalThis.fetch = async (url, init) => {
      requests.push({ url: String(url), body: init && init.body ? JSON.parse(init.body) : null });
      if (!serverReachable) throw new TypeError('Failed to fetch');
      return { ok: true, status: 200, json: async () => ({ summary: { brief: 'Uzlaştırma tamam.' } }) };
    };

    const base = process.argv[2];
    const { state, el, rememberOfflineBudget } = await import(`${base}/state.js`);
    const { confirmVoidSent, confirmComp } = await import(`${base}/sheets/void-comp.js`);
    const { reconcileOfflineActions } = await import(`${base}/offline-queue.js`);
    const results = {};

    rememberOfflineBudget({
      budgetId: 'b1', roleCode: 'waiter', expiresAt: '2999-01-01T00:00:00Z',
      lines: [
        { permissionCode: 'bills.comp', limitAmount: 100, maxCount: 5 },
        { permissionCode: 'bills.void', limitAmount: null, maxCount: 5 },
      ],
    }, 'u1');
    state.order = {
      orderId: 'o1', rowVersion: 7,
      items: [{ itemId: 'i1', totalPrice: 80, status: 'Sent' }, { itemId: 'i2', totalPrice: 60, status: 'Sent' }, { itemId: 'i3', totalPrice: 150, status: 'Sent' }],
    };
    const context = (itemId, key) => ({ itemId, reason: 'CustomerChange', idempotencyKey: key });

    state.optionsContext = context('i1', 'k-void');
    await confirmVoidSent();
    state.optionsContext = context('i2', 'k-comp');
    await confirmComp();
    results.queued = state.offlineActions.map(({ idempotencyKey, permissionCode, amount, reasonCode, subjectId }) => ({ idempotencyKey, permissionCode, amount, reasonCode, subjectId }));

    state.optionsContext = context('i1', 'k-void');
    await confirmVoidSent();
    results.afterRetry = { keys: state.offlineActions.map((action) => action.idempotencyKey), used: state.offlineBudget.used };

    state.optionsContext = context('i3', 'k-over');
    await confirmComp();
    results.afterOverLimit = state.offlineActions.map((action) => action.idempotencyKey);

    await reconcileOfflineActions();
    results.keptWhileOffline = state.offlineActions.length;

    serverReachable = true;
    requests.length = 0;
    await reconcileOfflineActions();
    results.reconcile = { urls: requests.map((request) => request.url), sentKeys: requests.map((request) => (request.body.actions || []).map((action) => action.idempotencyKey)), left: state.offlineActions.length, stored: JSON.parse(localStorage.getItem('alkaros_waiter_offline_actions') || '[]').length, toasts: el.toasts.children.map((toast) => toast.innerHTML) };

    serverReachable = false;
    rememberOfflineBudget({
      budgetId: 'b2', roleCode: 'waiter', expiresAt: '2999-01-01T00:00:00Z',
      lines: [{ permissionCode: 'bills.void', limitAmount: null, maxCount: 1 }],
    }, 'u1');
    state.optionsContext = context('i1', 'k-last');
    await confirmVoidSent();
    state.optionsContext = context('i1', 'k-last');
    await confirmVoidSent();
    results.lastRight = {
      keys: state.offlineActions.map((action) => action.idempotencyKey),
      used: state.offlineBudget.used,
      lastToast: el.toasts.children[el.toasts.children.length - 1].innerHTML,
    };

    process.stdout.write(JSON.stringify(results), () => process.exit(0));
    """
)


def run_harness(tmp_path_factory, script):
    node = shutil.which("node")
    if node is None:
        pytest.skip("node is not installed")
    harness = tmp_path_factory.mktemp("offline_authority") / "harness.mjs"
    harness.write_text(script, encoding="utf-8")
    completed = subprocess.run(
        [node, str(harness), (WWWROOT / "js").as_uri()],
        capture_output=True,
        text=True,
        encoding="utf-8",
        timeout=60,
    )
    assert completed.returncode == 0, completed.stderr
    return json.loads(completed.stdout.strip().splitlines()[-1])


@pytest.fixture(scope="module")
def results(tmp_path_factory):
    return run_harness(tmp_path_factory, HARNESS)


@pytest.fixture(scope="module")
def confirmed(tmp_path_factory):
    return run_harness(tmp_path_factory, CONFIRM_HARNESS)


def test_nothing_is_authorized_without_a_budget(results):
    assert results["noBudget"]["allowed"] is False
    assert "Bağlanınca tekrar deneyin" in results["noBudget"]["message"]


def test_the_login_budget_is_kept_with_its_role_code_and_user(results):
    stored = results["stored"]
    assert stored["budgetId"] == "b1"
    assert stored["roleCode"] == "waiter"
    assert stored["userId"] == "u1"
    assert stored["used"] == {}


def test_an_action_within_the_line_is_authorized(results):
    assert results["withinLimit"] == {"allowed": True}
    assert results["voidNoLimit"] == {"allowed": True}


def test_anything_outside_the_budget_is_refused_not_approved(results):
    for key in ("overLimit", "negative", "notInBudget", "expired", "countSpent"):
        assert results[key]["allowed"] is False, key
        assert "Bağlanınca tekrar deneyin" in results[key]["message"], key


def test_the_spent_count_survives_a_reload(results):
    assert results["usedPersisted"] == {"bills.comp": 2}


def test_a_budget_without_a_role_code_is_not_kept(results):
    assert results["withoutRoleCode"]["budget"] is None
    assert results["withoutRoleCode"]["stored"] is None


def test_signing_out_forgets_the_budget(results):
    assert results["forgotten"]["budget"] is None
    assert results["forgotten"]["stored"] is None


def test_void_and_comp_queue_with_the_line_amount_and_their_own_idempotency_key(confirmed):
    assert confirmed["queued"] == [
        {"idempotencyKey": "k-void", "permissionCode": "bills.void", "amount": 80, "reasonCode": "CustomerChange", "subjectId": "i1"},
        {"idempotencyKey": "k-comp", "permissionCode": "bills.comp", "amount": 60, "reasonCode": "CustomerChange", "subjectId": "i2"},
    ]


def test_confirming_the_same_offline_action_again_queues_and_spends_it_once(confirmed):
    assert confirmed["afterRetry"]["keys"] == ["k-void", "k-comp"]
    assert confirmed["afterRetry"]["used"] == {"bills.void": 1, "bills.comp": 1}


def test_a_second_confirm_of_a_waiting_action_says_it_is_queued_even_when_the_last_right_is_spent(confirmed):
    assert confirmed["lastRight"]["keys"] == ["k-last"]
    assert confirmed["lastRight"]["used"] == {"bills.void": 1}
    assert "zaten" in confirmed["lastRight"]["lastToast"]
    assert "hakkınız" not in confirmed["lastRight"]["lastToast"]


def test_a_comp_above_the_budget_line_is_not_queued(confirmed):
    assert confirmed["afterOverLimit"] == ["k-void", "k-comp"]


def test_actions_stay_queued_while_the_server_is_unreachable(confirmed):
    assert confirmed["keptWhileOffline"] == 2


def test_reconnect_sends_the_queued_actions_once_and_empties_the_queue(confirmed):
    reconcile = confirmed["reconcile"]
    assert len(reconcile["urls"]) == 1 and reconcile["urls"][0].endswith("/offline-reconciliation")
    assert reconcile["sentKeys"] == [["k-void", "k-comp"]]
    assert reconcile["left"] == 0
    assert reconcile["stored"] == 0
    assert any("Uzlaştırma tamam." in toast for toast in reconcile["toasts"])
