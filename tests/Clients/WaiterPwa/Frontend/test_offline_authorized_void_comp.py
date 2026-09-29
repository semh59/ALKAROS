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

    const stateModule = await import(process.argv[2]);
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


@pytest.fixture(scope="module")
def results(tmp_path_factory):
    node = shutil.which("node")
    if node is None:
        pytest.skip("node is not installed")
    harness = tmp_path_factory.mktemp("rmd297") / "harness.mjs"
    harness.write_text(HARNESS, encoding="utf-8")
    completed = subprocess.run(
        [node, str(harness), (WWWROOT / "js" / "state.js").as_uri()],
        capture_output=True,
        text=True,
        check=True,
    )
    return json.loads(completed.stdout.strip().splitlines()[-1])


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


def test_confirm_paths_queue_offline_and_reconnect_reconciles():
    void_comp = (WWWROOT / "js" / "sheets" / "void-comp.js").read_text(encoding="utf-8")
    queue = (WWWROOT / "js" / "offline-queue.js").read_text(encoding="utf-8")
    app = (WWWROOT / "waiter-app.js").read_text(encoding="utf-8")

    assert "authorizeOffline('bills.void', context, 0," in void_comp
    assert "authorizeOffline('bills.comp', context, item ? item.totalPrice : 0," in void_comp
    assert "apiUrl('/offline-reconciliation')" in queue
    assert "result.data.summary.brief" in queue
    assert "rememberOfflineBudget(result.data.offlineBudget, result.data.userId)" in app
    assert app.count("void reconcileOfflineActions()") == 3
