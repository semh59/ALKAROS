import { useCallback, useMemo, useState } from "react";
import { Button, SelectField, TextField } from "../../design-system";
import { formatMoney, formatQuantity } from "../../format";
import { managementText } from "../../strings";
import type { ManagementSectionProps } from "../management/sections";
import { createCostClient, type CostClient } from "./api";
import { Notice, PanelView, errorText, usePanel } from "../management/panel";
import { recipeStatusLabel, type CostSnapshot, type RecipeVersionOption } from "./models";
import "./management-menus-recipe-cost.css";

const t = managementText.recipeCost;
const today = () => new Date().toLocaleDateString("sv-SE");

export function RecipeCostSection({ client }: ManagementSectionProps & { client?: CostClient }) {
  const api = useMemo(() => client ?? createCostClient(), [client]);
  const [notice, setNotice] = useState<{ tone: "success" | "error"; text: string }>();
  const [busy, setBusy] = useState(false);
  const [versionId, setVersionId] = useState("");
  const [asOf, setAsOf] = useState(today);
  const [query, setQuery] = useState<{ versionId: string; asOf: string; nonce: number }>({ versionId: "", asOf: today(), nonce: 0 });

  const [versions, reloadVersions] = usePanel<readonly RecipeVersionOption[]>(useCallback(() => api.listRecipeVersions(), [api]), t.loadFailed);
  const [names] = usePanel<ReadonlyMap<string, string>>(useCallback(() => api.listStockItemNames(), [api]), t.loadFailed);
  const [snapshot] = usePanel<CostSnapshot | null>(useCallback(
    () => query.versionId ? api.getEffective(query.versionId, query.asOf) : Promise.resolve(null), [api, query]), t.loadFailed);

  async function calculate() {
    setBusy(true);
    try {
      await api.calculate(versionId, today());
      setNotice({ tone: "success", text: t.calculated });
      setQuery({ versionId, asOf: today(), nonce: query.nonce + 1 });
      setAsOf(today());
    } catch (reason) {
      setNotice({ tone: "error", text: errorText(reason, t.actionFailed) });
    } finally {
      setBusy(false);
    }
  }
  const ingredientName = (id: string) => names.state === "ready" ? names.data.get(id) ?? "—" : "—";

  return <div className="mrc">
    <Notice notice={notice} />
    <PanelView title={t.heading} loading={t.loading} failed={t.loadFailed} panel={versions} onRetry={reloadVersions}>
      {(list) => list.length === 0 ? <p className="mrc__empty">{t.noVersions}</p> : <div>
        <form className="mrc__toolbar" aria-label={t.heading} onSubmit={(event) => { event.preventDefault(); setNotice(undefined); setQuery({ versionId, asOf, nonce: query.nonce + 1 }); }}>
          <SelectField id="c-version" label={t.recipeVersion} value={versionId} onChange={(event) => setVersionId(event.target.value)}>
            <option value="">{t.pick}</option>
            {list.map((version) => <option key={version.id} value={version.id}>{version.label} ({recipeStatusLabel(version.status)})</option>)}
          </SelectField>
          <TextField id="c-date" label={t.asOf} type="date" value={asOf} onChange={(event) => setAsOf(event.target.value)} />
          <Button type="submit" variant="secondary" disabled={!versionId}>{t.show}</Button>
          <Button type="button" disabled={!versionId || busy} onClick={() => void calculate()}>{t.calculate}</Button>
        </form>
        {query.versionId && snapshot.state === "loading" && <p aria-busy="true">{t.loading}</p>}
        {query.versionId && snapshot.state === "error" && <p role="alert">{snapshot.message}</p>}
        {query.versionId && snapshot.state === "ready" && (snapshot.data === null ? <p className="mrc__empty">{t.none}</p> : <div>
          <dl className="mrc__facts">
            <div><dt>{t.basisDate}</dt><dd>{snapshot.data.costBasisDate}</dd></div>
            <div><dt>{t.total}</dt><dd>{formatMoney(snapshot.data.calculatedCost, snapshot.data.currency)}</dd></div>
            <div><dt>{t.perPortion}</dt><dd>{formatMoney(snapshot.data.costPerPortion, snapshot.data.currency)}</dd></div>
          </dl>
          <table>
            <thead><tr><th>{t.ingredient}</th><th>{t.quantity}</th><th>{t.unitCost}</th><th>{t.lineCost}</th></tr></thead>
            <tbody>{snapshot.data.items.map((item) => <tr key={item.stockItemId}>
              <td>{ingredientName(item.stockItemId)}</td><td>{formatQuantity(item.effectiveNativeQuantity)} {item.nativeUnitCode}</td>
              <td>{formatMoney(item.unitCost, snapshot.data!.currency)}</td><td>{formatMoney(item.lineCost, snapshot.data!.currency)}</td></tr>)}</tbody>
          </table>
        </div>)}
      </div>}
    </PanelView>
  </div>;
}
