import { useCallback, useEffect, useId, useMemo, useState } from "react";
import { formatMoney } from "../../format";
import {
  OnlineMenuApiError,
  loadOnlineMenu,
  loadPlatforms,
  publicationLabel,
  publishMenu,
  removeMapping,
  saleLabel,
  saveMapping,
  validationLabel,
  type OnlineMenu,
  type OnlineMenuProduct,
  type OnlinePlatform,
} from "./onlineMenuApi";
import "./online-menu.css";

const when = (iso: string) =>
  new Date(iso).toLocaleString("tr-TR", { day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit" });
const money = (value: number | null) => (value === null ? "—" : formatMoney(value));
const failure = (error: unknown, fallback: string) => (error instanceof OnlineMenuApiError ? error.message : fallback);

/**
 * V12-OUI-005: a manager maps each catalog product to its platform product, sees what the platform shows (on sale, last
 * published price), and publishes a catalog menu to the platform. On Trendyol Go the platform's own menu is read, so a
 * product is picked from it; on Yemeksepeti the platform code is typed.
 */
export function OnlineMenuTab({ terminalId }: { terminalId: string }) {
  const [platforms, setPlatforms] = useState<OnlinePlatform[]>([]);
  const [provider, setProvider] = useState<string>();
  const [menu, setMenu] = useState<OnlineMenu | null>(null);
  const [loadError, setLoadError] = useState<string>();
  const [actionError, setActionError] = useState<string>();
  const [notice, setNotice] = useState<string>();
  const [busy, setBusy] = useState(false);
  const [menuId, setMenuId] = useState("");
  const [search, setSearch] = useState("");
  const [onlyUnmapped, setOnlyUnmapped] = useState(false);
  const [drafts, setDrafts] = useState<Record<string, string>>({});
  const searchId = useId();
  const menuSelectId = useId();
  const codesListId = useId();

  useEffect(() => {
    void (async () => {
      try {
        const list = await loadPlatforms(terminalId);
        setPlatforms(list);
        setProvider((current) => current ?? list[0]?.provider);
      } catch (error) {
        setLoadError(failure(error, "Platformlar okunamadı."));
      }
    })();
  }, [terminalId]);

  const load = useCallback(async () => {
    if (!provider) return;
    try {
      const loaded = await loadOnlineMenu(terminalId, provider);
      setMenu(loaded);
      setMenuId((current) => (loaded.menus.some((m) => m.menuId === current) ? current : loaded.menus[0]?.menuId ?? ""));
      setLoadError(undefined);
    } catch (error) {
      setLoadError(failure(error, "Menü bilgileri okunamadı."));
    }
  }, [terminalId, provider]);

  useEffect(() => { setMenu(null); setDrafts({}); void load(); }, [load]);

  const act = async (work: () => Promise<string | undefined>) => {
    setBusy(true);
    setActionError(undefined);
    setNotice(undefined);
    try {
      setNotice(await work());
      await load();
    } catch (error) {
      setActionError(failure(error, "İşlem tamamlanamadı."));
    } finally {
      setBusy(false);
    }
  };

  const map = (product: OnlineMenuProduct) => act(async () => {
    const code = (drafts[product.productId] ?? "").trim();
    if (!code) throw new OnlineMenuApiError("Önce platform ürününü seçin veya kodunu yazın.");
    await saveMapping(terminalId, provider!, product.productId, code);
    setDrafts((current) => ({ ...current, [product.productId]: "" }));
    return `${product.name} eşlendi.`;
  });

  const unmap = (product: OnlineMenuProduct) => act(async () => {
    await removeMapping(terminalId, provider!, product.productId);
    return `${product.name} eşlemesi kaldırıldı.`;
  });

  const publish = () => act(async () => {
    const result = await publishMenu(terminalId, menu!.channel, menuId);
    const missing = result.validationErrors.length;
    const byName = new Map(menu!.products.map((p) => [p.productId, p.name]));
    const detail = result.validationErrors.slice(0, 5)
      .map((e) => `${byName.get(e.productId) ?? "Bir ürün"}: ${validationLabel(e.code)}`).join("; ");
    return `${publicationLabel({ status: result.status, hasError: false })} — ${result.itemCount} ürün.`
      + (missing > 0 ? ` ${missing} ürün yayınlanamadı: ${detail}${missing > 5 ? "…" : ""}` : "");
  });

  const products = useMemo(() => {
    const term = search.trim().toLocaleLowerCase("tr-TR");
    return (menu?.products ?? []).filter((p) =>
      (!onlyUnmapped || p.externalSku === null)
      && (term === "" || p.name.toLocaleLowerCase("tr-TR").includes(term) || p.catalogSku.toLocaleLowerCase("tr-TR").includes(term)));
  }, [menu, search, onlyUnmapped]);

  const platformNames = useMemo(() => new Map((menu?.platformProducts ?? []).map((p) => [p.id, p.name])), [menu]);
  const unmappedPlatform = (menu?.platformProducts ?? []).filter((p) => p.mappedProductId === null);
  const staleMappings = menu?.platformProducts
    ? menu.products.filter((p) => p.externalSku !== null && !platformNames.has(p.externalSku))
    : [];
  const displayName = platforms.find((p) => p.provider === provider)?.displayName ?? "Platform";

  return (
    <section className="online-menu" aria-label="Online menü">
      {platforms.length > 1 && (
        <div className="online-menu__platforms" role="group" aria-label="Platform seç">
          {platforms.map((p) => (
            <button key={p.provider} type="button" aria-pressed={p.provider === provider} onClick={() => setProvider(p.provider)}>
              {p.displayName}
            </button>
          ))}
        </div>
      )}
      {loadError && <p className="online-menu__error" role="alert">{loadError}</p>}
      {menu === null && !loadError && <p>Yükleniyor…</p>}
      {menu && (
        <>
          <div className="online-menu__publish">
            <label htmlFor={menuSelectId}>Yayınlanacak menü</label>
            <select id={menuSelectId} value={menuId} onChange={(event) => setMenuId(event.target.value)} disabled={busy || menu.menus.length === 0}>
              {menu.menus.length === 0 && <option value="">Menü yok</option>}
              {menu.menus.map((m) => <option key={m.menuId} value={m.menuId}>{m.name}</option>)}
            </select>
            <button type="button" disabled={busy || menuId === ""} onClick={() => void publish()}>
              {displayName} menüsünü yayınla
            </button>
          </div>
          {actionError && <p className="online-menu__error" role="alert">{actionError}</p>}
          <p className="online-menu__notice" role="status" aria-live="polite">{notice ?? ""}</p>

          {menu.publications.length > 0 && (
            <ul className="online-menu__publications" aria-label="Son yayınlar">
              {menu.publications.map((p) => (
                <li key={p.publicationId}>
                  {when(p.requestedAt)} — {publicationLabel(p)}, {p.itemCount} ürün
                  {p.validationErrorCount > 0 ? `, ${p.validationErrorCount} ürün yayınlanamadı` : ""}
                </li>
              ))}
            </ul>
          )}

          {menu.platformMenuUnavailable && (
            <p className="online-menu__hint">Platform menüsü okunamadı; platform ürün kodunu elle yazabilirsiniz.</p>
          )}
          {menu.unmappedCodes.length > 0 && (
            <section className="online-menu__warning" aria-label="Eşlenmemiş platform kodları">
              <p>Bu kodlar için gelen siparişler reddedildi; ilgili ürüne eşleyin:</p>
              <ul>
                {menu.unmappedCodes.map((c) => (
                  <li key={c.code}>
                    {platformNames.get(c.code) ? `${platformNames.get(c.code)} (${c.code})` : c.code} — {c.orderCount} sipariş, son {when(c.lastSeenAt)}
                  </li>
                ))}
              </ul>
            </section>
          )}
          {unmappedPlatform.length > 0 && (
            <p className="online-menu__hint">
              Platformda eşlenmemiş {unmappedPlatform.length} ürün var: {unmappedPlatform.slice(0, 8).map((p) => p.name || p.id).join(", ")}
              {unmappedPlatform.length > 8 ? "…" : ""}
            </p>
          )}
          {staleMappings.length > 0 && (
            <p className="online-menu__warning">
              Platform menüsünde bulunmayan eşleme: {staleMappings.map((p) => p.name).join(", ")}. Bu ürünler yayınlanmaz.
            </p>
          )}

          <div className="online-menu__filters">
            <label htmlFor={searchId}>Ürün ara</label>
            <input id={searchId} type="search" value={search} onChange={(event) => setSearch(event.target.value)} />
            <label className="online-menu__check">
              <input type="checkbox" checked={onlyUnmapped} onChange={(event) => setOnlyUnmapped(event.target.checked)} />
              Yalnız eşlenmemişler
            </label>
          </div>

          <datalist id={codesListId}>
            {menu.unmappedCodes.map((c) => <option key={c.code} value={c.code}>{c.code}</option>)}
          </datalist>

          <table className="online-menu__table">
            <caption className="online-menu__sr">{displayName} ürün eşlemeleri</caption>
            <thead>
              <tr>
                <th scope="col">Ürün</th><th scope="col">Katalog fiyatı</th><th scope="col">Son yayınlanan fiyat</th>
                <th scope="col">Platformda</th><th scope="col">Platform ürünü</th>
              </tr>
            </thead>
            <tbody>
              {products.map((product) => (
                <tr key={product.productId}>
                  <td>{product.name}{product.active ? "" : " (pasif)"}</td>
                  <td>{money(product.catalogPrice)}</td>
                  <td>{money(product.publishedPrice)}{product.publishedPrice !== null && product.catalogPrice !== null && product.publishedPrice !== product.catalogPrice ? " (farklı)" : ""}</td>
                  <td>{saleLabel(product.saleState)}</td>
                  <td>
                    {product.externalSku !== null ? (
                      <span className="online-menu__mapped">
                        {platformNames.get(product.externalSku) ?? product.externalSku}
                        <button type="button" className="online-menu__secondary" disabled={busy}
                          aria-label={`${product.name} eşlemesini kaldır`} onClick={() => void unmap(product)}>Kaldır</button>
                      </span>
                    ) : (
                      <span className="online-menu__mapped">
                        {menu.platformProducts ? (
                          <select aria-label={`${product.name} için platform ürünü`} value={drafts[product.productId] ?? ""} disabled={busy}
                            onChange={(event) => { const value = event.target.value; setDrafts((current) => ({ ...current, [product.productId]: value })); }}>
                            <option value="">Seçin</option>
                            {unmappedPlatform.map((p) => <option key={p.id} value={p.id}>{p.name || p.id}</option>)}
                          </select>
                        ) : (
                          <input aria-label={`${product.name} için platform ürün kodu`} list={codesListId} value={drafts[product.productId] ?? ""} disabled={busy}
                            onChange={(event) => { const value = event.target.value; setDrafts((current) => ({ ...current, [product.productId]: value })); }} />
                        )}
                        <button type="button" disabled={busy || !product.active} aria-label={`${product.name} ürününü eşle`} onClick={() => void map(product)}>Eşle</button>
                      </span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {products.length === 0 && <p className="online-menu__hint">Gösterilecek ürün yok.</p>}
        </>
      )}
    </section>
  );
}
