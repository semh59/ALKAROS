# V1-RMD-044 Verification Evidence

- Task ID: V1-RMD-044
- Date: 2026-08-29
- Assignee: a04804b8-a15d-498a-9753-7b7c3a0e27b3

## 1. Fixed PosTerminal TS2322 Errors

- In `src/Clients/PosTerminal/src/App.tsx`:
  - Fixed `handleSave` to return `Promise<BillSplitDesign>` and accept `(request: SaveSplitRequest, currentDesign: BillSplitDesign)`.
  - Fixed `handleClear` to return `Promise<BillSplitDesign>` and accept `(currentDesign: BillSplitDesign)`.
  - Passed `design: BillSplitDesign | null` directly into `BillSplitWorkspace`.

## 2. TableRoute Floor Plan Integration

- Added `FloorPlan`, `SaveFloorPlanInput`, `SaveFloorPlanResult` type imports in `App.tsx`.
- Bound `floorPlan`, `floorPlanBusy`, `floorPlanError` state in `TableRoute`.
- Implemented `handleSaveFloorPlan` with `client.saveFloorPlan(zoneId, input)`.
- Passed all floor plan props into `<TableWorkspace />`.

## 3. ExperienceRoute Freshness Calculation

- Dynamically computed `freshness` based on `backendStatus`:
  - When `"online"`: `status: "fresh"`, `label: "Çevrimiçi doğrulandı"`.
  - When `"offline"` / `"reconnecting"`: `status: "stale"`, `label: "Bağlantı bekleniyor"`, with `onRefresh` handler.
