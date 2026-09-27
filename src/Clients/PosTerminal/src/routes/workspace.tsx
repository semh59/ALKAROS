import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { ApiError, api } from "../api";
import { tryGetItem, trySetItem } from "../storage";
import { useRouter } from "../router";
import { navLabels, roleLabels } from "../strings";
import { ProductionShell } from "../shell";
import type { Connectivity, Freshness, RouteAuthorization, ShellIdentity, ShellNavigationItem, ShellSession } from "../shell/models";
import { TableWorkspace, createTableManagementClient, type CreateTableInput, type CreateZoneInput, type FloorPlan, type SaveFloorPlanInput, type SaveFloorPlanResult, type TableActionRequest, type TableWorkspaceState } from "../features/tables";
import { PendingChecksWorkspace } from "../features/pending-checks";
import { OnlineFoodHub, managerOnlyTabs, onlineHubPaths, onlineHubTabFor } from "../features/online-hub";
import { BillSplitWorkspace, createBillFromOrder, createBillingSplitClient, type BillSplitDesign, type BillSplitWorkspaceState, type SaveSplitRequest, type SplitOwnerOption } from "../features/billing";
import { CatalogWorkspace, createCatalogManagementClient, type CatalogCreateInput, type CatalogData, type CatalogWorkspaceState } from "../features/catalog";
import { KitchenOperationsWorkspace, createKitchenOperationsClient, loadKitchenRuntimeConfiguration, type KitchenData, type KitchenOperationsClient, type KitchenWorkspaceState } from "../features/kitchen-operations";
import { SystemHealthWorkspace, type SystemHealthState } from "../features/system-health";
import { AuthorizationDecisionsWorkspace, createAuthorizationDecisionsClient, type AuthorizationDecisionState, type AuthorizationDecisionsData } from "../features/authorization-decisions";

export type BackendStatus = "checking" | "online" | "offline";

export function ExperiencePage({
  terminalId,
  displayName,
  capabilities,
  path,
  backendStatus,
  customerDisplayUrl = "",
  onLogout,
}: {
  terminalId: string;
  displayName: string;
  capabilities: readonly string[];
  path: string;
  backendStatus: BackendStatus;
  customerDisplayUrl?: string;
  onLogout: () => Promise<void>;
}) {
  const { navigate } = useRouter();
  const capabilitySet = useMemo(() => new Set(capabilities), [capabilities]);
  // Which capability a route needs is a route->permission map, not a role guess.
  // `pos.cashier.mutate` was dropped from the permission catalog in migration
  // 049 (V1-IAM-024) — every check below used to key off it, so no session
  // could ever open Sales/Tables/Billing/Kitchen once granular permissions
  // shipped. Fixed to the granular codes each route's server endpoint
  // actually requires.
  const routeNeedsCatalogManage = path === "/catalog" || path === "/system-health";
  const routeNeedsReportsView = path === "/authorization";
  const routeNeedsBillsSplit = path === "/billing";
  const routeNeedsKitchenAdvance = path === "/kitchen";
  // V12-OUI-004: online food is one screen; its settings tab (V12-OUI-003's platform API settings) is a manager's
  // integration setting, like the QNB and relay screens. The old separate paths open the matching tab.
  const onlineTab = onlineHubTabFor(path);
  // V12-OUI-005: every manager-only tab (menu, settings) is closed to other sessions here too.
  const routeNeedsIntegrationsManage = onlineTab !== null && managerOnlyTabs.has(onlineTab);
  const canOpenRoute = routeNeedsIntegrationsManage
    ? capabilitySet.has("integrations.manage")
    : routeNeedsCatalogManage
    ? capabilitySet.has("catalog.manage")
    : routeNeedsReportsView
      ? capabilitySet.has("reports.view")
      : routeNeedsBillsSplit
        ? capabilitySet.has("bills.split")
        : routeNeedsKitchenAdvance
          // V1-IAM-028: either grant opens the tab — kitchen.advance so a
          // kitchen-staff-only session can reach it, orders.send so no
          // existing FOH session regresses even if a fixture/environment
          // somehow predates the additive migration. Which actions render
          // inside is decided separately by canAdvance/canOperate below
          // (KitchenRoute) — this only gates whether the tab opens at all.
          ? capabilitySet.has("kitchen.advance") || capabilitySet.has("orders.send")
          : capabilitySet.has("orders.create") || capabilitySet.has("tables.status");
  // Role label is derived from the session's capabilities, not from the current
  // route. (deep-analysis finding F-4)
  const roleLabel = capabilitySet.has("catalog.manage")
    ? roleLabels.manager
    : capabilitySet.has("bills.split")
      ? roleLabels.cashierOps
      : roleLabels.limited;
  const session: ShellSession = {
    status: "authenticated",
    identity: {
      // Branch (Sube) is omitted until there is a real multi-branch model;
      // a placeholder string would read as data. (deep-analysis finding F-5)
      terminalName: `Terminal ${terminalId.slice(0, 8).toUpperCase()}`,
      userName: displayName,
      roleLabel,
      capabilities: capabilitySet,
    } satisfies ShellIdentity,
  };
  const authorization: RouteAuthorization = canOpenRoute ? { status: "authorized" } : {
    status: "forbidden",
    onReturn: () => navigate("/"),
  };
  const [lastOnlineSync, setLastOnlineSync] = useState<string | null>(() => backendStatus === "online" ? new Date().toISOString() : null);

  useEffect(() => {
    if (backendStatus === "online") {
      setLastOnlineSync(new Date().toISOString());
    }
  }, [backendStatus]);

  const connectivity: Connectivity = backendStatus === "online"
    ? { status: "online" }
    : backendStatus === "offline"
      ? { status: "offline", onRetry: () => window.location.reload() }
      : { status: "reconnecting" };
  const freshness: Freshness = backendStatus === "online"
    ? {
        status: "fresh",
        dateTime: lastOnlineSync ?? new Date().toISOString(),
        label: "Çevrimiçi doğrulandı",
      }
    : {
        status: "stale",
        dateTime: lastOnlineSync ?? "—",
        label: lastOnlineSync ? "Bağlantı kesildi" : "Bağlantı bekleniyor",
        onRefresh: () => window.location.reload(),
      };
  const navigation: readonly ShellNavigationItem[] = [
    { id: "sales", label: navLabels.sales, href: "/", icon: "sales", requiredCapability: "orders.create" },
    { id: "tables", label: navLabels.tables, href: "/tables", icon: "tables", requiredCapability: "tables.status" },
    { id: "billing", label: navLabels.billing, href: "/billing", icon: "billing", requiredCapability: "bills.split" },
    { id: "pending-checks", label: navLabels.pendingChecks, href: "/pending-checks", icon: "billing", requiredCapability: "orders.create" },
    // V12-OUI-004: one entry for online food (orders, and for a manager its settings); same permission as accepting a QR order.
    { id: "online", label: navLabels.onlineFood, href: onlineHubPaths.orders, icon: "billing", requiredCapability: "orders.create" },
    // V1-IAM-028: kitchen.advance is additive — every role that held
    // orders.send before still holds it, and the new kitchen-staff
    // ("Mutfak Personeli") role holds ONLY kitchen.advance. Gating the nav
    // link on orders.send would have locked that role out of the Kitchen
    // tab entirely.
    { id: "kitchen", label: navLabels.kitchen, href: "/kitchen", icon: "kitchen", requiredCapability: "kitchen.advance" },
    { id: "catalog", label: navLabels.catalog, href: "/catalog", icon: "catalog", requiredCapability: "catalog.manage" },
    { id: "system-health", label: navLabels.system, href: "/system-health", icon: "system", requiredCapability: "catalog.manage" },
    { id: "authorization", label: navLabels.authorization, href: "/authorization", icon: "system", requiredCapability: "reports.view" },
  ];
  const title = path === "/tables" ? "Masa yönetimi" : path === "/pending-checks" ? "Bekleyen hesaplar" : onlineTab !== null ? "Online Yemek" : path === "/billing" ? "Hesap bölme" : path === "/catalog" ? "Menü ve katalog" : path === "/kitchen" ? "Mutfak ve operasyon" : path === "/system-health" ? "Sistem sağlığı" : path === "/authorization" ? "Yetki kararları" : "Kasa satış";
  const description = path === "/tables" ? "Salon, masa durumu ve servis akışı" : path === "/pending-checks" ? "Garsonların kasaya gönderdiği, tahsil edilmeyi bekleyen hesaplar" : onlineTab !== null ? "QR ve online platform siparişleri, platform bağlantıları ve ayarlar" : path === "/billing" ? "Kişi, ürün veya tutar bazlı hesap paylaştırma" : path === "/catalog" ? "Fiyat, ürün ve modifikatör kayıtları" : path === "/kitchen" ? "Ticket, yazıcı kurtarma ve operasyon sağlığı" : path === "/system-health" ? "Veritabanı, disk ve yedekleme durumu" : path === "/authorization" ? "Bekleyen istekler, süreli devirler ve davranışsal sıkılaştırmalar" : "Gerçek zamanlı sipariş ve müşteri ekranı";

  return <ProductionShell
    session={session}
    authorization={authorization}
    connectivity={connectivity}
    freshness={freshness}
    navigation={navigation}
    onNavigate={navigate}
    activeNavigationId={path === "/tables" ? "tables" : path === "/pending-checks" ? "pending-checks" : onlineTab !== null ? "online" : path === "/billing" ? "billing" : path === "/catalog" ? "catalog" : path === "/kitchen" ? "kitchen" : path === "/system-health" ? "system-health" : path === "/authorization" ? "authorization" : "sales"}
    workspaceTitle={title}
    workspaceDescription={description}
    headerActions={<><a className="experience-header-link" href={`${customerDisplayUrl.replace(/\/+$/, "")}/display`} target="alkaros-customer-display">Müşteri ekranı</a><button className="experience-header-button" type="button" onClick={() => void onLogout()}>Çıkış</button></>}
  >
    {path === "/tables" && <TableRoute terminalId={terminalId} canManage={canOpenRoute} />}
    {path === "/billing" && <BillingRoute terminalId={terminalId} canManage={canOpenRoute} />}
    {path === "/pending-checks" && <PendingChecksWorkspace terminalId={terminalId} />}
    {onlineTab !== null && (
      <OnlineFoodHub
        terminalId={terminalId}
        tab={onlineTab}
        canManage={capabilitySet.has("integrations.manage")}
        onSelectTab={(tab) => navigate(onlineHubPaths[tab])}
      />
    )}
    {path === "/catalog" && <CatalogRoute canManage={canOpenRoute} />}
    {path === "/kitchen" && <KitchenRoute terminalId={terminalId} canAdvance={capabilitySet.has("kitchen.advance") || capabilitySet.has("orders.send")} canOperate={capabilitySet.has("orders.send")} canManageReprints={capabilitySet.has("kitchen.reprint")} canManageRouting={capabilitySet.has("kitchen.routing.manage")} canSuspendAvailability={capabilitySet.has("kitchen.availability.suspend")} canViewReports={capabilitySet.has("reports.view")} />}
    {path === "/system-health" && <SystemHealthRoute terminalId={terminalId} canView={canOpenRoute} />}
    {path === "/authorization" && <AuthorizationDecisionsRoute canView={canOpenRoute} />}
    {onlineTab === null && !(["/", "/tables", "/billing", "/pending-checks", "/catalog", "/kitchen", "/system-health", "/authorization"] as readonly string[]).includes(path) && <div className="experience-not-found">Bu çalışma alanı bulunamadı.</div>}
  </ProductionShell>;
}

// Exported for the standalone Reservation Station screen (V1-CUI-006),
// which reuses this exact table-fetching + floor-plan wiring rather than
// duplicating it.
export function TableRoute({ terminalId, canManage }: { terminalId: string; canManage: boolean }) {
  const { navigate } = useRouter();
  const client = useMemo(() => createTableManagementClient(terminalId), [terminalId]);
  const [state, setState] = useState<TableWorkspaceState>("loading");
  const [zones, setZones] = useState<Awaited<ReturnType<typeof client.listZones>>>([]);
  const [tables, setTables] = useState<Awaited<ReturnType<typeof client.listTables>>>([]);
  const [selectedZoneId, setSelectedZoneId] = useState<string>("all");
  const [floorPlan, setFloorPlan] = useState<FloorPlan | undefined>();
  const [floorPlanBusy, setFloorPlanBusy] = useState(false);
  const [floorPlanError, setFloorPlanError] = useState<string>();
  const [errorMessage, setErrorMessage] = useState<string>();
  const [lastUpdated, setLastUpdated] = useState<string>();
  const [selectedTableId, setSelectedTableId] = useState<string | null>(null);
  const [orderBusy, setOrderBusy] = useState(false);
  const [orderError, setOrderError] = useState<string>();

  const loadFloorPlanForZone = useCallback(async (zoneId: string, currentZones: Awaited<ReturnType<typeof client.listZones>>) => {
    const targetZoneId = (zoneId === "all" && currentZones.length > 0) ? currentZones[0].zoneId : zoneId;
    if (targetZoneId && targetZoneId !== "all") {
      try {
        const plan = await client.getFloorPlan(targetZoneId);
        setFloorPlan(plan);
      } catch {
        setFloorPlan(undefined);
      }
    } else {
      setFloorPlan(undefined);
    }
  }, [client]);

  const load = useCallback(async () => {
    setState("loading");
    setErrorMessage(undefined);
    try {
      const [nextZones, nextTables] = await Promise.all([client.listZones(), client.listTables()]);
      setZones(nextZones);
      setTables(nextTables);
      setSelectedTableId((current) => current && nextTables.some((table) => table.tableId === current) ? current : nextTables[0]?.tableId ?? null);
      await loadFloorPlanForZone(selectedZoneId, nextZones);
      setLastUpdated(new Date().toISOString());
      setState(nextTables.length ? "ready" : "empty");
    } catch (reason) {
      const status = (reason as { status?: number }).status;
      setState(status === 0 ? "offline" : status === 401 ? "unauthorized" : status === 409 ? "stale" : "error");
      setErrorMessage(reason instanceof ApiError ? reason.message : "Masa verisi alınamadı.");
    }
  }, [client, selectedZoneId, loadFloorPlanForZone]);

  useEffect(() => { void load(); }, [load]);

  const handleSelectZone = (zoneId: string) => {
    setSelectedZoneId(zoneId);
    void loadFloorPlanForZone(zoneId, zones);
  };

  const mutate = async (action: () => Promise<unknown>) => { await action(); await load(); };
  const handleSaveFloorPlan = async (zoneId: string, input: SaveFloorPlanInput): Promise<SaveFloorPlanResult> => {
    setFloorPlanBusy(true);
    setFloorPlanError(undefined);
    try {
      const result = await client.saveFloorPlan(zoneId, input);
      setFloorPlan(result.floorPlan);
      return result;
    } catch (reason) {
      const msg = reason instanceof ApiError ? reason.message : "Kat planı kaydedilemedi.";
      setFloorPlanError(msg);
      throw reason;
    } finally {
      setFloorPlanBusy(false);
    }
  };
  const selectedTable = tables.find((table) => table.tableId === selectedTableId) ?? null;
  const canStartTableOrder = selectedTable?.active === true
    && (selectedTable.status === "Available" || (selectedTable.status === "Occupied" && selectedTable.currentOrderId !== null));
  const startTableOrder = async () => {
    if (!selectedTable || !canManage || !canStartTableOrder) return;
    setOrderBusy(true);
    setOrderError(undefined);
    try {
      await api.startTableOrder(terminalId, selectedTable.tableId, selectedTable.rowVersion);
      navigate("/");
    } catch (reason) {
      setOrderError(reason instanceof ApiError ? reason.message : "Masa siparişi açılamadı.");
      await load().catch(() => undefined);
    } finally {
      setOrderBusy(false);
    }
  };
  return <div className="table-route">
    <TableWorkspace
      state={state}
      zones={zones}
      tables={tables}
      canManage={canManage}
      selectedTableId={selectedTableId}
      onSelectTable={(tableId) => { setSelectedTableId(tableId); setOrderError(undefined); }}
      selectedZoneId={selectedZoneId}
      onSelectZone={handleSelectZone}
      onRefresh={load}
      onCreateZone={canManage ? (input: CreateZoneInput) => mutate(() => client.createZone(input)) : undefined}
      onCreateTable={canManage ? (input: CreateTableInput) => mutate(() => client.createTable(input)) : undefined}
      onAction={canManage ? (request: TableActionRequest) => mutate(() => client.execute(request)) : undefined}
      floorPlan={floorPlan}
      floorPlanBusy={floorPlanBusy}
      floorPlanError={floorPlanError}
      onSaveFloorPlan={canManage ? handleSaveFloorPlan : undefined}
      errorMessage={errorMessage}
      lastUpdated={lastUpdated}
    />
    {selectedTable && state === "ready" && <section className="table-order-bridge" aria-label="Masa sipariş bağlamı">
      <div>
        <span className="table-order-bridge__kicker">SİPARİŞ BAĞLAMI</span>
        <strong>{selectedTable.tableNumber}</strong>
        <p>{selectedTable.currentOrderId ? "Bu masadaki taslak siparişe devam edin." : "Bu masa seçildiğinde sipariş ve mutfak kaydı birlikte açılır."}</p>
      </div>
      <button
        className="primary"
        type="button"
        disabled={!canManage || !canStartTableOrder || orderBusy}
        aria-busy={orderBusy}
        onClick={() => void startTableOrder()}
      >{orderBusy ? "Sipariş hazırlanıyor…" : selectedTable.currentOrderId ? "Siparişe devam et" : "Masada sipariş aç"}</button>
      {!canStartTableOrder && <small className="table-order-bridge__reason">Masa şu anda sipariş almaya uygun değil; sunucu durumu değiştirilmeden işlem yapılamaz.</small>}
      {orderError && <p className="table-order-bridge__error" role="alert">{orderError}</p>}
    </section>}
  </div>;
}

function BillingRoute({ terminalId, canManage }: { terminalId: string; canManage: boolean }) {
  const searchParams = useMemo(() => new URLSearchParams(window.location.search), []);
  const orderParam = searchParams.get("orderId");
  const [billId, setBillId] = useState<string>(
    () => searchParams.get("billId") || tryGetItem("alkaros.current-bill-id") || "",
  );
  const client = useMemo(
    () => (billId ? createBillingSplitClient(terminalId, billId) : null),
    [terminalId, billId],
  );
  const [state, setState] = useState<BillSplitWorkspaceState>("loading");
  const [design, setDesign] = useState<BillSplitDesign | null>(null);
  // Owners come from the server (table seats + existing person allocations), not
  // from client-fabricated placeholder GUIDs. (deep-analysis finding F-2)
  const [owners, setOwners] = useState<readonly SplitOwnerOption[]>([]);
  const [errorMessage, setErrorMessage] = useState<string>();
  const [lastUpdated, setLastUpdated] = useState<string>();

  const load = useCallback(async () => {
    setState("loading");
    setErrorMessage(undefined);
    if (!orderParam && !client) {
      setState("error");
      setErrorMessage("Aktif adisyon yok. Bir masa veya siparişten hesap açın.");
      return;
    }
    try {
      const nextDesign = orderParam
        ? await createBillFromOrder(terminalId, orderParam)
        : await client!.get();
      setDesign(nextDesign);
      if (nextDesign.billId && nextDesign.billId !== billId) {
        setBillId(nextDesign.billId);
        trySetItem("alkaros.current-bill-id", nextDesign.billId);
      }
      const ownerClient = nextDesign.billId && nextDesign.billId !== billId
        ? createBillingSplitClient(terminalId, nextDesign.billId)
        : client!;
      try {
        setOwners(await ownerClient.getOwners());
      } catch {
        setOwners([]);
      }
      setLastUpdated(new Date().toISOString());
      setState("ready");
    } catch (reason) {
      const status = (reason as { status?: number }).status;
      setState(status === 0 ? "offline" : status === 401 ? "unauthorized" : status === 409 ? "stale" : "error");
      setErrorMessage(reason instanceof ApiError ? reason.message : "Hesap bölme verisi alınamadı.");
    }
  }, [client, orderParam, terminalId, billId]);

  useEffect(() => { void load(); }, [load]);

  const handleSave = async (request: SaveSplitRequest, currentDesign: BillSplitDesign): Promise<BillSplitDesign> => {
    const targetClient = createBillingSplitClient(terminalId, currentDesign.billId);
    const updated = await targetClient.save(request, currentDesign);
    setDesign(updated);
    if (updated.billId && updated.billId !== billId) {
      setBillId(updated.billId);
      trySetItem("alkaros.current-bill-id", updated.billId);
    }
    return updated;
  };

  const handleClear = async (currentDesign: BillSplitDesign): Promise<BillSplitDesign> => {
    const targetClient = createBillingSplitClient(terminalId, currentDesign.billId);
    const cleared = await targetClient.clear(currentDesign);
    setDesign(cleared);
    return cleared;
  };

  return (
    <div className="billing-route">
      <BillSplitWorkspace
        state={state}
        design={design}
        owners={owners}
        canMutate={canManage}
        onRefresh={load}
        onSave={handleSave}
        onClear={handleClear}
        errorMessage={errorMessage}
        lastUpdated={lastUpdated}
      />
      {design?.billId && (
        // The split editor only decides who owes what; collecting the money
        // happens on the cashier payment page (V13-PUI-001), which had no
        // inbound link from anywhere in the running app.
        <a
          className="billing-route__collect"
          href={`/cashier/payments/split-payment/index.html?billId=${encodeURIComponent(design.billId)}`}
        >
          Tahsilata geç
        </a>
      )}
    </div>
  );
}

const emptyCatalogData: CatalogData = {
  categories: [], taxes: [], products: [], modifierGroups: [], modifiers: [], prices: [],
};

function CatalogRoute({ canManage }: { canManage: boolean }) {
  const client = useMemo(() => createCatalogManagementClient(), []);
  const [state, setState] = useState<CatalogWorkspaceState>("loading");
  const [data, setData] = useState<CatalogData>(emptyCatalogData);
  const [errorMessage, setErrorMessage] = useState<string>();
  const [lastUpdated, setLastUpdated] = useState<string>();
  const load = useCallback(async () => {
    if (!canManage) { setState("unauthorized"); return; }
    setState("loading");
    setErrorMessage(undefined);
    try {
      setData(await client.load());
      setLastUpdated(new Date().toISOString());
      setState("ready");
    } catch (reason) {
      const status = (reason as { status?: number }).status;
      setState(status === 0 ? "offline" : status === 401 ? "unauthorized" : status === 409 ? "conflict" : "error");
      setErrorMessage(reason instanceof ApiError ? reason.message : "Katalog verisi alınamadı.");
    }
  }, [canManage, client]);
  useEffect(() => { void load(); }, [load]);
  const create = async (input: CatalogCreateInput) => { await client.create(input); await load(); };
  const setAvailability = async (productId: string, isAvailable: boolean) => { await client.setAvailability(productId, isAvailable); await load(); };
  const setPrepTime = async (productId: string, prepTimeMinutes: number | null) => { await client.setPrepTime(productId, prepTimeMinutes); await load(); };
  return <CatalogWorkspace state={state} data={data} canManage={canManage} onRefresh={load} onCreate={canManage ? create : undefined} onSetAvailability={canManage ? setAvailability : undefined} onSetPrepTime={canManage ? setPrepTime : undefined} errorMessage={errorMessage} lastUpdated={lastUpdated} />;
}

function SystemHealthRoute({ terminalId, canView }: { terminalId: string; canView: boolean }) {
  const [state, setState] = useState<SystemHealthState>("loading");
  const [health, setHealth] = useState<KitchenData["health"]>(null);
  const [backups, setBackups] = useState<KitchenData["backups"]>([]);
  const [errorMessage, setErrorMessage] = useState<string>();
  const [lastUpdated, setLastUpdated] = useState<string>();
  const load = useCallback(async () => {
    if (!canView) { setState("unauthorized"); return; }
    setState("loading");
    setErrorMessage(undefined);
    try {
      const configuration = await loadKitchenRuntimeConfiguration(terminalId);
      const client = createKitchenOperationsClient(terminalId, configuration.kitchenStationId);
      const next = await client.load();
      setHealth(next.health);
      setBackups(next.backups);
      setLastUpdated(new Date().toISOString());
      setState("ready");
    } catch (reason) {
      const status = (reason as { status?: number }).status;
      setHealth(null);
      setBackups([]);
      setState(status === 0 ? "offline" : status === 401 ? "unauthorized" : "error");
      setErrorMessage(reason instanceof ApiError ? reason.message : "Sağlık verisi alınamadı.");
    }
  }, [canView, terminalId]);
  useEffect(() => { void load(); }, [load]);
  return <SystemHealthWorkspace state={state} health={health} backups={backups} onRefresh={load} errorMessage={errorMessage} lastUpdated={lastUpdated} />;
}

const emptyAuthorizationDecisions: AuthorizationDecisionsData = { pendingGrants: [], delegations: [], tightenings: [] };

function AuthorizationDecisionsRoute({ canView }: { canView: boolean }) {
  const client = useMemo(() => createAuthorizationDecisionsClient(), []);
  const [state, setState] = useState<AuthorizationDecisionState>("loading");
  const [data, setData] = useState<AuthorizationDecisionsData>(emptyAuthorizationDecisions);
  const [errorMessage, setErrorMessage] = useState<string>();
  const [lastUpdated, setLastUpdated] = useState<string>();
  const load = useCallback(async () => {
    if (!canView) { setState("unauthorized"); return; }
    setState("loading");
    setErrorMessage(undefined);
    try {
      const next = await client.load();
      setData(next);
      setLastUpdated(new Date().toISOString());
      setState("ready");
    } catch (reason) {
      const status = (reason as { status?: number }).status;
      setData(emptyAuthorizationDecisions);
      setState(status === 0 ? "offline" : status === 401 ? "unauthorized" : status === 403 ? "unauthorized" : "error");
      setErrorMessage(reason instanceof ApiError ? reason.message : "Yetki verisi alınamadı.");
    }
  }, [canView, client]);
  useEffect(() => { void load(); }, [load]);
  const act = (run: () => Promise<void>) => async () => { await run(); await load(); };
  return <AuthorizationDecisionsWorkspace
    state={state}
    pendingGrants={data.pendingGrants}
    delegations={data.delegations}
    tightenings={data.tightenings}
    onApprove={(id) => act(() => client.approve(id))()}
    onDeny={(id) => act(() => client.deny(id))()}
    onRevokeDelegation={(id) => act(() => client.revokeDelegation(id))()}
    onClearTightening={(id) => act(() => client.clearTightening(id))()}
    onRefresh={load}
    errorMessage={errorMessage}
    lastUpdated={lastUpdated}
  />;
}

const emptyKitchenData: KitchenData = { tickets: [], printers: [], routes: [], categories: [], unknownDeliveries: [], health: null, backups: [], liveSyncEnabled: false, denseModeThreshold: 9 };

function KitchenRoute({ terminalId, canAdvance, canOperate, canManageReprints, canManageRouting, canSuspendAvailability, canViewReports }: { terminalId: string; canAdvance: boolean; canOperate: boolean; canManageReprints: boolean; canManageRouting: boolean; canSuspendAvailability: boolean; canViewReports: boolean }) {
  const [stationId, setStationId] = useState("");
  const [client, setClient] = useState<KitchenOperationsClient | null>(null);
  const [state, setState] = useState<KitchenWorkspaceState>("loading");
  const [data, setData] = useState<KitchenData>(emptyKitchenData);
  const [errorMessage, setErrorMessage] = useState<string>();
  const [lastUpdated, setLastUpdated] = useState<string>();
  const load = useCallback(async () => {
    if (!canAdvance) { setState("unauthorized"); return; }
    setState("loading");
    setErrorMessage(undefined);
    try {
      const configuration = await loadKitchenRuntimeConfiguration(terminalId);
      const nextClient = createKitchenOperationsClient(terminalId, configuration.kitchenStationId);
      const next = await nextClient.load();
      setStationId(configuration.kitchenStationId);
      setClient(nextClient);
      setData(next);
      setLastUpdated(new Date().toISOString());
      setState(next.tickets.length || next.unknownDeliveries.length ? "ready" : "empty");
    } catch (reason) {
      const status = (reason as { status?: number }).status;
      setStationId("");
      setClient(null);
      setData(emptyKitchenData);
      setState(status === 0 ? "offline" : status === 401 ? "unauthorized" : status === 409 ? "conflict" : "error");
      setErrorMessage(reason instanceof ApiError ? reason.message : "Mutfak verisi alınamadı.");
    }
  }, [canAdvance, terminalId]);
  useEffect(() => { void load(); }, [load]);

  // V1-RMD-223: found by an independent audit (2026-09-16) - load() (and
  // therefore onRefresh, which the workspace's own 8s poll calls) builds a
  // brand-new client every cycle, so `client` itself is a fresh reference
  // every poll even though nothing about it actually changed. A plain
  // `client ? (from, to) => client.getPerformanceReport(from, to) : undefined`
  // inline closure was a new function identity every render for the same
  // reason - the report screen's own effect depends on this prop, so it
  // kept re-firing (the Turkish "loading" state flashing) every poll tick while
  // someone was reading it, exactly the polling-inclusion the workspace's
  // own comment says the report is NOT supposed to have. A ref keeps the
  // callback's identity stable across polls while still always calling
  // whatever client is current at the time it is actually invoked.
  const clientRef = useRef(client);
  useEffect(() => { clientRef.current = client; }, [client]);
  const loadPerformanceReport = useCallback(
    (from: string, to: string) => {
      if (!clientRef.current) return Promise.reject(new Error("Mutfak istemcisi hazır değil."));
      return clientRef.current.getPerformanceReport(from, to);
    },
    [],
  );

  return <KitchenOperationsWorkspace
    state={state}
    stationId={stationId || "Mutfak"}
    data={data}
    canAdvance={canAdvance}
    canOperate={canOperate}
    canManageReprints={canManageReprints}
    canManageRouting={canManageRouting}
    canSuspendAvailability={canSuspendAvailability}
    canViewReports={canViewReports}
    onRefresh={load}
    onTransitionItem={canAdvance && client ? async (ticket, item, target) => { await client.transitionItem(ticket.id, item.id, target, ticket.rowVersion, item.rowVersion); await load(); } : undefined}
    onUndoItem={canAdvance && client ? async (ticket, item) => { await client.undoItem(ticket.id, item.id, ticket.rowVersion, item.rowVersion); await load(); } : undefined}
    onTransitionTicket={canOperate && client ? async (ticket, target, reason) => { await client.transitionTicket(ticket.id, target, ticket.rowVersion, reason); await load(); } : undefined}
    onApproveReprint={canManageReprints && client ? async (delivery, reason) => { await client.approveReprint(delivery.id, reason); await load(); } : undefined}
    onRejectReprint={canManageReprints && client ? async (delivery, reason) => { await client.rejectReprint(delivery.id, reason); await load(); } : undefined}
    onCreateCategoryRoute={canManageRouting && client ? async (categoryId, printerId) => { await client.createCategoryRoute(categoryId, printerId); await load(); } : undefined}
    // V1-KDS-002: present whenever a client exists - the workspace itself
    // decides whether the button is enabled or shown-but-locked, matching
    // this task's own acceptance evidence (a kitchen-staff session must see
    // the same button, locked, not a hidden one).
    onSuspendProductAvailability={client ? async (productId) => { const result = await client.suspendProductAvailability(productId); await load(); return result; } : undefined}
    onLoadPerformanceReport={client ? loadPerformanceReport : undefined}
    errorMessage={errorMessage}
    lastUpdated={lastUpdated}
  />;
}
