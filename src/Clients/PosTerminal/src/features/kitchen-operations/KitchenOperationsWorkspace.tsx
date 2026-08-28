import { useMemo, useState, type FormEvent } from "react";
import { Button, ModalDialog, StateMessage, TextField, ValidationSummary } from "../../design-system";
import {
  itemStatusLabels,
  ticketStatusLabels,
  type KitchenTicket,
  type KitchenTicketItem,
  type KitchenUnknownDelivery,
  type KitchenWorkspaceProps,
} from "./models";
import "./kitchen-operations.css";

type Feedback = { tone: "success" | "error" | "conflict"; message: string } | null;
type Filter = "all" | KitchenTicket["status"];

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

export function KitchenOperationsWorkspace({
  state,
  stationId,
  data,
  canOperate,
  canManageReprints,
  onRefresh,
  onTransitionItem,
  onTransitionTicket,
  onApproveReprint,
  onRejectReprint,
  errorMessage: suppliedError,
  lastUpdated,
}: KitchenWorkspaceProps) {
  const [filter, setFilter] = useState<Filter>("all");
  const [search, setSearch] = useState("");
  const [busyKey, setBusyKey] = useState<string | null>(null);
  const [feedback, setFeedback] = useState<Feedback>(null);
  const [selectedDelivery, setSelectedDelivery] = useState<KitchenUnknownDelivery | null>(null);
  const [decision, setDecision] = useState<"approve" | "reject" | null>(null);
  const [reason, setReason] = useState("");
  const [formErrors, setFormErrors] = useState<string[]>([]);

  const normalizedSearch = search.trim().toLocaleLowerCase("tr-TR");
  const tickets = useMemo(() => data.tickets.filter((ticket) => {
    if (filter !== "all" && ticket.status !== filter) return false;
    if (!normalizedSearch) return true;
    return `${ticket.ticketNumber} ${ticket.id} ${ticket.items.map((item) => item.productName).join(" ")}`
      .toLocaleLowerCase("tr-TR").includes(normalizedSearch);
  }), [data.tickets, filter, normalizedSearch]);

  const executeItemTransition = async (ticket: KitchenTicket, item: KitchenTicketItem) => {
    const target = nextItemState(item.status);
    if (!target || !onTransitionItem) return;
    const key = `item:${item.id}`;
    setBusyKey(key);
    setFeedback(null);
    try {
      await onTransitionItem(ticket, item, target);
      setFeedback({ tone: "success", message: `${item.productName} → ${itemStatusLabels[target]}.` });
    } catch (error) {
      setFeedback({ tone: isConflict(error) ? "conflict" : "error", message: isConflict(error) ? "Mutfak verisi değişti. Güncel kartı alın; işlem tekrarlanmadı." : error instanceof Error ? error.message : "Ürün durumu güncellenemedi." });
    } finally {
      setBusyKey(null);
    }
  };

  const executeTicketTransition = async (ticket: KitchenTicket, target: KitchenTicket["status"], transitionReason?: string) => {
    if (!onTransitionTicket) return;
    const key = `ticket:${ticket.id}`;
    setBusyKey(key);
    setFeedback(null);
    try {
      await onTransitionTicket(ticket, target, transitionReason);
      setFeedback({ tone: "success", message: `${ticket.ticketNumber} → ${ticketStatusLabels[target]}.` });
    } catch (error) {
      setFeedback({ tone: isConflict(error) ? "conflict" : "error", message: isConflict(error) ? "Ticket değişti veya hazır olma kuralı sağlanmıyor." : error instanceof Error ? error.message : "Ticket güncellenemedi." });
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
      setFormErrors(["Supervisor gerekçesi zorunlu."]);
      return;
    }
    const callback = decision === "approve" ? onApproveReprint : onRejectReprint;
    if (!callback) {
      setFormErrors(["Bu işlem için supervisor yetkisi gerekli."]);
      return;
    }
    const key = `delivery:${selectedDelivery.id}`;
    setBusyKey(key);
    setFormErrors([]);
    try {
      await callback(selectedDelivery, reason.trim());
      setDecision(null);
      setSelectedDelivery(null);
      setFeedback({ tone: "success", message: decision === "approve" ? "Reprint onaylandı; fiziksel gönderim ayrı worker tarafından yapılacak." : "Reprint reddedildi." });
    } catch (error) {
      setFeedback({ tone: isConflict(error) ? "conflict" : "error", message: isConflict(error) ? "Unknown kaydı değişti. Listeyi yenileyin." : error instanceof Error ? error.message : "Reprint kararı kaydedilemedi." });
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
    return <div className="kitchen-workspace kitchen-workspace--state"><StateMessage tone="offline" title="Bağlantı yok"><p>Eski ticket veya sağlık verisiyle işlem yapılmıyor.</p><Button onClick={onRefresh}>Tekrar dene</Button></StateMessage></div>;
  }
  if (state === "error") {
    return <div className="kitchen-workspace kitchen-workspace--state"><StateMessage tone="error" title="Mutfak verisi alınamadı"><p>{suppliedError ?? "Beklenmeyen bir hata oluştu."}</p><Button onClick={onRefresh}>Yeniden yükle</Button></StateMessage></div>;
  }
  if (state === "stale" || state === "conflict") {
    return <div className="kitchen-workspace kitchen-workspace--state"><StateMessage tone="conflict" title={state === "stale" ? "Mutfak verisi güncel değil" : "Mutfak çakışması"}><p>Operasyon komutu göndermeden önce sunucunun son durumunu alın.</p><Button onClick={onRefresh}>Güncel veriyi al</Button></StateMessage></div>;
  }

  return <section className="kitchen-workspace" aria-label="Mutfak ve operasyon yönetimi">
    <header className="kitchen-workspace__header">
      <div><span className="kitchen-workspace__kicker">OPERASYON / MUTFAK</span><h2>{stationId} istasyonu</h2><p>{lastUpdated ? `Son güncelleme ${lastUpdated}` : "Ticket akışı, yazıcı kurtarma ve sistem sağlığı"}</p></div>
      <div className="kitchen-workspace__header-actions"><span className="kitchen-workspace__source">Kaynak: production API</span><Button variant="secondary" onClick={() => void onRefresh()}>Yenile</Button></div>
    </header>

    {feedback && <div className={`kitchen-workspace__feedback kitchen-workspace__feedback--${feedback.tone}`} role={feedback.tone === "success" ? "status" : "alert"} aria-live="polite"><span>{feedback.message}</span><button type="button" aria-label="Mesajı kapat" onClick={() => setFeedback(null)}>×</button></div>}

    <div className="kitchen-workspace__stats" aria-label="Mutfak özeti">
      <Stat label="Aktif ticket" value={data.tickets.length} />
      <Stat label="Bekliyor" value={data.tickets.filter((ticket) => ticket.status === "Queued").length} tone="warning" />
      <Stat label="Hazırlanıyor" value={data.tickets.filter((ticket) => ticket.status === "Preparing").length} tone="accent" />
      <Stat label="Unknown baskı" value={data.unknownDeliveries.length} tone={data.unknownDeliveries.length ? "danger" : "success"} />
    </div>

    <div className="kitchen-workspace__toolbar"><label>Ticket ara<input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Ticket no veya ürün" aria-label="Ticket ara" /></label><div className="kitchen-workspace__filters" role="group" aria-label="Ticket durumuna göre filtrele">{(["all", "Queued", "Accepted", "Preparing", "Ready", "Cancelled"] as const).map((value) => <button type="button" key={value} className={filter === value ? "is-active" : ""} aria-pressed={filter === value} onClick={() => setFilter(value)}>{value === "all" ? "Tümü" : ticketStatusLabels[value]}<span>{value === "all" ? data.tickets.length : data.tickets.filter((ticket) => ticket.status === value).length}</span></button>)}</div></div>

    <div className="kitchen-workspace__layout">
      <div className="kitchen-workspace__tickets">
        {state === "empty" || tickets.length === 0 ? <div className="kitchen-workspace__empty"><StateMessage tone="info" title={state === "empty" ? "Aktif ticket yok" : "Eşleşen ticket yok"}><p>{state === "empty" ? "Bu istasyona henüz aktif ticket gelmedi." : "Aramayı veya durum filtresini değiştirin."}</p></StateMessage></div> : tickets.map((ticket) => <TicketCard key={ticket.id} ticket={ticket} canOperate={canOperate} busyKey={busyKey} onItemTransition={(item) => void executeItemTransition(ticket, item)} onTicketTransition={(target, transitionReason) => void executeTicketTransition(ticket, target, transitionReason)} />)}
      </div>
      <aside className="kitchen-workspace__rail" aria-label="Mutfak operasyon uyarıları">
        <UnknownPanel deliveries={data.unknownDeliveries} canManage={canManageReprints} busyKey={busyKey} onDecision={openDecision} />
        <HealthPanel health={data.health} backups={data.backups} />
        <PrinterPanel printers={data.printers} routes={data.routes} />
      </aside>
    </div>

    <ModalDialog open={decision !== null} title={decision === "approve" ? "Reprint onayı" : "Reprint reddi"} onClose={() => { if (!busyKey) { setDecision(null); setSelectedDelivery(null); } }}>
      <form className="kitchen-decision-form" onSubmit={(event) => void submitDecision(event)}><ValidationSummary title="Gerekçeyi kontrol edin" errors={formErrors} />{selectedDelivery && <p className="kitchen-decision-form__context"><strong>Unknown teslimat</strong> · {compactId(selectedDelivery.id)} · deneme {selectedDelivery.attemptNumber}</p>}<TextField label="Supervisor gerekçesi" hint="İstasyonda fiziksel kontrol yapıldı mı?" value={reason} onChange={(event) => setReason(event.target.value)} placeholder="Örn. Ticket yazıcıdan çıkmadı" autoComplete="off" /><div className="kitchen-decision-form__actions"><Button variant="secondary" disabled={Boolean(busyKey)} onClick={() => { setDecision(null); setSelectedDelivery(null); }}>Vazgeç</Button><Button type="submit" disabled={Boolean(busyKey)}>{busyKey ? "Kaydediliyor…" : decision === "approve" ? "Reprint'i onayla" : "Reprint'i reddet"}</Button></div></form>
    </ModalDialog>
  </section>;
}

function Stat({ label, value, tone = "neutral" }: { label: string; value: number; tone?: string }) {
  return <div className={`kitchen-stat kitchen-stat--${tone}`}><span>{label}</span><strong>{value}</strong></div>;
}

function TicketCard({ ticket, canOperate, busyKey, onItemTransition, onTicketTransition }: { ticket: KitchenTicket; canOperate: boolean; busyKey: string | null; onItemTransition: (item: KitchenTicketItem) => void; onTicketTransition: (target: KitchenTicket["status"], reason?: string) => void }) {
  const allReady = ticket.items.length > 0 && ticket.items.every((item) => item.status === "Ready" || item.status === "Served" || item.status === "Cancelled");
  const ticketBusy = busyKey === `ticket:${ticket.id}`;
  return <article className={`kitchen-ticket kitchen-ticket--${ticket.status.toLowerCase()}`}>
    <header className="kitchen-ticket__header"><div><span className="kitchen-ticket__number">{ticket.ticketNumber}</span><span className={`kitchen-ticket__status kitchen-ticket__status--${ticket.status.toLowerCase()}`}>{ticketStatusLabels[ticket.status]}</span></div><span className="kitchen-ticket__version">v{ticket.rowVersion}</span></header>
    <div className="kitchen-ticket__items">{ticket.items.map((item) => { const next = nextItemState(item.status); const itemBusy = busyKey === `item:${item.id}`; return <div className="kitchen-ticket__item" key={item.id}><div className="kitchen-ticket__item-main"><strong>{item.quantity}× {item.productName}</strong>{item.modifiers && <span>{item.modifiers}</span>}</div><div className="kitchen-ticket__item-action"><span className={`kitchen-item-status kitchen-item-status--${item.status.toLowerCase()}`}>{itemStatusLabels[item.status]}</span>{canOperate && next && <Button variant="quiet" disabled={itemBusy || ticketBusy} onClick={() => onItemTransition(item)}>{itemBusy ? "…" : `→ ${itemStatusLabels[next]}`}</Button>}</div></div>; })}</div>
    <footer className="kitchen-ticket__footer"><span>Order <code>{compactId(ticket.orderId)}</code></span><div>{canOperate && ticket.status === "Queued" && <Button variant="secondary" disabled={ticketBusy} onClick={() => onTicketTransition("Accepted")}>{ticketBusy ? "…" : "Kabul et"}</Button>}{canOperate && ticket.status === "Preparing" && allReady && <Button disabled={ticketBusy} onClick={() => onTicketTransition("Ready")}>{ticketBusy ? "…" : "Hazır"}</Button>}{canOperate && ticket.status !== "Cancelled" && ticket.status !== "Ready" && <Button variant="quiet" disabled={ticketBusy} onClick={() => onTicketTransition("Cancelled", "Kitchen operator cancelled ticket")}>İptal</Button>}</div></footer>
  </article>;
}

function UnknownPanel({ deliveries, canManage, busyKey, onDecision }: { deliveries: readonly KitchenUnknownDelivery[]; canManage: boolean; busyKey: string | null; onDecision: (delivery: KitchenUnknownDelivery, decision: "approve" | "reject") => void }) {
  return <section className={`kitchen-panel kitchen-panel--unknown ${deliveries.length ? "has-alert" : ""}`} aria-labelledby="unknown-heading"><header><div><span className="kitchen-panel__eyebrow">YAZICI KURTARMA</span><h3 id="unknown-heading">Unknown teslimatlar</h3></div><strong>{deliveries.length}</strong></header>{deliveries.length === 0 ? <p className="kitchen-panel__muted">Bekleyen belirsiz teslimat yok.</p> : <div className="kitchen-unknown-list">{deliveries.map((delivery) => <div className="kitchen-unknown" key={delivery.id}><div><strong>{compactId(delivery.ticketId)}</strong><span>{delivery.crashReason ?? "ACK alınamadı"}</span></div>{canManage ? <div className="kitchen-unknown__actions"><Button variant="secondary" disabled={busyKey === `delivery:${delivery.id}`} onClick={() => onDecision(delivery, "reject")}>Reddet</Button><Button disabled={busyKey === `delivery:${delivery.id}`} onClick={() => onDecision(delivery, "approve")}>Gerekçeli onay</Button></div> : <span className="kitchen-panel__muted">Supervisor gerekli</span>}</div>)}</div>}</section>;
}

function HealthPanel({ health, backups }: { health: KitchenWorkspaceProps["data"]["health"]; backups: KitchenWorkspaceProps["data"]["backups"] }) {
  const latestBackup = backups[0];
  return <section className="kitchen-panel" aria-labelledby="health-heading"><header><div><span className="kitchen-panel__eyebrow">SİSTEM</span><h3 id="health-heading">Sağlık ve yedek</h3></div><span role="img" className={`kitchen-health-dot kitchen-health-dot--${health?.diskStatus.toLowerCase() ?? "unknown"}`} aria-label={health?.diskStatus ?? "Bilinmiyor"} /></header>{health ? <dl className="kitchen-health"><div><dt>Veritabanı</dt><dd className={`kitchen-badge kitchen-badge--${health.databaseStatus.toLowerCase()}`}>{health.databaseStatus}</dd></div><div><dt>Disk</dt><dd className={`kitchen-badge kitchen-badge--${health.diskStatus.toLowerCase()}`}>{health.diskStatus}</dd></div><div><dt>Son yedek</dt><dd className={`kitchen-badge kitchen-badge--${health.lastBackupStatus.toLowerCase()}`}>{health.lastBackupStatus}</dd></div></dl> : <StateMessage tone="stale" title="Sağlık verisi yok"><p>Son snapshot alınamadı.</p></StateMessage>}{latestBackup && <div className={`kitchen-backup kitchen-backup--${latestBackup.status.toLowerCase()}`}><span>Son yedekleme</span><strong>{latestBackup.status === "Failed" ? "Başarısız" : latestBackup.status}</strong>{latestBackup.errorMessage && <p>{latestBackup.errorMessage}</p>}</div>}</section>;
}

function PrinterPanel({ printers, routes }: { printers: readonly KitchenWorkspaceProps["data"]["printers"][number][]; routes: readonly KitchenWorkspaceProps["data"]["routes"][number][] }) {
  return <section className="kitchen-panel" aria-labelledby="printer-heading"><header><div><span className="kitchen-panel__eyebrow">DONANIM</span><h3 id="printer-heading">Yazıcı rotaları</h3></div><span>{printers.filter((printer) => printer.isActive).length}/{printers.length} aktif</span></header>{printers.length === 0 ? <p className="kitchen-panel__muted">Yapılandırılmış yazıcı yok.</p> : <ul className="kitchen-printer-list">{printers.map((printer) => <li key={printer.id}><span className={`kitchen-printer-dot ${printer.isActive ? "is-active" : ""}`} /><div><strong>{printer.name}</strong><span>{printer.stationId} · {routes.filter((route) => route.printerId === printer.id && route.isActive).length} aktif rota</span></div></li>)}</ul>}</section>;
}
