import { useCallback, useMemo, useState } from "react";
import { Button, SelectField, TextField } from "../../design-system";
import { formatQuantity } from "../../format";
import { managementText } from "../../strings";
import type { ManagementSectionProps } from "../management/sections";
import { Notice, PanelView, errorText, usePanel } from "../management/panel";
import { createRecipesClient, type RecipesClient } from "./api";
import { parseNumber, versionStatusLabel, type ProductMapping, type Recipe, type RecipeVersion } from "./models";
import "./management-recipes.css";

const t = managementText.recipes;

export function RecipesSection({ client }: ManagementSectionProps & { client?: RecipesClient }) {
  const api = useMemo(() => client ?? createRecipesClient(), [client]);
  const [notice, setNotice] = useState<{ tone: "success" | "error"; text: string }>();
  const [busy, setBusy] = useState(false);
  const [fields, setFields] = useState<Record<string, string>>({});
  const field = (key: string) => fields[key] ?? "";
  const set = (key: string) => (event: { target: { value: string } }) => setFields((current) => ({ ...current, [key]: event.target.value }));
  const [openRecipeId, setOpenRecipeId] = useState<string>();
  const [openVersionId, setOpenVersionId] = useState<string>();
  const [mappingProductId, setMappingProductId] = useState("");

  const [recipes, reloadRecipes] = usePanel<readonly Recipe[]>(useCallback(() => api.listRecipes(), [api]), t.loadFailed);
  const [versions, reloadVersions] = usePanel<readonly RecipeVersion[]>(useCallback(
    () => openRecipeId ? api.listVersions(openRecipeId) : Promise.resolve([]), [api, openRecipeId]), t.loadFailed);
  const [conversions, reloadConversions] = usePanel(useCallback(() => api.listConversions(), [api]), t.loadFailed);
  const [lookups, reloadLookups] = usePanel(useCallback(
    () => Promise.all([api.listStockItems(), api.listProducts()]).then(([stockItems, products]) => ({ stockItems, products })), [api]), t.loadFailed);
  const [mapping, reloadMapping] = usePanel<ProductMapping | null>(useCallback(
    () => mappingProductId ? api.getMapping(mappingProductId) : Promise.resolve(null), [api, mappingProductId]), t.loadFailed);

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
  const stockItems = lookups.state === "ready" ? lookups.data.stockItems : [];
  const stockName = (id: string) => stockItems.find((item) => item.id === id)?.name ?? "—";
  const recipeName = (id: string) => recipes.state === "ready" ? recipes.data.find((recipe) => recipe.id === id)?.name ?? "—" : "—";
  const openVersion = versions.state === "ready" ? versions.data.find((version) => version.id === openVersionId) : undefined;

  return <div className="mrcp">
    <Notice notice={notice} />

    <PanelView title={t.heading} loading={t.loading} failed={t.loadFailed} panel={recipes} onRetry={reloadRecipes}>
      {(list) => <div>
        {list.length === 0 ? <p className="mrcp__empty">{t.empty}</p> : <table>
          <thead><tr><th>{t.code}</th><th>{t.name}</th><th>{t.actions}</th></tr></thead>
          <tbody>{list.map((recipe) => <tr key={recipe.id}>
            <td>{recipe.code}</td><td>{recipe.name}</td>
            <td><Button data-telemetry-id="b.management-recipes.recipessection.1" variant="quiet" onClick={() => { setOpenRecipeId(recipe.id); setOpenVersionId(undefined); }}>{t.showVersions}</Button></td>
          </tr>)}</tbody>
        </table>}

        {openRecipeId && versions.state === "ready" && <div className="mrcp__sub">
          <h4>{t.versions} · {recipeName(openRecipeId)}</h4>
          {versions.data.length === 0 ? <p className="mrcp__empty">{t.empty}</p> : <table>
            <thead><tr><th>{t.version}</th><th>{t.status}</th><th>{t.yield}</th><th>{t.locked}</th><th>{t.actions}</th></tr></thead>
            <tbody>{versions.data.map((version) => <tr key={version.id}>
              <td>{version.versionNumber}</td><td>{versionStatusLabel(version.status)}</td>
              <td>{formatQuantity(version.yieldQuantity)} {version.yieldUnitCode}</td><td>{version.isLocked ? t.locked : t.notLocked}</td>
              <td className="mrcp__actions">
                <Button data-telemetry-id="b.management-recipes.recipessection.2" variant="quiet" onClick={() => setOpenVersionId(version.id)}>{t.showIngredients}</Button>
                {version.status === "Draft" && <Button data-telemetry-id="b.management-recipes.recipessection.3" variant="quiet" disabled={busy}
                  onClick={() => void act(() => api.activate(version.recipeId, version.versionNumber), t.activated, reloadVersions)}>{t.activate}</Button>}
                {version.status === "Active" && !version.isLocked && <Button data-telemetry-id="b.management-recipes.recipessection.4" variant="quiet" disabled={busy}
                  onClick={() => void act(() => api.lock(version.id), t.lockedDone, reloadVersions)}>{t.lock}</Button>}
              </td></tr>)}</tbody>
          </table>}

          {versions.data.length === 0 ? <form className="mrcp__form" aria-label={t.firstVersion} onSubmit={(event) => {
            event.preventDefault();
            const quantity = parseNumber(field("v-yield"));
            if (quantity === null) return invalid();
            void act(() => api.createFirstVersion(openRecipeId, quantity, field("v-unit").trim()), t.versionAdded, reloadVersions, () => setFields({}));
          }}>
            <TextField id="v-yield" label={t.yieldQuantity} inputMode="decimal" value={field("v-yield")} onChange={set("v-yield")} />
            <TextField id="v-unit" label={t.yieldUnit} value={field("v-unit")} onChange={set("v-unit")} />
            <Button data-telemetry-id="b.management-recipes.recipessection.5" type="submit" disabled={busy}>{t.firstVersion}</Button>
          </form> : <Button data-telemetry-id="b.management-recipes.recipessection.6" variant="secondary" disabled={busy}
            onClick={() => void act(() => api.createNextVersion(openRecipeId), t.versionAdded, reloadVersions)}>{t.nextVersion}</Button>}

          {openVersion && <div className="mrcp__sub">
            <h4>{t.ingredients} · {t.version} {openVersion.versionNumber}</h4>
            {openVersion.ingredients.length === 0 ? <p className="mrcp__empty">{t.empty}</p> : <table>
              <thead><tr><th>{t.ingredient}</th><th>{t.quantity}</th><th>{t.loss}</th><th>{t.actions}</th></tr></thead>
              <tbody>{openVersion.ingredients.map((ingredient) => <tr key={ingredient.ingredientItemId}>
                <td>{stockName(ingredient.ingredientItemId)}</td>
                <td>{formatQuantity(ingredient.quantity)} {ingredient.unitCode}</td><td>{formatQuantity(ingredient.lossPercentage)}</td>
                <td>{openVersion.status === "Draft" && <Button data-telemetry-id="b.management-recipes.recipessection.7" variant="quiet" disabled={busy}
                  onClick={() => void act(() => api.removeIngredient(openVersion.id, ingredient.ingredientItemId), t.ingredientRemoved, reloadVersions)}>{t.remove}</Button>}</td>
              </tr>)}</tbody>
            </table>}
            {openVersion.status === "Draft" && <form className="mrcp__form" aria-label={t.addIngredient} onSubmit={(event) => {
              event.preventDefault();
              const quantity = parseNumber(field("i-quantity"));
              const loss = field("i-loss").trim() === "" ? 0 : parseNumber(field("i-loss"));
              if (quantity === null || loss === null) return invalid();
              void act(() => api.addIngredient(openVersion.id, {
                ingredientItemId: field("i-item"), quantity, unitCode: field("i-unit").trim(), lossPercentage: loss,
              }), t.ingredientAdded, reloadVersions, () => setFields({}));
            }}>
              <SelectField id="i-item" label={t.ingredient} value={field("i-item")} onChange={(event) => {
                const unit = stockItems.find((item) => item.id === event.target.value)?.trackingUnitCode ?? "";
                setFields((current) => ({ ...current, "i-item": event.target.value, "i-unit": unit }));
              }}>
                <option value="">{t.pick}</option>
                {stockItems.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
              </SelectField>
              <TextField id="i-quantity" label={t.quantity} inputMode="decimal" value={field("i-quantity")} onChange={set("i-quantity")} />
              <TextField id="i-unit" label={t.unit} value={field("i-unit")} onChange={set("i-unit")} />
              <TextField id="i-loss" label={t.loss} inputMode="decimal" value={field("i-loss")} onChange={set("i-loss")} />
              <Button data-telemetry-id="b.management-recipes.recipessection.8" type="submit" disabled={busy || !field("i-item")}>{t.add}</Button>
            </form>}
          </div>}
        </div>}

        <form className="mrcp__form" aria-label={t.newRecipe} onSubmit={(event) => {
          event.preventDefault();
          void act(() => api.createRecipe(field("r-code").trim(), field("r-name").trim(), field("r-description")), t.recipeAdded, reloadRecipes, () => setFields({}));
        }}>
          <h4>{t.newRecipe}</h4>
          <TextField id="r-code" label={t.code} value={field("r-code")} onChange={set("r-code")} />
          <TextField id="r-name" label={t.name} value={field("r-name")} onChange={set("r-name")} />
          <TextField id="r-description" label={t.description} value={field("r-description")} onChange={set("r-description")} />
          <Button data-telemetry-id="b.management-recipes.recipessection.9" type="submit" disabled={busy}>{t.add}</Button>
        </form>
      </div>}
    </PanelView>

    <PanelView title={t.mappingHeading} loading={t.loading} failed={t.loadFailed} panel={lookups} onRetry={reloadLookups}>
      {(data) => <div>
        <SelectField id="m-product" label={t.product} value={mappingProductId} onChange={(event) => setMappingProductId(event.target.value)}>
          <option value="">{t.pick}</option>
          {data.products.map((product) => <option key={product.id} value={product.id}>{product.name}</option>)}
        </SelectField>
        {mappingProductId && mapping.state === "ready" && (mapping.data ? <p>
          {t.mappedTo}: {recipeName(mapping.data.recipeId)}{" "}
          <Button data-telemetry-id="b.management-recipes.recipessection.10" variant="quiet" disabled={busy} onClick={() => void act(() => api.removeMapping(mappingProductId), t.mappingRemoved, reloadMapping)}>{t.unmap}</Button>
        </p> : <p className="mrcp__empty">{t.noMapping}</p>)}
        {mappingProductId && <form className="mrcp__form" aria-label={t.assign} onSubmit={(event) => {
          event.preventDefault();
          void act(() => api.assignMapping(mappingProductId, field("m-recipe")), t.mappingSaved, reloadMapping, () => setFields({}));
        }}>
          <SelectField id="m-recipe" label={t.recipe} value={field("m-recipe")} onChange={set("m-recipe")}>
            <option value="">{t.pick}</option>
            {recipes.state === "ready" && recipes.data.map((recipe) => <option key={recipe.id} value={recipe.id}>{recipe.name}</option>)}
          </SelectField>
          <Button data-telemetry-id="b.management-recipes.recipessection.11" type="submit" disabled={busy || !field("m-recipe")}>{t.assign}</Button>
        </form>}
      </div>}
    </PanelView>

    <PanelView title={t.conversionsHeading} loading={t.loading} failed={t.loadFailed} panel={conversions} onRetry={reloadConversions}>
      {(list) => <div>
        {list.length === 0 ? <p className="mrcp__empty">{t.empty}</p> : <table>
          <thead><tr><th>{t.fromUnit}</th><th>{t.toUnit}</th><th>{t.factor}</th></tr></thead>
          <tbody>{list.map((conversion) => <tr key={conversion.id}>
            <td>{conversion.fromUnitCode}</td><td>{conversion.toUnitCode}</td><td>{formatQuantity(conversion.factor)}</td>
          </tr>)}</tbody>
        </table>}
        <form className="mrcp__form" aria-label={t.newConversion} onSubmit={(event) => {
          event.preventDefault();
          const factor = parseNumber(field("c-factor"));
          if (factor === null) return invalid();
          void act(() => api.createConversion(field("c-from").trim(), field("c-to").trim(), factor), t.conversionAdded, reloadConversions, () => setFields({}));
        }}>
          <h4>{t.newConversion}</h4>
          <TextField id="c-from" label={t.fromUnit} value={field("c-from")} onChange={set("c-from")} />
          <TextField id="c-to" label={t.toUnit} value={field("c-to")} onChange={set("c-to")} />
          <TextField id="c-factor" label={t.factor} inputMode="decimal" value={field("c-factor")} onChange={set("c-factor")} />
          <Button data-telemetry-id="b.management-recipes.recipessection.12" type="submit" disabled={busy}>{t.add}</Button>
        </form>
      </div>}
    </PanelView>
  </div>;
}
