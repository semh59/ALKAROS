import http from "node:http";

const port = Number(process.env.FIXTURE_PORT ?? "5082");
const displayState = process.env.FIXTURE_STATE ?? "Active";
const anonymousSession = process.env.FIXTURE_SESSION === "anonymous";
let revision = 4;

const lines = [
  { itemId: "line-1", name: "Izgara Tavuk", quantity: 2, unitPrice: 180, lineTotal: 360 },
  { itemId: "line-2", name: "Ayran", quantity: 1, unitPrice: 45, lineTotal: 45 },
];

function snapshot(state = displayState) {
  const hidesOrder = state === "Idle" || state === "Unavailable";
  return {
    displayId: "display-fixture",
    terminalId: "terminal-fixture",
    orderId: hidesOrder ? null : "order-fixture",
    revision,
    state,
    editable: state === "Active",
    orderNumber: hidesOrder ? null : "POS-1042",
    lines: hidesOrder ? [] : lines,
    subtotal: hidesOrder ? 0 : 405,
    discountTotal: 0,
    taxTotal: hidesOrder ? 0 : 40.5,
    total: hidesOrder ? 0 : 445.5,
    currency: "TRY",
    serverTimestamp: new Date().toISOString(),
    message: state === "Paying" ? "Ödeme kasada alınıyor." : state === "Completed" ? "Siparişiniz tamamlandı." : "Siparişinizi kontrol edebilirsiniz.",
  };
}

function send(response, status, body) {
  response.writeHead(status, {
    "Cache-Control": "no-store",
    "Content-Type": "application/json; charset=utf-8",
  });
  response.end(JSON.stringify(body));
}

const server = http.createServer((request, response) => {
  const url = new URL(request.url ?? "/", `http://127.0.0.1:${port}`);
  if (url.pathname === "/health/ready") return send(response, 200, { status: "ready" });
  if (url.pathname === "/api/v1/auth/session") {
    return anonymousSession
      ? send(response, 401, { error: { code: "UNAUTHORIZED", message: "Oturum açmanız gerekiyor." } })
      : send(response, 200, { userId: "user-fixture", displayName: "Deniz Kaya", terminalId: "terminal-fixture" });
  }
  if (url.pathname === "/api/v1/auth/login") {
    return send(response, 200, { userId: "user-fixture", displayName: "Deniz Kaya", terminalId: "terminal-fixture" });
  }
  if (url.pathname === "/api/v1/auth/logout") return send(response, 204, null);
  if (url.pathname.endsWith("/catalog")) {
    return send(response, 200, [
      { productId: "p1", sku: "YMK-101", name: "Izgara Tavuk", categoryCode: "MAIN", categoryName: "Ana Yemek", unitPrice: 163.64, taxRate: 10 },
      { productId: "p2", sku: "ICK-202", name: "Ayran", categoryCode: "DRINK", categoryName: "İçecek", unitPrice: 40.91, taxRate: 10 },
      { productId: "p3", sku: "TAT-303", name: "Fırın Sütlaç", categoryCode: "DESSERT", categoryName: "Tatlı", unitPrice: 90.91, taxRate: 10 },
    ]);
  }
  if (url.pathname.endsWith("/orders/active")) return send(response, 200, snapshot());
  if (/\/customer-displays\/[^/]+\/snapshot$/.test(url.pathname)) return send(response, 200, snapshot());
  if (url.pathname === "/api/v1/customer-displays/pairing-requests") {
    return send(response, 200, {
      requestId: "request-fixture",
      displayId: "display-fixture",
      secret: "fixture-secret",
      code: "A7K2M9P4",
      expiresAt: new Date(Date.now() + 120_000).toISOString(),
    });
  }
  if (url.pathname.endsWith("/complete")) return send(response, 409, { error: { code: "PENDING", message: "Onay bekleniyor." } });
  if (url.pathname.endsWith("/pairings/approve")) return send(response, 204, null);
  if (url.pathname.endsWith("/display-sessions/revoke")) return send(response, 200, { revoked: 1 });
  if (request.method === "POST" && /\/orders$/.test(url.pathname)) {
    revision += 1;
    return send(response, 200, { orderId: "order-fixture", orderNumber: "POS-1043", revision });
  }
  if (/\/orders\/[^/]+\/(items|submit)/.test(url.pathname) || /\/orders\/[^/]+\/items\//.test(url.pathname)) {
    revision += 1;
    return send(response, 200, { orderId: "order-fixture", revision, rowVersion: revision });
  }
  return send(response, 404, { error: { code: "NOT_FOUND", message: "Fixture route not found." } });
});

server.listen(port, "127.0.0.1", () => {
  process.stdout.write(`UI fixture listening on http://127.0.0.1:${port} state=${displayState}\n`);
});

for (const signal of ["SIGINT", "SIGTERM"]) {
  process.on(signal, () => server.close(() => process.exit(0)));
}
