import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type FormEvent,
  type KeyboardEvent as ReactKeyboardEvent,
} from "react";
import { ApiError, api } from "../api";
import type { CatalogProduct, DisplaySnapshot } from "../contracts";
import { stateText } from "../strings";
import { formatMoney, formatQuantity, grossUnitPrice } from "../format";
import { savedId } from "../storage";
import { ExperiencePage, type BackendStatus } from "./workspace";

type CashierSession = "checking" | "anonymous" | "ready";
type FeedbackKind = "error" | "success" | "conflict" | "unauthorized";

const focusableSelector = [
  "button:not([disabled])",
  "input:not([disabled])",
  "[href]",
  "[tabindex]:not([tabindex='-1'])",
].join(",");

export function Cashier() {
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
          <button onClick={() => { setError(""); setNotice(""); }} aria-label={stateText.dismissMessage}>×</button>
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
