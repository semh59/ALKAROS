import type { SettingHistoryEntry, SettingRecord } from "./models";
import { createRequester } from "../management/http";

export interface SettingsClient {
  listSettings: () => Promise<readonly SettingRecord[]>;
  history: (key: string) => Promise<readonly SettingHistoryEntry[]>;
}

export function createSettingsClient(fetcher: typeof fetch = fetch): SettingsClient {
  const call = createRequester(fetcher, { prefix: "/settings" });
  const json = async <T>(path: string): Promise<T> => (await call(path)).json() as Promise<T>;
  return {
    listSettings: () => json("/"),
    history: (key) => json(`/${encodeURIComponent(key)}/history`),
  };
}
