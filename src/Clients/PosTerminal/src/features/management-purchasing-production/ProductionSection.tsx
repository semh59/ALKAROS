import { useCallback, useMemo, useState, type FormEvent } from "react";
import { Button, SelectField, TextField } from "../../design-system";
import { formatQuantity } from "../../format";
import { managementText } from "../../strings";
import type { ManagementSectionProps } from "../management/sections";
import { createProductionClient, type ProductionClient } from "./api";
import { Notice, PanelView, errorText, usePanel } from "../management/panel";
import { batchStatusLabel, batchStatuses, parseQuantity, type Batch, type LookupItem, type LookupLocation, type RecipeVersionOption } from "./models";
import "./management-purchasing-production.css";

const t = managementText.production;
type RowAction = { kind: "complete" | "cancel"; batch: Batch };

export function ProductionSection({ client }: ManagementSectionProps & { client?: ProductionClient }) {
  const api = useMemo(() => client ?? createProductionClient(), [client]);
  const [notice, setNotice] = useState<{ tone: "success" | "error"; text: string }>();
  const [busy, setBusy] = useState(false);
  const [statusFilter, setStatusFilter] = useState("");
  const [fields, setFields] = useState<Record<string, string>>({});
  const [action, setAction] = useState<RowAction>();
  const field = (key: string, fallback = "") => fields[key] ?? fallback;
  const set = (key: string) => (event: { target: { value: string } }) => setFields((current) => ({ ...current, [key]: event.target.value }));

  const [batches, reloadBatches] = usePanel<readonly Batch[]>(useCallback(() => api.listBatches(statusFilter), [api, statusFilter]), t.loadFailed);
  const [lookups] = usePanel<{ versions: readonly RecipeVersionOption[]; locations: readonly LookupLocation[]; items: readonly LookupItem[] }>(useCallback(
    () => Promise.all([api.listRecipeVersions(), api.listLocations(), api.listStockItems()]).then(([versions, locations, items]) => ({ versions, locations, items })),
    [api]), t.loadFailed);

  async function act(work: () => Promise<void>, success: string, done?: () => void) {
    setBusy(true);
    try {
      await work();
      setNotice({ tone: "success", text: success });
      done?.();
      reloadBatches();
    } catch (reason) {
      setNotice({ tone: "error", text: errorText(reason, t.actionFailed) });
    } finally {
      setBusy(false);
    }
  }
  const open = (next: RowAction) => { setNotice(undefined); setFields({}); setAction(next); };

  function submitAction(event: FormEvent) {
    event.preventDefault();
    if (!action) return;
    if (action.kind === "cancel") return void act(() => api.cancelBatch(action.batch.id, field("reason")), t.batchCancelled, () => setAction(undefined));
    const actual = parseQuantity(field("actual"));
    if (actual === null) return setNotice({ tone: "error", text: t.invalidNumber });
    if (!field("source")) return setNotice({ tone: "error", text: t.pickLocation });
    void act(() => api.completeBatch(action.batch.id, {
      actualQuantity: actual, sourceLocationId: field("source"), destinationLocationId: field("destination") || null, outputStockItemId: field("output") || null,
    }), t.batchCompleted, () => setAction(undefined));
  }

  function submitBatch(event: FormEvent) {
    event.preventDefault();
    const planned = parseQuantity(field("b-planned"));
    if (planned === null) return setNotice({ tone: "error", text: t.invalidNumber });
    void act(() => api.createBatch({
      batchNumber: field("b-number"), recipeVersionId: field("b-version"), plannedQuantity: planned, portionUnitCode: field("b-unit", "portion"),
    }), t.batchCreated, () => setFields({}));
  }

  return <div className="mpp">
    <Notice notice={notice} />
    <PanelView title={t.batchesHeading} loading={t.loading} failed={t.loadFailed} panel={batches} onRetry={reloadBatches}>
      {(list) => <div>
        <SelectField id="b-filter" label={t.statusFilter} value={statusFilter} onChange={(event) => setStatusFilter(event.target.value)}>
          <option value="">{t.allStatuses}</option>
          {batchStatuses.map((status) => <option key={status} value={status}>{batchStatusLabel(status)}</option>)}
        </SelectField>
        {list.length === 0 ? <p className="mpp__empty">{t.empty}</p> : <table>
          <thead><tr><th>{t.batchNumber}</th><th>{t.status}</th><th>{t.planned}</th><th>{t.actual}</th><th>{t.actions}</th></tr></thead>
          <tbody>{list.map((batch) => <tr key={batch.id}>
            <td>{batch.batchNumber}</td><td>{batchStatusLabel(batch.status)}</td>
            <td>{formatQuantity(batch.plannedQuantity)} {batch.portionUnitCode}</td><td>{formatQuantity(batch.actualQuantity)} {batch.portionUnitCode}</td>
            <td className="mpp__actions">
              {batch.status === "Planned" && <Button variant="quiet" disabled={busy} onClick={() => void act(() => api.startBatch(batch.id), t.batchStarted)}>{t.start}</Button>}
              {batch.status === "InProgress" && <Button variant="quiet" disabled={busy} onClick={() => open({ kind: "complete", batch })}>{t.complete}</Button>}
              {(batch.status === "Planned" || batch.status === "InProgress") && <Button variant="quiet" disabled={busy} onClick={() => open({ kind: "cancel", batch })}>{t.cancel}</Button>}
            </td></tr>)}</tbody>
        </table>}

        {action && <form className="mpp__form" aria-label={`${action.batch.batchNumber}`} onSubmit={submitAction}>
          <h4>{action.batch.batchNumber} · {action.kind === "complete" ? t.complete : t.cancel}</h4>
          {action.kind === "complete" ? <>
            <TextField id="p-actual" label={t.actualQuantity} inputMode="decimal" value={field("actual")} onChange={(event) => setFields((current) => ({ ...current, actual: event.target.value }))} />
            <SelectField id="p-source" label={t.sourceLocation} value={field("source")} onChange={(event) => setFields((current) => ({ ...current, source: event.target.value }))}>
              <option value="">{t.pick}</option>
              {lookups.state === "ready" && lookups.data.locations.map((location) => <option key={location.id} value={location.id}>{location.name}</option>)}
            </SelectField>
            <SelectField id="p-destination" label={t.destinationLocation} value={field("destination")} onChange={(event) => setFields((current) => ({ ...current, destination: event.target.value }))}>
              <option value="">{t.none}</option>
              {lookups.state === "ready" && lookups.data.locations.map((location) => <option key={location.id} value={location.id}>{location.name}</option>)}
            </SelectField>
            <SelectField id="p-output" label={t.outputItem} value={field("output")} onChange={(event) => setFields((current) => ({ ...current, output: event.target.value }))}>
              <option value="">{t.none}</option>
              {lookups.state === "ready" && lookups.data.items.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
            </SelectField>
          </> : <TextField id="p-reason" label={t.reason} value={field("reason")} onChange={(event) => setFields((current) => ({ ...current, reason: event.target.value }))} />}
          <div className="mpp__actions">
            <Button type="submit" disabled={busy}>{action.kind === "complete" ? t.confirmComplete : t.confirmCancel}</Button>
            <Button type="button" variant="secondary" onClick={() => setAction(undefined)}>{t.dismiss}</Button>
          </div>
        </form>}

        <PanelView title={t.newBatch} loading={t.loading} failed={t.loadFailed} panel={lookups} onRetry={() => undefined}>
          {(lookup) => lookup.versions.length === 0 ? <p className="mpp__empty">{t.noRecipes}</p> : <form className="mpp__form" aria-label={t.newBatch} onSubmit={submitBatch}>
            <TextField id="b-number" label={t.batchNumber} value={field("b-number")} onChange={set("b-number")} />
            <SelectField id="b-version" label={t.recipeVersion} value={field("b-version")} onChange={set("b-version")}>
              <option value="">{t.pick}</option>
              {lookup.versions.map((version) => <option key={version.id} value={version.id}>{version.label}</option>)}
            </SelectField>
            <TextField id="b-planned" label={t.plannedQuantity} inputMode="decimal" value={field("b-planned")} onChange={set("b-planned")} />
            <TextField id="b-unit" label={t.portionUnit} value={field("b-unit", "portion")} onChange={set("b-unit")} />
            <Button type="submit" disabled={busy}>{t.create}</Button>
          </form>}
        </PanelView>
      </div>}
    </PanelView>
  </div>;
}
