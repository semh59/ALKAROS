import {
  forwardRef,
  useEffect,
  useId,
  useRef,
  type ButtonHTMLAttributes,
  type InputHTMLAttributes,
  type KeyboardEvent,
  type ReactNode,
  type SelectHTMLAttributes,
} from "react";
import "./primitives.css";
import { Icon, type IconName } from "./Icon";

const focusableSelector = [
  "a[href]",
  "button:not([disabled])",
  "input:not([disabled])",
  "select:not([disabled])",
  "textarea:not([disabled])",
  "[tabindex]:not([tabindex='-1'])",
].join(",");

export type ButtonVariant = "primary" | "secondary" | "quiet";

export const Button = forwardRef<HTMLButtonElement, ButtonHTMLAttributes<HTMLButtonElement> & { variant?: ButtonVariant }>(
  function Button({ variant = "primary", className = "", type = "button", ...props }, ref) {
    return <button ref={ref} type={type} className={`ds-button ds-button--${variant} ${className}`.trim()} {...props} />;
  },
);

type FieldProps = Omit<InputHTMLAttributes<HTMLInputElement>, "id"> & {
  id?: string;
  label: string;
  hint?: string;
  error?: string;
};

export function TextField({ id, label, hint, error, className = "", ...props }: FieldProps) {
  const generatedId = useId();
  const fieldId = id ?? generatedId;
  const hintId = hint ? `${fieldId}-hint` : undefined;
  const errorId = error ? `${fieldId}-error` : undefined;
  const describedBy = [hintId, errorId].filter(Boolean).join(" ") || undefined;
  return (
    <label className="ds-field" htmlFor={fieldId}>
      <span className="ds-field__label">{label}</span>
      {hint && <span id={hintId} className="ds-field__hint">{hint}</span>}
      <input id={fieldId} className={`ds-input ${className}`.trim()} aria-invalid={error ? true : undefined} aria-describedby={describedBy} {...props} />
      {error && <span id={errorId} className="ds-field__error">{error}</span>}
    </label>
  );
}

type SelectFieldProps = Omit<SelectHTMLAttributes<HTMLSelectElement>, "id"> & {
  id?: string;
  label: string;
  hint?: string;
  error?: string;
  children: ReactNode;
};

export function SelectField({ id, label, hint, error, className = "", children, ...props }: SelectFieldProps) {
  const generatedId = useId();
  const fieldId = id ?? generatedId;
  const hintId = hint ? `${fieldId}-hint` : undefined;
  const errorId = error ? `${fieldId}-error` : undefined;
  const describedBy = [hintId, errorId].filter(Boolean).join(" ") || undefined;
  return (
    <label className="ds-field" htmlFor={fieldId}>
      <span className="ds-field__label">{label}</span>
      {hint && <span id={hintId} className="ds-field__hint">{hint}</span>}
      <select id={fieldId} className={`ds-select ${className}`.trim()} aria-invalid={error ? true : undefined} aria-describedby={describedBy} {...props}>{children}</select>
      {error && <span id={errorId} className="ds-field__error">{error}</span>}
    </label>
  );
}

export function ValidationSummary({ title, errors }: { title: string; errors: readonly string[] }) {
  if (errors.length === 0) return null;
  return (
    <div className="ds-validation-summary" role="alert">
      <span className="ds-validation-summary__icon" aria-hidden="true"><Icon name="warning" /></span>
      <div><strong>{title}</strong><ul>{errors.map((error) => <li key={error}>{error}</li>)}</ul></div>
    </div>
  );
}

export type StateTone = "info" | "success" | "warning" | "error" | "offline" | "stale" | "conflict" | "unauthorized" | "forbidden";
const stateIcons: Record<StateTone, IconName> = {
  info: "info", success: "check", warning: "warning", error: "close", offline: "offline", stale: "clock", conflict: "conflict", unauthorized: "lock", forbidden: "forbidden",
};

export function StateMessage({ tone, title, children, actions }: { tone: StateTone; title: string; children?: ReactNode; actions?: ReactNode }) {
  const urgent = tone === "error" || tone === "offline" || tone === "unauthorized" || tone === "forbidden";
  return (
    <section className={`ds-state-message ds-state-message--${tone}`} role={urgent ? "alert" : "status"} aria-live={urgent ? "assertive" : "polite"}>
      <span className="ds-state-message__symbol" aria-hidden="true"><Icon name={stateIcons[tone]} /></span>
      <div className="ds-state-message__content">
        <strong>{title}</strong>
        {children}
        {actions && <div className="ds-state-message__actions">{actions}</div>}
      </div>
    </section>
  );
}

function trapFocus(event: KeyboardEvent<HTMLElement>) {
  if (event.key !== "Tab") return;
  const controls = [...event.currentTarget.querySelectorAll<HTMLElement>(focusableSelector)];
  if (controls.length === 0) return;
  const first = controls[0];
  const last = controls[controls.length - 1];
  if (event.shiftKey && document.activeElement === first) {
    event.preventDefault();
    last.focus();
  } else if (!event.shiftKey && document.activeElement === last) {
    event.preventDefault();
    first.focus();
  }
}

export function ModalDialog({ open, title, onClose, children, initialFocusRef }: { open: boolean; title: string; onClose: () => void; children: ReactNode; initialFocusRef?: React.RefObject<HTMLElement | null> }) {
  const titleId = useId();
  const dialogRef = useRef<HTMLDivElement>(null);
  const returnFocusRef = useRef<HTMLElement | null>(null);

  useEffect(() => {
    if (!open) return;
    returnFocusRef.current = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    const first = initialFocusRef?.current ?? dialogRef.current?.querySelector<HTMLElement>(focusableSelector);
    first?.focus();
    return () => returnFocusRef.current?.focus();
  }, [initialFocusRef, open]);

  if (!open) return null;
  return (
    <div className="ds-dialog-backdrop">
      <div ref={dialogRef} className="ds-dialog" role="dialog" aria-modal="true" aria-labelledby={titleId} onKeyDown={(event) => {
        if (event.key === "Escape") { event.preventDefault(); onClose(); return; }
        trapFocus(event);
      }}>
        <header className="ds-dialog__header"><h2 id={titleId} className="ds-dialog__title">{title}</h2><button type="button" className="ds-dialog__close" onClick={onClose} aria-label="Pencereyi kapat">×</button></header>
        {children}
      </div>
    </div>
  );
}

export function ContextDrawer({ presentation, title, onClose, children }: { presentation: "persistent" | "sheet"; title: string; onClose?: () => void; children: ReactNode }) {
  const titleId = useId();
  const drawerRef = useRef<HTMLElement>(null);

  useEffect(() => {
    if (presentation !== "sheet") return;
    const returnFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    drawerRef.current?.querySelector<HTMLElement>(focusableSelector)?.focus();
    return () => returnFocus?.focus();
  }, [presentation]);

  return (
    <aside ref={drawerRef} className={`ds-drawer ds-drawer--${presentation}`} aria-labelledby={titleId} aria-modal={presentation === "sheet" ? true : undefined} role={presentation === "sheet" ? "dialog" : undefined} onKeyDown={presentation === "sheet" ? (event) => {
      if (event.key === "Escape" && onClose) { event.preventDefault(); onClose(); return; }
      trapFocus(event);
    } : undefined}>
      <header className="ds-drawer__header">
        <h2 id={titleId} className="ds-drawer__title">{title}</h2>
        {presentation === "sheet" && onClose && <button type="button" className="ds-drawer__close" onClick={onClose} aria-label="Bağlam panelini kapat">×</button>}
      </header>
      <div>{children}</div>
    </aside>
  );
}
