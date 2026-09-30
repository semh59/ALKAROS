import { useCallback, useMemo, useState, type FormEvent } from "react";
import { Button, SelectField, TextField } from "../../design-system";
import { formatMoney, formatQuantity } from "../../format";
import { managementText } from "../../strings";
import type { ManagementSectionProps } from "../management/sections";
import { createMenusClient, type MenusClient } from "./api";
import { Notice, PanelView, errorText, usePanel } from "./panel";
import { dailyMenuStatusLabel, parseNumber, type DailyMenuDetails, type DailyMenuItem, type Menu, type MenuComposition, type ProductOption, type RecipeVersionOption } from "./models";
import "./management-menus-recipe-cost.css";

const t = managementText.menus;
const today = () => new Date().toLocaleDateString("sv-SE");
type ItemEdit = { kind: "price" | "portions"; item: DailyMenuItem };

export function MenusSection({ client }: ManagementSectionProps & { client?: MenusClient }) {
  const api = useMemo(() => client ?? createMenusClient(), [client]);
  const [notice, setNotice] = useState<{ tone: "success" | "error"; text: string }>();
  const [busy, setBusy] = useState(false);
  const [fields, setFields] = useState<Record<string, string>>({});
  const field = (key: string, fallback = "") => fields[key] ?? fallback;
  const set = (key: string) => (event: { target: { value: string } }) => setFields((current) => ({ ...current, [key]: event.target.value }));
  const [openMenuId, setOpenMenuId] = useState<string>();
  const [date, setDate] = useState(today);
  const [dayQuery, setDayQuery] = useState(today);
  const [edit, setEdit] = useState<ItemEdit>();

  const [menus, reloadMenus] = usePanel<readonly Menu[]>(useCallback(() => api.listMenus(), [api]), t.loadFailed);
  const [composition, reloadComposition] = usePanel<MenuComposition | null>(useCallback(
    () => openMenuId ? api.getMenu(openMenuId) : Promise.resolve(null), [api, openMenuId]), t.loadFailed);
  const [daily, reloadDaily] = usePanel<DailyMenuDetails | null>(useCallback(() => api.getDailyMenu(dayQuery), [api, dayQuery]), t.loadFailed);
  const [lookups] = usePanel<{ products: readonly ProductOption[]; versions: readonly RecipeVersionOption[] }>(useCallback(
    () => Promise.all([api.listProducts(), api.listRecipeVersions()]).then(([products, versions]) => ({ products, versions })), [api]), t.loadFailed);

  async function act(work: () => Promise<unknown>, success: string, refresh: () => void, done?: () => void) {
    setBusy(true);
    try {
      await work();
      setNotice({ tone: "success", text: success });
      done?.();
      refresh();
    } catch (reason) {
      setNotice({ tone: "error", text: errorText(reason, t.actionFailed) });
    } finally {
      setBusy(false);
    }
  }
  const invalid = () => setNotice({ tone: "error", text: t.invalidNumber });
  const productOptions = lookups.state === "ready" ? lookups.data.products.map((product) => <option key={product.id} value={product.id}>{product.name}</option>) : null;

  function submitEdit(event: FormEvent) {
    event.preventDefault();
    if (!edit) return;
    const value = parseNumber(field("edit"));
    if (value === null) return invalid();
    const work = edit.kind === "price" ? () => api.setDailyItemPrice(edit.item.id, value) : () => api.setDailyItemPortions(edit.item.id, value);
    void act(work, edit.kind === "price" ? t.priceUpdated : t.portionsUpdated, reloadDaily, () => setEdit(undefined));
  }

  return <div className="mrc">
    <Notice notice={notice} />

    <PanelView title={t.menusHeading} loading={t.loading} failed={t.loadFailed} panel={menus} onRetry={reloadMenus}>
      {(list) => <div>
        {list.length === 0 ? <p className="mrc__empty">{t.empty}</p> : <table>
          <thead><tr><th>{t.code}</th><th>{t.name}</th><th>{t.status}</th><th>{t.actions}</th></tr></thead>
          <tbody>{list.map((menu) => <tr key={menu.id}>
            <td>{menu.code}</td><td>{menu.name}</td><td>{menu.isActive ? t.active : t.passive}</td>
            <td className="mrc__actions">
              <Button variant="quiet" onClick={() => setOpenMenuId(menu.id)}>{t.manage}</Button>
              <Button variant="quiet" disabled={busy} onClick={() => void act(() => api.setMenuActive(menu, !menu.isActive), t.menuUpdated, reloadMenus)}>{menu.isActive ? t.deactivate : t.activate}</Button>
            </td></tr>)}</tbody>
        </table>}

        {openMenuId && composition.state === "ready" && composition.data && <div className="mrc__sub">
          <h4>{composition.data.menu.name}</h4>
          {composition.data.items.length === 0 ? <p className="mrc__empty">{t.empty}</p> : <table>
            <thead><tr><th>{t.product}</th><th>{t.order}</th><th>{t.status}</th><th>{t.actions}</th></tr></thead>
            <tbody>{composition.data.items.map((item) => <tr key={item.menuItemId}>
              <td>{item.productName}{item.isProductActiveInCatalog ? "" : ` (${t.inactiveInCatalog})`}</td><td>{item.displayOrder}</td><td>{item.isActive ? t.active : t.passive}</td>
              <td><Button variant="quiet" disabled={busy} onClick={() => void act(() => api.setMenuItem(composition.data!.menu.id, item.menuItemId, item.displayOrder, !item.isActive), t.itemUpdated, reloadComposition)}>{item.isActive ? t.deactivate : t.activate}</Button></td></tr>)}</tbody>
          </table>}
          <form className="mrc__form" aria-label={t.addProduct} onSubmit={(event) => {
            event.preventDefault();
            const order = field("m-order").trim() === "" ? 0 : parseNumber(field("m-order"));
            if (order === null) return invalid();
            void act(() => api.addMenuItem(composition.data!.menu.id, field("m-product"), Math.trunc(order)), t.productAdded, reloadComposition, () => setFields({}));
          }}>
            <SelectField id="m-product" label={t.product} value={field("m-product")} onChange={set("m-product")}><option value="">{t.pick}</option>{productOptions}</SelectField>
            <TextField id="m-order" label={t.order} inputMode="numeric" value={field("m-order")} onChange={set("m-order")} />
            <Button type="submit" disabled={busy}>{t.add}</Button>
          </form>
        </div>}

        <form className="mrc__form" aria-label={t.newMenu} onSubmit={(event) => {
          event.preventDefault();
          void act(() => api.createMenu(field("n-code"), field("n-name")), t.menuAdded, reloadMenus, () => setFields({}));
        }}>
          <h4>{t.newMenu}</h4>
          <TextField id="n-code" label={t.code} value={field("n-code")} onChange={set("n-code")} />
          <TextField id="n-name" label={t.name} value={field("n-name")} onChange={set("n-name")} />
          <Button type="submit" disabled={busy}>{t.add}</Button>
        </form>
      </div>}
    </PanelView>

    <PanelView title={t.dailyHeading} loading={t.loading} failed={t.loadFailed} panel={daily} onRetry={reloadDaily}>
      {(details) => <div>
        <form className="mrc__toolbar" onSubmit={(event) => { event.preventDefault(); setDayQuery(date); }}>
          <TextField id="d-date" label={t.date} type="date" value={date} onChange={(event) => setDate(event.target.value)} />
          <Button type="submit" variant="secondary">{t.load}</Button>
        </form>
        {details === null ? <div>
          <p>{t.noDaily}</p>
          <form className="mrc__form" aria-label={t.createDaily} onSubmit={(event) => {
            event.preventDefault();
            void act(() => api.createDailyMenu(dayQuery, field("d-note")), t.dailyCreated, reloadDaily, () => setFields({}));
          }}>
            <TextField id="d-note" label={t.note} value={field("d-note")} onChange={set("d-note")} />
            <Button type="submit" disabled={busy}>{t.createDaily}</Button>
          </form>
        </div> : <div>
          <p><strong>{dailyMenuStatusLabel(details.menu.status)}</strong>{details.menu.note ? ` · ${details.menu.note}` : ""}</p>
          <div className="mrc__actions">
            {details.menu.status === "Draft" && <Button disabled={busy} onClick={() => void act(() => api.openDailyMenu(details.menu.id), t.dailyOpened, reloadDaily)}>{t.openDaily}</Button>}
            {(details.menu.status === "Open" || details.menu.status === "PartiallyConsumed") && <Button variant="secondary" disabled={busy} onClick={() => void act(() => api.closeDailyMenu(details.menu.id), t.dailyClosed, reloadDaily)}>{t.closeDaily}</Button>}
          </div>
          {details.items.length === 0 ? <p className="mrc__empty">{t.empty}</p> : <table>
            <thead><tr><th>{t.product}</th><th>{t.price}</th><th>{t.planned}</th><th>{t.prepared}</th><th>{t.available}</th><th>{t.actions}</th></tr></thead>
            <tbody>{details.items.map((item) => <tr key={item.id}>
              <td>{item.productNameSnapshot}{item.isActive ? "" : ` (${t.passive})`}</td><td>{formatMoney(item.price)}</td><td>{formatQuantity(item.plannedPortions)}</td>
              <td>{formatQuantity(item.preparedPortions)}</td><td>{item.isOutOfStock ? t.outOfStock : formatQuantity(item.availablePortions)}</td>
              <td className="mrc__actions">
                {details.menu.status !== "Closed" && <>
                  <Button variant="quiet" onClick={() => { setNotice(undefined); setFields({ edit: String(item.price) }); setEdit({ kind: "price", item }); }}>{t.setPrice}</Button>
                  <Button variant="quiet" onClick={() => { setNotice(undefined); setFields({ edit: String(item.plannedPortions) }); setEdit({ kind: "portions", item }); }}>{t.setPortions}</Button>
                  <Button variant="quiet" disabled={busy} onClick={() => void act(() => api.setDailyItemActive(item.id, !item.isActive), t.itemUpdated, reloadDaily)}>{item.isActive ? t.deactivate : t.activate}</Button>
                </>}
              </td></tr>)}</tbody>
          </table>}
          {edit && <form className="mrc__form" aria-label={edit.item.productNameSnapshot} onSubmit={submitEdit}>
            <TextField id="d-edit" label={edit.kind === "price" ? t.price : t.planned} inputMode="decimal" value={field("edit")} onChange={set("edit")} />
            <div className="mrc__actions">
              <Button type="submit" disabled={busy}>{t.save}</Button>
              <Button type="button" variant="secondary" onClick={() => setEdit(undefined)}>{t.cancel}</Button>
            </div>
          </form>}
          {details.menu.status !== "Closed" && <form className="mrc__form" aria-label={t.addDailyItem} onSubmit={(event) => {
            event.preventDefault();
            const planned = parseNumber(field("i-planned"));
            const price = field("i-price").trim() === "" ? null : parseNumber(field("i-price"));
            if (planned === null || (field("i-price").trim() !== "" && price === null)) return invalid();
            void act(() => api.addDailyMenuItem(details.menu.id, { productId: field("i-product"), price, plannedPortions: planned, recipeVersionId: field("i-recipe") || null }),
              t.dailyItemAdded, reloadDaily, () => setFields({}));
          }}>
            <h4>{t.addDailyItem}</h4>
            <SelectField id="i-product" label={t.product} value={field("i-product")} onChange={set("i-product")}><option value="">{t.pick}</option>{productOptions}</SelectField>
            <TextField id="i-price" label={t.priceOptional} inputMode="decimal" value={field("i-price")} onChange={set("i-price")} />
            <TextField id="i-planned" label={t.planned} inputMode="decimal" value={field("i-planned")} onChange={set("i-planned")} />
            <SelectField id="i-recipe" label={t.recipeOptional} value={field("i-recipe")} onChange={set("i-recipe")}>
              <option value="">{t.none}</option>
              {lookups.state === "ready" && lookups.data.versions.filter((version) => version.status === "Active").map((version) => <option key={version.id} value={version.id}>{version.label}</option>)}
            </SelectField>
            <Button type="submit" disabled={busy}>{t.add}</Button>
          </form>}
        </div>}
      </div>}
    </PanelView>
  </div>;
}
