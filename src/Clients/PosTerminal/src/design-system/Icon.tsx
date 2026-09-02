import "./icon.css";

/**
 * The single icon set for ALKAROS. Line icons on a 24x24 grid, stroked with
 * `currentColor` so they inherit text colour and scale with `1em`. This
 * replaces the three ad-hoc strategies the deep-analysis review flagged:
 * PosTerminal typographic glyphs, vanilla-client emoji, and the unimplemented
 * `lucide:*` names in DESIGN.md (finding F-8). The vanilla clients mirror this
 * set as an inline `<svg>` sprite in their index.html - keep the two in sync.
 */
export type IconName =
  | "sales"
  | "tables"
  | "billing"
  | "kitchen"
  | "catalog"
  | "system"
  | "check"
  | "refresh"
  | "offline"
  | "clock"
  | "info"
  | "warning"
  | "close"
  | "conflict"
  | "lock"
  | "forbidden"
  | "brand"
  | "user"
  | "register"
  | "add"
  | "send"
  | "trash"
  | "pause"
  | "recall"
  | "gift"
  | "search";

const paths: Record<IconName, string> = {
  sales:
    "M2 8h20M2 8v9a1 1 0 0 0 1 1h18a1 1 0 0 0 1-1V8M2 8l2-4h16l2 4M9 13h6",
  tables:
    "M4 4h6v6H4zM14 4h6v6h-6zM14 14h6v6h-6zM4 14h6v6H4z",
  billing: "M12 5h.01M5 12h14M12 19h.01",
  kitchen:
    "M6 17h12M17 21H7a1 1 0 0 1-1-1v-6a4 4 0 0 1-1.6-7.6A5 5 0 0 1 13.6 4.4 4 4 0 0 1 19 14v6a1 1 0 0 1-1 1Z",
  catalog: "M4 6h16M4 12h16M4 18h16",
  system: "M2 12h4l3 8 4-16 3 8h4",
  check: "M20 6 9 17l-5-5",
  refresh: "M21 12a9 9 0 1 1-3-6.7M21 4v5h-5",
  offline: "m2 2 20 20M8.5 16.4a5 5 0 0 1 7 0M5 12.9a10 10 0 0 1 4-2.6M19 12.9a10 10 0 0 0-2-1.5M12 20h.01",
  clock: "M12 7v5l3 2M12 22a10 10 0 1 1 0-20 10 10 0 0 1 0 20Z",
  info: "M12 22a10 10 0 1 1 0-20 10 10 0 0 1 0 20ZM12 16v-4M12 8h.01",
  warning: "M10.3 4 2.6 18a2 2 0 0 0 1.7 3h15.4a2 2 0 0 0 1.7-3L13.7 4a2 2 0 0 0-3.4 0ZM12 9v4M12 17h.01",
  close: "M18 6 6 18M6 6l12 12",
  conflict: "M8 3 4 7l4 4M4 7h16M16 21l4-4-4-4M20 17H4",
  lock: "M5 11h14a1 1 0 0 1 1 1v8a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1v-8a1 1 0 0 1 1-1ZM8 11V7a4 4 0 0 1 8 0v4",
  forbidden: "M12 22a10 10 0 1 1 0-20 10 10 0 0 1 0 20ZM5 5l14 14",
  brand: "M13 2 4 14h7l-1 8 9-12h-7l1-8Z",
  user: "M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2M12 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8Z",
  register: "M4 9 5 4h14l1 5M4 9v11h16V9M9 20v-6h6v6M4 9h16",
  add: "M5 12h14M12 5v14",
  send: "m22 2-7 20-4-9-9-4 20-7Z",
  trash: "M3 6h18M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2m3 0v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6M10 11v6M14 11v6",
  pause: "M6 4h3v16H6zM15 4h3v16h-3z",
  recall:
    "M6 14 7.5 11.1A2 2 0 0 1 9.3 10H20a2 2 0 0 1 1.9 2.5l-1.5 6A2 2 0 0 1 18.5 20H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h3.9a2 2 0 0 1 1.7.9l.8 1.2a2 2 0 0 0 1.7.9H18a2 2 0 0 1 2 2v2",
  gift: "M20 12v8a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1v-8M2 8h20v4H2zM12 8v13M12 8 8.5 4.5a2.1 2.1 0 0 1 3-3L12 8l.5-6.5a2.1 2.1 0 0 1 3 3Z",
  search: "M21 21l-4.3-4.3M11 19a8 8 0 1 0 0-16 8 8 0 0 0 0 16Z",
};

export interface IconProps {
  name: IconName;
  /** Accessible label. When omitted the icon is decorative (aria-hidden). */
  label?: string;
  className?: string;
}

export function Icon({ name, label, className }: IconProps) {
  return (
    <svg
      className={className ? `ds-icon ${className}` : "ds-icon"}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={2}
      strokeLinecap="round"
      strokeLinejoin="round"
      role={label ? "img" : undefined}
      aria-label={label}
      aria-hidden={label ? undefined : true}
      focusable="false"
    >
      <path d={paths[name]} />
    </svg>
  );
}
