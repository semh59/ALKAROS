import type {
  AllocationVersionRequest,
  AmountSplitTarget,
  BillSplitDesign,
  ItemSplitTarget,
  SaveSplitRequest,
  SplitOwnerRequest,
} from "./models";

export class BillingSplitApiError extends Error {
  constructor(public readonly status: number, public readonly code: string, message: string) { super(message); }
}

export interface BillingSplitClient {
  get: () => Promise<BillSplitDesign>;
  save: (request: SaveSplitRequest, design: BillSplitDesign) => Promise<BillSplitDesign>;
  clear: (design: BillSplitDesign) => Promise<BillSplitDesign>;
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
    save: (request, design) => {
      const common = { expectedBillRowVersion: design.billRowVersion, expectedAllocations: versions(design) };
      if (request.mode === "EqualByPerson") return call("/equal", "PUT", { ...common, owners: request.owners satisfies readonly SplitOwnerRequest[] });
      if (request.mode === "ByItem") return call("/items", "PUT", { ...common, targets: request.targets satisfies readonly ItemSplitTarget[] });
      return call("/amounts", "PUT", { ...common, targets: request.targets satisfies readonly AmountSplitTarget[] });
    },
    clear: (design) => call("/clear", "POST", { expectedBillRowVersion: design.billRowVersion, expectedAllocations: versions(design) }),
  };
}
