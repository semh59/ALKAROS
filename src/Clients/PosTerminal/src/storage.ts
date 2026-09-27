// V1-RMD-350 (independent 2026-09-26 audit, a low-severity finding): every
// localStorage call here used to be unguarded - a private tab, a disabled
// storage setting, or a full quota would throw and crash whatever screen
// called savedId() (nearly every PosTerminal route calls it once at mount,
// via useState(() => savedId(...))), instead of just failing to persist.
export function tryGetItem(key: string): string | null {
  try {
    return localStorage.getItem(key);
  } catch {
    return null;
  }
}

export function trySetItem(key: string, value: string): void {
  try {
    localStorage.setItem(key, value);
  } catch {
    // The value simply will not survive a reload in this session; that is
    // strictly safer than crashing the screen that tried to save it.
  }
}

export function savedId(key: string): string {
  let existing: string | null = null;
  try {
    existing = localStorage.getItem(key);
  } catch {
    existing = null;
  }
  if (existing) return existing;
  const created = crypto.randomUUID();
  trySetItem(key, created);
  return created;
}
