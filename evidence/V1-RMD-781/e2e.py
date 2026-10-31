"""Live-stack walk: login -> catalog -> draft -> submit -> send-to-cashier -> bill -> cash session -> cash tender.
Reads the admin password from deploy/docker/admin_password and never prints it."""
import json, sys, uuid, urllib.request, urllib.error, http.cookiejar

BASE = "http://localhost:8090"
TERMINAL = "d60b3df9-6396-447e-81ba-7babfcf0e205"
KASA = "00000000-0000-0000-0000-000000000001"
SKU = sys.argv[1] if len(sys.argv) > 1 else "FOOD-002"
QTY = int(sys.argv[2]) if len(sys.argv) > 2 else 2

jar = http.cookiejar.CookieJar()
opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(jar))


def call(method, path, body=None):
    data = None if body is None else json.dumps(body).encode()
    req = urllib.request.Request(BASE + path, data=data, method=method, headers={"Content-Type": "application/json"})
    try:
        with opener.open(req) as r:
            raw = r.read().decode()
            return r.status, (json.loads(raw) if raw else None)
    except urllib.error.HTTPError as e:
        raw = e.read().decode()
        try:
            return e.code, json.loads(raw)
        except ValueError:
            return e.code, raw


def step(name, result):
    status, body = result
    shown = body if not isinstance(body, (dict, list)) else json.dumps(body, ensure_ascii=False)[:300]
    print(f"{name}: {status} {shown}")
    return status, body


password = open("deploy/docker/admin_password", encoding="utf-8").read().strip()
s, _ = step("login", call("POST", "/api/v1/auth/login", {"Username": "admin", "Password": password, "TerminalId": TERMINAL}))
assert s == 200
t = f"/api/v1/terminals/{TERMINAL}"

s, catalog = call("GET", f"{t}/catalog")
print("catalog:", s)
products = catalog["products"] if isinstance(catalog, dict) else catalog
product = next(p for p in products if p.get("sku") == SKU)
price = product["unitPrice"]
print("product:", product["name"], price)

order_id = str(uuid.uuid4())
line_id = str(uuid.uuid4())
payload = {
    "id": order_id, "tableId": KASA, "tableNumber": "KASA-1",
    "items": [{"id": line_id, "productId": product["productId"],
               "name": product["name"], "productName": product["name"], "quantity": QTY,
               "unitPrice": price, "specialInstructions": None, "modifiers": []}],
}
s, draft = step("table-draft", call("POST", f"{t}/orders/table-draft", payload))
assert s == 200
s, sub = step("submit-draft", call("POST", f"{t}/orders/{draft['orderId']}/submit-draft",
              {"orderId": draft["orderId"], "expectedRowVersion": draft["rowVersion"], "operationId": f"{draft['orderId']}:submit"}))
assert s == 200
s, _ = step("send-to-cashier", call("POST", f"{t}/orders/{draft['orderId']}/send-to-cashier", {"tableId": KASA}))
s, bill = step("bill from-order", call("POST", f"{t}/billing/bills/from-order/{draft['orderId']}"))
assert s == 200
bill_id = bill.get("billId") or bill.get("id")
payable = bill.get("payableAmount") or bill.get("totalAmount")
print("billId:", bill_id, "payable:", payable)

s, active = call("GET", f"{t}/cash-sessions/active")
if s == 404:
    s, active = step("open cash session", call("POST", f"{t}/cash-sessions/", {"OpeningBalance": 0}))
    assert s == 201
session_id = active.get("cashSessionId")
s, paid = step("cash-tender", call("POST", f"{t}/cash-sessions/{session_id}/cash-tender",
               {"BillId": bill_id, "AmountDue": payable, "TenderedAmount": payable, "IdempotencyKey": str(uuid.uuid4())}))
print("orderId:", draft["orderId"], "billId:", bill_id)
