import { useMemo, useState, type FormEvent } from "react";
import { ApiError } from "../../api";
import { Button, ModalDialog, SelectField, StateMessage, TextField, ValidationSummary } from "../../design-system";
import { commonActions, stateText } from "../../strings";
import {
  catalogAddLabels,
  catalogEntityLabels,
  productTypeLabels,
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
};

const emptyDraft = (): Draft => ({
  code: "", name: "", description: "", sku: "", productType: "MenuItem", stockMode: "Untracked",
  categoryId: "", taxProfileId: "", modifierGroupId: "", productId: "", priceType: "SalePrice",
  price: "", vatRate: "10", sortOrder: "0", priceDelta: "0", effectiveFrom: new Date().toISOString().slice(0, 16),
});

const kindOrder: readonly CatalogEntityKind[] = ["products", "categories", "taxes", "modifiers", "prices"];

export function CatalogWorkspace({ state, data, canManage, onRefresh, onCreate, onSetAvailability, errorMessage: suppliedError, lastUpdated }: CatalogWorkspaceProps) {
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

  const openEditor = () => {
    setDraft(emptyDraft());
    setFormErrors([]);
    setFeedback(null);
    setEditorOpen(true);
  };

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    const errors = validate(kind, draft);
    if (errors.length || !onCreate) {
      setFormErrors(errors.length ? errors : ["Bu işlem için katalog yönetimi yetkisi gerekli."]);
      return;
    }
    try {
      await onCreate(toCreateInput(kind, draft, crypto.randomUUID()));
      setEditorOpen(false);
      setFeedback({ tone: "success", message: `${catalogEntityLabels[kind]} kaydı oluşturuldu.` });
    } catch (reason) {
      const message = reason instanceof ApiError ? reason.message : "Kayıt oluşturulamadı.";
      const conflict = /409|conflict|overlap|duplicate/i.test(message);
      setFeedback({ tone: conflict ? "conflict" : "error", message: conflict ? "Kayıt çakıştı. Formunuz korunuyor; güncel veriyi yenileyin." : message });
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
      setFeedback({ tone: "error", message: reason instanceof ApiError ? reason.message : "Kullanılabilirlik güncellenemedi." });
    } finally {
      setAvailabilityBusy(false);
    }
  };

  if (state === "loading" || state === "busy") return <div className="catalog-workspace catalog-workspace--state" aria-busy="true"><StateMessage tone="info" title={state === "busy" ? "Katalog kaydediliyor" : "Katalog yükleniyor"}><p>Yetkili katalog verisi alınıyor…</p></StateMessage></div>;
  if (state === "unauthorized") return <div className="catalog-workspace catalog-workspace--state"><StateMessage tone="unauthorized" title={stateText.managerUnauthorizedTitle}><p>Katalog düzenlemek için yetkili yönetici hesabıyla giriş yapın.</p></StateMessage></div>;
  if (state === "offline") return <div className="catalog-workspace catalog-workspace--state"><StateMessage tone="offline" title={stateText.offlineTitle}><p>Eski katalog verisiyle değişiklik yapılamaz.</p><Button onClick={onRefresh}>{commonActions.retry}</Button></StateMessage></div>;
  if (state === "error") return <div className="catalog-workspace catalog-workspace--state"><StateMessage tone="error" title="Katalog alınamadı"><p>{suppliedError ?? stateText.unexpectedError}</p><Button onClick={onRefresh}>{commonActions.reload}</Button></StateMessage></div>;
  if (state === "stale" || state === "conflict") return <div className="catalog-workspace catalog-workspace--state"><StateMessage tone="conflict" title={state === "stale" ? "Katalog güncel değil" : "Katalog çakışması"}><p>Satışa açık bilgiyi değiştirmeden önce sunucunun son halini alın.</p><Button onClick={onRefresh}>Güncel veriyi al</Button></StateMessage></div>;

  return <section className="catalog-workspace" aria-label="Menü ve katalog yönetimi">
    <header className="catalog-workspace__header"><div><span className="catalog-workspace__kicker">YÖNETİM / KATALOG</span><h2>Menü ve katalog</h2><p>{lastUpdated ? `Son güncelleme ${lastUpdated}` : "Fiyat, ürün ve modifikatör kayıtları"}</p></div><div className="catalog-workspace__header-actions">{canManage && onCreate && <Button onClick={openEditor}>+ {catalogAddLabels[kind]} ekle</Button>}<Button variant="secondary" onClick={() => void onRefresh()}>{commonActions.refresh}</Button></div></header>
    {feedback && <div className={`catalog-workspace__feedback catalog-workspace__feedback--${feedback.tone}`} role={feedback.tone === "success" ? "status" : "alert"} aria-live="polite"><span>{feedback.message}</span><button type="button" aria-label={stateText.dismissMessage} onClick={() => setFeedback(null)}>×</button></div>}
    <nav className="catalog-workspace__tabs" aria-label="Katalog kaynak türü">{kindOrder.map((item) => <button key={item} type="button" className={kind === item ? "is-active" : ""} aria-current={kind === item ? "page" : undefined} onClick={() => changeKind(item)}>{catalogEntityLabels[item]}<span>{catalogCount(item, data)}</span></button>)}</nav>
    <div className="catalog-workspace__toolbar"><label>Katalog ara<input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Kod, SKU veya ad" aria-label="Katalog ara" /></label><span className="catalog-workspace__authority">Yayınlama yok · yalnızca yönetici ekleyip düzenleyebilir</span></div>
    {state === "empty" || filteredRows.length === 0 ? <div className="catalog-workspace__empty"><StateMessage tone="info" title={state === "empty" ? "Katalog kaydı yok" : "Eşleşen kayıt yok"}><p>{state === "empty" ? "Bu kaynak türünde ilk kaydı oluşturarak başlayın." : "Arama ifadenizi değiştirin veya temizleyin."}</p>{canManage && onCreate && <Button onClick={openEditor}>İlk kaydı ekle</Button>}</StateMessage></div> : <div className="catalog-workspace__content"><div className="catalog-list">{filteredRows.map((row) => <button type="button" key={row.id} className={`catalog-row ${selected?.id === row.id ? "is-selected" : ""}`} aria-pressed={selected?.id === row.id} onClick={() => setSelectedId(row.id)}><span className="catalog-row__title"><strong>{row.title}</strong><span className={row.active ? "catalog-active" : "catalog-inactive"}>{row.active ? "Aktif" : "Pasif"}</span></span><span className="catalog-row__subtitle">{row.subtitle}</span><span className="catalog-row__value">{row.value}</span></button>)}</div>{selected && <CatalogDetails kind={kind} row={selected} product={selectedProduct} data={data} />}{kind === "products" && selectedProduct && canManage && onSetAvailability && <div className="catalog-availability" role="group" aria-label="Ürün kullanılabilirliği"><span className={selectedProduct.isAvailable ? "catalog-available" : "catalog-suspended"}>{selectedProduct.isAvailable ? "Satışa açık" : "Menüden kaldırıldı"}</span><Button variant={selectedProduct.isAvailable ? "secondary" : "primary"} disabled={availabilityBusy} onClick={() => void toggleAvailability(selectedProduct)}>{availabilityBusy ? "…" : selectedProduct.isAvailable ? "Menüden kaldır (86)" : "Menüye geri al"}</Button></div>}</div>}
    <CatalogEditor open={editorOpen} kind={kind} draft={draft} data={data} errors={formErrors} onDraft={setDraft} onClose={() => setEditorOpen(false)} onSubmit={submit} />
  </section>;
}

interface Row { id: string; title: string; subtitle: string; value: string; active: boolean }

function catalogRows(kind: CatalogEntityKind, data: CatalogData): Row[] {
  if (kind === "categories") return data.categories.map((item) => ({ id: item.id, title: item.name, subtitle: item.code, value: `Sıra ${item.sortOrder}`, active: item.active }));
  if (kind === "taxes") return data.taxes.map((item) => ({ id: item.id, title: item.name, subtitle: item.code, value: `%${item.vatRate} KDV`, active: item.active }));
  if (kind === "products") return data.products.map((item) => ({ id: item.id, title: item.name, subtitle: `${item.sku} · ${item.productType}`, value: item.currentPrice === null ? "Fiyat yok" : `${item.currentPrice.toFixed(2)} TRY`, active: item.active }));
  if (kind === "modifiers") return data.modifiers.map((item) => ({ id: item.id, title: item.name, subtitle: item.code, value: `${item.priceDelta >= 0 ? "+" : ""}${item.priceDelta.toFixed(2)} TRY`, active: item.active }));
  return data.prices.map((item) => ({ id: item.id, title: `${item.price.toFixed(2)} ${item.currencyCode}`, subtitle: `${item.priceType} · ${item.productId.slice(0, 8)}`, value: item.effectiveTo ? "Süreli" : "Açık uçlu", active: true }));
}

function catalogCount(kind: CatalogEntityKind, data: CatalogData) { return kind === "categories" ? data.categories.length : kind === "taxes" ? data.taxes.length : kind === "products" ? data.products.length : kind === "modifiers" ? data.modifiers.length : data.prices.length; }

function CatalogDetails({ kind, row, product, data }: { kind: CatalogEntityKind; row: Row; product?: CatalogProduct; data: CatalogData }) {
  const prices = product ? data.prices.filter((price) => price.productId === product.id).sort((a, b) => b.effectiveFrom.localeCompare(a.effectiveFrom)) : [];
  const modifiers = product ? data.modifiers.filter((modifier) => modifier.productId === product.id) : [];
  return <aside className="catalog-details" aria-label={`${row.title} ayrıntıları`}><div className="catalog-details__heading"><span className="catalog-workspace__kicker">SEÇİLİ KAYIT</span><h3>{row.title}</h3><span>{catalogEntityLabels[kind]}</span></div><dl><div><dt>Kod / kimlik</dt><dd>{row.subtitle}</dd></div><div><dt>Durum</dt><dd>{row.active ? "Aktif" : "Pasif"}</dd></div><div><dt>Değer</dt><dd>{row.value}</dd></div>{product && <><div><dt>Stok modu</dt><dd>{product.stockMode}</dd></div><div><dt>KDV profili</dt><dd>{product.taxProfileId ?? "Atanmamış"}</dd></div></>}</dl>{product && <><section className="catalog-details__section"><span>MODİFİKATÖR ATAMALARI</span>{modifiers.length ? <ul>{modifiers.map((modifier) => <li key={modifier.id}>{modifier.name} <small>{modifier.priceDelta >= 0 ? "+" : ""}{modifier.priceDelta.toFixed(2)} TRY</small></li>)}</ul> : <p>Bu ürüne modifikatör atanmadı.</p>}</section><section className="catalog-details__section"><span>GEÇERLİ FİYAT ZAMAN ÇİZELGESİ</span>{prices.length ? <ol>{prices.map((price) => <li key={price.id}><strong>{price.price.toFixed(2)} {price.currencyCode}</strong><small>{new Date(price.effectiveFrom).toLocaleDateString("tr-TR")} → {price.effectiveTo ? new Date(price.effectiveTo).toLocaleDateString("tr-TR") : "açık uçlu"}</small></li>)}</ol> : <p className="catalog-inactive">Geçerli fiyat yok; satılabilir değil.</p>}</section></>}<p className="catalog-details__note">Bu ekran yalnız kayıt yönetir. Yayınlama veya kasiyer görünürlüğü, aktif ve geçerli fiyat kuralları sağlanmadan değişmez.</p></aside>;
}

function CatalogEditor({ open, kind, draft, data, errors, onDraft, onClose, onSubmit }: { open: boolean; kind: CatalogEntityKind; draft: Draft; data: CatalogData; errors: readonly string[]; onDraft: (draft: Draft) => void; onClose: () => void; onSubmit: (event: FormEvent) => void }) {
  return <ModalDialog open={open} title={`Yeni ${catalogEntityLabels[kind].toLocaleLowerCase("tr-TR").replace(/ler$|lar$/i, "")}`} onClose={onClose}><form className="catalog-form" onSubmit={onSubmit}><ValidationSummary title="Formu kontrol edin" errors={errors} />{kind === "categories" && <><TextField label="Kod" value={draft.code} onChange={(event) => onDraft({ ...draft, code: event.target.value })} placeholder="SALON" /><TextField label="Kategori adı" value={draft.name} onChange={(event) => onDraft({ ...draft, name: event.target.value })} placeholder="Salon" /><TextField label="Sıra" type="number" min={0} value={draft.sortOrder} onChange={(event) => onDraft({ ...draft, sortOrder: event.target.value })} /></>}{kind === "taxes" && <><TextField label="Kod" value={draft.code} onChange={(event) => onDraft({ ...draft, code: event.target.value })} placeholder="VAT10" /><TextField label="Vergi profili" value={draft.name} onChange={(event) => onDraft({ ...draft, name: event.target.value })} placeholder="KDV %10" /><TextField label="KDV oranı" type="number" min={0} max={100} step="0.01" value={draft.vatRate} onChange={(event) => onDraft({ ...draft, vatRate: event.target.value })} /></>}{kind === "products" && <><TextField label="SKU" value={draft.sku} onChange={(event) => onDraft({ ...draft, sku: event.target.value })} placeholder="ESP-01" /><TextField label="Ürün adı" value={draft.name} onChange={(event) => onDraft({ ...draft, name: event.target.value })} placeholder="Espresso" /><SelectField label="Ürün tipi" value={draft.productType} onChange={(event) => onDraft({ ...draft, productType: event.target.value })}><option value="MenuItem">{productTypeLabels.MenuItem}</option><option value="Modifier">{productTypeLabels.Modifier}</option><option value="AddOn">{productTypeLabels.AddOn}</option><option value="Packaging">{productTypeLabels.Packaging}</option><option value="ServiceItem">{productTypeLabels.ServiceItem}</option></SelectField><SelectField label="Stok modu" value={draft.stockMode} onChange={(event) => onDraft({ ...draft, stockMode: event.target.value })}><option value="Untracked">Takipsiz</option><option value="QuantityTracked">Miktar takipli</option><option value="PortionTracked">Porsiyon takipli</option><option value="RecipeDerived">Reçeteden</option></SelectField><TextField label="Satış fiyatı (TRY)" type="number" min={0} step="0.01" value={draft.price} onChange={(event) => onDraft({ ...draft, price: event.target.value })} /></>}{kind === "modifiers" && <><TextField label="Kod" value={draft.code} onChange={(event) => onDraft({ ...draft, code: event.target.value })} placeholder="SYC-S" /><TextField label="Modifikatör adı" value={draft.name} onChange={(event) => onDraft({ ...draft, name: event.target.value })} placeholder="Soya sütü" /><SelectField label="Grup" value={draft.modifierGroupId} onChange={(event) => onDraft({ ...draft, modifierGroupId: event.target.value })}><option value="">Grup seçin</option>{data.modifierGroups.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}</SelectField><TextField label="Fiyat farkı (TRY)" type="number" step="0.01" value={draft.priceDelta} onChange={(event) => onDraft({ ...draft, priceDelta: event.target.value })} /></>}{kind === "prices" && <><SelectField label="Ürün" value={draft.productId} onChange={(event) => onDraft({ ...draft, productId: event.target.value })}><option value="">Ürün seçin</option>{data.products.map((item) => <option key={item.id} value={item.id}>{item.name} · {item.sku}</option>)}</SelectField><TextField label="Fiyat (TRY)" type="number" min={0} step="0.01" value={draft.price} onChange={(event) => onDraft({ ...draft, price: event.target.value })} /><TextField label="Geçerlilik başlangıcı" type="datetime-local" value={draft.effectiveFrom} onChange={(event) => onDraft({ ...draft, effectiveFrom: event.target.value })} /></>}<div className="catalog-form__actions"><Button variant="secondary" onClick={onClose}>Vazgeç</Button><Button type="submit">Kaydı oluştur</Button></div></form></ModalDialog>;
}

function validate(kind: CatalogEntityKind, draft: Draft): string[] {
  const errors: string[] = [];
  if (kind === "categories" && !draft.code.trim()) errors.push("Kod alanı gerekli.");
  if ((kind === "categories" || kind === "taxes" || kind === "modifiers") && !draft.name.trim()) errors.push("Ad alanı gerekli.");
  if ((kind === "taxes") && (!Number.isFinite(Number(draft.vatRate)) || Number(draft.vatRate) < 0 || Number(draft.vatRate) > 100)) errors.push("KDV oranı 0-100 arasında olmalı.");
  if (kind === "products" && !draft.sku.trim()) errors.push("SKU alanı gerekli.");
  if (kind === "products" && !draft.name.trim()) errors.push("Ürün adı alanı gerekli.");
  if ((kind === "products" || kind === "prices") && (!draft.price || !Number.isFinite(Number(draft.price)) || Number(draft.price) < 0)) errors.push("Fiyat alanı 0 veya daha büyük olmalı.");
  if (kind === "modifiers" && !draft.modifierGroupId) errors.push("Modifikatör grubu seçin.");
  if (kind === "prices" && !draft.productId) errors.push("Ürün seçin.");
  if (kind === "prices" && !draft.effectiveFrom) errors.push("Geçerlilik başlangıcı gerekli.");
  return errors;
}

function toCreateInput(kind: CatalogEntityKind, draft: Draft, id: string): CatalogCreateInput {
  if (kind === "categories") return { kind, value: { id, code: draft.code.trim().toUpperCase(), name: draft.name.trim(), sortOrder: Number(draft.sortOrder) || 0 } };
  if (kind === "taxes") return { kind, value: { id, code: draft.code.trim().toUpperCase(), name: draft.name.trim(), vatRate: Number(draft.vatRate) } };
  if (kind === "products") return { kind, value: { id, sku: draft.sku.trim(), name: draft.name.trim(), productType: draft.productType, stockMode: draft.stockMode, categoryId: draft.categoryId || null, taxProfileId: draft.taxProfileId || null, description: draft.description || null, printerRoutePolicy: null, displayOrder: 0, currentPrice: Number(draft.price) } };
  if (kind === "modifiers") return { kind, value: { id, modifierGroupId: draft.modifierGroupId, code: draft.code.trim().toUpperCase(), name: draft.name.trim(), priceDelta: Number(draft.priceDelta) || 0, productId: draft.productId || null } };
  return { kind, value: { id, productId: draft.productId, priceType: draft.priceType, price: Number(draft.price), currencyCode: "TRY", effectiveFrom: new Date(draft.effectiveFrom).toISOString() } };
}
