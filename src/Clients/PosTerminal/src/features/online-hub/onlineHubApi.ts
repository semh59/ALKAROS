/** V12-OUI-004: the online food screen's tabs and its platform status line. */

export type OnlineHubTab = "orders" | "settings";

export type ChannelPolling = "NotPolled" | "Working" | "RateLimited" | "Failing";

export interface OnlineChannelHealth {
  provider: string;
  displayName: string;
  configured: boolean;
  lastEventAt: string | null;
  polling: ChannelPolling | string;
}

/** The path of each tab; the old separate screens open the matching tab. */
export const onlineHubPaths: Record<OnlineHubTab, string> = { orders: "/online", settings: "/online/settings" };

/** Which tab a path opens, or null when the path is not the online food screen. */
export function onlineHubTabFor(path: string): OnlineHubTab | null {
  switch (path) {
    case "/online":
    case "/online-operations":
      return "orders";
    case "/online/settings":
    case "/online-platforms":
      return "settings";
    default:
      return null;
  }
}

/** Tabs only a manager's session sees (their data is never even requested otherwise). */
export const managerOnlyTabs: ReadonlySet<OnlineHubTab> = new Set(["settings"]);

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
