import { useMemo, useState, type FormEvent } from "react";
import { ApiError } from "../../api";
import {
  Button,
  ModalDialog,
  SelectField,
  StateMessage,
  TextField,
  ValidationSummary,
} from "../../design-system";
import { commonActions, stateText } from "../../strings";
import {
  actionNeedsReason,
  isClientExecutableAction,
  tableActionLabels,
  tableStatusLabels,
  type CreateTableInput,
  type CreateZoneInput,
  type UpdateTableInput,
  type UpdateZoneInput,
  type TableAction,
  type TableRecord,
  type TableWorkspaceProps,
  type TableView,
} from "./models";
import { FloorPlanWorkspace } from "./FloorPlanWorkspace";
import { TableManagementApiError } from "./tableApi";
import "./tables.css";

type Feedback = { tone: "success" | "error" | "conflict"; message: string } | null;

const statusOptions = ["all", "Available", "Occupied", "Reserved", "Cleaning", "OutOfService"] as const;

// Floor occupancy escalation windows: a table occupied longer than the warning
// threshold needs attention; past the critical threshold it is flagged red.
// DESIGN.md §1's heatmap rule is 0-20 / 20-45 / 45+ minutes — found by an
// independent audit (2026-09-06) to be hardcoded here as 75/120, matching
// neither boundary.
const OCCUPANCY_WARNING_MINUTES = 20;
const OCCUPANCY_CRITICAL_MINUTES = 45;

function occupiedMinutes(occupiedSince?: string | null): number | null {
  if (!occupiedSince) return null;
  const started = Date.parse(occupiedSince);
  if (!Number.isFinite(started)) return null;
  return Math.max(0, Math.floor((Date.now() - started) / 60_000));
}

function elapsedLabel(occupiedSince?: string | null) {
  const elapsedMinutes = occupiedMinutes(occupiedSince);
  if (elapsedMinutes === null) return "—";
  if (elapsedMinutes < 60) return `${elapsedMinutes} dk`;
  return `${Math.floor(elapsedMinutes / 60)} sa ${elapsedMinutes % 60} dk`;
}

function occupancyTone(occupiedSince?: string | null): "ok" | "warn" | "crit" {
  const elapsedMinutes = occupiedMinutes(occupiedSince);
  if (elapsedMinutes === null) return "ok";
  if (elapsedMinutes >= OCCUPANCY_CRITICAL_MINUTES) return "crit";
  if (elapsedMinutes >= OCCUPANCY_WARNING_MINUTES) return "warn";
  return "ok";
}

// V1-TBL-009 (Semih, 2026-09-12: a practical fix that adds zero clicks to an
// already busy staff's workload): a table that just went back to Available
// might still need wiping down — nobody is required to mark that, so the
// only signal available is "how recently did it change status at all". A
// passive visual hint, not a blocking state: the table is fully bookable the
// whole time this shows, it just fades on its own once the window passes.
// 10 minutes is a visual default (same kind of judgement call as
// OCCUPANCY_WARNING_MINUTES above), not a business policy — long enough for
// a busser to notice and swing by, short enough that it never lingers on a
// table nobody actually needs to check.
const RECENTLY_VACATED_MINUTES = 10;

function isRecentlyVacated(table: TableRecord): boolean {
  if (table.status !== "Available") return false;
  const minutesSinceChange = occupiedMinutes(table.statusChangedAt);
  return minutesSinceChange !== null && minutesSinceChange < RECENTLY_VACATED_MINUTES;
}

// A prepared reason per action, so the common case needs no typing; "other" opens a free text field.
const OTHER_REASON = "__other";
const reasonPresets: Partial<Record<TableAction, readonly string[]>> = {
  Reserve: ["Telefonla rezervasyon", "Yüz yüze rezervasyon"],
  CancelReservation: ["Misafir vazgeçti", "Misafir gelmedi"],
  Transfer: ["Misafir masa değiştirdi", "Masa sorunlu"],
  Merge: ["Kalabalık grup", "Misafir isteği"],
  Unmerge: ["Grup ayrıldı"],
};

function errorMessage(reason: unknown) {
  // V1-RMD-114: only ApiError's message is guaranteed to come from the
  // backend's own Turkish-mapped exception filter — a raw network failure
  // (fetch() itself throwing) is a native, English, browser message
  // (independent audit, 2026-09-06).
  // Table actions go through tableApi.ts, which throws its OWN error class
  // (TableManagementApiError), not the shared ApiError - checking only
  // ApiError made every table-action failure (conflict, forbidden, an
  // unsettled payment) fall through to this generic text, hiding the
  // backend's own Turkish message from the manager.
  return reason instanceof ApiError || reason instanceof TableManagementApiError
    ? reason.message
    : "İşlem tamamlanamadı. Tekrar deneyin.";
}

// Only a genuine optimistic-concurrency failure means "the table changed
// under you, reloaded, nothing was repeated". Every other 409 (an unsettled
// payment, a state rule) is a different situation with its own message.
function isConcurrencyConflict(reason: unknown) {
  return reason instanceof TableManagementApiError && reason.code === "CONCURRENT_MODIFICATION";
}

export function TableWorkspace({
  state,
  zones,
  tables,
  canManage,
  selectedTableId,
  onSelectTable,
  selectedZoneId,
  onSelectZone,
  onRefresh,
  onCreateZone,
  onCreateTable,
  onUpdateZone,
  onDeleteZone,
  onUpdateTable,
  onAction,
  floorPlan,
  floorPlanBusy,
  floorPlanError,
  onSaveFloorPlan,
  errorMessage: suppliedError,
  refreshError,
  lastUpdated,
}: TableWorkspaceProps) {
  const [view, setView] = useState<TableView>("map");
  const [internalZoneFilter, setInternalZoneFilter] = useState("all");
  const zoneFilter = selectedZoneId !== undefined ? selectedZoneId : internalZoneFilter;
  const setZoneFilter = (z: string) => {
    setInternalZoneFilter(z);
    onSelectZone?.(z);
  };
  const [statusFilter, setStatusFilter] = useState<(typeof statusOptions)[number]>("all");
  const [search, setSearch] = useState("");
  const [zoneDialogOpen, setZoneDialogOpen] = useState(false);
  const [tableDialogOpen, setTableDialogOpen] = useState(false);
  const [action, setAction] = useState<TableAction | null>(null);
  const [actionTableId, setActionTableId] = useState<string | null>(null);
  const [actionBusy, setActionBusy] = useState(false);
  const [feedback, setFeedback] = useState<Feedback>(null);
  const [formErrors, setFormErrors] = useState<string[]>([]);
  const [zoneDraft, setZoneDraft] = useState<CreateZoneInput>({ code: "", name: "", sortOrder: 0 });
  const [tableDraft, setTableDraft] = useState<CreateTableInput>({ tableNumber: "", zoneId: null, capacity: 2 });
  const [zonesDialogOpen, setZonesDialogOpen] = useState(false);
  const [editZoneId, setEditZoneId] = useState<string | null>(null);
  const [deleteZoneId, setDeleteZoneId] = useState<string | null>(null);
  const [zoneEdit, setZoneEdit] = useState<UpdateZoneInput>({ name: "", sortOrder: 0, active: true });
  const [tableEditOpen, setTableEditOpen] = useState(false);
  const [tableEdit, setTableEdit] = useState<UpdateTableInput>({ tableNumber: "", zoneId: null, capacity: 2, active: true });
  const [reason, setReason] = useState("");
  const [reasonChoice, setReasonChoice] = useState(OTHER_REASON);
  const [partySize, setPartySize] = useState(2);
  const [targetTableId, setTargetTableId] = useState("");
  const [participantTableIds, setParticipantTableIds] = useState<string[]>([]);

  const filteredTables = useMemo(() => {
    const normalized = search.trim().toLocaleLowerCase("tr-TR");
    return tables.filter((table) =>
      (zoneFilter === "all" || table.zoneId === zoneFilter)
      && (statusFilter === "all" || table.status === statusFilter)
      && (!normalized || table.tableNumber.toLocaleLowerCase("tr-TR").includes(normalized)),
    );
  }, [search, statusFilter, tables, zoneFilter]);

  const selectedTable = tables.find((table) => table.tableId === selectedTableId) ?? filteredTables[0] ?? null;
  const selectedZone = selectedTable ? zones.find((zone) => zone.zoneId === selectedTable.zoneId) : undefined;
  const actionTable = tables.find((table) => table.tableId === actionTableId) ?? selectedTable;
  // The server only accepts a free table as the transfer target and refuses reserved, cleaning
  // and out-of-service tables in a merge, so the lists offer only what can succeed.
  const transferTargets = tables.filter((table) => table.tableId !== actionTable?.tableId && table.active && table.status === "Available");
  const mergeTargets = tables.filter((table) => table.tableId !== actionTable?.tableId && table.active && (table.status === "Available" || table.status === "Occupied"));
  const canCreate = canManage && Boolean(onCreateZone && onCreateTable);

  const openAction = (nextAction: TableAction, targetTable = selectedTable) => {
    if (!targetTable) return;
    setAction(nextAction);
    setActionTableId(targetTable.tableId);
    setReason("");
    setReasonChoice(reasonPresets[nextAction]?.[0] ?? OTHER_REASON);
    setPartySize(targetTable.capacity || 2);
    setTargetTableId("");
    setParticipantTableIds([]);
    setFormErrors([]);
    setFeedback(null);
  };

  const submitZone = async (event: FormEvent) => {
    event.preventDefault();
    const errors = [
      zoneDraft.code.trim() ? "" : "Kod gerekli.",
      zoneDraft.name.trim() ? "" : "Bölge adı gerekli.",
    ].filter(Boolean);
    if (errors.length || !onCreateZone) {
      setFormErrors(errors.length ? errors : ["Bu işlem için yetkiniz yok."]);
      return;
    }
    try {
      await onCreateZone({ ...zoneDraft, code: zoneDraft.code.trim().toUpperCase(), name: zoneDraft.name.trim() });
      setZoneDialogOpen(false);
      setZoneDraft({ code: "", name: "", sortOrder: 0 });
      setFeedback({ tone: "success", message: "Bölge oluşturuldu." });
    } catch (reason) {
      setFormErrors([errorMessage(reason)]);
    }
  };

  const submitTable = async (event: FormEvent) => {
    event.preventDefault();
    const errors = [
      tableDraft.tableNumber.trim() ? "" : "Masa numarası gerekli.",
      tableDraft.capacity > 0 ? "" : "Kapasite 1 veya daha büyük olmalı.",
    ].filter(Boolean);
    if (errors.length || !onCreateTable) {
      setFormErrors(errors.length ? errors : ["Bu işlem için yetkiniz yok."]);
      return;
    }
    try {
      await onCreateTable({ ...tableDraft, tableNumber: tableDraft.tableNumber.trim() });
      setTableDialogOpen(false);
      setTableDraft({ tableNumber: "", zoneId: zoneFilter === "all" ? null : zoneFilter, capacity: 2 });
      setFeedback({ tone: "success", message: "Masa oluşturuldu." });
    } catch (reason) {
      setFormErrors([errorMessage(reason)]);
    }
  };

  const failEdit = (reasonValue: unknown) => {
    const conflict = isConcurrencyConflict(reasonValue);
    setFormErrors([conflict ? "Kayıt başka biri tarafından değiştirildi. Listeyi yenileyip tekrar deneyin." : errorMessage(reasonValue)]);
  };

  const submitZoneEdit = async (event: FormEvent) => {
    event.preventDefault();
    const zone = zones.find((candidate) => candidate.zoneId === editZoneId);
    const errors = [zoneEdit.name.trim() ? "" : "Bölge adı gerekli."].filter(Boolean);
    if (!zone || !onUpdateZone || errors.length) {
      setFormErrors(errors);
      return;
    }
    try {
      await onUpdateZone(zone, { ...zoneEdit, name: zoneEdit.name.trim() });
      setEditZoneId(null);
      setFormErrors([]);
      setFeedback({ tone: "success", message: "Bölge güncellendi." });
    } catch (reasonValue) {
      failEdit(reasonValue);
    }
  };

  const confirmZoneDelete = async () => {
    const zone = zones.find((candidate) => candidate.zoneId === deleteZoneId);
    if (!zone || !onDeleteZone) return;
    try {
      await onDeleteZone(zone);
      setDeleteZoneId(null);
      setFormErrors([]);
      setFeedback({ tone: "success", message: "Bölge silindi." });
    } catch (reasonValue) {
      failEdit(reasonValue);
    }
  };

  const openTableEdit = () => {
    if (!selectedTable) return;
    setTableEdit({ tableNumber: selectedTable.tableNumber, zoneId: selectedTable.zoneId, capacity: selectedTable.capacity, active: selectedTable.active });
    setFormErrors([]);
    setFeedback(null);
    setTableEditOpen(true);
  };

  const submitTableEdit = async (event: FormEvent) => {
    event.preventDefault();
    const errors = [
      tableEdit.tableNumber.trim() ? "" : "Masa numarası gerekli.",
      tableEdit.capacity > 0 ? "" : "Kapasite 1 veya daha büyük olmalı.",
    ].filter(Boolean);
    if (!selectedTable || !onUpdateTable || errors.length) {
      setFormErrors(errors);
      return;
    }
    try {
      await onUpdateTable(selectedTable, { ...tableEdit, tableNumber: tableEdit.tableNumber.trim() });
      setTableEditOpen(false);
      setFormErrors([]);
      setFeedback({ tone: "success", message: "Masa güncellendi." });
    } catch (reasonValue) {
      failEdit(reasonValue);
    }
  };

  const submitAction = async (event: FormEvent) => {
    event.preventDefault();
    if (!actionTable || !action || !onAction) return;
    const effectiveReason = reasonChoice === OTHER_REASON ? reason.trim() : reasonChoice;
    const errors = [
      actionNeedsReason(action) && !effectiveReason ? "Bu işlem için açıklama gerekli." : "",
      action === "Transfer" && !targetTableId ? "Hedef masa seçin." : "",
      action === "Merge" && participantTableIds.length === 0 ? "En az bir katılımcı masa seçin." : "",
    ].filter(Boolean);
    if (errors.length) {
      setFormErrors(errors);
      return;
    }
    setActionBusy(true);
    setFormErrors([]);
    try {
      const floorTable = floorPlan?.tables.find((table) => table.tableId === actionTable.tableId);
      const unmergeParticipants = action === "Unmerge" && floorTable?.mergeGroupId
        ? floorPlan?.tables
          .filter((table) => table.mergeGroupId === floorTable.mergeGroupId && table.tableId !== actionTable.tableId)
          .map((table) => ({ tableId: table.tableId, rowVersion: table.tableRowVersion }))
        : undefined;
      await onAction({
        table: actionTable,
        action,
        reason: effectiveReason || undefined,
        partySize: action === "Reserve" ? partySize : undefined,
        targetTableId: targetTableId || undefined,
        targetTableVersion: targetTableId ? tables.find((table) => table.tableId === targetTableId)?.rowVersion : undefined,
        participantTableIds: participantTableIds.length ? participantTableIds : undefined,
        participantTableVersions: unmergeParticipants ?? (participantTableIds.length
          ? participantTableIds.map((tableId) => ({ tableId, rowVersion: tables.find((table) => table.tableId === tableId)?.rowVersion ?? 0 }))
          : undefined),
        mergeGroupId: action === "Unmerge" ? floorTable?.mergeGroupId ?? undefined : undefined,
      });
      setAction(null);
      setActionTableId(null);
      setFeedback({ tone: "success", message: `${tableActionLabels[action]} tamamlandı.` });
    } catch (reasonValue) {
      // The failure belongs to the dialog the person is looking at; the dialog stays open and,
      // after a concurrency conflict, already shows the refreshed table.
      setFormErrors([isConcurrencyConflict(reasonValue)
        ? "Masa başka biri tarafından değiştirildi. Güncel durum yüklendi; işlem tekrarlanmadı, bilgileri kontrol edip yeniden onaylayın."
        : errorMessage(reasonValue)]);
    } finally {
      setActionBusy(false);
    }
  };

  if (state === "loading") {
    return <div className="table-workspace table-workspace--state" aria-busy="true"><StateMessage tone="info" title="Masa düzeni yükleniyor"><p>Bölge ve masa durumu güvenli biçimde alınıyor…</p></StateMessage></div>;
  }
  if (state === "unauthorized") {
    return <div className="table-workspace table-workspace--state"><StateMessage tone="unauthorized" title={stateText.unauthorizedTitle}><p>Masa çalışma alanını görmek için yeniden giriş yapın.</p></StateMessage></div>;
  }
  if (state === "offline") {
    return <div className="table-workspace table-workspace--state"><StateMessage tone="offline" title={stateText.offlineTitle}><p>Sunucuya ulaşılamıyor; eski masa durumu işlem için kullanılmıyor.</p><Button data-telemetry-id="b.tables.tableworkspace.1" onClick={onRefresh}>{commonActions.retry}</Button></StateMessage></div>;
  }
  if (state === "error") {
    return <div className="table-workspace table-workspace--state"><StateMessage tone="error" title="Masa düzeni alınamadı"><p>{suppliedError ?? stateText.unexpectedError}</p><Button data-telemetry-id="b.tables.tableworkspace.2" onClick={onRefresh}>{commonActions.reload}</Button></StateMessage></div>;
  }
  if (state === "stale") {
    return <div className="table-workspace table-workspace--state"><StateMessage tone="stale" title="Masa verisi güncel değil"><p>İşlem yapmadan önce güncel durumu alın.</p><Button data-telemetry-id="b.tables.tableworkspace.3" onClick={onRefresh}>Güncelle</Button></StateMessage></div>;
  }

  return (
    <section className="table-workspace" aria-label="Masa yönetimi">
      <header className="table-workspace__toolbar">
        <div className="table-workspace__heading">
          <span className="table-workspace__kicker">OPERASYON / MASALAR</span>
          <h2>Masa düzeni</h2>
          <p>{lastUpdated ? `Son güncelleme ${lastUpdated}` : "Canlı masa ve sipariş bağlamı"}</p>
          {refreshError && <p className="table-workspace__refresh-error" role="alert">Masa durumu güncellenemedi; son bilinen durum gösteriliyor. {refreshError}</p>}
        </div>
        <div className="table-workspace__toolbar-actions">
          {canCreate && <><Button data-telemetry-id="b.tables.tableworkspace.4" variant="secondary" onClick={() => { setFormErrors([]); setZoneDialogOpen(true); }}>+ Bölge ekle</Button><Button data-telemetry-id="b.tables.tableworkspace.5" onClick={() => { setFormErrors([]); setTableDialogOpen(true); }}>+ Masa ekle</Button></>}
          {canManage && onUpdateTable && selectedTable && <Button data-telemetry-id="b.tables.tableworkspace.6" variant="secondary" onClick={openTableEdit}>Masayı düzenle</Button>}
          {canManage && onUpdateZone && <Button data-telemetry-id="b.tables.tableworkspace.7" variant="secondary" onClick={() => { setFormErrors([]); setEditZoneId(null); setDeleteZoneId(null); setZonesDialogOpen(true); }}>Bölgeleri yönet</Button>}
          <Button data-telemetry-id="b.tables.tableworkspace.8" variant="secondary" onClick={() => void onRefresh()}>{commonActions.refresh}</Button>
        </div>
      </header>

      {feedback && <div className={`table-workspace__feedback table-workspace__feedback--${feedback.tone}`} role={feedback.tone === "error" || feedback.tone === "conflict" ? "alert" : "status"} aria-live="polite"><span>{feedback.message}</span><button data-telemetry-id="b.tables.tableworkspace.9" type="button" aria-label={stateText.dismissMessage} onClick={() => setFeedback(null)}>×</button></div>}

      <div className="table-workspace__filters" role="group" aria-label="Masa filtreleri">
        <SelectField label="Bölge" value={zoneFilter} onChange={(event) => setZoneFilter(event.target.value)}>
          <option value="all">Tüm bölgeler</option>
          {zones.filter((zone) => zone.active).map((zone) => <option key={zone.zoneId} value={zone.zoneId}>{zone.name}</option>)}
        </SelectField>
        <label className="table-workspace__search">Masa ara<input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Örn. S-09" aria-label="Masa ara" /></label>
        <div className="table-workspace__status-filter" role="group" aria-label="Masa durumuna göre filtrele">
          {statusOptions.map((status) => <button data-telemetry-id="b.tables.tableworkspace.10" key={status} type="button" className={statusFilter === status ? "is-active" : ""} aria-pressed={statusFilter === status} onClick={() => setStatusFilter(status)}>{status === "all" ? "Tümü" : tableStatusLabels[status]}</button>)}
        </div>
        <div className="table-workspace__view-toggle" role="group" aria-label="Görünüm">
          <button data-telemetry-id="b.tables.tableworkspace.11" type="button" className={view === "map" ? "is-active" : ""} aria-pressed={view === "map"} onClick={() => setView("map")}>Salon planı</button>
          <button data-telemetry-id="b.tables.tableworkspace.12" type="button" className={view === "list" ? "is-active" : ""} aria-pressed={view === "list"} onClick={() => setView("list")}>Liste</button>
        </div>
      </div>

      <div className="table-workspace__stats" role="group" aria-label="Masa özeti">
        <Stat label="Toplam masa" value={tables.length} />
        <Stat label="Müsait" value={tables.filter((table) => table.status === "Available").length} tone="success" />
        <Stat label="Dolu" value={tables.filter((table) => table.status === "Occupied").length} tone="accent" />
        <Stat label="Rezervasyon" value={tables.filter((table) => table.status === "Reserved").length} tone="warning" />
      </div>

      {state === "empty" || filteredTables.length === 0 ? (
        <div className="table-workspace__empty"><StateMessage tone="info" title={state === "empty" ? "Henüz masa yok" : "Filtreyle eşleşen masa yok"}><p>{state === "empty" ? "Yönetici olarak ilk bölgeyi ve masayı ekleyerek başlayın." : "Filtreyi değiştirin veya aramayı temizleyin."}</p>{state === "empty" && canCreate && <Button data-telemetry-id="b.tables.tableworkspace.13" onClick={() => setTableDialogOpen(true)}>İlk masayı ekle</Button>}</StateMessage></div>
      ) : (
        <div className={`table-workspace__content table-workspace__content--${view}`}>
          {view === "map" && floorPlan ? <div className="table-workspace__floor-plan">
            <FloorPlanWorkspace
              plan={floorPlan}
              tables={tables}
              selectedTableId={selectedTable?.tableId}
              canManage={canManage}
              busy={floorPlanBusy}
              error={floorPlanError}
              lastUpdated={lastUpdated}
              onSelectTable={onSelectTable}
              onOpenAction={(nextAction, table) => openAction(nextAction, table)}
              onSave={onSaveFloorPlan}
            />
          </div> : <>
            <div className="table-workspace__table-area">
              <div className="table-workspace__area-heading"><div><strong>{selectedZone?.name ?? "Tüm bölgeler"}</strong><span>{filteredTables.length} masa</span></div><span className="table-workspace__legend"><i className="legend-dot legend-dot--available" /> Müsait <i className="legend-dot legend-dot--occupied" /> Dolu <i className="legend-dot legend-dot--reserved" /> Rezerve</span></div>
              <div className="table-grid">
                {filteredTables.map((table) => <TableCard key={table.tableId} table={table} selected={table.tableId === selectedTable?.tableId} onSelect={() => onSelectTable(table.tableId)} onAction={(nextAction) => openAction(nextAction, table)} />)}
              </div>
            </div>
            {selectedTable && <TableDetails table={selectedTable} zoneName={selectedZone?.name} onAction={openAction} />}
          </>}
        </div>
      )}

      <ModalDialog open={zoneDialogOpen} title="Yeni bölge" onClose={() => setZoneDialogOpen(false)}>
        <form className="table-form" onSubmit={(event) => void submitZone(event)}>
          <ValidationSummary title="Bölge bilgilerini kontrol edin" errors={formErrors} />
          <TextField label="Kod" value={zoneDraft.code} onChange={(event) => setZoneDraft({ ...zoneDraft, code: event.target.value })} placeholder="SALON" autoComplete="off" />
          <TextField label="Bölge adı" value={zoneDraft.name} onChange={(event) => setZoneDraft({ ...zoneDraft, name: event.target.value })} placeholder="Salon" autoComplete="off" />
          <TextField label="Sıra" type="number" min={0} value={zoneDraft.sortOrder} onChange={(event) => setZoneDraft({ ...zoneDraft, sortOrder: Number(event.target.value) })} />
          <div className="table-form__actions"><Button data-telemetry-id="b.tables.tableworkspace.14" variant="secondary" onClick={() => setZoneDialogOpen(false)}>Vazgeç</Button><Button data-telemetry-id="b.tables.tableworkspace.15" type="submit">Bölge oluştur</Button></div>
        </form>
      </ModalDialog>

      <ModalDialog open={tableDialogOpen} title="Yeni masa" onClose={() => setTableDialogOpen(false)}>
        <form className="table-form" onSubmit={(event) => void submitTable(event)}>
          <ValidationSummary title="Masa bilgilerini kontrol edin" errors={formErrors} />
          <TextField label="Masa numarası" value={tableDraft.tableNumber} onChange={(event) => setTableDraft({ ...tableDraft, tableNumber: event.target.value })} placeholder="S-09" autoComplete="off" />
          <SelectField label="Bölge" value={tableDraft.zoneId ?? ""} onChange={(event) => setTableDraft({ ...tableDraft, zoneId: event.target.value || null })}><option value="">Bölge seçin</option>{zones.filter((zone) => zone.active).map((zone) => <option key={zone.zoneId} value={zone.zoneId}>{zone.name}</option>)}</SelectField>
          <TextField label="Kapasite" type="number" min={1} max={100} value={tableDraft.capacity} onChange={(event) => setTableDraft({ ...tableDraft, capacity: Number(event.target.value) })} />
          <div className="table-form__actions"><Button data-telemetry-id="b.tables.tableworkspace.16" variant="secondary" onClick={() => setTableDialogOpen(false)}>Vazgeç</Button><Button data-telemetry-id="b.tables.tableworkspace.17" type="submit">Masa oluştur</Button></div>
        </form>
      </ModalDialog>

      <ModalDialog open={zonesDialogOpen} title="Bölgeleri yönet" onClose={() => { setZonesDialogOpen(false); setEditZoneId(null); setDeleteZoneId(null); }}>
        <ValidationSummary title="İşlem tamamlanamadı" errors={formErrors} />
        {zones.length === 0 ? <p>Henüz bölge yok.</p> : <ul className="table-form__checklist">{zones.map((zone) => <li key={zone.zoneId}>
          <strong>{zone.name}</strong> · {zone.code} · {zone.active ? "Etkin" : "Pasif"}
          <Button data-telemetry-id="b.tables.tableworkspace.18" variant="quiet" onClick={() => { setFormErrors([]); setDeleteZoneId(null); setEditZoneId(zone.zoneId); setZoneEdit({ name: zone.name, sortOrder: zone.sortOrder, active: zone.active }); }}>Düzenle</Button>
          <Button data-telemetry-id="b.tables.tableworkspace.19" variant="quiet" onClick={() => { setFormErrors([]); setEditZoneId(null); setDeleteZoneId(zone.zoneId); }}>Sil</Button>
        </li>)}</ul>}
        {deleteZoneId && <div className="table-form__context" role="alert">
          <p>Bu bölge silinecek. Bölgede masa varsa sunucu işlemi reddeder.</p>
          <Button data-telemetry-id="b.tables.tableworkspace.20" onClick={() => void confirmZoneDelete()}>Evet, bölgeyi sil</Button>
          <Button data-telemetry-id="b.tables.tableworkspace.21" variant="secondary" onClick={() => setDeleteZoneId(null)}>Vazgeç</Button>
        </div>}
        {editZoneId && <form className="table-form" aria-label="Bölgeyi düzenle" onSubmit={(event) => void submitZoneEdit(event)}>
          <TextField label="Bölge adı" value={zoneEdit.name} onChange={(event) => setZoneEdit({ ...zoneEdit, name: event.target.value })} autoComplete="off" />
          <TextField label="Sıra" type="number" min={0} value={zoneEdit.sortOrder} onChange={(event) => setZoneEdit({ ...zoneEdit, sortOrder: Number(event.target.value) })} />
          <label><input type="checkbox" checked={zoneEdit.active} onChange={(event) => setZoneEdit({ ...zoneEdit, active: event.target.checked })} /> Etkin</label>
          <div className="table-form__actions"><Button data-telemetry-id="b.tables.tableworkspace.22" variant="secondary" onClick={() => setEditZoneId(null)}>Vazgeç</Button><Button data-telemetry-id="b.tables.tableworkspace.23" type="submit">Bölgeyi kaydet</Button></div>
        </form>}
      </ModalDialog>

      <ModalDialog open={tableEditOpen} title="Masayı düzenle" onClose={() => setTableEditOpen(false)}>
        <form className="table-form" aria-label="Masayı düzenle" onSubmit={(event) => void submitTableEdit(event)}>
          <ValidationSummary title="Masa bilgilerini kontrol edin" errors={formErrors} />
          <TextField label="Masa numarası" value={tableEdit.tableNumber} onChange={(event) => setTableEdit({ ...tableEdit, tableNumber: event.target.value })} autoComplete="off" />
          <SelectField label="Bölge" value={tableEdit.zoneId ?? ""} onChange={(event) => setTableEdit({ ...tableEdit, zoneId: event.target.value || null })}><option value="">Bölge seçin</option>{zones.map((zone) => <option key={zone.zoneId} value={zone.zoneId}>{zone.name}</option>)}</SelectField>
          <TextField label="Kapasite" type="number" min={1} max={100} value={tableEdit.capacity} onChange={(event) => setTableEdit({ ...tableEdit, capacity: Number(event.target.value) })} />
          <label><input type="checkbox" checked={tableEdit.active} onChange={(event) => setTableEdit({ ...tableEdit, active: event.target.checked })} /> Etkin</label>
          <div className="table-form__actions"><Button data-telemetry-id="b.tables.tableworkspace.24" variant="secondary" onClick={() => setTableEditOpen(false)}>Vazgeç</Button><Button data-telemetry-id="b.tables.tableworkspace.25" type="submit">Masayı kaydet</Button></div>
        </form>
      </ModalDialog>

      <ModalDialog open={action !== null} title={action ? tableActionLabels[action] ?? "Masa işlemi" : "Masa işlemi"} onClose={() => { if (!actionBusy) { setAction(null); setActionTableId(null); } }}>
        <form className="table-form" onSubmit={(event) => void submitAction(event)}>
          <ValidationSummary title="İşlem bilgilerini kontrol edin" errors={formErrors} />
          {actionTable && <p className="table-form__context"><strong>{actionTable.tableNumber}</strong> · {tableStatusLabels[actionTable.status]}</p>}
          {action === "Transfer" && (transferTargets.length === 0
            ? <p className="table-form__context">Şu anda boş masa yok; masa değiştirilemez.</p>
            : <SelectField label="Hedef masa" value={targetTableId} onChange={(event) => setTargetTableId(event.target.value)}><option value="">Hedef seçin</option>{transferTargets.map((table) => <option key={table.tableId} value={table.tableId}>{table.tableNumber}</option>)}</SelectField>)}
          {action === "Reserve" && <TextField label="Kişi sayısı" type="number" min={1} max={50} value={partySize} onChange={(event) => setPartySize(Math.max(1, Number(event.target.value)))} />}
          {action === "Merge" && <fieldset className="table-form__checklist"><legend>Birleştirilecek masalar</legend>{mergeTargets.map((table) => <label key={table.tableId}><input type="checkbox" checked={participantTableIds.includes(table.tableId)} onChange={(event) => setParticipantTableIds(event.target.checked ? [...participantTableIds, table.tableId] : participantTableIds.filter((id) => id !== table.tableId))} /> <span>{table.tableNumber} · {tableStatusLabels[table.status]}</span></label>)}</fieldset>}
          {action === "Unmerge" && <p className="table-form__context">Birleşimdeki tüm katılımcı masalar güncel satır sürümleriyle ayrılacaktır.</p>}
          {(action && actionNeedsReason(action)) && <>
            <SelectField label="Gerekçe" value={reasonChoice} onChange={(event) => setReasonChoice(event.target.value)}>
              {(reasonPresets[action] ?? []).map((preset) => <option key={preset} value={preset}>{preset}</option>)}
              <option value={OTHER_REASON}>Diğer (yazın)</option>
            </SelectField>
            {reasonChoice === OTHER_REASON && <TextField label="Açıklama" value={reason} onChange={(event) => setReason(event.target.value)} placeholder="İşlem gerekçesi" autoComplete="off" />}
          </>}
          <div className="table-form__actions"><Button data-telemetry-id="b.tables.tableworkspace.26" variant="secondary" disabled={actionBusy} onClick={() => { setAction(null); setActionTableId(null); }}>Vazgeç</Button><Button data-telemetry-id="b.tables.tableworkspace.27" type="submit" disabled={actionBusy}>{actionBusy ? "İşleniyor…" : "Onayla"}</Button></div>
        </form>
      </ModalDialog>
    </section>
  );
}

function Stat({ label, value, tone = "neutral" }: { label: string; value: number; tone?: string }) {
  return <div className={`table-stat table-stat--${tone}`}><span>{label}</span><strong>{value}</strong></div>;
}

function TableCard({ table, selected, onSelect, onAction }: { table: TableRecord; selected: boolean; onSelect: () => void; onAction: (action: TableAction) => void }) {
  const command = table.allowedCommands.find((value): value is TableAction => value in tableActionLabels && isClientExecutableAction(value as TableAction, table));
  return <article className={`table-card table-card--${table.status.toLowerCase()} ${selected ? "is-selected" : ""}`}>
    <button data-telemetry-id="b.tables.tableworkspace.28" type="button" className="table-card__select" aria-label={`${table.tableNumber} masasını seç, ${tableStatusLabels[table.status]}`} aria-pressed={selected} onClick={onSelect}>
      <span className="table-card__top"><strong>{table.tableNumber}</strong><span className="table-card__status-group"><span className="table-status">{tableStatusLabels[table.status]}</span>{isRecentlyVacated(table) && <span className="table-card__recently-vacated" title="Masa az önce boşaldı, kontrol edilmesi faydalı olabilir">Az önce boşaldı</span>}</span></span>
      <span className="table-card__capacity">◉ {table.capacity} kişilik</span>
      <span className="table-card__context">{table.currentOrderId ? `Sipariş #${table.currentOrderId.slice(0, 8)}` : table.currentBillId ? `Hesap #${table.currentBillId.slice(0, 8)}` : "Sipariş yok"}</span>
    </button>
    <footer className="table-card__footer"><span className={table.status === "Occupied" ? `table-card__elapsed table-card__elapsed--${occupancyTone(table.occupiedSince)}` : undefined}>{table.status === "Occupied" ? elapsedLabel(table.occupiedSince) : ""}</span>{command && <button data-telemetry-id="b.tables.tableworkspace.29" type="button" className="table-card__quick-action" aria-label={`${table.tableNumber}: ${tableActionLabels[command]}`} onClick={() => onAction(command)}>{tableActionLabels[command]}</button>}</footer>
  </article>;
}

function TableDetails({ table, zoneName, onAction }: { table: TableRecord; zoneName?: string; onAction: (action: TableAction) => void }) {
  const commands = table.allowedCommands.filter((value): value is TableAction => value in tableActionLabels);
  return <section className="table-details" aria-label={`${table.tableNumber} masa bağlamı`}>
    <div className="table-details__header"><div><span className="table-workspace__kicker">SEÇİLİ MASA</span><h3>{table.tableNumber}</h3><span>{zoneName ?? "Bölge atanmamış"}</span></div><div className="table-details__status-group"><span className={`table-details__status table-details__status--${table.status.toLowerCase()}`}>{tableStatusLabels[table.status]}</span>{isRecentlyVacated(table) && <span className="table-card__recently-vacated" title="Masa az önce boşaldı, kontrol edilmesi faydalı olabilir">Az önce boşaldı</span>}</div></div>
    <div className="table-details__facts"><div><span>Kapasite</span><strong>{table.capacity} kişi</strong></div><div><span>Geçen süre</span><strong className={table.status === "Occupied" ? `table-card__elapsed table-card__elapsed--${occupancyTone(table.occupiedSince)}` : undefined}>{table.status === "Occupied" ? elapsedLabel(table.occupiedSince) : "—"}</strong></div></div>
    <div className="table-details__pointer"><span className="table-details__label">AKTİF BAĞLAM</span>{table.currentOrderId ? <p><strong>Açık sipariş var</strong></p> : <p className="is-muted">Bu masada aktif sipariş yok.</p>}{table.currentBillId && <p><strong>Açık hesap var</strong></p>}</div>
    {commands.length > 0 ? <div className="table-details__actions"><span className="table-details__label">İŞLEMLER</span>{commands.map((command) => <Button data-telemetry-id="b.tables.tableworkspace.30" key={command} variant={command === "SetOutOfService" ? "secondary" : "primary"} disabled={!isClientExecutableAction(command, table)} onClick={() => onAction(command)}>{tableActionLabels[command]}</Button>)}{commands.some((command) => !isClientExecutableAction(command, table)) && <small className="table-details__disabled-reason">Soluk işlem kullanılamıyor: bu masa için aktif rezervasyon bulunamadı.</small>}</div> : <StateMessage tone="forbidden" title="İşlem kullanılamıyor"><p>Bu masa için sunucu tarafından izin verilen işlem yok.</p></StateMessage>}
  </section>;
}
