import { useEffect, useState } from "react";
import { api, ApiError, notifySessionExpired } from "../api";
import type { TableOrder } from "../contracts";
import { formatMoney } from "../format";
import { BillingSplitApiError } from "../features/billing";
import { collectionHrefForOrder } from "../collectOrder";
import { collectionHref } from "../features/pending-checks/pendingChecksApi";
import { printPreReceipt, PreReceiptError } from "../features/pending-checks/preReceipt";
import { billOnOtherTillText, kitchenStateNotes, kitchenlessText, preReceiptText, servedBillNote, voidSentText } from "../strings";

interface BridgeTable {
  tableId: string;
  tableNumber: string;
  currentOrderId: string | null;
  currentBillId: string | null;
  rowVersion: number;
  currentOrderTotal?: number;
  billOnOtherTerminal?: boolean;
}

/** A check the waiter sent to the till: it has left the table row but is waiting in the till's own list. */
export interface AwaitingCheck {
  orderId: string;
  itemCount: number;
  total: number;
}

interface TableOrderBridgeProps {
  terminalId: string;
  table: BridgeTable;
  /** Whether the table can take or continue an order right now. */
  orderAllowed: boolean;
  canManage: boolean;
  canCollect: boolean;
  /** The check this table sent to the till and nobody opened yet; absent when there is none. */
  awaitingCheck?: AwaitingCheck;
  /** Reading the bill lines needs the order permission, which the bill permission does not imply. */
  canReadLines: boolean;
  /** Whether this user may cancel a bill that holds a served line (the refund approver). */
  canCancelServed?: boolean;
  /** Adding a line at the till without a kitchen ticket. */
  canAddWithoutKitchen: boolean;
  busy: boolean;
  error?: string;
  onStartOrder: () => void;
  onAddWithoutKitchen: () => void;
  /** Called after the unsent lines were sent to the kitchen, so the table list is read again. */
  onSent: () => void;
  /** Opens the table move form; absent when the table cannot be moved. */
  onTransfer?: () => void;
  navigateTo?: (href: string) => void;
}

const lineRefreshMs = 15_000;

function lineNote(line: TableOrder["items"][number]) {
  if (line.status === "Draft") return kitchenlessText.notSent;
  return line.addedWithoutKitchenReason ? kitchenlessText.addedAtTill : kitchenStateNotes[line.kitchenState ?? ""];
}

/** What the cashier can do with the selected table: take or continue its order, and collect its check. */
export function TableOrderBridge({
  terminalId, table, orderAllowed, canManage, canCollect, awaitingCheck, canReadLines, canCancelServed = false, canAddWithoutKitchen, busy, error, onStartOrder, onAddWithoutKitchen, onSent, onTransfer,
  navigateTo = (href: string) => window.location.assign(href),
}: TableOrderBridgeProps) {
  const [collecting, setCollecting] = useState(false);
  const [collectError, setCollectError] = useState<string>();
  const [printing, setPrinting] = useState(false);
  const [printError, setPrintError] = useState<string>();
  const [sending, setSending] = useState(false);
  const [sendError, setSendError] = useState<string>();
  // Sending changes the lines but not the table row, so the effect below needs its own cue to read them again.
  const [sentCount, setSentCount] = useState(0);
  const [voidingId, setVoidingId] = useState<string | null>(null);
  const [voidReason, setVoidReason] = useState<string | null>(null);
  const [voiding, setVoiding] = useState(false);
  const [voidError, setVoidError] = useState<string>();
  const [voidNotice, setVoidNotice] = useState<string>();
  // A void the cashier may not make alone: the manager answers it here, with their own name and password.
  const [waiting, setWaiting] = useState<{ itemId: string; grantId: string; reason: string } | null>(null);
  const [managerName, setManagerName] = useState("");
  const [managerPassword, setManagerPassword] = useState("");
  const [bill, setBill] = useState<TableOrder | "failed" | null>(null);
  const hasCheck = table.currentOrderId !== null || table.currentBillId !== null;
  const sentCheck = hasCheck || !canCollect ? undefined : awaitingCheck;
  const billed = table.currentBillId !== null;
  const otherTill = table.billOnOtherTerminal === true;
  const { tableId, currentOrderId } = table;
  useEffect(() => {
    if (!currentOrderId || !canReadLines) return;
    let current = true;
    const read = (quiet: boolean) => api.tableOrder(terminalId, tableId).then(
      (order) => { if (current) setBill(order); },
      () => { if (current && !quiet) setBill("failed"); },
    );
    void read(false);
    // The kitchen marks lines ready or served without touching the table row, so the lines are read again on a timer.
    const timer = window.setInterval(() => void read(true), lineRefreshMs);
    return () => { current = false; window.clearInterval(timer); };
  }, [terminalId, tableId, currentOrderId, canReadLines, table.currentBillId, table.rowVersion, table.currentOrderTotal, sentCount]);
  const billingHref = table.currentBillId ? `/billing?billId=${encodeURIComponent(table.currentBillId)}` : `/billing?orderId=${encodeURIComponent(table.currentOrderId ?? "")}`;
  // What was read belongs to one order: once the table has another order, none, or the lines may not be read, it is stale.
  const read = !canReadLines || (bill !== "failed" && bill?.orderId !== currentOrderId) ? null : bill;
  const lines = read && read !== "failed" ? read.items.filter((item) => item.status !== "Cancelled" && item.status !== "Waste") : [];
  const unsent = read && read !== "failed" ? read.items.reduce((sum, item) => sum + (item.status === "Draft" ? item.quantity : 0), 0) : 0;
  const servedLine = lines.some((line) => line.kitchenState === "Served");
  const sentOrder = read !== null && read !== "failed" && read.status !== "Draft";
  const send = async () => {
    setSending(true);
    setSendError(undefined);
    try {
      const started = await api.startTableOrder(terminalId, tableId, table.rowVersion);
      await api.submitOrder(terminalId, started.orderId, started.revision);
      setSentCount((count) => count + 1);
      onSent();
    } catch (reason) {
      setSendError(reason instanceof ApiError ? reason.message : "Sipariş mutfağa gönderilemedi.");
    } finally {
      setSending(false);
    }
  };
  const closeVoid = () => { setVoidingId(null); setVoidReason(null); };
  const closeWaiting = () => { setWaiting(null); setManagerName(""); setManagerPassword(""); };
  const voidApplied = (result: { stockRestored?: boolean }) => {
    setVoidNotice(result.stockRestored ? voidSentText.appliedRestored : voidSentText.applied);
    setSentCount((count) => count + 1);
    onSent();
  };
  const voidLine = async (itemId: string) => {
    if (!read || read === "failed" || !voidReason) return;
    setVoiding(true);
    setVoidError(undefined);
    setVoidNotice(undefined);
    try {
      const result = await api.voidSentItem(terminalId, read.orderId, itemId, read.rowVersion, voidReason);
      closeVoid();
      if (result.status === "Pending") {
        setWaiting({ itemId, grantId: result.grantId!, reason: voidReason });
        return;
      }
      voidApplied(result);
    } catch (reason) {
      setVoidError(reason instanceof ApiError ? reason.message : voidSentText.failed);
      if (reason instanceof ApiError && reason.status === 409) {
        closeVoid();
        setSentCount((count) => count + 1);
      }
    } finally {
      setVoiding(false);
    }
  };
  const approveLine = async () => {
    if (!read || read === "failed" || !waiting) return;
    setVoiding(true);
    setVoidError(undefined);
    try {
      const result = await api.approveVoidSentInPlace(terminalId, read.orderId, waiting.itemId, waiting.grantId, read.rowVersion, waiting.reason, managerName, managerPassword);
      closeWaiting();
      voidApplied(result);
    } catch (reason) {
      setVoidError(reason instanceof ApiError ? reason.message : voidSentText.failed);
      setManagerPassword("");
      if (reason instanceof ApiError && reason.status === 409) {
        closeWaiting();
        setSentCount((count) => count + 1);
      }
    } finally {
      setVoiding(false);
    }
  };
  const collect = async () => {
    const orderId = table.currentOrderId ?? sentCheck?.orderId;
    if (!orderId && !table.currentBillId) return;
    setCollecting(true);
    setCollectError(undefined);
    try {
      navigateTo(await collectionHrefForOrder(terminalId, orderId ?? "", table.currentBillId));
    } catch (reason) {
      if (reason instanceof BillingSplitApiError && reason.status === 401) notifySessionExpired();
      setCollectError(reason instanceof BillingSplitApiError ? reason.message : "Hesap açılamadı. Tekrar deneyin.");
      setCollecting(false);
    }
  };
  const slipOrderId = table.currentOrderId ?? sentCheck?.orderId;
  const printSlip = async () => {
    if (!slipOrderId) return;
    setPrinting(true);
    setPrintError(undefined);
    try {
      await printPreReceipt(terminalId, slipOrderId);
    } catch (reason) {
      setPrintError(reason instanceof PreReceiptError ? reason.message : preReceiptText.failed);
    } finally {
      setPrinting(false);
    }
  };
  return <section className="table-order-bridge" aria-label="Masa hesabı">
    <p id="table-order-bridge-note" className="table-order-bridge__note">{otherTill && hasCheck && canCollect ? billOnOtherTillText : sentCheck ? "Garson hesabı kasaya gönderdi; ödemeyi almak için Öde'ye basın." : !canManage
      ? hasCheck && canCollect ? "Bu masadaki hesabı ödeme ekranından alın." : "Bu masada sizin yapabileceğiniz bir işlem yok."
      : billed ? canCollect ? servedLine && !canCancelServed ? `Hesap açıldığı için ürün eklenemez. ${servedBillNote}` : "Hesap açıldığı için ürün eklenemez. Eklemek için aşağıdaki 'İptal / iade' düğmesiyle hesabı iptal edin; ödeme alındıysa önce iade gerekir." : "Hesap açıldığı için ürün eklenemez."
        : table.currentOrderId ? unsent > 0 ? kitchenlessText.sendFirst : "Siparişe ürün ekleyin ya da hesabı ödeyin." : "Sipariş al deyince sipariş ve mutfak kaydı birlikte açılır."}</p>
    <div className="table-order-bridge__actions">
      {(hasCheck || sentCheck) && canCollect && <button data-telemetry-id="b.routes.tableorderbridge.2"
        className="primary"
        type="button"
        disabled={busy || collecting || otherTill}
        aria-busy={collecting}
        onClick={() => void collect()}
      >{collecting ? "Hesap açılıyor…" : "Öde"}</button>}
      {canManage && <button data-telemetry-id="b.routes.tableorderbridge.1"
        className={hasCheck || sentCheck ? "secondary" : "primary"}
        type="button"
        disabled={!orderAllowed || busy || billed}
        aria-busy={busy}
        aria-describedby="table-order-bridge-note"
        onClick={onStartOrder}
      >{busy ? "Sipariş hazırlanıyor…" : table.currentOrderId ? "Ürün ekle" : "Sipariş al"}</button>}
      {unsent > 0 && canManage && !billed && <button data-telemetry-id="b.routes.tableorderbridge.send" className="secondary" type="button" disabled={busy || sending || collecting} aria-busy={sending} onClick={() => void send()}>{sending ? "Gönderiliyor…" : `Mutfağa gönder (${unsent})`}</button>}
      {sentOrder && canAddWithoutKitchen && !billed && <button data-telemetry-id="b.routes.tableorderbridge.kitchenless" className="secondary" type="button" disabled={busy || sending || collecting} onClick={onAddWithoutKitchen}>{kitchenlessText.open}</button>}
      {slipOrderId && (hasCheck || sentCheck) && canCollect && <button data-telemetry-id="b.routes.tableorderbridge.pre-receipt"
        className="secondary"
        type="button"
        disabled={busy || collecting || printing || otherTill}
        aria-busy={printing}
        onClick={() => void printSlip()}
      >{printing ? preReceiptText.busy : preReceiptText.action}</button>}
    </div>
    {hasCheck && (canCollect || canManage) && <div className="table-order-bridge__mini">
      {canCollect && <button data-telemetry-id="b.routes.tableorderbridge.split" className="secondary" type="button" disabled={busy || collecting || otherTill} onClick={() => navigateTo(billingHref)}>Böl</button>}
      {canManage && <button data-telemetry-id="b.routes.tableorderbridge.transfer" className="secondary" type="button" disabled={busy || collecting || otherTill || !onTransfer} title={otherTill ? billOnOtherTillText : undefined} onClick={onTransfer}>Masa taşı</button>}
    </div>}
    {hasCheck && canManage && !onTransfer && !otherTill && <small className="table-order-bridge__reason">Bu masa şu anda taşınamaz.</small>}
    {table.currentBillId && canCollect && <div className="table-order-bridge__danger"><button data-telemetry-id="b.routes.tableorderbridge.cancel" className="table-order-bridge__cancel" type="button" disabled={busy || collecting || otherTill} onClick={() => navigateTo(`${collectionHref(table.currentBillId!)}&open=refund`)}>İptal / iade</button></div>}
    {canManage && !orderAllowed && !sentCheck && <small className="table-order-bridge__reason">Masa şu anda sipariş almaya uygun değil (rezerve, temizlikte veya kapalı); durumunu Diğer işlemler menüsünden değiştirin.</small>}
    {(error ?? collectError ?? sendError ?? printError ?? voidError) && <p className="table-order-bridge__error" role="alert">{error ?? collectError ?? sendError ?? printError ?? voidError}</p>}
    {voidNotice && <p role="status">{voidNotice}</p>}
    {hasCheck ? !canReadLines ? null : read === "failed" ? <p className="table-order-bridge__lines-note">Hesap kalemleri alınamadı. Masayı yenileyip tekrar deneyin.</p>
      : read && <>
        <ul className="table-order-bridge__lines" aria-label="Hesap kalemleri">
          {lines.map((line) => <li key={line.itemId}><span>{line.quantity} × {line.productName}{lineNote(line) && <small className="table-order-bridge__unsent"> · {lineNote(line)}</small>}</span><span>{formatMoney(line.totalPrice)}</span>
            {line.canVoidSent && (voidingId === line.itemId
              ? <div className="table-order-bridge__void" role="group" aria-label={voidSentText.pickReason}>
                <strong>{voidSentText.pickReason}</strong>
                <p>{voidSentText.warning}</p>
                {voidSentText.reasons.map((reason) => <button data-telemetry-id="b.routes.tableorderbridge.void-reason" key={reason.code} type="button" aria-pressed={voidReason === reason.code} disabled={voiding} onClick={() => setVoidReason(reason.code)}>{reason.label}</button>)}
                {!voidReason && <small>{voidSentText.needReason}</small>}
                <button data-telemetry-id="b.routes.tableorderbridge.void-confirm" className="table-order-bridge__void-confirm" type="button" disabled={!voidReason || voiding} aria-busy={voiding} onClick={() => void voidLine(line.itemId)}>{voiding ? voidSentText.busy : voidSentText.confirm(table.tableNumber, line.quantity, line.productName, formatMoney(line.totalPrice))}</button>
                <button data-telemetry-id="b.routes.tableorderbridge.void-dismiss" type="button" disabled={voiding} onClick={closeVoid}>{voidSentText.dismiss}</button>
              </div>
              : waiting?.itemId === line.itemId
                ? <form className="table-order-bridge__void" aria-label={voidSentText.approve} onSubmit={(event) => { event.preventDefault(); void approveLine(); }}>
                  <p>{voidSentText.pending}</p>
                  <label>{voidSentText.managerName}<input autoComplete="off" autoCapitalize="none" spellCheck={false} maxLength={64} value={managerName} disabled={voiding} onChange={(event) => setManagerName(event.target.value)} /></label>
                  <label>{voidSentText.managerPassword}<input type="password" autoComplete="new-password" maxLength={256} value={managerPassword} disabled={voiding} onChange={(event) => setManagerPassword(event.target.value)} /></label>
                  <button data-telemetry-id="b.routes.tableorderbridge.void-approve" className="table-order-bridge__void-confirm" type="submit" disabled={!managerName.trim() || !managerPassword || voiding} aria-busy={voiding}>{voiding ? voidSentText.approving : voidSentText.approve}</button>
                  <button data-telemetry-id="b.routes.tableorderbridge.void-approve-dismiss" type="button" disabled={voiding} onClick={closeWaiting}>{voidSentText.dismiss}</button>
                </form>
              : <button data-telemetry-id="b.routes.tableorderbridge.void-open" className="table-order-bridge__void-open" type="button" disabled={busy || voiding || sending || collecting || printing || otherTill} aria-label={`${voidSentText.open}: ${line.productName}`} title={otherTill ? billOnOtherTillText : undefined} onClick={() => { setVoidNotice(undefined); setVoidError(undefined); setVoidReason(null); setVoidingId(line.itemId); }}>{voidSentText.open}</button>)}
          </li>)}
        </ul>
        <p className="table-order-bridge__total"><span>Toplam</span><strong>{formatMoney(read.totalAmount)}</strong></p>
      </> : sentCheck ? <p className="table-order-bridge__total"><span>{sentCheck.itemCount} kalem</span><strong>{formatMoney(sentCheck.total)}</strong></p>
      : <p className="table-order-bridge__lines-note">Bu masada açık hesap yok.</p>}
  </section>;
}
