import { useCallback, useEffect, useId, useState } from "react";
import { OnlineMenuTab } from "../online-menu";
import { OnlineOperationsWorkspace } from "../online-operations";
import { OnlinePlatformCredentialsWorkspace } from "../online-platform-credentials";
import { OnlineProblemsTab } from "../online-problems";
import { OnlineStoreStatusPanel } from "../online-store-status";
import {
  OnlineHubApiError,
  canSeeOnlineTab,
  channelNeedsAttention,
  channelStatusText,
  loadChannelHealth,
  type OnlineChannelHealth,
  type OnlineHubTab,
} from "./onlineHubApi";
import "./online-hub.css";

const REFRESH_MS = 60_000;

const tabLabels: Record<OnlineHubTab, string> = { orders: "Siparişler", menu: "Menü", problems: "Sorunlar", settings: "Ayarlar" };
const tabOrder: readonly OnlineHubTab[] = ["orders", "menu", "problems", "settings"];

/**
 * V12-OUI-004: online food in one screen. The platform status line is on top; the tabs below hold the order queue and,
 * by the session's capabilities, the menu and settings (a manager's) and the problems (V12-OUI-006). A tab a session may
 * not see is neither shown nor loaded. Settings opens with the restaurant's open/closed switch per platform (V12-ONL-011).
 */
export function OnlineFoodHub({
  terminalId,
  tab,
  capabilities,
  onSelectTab,
}: {
  terminalId: string;
  tab: OnlineHubTab;
  capabilities: readonly string[];
  onSelectTab: (tab: OnlineHubTab) => void;
}) {
  const granted = new Set(capabilities);
  const canManage = canSeeOnlineTab("settings", granted);
  const tabs = tabOrder.filter((option) => canSeeOnlineTab(option, granted));
  const active = tabs.includes(tab) ? tab : "orders";
  const panelId = useId();

  return (
    <div className="online-hub">
      <ChannelStatus terminalId={terminalId} canManage={canManage} onOpenSettings={() => onSelectTab("settings")} />
      <div className="online-hub__tabs" role="tablist" aria-label="Online yemek bölümleri">
        {tabs.map((option) => (
          <button
            key={option}
            type="button"
            role="tab"
            aria-selected={active === option}
            aria-controls={panelId}
            className="online-hub__tab"
            onClick={() => onSelectTab(option)}
          >
            {tabLabels[option]}
          </button>
        ))}
      </div>
      <div id={panelId} role="tabpanel" aria-label={tabLabels[active]}>
        {active === "orders" && <OnlineOperationsWorkspace terminalId={terminalId} />}
        {active === "menu" && <OnlineMenuTab terminalId={terminalId} />}
        {active === "problems" && <OnlineProblemsTab terminalId={terminalId} canAct={granted.has("reconciliation.manage")} />}
        {active === "settings" && (
          <>
            <OnlineStoreStatusPanel terminalId={terminalId} />
            <OnlinePlatformCredentialsWorkspace terminalId={terminalId} />
          </>
        )}
      </div>
    </div>
  );
}

function ChannelStatus({ terminalId, canManage, onOpenSettings }: { terminalId: string; canManage: boolean; onOpenSettings: () => void }) {
  const [channels, setChannels] = useState<OnlineChannelHealth[] | null>(null);
  const [error, setError] = useState<string>();

  const load = useCallback(async () => {
    try {
      setChannels(await loadChannelHealth(terminalId));
      setError(undefined);
    } catch (reason) {
      setError(reason instanceof OnlineHubApiError ? reason.message : "Platform durumu okunamadı.");
    }
  }, [terminalId]);

  useEffect(() => {
    void load();
    const timer = window.setInterval(() => void load(), REFRESH_MS);
    return () => window.clearInterval(timer);
  }, [load]);

  return (
    <section className="online-hub__status" aria-label="Platform bağlantıları">
      {error && <p className="online-hub__status-error" role="alert">{error}</p>}
      {channels?.map((channel) => (
        <p
          key={channel.provider}
          className={`online-hub__channel${channelNeedsAttention(channel) ? " online-hub__channel--warning" : ""}`}
        >
          <strong>{channel.displayName}</strong>
          <span>{channelStatusText(channel)}</span>
          {!channel.configured && canManage && (
            <button type="button" className="online-hub__link" onClick={onOpenSettings}>Ayarlara git</button>
          )}
        </p>
      ))}
    </section>
  );
}
