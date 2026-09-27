/** V12-OUI-004: the online food screen's tabs and its platform status line. */

export type OnlineHubTab = "orders" | "menu" | "problems" | "settings";

export type ChannelPolling = "NotPolled" | "Working" | "RateLimited" | "Failing";

export interface OnlineChannelHealth {
  provider: string;
  displayName: string;
  configured: boolean;
  lastEventAt: string | null;
  polling: ChannelPolling | string;
}

/** The path of each tab; the old separate screens open the matching tab. */
export const onlineHubPaths: Record<OnlineHubTab, string> = { orders: "/online", menu: "/online/menu", problems: "/online/problems", settings: "/online/settings" };

/** Which tab a path opens, or null when the path is not the online food screen. */
export function onlineHubTabFor(path: string): OnlineHubTab | null {
  switch (path) {
    case "/online":
    case "/online-operations":
      return "orders";
    case "/online/menu":
      return "menu";
    case "/online/problems":
      return "problems";
    case "/online/settings":
    case "/online-platforms":
      return "settings";
    default:
      return null;
  }
}

/**
 * The capabilities a session needs to see each tab (any one of them); null means the screen's own permission is enough.
 * A tab a session may not see is never shown and its data is never requested. Menu and settings are a manager's
 * (V12-GOV-009); problems are for staff who see reports, and only a reconciliation manager acts on them.
 */
export const onlineTabCapabilities: Record<OnlineHubTab, readonly string[] | null> = {
  orders: null,
  menu: ["integrations.manage"],
  problems: ["reports.view", "reconciliation.manage"],
  settings: ["integrations.manage"],
};

export const canSeeOnlineTab = (tab: OnlineHubTab, capabilities: ReadonlySet<string>) => {
  const needed = onlineTabCapabilities[tab];
  return needed === null || needed.some((capability) => capabilities.has(capability));
};

const time = (iso: string) =>
  new Date(iso).toLocaleString("tr-TR", { day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit" });

/** One line per platform, in Turkish; never the raw status code. */
export function channelStatusText(channel: OnlineChannelHealth): string {
  if (!channel.configured) return "Bağlantı bilgileri eksik";
  if (channel.polling === "Failing") return "Sipariş çekme hata veriyor, yeniden deneniyor";
  if (channel.polling === "RateLimited") return "Platform istek sınırına takıldı, bekleniyor";
  return channel.lastEventAt ? `Son sipariş olayı: ${time(channel.lastEventAt)}` : "Henüz sipariş olayı gelmedi";
}

/** Whether the line needs attention (shown as a warning). */
export const channelNeedsAttention = (channel: OnlineChannelHealth) =>
  !channel.configured || channel.polling === "Failing" || channel.polling === "RateLimited";

export class OnlineHubApiError extends Error {}

export async function loadChannelHealth(terminalId: string, fetcher: typeof fetch = fetch): Promise<OnlineChannelHealth[]> {
  let response: Response;
  try {
    response = await fetcher(`/api/v1/terminals/${encodeURIComponent(terminalId)}/online-channels`, {
      credentials: "same-origin", signal: AbortSignal.timeout(8_000),
    });
  } catch {
    throw new OnlineHubApiError("Platform durumu okunamadı.");
  }
  if (!response.ok) throw new OnlineHubApiError(response.status === 401 ? "Oturum sona erdi." : "Platform durumu okunamadı.");
  return ((await response.json()) as { platforms: OnlineChannelHealth[] }).platforms;
}
