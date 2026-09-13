import { useEffect, useMemo, useState, type FormEvent } from "react";
import { ApiError } from "../../api";
import { Button, ModalDialog, StateMessage, TextField, ValidationSummary } from "../../design-system";
import { commonActions, kitchenReprintText, stateText } from "../../strings";
import {
  healthStatusLabel,
  itemStatusLabels,
  type KitchenHealthSnapshot,
  type KitchenTicket,
  type KitchenTicketItem,
  type KitchenUnknownDelivery,
  type KitchenWorkspaceProps,
} from "./models";
import "./kitchen-operations.css";

type Feedback = { tone: "success" | "error" | "conflict"; message: string } | null;
type AgeTone = "ok" | "warn" | "crit";
type Density = "auto" | "sparse" | "dense";

// V1-KDS-001: an open ticket/item count at or above this makes the board
// switch to dense mode on its own (fewer secondary details, so the screen
// itself doesn't turn into a "white-out" rail during a rush). Kitchen staff
// can always override with the density toggle; the override holds until the
// next new ticket arrives, then the automatic evaluation runs again fresh.
const AUTO_DENSE_OPEN_ITEM_THRESHOLD = 9;
// Matches KitchenPrintDispatchHostedService's own 5s dispatch rhythm closely
// enough that a ticket someone else advances shows up here without a manual
// refresh, without hammering the API every tick.
const POLL_INTERVAL_MS = 8_000;
const STAGES = ["Queued", "Preparing", "Ready", "Served"] as const;
// V1-KIT-009/V1-KDS-003: mirrors KitchenTicketItem.UndoWindow (10s) — the
// backend is the actual authority (a request past this shows a normal error
// like any other), this is only how long the affordance stays visible so
// kitchen staff isn't shown a button that would just fail.
const UNDO_WINDOW_MS = 10_000;

const DEFAULT_TARGET_PREP_MINUTES = 15;
const CRITICAL_TARGET_MULTIPLIER = 5 / 3;

function elapsedMinutesSince(iso: string, now: number): number {
  const started = Date.parse(iso);
  if (!Number.isFinite(started)) return 0;
  return Math.max(0, Math.floor((now - started) / 60_000));
}

function ageTone(minutes: number, targetMinutes: number): AgeTone {
  const target = targetMinutes > 0 ? targetMinutes : DEFAULT_TARGET_PREP_MINUTES;
  if (minutes >= target * CRITICAL_TARGET_MULTIPLIER) return "crit";
  if (minutes >= target) return "warn";
  return "ok";
}

function ageLabel(minutes: number): string {
  if (minutes < 60) return `${minutes} dk`;
  return `${Math.floor(minutes / 60)} sa ${minutes % 60} dk`;
}

const healthRank: Record<KitchenHealthSnapshot["databaseStatus"], number> = { Healthy: 0, Degraded: 1, Unhealthy: 2 };

function worstHealth(health: KitchenHealthSnapshot | null): KitchenHealthSnapshot["databaseStatus"] | null {
  if (!health) return null;
  return [health.databaseStatus, health.diskStatus, health.lastBackupStatus].reduce((worst, current) =>
    healthRank[current] > healthRank[worst] ? current : worst);
}

function nextItemState(status: KitchenTicketItem["status"]): KitchenTicketItem["status"] | null {
  if (status === "Queued") return "Preparing";
  if (status === "Preparing") return "Ready";
  if (status === "Ready") return "Served";
  return null;
}

function isConflict(error: unknown) {
  return error instanceof Error && /409|conflict|concurrent|version/i.test(error.message);
}

function compactId(value: string) {
  return value.length > 10 ? value.slice(0, 8) : value;
}

// V1-KDS-001: tickets carry an orderId but no table/check label — grouping
// by order is the closest available proxy to "one table's current round"
// without inventing data the backend contract does not provide ("backend
// is smart, frontend is dumb" — foundations.md §0). A real table label
// needs a small contract addition (KitchenTicketV1 has no TableId today) —
// a separate follow-up, not fabricated here.
function groupByOrder(tickets: readonly KitchenTicket[]): { orderId: string; tickets: KitchenTicket[] }[] {
  const order: string[] = [];
  const byOrder = new Map<string, KitchenTicket[]>();
  for (const ticket of tickets) {
    const existing = byOrder.get(ticket.orderId);
    if (existing) existing.push(ticket);
    else { byOrder.set(ticket.orderId, [ticket]); order.push(ticket.orderId); }
  }
  return order.map((orderId) => ({ orderId, tickets: byOrder.get(orderId)! }));
}

export function KitchenOperationsWorkspace({
  state,
  stationId,
  data,
  canAdvance,
  canOperate,
  canManageReprints,
  canSuspendAvailability,
  onRefresh,
  onTransitionItem,
  onUndoItem,
  onTransitionTicket,
  onApproveReprint,
  onRejectReprint,
  onCreateCategoryRoute,
  onSuspendProductAvailability,
  errorMessage: suppliedError,
  lastUpdated,
}: KitchenWorkspaceProps) {
  const [busyKey, setBusyKey] = useState<string | null>(null);
  const [feedback, setFeedback] = useState<Feedback>(null);
  const [selectedDelivery, setSelectedDelivery] = useState<KitchenUnknownDelivery | null>(null);
  const [decision, setDecision] = useState<"approve" | "reject" | null>(null);
  const [reason, setReason] = useState("");
  const [formErrors, setFormErrors] = useState<string[]>([]);
  const [now, setNow] = useState(() => Date.now());
  const [densityOverride, setDensityOverride] = useState<Density>("auto");
  const [cancelTarget, setCancelTarget] = useState<KitchenTicket | null>(null);
  const [cancelReason, setCancelReason] = useState("");
  const [suspendPromptOpen, setSuspendPromptOpen] = useState(false);
  const [suspendProductId, setSuspendProductId] = useState("");

  // V1-KDS-003: ticks every second (not 15s like before undo existed) so the
  // undo affordance's countdown and disappearance track the backend's real
  // 10s window closely — the age/timer displays are just as accurate this
  // way, not a regression.
  useEffect(() => {
    const timer = setInterval(() => setNow(Date.now()), 1_000);
    return () => clearInterval(timer);
  }, []);

  // Auto-refresh (the comparison report's own CRIT finding) — only polls
  // while the screen is ready/empty, never hammers the server on an
  // error/auth state.
  useEffect(() => {
    if (state !== "ready" && state !== "empty") return;
    const timer = setInterval(() => { void onRefresh(); }, POLL_INTERVAL_MS);
    return () => clearInterval(timer);
  }, [state, onRefresh]);

  // A manually chosen density resets whenever the ticket count changes (a
  // ticket arrived or left) — the automatic evaluation starts fresh again.
  useEffect(() => { setDensityOverride("auto"); }, [data.tickets.length]);

  const openItemCount = useMemo(
    () => data.tickets.reduce((sum, ticket) => sum + ticket.items.filter((item) => item.status !== "Cancelled" && item.status !== "Served").length, 0),
    [data.tickets],
  );
  const autoDense = openItemCount >= AUTO_DENSE_OPEN_ITEM_THRESHOLD;
  const density: Density = densityOverride === "auto" ? (autoDense ? "dense" : "sparse") : densityOverride;
  const isDense = density === "dense";

  const orderGroups = useMemo(() => groupByOrder(data.tickets), [data.tickets]);
  const overallHealth = worstHealth(data.health);

  // V1-KDS-002: the Goal's own scope - products from open tickets, not
  // every product in the catalog. A distinct product can appear on several
  // tickets/items; only the name is needed once each.
  const openProducts = useMemo(() => {
    const seen = new Map<string, string>();
    for (const ticket of data.tickets)
      for (const item of ticket.items)
        if (item.status !== "Cancelled" && !seen.has(item.productId)) seen.set(item.productId, item.productName);
    return Array.from(seen, ([productId, productName]) => ({ productId, productName }));
  }, [data.tickets]);

  const executeItemTransition = async (ticket: KitchenTicket, item: KitchenTicketItem, target: KitchenTicketItem["status"]) => {
    if (!onTransitionItem) return;
    const key = `item:${item.id}`;
    setBusyKey(key);
    setFeedback(null);
    try {
      await onTransitionItem(ticket, item, target);
      setFeedback({ tone: "success", message: `${item.productName} → ${itemStatusLabels[target]}.` });
    } catch (error) {
      setFeedback({ tone: isConflict(error) ? "conflict" : "error", message: isConflict(error) ? "Mutfak verisi değişti. Güncel kartı alın; işlem tekrarlanmadı." : error instanceof ApiError ? error.message : "Ürün durumu güncellenemedi." });
    } finally {
      setBusyKey(null);
    }
  };

  const executeUndoItem = async (ticket: KitchenTicket, item: KitchenTicketItem) => {
    if (!onUndoItem) return;
    const key = `undo:${item.id}`;
    setBusyKey(key);
    setFeedback(null);
    try {
      await onUndoItem(ticket, item);
      setFeedback({ tone: "success", message: `${item.productName} geri alındı.` });
    } catch (error) {
      setFeedback({ tone: isConflict(error) ? "conflict" : "error", message: isConflict(error) ? "Geri alma penceresi kapandı veya kart değişti. Güncel kartı alın." : error instanceof ApiError ? error.message : "Geri alınamadı." });
    } finally {
      setBusyKey(null);
    }
  };

  const openCancelPrompt = (ticket: KitchenTicket) => {
    setCancelTarget(ticket);
    setCancelReason("");
    setFormErrors([]);
  };

  const submitCancel = async (event: FormEvent) => {
    event.preventDefault();
    if (!cancelTarget || !onTransitionTicket) return;
    if (!cancelReason.trim()) {
      setFormErrors(["Sorun/iptal gerekçesi gerekli."]);
      return;
    }
    const key = `ticket:${cancelTarget.id}`;
    setBusyKey(key);
    setFormErrors([]);
    try {
      await onTransitionTicket(cancelTarget, "Cancelled", cancelReason.trim());
      setFeedback({ tone: "success", message: `${cancelTarget.ticketNumber} iptal edildi.` });
      setCancelTarget(null);
    } catch (error) {
      setFeedback({ tone: isConflict(error) ? "conflict" : "error", message: isConflict(error) ? "Ticket değişti; listeyi yenileyin." : error instanceof ApiError ? error.message : "İptal edilemedi." });
    } finally {
      setBusyKey(null);
    }
  };

  const openSuspendPrompt = () => {
    setSuspendProductId("");
    setFormErrors([]);
    setSuspendPromptOpen(true);
  };

  const submitSuspend = async (event: FormEvent) => {
    event.preventDefault();
    if (!onSuspendProductAvailability) return;
    if (!suspendProductId) {
      setFormErrors(["Bir ürün seçin."]);
      return;
    }
    const product = openProducts.find((candidate) => candidate.productId === suspendProductId);
    const key = `suspend:${suspendProductId}`;
    setBusyKey(key);
    setFormErrors([]);
    try {
      const result = await onSuspendProductAvailability(suspendProductId);
      setFeedback({
        tone: "success",
        message: result.planConflict
          ? `${product?.productName ?? "Ürün"} tükendi olarak işaretlendi; plana aykırı olduğu için yöneticiye bildirim kaydı düşüldü.`
          : `${product?.productName ?? "Ürün"} tükendi olarak işaretlendi.`,
      });
      setSuspendPromptOpen(false);
    } catch (error) {
      setFeedback({ tone: isConflict(error) ? "conflict" : "error", message: isConflict(error) ? "Ürün başka bir işlemle değişti; tekrar deneyin." : error instanceof ApiError ? error.message : "Ürün tükendi olarak işaretlenemedi." });
    } finally {
      setBusyKey(null);
    }
  };

  const openDecision = (delivery: KitchenUnknownDelivery, nextDecision: "approve" | "reject") => {
    setSelectedDelivery(delivery);
    setDecision(nextDecision);
    setReason("");
    setFormErrors([]);
    setFeedback(null);
  };

  const submitDecision = async (event: FormEvent) => {
    event.preventDefault();
    if (!selectedDelivery || !decision) return;
    if (!reason.trim()) {
      setFormErrors([kitchenReprintText.reasonRequired]);
      return;
    }
    const callback = decision === "approve" ? onApproveReprint : onRejectReprint;
    if (!callback) {
      setFormErrors(["Bu işlem için süpervizör yetkisi gerekli."]);
      return;
    }
    const key = `delivery:${selectedDelivery.id}`;
    setBusyKey(key);
    setFormErrors([]);
    try {
      await callback(selectedDelivery, reason.trim());
      setDecision(null);
      setSelectedDelivery(null);
      setFeedback({ tone: "success", message: decision === "approve" ? "Yeniden yazdırma onaylandı; fiziksel gönderim ayrı bir arka plan işlemi tarafından yapılacak." : "Yeniden yazdırma reddedildi." });
    } catch (error) {
      setFeedback({ tone: isConflict(error) ? "conflict" : "error", message: isConflict(error) ? "Kayıt değişti; listeyi yenileyin." : error instanceof ApiError ? error.message : "Yeniden yazdırma kararı kaydedilemedi." });
    } finally {
      setBusyKey(null);
    }
  };

  if (state === "loading" || state === "busy") {
    return <div className="kitchen-workspace kitchen-workspace--state" aria-busy="true"><StateMessage tone="info" title={state === "busy" ? "Mutfak güncelleniyor" : "Mutfak yükleniyor"}><p>{stationId} istasyonu için ticket, yazıcı ve operasyon durumu alınıyor…</p></StateMessage></div>;
  }
  if (state === "unauthorized") {
    return <div className="kitchen-workspace kitchen-workspace--state"><StateMessage tone="unauthorized" title="Kasiyer oturumu gerekli"><p>Mutfak çalışma alanını görmek için terminal oturumunu yenileyin.</p></StateMessage></div>;
  }
  if (state === "offline") {
    return <div className="kitchen-workspace kitchen-workspace--state"><StateMessage tone="offline" title={stateText.offlineTitle}><p>Eski ticket veya sağlık verisiyle işlem yapılmıyor.</p><Button onClick={onRefresh}>{commonActions.retry}</Button></StateMessage></div>;
  }
  if (state === "error") {
    return <div className="kitchen-workspace kitchen-workspace--state"><StateMessage tone="error" title="Mutfak verisi alınamadı"><p>{suppliedError ?? stateText.unexpectedError}</p><Button onClick={onRefresh}>{commonActions.reload}</Button></StateMessage></div>;
  }
  if (state === "stale" || state === "conflict") {
    return <div className="kitchen-workspace kitchen-workspace--state"><StateMessage tone="conflict" title={state === "stale" ? "Mutfak verisi güncel değil" : "Mutfak çakışması"}><p>Operasyon komutu göndermeden önce sunucunun son durumunu alın.</p><Button onClick={onRefresh}>Güncel veriyi al</Button></StateMessage></div>;
  }

  return <section className={`kitchen-workspace ${isDense ? "is-dense" : ""}`} aria-label="Mutfak ve operasyon yönetimi">
    <header className="kitchen-workspace__header">
      <div><span className="kitchen-workspace__kicker">EXPO / MUTFAK</span><h2>{stationId} istasyonu</h2><p>{lastUpdated ? `Son güncelleme ${lastUpdated}` : "Sipariş bazlı çapraz istasyon görünümü"}</p></div>
      <div className="kitchen-workspace__header-actions">
        <span className="kitchen-live-dot" aria-hidden="true" /><span className="kitchen-workspace__source">Canlı</span>
        {!data.liveSyncEnabled && <span className="kitchen-live-sync-badge" title="Kalem hazır olduğunda garsona bildirim gitmiyor">Canlı senkron kapalı</span>}
        {!canOperate && canAdvance && <span className="kitchen-role-badge" title="Yalnız ilerletme yapabilirsiniz">Mutfak Personeli</span>}
        <span className={`kitchen-health-dot kitchen-health-dot--${(overallHealth ?? "unknown").toLowerCase()}`} role="img" aria-label={`Sistem durumu: ${healthStatusLabel(overallHealth)}`} />
        {onSuspendProductAvailability && <Button
          variant="secondary"
          disabled={!canSuspendAvailability}
          title={canSuspendAvailability ? undefined : "Bu işlem için Mutfak Şefi yetkisi gerekli"}
          onClick={openSuspendPrompt}
        >Ürün Tükendi Bildir</Button>}
        <Button variant="secondary" onClick={() => void onRefresh()}>{commonActions.refresh}</Button>
      </div>
    </header>

    {feedback && <div className={`kitchen-workspace__feedback kitchen-workspace__feedback--${feedback.tone}`} role={feedback.tone === "success" ? "status" : "alert"} aria-live="polite"><span>{feedback.message}</span><button type="button" aria-label={stateText.dismissMessage} onClick={() => setFeedback(null)}>×</button></div>}

    <div className="kitchen-workspace__toolbar">
      <div className="kitchen-workspace__stats" aria-label="Mutfak özeti">
        <Stat label="Açık sipariş" value={orderGroups.length} />
        <Stat label="Açık kalem" value={openItemCount} tone={autoDense ? "warning" : "neutral"} />
        <Stat label="Doğrulanamayan baskı" value={data.unknownDeliveries.length} tone={data.unknownDeliveries.length ? "danger" : "success"} />
      </div>
      <div className="kitchen-density" role="group" aria-label="Ekran yoğunluğu">
        <button type="button" className={densityOverride === "auto" ? "is-active" : ""} onClick={() => setDensityOverride("auto")}>Otomatik{autoDense && densityOverride === "auto" ? " (yoğun)" : ""}</button>
        <button type="button" className={densityOverride === "sparse" ? "is-active" : ""} onClick={() => setDensityOverride("sparse")}>Sakin mod</button>
        <button type="button" className={densityOverride === "dense" ? "is-active" : ""} onClick={() => setDensityOverride("dense")}>Yoğun mod</button>
      </div>
    </div>

    <div className="kitchen-workspace__layout">
      <div className="kitchen-workspace__board">
        {state === "empty" || orderGroups.length === 0 ? <div className="kitchen-workspace__empty"><StateMessage tone="info" title="Aktif ticket yok"><p>Bu istasyona henüz aktif ticket gelmedi.</p></StateMessage></div> : orderGroups.map((group) => <OrderGroupCard
          key={group.orderId}
          orderId={group.orderId}
          tickets={group.tickets}
          now={now}
          canAdvance={canAdvance}
          canOperate={canOperate}
          busyKey={busyKey}
          onItemTransition={executeItemTransition}
          onUndoItem={onUndoItem ? executeUndoItem : undefined}
          onCancel={openCancelPrompt}
        />)}
      </div>
      <aside className="kitchen-workspace__rail" aria-label="Mutfak operasyon uyarıları">
        <UnknownPanel deliveries={data.unknownDeliveries} canManage={canManageReprints} busyKey={busyKey} onDecision={openDecision} />
        <PrinterPanel
          printers={data.printers}
          routes={data.routes}
          categories={data.categories}
          canOperate={canOperate}
          onCreateCategoryRoute={onCreateCategoryRoute}
        />
      </aside>
    </div>

    <ModalDialog open={cancelTarget !== null} title="Sorun bildir / iptal et" onClose={() => { if (!busyKey) setCancelTarget(null); }}>
      <form className="kitchen-decision-form" onSubmit={(event) => void submitCancel(event)}>
        <ValidationSummary title="Gerekçeyi kontrol edin" errors={formErrors} />
        {cancelTarget && <p className="kitchen-decision-form__context"><strong>{cancelTarget.ticketNumber}</strong> iptal edilecek.</p>}
        <TextField label="Gerekçe" hint="Bu bilgi denetim kaydına geçer." value={cancelReason} onChange={(event) => setCancelReason(event.target.value)} placeholder="Örn. Malzeme bitti" autoComplete="off" />
        <div className="kitchen-decision-form__actions">
          <Button variant="secondary" disabled={Boolean(busyKey)} onClick={() => setCancelTarget(null)}>Vazgeç</Button>
          <Button type="submit" disabled={Boolean(busyKey)}>{busyKey ? "Kaydediliyor…" : "İptal et"}</Button>
        </div>
      </form>
    </ModalDialog>

    <ModalDialog open={decision !== null} title={decision === "approve" ? "Reprint onayı" : "Reprint reddi"} onClose={() => { if (!busyKey) { setDecision(null); setSelectedDelivery(null); } }}>
      <form className="kitchen-decision-form" onSubmit={(event) => void submitDecision(event)}><ValidationSummary title="Gerekçeyi kontrol edin" errors={formErrors} />{selectedDelivery && <p className="kitchen-decision-form__context"><strong>Doğrulanamayan teslimat</strong> · {compactId(selectedDelivery.id)} · deneme {selectedDelivery.attemptNumber}</p>}<TextField label={kitchenReprintText.reasonLabel} hint="İstasyonda fiziksel kontrol yapıldı mı?" value={reason} onChange={(event) => setReason(event.target.value)} placeholder="Örn. Ticket yazıcıdan çıkmadı" autoComplete="off" /><div className="kitchen-decision-form__actions"><Button variant="secondary" disabled={Boolean(busyKey)} onClick={() => { setDecision(null); setSelectedDelivery(null); }}>Vazgeç</Button><Button type="submit" disabled={Boolean(busyKey)}>{busyKey ? "Kaydediliyor…" : decision === "approve" ? "Reprint'i onayla" : "Reprint'i reddet"}</Button></div></form>
    </ModalDialog>

    <ModalDialog open={suspendPromptOpen} title="Ürün Tükendi Bildir" onClose={() => { if (!busyKey) setSuspendPromptOpen(false); }}>
      <form className="kitchen-decision-form" onSubmit={(event) => void submitSuspend(event)}>
        <ValidationSummary title="Seçimi kontrol edin" errors={formErrors} />
        {openProducts.length === 0 ? <p className="kitchen-panel__muted">Açık biletlerde ürün yok.</p> : <label>Ürün
          <select value={suspendProductId} onChange={(event) => setSuspendProductId(event.target.value)} aria-label="Tükenen ürün" disabled={Boolean(busyKey)}>
            <option value="">Seçin…</option>
            {openProducts.map((product) => <option key={product.productId} value={product.productId}>{product.productName}</option>)}
          </select>
        </label>}
        <p className="kitchen-decision-form__context">Ürün pasife alınır; Catalog Management'ta da pasif görünecektir. Ürün hâlâ satışta iken 86'lanıyorsa, yöneticiye denetlenebilir bir bildirim kaydı düşülür.</p>
        <div className="kitchen-decision-form__actions">
          <Button variant="secondary" disabled={Boolean(busyKey)} onClick={() => setSuspendPromptOpen(false)}>Vazgeç</Button>
          <Button type="submit" disabled={Boolean(busyKey) || openProducts.length === 0}>{busyKey ? "Kaydediliyor…" : "Tükendi olarak işaretle"}</Button>
        </div>
      </form>
    </ModalDialog>
  </section>;
}

function Stat({ label, value, tone = "neutral" }: { label: string; value: number; tone?: string }) {
  return <div className={`kitchen-stat kitchen-stat--${tone}`}><span>{label}</span><strong>{value}</strong></div>;
}

// Expo view: one order can spread across several stations (tickets) — all
// of them render side by side/stacked here so a table's whole round can be
// tracked from one place.
function OrderGroupCard({
  orderId,
  tickets,
  now,
  canAdvance,
  canOperate,
  busyKey,
  onItemTransition,
  onUndoItem,
  onCancel,
}: {
  orderId: string;
  tickets: readonly KitchenTicket[];
  now: number;
  canAdvance: boolean;
  canOperate: boolean;
  busyKey: string | null;
  onItemTransition: (ticket: KitchenTicket, item: KitchenTicketItem, target: KitchenTicketItem["status"]) => void;
  onUndoItem?: (ticket: KitchenTicket, item: KitchenTicketItem) => void;
  onCancel: (ticket: KitchenTicket) => void;
}) {
  const oldestCreatedAt = tickets.reduce((oldest, t) => (Date.parse(t.createdAt) < Date.parse(oldest) ? t.createdAt : oldest), tickets[0].createdAt);
  const targetPrepMinutes = Math.max(...tickets.map((t) => t.targetPrepMinutes));
  const minutes = elapsedMinutesSince(oldestCreatedAt, now);
  const tone = ageTone(minutes, targetPrepMinutes);
  const allDone = tickets.every((t) => t.items.every((i) => i.status === "Ready" || i.status === "Served" || i.status === "Cancelled"));
  const anyCancellable = tickets.some((t) => t.status !== "Cancelled");

  return <article className={`kitchen-order kitchen-order--${tone}`}>
    <header className="kitchen-order__head">
      <div><span className="kitchen-order__label">Sipariş</span><span className="kitchen-order__id">{compactId(orderId)}</span></div>
      <div className={`kitchen-order__timer kitchen-order__timer--${tone}`}>{ageLabel(minutes)}<small>hedef {targetPrepMinutes} dk</small></div>
      {allDone && <span className="kitchen-order__done">✓ Tüm kalemler hazır</span>}
      {canOperate && anyCancellable && <button type="button" className="kitchen-flag-btn" title="Sorun bildir / iptal et" onClick={() => onCancel(tickets[0])}>⚠</button>}
    </header>
    <div className="kitchen-order__stations">
      {tickets.map((ticket) => <StationColumn
        key={ticket.id}
        ticket={ticket}
        now={now}
        canAdvance={canAdvance}
        busyKey={busyKey}
        onItemTransition={onItemTransition}
        onUndoItem={onUndoItem}
      />)}
    </div>
  </article>;
}

function StationColumn({
  ticket,
  now,
  canAdvance,
  busyKey,
  onItemTransition,
  onUndoItem,
}: {
  ticket: KitchenTicket;
  now: number;
  canAdvance: boolean;
  busyKey: string | null;
  onItemTransition: (ticket: KitchenTicket, item: KitchenTicketItem, target: KitchenTicketItem["status"]) => void;
  onUndoItem?: (ticket: KitchenTicket, item: KitchenTicketItem) => void;
}) {
  const done = ticket.items.every((i) => i.status === "Ready" || i.status === "Served" || i.status === "Cancelled");
  const doing = ticket.items.some((i) => i.status === "Preparing");
  return <div className="kitchen-station">
    <div className={`kitchen-station__label ${done ? "is-done" : doing ? "is-doing" : ""}`}><span className="kitchen-station__pip" />{ticket.stationId} <span className="kitchen-station__number">· {ticket.ticketNumber}</span></div>
    {ticket.items.filter((item) => item.status !== "Cancelled").map((item) => <ItemRow
      key={item.id}
      item={item}
      now={now}
      busy={busyKey === `item:${item.id}` || busyKey === `undo:${item.id}`}
      canAdvance={canAdvance}
      onAdvance={(target) => onItemTransition(ticket, item, target)}
      onUndo={onUndoItem ? () => onUndoItem(ticket, item) : undefined}
    />)}
  </div>;
}

function ItemRow({
  item,
  now,
  busy,
  canAdvance,
  onAdvance,
  onUndo,
}: {
  item: KitchenTicketItem;
  now: number;
  busy: boolean;
  canAdvance: boolean;
  onAdvance: (target: KitchenTicketItem["status"]) => void;
  onUndo?: () => void;
}) {
  const currentIndex = STAGES.indexOf(item.status as typeof STAGES[number]);
  // V1-KIT-009/V1-KDS-003: a fresh transition (Preparing/Ready/Served, never
  // Queued — nothing to undo from there) leaves a short window to undo it.
  const updatedAtMs = item.updatedAt ? Date.parse(item.updatedAt) : NaN;
  const remainingMs = Number.isFinite(updatedAtMs) ? UNDO_WINDOW_MS - (now - updatedAtMs) : 0;
  const canShowUndo = canAdvance && Boolean(onUndo) && currentIndex > 0 && remainingMs > 0;
  return <div className="kitchen-item-row">
    <div className="kitchen-item-row__main">
      <span className="kitchen-item-row__qty">{item.quantity}×</span>
      <div className="kitchen-item-row__body">
        <span className="kitchen-item-row__name">{item.productName}{item.isAgeRestricted && <span className="kitchen-age-badge" title="Servis öncesi kimlik kontrolü gerekli">🔞</span>}</span>
        {item.modifiers && <span className="kitchen-item-row__detail">{item.modifiers}</span>}
        {item.notes && <span className="kitchen-item-row__detail kitchen-item-row__detail--note">Not: {item.notes}</span>}
      </div>
      {canShowUndo && <button type="button" className="kitchen-undo-btn" disabled={busy} onClick={onUndo}>{busy ? "…" : `Geri Al · ${Math.ceil(remainingMs / 1000)}sn`}</button>}
    </div>
    {currentIndex < 0 ? null : <div className="kitchen-stepper" role="group" aria-label={`${item.productName} durumu`}>
      {STAGES.map((stage, index) => {
        const isPast = index < currentIndex;
        const isCurrent = index === currentIndex;
        const isNext = index === currentIndex + 1;
        const clickable = canAdvance && isNext && !busy;
        return <button
          key={stage}
          type="button"
          className={`kitchen-step ${isPast ? "is-done" : ""} ${isCurrent ? `is-current is-current--${stage.toLowerCase()}` : ""} ${isNext ? "is-next" : ""}`}
          disabled={!clickable}
          onClick={clickable ? () => onAdvance(stage) : undefined}
        >{isPast ? "✓ " : ""}{busy && isNext ? "…" : itemStatusLabels[stage]}</button>;
      })}
    </div>}
  </div>;
}

function UnknownPanel({ deliveries, canManage, busyKey, onDecision }: { deliveries: readonly KitchenUnknownDelivery[]; canManage: boolean; busyKey: string | null; onDecision: (delivery: KitchenUnknownDelivery, decision: "approve" | "reject") => void }) {
  return <section className={`kitchen-panel kitchen-panel--unknown ${deliveries.length ? "has-alert" : ""}`} aria-labelledby="unknown-heading"><header><div><span className="kitchen-panel__eyebrow">YAZICI KURTARMA</span><h3 id="unknown-heading">Doğrulanamayan teslimatlar</h3></div><strong>{deliveries.length}</strong></header>{deliveries.length === 0 ? <p className="kitchen-panel__muted">Bekleyen belirsiz teslimat yok.</p> : <div className="kitchen-unknown-list">{deliveries.map((delivery) => <div className="kitchen-unknown" key={delivery.id}><div><strong>{compactId(delivery.ticketId)}</strong><span>{delivery.crashReason ?? "ACK alınamadı"}</span></div>{canManage ? <div className="kitchen-unknown__actions"><Button variant="secondary" disabled={busyKey === `delivery:${delivery.id}`} onClick={() => onDecision(delivery, "reject")}>Reddet</Button><Button disabled={busyKey === `delivery:${delivery.id}`} onClick={() => onDecision(delivery, "approve")}>Gerekçeli onay</Button></div> : <span className="kitchen-panel__muted">Süpervizör gerekli</span>}</div>)}</div>}</section>;
}

function PrinterPanel({
  printers,
  routes,
  categories,
  canOperate,
  onCreateCategoryRoute,
}: {
  printers: readonly KitchenWorkspaceProps["data"]["printers"][number][];
  routes: readonly KitchenWorkspaceProps["data"]["routes"][number][];
  categories: readonly KitchenWorkspaceProps["data"]["categories"][number][];
  canOperate: boolean;
  onCreateCategoryRoute?: KitchenWorkspaceProps["onCreateCategoryRoute"];
}) {
  const categoryName = (id: string | null) => categories.find((category) => category.id === id)?.name ?? id;
  const categoryRoutes = routes.filter((route) => route.routeLevel === "Category" && route.isActive);
  // Every category already routed can't take a second active route (the
  // same category-level uniqueness kitchen.printer_routes itself enforces)
  // - offering it again would just fail server-side on submit.
  const routedCategoryIds = new Set(categoryRoutes.map((route) => route.categoryId));
  const availableCategories = categories.filter((category) => !routedCategoryIds.has(category.id));

  const [selectedCategoryId, setSelectedCategoryId] = useState("");
  const [selectedPrinterId, setSelectedPrinterId] = useState("");
  const [routeBusy, setRouteBusy] = useState(false);
  const [routeFeedback, setRouteFeedback] = useState<Feedback>(null);

  async function submitCategoryRoute(event: FormEvent) {
    event.preventDefault();
    if (!onCreateCategoryRoute || !selectedCategoryId || !selectedPrinterId) return;
    setRouteBusy(true);
    setRouteFeedback(null);
    try {
      await onCreateCategoryRoute(selectedCategoryId, selectedPrinterId);
      setRouteFeedback({ tone: "success", message: "Kategori rotası kaydedildi." });
      setSelectedCategoryId("");
      setSelectedPrinterId("");
    } catch (error) {
      setRouteFeedback({ tone: "error", message: error instanceof Error ? error.message : "Rota kaydedilemedi." });
    } finally {
      setRouteBusy(false);
    }
  }

  return <section className="kitchen-panel" aria-labelledby="printer-heading">
    <header><div><span className="kitchen-panel__eyebrow">DONANIM</span><h3 id="printer-heading">Yazıcı rotaları</h3></div><span>{printers.filter((printer) => printer.isActive).length}/{printers.length} aktif</span></header>
    {printers.length === 0 ? <p className="kitchen-panel__muted">Yapılandırılmış yazıcı yok.</p> : <ul className="kitchen-printer-list">{printers.map((printer) => <li key={printer.id}><span className={`kitchen-printer-dot ${printer.isActive ? "is-active" : ""}`} /><div><strong>{printer.name}</strong><span>{printer.stationId} · {routes.filter((route) => route.printerId === printer.id && route.isActive).length} aktif rota</span></div></li>)}</ul>}

    {categoryRoutes.length > 0 && <ul className="kitchen-printer-list" aria-label="Kategori yönlendirmeleri">
      {categoryRoutes.map((route) => <li key={route.id}>
        <span className="kitchen-printer-dot is-active" />
        <div><strong>{categoryName(route.categoryId)}</strong><span>→ {printers.find((printer) => printer.id === route.printerId)?.name ?? route.printerId}</span></div>
      </li>)}
    </ul>}

    {canOperate && onCreateCategoryRoute && <form className="kitchen-route-form" onSubmit={(event) => void submitCategoryRoute(event)}>
      <p className="kitchen-panel__muted">Bir ürün grubunun tamamını tek yazıcıya yönlendir (örn. "Izgara" grubu ızgara yazıcısına).</p>
      {routeFeedback && <p className={`kitchen-route-form__feedback kitchen-route-form__feedback--${routeFeedback.tone}`}>{routeFeedback.message}</p>}
      <label>Ürün grubu
        <select value={selectedCategoryId} onChange={(event) => setSelectedCategoryId(event.target.value)} aria-label="Ürün grubu" disabled={routeBusy}>
          <option value="">Seçin…</option>
          {availableCategories.map((category) => <option key={category.id} value={category.id}>{category.name}</option>)}
        </select>
      </label>
      <label>Yazıcı
        <select value={selectedPrinterId} onChange={(event) => setSelectedPrinterId(event.target.value)} aria-label="Yazıcı" disabled={routeBusy}>
          <option value="">Seçin…</option>
          {printers.filter((printer) => printer.isActive).map((printer) => <option key={printer.id} value={printer.id}>{printer.name}</option>)}
        </select>
      </label>
      <Button type="submit" variant="secondary" disabled={routeBusy || !selectedCategoryId || !selectedPrinterId}>{routeBusy ? "Kaydediliyor…" : "Rota ekle"}</Button>
    </form>}
  </section>;
}
