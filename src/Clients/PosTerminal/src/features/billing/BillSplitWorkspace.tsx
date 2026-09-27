import { useEffect, useState } from "react";
import { ApiError } from "../../api";
import { BillingSplitApiError } from "./billingApi";
import { Button, StateMessage, ValidationSummary } from "../../design-system";
import { commonActions, stateText } from "../../strings";
import {
  billStatusLabels,
  modeLabels,
  type BillSplitDesign,
  type BillSplitWorkspaceProps,
  type SaveSplitRequest,
  type SplitMode,
  type SplitOwnerOption,
} from "./models";
import "./billing.css";

type ItemDraft = Record<string, Record<string, number>>;
type AmountDraft = Record<string, number>;
const round = (value: number) => Math.round((value + Number.EPSILON) * 100) / 100;

function money(value: number, currency: string) {
  return new Intl.NumberFormat("tr-TR", { style: "currency", currency }).format(value);
}

function ownerKey(owner: Pick<SplitOwnerOption, "kind" | "ownerId">) { return `${owner.kind}:${owner.ownerId}`; }

function initialDraft(design: BillSplitDesign) {
  const equalOwners: string[] = [];
  const itemDraft: ItemDraft = {};
  const amountDraft: AmountDraft = {};
  for (const allocation of design.allocations) {
    if (!allocation.ownerId || (allocation.ownerKind !== "Seat" && allocation.ownerKind !== "Person")) continue;
    const key = `${allocation.ownerKind}:${allocation.ownerId}`;
    if (allocation.mode === "EqualByPerson") equalOwners.push(key);
    if (allocation.mode === "ByItem" && allocation.billItemId && allocation.quantity) {
      itemDraft[allocation.billItemId] = { ...itemDraft[allocation.billItemId], [key]: allocation.quantity };
    }
    if (allocation.mode === "ByAmount") amountDraft[key] = allocation.amount;
  }
  return { equalOwners, itemDraft, amountDraft };
}

export function BillSplitWorkspace({ state, design: suppliedDesign, owners, canMutate, onRefresh, onSave, onClear, errorMessage, lastUpdated }: BillSplitWorkspaceProps) {
  const [design, setDesign] = useState(suppliedDesign);
  const [mode, setMode] = useState<SplitMode>(suppliedDesign?.mode === "ByItem" || suppliedDesign?.mode === "ByAmount" ? suppliedDesign.mode : "EqualByPerson");
  const seeded = initialDraft(suppliedDesign ?? emptyDesign);
  const [equalOwners, setEqualOwners] = useState<string[]>(seeded.equalOwners);
  const [itemDraft, setItemDraft] = useState<ItemDraft>(seeded.itemDraft);
  const [amountDraft, setAmountDraft] = useState<AmountDraft>(seeded.amountDraft);
  const [busy, setBusy] = useState(false);
  const [dirty, setDirty] = useState(false);
  const [feedback, setFeedback] = useState<{ tone: "success" | "error" | "conflict"; message: string }>();

  useEffect(() => {
    if (dirty || !suppliedDesign) return;
    setDesign(suppliedDesign);
    const next = initialDraft(suppliedDesign);
    setEqualOwners(next.equalOwners); setItemDraft(next.itemDraft); setAmountDraft(next.amountDraft);
  }, [dirty, suppliedDesign]);

  if (state !== "ready" || !design) return <BoundedState state={state} message={errorMessage} onRefresh={onRefresh} />;

  const selectedOwners = owners.filter((owner) => equalOwners.includes(ownerKey(owner)));
  const itemAllocated = design.items.reduce((sum, item) => sum + Object.values(itemDraft[item.billItemId] ?? {}).reduce((itemSum, quantity) => itemSum + item.grossAmount * quantity / item.quantity, 0), 0);
  const allocated = mode === "EqualByPerson" ? (selectedOwners.length >= 2 ? design.payableAmount : 0) : mode === "ByItem" ? round(itemAllocated) : round(Object.values(amountDraft).reduce((sum, amount) => sum + amount, 0));
  const unallocated = round(design.payableAmount - allocated);
  const mutableState = ["Open", "PartiallyAllocated", "Allocated", "Reopened"].includes(design.billStatus);
  const command = mode === "EqualByPerson" ? "SaveEqual" : mode === "ByItem" ? "SaveItems" : "SaveAmounts";
  const validation = (() => {
    const errors: string[] = [];
    if (!mutableState) errors.push(`${billStatusLabels[design.billStatus] ?? "Bilinmiyor"} durumundaki hesap değiştirilemez.`);
    if (mode === "EqualByPerson" && selectedOwners.length < 2) errors.push("Eşit bölme için en az iki sandalye veya kişi seçin.");
    if (mode === "ByAmount" && Object.values(amountDraft).some((amount) => amount <= 0)) errors.push("Girilen her tutar sıfırdan büyük olmalı.");
    if (mode === "ByAmount" && round(allocated) !== round(design.payableAmount)) errors.push("Dağıtılan tutar, ödenecek tutara tam eşit olmalı.");
    if (mode === "ByItem") for (const item of design.items) {
      const quantities = Object.values(itemDraft[item.billItemId] ?? {});
      if (quantities.some((quantity) => quantity <= 0)) errors.push(`${item.productName} miktarları sıfırdan büyük olmalı.`);
      if (round(quantities.reduce((sum, quantity) => sum + quantity, 0)) > item.quantity) errors.push(`${item.productName} toplam miktarı ${item.quantity} değerini aşıyor.`);
    }
    if (mode === "ByItem" && !Object.values(itemDraft).some((targets) => Object.values(targets).some((quantity) => quantity > 0))) errors.push("En az bir ürün miktarı dağıtılmalı.");
    if (!design.allowedCommands.includes(command)) errors.push("Sunucu bu dağıtım modunu şu anda yetkilendirmiyor.");
    return errors;
  })();

  const setModeSafely = (next: SplitMode) => { setMode(next); setFeedback(undefined); };
  const ownerFromKey = (key: string) => owners.find((owner) => ownerKey(owner) === key)!;
  const request = (): SaveSplitRequest => mode === "EqualByPerson"
    ? { mode, owners: selectedOwners.map(({ kind, ownerId }) => ({ kind, ownerId })) }
    : mode === "ByItem"
      ? { mode, targets: design.items.flatMap((item) => Object.entries(itemDraft[item.billItemId] ?? {}).filter(([, quantity]) => quantity > 0).map(([key, quantity]) => { const owner = ownerFromKey(key); return { owner: { kind: owner.kind, ownerId: owner.ownerId }, billItemId: item.billItemId, quantity }; })) }
      : { mode, targets: Object.entries(amountDraft).filter(([, amount]) => amount > 0).map(([key, amount]) => { const owner = ownerFromKey(key); return { owner: { kind: owner.kind, ownerId: owner.ownerId }, amount }; }) };

  const save = async () => {
    if (validation.length || busy) return;
    setBusy(true); setFeedback(undefined);
    try {
      const next = await onSave(request(), design); const loaded = initialDraft(next);
      setDesign(next); setEqualOwners(loaded.equalOwners); setItemDraft(loaded.itemDraft); setAmountDraft(loaded.amountDraft); setDirty(false);
      setFeedback({ tone: "success", message: "Hesap dağıtım tasarımı sunucuda kaydedildi. Ödeme işlemi yapılmadı." });
    } catch (reason) {
      // V1-RMD-366 (module-by-module UI audit): billingApi.ts throws its own
      // BillingSplitApiError, not the shared ApiError this used to check
      // alone - the real backend Turkish message (a genuine validation
      // failure, not just a 409) was silently replaced with the generic
      // fallback below.
      const message = reason instanceof ApiError || reason instanceof BillingSplitApiError ? reason.message : "Dağıtım kaydedilemedi.";
      setFeedback({ tone: /409|concurrent|conflict|version/i.test(message) ? "conflict" : "error", message: /409|concurrent|conflict|version/i.test(message) ? "Hesap sunucuda değişti. Taslağınız korundu; güncel hesabı açıp karşılaştırın." : message });
    } finally { setBusy(false); }
  };
  const clear = async () => {
    if (busy || !design.allowedCommands.includes("Clear")) return;
    setBusy(true); setFeedback(undefined);
    try {
      const next = await onClear(design); setDesign(next); setEqualOwners([]); setItemDraft({}); setAmountDraft({}); setDirty(false);
      setFeedback({ tone: "success", message: "Dağıtım tasarımı sıfırlandı. Hesap veya ödeme durumu değişmedi." });
    } catch (reason) { setFeedback({ tone: "error", message: reason instanceof ApiError || reason instanceof BillingSplitApiError ? reason.message : "Dağıtım sıfırlanamadı." }); }
    finally { setBusy(false); }
  };

  return <section className="bill-split" aria-label="Hesap bölme çalışma alanı">
    <header className="bill-split__header"><div><span>HESAP / DAĞITIM TASARIMI</span><h2>{design.billNumber}</h2><p>{billStatusLabels[design.billStatus] ?? "Bilinmiyor"} · v{design.billRowVersion} · {lastUpdated ?? "sunucu durumu"}</p></div><div className="bill-split__totals"><span>Ödenecek<strong>{money(design.payableAmount, design.currencyCode)}</strong></span><span>Vergi<strong>{money(design.taxTotal, design.currencyCode)}</strong></span></div></header>
    {feedback && <div className={`bill-split__feedback bill-split__feedback--${feedback.tone}`} role={feedback.tone === "success" ? "status" : "alert"}>{feedback.message}</div>}
    <div className="bill-split__modes" role="tablist" aria-label="Dağıtım modu">{(Object.keys(modeLabels) as SplitMode[]).map((option) => <button key={option} type="button" role="tab" aria-selected={mode === option} onClick={() => setModeSafely(option)}>{modeLabels[option]}</button>)}</div>
    <div className="bill-split__layout">
      <main className="bill-split__editor">
        {mode === "EqualByPerson" && <OwnerPicker owners={owners} selected={equalOwners} onChange={(next) => { setEqualOwners(next); setDirty(true); }} />}
        {mode === "ByItem" && <ItemEditor design={design} owners={owners} draft={itemDraft} onChange={(next) => { setItemDraft(next); setDirty(true); }} />}
        {mode === "ByAmount" && <AmountEditor design={design} owners={owners} draft={amountDraft} onChange={(next) => { setAmountDraft(next); setDirty(true); }} />}
      </main>
      <aside className="bill-split__review" aria-label="Dağıtım incelemesi">
        <span>İNCELEME</span><div><small>Dağıtıldı</small><strong>{money(allocated, design.currencyCode)}</strong></div><div><small>Dağıtılmadı</small><strong className={unallocated !== 0 ? "is-warning" : ""}>{money(unallocated, design.currencyCode)}</strong></div>
        <ValidationSummary title="Kaydetmeden önce düzeltin" errors={validation} />
        <p>Bu ekran yalnız hesap dağıtım tasarımını kaydeder; tahsilat veya ödeme yürütmez.</p>
        <Button disabled={!canMutate || busy || validation.length > 0} onClick={() => void save()}>{busy ? "Kaydediliyor…" : "Dağıtımı kaydet"}</Button>
        <Button variant="secondary" disabled={!canMutate || busy || !design.allowedCommands.includes("Clear") || design.allocations.length === 0} onClick={() => void clear()}>Dağıtımı sıfırla</Button>
      </aside>
    </div>
  </section>;
}

function OwnerPicker({ owners, selected, onChange }: { owners: readonly SplitOwnerOption[]; selected: readonly string[]; onChange: (next: string[]) => void }) {
  return <section className="bill-split__owner-picker" aria-labelledby="equal-title"><div><span>1</span><h3 id="equal-title">Sandalye veya kişi seçin</h3></div><p>Sunucu kuruş farkını seçilen sahipler arasında deterministik dağıtır.</p><div>{owners.map((owner) => { const key = ownerKey(owner); return <label key={key}><input type="checkbox" checked={selected.includes(key)} onChange={(event) => onChange(event.target.checked ? [...selected, key] : selected.filter((value) => value !== key))} /><span><strong>{owner.label}</strong><small>{owner.secondaryLabel ?? (owner.kind === "Seat" ? "Kalıcı sandalye" : "Kişi")}</small></span></label>; })}</div></section>;
}

function ItemEditor({ design, owners, draft, onChange }: { design: BillSplitDesign; owners: readonly SplitOwnerOption[]; draft: ItemDraft; onChange: (next: ItemDraft) => void }) {
  return <section className="bill-split__items" aria-labelledby="item-title"><div><span>1</span><h3 id="item-title">Ürün miktarlarını dağıtın</h3></div>{design.items.map((item) => <article key={item.billItemId}><header><div><strong>{item.productName}</strong><small>{item.quantity} adet · {money(item.grossAmount, design.currencyCode)}</small></div><span>Kalan {round(item.quantity - Object.values(draft[item.billItemId] ?? {}).reduce((sum, quantity) => sum + quantity, 0))}</span></header><div>{owners.map((owner) => { const key = ownerKey(owner); return <label key={key}><span>{owner.label}</span><input aria-label={`${item.productName} / ${owner.label} miktarı`} type="number" min="0" step="0.001" value={draft[item.billItemId]?.[key] ?? ""} onChange={(event) => onChange({ ...draft, [item.billItemId]: { ...draft[item.billItemId], [key]: Number(event.target.value) } })} /></label>; })}</div></article>)}</section>;
}

function AmountEditor({ design, owners, draft, onChange }: { design: BillSplitDesign; owners: readonly SplitOwnerOption[]; draft: AmountDraft; onChange: (next: AmountDraft) => void }) {
  return <section className="bill-split__amounts" aria-labelledby="amount-title"><div><span>1</span><h3 id="amount-title">Kişi tutarlarını girin</h3></div><p>Toplam tam olarak {money(design.payableAmount, design.currencyCode)} olmalıdır.</p>{owners.map((owner) => { const key = ownerKey(owner); return <label key={key}><span><strong>{owner.label}</strong><small>{owner.secondaryLabel ?? owner.kind}</small></span><input aria-label={`${owner.label} tutarı`} type="number" min="0" step="0.01" value={draft[key] ?? ""} onChange={(event) => onChange({ ...draft, [key]: Number(event.target.value) })} /></label>; })}</section>;
}

function BoundedState({ state, message, onRefresh }: { state: BillSplitWorkspaceProps["state"]; message?: string; onRefresh: BillSplitWorkspaceProps["onRefresh"] }) {
  const content = state === "loading" ? ["info", "Hesap yükleniyor", "Authoritative hesap ve dağıtımlar alınıyor…"] : state === "offline" ? ["offline", stateText.offlineTitle, "Taslak gönderilmedi; sunucuya yeniden bağlanın."] : state === "stale" ? ["stale", "Hesap güncel değil", "Dağıtmadan önce güncel hesap sürümünü alın."] : state === "unauthorized" ? ["unauthorized", stateText.unauthorizedTitle, "Hesap dağıtımını görmek için yeniden giriş yapın."] : ["error", "Hesap alınamadı", message ?? stateText.unexpectedError];
  return <div className="bill-split bill-split--state"><StateMessage tone={content[0] as "info" | "offline" | "stale" | "unauthorized" | "error"} title={content[1]} actions={state === "loading" || state === "unauthorized" ? undefined : <Button onClick={() => void onRefresh()}>{commonActions.retry}</Button>}><p>{content[2]}</p></StateMessage></div>;
}

const emptyDesign: BillSplitDesign = { billId: "", billNumber: "", billStatus: "", currencyCode: "TRY", payableAmount: 0, taxTotal: 0, billRowVersion: 0, mode: "None", executionState: "DesignOnly", allowedCommands: [], items: [], allocations: [] };
