import { useEffect, useMemo, useRef, useState, type KeyboardEvent, type PointerEvent } from "react";
import { ApiError } from "../../api";
import { Button, SelectField, TextField } from "../../design-system";
import {
  tableStatusLabels,
  isClientExecutableAction,
  type FloorPlan,
  type FloorPlanSeat,
  type FloorPlanTable,
  type FloorTableShape,
  type SaveFloorPlanInput,
  type SaveFloorPlanResult,
  type TableAction,
  type TableRecord,
  type TableStatus,
  tableActionLabels,
} from "./models";
import "./floorPlan.css";

type WorkspaceMode = "operation" | "setup";
type DraftSeat = FloorPlanSeat;
type DraftTable = Omit<FloorPlanTable, "x" | "y" | "width" | "height" | "shape" | "rotationDegrees" | "seats"> & {
  x: number;
  y: number;
  width: number;
  height: number;
  shape: FloorTableShape;
  rotationDegrees: 0 | 90 | 180 | 270;
  seats: DraftSeat[];
};
type DraftPlan = Omit<FloorPlan, "tables"> & { tables: DraftTable[] };

interface FloorPlanWorkspaceProps {
  plan: FloorPlan;
  tables: readonly TableRecord[];
  selectedTableId?: string | null;
  canManage: boolean;
  busy?: boolean;
  error?: string;
  lastUpdated?: string;
  onSelectTable: (tableId: string) => void;
  onOpenAction?: (action: TableAction, table: TableRecord) => void;
  onSave?: (zoneId: string, input: SaveFloorPlanInput) => SaveFloorPlanResult | Promise<SaveFloorPlanResult>;
}

interface DragState {
  pointerId: number;
  tableId: string;
  startClientX: number;
  startClientY: number;
  startX: number;
  startY: number;
  scaleX: number;
  scaleY: number;
}

const rotations = [0, 90, 180, 270] as const;

function clonePlan(plan: FloorPlan): DraftPlan {
  return {
    ...plan,
    tables: plan.tables.map((table, index) => ({
      ...table,
      x: table.x ?? 48 + (index % 5) * 184,
      y: table.y ?? 48 + Math.floor(index / 5) * 144,
      width: table.width ?? 144,
      height: table.height ?? 88,
      shape: table.shape ?? "Rectangle",
      rotationDegrees: table.rotationDegrees ?? 0,
      seats: table.seats.map((seat) => ({ ...seat })),
    })),
  };
}

function overlap(first: DraftTable, second: DraftTable) {
  return first.x < second.x + second.width
    && first.x + first.width > second.x
    && first.y < second.y + second.height
    && first.y + first.height > second.y;
}

function moveTable(table: DraftTable, x: number, y: number): DraftTable {
  const deltaX = x - table.x;
  const deltaY = y - table.y;
  return {
    ...table,
    x,
    y,
    seats: table.seats.map((seat) => ({ ...seat, x: seat.x + deltaX, y: seat.y + deltaY })),
  };
}

function resizeTable(table: DraftTable, width: number, height: number): DraftTable {
  const scaleX = width / table.width;
  const scaleY = height / table.height;
  return {
    ...table,
    width,
    height,
    seats: table.seats.map((seat) => ({
      ...seat,
      x: Math.round(table.x + (seat.x - table.x) * scaleX),
      y: Math.round(table.y + (seat.y - table.y) * scaleY),
    })),
  };
}

export function validateFloorPlan(plan: DraftPlan) {
  const errors: string[] = [];
  const warnings: string[] = [];
  for (const table of plan.tables) {
    if (table.x < 0 || table.y < 0 || table.x + table.width > plan.canvasWidth || table.y + table.height > plan.canvasHeight)
      errors.push(`${table.tableNumber} salon sınırının dışında.`);
    if (table.width < 48 || table.height < 48 || table.width > 640 || table.height > 640)
      errors.push(`${table.tableNumber} boyutu 48–640 aralığında olmalı.`);
    if (table.shape === "Square" && table.width !== table.height)
      errors.push(`${table.tableNumber} kare biçiminde eşit kenar ister.`);
    if (new Set(table.seats.map((seat) => seat.number)).size !== table.seats.length)
      errors.push(`${table.tableNumber} sandalye numaraları benzersiz olmalı.`);
    if (table.seats.some((seat) => seat.x < 0 || seat.y < 0 || seat.x > plan.canvasWidth || seat.y > plan.canvasHeight))
      errors.push(`${table.tableNumber} sandalyeleri salon sınırları içinde olmalı.`);
    if (table.capacity !== table.seats.length)
      warnings.push(`${table.tableNumber}: kapasite ${table.capacity}, sandalye ${table.seats.length}.`);
  }
  for (let first = 0; first < plan.tables.length; first += 1) {
    for (let second = first + 1; second < plan.tables.length; second += 1) {
      const a = plan.tables[first];
      const b = plan.tables[second];
      const sameMerge = Boolean(a.mergeGroupId && a.mergeGroupId === b.mergeGroupId);
      if (!sameMerge && overlap(a, b)) errors.push(`${a.tableNumber} ile ${b.tableNumber} çakışıyor.`);
    }
  }
  return { errors, warnings };
}

function toInput(plan: DraftPlan): SaveFloorPlanInput {
  return {
    expectedRowVersion: plan.rowVersion,
    canvasWidth: plan.canvasWidth,
    canvasHeight: plan.canvasHeight,
    tables: plan.tables.map((table) => ({
      tableId: table.tableId,
      expectedTableRowVersion: table.tableRowVersion,
      expectedLayoutRowVersion: table.layoutRowVersion,
      x: table.x,
      y: table.y,
      width: table.width,
      height: table.height,
      shape: table.shape,
      rotationDegrees: table.rotationDegrees,
      seats: table.seats.map((seat) => ({
        seatId: seat.seatId,
        expectedRowVersion: seat.rowVersion,
        number: seat.number,
        label: seat.label,
        x: seat.x,
        y: seat.y,
      })),
    })),
  };
}

function contextLabel(table: FloorPlanTable) {
  if (table.currentBillId) return `Hesap ${table.currentBillId.slice(0, 8)}`;
  if (table.currentOrderId) return `Sipariş ${table.currentOrderId.slice(0, 8)}`;
  if (table.activeReservationId) return `Rezervasyon · ${table.reservationPartySize ?? "?"} kişi`;
  return "Açık işlem yok";
}

function elapsedLabel(occupiedSince?: string | null) {
  if (!occupiedSince) return "—";
  const elapsedMinutes = Math.max(0, Math.floor((Date.now() - Date.parse(occupiedSince)) / 60_000));
  if (!Number.isFinite(elapsedMinutes)) return "—";
  if (elapsedMinutes < 60) return `${elapsedMinutes} dk`;
  return `${Math.floor(elapsedMinutes / 60)} sa ${elapsedMinutes % 60} dk`;
}

// V1-TBL-009: same passive "just vacated" hint as TableWorkspace.tsx's own
// isRecentlyVacated — a local duplicate rather than a shared import,
// matching this file's existing elapsedLabel duplication above.
const RECENTLY_VACATED_MINUTES = 10;

function isRecentlyVacated(table: { status: TableStatus; statusChangedAt: string }): boolean {
  if (table.status !== "Available") return false;
  const started = Date.parse(table.statusChangedAt);
  if (!Number.isFinite(started)) return false;
  const minutesSinceChange = Math.max(0, Math.floor((Date.now() - started) / 60_000));
  return minutesSinceChange < RECENTLY_VACATED_MINUTES;
}

export function FloorPlanWorkspace({
  plan,
  tables,
  selectedTableId,
  canManage,
  busy = false,
  error,
  lastUpdated,
  onSelectTable,
  onOpenAction,
  onSave,
}: FloorPlanWorkspaceProps) {
  const [mode, setMode] = useState<WorkspaceMode>("operation");
  const [draft, setDraft] = useState(() => clonePlan(plan));
  const [saveState, setSaveState] = useState<"idle" | "saving" | "success" | "conflict" | "error">("idle");
  const [saveMessage, setSaveMessage] = useState<string>();
  const drag = useRef<DragState | null>(null);

  useEffect(() => {
    if (mode === "operation") setDraft(clonePlan(plan));
  }, [mode, plan]);

  const selected = draft.tables.find((table) => table.tableId === selectedTableId) ?? draft.tables[0] ?? null;
  const validation = useMemo(() => validateFloorPlan(draft), [draft]);
  const tableRecords = useMemo(() => new Map(tables.map((table) => [table.tableId, table])), [tables]);

  const updateTable = (tableId: string, mutate: (table: DraftTable) => DraftTable) => {
    setDraft((current) => ({
      ...current,
      tables: current.tables.map((table) => table.tableId === tableId ? mutate(table) : table),
    }));
    setSaveState("idle");
  };

  const beginSetup = () => {
    setDraft(clonePlan(plan));
    setMode("setup");
    setSaveState("idle");
    setSaveMessage(undefined);
  };

  const cancelSetup = () => {
    setDraft(clonePlan(plan));
    setMode("operation");
    setSaveState("idle");
    setSaveMessage(undefined);
  };

  const save = async () => {
    if (!onSave || validation.errors.length) return;
    setSaveState("saving");
    setSaveMessage(undefined);
    try {
      const result = await onSave(plan.zoneId, toInput(draft));
      setDraft(clonePlan(result.floorPlan));
      setSaveState("success");
      setSaveMessage(result.warnings.length
        ? `Plan kaydedildi. ${result.warnings.map((warning) => warning.message).join(" ")}`
        : "Salon planı atomik olarak kaydedildi.");
      setMode("operation");
    } catch (reason) {
      const message = reason instanceof ApiError ? reason.message : "Salon planı kaydedilemedi.";
      const conflict = /409|conflict|concurrent|version/i.test(message);
      setSaveState(conflict ? "conflict" : "error");
      setSaveMessage(conflict
        ? "Plan sunucuda değişti. Taslağınız korundu; güncel sürümü açıp değişiklikleri karşılaştırın."
        : message);
    }
  };

  const onTableKeyDown = (event: KeyboardEvent<HTMLButtonElement>, table: DraftTable) => {
    if (mode !== "setup") return;
    if (event.key.toLowerCase() === "r") {
      event.preventDefault();
      updateTable(table.tableId, (current) => ({
        ...current,
        rotationDegrees: rotations[(rotations.indexOf(current.rotationDegrees) + 1) % rotations.length],
      }));
      return;
    }
    if (!event.key.startsWith("Arrow")) return;
    event.preventDefault();
    const step = event.ctrlKey ? 1 : 8;
    const horizontal = event.key === "ArrowLeft" ? -step : event.key === "ArrowRight" ? step : 0;
    const vertical = event.key === "ArrowUp" ? -step : event.key === "ArrowDown" ? step : 0;
    updateTable(table.tableId, (current) => {
      if (event.shiftKey) {
        const width = Math.max(48, Math.min(640, current.width + horizontal));
        const height = Math.max(48, Math.min(640, current.height + vertical));
        return resizeTable(current, width, height);
      }
      const x = Math.max(0, Math.min(draft.canvasWidth - current.width, current.x + horizontal));
      const y = Math.max(0, Math.min(draft.canvasHeight - current.height, current.y + vertical));
      return moveTable(current, x, y);
    });
  };

  const onPointerDown = (event: PointerEvent<HTMLButtonElement>, table: DraftTable) => {
    onSelectTable(table.tableId);
    if (mode !== "setup" || event.button !== 0) return;
    const canvas = event.currentTarget.closest<HTMLElement>(".floor-plan-canvas");
    if (!canvas) return;
    const rect = canvas.getBoundingClientRect();
    event.currentTarget.setPointerCapture(event.pointerId);
    drag.current = {
      pointerId: event.pointerId,
      tableId: table.tableId,
      startClientX: event.clientX,
      startClientY: event.clientY,
      startX: table.x,
      startY: table.y,
      scaleX: draft.canvasWidth / Math.max(1, rect.width),
      scaleY: draft.canvasHeight / Math.max(1, rect.height),
    };
  };

  const onPointerMove = (event: PointerEvent<HTMLButtonElement>) => {
    const currentDrag = drag.current;
    if (!currentDrag || currentDrag.pointerId !== event.pointerId) return;
    updateTable(currentDrag.tableId, (table) => moveTable(
      table,
      Math.max(0, Math.min(draft.canvasWidth - table.width, Math.round(currentDrag.startX + (event.clientX - currentDrag.startClientX) * currentDrag.scaleX))),
      Math.max(0, Math.min(draft.canvasHeight - table.height, Math.round(currentDrag.startY + (event.clientY - currentDrag.startClientY) * currentDrag.scaleY))),
    ));
  };

  const endPointer = (event: PointerEvent<HTMLButtonElement>) => {
    if (drag.current?.pointerId === event.pointerId) drag.current = null;
  };

  return (
    <section className={`floor-plan-shell floor-plan-shell--${mode}`} aria-label={`${plan.zoneName} salon planı`}>
      <header className="floor-plan-shell__toolbar">
        <div>
          <span className="floor-plan-shell__eyebrow">{mode === "setup" ? "KURULUM MODU" : "CANLI SALON"}</span>
          <strong>{plan.zoneName}</strong>
          <small>{draft.canvasWidth} × {draft.canvasHeight} · v{draft.rowVersion} · {lastUpdated ? `güncellendi ${lastUpdated}` : "sunucu durumu"}</small>
        </div>
        <div className="floor-plan-shell__mode-actions">
          {mode === "operation" && canManage && onSave && <Button variant="secondary" onClick={beginSetup}>Planı düzenle</Button>}
          {mode === "setup" && <>
            <Button variant="secondary" disabled={saveState === "saving"} onClick={cancelSetup}>Değişiklikleri bırak</Button>
            <Button disabled={saveState === "saving" || validation.errors.length > 0} onClick={() => void save()}>
              {saveState === "saving" ? "Kaydediliyor…" : "İncele ve kaydet"}
            </Button>
          </>}
        </div>
      </header>

      {(busy || error || saveMessage) && <div className={`floor-plan-shell__notice floor-plan-shell__notice--${error || saveState === "error" ? "error" : saveState === "conflict" ? "conflict" : "info"}`} role={error || saveState === "error" || saveState === "conflict" ? "alert" : "status"} aria-live="polite">
        {busy ? "Salon planı güncelleniyor…" : error ?? saveMessage}
      </div>}

      <div className="floor-plan-shell__workspace">
        <div className="floor-plan-shell__canvas-pane">
          <div
            className="floor-plan-canvas"
            style={{ aspectRatio: `${draft.canvasWidth} / ${draft.canvasHeight}` }}
            role="group"
            aria-label={`${plan.zoneName} mekânsal masa yerleşimi`}
          >
            <span className="floor-plan-canvas__north" aria-hidden="true">SALON GİRİŞİ ↑</span>
            {draft.tables.map((table) => {
              const authoritative = tableRecords.get(table.tableId);
              const selectedState = table.tableId === selected?.tableId;
              return <div
                key={table.tableId}
                className="floor-plan-table-wrap"
                style={{
                  left: `${table.x / draft.canvasWidth * 100}%`,
                  top: `${table.y / draft.canvasHeight * 100}%`,
                  width: `${table.width / draft.canvasWidth * 100}%`,
                  height: `${table.height / draft.canvasHeight * 100}%`,
                }}
              >
                <button
                  type="button"
                  className={`floor-plan-table floor-plan-table--${table.status.toLowerCase()} floor-plan-table--${table.shape.toLowerCase()} ${selectedState ? "is-selected" : ""}`}
                  aria-label={`${table.tableNumber}, ${tableStatusLabels[table.status]}, ${contextLabel(table)}${mode === "setup" ? ", ok tuşlarıyla taşı" : ""}`}
                  aria-pressed={selectedState}
                  onClick={() => onSelectTable(table.tableId)}
                  onKeyDown={(event) => onTableKeyDown(event, table)}
                  onPointerDown={(event) => onPointerDown(event, table)}
                  onPointerMove={onPointerMove}
                  onPointerUp={endPointer}
                  onPointerCancel={endPointer}
                  style={{ transform: `rotate(${table.rotationDegrees}deg)` }}
                >
                  <span className="floor-plan-table__number">{table.tableNumber}</span>
                  <span className="floor-plan-table__state">{tableStatusLabels[table.status]}</span>
                  <span className="floor-plan-table__context">{contextLabel(table)}</span>
                  {table.mergeGroupId && <span className="floor-plan-table__badge">{table.isMergePrimary ? "Birleşim lideri" : "Birleşik"}</span>}
                  {table.activeReservationId && <span className="floor-plan-table__badge">Rezerve</span>}
                  {authoritative?.occupiedSince && table.status === "Occupied" && <span className="floor-plan-table__elapsed">{elapsedLabel(authoritative.occupiedSince)}</span>}
                  {/* V1-TBL-009: same passive "just vacated" hint as TableWorkspace's own table card. */}
                  {table.status === "Available" && authoritative && isRecentlyVacated(authoritative) && <span className="floor-plan-table__badge floor-plan-table__badge--info">Az önce boşaldı</span>}
                </button>
                {table.seats.map((seat) => <span
                  key={seat.seatId}
                  className="floor-plan-seat"
                  style={{
                    left: `${(seat.x - table.x) / table.width * 100}%`,
                    top: `${(seat.y - table.y) / table.height * 100}%`,
                  }}
                  title={seat.label}
                >{seat.number}</span>)}
              </div>;
            })}
          </div>
          <div className="floor-plan-dense-list" role="group" aria-label="Erişilebilir masa listesi">
            {draft.tables.map((table) => <button key={table.tableId} type="button" onClick={() => onSelectTable(table.tableId)} aria-pressed={table.tableId === selected?.tableId}>
              <span><strong>{table.tableNumber}</strong><small>{tableStatusLabels[table.status]} · {table.capacity} kişi</small></span>
              <span>{contextLabel(table)}</span>
            </button>)}
          </div>
        </div>

        <aside className="floor-plan-inspector" aria-label="Salon planı denetçisi">
          {selected ? <>
            <div className="floor-plan-inspector__title">
              <span>SEÇİLİ MASA</span><strong>{selected.tableNumber}</strong><small>{tableStatusLabels[selected.status]} · tablo v{selected.tableRowVersion} · yerleşim v{selected.layoutRowVersion || "yeni"}</small>
            </div>
            <dl className="floor-plan-inspector__signals">
              <div><dt>Sipariş</dt><dd>{selected.currentOrderId?.slice(0, 8) ?? "Yok"}</dd></div>
              <div><dt>Hesap</dt><dd>{selected.currentBillId?.slice(0, 8) ?? "Yok"}</dd></div>
              <div><dt>Rezervasyon</dt><dd>{selected.activeReservationId ? `${selected.reservationPartySize ?? "?"} kişi` : "Yok"}</dd></div>
              <div><dt>Birleşim</dt><dd>{selected.mergeGroupId ? selected.isMergePrimary ? "Lider" : "Katılımcı" : "Yok"}</dd></div>
            </dl>
            {mode === "setup" ? <SetupFields table={selected} plan={draft} update={(mutate) => updateTable(selected.tableId, mutate)} /> : (() => {
              const selectedRecord = tableRecords.get(selected.tableId);
              return <>
                <div className="floor-plan-inspector__commands">
                  <span>Sunucunun izin verdiği işlemler</span>
                  {selected.allowedCommands.some((command): command is TableAction => command in tableActionLabels)
                    ? <div className="floor-plan-inspector__command-buttons">{selected.allowedCommands
                      .filter((command): command is TableAction => command in tableActionLabels)
                      .map((command) => <Button
                        key={command}
                        variant={command === "SetOutOfService" ? "secondary" : "primary"}
                        disabled={!onOpenAction || !selectedRecord || !isClientExecutableAction(command, selectedRecord)}
                        title={!selectedRecord || isClientExecutableAction(command, selectedRecord) ? undefined : "Bu masa için aktif rezervasyon bulunamadığından işlem güvenli biçimde kapalı."}
                        onClick={() => {
                          if (selectedRecord) onOpenAction?.(command, selectedRecord);
                        }}
                      >{tableActionLabels[command]}</Button>)}</div>
                    : <p>Bu durumda kullanılabilir işlem yok.</p>}
                  {selected.isMergePrimary && !selected.allowedCommands.includes("Unmerge") && <div className="floor-plan-inspector__command-buttons">
                    <Button variant="secondary" disabled title="Sunucu bu birleşim için ayırma komutunu henüz yayınlamadı.">Birleşimi ayır</Button>
                  </div>}
                </div>
                <p className="floor-plan-inspector__hint">Operasyon modunda geometri kilitlidir. İşlemler yalnız sunucunun bu masa için döndürdüğü komutlardan açılır.</p>
              </>;
            })()}
          </> : <p>Denetlemek için bir masa seçin.</p>}

          {mode === "setup" && <section className="floor-plan-review" aria-label="Plan doğrulama incelemesi">
            <div><strong>Doğrulama</strong><span>{validation.errors.length ? `${validation.errors.length} engel` : "Kayda hazır"}</span></div>
            {validation.errors.length > 0 && <ul className="floor-plan-review__errors">{validation.errors.map((issue) => <li key={issue}>{issue}</li>)}</ul>}
            {validation.warnings.length > 0 && <ul className="floor-plan-review__warnings">{validation.warnings.map((issue) => <li key={issue}>{issue}</li>)}</ul>}
            <p>Ok tuşu: taşı · Shift + ok: boyutlandır · Ctrl: 1 px hassasiyet · R: döndür</p>
          </section>}
        </aside>
      </div>
    </section>
  );
}

function SetupFields({ table, plan, update }: { table: DraftTable; plan: DraftPlan; update: (mutate: (table: DraftTable) => DraftTable) => void }) {
  const number = (field: "x" | "y" | "width" | "height", value: string) => update((current) => {
    const next = Number(value);
    if (field === "x") return moveTable(current, next, current.y);
    if (field === "y") return moveTable(current, current.x, next);
    if (field === "width") return resizeTable(current, next, current.height);
    return resizeTable(current, current.width, next);
  });
  const updateSeat = (seatId: string, mutate: (seat: DraftSeat) => DraftSeat) => update((current) => ({
    ...current,
    seats: current.seats.map((seat) => seat.seatId === seatId ? mutate(seat) : seat),
  }));
  const addSeat = () => update((current) => ({
    ...current,
    seats: [...current.seats, {
      seatId: crypto.randomUUID(),
      number: current.seats.reduce((maximum, seat) => Math.max(maximum, seat.number), 0) + 1,
      label: `Sandalye ${current.seats.length + 1}`,
      x: current.x + Math.round(current.width / 2),
      y: current.y + current.height,
      rowVersion: 0,
    }],
  }));

  return <div className="floor-plan-setup-fields">
    <div className="floor-plan-setup-fields__grid">
      <TextField label="X" type="number" min={0} max={plan.canvasWidth - table.width} value={table.x} onChange={(event) => number("x", event.target.value)} />
      <TextField label="Y" type="number" min={0} max={plan.canvasHeight - table.height} value={table.y} onChange={(event) => number("y", event.target.value)} />
      <TextField label="Genişlik" type="number" min={48} max={640} value={table.width} onChange={(event) => number("width", event.target.value)} />
      <TextField label="Yükseklik" type="number" min={48} max={640} value={table.height} onChange={(event) => number("height", event.target.value)} />
      <SelectField label="Biçim" value={table.shape} onChange={(event) => update((current) => {
        const shape = event.target.value as FloorTableShape;
        return shape === "Square" ? { ...resizeTable(current, current.width, current.width), shape } : { ...current, shape };
      })}>
        <option value="Rectangle">Dikdörtgen</option><option value="Round">Yuvarlak</option><option value="Square">Kare</option>
      </SelectField>
      <SelectField label="Dönüş" value={table.rotationDegrees} onChange={(event) => update((current) => ({ ...current, rotationDegrees: Number(event.target.value) as DraftTable["rotationDegrees"] }))}>
        {rotations.map((rotation) => <option key={rotation} value={rotation}>{rotation}°</option>)}
      </SelectField>
    </div>
    <div className="floor-plan-seat-editor">
      <div><strong>Sandalyeler</strong><Button variant="secondary" onClick={addSeat}>Sandalye ekle</Button></div>
      {table.seats.map((seat) => <div className="floor-plan-seat-editor__row" key={seat.seatId}>
        <input aria-label={`${seat.label} numarası`} type="number" min={1} value={seat.number} onChange={(event) => updateSeat(seat.seatId, (current) => ({ ...current, number: Number(event.target.value) }))} />
        <input aria-label={`${seat.number}. sandalye etiketi`} value={seat.label} onChange={(event) => updateSeat(seat.seatId, (current) => ({ ...current, label: event.target.value }))} />
        <button type="button" aria-label={`${seat.label} sandalyesini kaldır`} onClick={() => update((current) => ({ ...current, seats: current.seats.filter((candidate) => candidate.seatId !== seat.seatId) }))}>Kaldır</button>
      </div>)}
    </div>
  </div>;
}
