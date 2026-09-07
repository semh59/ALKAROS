import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { ApiError, api } from "../api";
import type { CatalogProduct, NfcOrderResult } from "../contracts";
import { formatMoney, formatQuantity } from "../format";
import { Icon } from "../design-system";
import "./nfc-order.css";

type PageState = "loading" | "browsing" | "submitting" | "placed" | "blocked" | "error";

/**
 * V14-NFC-003. The customer-facing screen an NFC tap opens
 * (`/nfc/{tableId}`) — no login, no cashier/waiter session, table id comes
 * only from the URL the physical tag encodes. Per the "thin frontend"
 * principle (docs/design/foundations.md, section 0): this component holds
 * only UI-local state (the cart before it is ever sent); once submitted,
 * the confirmation screen shows exactly what `V14-NFC-001`'s API returned,
 * nothing re-derived.
 */
function tableIdFromPath(pathname: string): string {
  const match = /^\/nfc\/([^/]+)/.exec(pathname);
  return match ? decodeURIComponent(match[1]) : "";
}

export function NfcOrder() {
  const [tableId] = useState(() => tableIdFromPath(window.location.pathname));
  const [state, setState] = useState<PageState>("loading");
  const [products, setProducts] = useState<CatalogProduct[]>([]);
  const [cart, setCart] = useState<Record<string, number>>({});
  const [message, setMessage] = useState("");
  const [result, setResult] = useState<NfcOrderResult | null>(null);
  // Stable for the lifetime of one in-flight submission attempt so a retry
  // after a dropped connection replays the same order instead of starting a
  // second one (V14-NFC-001's ux_orders_table_submission index); reset once
  // that attempt either succeeds or the customer starts a fresh round.
  const submissionIdRef = useRef<string | null>(null);

  const loadCatalog = useCallback(async () => {
    setState("loading");
    setMessage("");
    try {
      const list = await api.nfcCatalog(tableId);
      setProducts(list);
      setState("browsing");
    } catch (reason) {
      setMessage(reason instanceof Error ? reason.message : "Menü yüklenemedi.");
      setState("error");
    }
  }, [tableId]);

  useEffect(() => {
    if (!tableId) {
      setMessage("Bu bağlantı geçersiz. Lütfen garsonu çağırın.");
      setState("error");
      return;
    }
    void loadCatalog();
  }, [tableId, loadCatalog]);

  const changeQuantity = (productId: string, delta: number) => {
    setCart((previous) => {
      const next = Math.max(0, (previous[productId] ?? 0) + delta);
      const updated = { ...previous };
      if (next === 0) delete updated[productId];
      else updated[productId] = next;
      return updated;
    });
  };

  const cartLines = useMemo(
    () =>
      products
        .filter((product) => (cart[product.productId] ?? 0) > 0)
        .map((product) => ({ product, quantity: cart[product.productId] })),
    [products, cart],
  );
  const total = cartLines.reduce((sum, line) => sum + line.product.unitPrice * line.quantity, 0);

  const submit = async () => {
    if (cartLines.length === 0) return;
    submissionIdRef.current ??= crypto.randomUUID();
    setState("submitting");
    setMessage("");
    try {
      const order = await api.placeNfcOrder(
        tableId,
        cartLines.map((line) => ({
          id: crypto.randomUUID(),
          productId: line.product.productId,
          quantity: line.quantity,
        })),
        submissionIdRef.current,
      );
      submissionIdRef.current = null;
      setResult(order);
      setState("placed");
    } catch (reason) {
      if (reason instanceof ApiError && reason.code === "TABLE_NOT_AVAILABLE") {
        setMessage(reason.message);
        setState("blocked");
        return;
      }
      setMessage(reason instanceof Error ? reason.message : "Sipariş gönderilemedi.");
      setState("browsing");
    }
  };

  const startNewRound = () => {
    setCart({});
    setResult(null);
    submissionIdRef.current = null;
    setState("browsing");
  };

  if (state === "loading") {
    return (
      <main className="nfc-order-page">
        <div className="nfc-center-screen" role="status" aria-live="polite">
          <span className="nfc-loading-spinner" aria-hidden="true" />
          <p>Menü hazırlanıyor…</p>
        </div>
      </main>
    );
  }

  if (state === "error") {
    return (
      <main className="nfc-order-page">
        <div className="nfc-center-screen" role="alert">
          <Icon name="warning" />
          <h1>Menüye ulaşılamadı</h1>
          <p>{message}</p>
          {tableId && (
            <button className="nfc-secondary-button" onClick={() => void loadCatalog()}>
              Tekrar dene
            </button>
          )}
        </div>
      </main>
    );
  }

  if (state === "blocked") {
    return (
      <main className="nfc-order-page">
        <div className="nfc-center-screen" role="alert">
          <Icon name="warning" />
          <h1>Şu anda kendi kendine sipariş verilemiyor</h1>
          <p>{message}</p>
        </div>
      </main>
    );
  }

  if (state === "placed" && result) {
    return (
      <main className="nfc-order-page">
        <div className="nfc-confirmation">
          <div className="nfc-confirmation__icon"><Icon name="check" /></div>
          <h1>Siparişiniz alındı</h1>
          <p>Siparişiniz mutfağa iletildi, afiyet olsun.</p>
          <div className="nfc-confirmation__summary">
            {result.items.map((item) => (
              <div className="nfc-confirmation__summary-line" key={item.itemId}>
                <span>{formatQuantity(item.quantity)}× {item.productName}</span>
                <span>{formatMoney(item.totalPrice)}</span>
              </div>
            ))}
            <div className="nfc-confirmation__summary-total">
              <span>Toplam</span>
              <span>{formatMoney(result.totalAmount)}</span>
            </div>
          </div>
          <button className="nfc-secondary-button" onClick={startNewRound}>
            Başka bir şey eklemek istiyorum
          </button>
        </div>
      </main>
    );
  }

  return (
    <main className="nfc-order-page">
      <header className="nfc-header">
        <strong>ALKAROS</strong>
        <span>Masa siparişi</span>
      </header>
      {message && <div className="nfc-banner nfc-banner--danger" role="alert">{message}</div>}
      <div className="nfc-menu">
        {products.map((product) => {
          const quantity = cart[product.productId] ?? 0;
          return (
            <div className="nfc-product" key={product.productId}>
              <div className="nfc-product__info">
                <span className="nfc-product__name">{product.name}</span>
                <span className="nfc-product__price">{formatMoney(product.unitPrice)}</span>
              </div>
              <div className="nfc-stepper">
                <button
                  type="button"
                  aria-label={`${product.name} adedini azalt`}
                  disabled={quantity === 0 || state === "submitting"}
                  onClick={() => changeQuantity(product.productId, -1)}
                >
                  −
                </button>
                <span className="nfc-stepper__count" aria-live="polite">{quantity}</span>
                <button
                  type="button"
                  aria-label={`${product.name} adedini artır`}
                  disabled={state === "submitting"}
                  onClick={() => changeQuantity(product.productId, 1)}
                >
                  +
                </button>
              </div>
            </div>
          );
        })}
      </div>
      {cartLines.length > 0 && (
        <div className="nfc-cart-bar">
          <span className="nfc-cart-bar__total">
            {formatMoney(total)}
            <small>{formatQuantity(cartLines.reduce((sum, line) => sum + line.quantity, 0))} ürün</small>
          </span>
          <button
            type="button"
            className="nfc-primary-button"
            disabled={state === "submitting"}
            onClick={() => void submit()}
          >
            {state === "submitting" ? "Gönderiliyor…" : "Siparişi Gönder"}
          </button>
        </div>
      )}
    </main>
  );
}
