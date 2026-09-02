import type {
  AllocationVersionRequest,
  AmountSplitTarget,
  BillSplitDesign,
  ItemSplitTarget,
  SaveSplitRequest,
  SplitOwnerOption,
  SplitOwnerRequest,
} from "./models";

interface ServerOwnerOption {
  kind: string;
  id: string;
  label: string;
  secondaryLabel: string | null;
}

export class BillingSplitApiError extends Error {
  constructor(public readonly status: number, public readonly code: string, message: string) { super(message); }
}

export interface BillingSplitClient {
  get: () => Promise<BillSplitDesign>;
  getOwners: () => Promise<SplitOwnerOption[]>;
  save: (request: SaveSplitRequest, design: BillSplitDesign) => Promise<BillSplitDesign>;
  clear: (design: BillSplitDesign) => Promise<BillSplitDesign>;
  createFromOrder: (orderId: string) => Promise<BillSplitDesign>;
}

function versions(design: BillSplitDesign): AllocationVersionRequest[] {
  return design.allocations.map((allocation) => ({ allocationId: allocation.allocationId, rowVersion: allocation.rowVersion }));
}

export function createBillingSplitClient(terminalId: string, billId: string, fetcher: typeof fetch = fetch): BillingSplitClient {
  if (!terminalId.trim() || !billId.trim()) throw new Error("terminalId and billId are required");
  const prefix = `/api/v1/terminals/${encodeURIComponent(terminalId)}/billing/bills/${encodeURIComponent(billId)}/split-design`;

  async function call(path = "", method = "GET", body?: unknown) {
    let response: Response;
    try {
      response = await fetcher(`${prefix}${path}`, {
        method,
        credentials: "same-origin",
        headers: { "Content-Type": "application/json", "X-Correlation-Id": crypto.randomUUID() },
        body: body === undefined ? undefined : JSON.stringify(body),
        signal: AbortSignal.timeout(8_000),
      });
    } catch {
      throw new BillingSplitApiError(0, "NETWORK_UNAVAILABLE", "Sunucuya ulaşılamadı. Dağıtım taslağı korundu.");
    }
    if (!response.ok) {
      const payload = await response.json().catch(() => undefined) as { error?: { code?: string; message?: string } } | undefined;
      throw new BillingSplitApiError(response.status, payload?.error?.code ?? "REQUEST_FAILED", payload?.error?.message ?? "Hesap dağıtımı kaydedilemedi.");
    }
    return response.json() as Promise<BillSplitDesign>;
  }

  return {
    get: () => call(),
    getOwners: async () => {
      const raw = (await call("/owners")) as unknown as readonly ServerOwnerOption[];
      return raw.map((owner) => ({
        kind: owner.kind === "Seat" ? "Seat" : "Person",
        ownerId: owner.id,
        label: owner.label,
        secondaryLabel: owner.secondaryLabel ?? undefined,
      }));
    },
    save: (request, design) => {
      const common = { expectedBillRowVersion: design.billRowVersion, expectedAllocations: versions(design) };
      if (request.mode === "EqualByPerson") return call("/equal", "PUT", { ...common, owners: request.owners satisfies readonly SplitOwnerRequest[] });
      if (request.mode === "ByItem") return call("/items", "PUT", { ...common, targets: request.targets satisfies readonly ItemSplitTarget[] });
      return call("/amounts", "PUT", { ...common, targets: request.targets satisfies readonly AmountSplitTarget[] });
    },
    clear: (design) => call("/clear", "POST", { expectedBillRowVersion: design.billRowVersion, expectedAllocations: versions(design) }),
    createFromOrder: (orderId: string) => createBillFromOrder(terminalId, orderId, fetcher),
  };
}

export async function createBillFromOrder(
  terminalId: string,
  orderId: string,
  fetcher: typeof fetch = fetch
): Promise<BillSplitDesign> {
  if (!terminalId.trim() || !orderId.trim()) throw new Error("terminalId and orderId are required");
  const url = `/api/v1/terminals/${encodeURIComponent(terminalId)}/billing/bills/from-order/${encodeURIComponent(orderId)}`;

  let response: Response;
  try {
    response = await fetcher(url, {
      method: "POST",
      credentials: "same-origin",
      headers: { "Content-Type": "application/json", "X-Correlation-Id": crypto.randomUUID() },
      signal: AbortSignal.timeout(8_000),
    });
  } catch {
    throw new BillingSplitApiError(0, "NETWORK_UNAVAILABLE", "Sunucuya ulaşılamadı. Adisyon oluşturulamadı.");
  }
  if (!response.ok) {
    const payload = await response.json().catch(() => undefined) as { error?: { code?: string; message?: string } } | undefined;
    throw new BillingSplitApiError(response.status, payload?.error?.code ?? "REQUEST_FAILED", payload?.error?.message ?? "Adisyon oluşturulamadı.");
  }
  return response.json() as Promise<BillSplitDesign>;
}
