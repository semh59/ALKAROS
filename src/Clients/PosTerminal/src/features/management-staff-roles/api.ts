import type { PermissionInfo, RoleInfo, UserInfo } from "./models";

export class StaffApiError extends Error {
  constructor(public readonly status: number, public readonly code: string, message: string) { super(message); }
}

export interface UserLookup { userId: string; displayName: string; active: boolean; isLocked: boolean }

export interface StaffClient {
  createUser: (username: string, password: string, displayName: string) => Promise<void>;
  lookupUser: (username: string) => Promise<UserLookup | null>;
  setActive: (userId: string, active: boolean) => Promise<void>;
  listRoles: () => Promise<readonly RoleInfo[]>;
  listPermissions: () => Promise<readonly PermissionInfo[]>;
  listUsers: () => Promise<readonly UserInfo[]>;
  createRole: (code: string, name: string) => Promise<void>;
  assignPermission: (roleId: string, permissionCode: string) => Promise<void>;
  revokePermission: (roleId: string, permissionCode: string) => Promise<void>;
  assignUser: (roleId: string, userId: string) => Promise<void>;
  revokeUser: (roleId: string, userId: string) => Promise<void>;
}

const fallbackByStatus = (status: number): string =>
  status === 401 ? "Oturum sona erdi; yeniden giriş yapın."
    : status === 403 ? "Bu işlem için yetkiniz yok."
    : "İşlem tamamlanamadı.";

export function createStaffClient(fetcher: typeof fetch = fetch): StaffClient {
  async function call(path: string, init: RequestInit = {}): Promise<Response> {
    let response: Response;
    try {
      response = await fetcher(`/api/v1/management${path}`, {
        ...init,
        credentials: "same-origin",
        headers: { "Content-Type": "application/json", "X-Correlation-Id": crypto.randomUUID() },
        signal: AbortSignal.timeout(8_000),
      });
    } catch {
      throw new StaffApiError(0, "NETWORK_UNAVAILABLE", "Sunucuya ulaşılamadı. Bağlantıyı kontrol edip tekrar deneyin.");
    }
    if (response.ok) return response;
    const body = await response.json().catch(() => undefined) as { error?: { code?: string; message?: string } } | undefined;
    throw new StaffApiError(response.status, body?.error?.code ?? "REQUEST_FAILED", body?.error?.message ?? fallbackByStatus(response.status));
  }
  const json = async <T>(path: string): Promise<T> => (await call(path)).json() as Promise<T>;
  const send = (method: string, path: string, body?: unknown) =>
    call(path, { method, body: body === undefined ? undefined : JSON.stringify(body) }).then(() => undefined);
  const roles = "/roles/roles";
  return {
    createUser: (username, password, displayName) => send("POST", "/users", { username, password, displayName }),
    async lookupUser(username) {
      try {
        return await json<UserLookup>(`/security/users/lookup?username=${encodeURIComponent(username)}`);
      } catch (reason) {
        if (reason instanceof StaffApiError && reason.status === 404) return null;
        throw reason;
      }
    },
    setActive: (userId, active) => send("POST", `/security/users/${userId}/${active ? "reactivate" : "deactivate"}`, {}),
    listRoles: () => json(roles),
    listPermissions: () => json("/roles/permissions"),
    listUsers: () => json("/users"),
    createRole: (code, name) => send("POST", roles, { code, name }),
    assignPermission: (roleId, permissionCode) => send("POST", `${roles}/${roleId}/permissions`, { permissionCode }),
    revokePermission: (roleId, permissionCode) => send("DELETE", `${roles}/${roleId}/permissions/${encodeURIComponent(permissionCode)}`),
    assignUser: (roleId, userId) => send("POST", `${roles}/${roleId}/users/${userId}`),
    revokeUser: (roleId, userId) => send("DELETE", `${roles}/${roleId}/users/${userId}`),
  };
}
