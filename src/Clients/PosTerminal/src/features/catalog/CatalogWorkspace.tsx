import { useEffect, useMemo, useState, type FormEvent } from "react";
import { ApiError } from "../../api";
import { Button, ModalDialog, SelectField, StateMessage, TextField, ValidationSummary } from "../../design-system";
import { commonActions, stateText } from "../../strings";
import { CatalogApiError } from "./catalogApi";
import {
  catalogAddLabels,
  catalogEntityLabels,
  priceTypeLabels,
  productTypeLabels,
  selectableStockModes,
  selectionTypeLabels,
  stockModeHint,
  stockModeLabels,
  type CatalogCreateInput,
  type CatalogData,
  type CatalogEntityKind,
  type CatalogProduct,
  type CatalogWorkspaceProps,
} from "./models";
import "./catalog.css";

type Draft = {
  code: string;
  name: string;
  description: string;
  sku: string;
  productType: string;
  stockMode: string;
  categoryId: string;
  taxProfileId: string;
  modifierGroupId: string;
  productId: string;
  priceType: string;
  price: string;
  vatRate: string;
  sortOrder: string;
  priceDelta: string;
  effectiveFrom: string;
  selectionType: string;
  minSelections: string;
  maxSelections: string;
  prepTimeMinutes: string;
};

const localDateTimeInput = (date: Date) => new Date(date.getTime() - date.getTimezoneOffset() * 60_000).toISOString().slice(0, 16);

const emptyDraft = (): Draft => ({
  code: "", name: "", description: "", sku: "", productType: "MenuItem", stockMode: "QuantityTracked",
  categoryId: "", taxProfileId: "", modifierGroupId: "", productId: "", priceType: "SalePrice",
  price: "", vatRate: "10", sortOrder: "0", priceDelta: "0", effectiveFrom: localDateTimeInput(new Date()),
  selectionType: "SelectOne", minSelections: "0", maxSelections: "1", prepTimeMinutes: "",
});

// Found by an independent audit (2026-09-07): modifierGroups was missing
// from this order entirely (and from CatalogCreateInput/catalogApi.ts's
// create() below) — an operator could create individual modifiers but never
// the group their mandatory ModifierGroupId must reference. Placed before
// "modifiers" since a group must exist before a modifier can join one.
const kindOrder: readonly CatalogEntityKind[] = ["products", "categories", "taxes", "modifierGroups", "modifiers", "prices"];

export function CatalogWorkspace({ state, data, canManage, onRefresh, onCreate, onUpdateProduct, onSetAvailability, onSetPrepTime, errorMessage: suppliedError, lastUpdated }: CatalogWorkspaceProps) {
  const [kind, setKind] = useState<CatalogEntityKind>("products");
  const [search, setSearch] = useState("");
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [editorOpen, setEditorOpen] = useState(false);
  const [draft, setDraft] = useState<Draft>(emptyDraft);
  const [formErrors, setFormErrors] = useState<string[]>([]);
  const [feedback, setFeedback] = useState<{ tone: "success" | "error" | "conflict"; message: string } | null>(null);

  const rows = useMemo(() => catalogRows(kind, data), [data, kind]);
  const filteredRows = useMemo(() => {
    const normalized = search.trim().toLocaleLowerCase("tr-TR");
    return rows.filter((row) => !normalized || `${row.title} ${row.subtitle}`.toLocaleLowerCase("tr-TR").includes(normalized));
  }, [rows, search]);
  const selected = rows.find((row) => row.id === selectedId) ?? filteredRows[0] ?? null;
  const selectedProduct = kind === "products" ? data.products.find((product) => product.id === selected?.id) : undefined;

  const changeKind = (nextKind: CatalogEntityKind) => {
    setKind(nextKind);
    setSelectedId(null);
    setSearch("");
    setFeedback(null);
  };

  const [editing, setEditing] = useState<CatalogProduct | null>(null);
  const openProductEditor = (product: CatalogProduct) => {
    setEditing(product);
    setDraft({ ...emptyDraft(), sku: product.sku, name: product.name, categoryId: product.categoryId ?? "", taxProfileId: product.taxProfileId ?? "" });
    setFormErrors([]);
    setFeedback(null);
    setEditorOpen(true);
  };
  const [draftId, setDraftId] = useState(() => crypto.randomUUID());
  const openEditor = () => {
    setEditing(null);
    setDraftId(crypto.randomUUID());
    setDraft(emptyDraft());
    setFormErrors([]);
    setFeedback(null);
    setEditorOpen(true);
  };

  const [submitting, setSubmitting] = useState(false);
  const submit = async (event: FormEvent) => {
    event.preventDefault();
    if (submitting) return;
    const errors = validate(kind, draft, editing !== null);
    if (editing) {
      if (errors.length || !onUpdateProduct) {
        setFormErrors(errors.length ? errors : ["Bu işlem için katalog yönetimi yetkisi gerekli."]);
        return;
      }
      setSubmitting(true);
      try {
        await onUpdateProduct(editing.id, { sku: draft.sku.trim(), name: draft.name.trim(), categoryId: draft.categoryId || null, taxProfileId: draft.taxProfileId || null, description: editing.description, displayOrder: editing.displayOrder });
        setEditorOpen(false);
        setFeedback({ tone: "success", message: `${draft.name.trim()} güncellendi.` });
      } catch (reason) {
        const message = reason instanceof ApiError || reason instanceof CatalogApiError ? reason.message : "Ürün güncellenemedi.";
        const conflict = (reason instanceof ApiError || reason instanceof CatalogApiError) && reason.status === 409;
        setFormErrors([conflict ? `Kayıt çakıştı: ${message} Formunuz korunuyor; güncel veriyi yenileyin.` : message]);
      } finally {
        setSubmitting(false);
      }
      return;
    }
    if (errors.length || !onCreate) {
      setFormErrors(errors.length ? errors : ["Bu işlem için katalog yönetimi yetkisi gerekli."]);
      return;
    }
    setSubmitting(true);
    try {
      await onCreate(toCreateInput(kind, draft, draftId));
      setEditorOpen(false);
      setFeedback({ tone: "success", message: `${catalogEntityLabels[kind]} kaydı oluşturuldu.` });
    } catch (reason) {
      // V1-RMD-368 (module-by-module UI audit): catalogApi.ts throws its own
      // CatalogApiError, not the shared ApiError this checked alone.
      const message = reason instanceof ApiError || reason instanceof CatalogApiError ? reason.message : "Kayıt oluşturulamadı.";
      const conflict = (reason instanceof ApiError || reason instanceof CatalogApiError) && reason.status === 409;
      setFormErrors([conflict ? `Kayıt çakıştı: ${message} Formunuz korunuyor; güncel veriyi yenileyin.` : message]);
    } finally {
      setSubmitting(false);
    }
  };

  const [assignGroupId, setAssignGroupId] = useState("");
  const [assignBusy, setAssignBusy] = useState(false);
  const assignGroup = async (product: CatalogProduct) => {
    if (!onCreate || !assignGroupId || assignBusy) return;
    setAssignBusy(true);
    setFeedback(null);
    try {
      await onCreate({ kind: "assignments", value: { id: crypto.randomUUID(), productId: product.id, modifierGroupId: assignGroupId } });
      setAssignGroupId("");
      setFeedback({ tone: "success", message: `${product.name} için modifikatör grubu atandı.` });
    } catch (reason) {
      setFeedback({ tone: "error", message: reason instanceof ApiError || reason instanceof CatalogApiError ? reason.message : "Modifikatör grubu atanamadı." });
    } finally {
      setAssignBusy(false);
    }
  };

  const [availabilityBusy, setAvailabilityBusy] = useState(false);
  const toggleAvailability = async (product: CatalogProduct) => {
    if (!onSetAvailability || availabilityBusy) return;
    const next = !product.isAvailable;
    setAvailabilityBusy(true);
    setFeedback(null);
    try {
      await onSetAvailability(product.id, next);
      setFeedback({ tone: "success", message: next ? `${product.name} yeniden satışa açıldı.` : `${product.name} menüden kaldırıldı ("86").` });
    } catch (reason) {
      setFeedback({ tone: "error", message: reason instanceof ApiError || reason instanceof CatalogApiError ? reason.message : "Kullanılabilirlik güncellenemedi." });
    } finally {
      setAvailabilityBusy(false);
    }
  };

  // V1-WTR-017: same single-field pattern as toggleAvailability above. The
  // input is controlled but re-synced from the server value whenever the
  // selected product changes, so switching products never leaves a stale
  // draft value sitting in the box.
  const [prepTimeBusy, setPrepTimeBusy] = useState(false);
  const [prepTimeInput, setPrepTimeInput] = useState("");
  useEffect(() => {
    setPrepTimeInput(selectedProduct?.prepTimeMinutes != null ? String(selectedProduct.prepTimeMinutes) : "");
  }, [selectedProduct?.id, selectedProduct?.prepTimeMinutes]);
  const savePrepTime = async (product: CatalogProduct) => {
    if (!onSetPrepTime || prepTimeBusy) return;
    const trimmed = prepTimeInput.trim();
    if (trimmed && (!Number.isInteger(Number(trimmed)) || Number(trimmed) < 1 || Number(trimmed) > 180)) {
      setFeedback({ tone: "error", message: "Hazırlama süresi 1-180 dakika arasında bir tam sayı olmalı." });
      return;
    }
    const next = trimmed ? Number(trimmed) : null;
    setPrepTimeBusy(true);
    setFeedback(null);
    try {
      await onSetPrepTime(product.id, next);
      setFeedback({ tone: "success", message: next === null ? `${product.name} için hazırlama süresi kaldırıldı.` : `${product.name} için hazırlama süresi ${next} dk olarak kaydedildi.` });
    } catch (reason) {
      setFeedback({ tone: "error", message: reason instanceof ApiError || reason instanceof CatalogApiError ? reason.message : "Hazırlama süresi güncellenemedi." });
    } finally {
      setPrepTimeBusy(false);
    }
  };

  if (state === "loading" || state === "busy") return <div className="catalog-workspace catalog-workspace--state" aria-busy="true"><StateMessage tone="info" title={state === "busy" ? "Katalog kaydediliyor" : "Katalog yükleniyor"}><p>Yetkili katalog verisi alınıyor…</p></StateMessage></div>;
  if (state === "unauthorized") return <div className="catalog-workspace catalog-workspace--state"><StateMessage tone="unauthorized" title={stateText.managerUnauthorizedTitle}><p>Katalog düzenlemek için yetkili yönetici hesabıyla giriş yapın.</p></StateMessage></div>;
  if (state === "offline") return <div className="catalog-workspace catalog-workspace--state"><StateMessage tone="offline" title={stateText.offlineTitle}><p>Eski katalog verisiyle değişiklik yapılamaz.</p><Button data-telemetry-id="b.catalog.catalogworkspace.1" onClick={onRefresh}>{commonActions.retry}</Button></StateMessage></div>;
  if (state === "error") return <div className="catalog-workspace catalog-workspace--state"><StateMessage tone="error" title="Katalog alınamadı"><p>{suppliedError ?? stateText.unexpectedError}</p><Button data-telemetry-id="b.catalog.catalogworkspace.2" onClick={onRefresh}>{commonActions.reload}</Button></StateMessage></div>;
  if (state === "stale" || state === "conflict") return <div className="catalog-workspace catalog-workspace--state"><StateMessage tone="conflict" title={state === "stale" ? "Katalog güncel değil" : "Katalog çakışması"}><p>Satışa açık bilgiyi değiştirmeden önce sunucunun son halini alın.</p><Button data-telemetry-id="b.catalog.catalogworkspace.3" onClick={onRefresh}>Güncel veriyi al</Button></StateMessage></div>;

  return <section className="catalog-workspace" aria-label="Menü ve katalog yönetimi">
    <header className="catalog-workspace__header"><div><span className="catalog-workspace__kicker">YÖNETİM / KATALOG</span><h2>Menü ve katalog</h2><p>{lastUpdated ? `Son güncelleme ${lastUpdated}` : "Fiyat, ürün ve modifikatör kayıtları"}</p></div><div className="catalog-workspace__header-actions">{canManage && onCreate && <Button data-telemetry-id="b.catalog.catalogworkspace.4" onClick={openEditor}>+ {catalogAddLabels[kind]} ekle</Button>}<Button data-telemetry-id="b.catalog.catalogworkspace.5" variant="secondary" onClick={() => void onRefresh()}>{commonActions.refresh}</Button></div></header>
    {feedback && <div className={`catalog-workspace__feedback catalog-workspace__feedback--${feedback.tone}`} role={feedback.tone === "success" ? "status" : "alert"} aria-live="polite"><span>{feedback.message}</span><button data-telemetry-id="b.catalog.catalogworkspace.6" type="button" aria-label={stateText.dismissMessage} onClick={() => setFeedback(null)}>×</button></div>}
    <nav className="catalog-workspace__tabs" aria-label="Katalog kaynak türü">{kindOrder.map((item) => <button data-telemetry-id="b.catalog.catalogworkspace.7" key={item} type="button" className={kind === item ? "is-active" : ""} aria-current={kind === item ? "page" : undefined} onClick={() => changeKind(item)}>{catalogEntityLabels[item]}<span>{catalogCount(item, data)}</span></button>)}</nav>
    <div className="catalog-workspace__toolbar"><label>Katalog ara<input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Kod, SKU veya ad" aria-label="Katalog ara" /></label><span className="catalog-workspace__authority">Yayınlama yok · yalnızca yönetici ekleyip düzenleyebilir</span></div>
    {state === "empty" || filteredRows.length === 0 ? <div className="catalog-workspace__empty"><StateMessage tone="info" title={state === "empty" ? "Katalog kaydı yok" : "Eşleşen kayıt yok"}><p>{state === "empty" ? "Bu kaynak türünde ilk kaydı oluşturarak başlayın." : "Arama ifadenizi değiştirin veya temizleyin."}</p>{canManage && onCreate && <Button data-telemetry-id="b.catalog.catalogworkspace.8" onClick={openEditor}>İlk kaydı ekle</Button>}</StateMessage></div> : <div className="catalog-workspace__content"><div className="catalog-list">{filteredRows.map((row) => <button data-telemetry-id="b.catalog.catalogworkspace.9" type="button" key={row.id} className={`catalog-row ${selected?.id === row.id ? "is-selected" : ""}`} aria-pressed={selected?.id === row.id} onClick={() => setSelectedId(row.id)}><span className="catalog-row__title"><strong>{row.title}</strong><span className={row.active ? "catalog-active" : "catalog-inactive"}>{row.active ? "Aktif" : "Pasif"}</span></span><span className="catalog-row__subtitle">{row.subtitle}</span><span className="catalog-row__value">{row.value}</span></button>)}</div>{selected && <CatalogDetails kind={kind} row={selected} product={selectedProduct} data={data} canAssign={canManage && !!onCreate} onEdit={canManage && onUpdateProduct ? openProductEditor : undefined} assignGroupId={assignGroupId} assignBusy={assignBusy} onAssignGroupChange={setAssignGroupId} onAssign={assignGroup} />}{kind === "products" && selectedProduct && canManage && onSetAvailability && <div className="catalog-availability" role="group" aria-label="Ürün kullanılabilirliği"><span className={selectedProduct.isAvailable ? "catalog-available" : "catalog-suspended"}>{selectedProduct.isAvailable ? "Satışa açık" : "Menüden kaldırıldı"}</span><Button data-telemetry-id="b.catalog.catalogworkspace.10" variant={selectedProduct.isAvailable ? "secondary" : "primary"} disabled={availabilityBusy} onClick={() => void toggleAvailability(selectedProduct)}>{availabilityBusy ? "…" : selectedProduct.isAvailable ? "Menüden kaldır (86)" : "Menüye geri al"}</Button></div>}{kind === "products" && selectedProduct && canManage && onSetPrepTime && <div className="catalog-availability catalog-prep-time" role="group" aria-label="Ürün hazırlama süresi"><TextField label="Hazırlama süresi (dk)" type="number" min={1} max={180} placeholder="Girilmedi" value={prepTimeInput} onChange={(event) => setPrepTimeInput(event.target.value)} /><Button data-telemetry-id="b.catalog.catalogworkspace.11" variant="secondary" disabled={prepTimeBusy} onClick={() => void savePrepTime(selectedProduct)}>{prepTimeBusy ? "…" : "Kaydet"}</Button></div>}</div>}
    <CatalogEditor open={editorOpen} editing={editing !== null} kind={kind} draft={draft} data={data} errors={formErrors} submitting={submitting} onDraft={setDraft} onClose={() => setEditorOpen(false)} onSubmit={submit} />
  </section>;
}

interface Row { id: string; title: string; subtitle: string; value: string; active: boolean }

function catalogRows(kind: CatalogEntityKind, data: CatalogData): Row[] {
  if (kind === "categories") return data.categories.map((item) => ({ id: item.id, title: item.name, subtitle: item.code, value: `Sıra ${item.sortOrder}`, active: item.active }));
  if (kind === "taxes") return data.taxes.map((item) => ({ id: item.id, title: item.name, subtitle: item.code, value: `%${item.vatRate} KDV`, active: item.active }));
  if (kind === "products") return data.products.map((item) => ({ id: item.id, title: item.name, subtitle: `${item.sku} · ${productTypeLabels[item.productType] ?? "Tanımsız"}`, value: item.currentPrice === null ? "Fiyat yok" : `${item.currentPrice.toFixed(2)} TRY`, active: item.active }));
  if (kind === "modifierGroups") return data.modifierGroups.map((item) => ({ id: item.id, title: item.name, subtitle: item.code, value: `${selectionTypeLabels[item.selectionType] ?? item.selectionType} · ${item.minSelections}-${item.maxSelections}`, active: item.active }));
  if (kind === "modifiers") return data.modifiers.map((item) => ({ id: item.id, title: item.name, subtitle: item.code, value: `${item.priceDelta >= 0 ? "+" : ""}${item.priceDelta.toFixed(2)} TRY`, active: item.active }));
  return data.prices.map((item) => ({ id: item.id, title: `${item.price.toFixed(2)} ${item.currencyCode}`, subtitle: `${priceTypeLabels[item.priceType] ?? "Tanımsız"} · ${data.products.find((product) => product.id === item.productId)?.name ?? "Bilinmeyen ürün"}`, value: item.effectiveTo ? "Süreli" : "Açık uçlu", active: true }));
}

function catalogCount(kind: CatalogEntityKind, data: CatalogData) { return kind === "categories" ? data.categories.length : kind === "taxes" ? data.taxes.length : kind === "products" ? data.products.length : kind === "modifierGroups" ? data.modifierGroups.length : kind === "modifiers" ? data.modifiers.length : data.prices.length; }

function CatalogDetails({ kind, row, product, data, canAssign, onEdit, assignGroupId, assignBusy, onAssignGroupChange, onAssign }: { kind: CatalogEntityKind; row: Row; product?: CatalogProduct; data: CatalogData; canAssign: boolean; onEdit?: (product: CatalogProduct) => void; assignGroupId: string; assignBusy: boolean; onAssignGroupChange: (id: string) => void; onAssign: (product: CatalogProduct) => void }) {
  const prices = product ? data.prices.filter((price) => price.productId === product.id).sort((a, b) => b.effectiveFrom.localeCompare(a.effectiveFrom)) : [];
  const modifiers = product ? data.modifiers.filter((modifier) => modifier.productId === product.id) : [];
  const assignedGroupIds = product ? data.assignments.filter((item) => item.productId === product.id).map((item) => item.modifierGroupId) : [];
  const assignedGroups = data.modifierGroups.filter((group) => assignedGroupIds.includes(group.id));
  const assignableGroups = data.modifierGroups.filter((group) => group.active && !assignedGroupIds.includes(group.id));
  return <aside className="catalog-details" aria-label={`${row.title} ayrıntıları`}><div className="catalog-details__heading"><span className="catalog-workspace__kicker">SEÇİLİ KAYIT</span><h3>{row.title}</h3><span>{catalogEntityLabels[kind]}</span></div><dl><div><dt>Kod / kimlik</dt><dd>{row.subtitle}</dd></div><div><dt>Durum</dt><dd>{row.active ? "Aktif" : "Pasif"}</dd></div><div><dt>Değer</dt><dd>{row.value}</dd></div>{product && <><div><dt>Stok modu</dt><dd>{stockModeLabels[product.stockMode] ?? "Tanımsız"}</dd></div><div><dt>KDV profili</dt><dd>{product.taxProfileId ? data.taxes.find((tax) => tax.id === product.taxProfileId)?.name ?? "Tanımsız" : "Atanmamış"}</dd></div></>}</dl>{product && <><section className="catalog-details__section"><span>MODİFİKATÖR ATAMALARI</span>{modifiers.length ? <ul>{modifiers.map((modifier) => <li key={modifier.id}>{modifier.name} <small>{modifier.priceDelta >= 0 ? "+" : ""}{modifier.priceDelta.toFixed(2)} TRY</small></li>)}</ul> : <p>Bu ürüne modifikatör atanmadı.</p>}</section><section className="catalog-details__section"><span>BAĞLI MODİFİKATÖR GRUPLARI</span>{assignedGroups.length ? <ul>{assignedGroups.map((group) => <li key={group.id}>{group.name} <small>{data.modifiers.filter((item) => item.modifierGroupId === group.id).length} seçenek</small></li>)}</ul> : <p>Bu ürüne modifikatör grubu bağlı değil.</p>}{canAssign && <div className="catalog-details__assign"><label>Grup ata<select value={assignGroupId} onChange={(event) => onAssignGroupChange(event.target.value)}><option value="">Grup seçin</option>{assignableGroups.map((group) => <option key={group.id} value={group.id}>{group.name}</option>)}</select></label><Button data-telemetry-id="b.catalog.catalogworkspace.14" variant="secondary" disabled={!assignGroupId || assignBusy} onClick={() => onAssign(product)}>Grubu ata</Button></div>}</section><section className="catalog-details__section"><span>GEÇERLİ FİYAT ZAMAN ÇİZELGESİ</span>{prices.length ? <ol>{prices.map((price) => <li key={price.id}><strong>{price.price.toFixed(2)} {price.currencyCode}</strong><small>{new Date(price.effectiveFrom).toLocaleDateString("tr-TR")} → {price.effectiveTo ? new Date(price.effectiveTo).toLocaleDateString("tr-TR") : "açık uçlu"}</small></li>)}</ol> : <p className="catalog-inactive">Geçerli fiyat yok; satılabilir değil.</p>}</section></>}{product && onEdit && <Button data-telemetry-id="b.catalog.catalogworkspace.15" variant="secondary" onClick={() => onEdit(product)}>Ürünü düzenle</Button>}<p className="catalog-details__note">Bu ekran yalnız kayıt yönetir. Yayınlama veya kasiyer görünürlüğü, aktif ve geçerli fiyat kuralları sağlanmadan değişmez.</p></aside>;
}

function CatalogEditor({ open, editing, kind, draft, data, errors, submitting, onDraft, onClose, onSubmit }: { open: boolean; editing: boolean; submitting: boolean; kind: CatalogEntityKind; draft: Draft; data: CatalogData; errors: readonly string[]; onDraft: (draft: Draft) => void; onClose: () => void; onSubmit: (event: FormEvent) => void }) {
  return <ModalDialog open={open} title={editing ? "Ürünü düzenle" : `Yeni ${catalogEntityLabels[kind].toLocaleLowerCase("tr-TR").replace(/ler$|lar$/i, "")}`} onClose={onClose}><form className="catalog-form" onSubmit={onSubmit}><ValidationSummary title="Formu kontrol edin" errors={errors} />{kind === "categories" && <><TextField label="Kod" value={draft.code} onChange={(event) => onDraft({ ...draft, code: event.target.value })} placeholder="SALON" /><TextField label="Kategori adı" value={draft.name} onChange={(event) => onDraft({ ...draft, name: event.target.value })} placeholder="Salon" /><TextField label="Sıra" type="number" min={0} value={draft.sortOrder} onChange={(event) => onDraft({ ...draft, sortOrder: event.target.value })} /></>}{kind === "taxes" && <><TextField label="Kod" value={draft.code} onChange={(event) => onDraft({ ...draft, code: event.target.value })} placeholder="VAT10" /><TextField label="Vergi profili" value={draft.name} onChange={(event) => onDraft({ ...draft, name: event.target.value })} placeholder="KDV %10" /><TextField label="KDV oranı" type="number" min={0} max={100} step="0.01" value={draft.vatRate} onChange={(event) => onDraft({ ...draft, vatRate: event.target.value })} /></>}{kind === "products" && <><TextField label="SKU" value={draft.sku} onChange={(event) => onDraft({ ...draft, sku: event.target.value })} placeholder="ESP-01" /><TextField label="Ürün adı" value={draft.name} onChange={(event) => onDraft({ ...draft, name: event.target.value })} placeholder="Espresso" />{!editing && <><SelectField label="Ürün tipi" value={draft.productType} onChange={(event) => onDraft({ ...draft, productType: event.target.value })}><option value="MenuItem">{productTypeLabels.MenuItem}</option><option value="Modifier">{productTypeLabels.Modifier}</option><option value="AddOn">{productTypeLabels.AddOn}</option><option value="Packaging">{productTypeLabels.Packaging}</option><option value="ServiceItem">{productTypeLabels.ServiceItem}</option></SelectField><SelectField label="Stok modu" hint={stockModeHint} value={draft.stockMode} onChange={(event) => onDraft({ ...draft, stockMode: event.target.value })}>{selectableStockModes.map((mode) => <option key={mode} value={mode}>{stockModeLabels[mode]}</option>)}</SelectField></>}<SelectField label="KDV profili" value={draft.taxProfileId} onChange={(event) => onDraft({ ...draft, taxProfileId: event.target.value })}><option value="">KDV profili seçin</option>{data.taxes.filter((item) => item.active).map((item) => <option key={item.id} value={item.id}>{item.name} · %{item.vatRate}</option>)}</SelectField><SelectField label="Kategori (opsiyonel)" value={draft.categoryId} onChange={(event) => onDraft({ ...draft, categoryId: event.target.value })}><option value="">Kategorisiz</option>{data.categories.filter((item) => item.active).map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}</SelectField>{!editing && <><TextField label="Satış fiyatı (TRY)" type="number" min={0} step="0.01" value={draft.price} onChange={(event) => onDraft({ ...draft, price: event.target.value })} /><TextField label="Hazırlama süresi (dk, opsiyonel)" type="number" min={1} max={180} value={draft.prepTimeMinutes} onChange={(event) => onDraft({ ...draft, prepTimeMinutes: event.target.value })} /></>}</>}{kind === "modifierGroups" && <><TextField label="Kod" value={draft.code} onChange={(event) => onDraft({ ...draft, code: event.target.value })} placeholder="MILK" /><TextField label="Grup adı" value={draft.name} onChange={(event) => onDraft({ ...draft, name: event.target.value })} placeholder="Süt seçimi" /><SelectField label="Seçim türü" value={draft.selectionType} onChange={(event) => onDraft({ ...draft, selectionType: event.target.value })}><option value="SelectOne">{selectionTypeLabels.SelectOne}</option><option value="SelectMany">{selectionTypeLabels.SelectMany}</option></SelectField><TextField label="Minimum seçim" type="number" min={0} value={draft.minSelections} onChange={(event) => onDraft({ ...draft, minSelections: event.target.value })} /><TextField label="Maksimum seçim" type="number" min={1} value={draft.maxSelections} onChange={(event) => onDraft({ ...draft, maxSelections: event.target.value })} /></>}{kind === "modifiers" && <><TextField label="Kod" value={draft.code} onChange={(event) => onDraft({ ...draft, code: event.target.value })} placeholder="SYC-S" /><TextField label="Modifikatör adı" value={draft.name} onChange={(event) => onDraft({ ...draft, name: event.target.value })} placeholder="Soya sütü" /><SelectField label="Grup" value={draft.modifierGroupId} onChange={(event) => onDraft({ ...draft, modifierGroupId: event.target.value })}><option value="">Grup seçin</option>{data.modifierGroups.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}</SelectField><TextField label="Fiyat farkı (TRY)" type="number" step="0.01" value={draft.priceDelta} onChange={(event) => onDraft({ ...draft, priceDelta: event.target.value })} /></>}{kind === "prices" && <><SelectField label="Ürün" value={draft.productId} onChange={(event) => onDraft({ ...draft, productId: event.target.value })}><option value="">Ürün seçin</option>{data.products.map((item) => <option key={item.id} value={item.id}>{item.name} · {item.sku}</option>)}</SelectField><TextField label="Fiyat (TRY)" type="number" min={0} step="0.01" value={draft.price} onChange={(event) => onDraft({ ...draft, price: event.target.value })} /><TextField label="Geçerlilik başlangıcı" type="datetime-local" value={draft.effectiveFrom} onChange={(event) => onDraft({ ...draft, effectiveFrom: event.target.value })} /></>}<div className="catalog-form__actions"><Button data-telemetry-id="b.catalog.catalogworkspace.12" variant="secondary" onClick={onClose}>Vazgeç</Button><Button data-telemetry-id="b.catalog.catalogworkspace.13" type="submit" disabled={submitting}>{editing ? "Kaydet" : "Kaydı oluştur"}</Button></div></form></ModalDialog>;
}

function validate(kind: CatalogEntityKind, draft: Draft, editing = false): string[] {
  const errors: string[] = [];
  if (kind === "categories" && !draft.code.trim()) errors.push("Kod alanı gerekli.");
  if ((kind === "categories" || kind === "taxes" || kind === "modifierGroups" || kind === "modifiers") && !draft.name.trim()) errors.push("Ad alanı gerekli.");
  if ((kind === "taxes") && (!Number.isFinite(Number(draft.vatRate)) || Number(draft.vatRate) < 0 || Number(draft.vatRate) > 100)) errors.push("KDV oranı 0-100 arasında olmalı.");
  if (kind === "products" && !draft.sku.trim()) errors.push("SKU alanı gerekli.");
  if (kind === "products" && !draft.name.trim()) errors.push("Ürün adı alanı gerekli.");
  if (kind === "products" && !draft.taxProfileId) errors.push("KDV profili seçin.");
  if (!editing && (kind === "products" || kind === "prices") && (!draft.price || !Number.isFinite(Number(draft.price)) || Number(draft.price) < 0)) errors.push("Fiyat alanı 0 veya daha büyük olmalı.");
  if (!editing && kind === "products" && draft.prepTimeMinutes.trim() && (!Number.isInteger(Number(draft.prepTimeMinutes)) || Number(draft.prepTimeMinutes) < 1 || Number(draft.prepTimeMinutes) > 180)) errors.push("Hazırlama süresi 1-180 dakika arasında bir tam sayı olmalı.");
  if (kind === "modifierGroups" && !draft.code.trim()) errors.push("Kod alanı gerekli.");
  if (kind === "modifierGroups" && (!Number.isInteger(Number(draft.minSelections)) || Number(draft.minSelections) < 0)) errors.push("Minimum seçim 0 veya daha büyük bir tam sayı olmalı.");
  if (kind === "modifierGroups" && (!Number.isInteger(Number(draft.maxSelections)) || Number(draft.maxSelections) < 1)) errors.push("Maksimum seçim 1 veya daha büyük bir tam sayı olmalı.");
  if (kind === "modifierGroups" && Number(draft.minSelections) > Number(draft.maxSelections)) errors.push("Minimum seçim, maksimum seçimden büyük olamaz.");
  if (kind === "modifiers" && !draft.modifierGroupId) errors.push("Modifikatör grubu seçin.");
  if (kind === "prices" && !draft.productId) errors.push("Ürün seçin.");
  if (kind === "prices" && !draft.effectiveFrom) errors.push("Geçerlilik başlangıcı gerekli.");
  return errors;
}

function toCreateInput(kind: CatalogEntityKind, draft: Draft, id: string): CatalogCreateInput {
  if (kind === "categories") return { kind, value: { id, code: draft.code.trim().toUpperCase(), name: draft.name.trim(), sortOrder: Number(draft.sortOrder) || 0 } };
  if (kind === "taxes") return { kind, value: { id, code: draft.code.trim().toUpperCase(), name: draft.name.trim(), vatRate: Number(draft.vatRate) } };
  if (kind === "products") return { kind, value: { id, sku: draft.sku.trim(), name: draft.name.trim(), productType: draft.productType, stockMode: draft.stockMode, categoryId: draft.categoryId || null, taxProfileId: draft.taxProfileId || null, description: draft.description || null, printerRoutePolicy: null, displayOrder: 0, currentPrice: Number(draft.price), prepTimeMinutes: draft.prepTimeMinutes.trim() ? Number(draft.prepTimeMinutes) : null } };
  if (kind === "modifierGroups") return { kind, value: { id, code: draft.code.trim().toUpperCase(), name: draft.name.trim(), selectionType: draft.selectionType, minSelections: Number(draft.minSelections) || 0, maxSelections: Number(draft.maxSelections) || 1 } };
  if (kind === "modifiers") return { kind, value: { id, modifierGroupId: draft.modifierGroupId, code: draft.code.trim().toUpperCase(), name: draft.name.trim(), priceDelta: Number(draft.priceDelta) || 0, productId: draft.productId || null } };
  return { kind, value: { id, productId: draft.productId, priceType: draft.priceType, price: Number(draft.price), currencyCode: "TRY", effectiveFrom: new Date(draft.effectiveFrom).toISOString() } };
}
