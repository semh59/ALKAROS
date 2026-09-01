import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type FormEvent,
  type KeyboardEvent as ReactKeyboardEvent,
  type ReactNode,
} from "react";
import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import { ApiError, api } from "./api";
import type { CatalogProduct, DisplaySnapshot, PairingCreated } from "./contracts";
import { ProductionShell } from "./shell";
import type { Connectivity, Freshness, RouteAuthorization, ShellIdentity, ShellNavigationItem, ShellSession } from "./shell/models";
import { TableWorkspace, createTableManagementClient, type CreateTableInput, type CreateZoneInput, type FloorPlan, type SaveFloorPlanInput, type SaveFloorPlanResult, type TableActionRequest, type TableWorkspaceState } from "./features/tables";
import { BillSplitWorkspace, createBillingSplitClient, type BillSplitDesign, type BillSplitWorkspaceState, type SaveSplitRequest, type SplitOwnerOption } from "./features/billing";
import { CatalogWorkspace, createCatalogManagementClient, type CatalogCreateInput, type CatalogData, type CatalogWorkspaceState } from "./features/catalog";
import { KitchenOperationsWorkspace, createKitchenOperationsClient, loadKitchenRuntimeConfiguration, type KitchenData, type KitchenOperationsClient, type KitchenWorkspaceState } from "./features/kitchen-operations";
import { SystemHealthWorkspace, type SystemHealthState } from "./features/system-health";
import {
  afterFailure,
  afterSnapshot,
  displayPresentation,
  type DisplayFreshness,
} from "./stale";

const formatMoney = (value: number, currency = "TRY") =>
  new Intl.NumberFormat("tr-TR", { style: "currency", currency }).format(value);

const formatQuantity = (value: number) =>
  new Intl.NumberFormat("tr-TR", { maximumFractionDigits: 2 }).format(value);

const grossUnitPrice = (netUnitPrice: number, taxRate: number) =>
  Math.round((netUnitPrice * (1 + taxRate / 100) + Number.EPSILON) * 100) / 100;

function savedId(key: string): string {
  const existing = localStorage.getItem(key);
  if (existing) return existing;
  const created = crypto.randomUUID();
  localStorage.setItem(key, created);
  return created;
}

export function App() {
  return window.location.pathname.startsWith("/display") ? <CustomerDisplay /> : <Cashier />;
}

type BackendStatus = "checking" | "online" | "offline";
type CashierSession = "checking" | "anonymous" | "ready";
type FeedbackKind = "error" | "success" | "conflict" | "unauthorized";
type DisplaySession = "checking" | "pairing" | "ready" | "error";

const focusableSelector = [
  "button:not([disabled])",
  "input:not([disabled])",
  "[href]",
  "[tabindex]:not([tabindex='-1'])",
].join(",");

function Cashier() {
  const [terminalId] = useState(() => savedId("alkaros.terminal-id"));
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [capabilities, setCapabilities] = useState<string[]>([]);
  const [catalog, setCatalog] = useState<CatalogProduct[]>([]);
  const [order, setOrder] = useState<DisplaySnapshot | null>(null);
  const [pairingCode, setPairingCode] = useState("");
  const [session, setSession] = useState<CashierSession>("checking");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [feedbackKind, setFeedbackKind] = useState<FeedbackKind>("success");
  const [search, setSearch] = useState("");
  const [activeCategory, setActiveCategory] = useState("ALL");
  const [backendStatus, setBackendStatus] = useState<BackendStatus>("checking");
  const [pairingOpen, setPairingOpen] = useState(false);
  const pairingTrigger = useRef<HTMLButtonElement>(null);
  const pairingDialog = useRef<HTMLDivElement>(null);

  const reload = useCallback(async () => {
    const [session, products, active] = await Promise.all([
      api.session(terminalId),
      api.catalog(terminalId),
      api.activeOrder(terminalId),
    ]);
    setDisplayName(session.displayName);
    setCapabilities(session.capabilities ?? []);
    setCatalog(products);
    setOrder(active);
    setSession("ready");
    setBackendStatus("online");
  }, [terminalId]);

  const restoreSession = useCallback(async () => {
    try {
      await reload();
    } catch (reason) {
      if (reason instanceof ApiError && reason.status === 401) setSession("anonymous");
      else {
        setSession("anonymous");
        setBackendStatus("offline");
      }
    }
  }, [reload]);

  useEffect(() => {
    void restoreSession();
  }, [restoreSession]);

  useEffect(() => {
    const check = () => {
      api.health()
        .then(() => setBackendStatus("online"))
        .catch(() => setBackendStatus("offline"));
    };
    check();
    const timer = window.setInterval(check, 10_000);
    return () => window.clearInterval(timer);
  }, []);

  useEffect(() => {
    if (!pairingOpen) return;
    const dialog = pairingDialog.current;
    const first = dialog?.querySelector<HTMLElement>(focusableSelector);
    first?.focus();
    return () => pairingTrigger.current?.focus();
  }, [pairingOpen]);

  const closePairing = () => setPairingOpen(false);

  const trapPairingFocus = (event: ReactKeyboardEvent<HTMLDivElement>) => {
    if (event.key === "Escape") {
      event.preventDefault();
      closePairing();
      return;
    }
    if (event.key !== "Tab") return;
    const controls = [...event.currentTarget.querySelectorAll<HTMLElement>(focusableSelector)];
    if (controls.length === 0) return;
    const first = controls[0];
    const last = controls.at(-1)!;
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  };

  const execute = async (action: () => Promise<unknown>, success?: string) => {
    setBusy(true);
    setError("");
    setNotice("");
    try {
      await action();
      await reload();
      if (success) {
        setFeedbackKind("success");
        setNotice(success);
      }
      return true;
    } catch (reason) {
      if (reason instanceof ApiError && reason.status === 401) {
        setSession("anonymous");
        setFeedbackKind("unauthorized");
      } else if (reason instanceof ApiError && reason.status === 409) {
        setFeedbackKind("conflict");
        await reload().catch(() => setBackendStatus("offline"));
      } else {
        setFeedbackKind("error");
      }
      setError(reason instanceof Error ? reason.message : "İşlem tamamlanamadı.");
      return false;
    } finally {
      setBusy(false);
    }
  };

  const login = async (event: FormEvent) => {
    event.preventDefault();
    setBusy(true);
    setError("");
    setFeedbackKind("error");
    try {
      const result = await api.login(username, password, terminalId);
      setDisplayName(result.displayName);
      setCapabilities(result.capabilities ?? []);
      setPassword("");
      await reload();
    } catch (reason) {
      setSession("anonymous");
      setFeedbackKind(reason instanceof ApiError && reason.status === 401 ? "unauthorized" : "error");
      setError(reason instanceof Error ? reason.message : "Giriş yapılamadı.");
    } finally {
      setBusy(false);
    }
  };

  const logout = async () => {
    setBusy(true);
    setError("");
    try {
      await api.logout(terminalId);
      setSession("anonymous");
      setDisplayName("");
      setCapabilities([]);
      setCatalog([]);
      setOrder(null);
      setNotice("");
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Oturum kapatılamadı.");
    } finally {
      setBusy(false);
    }
  };

  const categories = useMemo(() => {
    const values = new Map<string, string>();
    catalog.forEach((product) => values.set(product.categoryCode, product.categoryName));
    return [...values].map(([code, name]) => ({ code, name }));
  }, [catalog]);

  if (session !== "ready") {
    return (
      <main className="login-shell">
        <section className="login-intro" aria-label="ALKAROS kasa">
          <div className="brand-mark">A</div>
          <span className="eyebrow">ALKAROS RESTAURANT OS</span>
          <h1>Servis hızını<br />kasada koruyun.</h1>
          <p>Sipariş, müşteri ekranı ve mutfak akışı aynı gerçek veriyi takip eder.</p>
          <div className={`system-state ${backendStatus}`}>
            <span />
            {backendStatus === "online" ? "Kasa sunucusu hazır" :
              backendStatus === "offline" ? "Kasa sunucusuna ulaşılamıyor" : "Bağlantı kontrol ediliyor"}
          </div>
        </section>
        <form className="login-card" onSubmit={login} aria-busy={busy || session === "checking"}>
          <div className="login-card-heading">
            <span>Personel girişi</span>
            <h2>Kasayı aç</h2>
            <p>Yetkili kullanıcı hesabınızla devam edin.</p>
          </div>
          <div className={`system-state login-state ${backendStatus}`} role="status">
            <span />
            {backendStatus === "online" ? "Kasa sunucusu çevrimiçi" :
              backendStatus === "offline" ? "Kasa sunucusu çevrimdışı" : "Bağlantı kontrol ediliyor"}
          </div>
          <label>
            Kullanıcı adı
            <input
              value={username}
              onChange={(event) => setUsername(event.target.value)}
              autoComplete="username"
              autoFocus
            />
          </label>
          <label>
            Parola
            <input
              type="password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              autoComplete="current-password"
            />
          </label>
          {session === "checking" && (
            <div className="alert information" role="status">Oturum ve bağlantı doğrulanıyor…</div>
          )}
          {error && <div className={`alert ${feedbackKind}`} role="alert">{error}</div>}
          <button className="primary login-submit" disabled={busy || session === "checking" || backendStatus === "offline"}>
            {session === "checking" ? "Oturum kontrol ediliyor…" : busy ? "Giriş yapılıyor…" : "Giriş yap"}
          </button>
          <small className="terminal-reference">Terminal · {terminalId.slice(0, 8).toUpperCase()}</small>
        </form>
      </main>
    );
  }

  const activeOrder = order?.orderId ? order : null;
  const normalizedSearch = search.trim().toLocaleLowerCase("tr-TR");
  const visibleProducts = catalog.filter((product) =>
    (activeCategory === "ALL" || product.categoryCode === activeCategory)
    && (!normalizedSearch
      || product.name.toLocaleLowerCase("tr-TR").includes(normalizedSearch)
      || product.sku.toLocaleLowerCase("tr-TR").includes(normalizedSearch)));
  const itemCount = activeOrder?.lines.reduce((sum, line) => sum + line.quantity, 0) ?? 0;
  const canStartNextOrder = !activeOrder || !activeOrder.editable;

  const currentPath = window.location.pathname.replace(/\/$/, "") || "/";
  if (currentPath !== "/") {
    return <ExperiencePage
      terminalId={terminalId}
      displayName={displayName || username}
      capabilities={capabilities}
      path={currentPath}
      backendStatus={backendStatus}
      onLogout={logout}
    />;
  }

  return (
    <main className="cashier-shell">
      <header className="pos-header">
        <div className="pos-brand">
          <div className="brand-mark compact">A</div>
          <div><strong>ALKAROS</strong><span>Kasa satış</span></div>
        </div>
        <div className="pos-header-actions">
          <div className={`system-state compact ${backendStatus}`} title="Gerçek /health/ready sonucu">
            <span />
            {backendStatus === "online" ? "Sunucu hazır" : "Bağlantı sorunu"}
          </div>
          <button className="header-action" onClick={() => window.open("/display", "alkaros-customer-display")}>
            Müşteri ekranı
          </button>
          <a className="header-action" href="/tables">Masalar</a>
          <a className="header-action" href="/kitchen">Mutfak</a>
          {capabilities.includes("catalog.manage") && <a className="header-action" href="/catalog">Menü</a>}
          <button
            className="header-action"
            ref={pairingTrigger}
            aria-haspopup="dialog"
            aria-expanded={pairingOpen}
            onClick={() => setPairingOpen(true)}
          >
            Ekranı eşleştir
          </button>
          <div className="operator-card"><span>{displayName || username}</span><small>Kasiyer</small></div>
          <button className="icon-action" disabled={busy} onClick={() => void logout()} aria-label="Oturumu kapat">
            Çıkış
          </button>
        </div>
      </header>

      {(error || notice) && (
        <div
          className={`toast-message ${error ? feedbackKind : "success"}`}
          role={error ? "alert" : "status"}
          aria-live={error ? "assertive" : "polite"}
        >
          <span>{error || notice}</span>
          <button onClick={() => { setError(""); setNotice(""); }} aria-label="Mesajı kapat">×</button>
        </div>
      )}

      <div className="mobile-ticket-bar" aria-label="Sipariş özeti">
        <div>
          <span>{activeOrder ? "Toplam" : "Aktif sipariş yok"}</span>
          <strong>{activeOrder ? formatMoney(activeOrder.total) : "Yeni satış"}</strong>
        </div>
        {!activeOrder ? (
          <button className="primary" disabled={busy} onClick={() => execute(() => api.startOrder(terminalId))}>
            Sipariş aç
          </button>
        ) : activeOrder.editable ? (
          <button
            className="primary"
            disabled={busy || activeOrder.lines.length === 0}
            onClick={() => execute(
              () => api.submitOrder(terminalId, activeOrder.orderId!, activeOrder.revision),
              "Sipariş gönderildi. Yeni satış açabilirsiniz.",
            )}
          >Gönder</button>
        ) : (
          <button className="secondary" disabled={busy} onClick={() => execute(() => api.startOrder(terminalId))}>
            Sonraki sipariş
          </button>
        )}
      </div>

      <div className="pos-workspace">
        <nav className="category-rail" aria-label="Ürün kategorileri">
          <button
            className={activeCategory === "ALL" ? "active" : ""}
            onClick={() => setActiveCategory("ALL")}
          >
            <span className="category-glyph">⌂</span>
            Tümü
            <small>{catalog.length}</small>
          </button>
          {categories.map((category, index) => (
            <button
              className={activeCategory === category.code ? "active" : ""}
              key={category.code}
              onClick={() => setActiveCategory(category.code)}
            >
              <span className="category-glyph">{String(index + 1).padStart(2, "0")}</span>
              {category.name}
              <small>{catalog.filter((product) => product.categoryCode === category.code).length}</small>
            </button>
          ))}
        </nav>

        <section className="catalog-workspace">
          <div className="catalog-toolbar">
            <div>
              <span className="eyebrow dark">HIZLI SİPARİŞ</span>
              <h1>{activeCategory === "ALL" ? "Tüm ürünler" : categories.find((c) => c.code === activeCategory)?.name}</h1>
            </div>
            <label className="search-field">
              <span>⌕</span>
              <input
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                placeholder="Ürün veya SKU ara"
                aria-label="Ürün ara"
              />
            </label>
          </div>

          <div className="product-grid">
            {visibleProducts.map((product) => (
              <button
                className="product-card"
                key={product.productId}
                disabled={busy || !activeOrder?.editable}
                onClick={() => execute(() => api.addItem(
                  terminalId,
                  activeOrder!.orderId!,
                  product.productId,
                  activeOrder!.revision,
                ))}
              >
                <span className="product-category">{product.categoryName}</span>
                <strong>{product.name}</strong>
                <span className="product-card-footer">
                  <small>{product.sku}</small>
                  <span className="product-price"><b>{formatMoney(grossUnitPrice(product.unitPrice, product.taxRate))}</b><small>KDV dahil</small></span>
                </span>
              </button>
            ))}
            {visibleProducts.length === 0 && (
              <div className="catalog-empty">Aramanızla eşleşen aktif ürün bulunamadı.</div>
            )}
          </div>
        </section>

        <aside className="ticket-panel">
          <div className="ticket-heading">
            <div>
              <span className="eyebrow dark">AKTİF SİPARİŞ</span>
              <h2>{activeOrder?.orderNumber ?? "Yeni satış"}</h2>
            </div>
            <div className="ticket-meta"><strong>{formatQuantity(itemCount)}</strong><span>ürün</span></div>
          </div>

          {!activeOrder ? (
            <div className="new-order-state">
              <div className="new-order-icon">＋</div>
              <h3>Siparişe hazır</h3>
              <p>Ürünleri açmak ve yeni satışa başlamak için sipariş oluşturun.</p>
              <button className="primary large" disabled={busy} onClick={() => execute(() => api.startOrder(terminalId))}>
                Yeni sipariş aç
              </button>
            </div>
          ) : (
            <>
              <div className="ticket-lines" aria-live="polite" aria-atomic="false">
                {activeOrder.lines.length === 0 && (
                  <div className="ticket-empty">Soldaki katalogdan ürün seçin.</div>
                )}
                {activeOrder.lines.map((line) => (
                  <div className="ticket-line" key={line.itemId}>
                    <div className="ticket-line-main">
                      <strong>{line.name}</strong>
                      <small>{formatMoney(line.lineTotal / line.quantity)} × {formatQuantity(line.quantity)} · KDV dahil</small>
                    </div>
                    <strong className="line-total">{formatMoney(line.lineTotal)}</strong>
                    <div className="quantity-controls">
                      <button
                        aria-label={`${line.name} azalt`}
                        disabled={busy || line.quantity <= 1 || !activeOrder.editable}
                        onClick={() => execute(() => api.changeQuantity(
                          terminalId, activeOrder.orderId!, line.itemId, line.quantity - 1, activeOrder.revision,
                        ))}
                      >−</button>
                      <span>{formatQuantity(line.quantity)}</span>
                      <button
                        aria-label={`${line.name} artır`}
                        disabled={busy || !activeOrder.editable}
                        onClick={() => execute(() => api.changeQuantity(
                          terminalId, activeOrder.orderId!, line.itemId, line.quantity + 1, activeOrder.revision,
                        ))}
                      >+</button>
                      <button
                        className="remove"
                        disabled={busy || !activeOrder.editable}
                        onClick={() => execute(() => api.removeItem(
                          terminalId, activeOrder.orderId!, line.itemId, activeOrder.revision,
                        ))}
                      >Kaldır</button>
                    </div>
                  </div>
                ))}
              </div>
              <div className="ticket-totals">
                <div><span>Ara toplam</span><span>{formatMoney(activeOrder.subtotal)}</span></div>
                {activeOrder.discountTotal > 0 && (
                  <div><span>İndirim</span><span>−{formatMoney(activeOrder.discountTotal)}</span></div>
                )}
                <div><span>KDV</span><span>{formatMoney(activeOrder.taxTotal)}</span></div>
                <div className="grand-total"><span>Toplam</span><strong>{formatMoney(activeOrder.total)}</strong></div>
              </div>
              {activeOrder.editable ? (
                <button
                  className="primary submit-order"
                  disabled={busy || activeOrder.lines.length === 0}
                  onClick={() => execute(
                    () => api.submitOrder(terminalId, activeOrder.orderId!, activeOrder.revision),
                    "Sipariş gönderildi. Yeni satış açabilirsiniz.",
                  )}
                >Siparişi gönder</button>
              ) : (
                <div className="submitted-state">
                  <span>✓</span>
                  <div><strong>Sipariş gönderildi</strong><small>Revision {activeOrder.revision}</small></div>
                </div>
              )}
              {canStartNextOrder && (
                <button className="secondary next-order" disabled={busy} onClick={() => execute(() => api.startOrder(terminalId))}>
                  Sonraki siparişi aç
                </button>
              )}
            </>
          )}

        </aside>
      </div>
      {pairingOpen && (
        <div className="dialog-backdrop" onMouseDown={(event) => {
          if (event.target === event.currentTarget) closePairing();
        }}>
          <div
            className="pairing-dialog"
            ref={pairingDialog}
            role="dialog"
            aria-modal="true"
            aria-labelledby="pairing-title"
            aria-describedby="pairing-description"
            onKeyDown={trapPairingFocus}
          >
            <div className="dialog-heading">
              <div>
                <span className="eyebrow dark">MÜŞTERİ EKRANI</span>
                <h2 id="pairing-title">Ekran bağlantısını yönetin</h2>
              </div>
              <button className="dialog-close" onClick={closePairing} aria-label="Eşleştirme penceresini kapat">×</button>
            </div>
            <p id="pairing-description">Müşteri ekranındaki 8 karakterli kodu girin veya mevcut ekran yetkisini kaldırın.</p>
            <label>
              Eşleştirme kodu
              <input
                value={pairingCode}
                onChange={(event) => setPairingCode(event.target.value.toUpperCase().replace(/[^A-Z0-9]/g, ""))}
                placeholder="Örn. A7K2M9P4"
                maxLength={8}
                autoComplete="off"
                inputMode="text"
              />
            </label>
            <div className="dialog-actions">
              <button className="secondary" onClick={closePairing}>Vazgeç</button>
              <button
                className="primary"
                disabled={busy || pairingCode.length !== 8}
                onClick={() => void execute(
                  () => api.approvePairing(terminalId, pairingCode),
                  "Müşteri ekranı eşleştirildi.",
                ).then((completed) => { if (completed) closePairing(); })}
              >{busy ? "Eşleştiriliyor…" : "Ekranı eşleştir"}</button>
            </div>
            <div className="dialog-danger-zone">
              <strong>Bağlantıyı sonlandır</strong>
              <span>Yetki kaldırıldığında müşteri ekranı yeniden kod ister.</span>
              <button
                className="danger-text"
                disabled={busy}
                onClick={() => void execute(
                  () => api.revokeDisplay(terminalId),
                  "Ekran yetkisi kaldırıldı.",
                ).then((completed) => { if (completed) closePairing(); })}
              >Aktif ekran yetkisini kaldır</button>
            </div>
          </div>
        </div>
      )}
    </main>
  );
}

function ExperiencePage({
  terminalId,
  displayName,
  capabilities,
  path,
  backendStatus,
  onLogout,
}: {
  terminalId: string;
  displayName: string;
  capabilities: readonly string[];
  path: string;
  backendStatus: BackendStatus;
  onLogout: () => Promise<void>;
}) {
  const capabilitySet = useMemo(() => new Set(capabilities), [capabilities]);
  const isManagerRoute = path === "/catalog" || path === "/system-health";
  const canOpenRoute = isManagerRoute
    ? capabilitySet.has("catalog.manage")
    : capabilitySet.has("pos.cashier.mutate");
  const session: ShellSession = {
    status: "authenticated",
    identity: {
      branchName: "Şube bağlamı",
      terminalName: `Terminal ${terminalId.slice(0, 8).toUpperCase()}`,
      userName: displayName,
      roleLabel: isManagerRoute ? "Manager" : "Kasiyer / Operasyon",
      capabilities: capabilitySet,
    } satisfies ShellIdentity,
  };
  const authorization: RouteAuthorization = canOpenRoute ? { status: "authorized" } : {
    status: "forbidden",
    onReturn: () => { window.location.href = "/"; },
  };
  const [lastOnlineSync, setLastOnlineSync] = useState<string | null>(() => backendStatus === "online" ? new Date().toISOString() : null);

  useEffect(() => {
    if (backendStatus === "online") {
      setLastOnlineSync(new Date().toISOString());
    }
  }, [backendStatus]);

  const connectivity: Connectivity = backendStatus === "online"
    ? { status: "online" }
    : backendStatus === "offline"
      ? { status: "offline", onRetry: () => window.location.reload() }
      : { status: "reconnecting" };
  const freshness: Freshness = backendStatus === "online"
    ? {
        status: "fresh",
        dateTime: lastOnlineSync ?? new Date().toISOString(),
        label: "Çevrimiçi doğrulandı",
      }
    : {
        status: "stale",
        dateTime: lastOnlineSync ?? "—",
        label: lastOnlineSync ? "Bağlantı kesildi" : "Bağlantı bekleniyor",
        onRefresh: () => window.location.reload(),
      };
  const navigation: readonly ShellNavigationItem[] = [
    { id: "sales", label: "Kasa", href: "/", symbol: "₺", requiredCapability: "pos.cashier.mutate" },
    { id: "tables", label: "Masalar", href: "/tables", symbol: "▦", requiredCapability: "pos.cashier.mutate" },
    { id: "billing", label: "Hesap", href: "/billing", symbol: "÷", requiredCapability: "pos.cashier.mutate" },
    { id: "kitchen", label: "Mutfak", href: "/kitchen", symbol: "◇", requiredCapability: "pos.cashier.mutate" },
    { id: "catalog", label: "Menü", href: "/catalog", symbol: "≡", requiredCapability: "catalog.manage" },
    { id: "system-health", label: "Sistem", href: "/system-health", symbol: "✚", requiredCapability: "catalog.manage" },
  ];
  const title = path === "/tables" ? "Masa yönetimi" : path === "/billing" ? "Hesap bölme" : path === "/catalog" ? "Menü ve katalog" : path === "/kitchen" ? "Mutfak ve operasyon" : path === "/system-health" ? "Sistem sağlığı" : "Kasa satış";
  const description = path === "/tables" ? "Salon, masa durumu ve servis akışı" : path === "/billing" ? "Kişi, ürün veya tutar bazlı hesap paylaştırma" : path === "/catalog" ? "Fiyat, ürün ve modifier kayıtları" : path === "/kitchen" ? "Ticket, yazıcı kurtarma ve operasyon sağlığı" : path === "/system-health" ? "Veritabanı, disk ve yedekleme durumu" : "Gerçek zamanlı sipariş ve müşteri ekranı";

  return <ProductionShell
    session={session}
    authorization={authorization}
    connectivity={connectivity}
    freshness={freshness}
    navigation={navigation}
    activeNavigationId={path === "/tables" ? "tables" : path === "/billing" ? "billing" : path === "/catalog" ? "catalog" : path === "/kitchen" ? "kitchen" : path === "/system-health" ? "system-health" : "sales"}
    workspaceTitle={title}
    workspaceDescription={description}
    headerActions={<><a className="experience-header-link" href="/display" target="alkaros-customer-display">Müşteri ekranı</a><button className="experience-header-button" type="button" onClick={() => void onLogout()}>Çıkış</button></>}
  >
    {path === "/tables" && <TableRoute terminalId={terminalId} canManage={canOpenRoute} />}
    {path === "/billing" && <BillingRoute terminalId={terminalId} canManage={canOpenRoute} />}
    {path === "/catalog" && <CatalogRoute canManage={canOpenRoute} />}
    {path === "/kitchen" && <KitchenRoute terminalId={terminalId} canOperate={canOpenRoute} />}
    {path === "/system-health" && <SystemHealthRoute terminalId={terminalId} canView={canOpenRoute} />}
    {!(["/", "/tables", "/billing", "/catalog", "/kitchen", "/system-health"] as readonly string[]).includes(path) && <div className="experience-not-found">Bu çalışma alanı bulunamadı.</div>}
  </ProductionShell>;
}

function TableRoute({ terminalId, canManage }: { terminalId: string; canManage: boolean }) {
  const client = useMemo(() => createTableManagementClient(terminalId), [terminalId]);
  const [state, setState] = useState<TableWorkspaceState>("loading");
  const [zones, setZones] = useState<Awaited<ReturnType<typeof client.listZones>>>([]);
  const [tables, setTables] = useState<Awaited<ReturnType<typeof client.listTables>>>([]);
  const [selectedZoneId, setSelectedZoneId] = useState<string>("all");
  const [floorPlan, setFloorPlan] = useState<FloorPlan | undefined>();
  const [floorPlanBusy, setFloorPlanBusy] = useState(false);
  const [floorPlanError, setFloorPlanError] = useState<string>();
  const [errorMessage, setErrorMessage] = useState<string>();
  const [lastUpdated, setLastUpdated] = useState<string>();
  const [selectedTableId, setSelectedTableId] = useState<string | null>(null);
  const [orderBusy, setOrderBusy] = useState(false);
  const [orderError, setOrderError] = useState<string>();

  const loadFloorPlanForZone = useCallback(async (zoneId: string, currentZones: Awaited<ReturnType<typeof client.listZones>>) => {
    const targetZoneId = (zoneId === "all" && currentZones.length > 0) ? currentZones[0].zoneId : zoneId;
    if (targetZoneId && targetZoneId !== "all") {
      try {
        const plan = await client.getFloorPlan(targetZoneId);
        setFloorPlan(plan);
      } catch {
        setFloorPlan(undefined);
      }
    } else {
      setFloorPlan(undefined);
    }
  }, [client]);

  const load = useCallback(async () => {
    setState("loading");
    setErrorMessage(undefined);
    try {
      const [nextZones, nextTables] = await Promise.all([client.listZones(), client.listTables()]);
      setZones(nextZones);
      setTables(nextTables);
      setSelectedTableId((current) => current && nextTables.some((table) => table.tableId === current) ? current : nextTables[0]?.tableId ?? null);
      await loadFloorPlanForZone(selectedZoneId, nextZones);
      setLastUpdated(new Date().toISOString());
      setState(nextTables.length ? "ready" : "empty");
    } catch (reason) {
      const status = (reason as { status?: number }).status;
      setState(status === 0 ? "offline" : status === 401 ? "unauthorized" : status === 409 ? "stale" : "error");
      setErrorMessage(reason instanceof Error ? reason.message : "Masa verisi alınamadı.");
    }
  }, [client, selectedZoneId, loadFloorPlanForZone]);

  useEffect(() => { void load(); }, [load]);

  const handleSelectZone = (zoneId: string) => {
    setSelectedZoneId(zoneId);
    void loadFloorPlanForZone(zoneId, zones);
  };

  const mutate = async (action: () => Promise<unknown>) => { await action(); await load(); };
  const handleSaveFloorPlan = async (zoneId: string, input: SaveFloorPlanInput): Promise<SaveFloorPlanResult> => {
    setFloorPlanBusy(true);
    setFloorPlanError(undefined);
    try {
      const result = await client.saveFloorPlan(zoneId, input);
      setFloorPlan(result.floorPlan);
      return result;
    } catch (reason) {
      const msg = reason instanceof Error ? reason.message : "Kat planı kaydedilemedi.";
      setFloorPlanError(msg);
      throw reason;
    } finally {
      setFloorPlanBusy(false);
    }
  };
  const selectedTable = tables.find((table) => table.tableId === selectedTableId) ?? null;
  const canStartTableOrder = selectedTable?.active === true
    && (selectedTable.status === "Available" || (selectedTable.status === "Occupied" && selectedTable.currentOrderId !== null));
  const startTableOrder = async () => {
    if (!selectedTable || !canManage || !canStartTableOrder) return;
    setOrderBusy(true);
    setOrderError(undefined);
    try {
      await api.startTableOrder(terminalId, selectedTable.tableId, selectedTable.rowVersion);
      window.location.assign("/");
    } catch (reason) {
      setOrderError(reason instanceof Error ? reason.message : "Masa siparişi açılamadı.");
      await load().catch(() => undefined);
    } finally {
      setOrderBusy(false);
    }
  };
  return <div className="table-route">
    <TableWorkspace
      state={state}
      zones={zones}
      tables={tables}
      canManage={canManage}
      selectedTableId={selectedTableId}
      onSelectTable={(tableId) => { setSelectedTableId(tableId); setOrderError(undefined); }}
      selectedZoneId={selectedZoneId}
      onSelectZone={handleSelectZone}
      onRefresh={load}
      onCreateZone={canManage ? (input: CreateZoneInput) => mutate(() => client.createZone(input)) : undefined}
      onCreateTable={canManage ? (input: CreateTableInput) => mutate(() => client.createTable(input)) : undefined}
      onAction={canManage ? (request: TableActionRequest) => mutate(() => client.execute(request)) : undefined}
      floorPlan={floorPlan}
      floorPlanBusy={floorPlanBusy}
      floorPlanError={floorPlanError}
      onSaveFloorPlan={canManage ? handleSaveFloorPlan : undefined}
      errorMessage={errorMessage}
      lastUpdated={lastUpdated}
    />
    {selectedTable && state === "ready" && <section className="table-order-bridge" aria-label="Masa sipariş bağlamı">
      <div>
        <span className="table-order-bridge__kicker">SİPARİŞ BAĞLAMI</span>
        <strong>{selectedTable.tableNumber}</strong>
        <p>{selectedTable.currentOrderId ? "Bu masadaki taslak siparişe devam edin." : "Bu masa seçildiğinde sipariş ve mutfak kaydı birlikte açılır."}</p>
      </div>
      <button
        className="primary"
        type="button"
        disabled={!canManage || !canStartTableOrder || orderBusy}
        aria-busy={orderBusy}
        onClick={() => void startTableOrder()}
      >{orderBusy ? "Sipariş hazırlanıyor…" : selectedTable.currentOrderId ? "Siparişe devam et" : "Masada sipariş aç"}</button>
      {!canStartTableOrder && <small className="table-order-bridge__reason">Masa şu anda sipariş almaya uygun değil; sunucu durumu değiştirilmeden işlem yapılamaz.</small>}
      {orderError && <p className="table-order-bridge__error" role="alert">{orderError}</p>}
    </section>}
  </div>;
}

function BillingRoute({ terminalId, canManage }: { terminalId: string; canManage: boolean }) {
  const searchParams = useMemo(() => new URLSearchParams(window.location.search), []);
  const initialBillId = searchParams.get("billId") || localStorage.getItem("alkaros.current-bill-id") || "00000000-0000-0000-0000-000000000001";
  const [billId, setBillId] = useState<string>(initialBillId);
  const client = useMemo(() => createBillingSplitClient(terminalId, billId), [terminalId, billId]);
  const [state, setState] = useState<BillSplitWorkspaceState>("loading");
  const [design, setDesign] = useState<BillSplitDesign | null>(null);
  const [errorMessage, setErrorMessage] = useState<string>();
  const [lastUpdated, setLastUpdated] = useState<string>();

  const load = useCallback(async () => {
    setState("loading");
    setErrorMessage(undefined);
    try {
      const orderParam = searchParams.get("orderId");
      let nextDesign: BillSplitDesign;
      if (orderParam) {
        nextDesign = await client.createFromOrder(orderParam);
      } else {
        nextDesign = await client.get();
      }
      setDesign(nextDesign);
      if (nextDesign.billId && nextDesign.billId !== billId) {
        setBillId(nextDesign.billId);
        localStorage.setItem("alkaros.current-bill-id", nextDesign.billId);
      }
      setLastUpdated(new Date().toISOString());
      setState("ready");
    } catch (reason) {
      const status = (reason as { status?: number }).status;
      setState(status === 0 ? "offline" : status === 401 ? "unauthorized" : status === 409 ? "stale" : "error");
      setErrorMessage(reason instanceof Error ? reason.message : "Hesap bölme verisi alınamadı.");
    }
  }, [client, searchParams, billId]);

  useEffect(() => { void load(); }, [load]);

  const owners: readonly SplitOwnerOption[] = useMemo(() => {
    const existingOwners = (design?.allocations || [])
      .filter(a => a.ownerId && a.ownerKind === "Person")
      .map((a, index) => ({
        kind: "Person" as const,
        ownerId: a.ownerId!,
        label: `${index + 1}. Kişi`
      }));

    const count = Math.max(existingOwners.length, 4);
    const result: SplitOwnerOption[] = [];
    for (let i = 1; i <= count; i++) {
      const hex = i.toString(16).padStart(12, '0');
      const id = `00000000-0000-0000-0000-${hex}`;
      const existing = existingOwners.find(o => o.ownerId === id);
      result.push(existing || { kind: "Person", ownerId: id, label: `${i}. Kişi` });
    }
    return result;
  }, [design]);

  const handleSave = async (request: SaveSplitRequest, currentDesign: BillSplitDesign): Promise<BillSplitDesign> => {
    const targetClient = currentDesign.billId && currentDesign.billId !== billId
      ? createBillingSplitClient(terminalId, currentDesign.billId)
      : client;
    const updated = await targetClient.save(request, currentDesign);
    setDesign(updated);
    if (updated.billId && updated.billId !== billId) {
      setBillId(updated.billId);
      localStorage.setItem("alkaros.current-bill-id", updated.billId);
    }
    return updated;
  };

  const handleClear = async (currentDesign: BillSplitDesign): Promise<BillSplitDesign> => {
    const targetClient = currentDesign.billId && currentDesign.billId !== billId
      ? createBillingSplitClient(terminalId, currentDesign.billId)
      : client;
    const cleared = await targetClient.clear(currentDesign);
    setDesign(cleared);
    return cleared;
  };

  return (
    <div className="billing-route">
      <BillSplitWorkspace
        state={state}
        design={design}
        owners={owners}
        canMutate={canManage}
        onRefresh={load}
        onSave={handleSave}
        onClear={handleClear}
        errorMessage={errorMessage}
        lastUpdated={lastUpdated}
      />
    </div>
  );
}

const emptyCatalogData: CatalogData = {
  categories: [], taxes: [], products: [], modifierGroups: [], modifiers: [], prices: [],
};

function CatalogRoute({ canManage }: { canManage: boolean }) {
  const client = useMemo(() => createCatalogManagementClient(), []);
  const [state, setState] = useState<CatalogWorkspaceState>("loading");
  const [data, setData] = useState<CatalogData>(emptyCatalogData);
  const [errorMessage, setErrorMessage] = useState<string>();
  const [lastUpdated, setLastUpdated] = useState<string>();
  const load = useCallback(async () => {
    if (!canManage) { setState("unauthorized"); return; }
    setState("loading");
    setErrorMessage(undefined);
    try {
      setData(await client.load());
      setLastUpdated(new Date().toISOString());
      setState("ready");
    } catch (reason) {
      const status = (reason as { status?: number }).status;
      setState(status === 0 ? "offline" : status === 401 ? "unauthorized" : status === 409 ? "conflict" : "error");
      setErrorMessage(reason instanceof Error ? reason.message : "Katalog verisi alınamadı.");
    }
  }, [canManage, client]);
  useEffect(() => { void load(); }, [load]);
  const create = async (input: CatalogCreateInput) => { await client.create(input); await load(); };
  const setAvailability = async (productId: string, isAvailable: boolean) => { await client.setAvailability(productId, isAvailable); await load(); };
  return <CatalogWorkspace state={state} data={data} canManage={canManage} onRefresh={load} onCreate={canManage ? create : undefined} onSetAvailability={canManage ? setAvailability : undefined} errorMessage={errorMessage} lastUpdated={lastUpdated} />;
}

function SystemHealthRoute({ terminalId, canView }: { terminalId: string; canView: boolean }) {
  const [state, setState] = useState<SystemHealthState>("loading");
  const [health, setHealth] = useState<KitchenData["health"]>(null);
  const [backups, setBackups] = useState<KitchenData["backups"]>([]);
  const [errorMessage, setErrorMessage] = useState<string>();
  const [lastUpdated, setLastUpdated] = useState<string>();
  const load = useCallback(async () => {
    if (!canView) { setState("unauthorized"); return; }
    setState("loading");
    setErrorMessage(undefined);
    try {
      const configuration = await loadKitchenRuntimeConfiguration(terminalId);
      const client = createKitchenOperationsClient(terminalId, configuration.kitchenStationId);
      const next = await client.load();
      setHealth(next.health);
      setBackups(next.backups);
      setLastUpdated(new Date().toISOString());
      setState("ready");
    } catch (reason) {
      const status = (reason as { status?: number }).status;
      setHealth(null);
      setBackups([]);
      setState(status === 0 ? "offline" : status === 401 ? "unauthorized" : "error");
      setErrorMessage(reason instanceof Error ? reason.message : "Sağlık verisi alınamadı.");
    }
  }, [canView, terminalId]);
  useEffect(() => { void load(); }, [load]);
  return <SystemHealthWorkspace state={state} health={health} backups={backups} onRefresh={load} errorMessage={errorMessage} lastUpdated={lastUpdated} />;
}

const emptyKitchenData: KitchenData = { tickets: [], printers: [], routes: [], unknownDeliveries: [], health: null, backups: [] };

function KitchenRoute({ terminalId, canOperate }: { terminalId: string; canOperate: boolean }) {
  const [stationId, setStationId] = useState("");
  const [client, setClient] = useState<KitchenOperationsClient | null>(null);
  const [state, setState] = useState<KitchenWorkspaceState>("loading");
  const [data, setData] = useState<KitchenData>(emptyKitchenData);
  const [errorMessage, setErrorMessage] = useState<string>();
  const [lastUpdated, setLastUpdated] = useState<string>();
  const load = useCallback(async () => {
    if (!canOperate) { setState("unauthorized"); return; }
    setState("loading");
    setErrorMessage(undefined);
    try {
      const configuration = await loadKitchenRuntimeConfiguration(terminalId);
      const nextClient = createKitchenOperationsClient(terminalId, configuration.kitchenStationId);
      const next = await nextClient.load();
      setStationId(configuration.kitchenStationId);
      setClient(nextClient);
      setData(next);
      setLastUpdated(new Date().toISOString());
      setState(next.tickets.length || next.unknownDeliveries.length ? "ready" : "empty");
    } catch (reason) {
      const status = (reason as { status?: number }).status;
      setStationId("");
      setClient(null);
      setData(emptyKitchenData);
      setState(status === 0 ? "offline" : status === 401 ? "unauthorized" : status === 409 ? "conflict" : "error");
      setErrorMessage(reason instanceof Error ? reason.message : "Mutfak verisi alınamadı.");
    }
  }, [canOperate, terminalId]);
  useEffect(() => { void load(); }, [load]);
  return <KitchenOperationsWorkspace
    state={state}
    stationId={stationId || "Mutfak"}
    data={data}
    canOperate={canOperate}
    canManageReprints={canOperate}
    onRefresh={load}
    onTransitionItem={canOperate && client ? async (ticket, item, target) => { await client.transitionItem(ticket.id, item.id, target, ticket.rowVersion, item.rowVersion); await load(); } : undefined}
    onTransitionTicket={canOperate && client ? async (ticket, target, reason) => { await client.transitionTicket(ticket.id, target, ticket.rowVersion, reason); await load(); } : undefined}
    onApproveReprint={canOperate && client ? async (delivery, reason) => { await client.approveReprint(delivery.id, reason); await load(); } : undefined}
    onRejectReprint={canOperate && client ? async (delivery, reason) => { await client.rejectReprint(delivery.id, reason); await load(); } : undefined}
    errorMessage={errorMessage}
    lastUpdated={lastUpdated}
  />;
}

function CustomerDisplay() {
  const [displayId] = useState(() => savedId("alkaros.customer-display-id"));
  const [pairing, setPairing] = useState<PairingCreated | null>(null);
  const [pairingError, setPairingError] = useState("");
  const [displaySession, setDisplaySession] = useState<DisplaySession>("checking");
  const [freshness, setFreshness] = useState<DisplayFreshness>({
    snapshot: null,
    lastSuccessAt: null,
    connectionLost: false,
  });
  const [completedConcealed, setCompletedConcealed] = useState(false);
  const initialized = useRef(false);
  const pairingRequestInFlight = useRef(false);
  const hasSuccessfulSnapshot = useRef(false);

  const beginPairing = useCallback(async () => {
    if (pairingRequestInFlight.current) return;
    pairingRequestInFlight.current = true;
    setDisplaySession("pairing");
    setPairing(null);
    setPairingError("");
    try {
      setPairing(await api.createPairing(displayId));
    } catch (reason) {
      setPairingError(reason instanceof Error ? reason.message : "Eşleştirme başlatılamadı.");
      setDisplaySession("error");
    } finally {
      pairingRequestInFlight.current = false;
    }
  }, [displayId]);

  const refresh = useCallback(async () => {
    try {
      const snapshot = await api.snapshot(displayId);
      hasSuccessfulSnapshot.current = true;
      setFreshness(afterSnapshot(snapshot, Date.now()));
      setPairing(null);
      setPairingError("");
      setDisplaySession("ready");
    } catch (reason) {
      setFreshness((previous) => afterFailure(previous, Date.now()));
      if (reason instanceof ApiError && reason.status === 401) {
        await beginPairing();
      } else if (!hasSuccessfulSnapshot.current) {
        setPairingError(reason instanceof Error ? reason.message : "Ekran bilgisi alınamadı.");
        setDisplaySession("error");
      }
    }
  }, [beginPairing, displayId]);

  useEffect(() => {
    if (initialized.current) return;
    initialized.current = true;
    void refresh();
  }, [refresh]);

  useEffect(() => {
    if (!pairing || displaySession !== "pairing") return;
    const timer = window.setInterval(() => {
      api.completePairing(pairing.requestId, pairing.secret)
        .then(() => {
          setPairing(null);
          return refresh();
        })
        .catch((reason: unknown) => {
          if (reason instanceof ApiError && reason.status === 409) return;
          setPairing(null);
          setPairingError(reason instanceof Error ? reason.message : "Eşleştirme tamamlanamadı.");
          setDisplaySession("error");
        });
    }, 2_000);
    return () => window.clearInterval(timer);
  }, [displaySession, pairing, refresh]);

  useEffect(() => {
    if (!pairing) return;
    const expiresIn = Math.max(0, new Date(pairing.expiresAt).getTime() - Date.now());
    const timer = window.setTimeout(() => void beginPairing(), expiresIn);
    return () => window.clearTimeout(timer);
  }, [beginPairing, pairing]);

  useEffect(() => {
    if (displaySession !== "ready") return;
    const polling = window.setInterval(() => void refresh(), 5_000);
    const connection = new HubConnectionBuilder()
      .withUrl("/hubs/customer-display")
      .withAutomaticReconnect([0, 1_000, 3_000, 5_000])
      .configureLogging(LogLevel.Warning)
      .build();
    connection.on("SnapshotChanged", () => void refresh());
    connection.onreconnecting(() => setFreshness((previous) => ({ ...previous, connectionLost: true })));
    void connection.start().catch(() => setFreshness((previous) => ({ ...previous, connectionLost: true })));
    return () => {
      window.clearInterval(polling);
      void connection.stop();
    };
  }, [displaySession, refresh]);

  useEffect(() => {
    if (!freshness.connectionLost || freshness.lastSuccessAt === null) return;
    const remaining = Math.max(0, 10_000 - (Date.now() - freshness.lastSuccessAt));
    const timer = window.setTimeout(
      () => setFreshness((previous) => afterFailure(previous, Date.now())),
      remaining,
    );
    return () => window.clearTimeout(timer);
  }, [freshness.connectionLost, freshness.lastSuccessAt]);

  useEffect(() => {
    const markOffline = () => setFreshness((previous) => afterFailure(previous, Date.now()));
    const markOnline = () => void refresh();
    window.addEventListener("offline", markOffline);
    window.addEventListener("online", markOnline);
    return () => {
      window.removeEventListener("offline", markOffline);
      window.removeEventListener("online", markOnline);
    };
  }, [refresh]);

  useEffect(() => {
    if (freshness.snapshot?.state !== "Completed") {
      setCompletedConcealed(false);
      return;
    }
    setCompletedConcealed(false);
    const timer = window.setTimeout(() => setCompletedConcealed(true), 7_000);
    return () => window.clearTimeout(timer);
  }, [freshness.snapshot?.revision, freshness.snapshot?.state]);

  if (displaySession === "checking") {
    return (
      <DisplayMessageScreen
        className="pairing-screen"
        kicker="BAĞLANTI KONTROLÜ"
        title="Müşteri ekranı hazırlanıyor"
        description="Güvenli ekran oturumu ve güncel sipariş bilgisi doğrulanıyor."
        busy
      />
    );
  }

  if (displaySession === "pairing") {
    return (
      <main className="display-shell pairing-screen">
        <DisplayBrand />
        <section className="display-center-card" aria-live="polite" aria-busy={!pairing}>
          <span className="display-kicker">GÜVENLİ EKRAN BAĞLANTISI</span>
          <h1>Müşteri ekranını<br />kasaya bağlayın</h1>
          {pairing ? (
            <>
              <p>Bu kodu kasa ekranındaki “Müşteri ekranı bağlantısı” alanına girin.</p>
              <div className="pairing-code">{pairing.code}</div>
              <small>Kod iki dakika sonra otomatik olarak yenilenir.</small>
            </>
          ) : <><span className="loading-spinner" aria-hidden="true" /><p>Güvenli eşleştirme kodu hazırlanıyor…</p></>}
        </section>
      </main>
    );
  }

  if (displaySession === "error") {
    return (
      <DisplayMessageScreen
        className="unavailable-screen"
        kicker="BAĞLANTI KURULAMADI"
        title="Ekran bilgisi alınamadı"
        description={pairingError || "Sunucuya ulaşılamadı. Bağlantıyı kontrol edip yeniden deneyin."}
        action={<button className="display-retry" onClick={() => void refresh()}>Tekrar dene</button>}
        alert
      />
    );
  }

  const snapshot = freshness.snapshot;
  if (!snapshot) {
    return <DisplayUnavailable onRetry={refresh} />;
  }

  const presentation = displayPresentation(snapshot);

  if (presentation === "unavailable") {
    return <DisplayUnavailable onRetry={refresh} />;
  }

  if (presentation === "completed" && !completedConcealed) {
    return (
      <DisplayMessageScreen
        className="completed-screen"
        kicker="SİPARİŞ TAMAMLANDI"
        title="Teşekkür ederiz."
        description={snapshot.message || "Siparişiniz tamamlandı. Afiyet olsun."}
        icon="✓"
      />
    );
  }

  if (presentation === "idle" || (presentation === "completed" && completedConcealed)) {
    return (
      <main className="display-shell idle-screen">
        <DisplayBrand />
        {freshness.connectionLost && <ConnectionBanner />}
        <section className="display-center-card welcome-card">
          <span className="display-kicker">HOŞ GELDİNİZ</span>
          <h1>Siparişiniz için hazırız.</h1>
          <p>Kasiyer işlemi başlattığında ürün ve toplam bilgileri burada canlı olarak görünecek.</p>
          <div className="privacy-note">Bu ekran yalnız sipariş içeriğini gösterir.</div>
        </section>
      </main>
    );
  }

  const paying = presentation === "paying";
  const submitted = presentation === "active" && !snapshot.editable;
  return (
    <main className={`display-shell active-display ${paying ? "paying-display" : ""}`}>
      {freshness.connectionLost && <ConnectionBanner />}
      <header className="display-header">
        <DisplayBrand />
        <div className="display-order-number">
          <span>Sipariş</span>
          <strong>{snapshot.orderNumber}</strong>
        </div>
        <div className={`display-phase ${paying ? "paying" : submitted ? "submitted" : "active"}`} role="status">
          {paying ? "Ödeme işlemi sürüyor" : submitted ? "Sipariş kasadan gönderildi" : "Sipariş güncelleniyor"}
        </div>
      </header>
      <section className="display-order-grid">
        <div className="display-lines-card">
          <div className="display-section-title">
            <div><span className="display-kicker">SİPARİŞİNİZ</span><h1>Ürünler</h1></div>
            <strong>{formatQuantity(snapshot.lines.reduce((sum, line) => sum + line.quantity, 0))} adet</strong>
          </div>
          <div className="display-table-head"><span>Ürün</span><span>Adet</span><span>Tutar</span></div>
          <div className="display-lines-scroll">
            {snapshot.lines.map((line) => (
              <div className="display-line" key={line.itemId}>
                <div><strong>{line.name}</strong><small>{formatMoney(line.lineTotal / line.quantity, snapshot.currency)} · KDV dahil</small></div>
                <span className="display-quantity">{formatQuantity(line.quantity)}</span>
                <strong>{formatMoney(line.lineTotal, snapshot.currency)}</strong>
              </div>
            ))}
          </div>
        </div>
        <aside className="display-total-card">
          <span className="display-kicker">{paying ? "ÖDEMEDEKİ TUTAR" : "ÖDENECEK TOPLAM"}</span>
          <strong>{formatMoney(snapshot.total, snapshot.currency)}</strong>
          <div className="display-total-breakdown">
            <div><span>Ara toplam</span><span>{formatMoney(snapshot.subtotal, snapshot.currency)}</span></div>
            {snapshot.discountTotal > 0 && (
              <div><span>İndirim</span><span>−{formatMoney(snapshot.discountTotal, snapshot.currency)}</span></div>
            )}
            <div><span>KDV</span><span>{formatMoney(snapshot.taxTotal, snapshot.currency)}</span></div>
          </div>
          <div className="display-message"><span>{paying ? "…" : "✓"}</span><p>{snapshot.message}</p></div>
          <small>Canlı güncelleme · v{snapshot.revision}</small>
        </aside>
      </section>
    </main>
  );
}

function DisplayBrand() {
  return <div className="display-brand"><span>A</span><strong>ALKAROS</strong></div>;
}

function DisplayMessageScreen({
  className,
  kicker,
  title,
  description,
  action,
  icon,
  busy = false,
  alert = false,
}: {
  className: string;
  kicker: string;
  title: string;
  description: string;
  action?: ReactNode;
  icon?: string;
  busy?: boolean;
  alert?: boolean;
}) {
  return (
    <main className={`display-shell ${className}`}>
      <DisplayBrand />
      <section
        className="display-center-card"
        role={alert ? "alert" : "status"}
        aria-live={alert ? "assertive" : "polite"}
        aria-busy={busy}
      >
        {busy && <span className="loading-spinner" aria-hidden="true" />}
        {icon && <span className="completion-icon" aria-hidden="true">{icon}</span>}
        <span className="display-kicker">{kicker}</span>
        <h1>{title}</h1>
        <p>{description}</p>
        {action}
      </section>
    </main>
  );
}

function DisplayUnavailable({ onRetry }: { onRetry: () => Promise<void> }) {
  return (
    <DisplayMessageScreen
      className="unavailable-screen"
      kicker="GÜNCEL VERİ YOK"
      title="Bilgi güncellenemiyor"
      description="Eski ürün ve tutarlar güvenlik nedeniyle ekrandan kaldırıldı. Bağlantıyı kontrol edip yeniden deneyin."
      action={<button className="display-retry" onClick={() => void onRetry()}>Tekrar dene</button>}
      icon="!"
      alert
    />
  );
}

function ConnectionBanner() {
  return <div className="connection-banner" role="status">Bağlantı yenileniyor · güncel olmayan tutarlar otomatik temizlenecek</div>;
}
