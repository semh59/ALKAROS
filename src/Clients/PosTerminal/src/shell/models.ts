export type ViewportMode = "wide" | "compact" | "mobile";

export function classifyViewport(width: number): ViewportMode {
  if (width >= 1280) return "wide";
  if (width >= 768) return "compact";
  return "mobile";
}

export interface ShellIdentity {
  /** Omitted until a real multi-branch model exists (finding F-5). */
  branchName?: string;
  terminalName: string;
  userName: string;
  roleLabel: string;
  capabilities: ReadonlySet<string>;
}

export type ShellSession =
  | { status: "authenticated"; identity: ShellIdentity }
  | { status: "unauthenticated"; reason: "missing" | "expired"; onSignIn: () => void };

export type RouteAuthorization =
  | { status: "authorized" }
  | { status: "forbidden"; onReturn?: () => void };

export type Connectivity =
  | { status: "online" }
  | { status: "reconnecting" }
  | { status: "offline"; onRetry?: () => void };

export type Freshness =
  | { status: "fresh"; dateTime: string; label: string }
  | { status: "stale"; dateTime: string; label: string; onRefresh: () => void };

export interface ShellNavigationItem {
  id: string;
  label: string;
  href: string;
  symbol: string;
  requiredCapability: string;
}

export function allowedNavigation(items: readonly ShellNavigationItem[], capabilities: ReadonlySet<string>) {
  return items.filter((item) => capabilities.has(item.requiredCapability));
}

