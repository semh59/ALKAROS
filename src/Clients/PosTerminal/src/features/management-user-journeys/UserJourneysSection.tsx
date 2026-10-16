import { useCallback, useMemo, useState } from "react";
import { Button, SelectField, TextField } from "../../design-system";
import { managementText } from "../../strings";
import type { ManagementSectionProps } from "../management/sections";
import { PanelView, usePanel } from "../management/panel";
import { createUserJourneysClient, type UserJourneysClient } from "./api";
import {
  formatMilliseconds, formatWhen, isGuid, roleLabel, sortLabel, stepKindLabel, type ActionFilter,
} from "./models";
import "./management-user-journeys.css";

const t = managementText.journeys;
type View = "actions" | "requests" | "staff";

function Timeline({ api, traceId }: { api: UserJourneysClient; traceId: string }) {
  const [panel, reload] = usePanel(useCallback(() => api.journey(traceId), [api, traceId]), t.timelineFailed);
  return <PanelView title={t.timelineHeading} loading={t.loading} failed={t.timelineFailed} panel={panel} onRetry={reload}>
    {(journey) => {
      const total = Math.max(1, ...journey.steps.map((step) => step.offsetMilliseconds + step.durationMilliseconds));
      return <>
        {!journey.requestCaptured && <p className="mjny__help">{t.requestNotCaptured}</p>}
        {journey.droppedSpans > 0 && <p className="mjny__help">{t.droppedSteps.replace("{0}", String(journey.droppedSpans))}</p>}
        <table>
          <thead><tr><th>{t.step}</th><th>{t.what}</th><th>{t.startsAt}</th><th>{t.duration}</th><th /></tr></thead>
          <tbody>{journey.steps.map((step, index) => <tr key={`${index}-${step.kind}`} className={step.failed ? "mjny__failed" : undefined}>
            <td>{stepKindLabel(step.kind)}</td><td>{step.name}</td>
            <td>{formatMilliseconds(step.offsetMilliseconds)}</td>
            <td>{step.failed ? `${formatMilliseconds(step.durationMilliseconds)} · ${t.failed}` : formatMilliseconds(step.durationMilliseconds)}</td>
            <td><span className="mjny__bar" style={{ width: `${Math.max(1, (step.durationMilliseconds / total) * 100)}%` }} /></td>
          </tr>)}</tbody>
        </table>
      </>;
    }}
  </PanelView>;
}

function ActionsView({ api }: { api: UserJourneysClient }) {
  const [filter, setFilter] = useState<ActionFilter>({ sort: "slowest", role: "", terminalId: "" });
  const validTerminal = filter.terminalId === "" || isGuid(filter.terminalId);
  const [panel, reload] = usePanel(
    useCallback(() => api.actions(validTerminal ? filter : { ...filter, terminalId: "" }), [api, filter, validTerminal]), t.loadFailed);
  return <>
    <div className="mjny__filters">
      <SelectField id="uj-sort" label={t.sortBy} value={filter.sort} onChange={(event) => setFilter({ ...filter, sort: event.target.value })}>
        {["slowest", "failing"].map((value) => <option key={value} value={value}>{sortLabel(value)}</option>)}
      </SelectField>
      <SelectField id="uj-role" label={t.role} value={filter.role} onChange={(event) => setFilter({ ...filter, role: event.target.value })}>
        <option value="">{t.all}</option>
        {["cashier", "waiter", "kitchen", "manager"].map((value) => <option key={value} value={value}>{roleLabel(value)}</option>)}
      </SelectField>
      <TextField id="uj-terminal" label={t.terminal} value={filter.terminalId} error={validTerminal ? undefined : t.terminalInvalid}
        onChange={(event) => setFilter({ ...filter, terminalId: event.target.value.trim() })} />
    </div>
    <PanelView title={t.actionsHeading} loading={t.loading} failed={t.loadFailed} panel={panel} onRetry={reload}>
      {(rows) => rows.length === 0 ? <p className="mjny__empty">{t.noActions}</p> : <table>
        <thead><tr><th>{t.buttonCode}</th><th>{t.times}</th><th>{t.average}</th><th>{t.slowest}</th><th>{t.errors}</th></tr></thead>
        <tbody>{rows.map((row) => <tr key={row.key}>
          <td>{row.key}</td><td>{row.count}</td><td>{formatMilliseconds(row.averageMilliseconds)}</td>
          <td>{formatMilliseconds(row.maxMilliseconds)}</td><td>{row.errorCount}</td>
        </tr>)}</tbody>
      </table>}
    </PanelView>
  </>;
}

function RequestsView({ api }: { api: UserJourneysClient }) {
  const [sort, setSort] = useState("slowest");
  const [selected, setSelected] = useState<string>();
  const [panel, reload] = usePanel(useCallback(() => api.requests(sort, 0), [api, sort]), t.loadFailed);
  return <>
    <div className="mjny__filters">
      <SelectField id="uj-request-sort" label={t.sortBy} value={sort} onChange={(event) => { setSort(event.target.value); setSelected(undefined); }}>
        {["slowest", "failing", "recent"].map((value) => <option key={value} value={value}>{sortLabel(value)}</option>)}
      </SelectField>
    </div>
    <p className="mjny__help">{t.requestsHelp}</p>
    <PanelView title={t.requestsHeading} loading={t.loading} failed={t.loadFailed} panel={panel} onRetry={reload}>
      {(rows) => rows.length === 0 ? <p className="mjny__empty">{t.noRequests}</p> : <table>
        <thead><tr><th>{t.address}</th><th>{t.when}</th><th>{t.duration}</th><th>{t.steps}</th><th /></tr></thead>
        <tbody>{rows.map((row) => <tr key={row.traceId}>
          <td>{row.method} {row.route}</td><td>{formatWhen(row.startedAt)}</td>
          <td>{formatMilliseconds(row.durationMilliseconds)}</td><td>{row.spanCount}</td>
          <td><Button data-telemetry-id="b.management-user-journeys.userjourneyssection.1" variant="quiet" onClick={() => setSelected(row.traceId)}>{t.timeline}</Button></td>
        </tr>)}</tbody>
      </table>}
    </PanelView>
    {selected && <Timeline api={api} traceId={selected} />}
  </>;
}

function StaffView({ api }: { api: UserJourneysClient }) {
  const [panel, reload] = usePanel(useCallback(() => api.staffMovements(), [api]), t.loadFailed);
  return <PanelView title={t.staffHeading} loading={t.loading} failed={t.loadFailed} panel={panel} onRetry={reload}>
    {(rows) => rows.length === 0 ? <p className="mjny__empty">{t.noStaff}</p> : <table>
      <thead><tr><th>{t.when}</th><th>{t.person}</th><th>{t.what}</th></tr></thead>
      <tbody>{rows.map((row, index) => <tr key={`${row.occurredAt}-${index}`}>
        <td>{formatWhen(row.occurredAt)}</td><td>{row.actorName ?? t.unknownPerson}</td><td>{row.eventName}</td>
      </tr>)}</tbody>
    </table>}
  </PanelView>;
}

export function UserJourneysSection({ capabilities, client }: ManagementSectionProps & { client?: UserJourneysClient }) {
  const api = useMemo(() => client ?? createUserJourneysClient(), [client]);
  const [view, setView] = useState<View>("requests");
  const views: readonly View[] = capabilities.has("observability.manage") ? ["requests", "actions", "staff"] : ["requests", "actions"];
  const names: Record<View, string> = { requests: t.tabRequests, actions: t.tabActions, staff: t.tabStaff };
  return <div className="mjny">
    <p className="mjny__help">{t.glossary}</p>
    <div className="mjny__actions" role="tablist">
      {views.map((name) => <Button key={name} data-telemetry-id="b.management-user-journeys.userjourneyssection.2" variant={name === view ? "primary" : "secondary"}
        role="tab" aria-selected={name === view} onClick={() => setView(name)}>{names[name]}</Button>)}
    </div>
    {view === "requests" && <RequestsView api={api} />}
    {view === "actions" && <ActionsView api={api} />}
    {view === "staff" && <StaffView api={api} />}
  </div>;
}
