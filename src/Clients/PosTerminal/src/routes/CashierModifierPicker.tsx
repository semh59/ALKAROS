import { useEffect, useRef, useState, type KeyboardEvent } from "react";
import type { CatalogModifierGroup, CatalogProduct } from "../contracts";
import { formatMoney } from "../format";
import "./cashier-modifier-picker.css";

const focusableSelector = "button:not([disabled]),input:not([disabled])";

const isSatisfied = (group: CatalogModifierGroup, chosen: Set<string>) => {
  const count = group.modifiers.filter((modifier) => chosen.has(modifier.modifierId)).length;
  return count >= group.minSelections && count <= group.maxSelections;
};

export function CashierModifierPicker({ product, onConfirm, onCancel }: {
  product: CatalogProduct;
  onConfirm: (modifierIds: string[]) => void;
  onCancel: () => void;
}) {
  const groups = product.modifierGroups ?? [];
  const [chosen, setChosen] = useState<Set<string>>(new Set());
  const dialog = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const opener = document.activeElement as HTMLElement | null;
    dialog.current?.querySelector<HTMLElement>(focusableSelector)?.focus();
    return () => opener?.focus();
  }, []);

  const toggle = (group: CatalogModifierGroup, modifierId: string) =>
    setChosen((current) => {
      const next = new Set(current);
      if (next.has(modifierId)) {
        next.delete(modifierId);
      } else {
        if (group.maxSelections === 1) group.modifiers.forEach((modifier) => next.delete(modifier.modifierId));
        next.add(modifierId);
      }
      return next;
    });

  const trapFocus = (event: KeyboardEvent<HTMLDivElement>) => {
    if (event.key === "Escape") {
      event.preventDefault();
      onCancel();
      return;
    }
    if (event.key !== "Tab") return;
    const controls = [...event.currentTarget.querySelectorAll<HTMLElement>(focusableSelector)];
    const first = controls[0];
    const last = controls.at(-1);
    if (!first || !last) return;
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  };

  return (
    <div className="dialog-backdrop" role="presentation" onKeyDown={trapFocus} onMouseDown={(event) => {
      if (event.target === event.currentTarget) onCancel();
    }}>
      <div className="pairing-dialog" ref={dialog} role="dialog" aria-modal="true" aria-labelledby="modifier-picker-title">
        <div className="dialog-heading">
          <div>
            <span className="eyebrow dark">SEÇENEKLER</span>
            <h2 id="modifier-picker-title">{product.name}</h2>
          </div>
          <button data-telemetry-id="b.routes.cashiermodifierpicker.1" className="dialog-close" onClick={onCancel} aria-label="Seçenek penceresini kapat">×</button>
        </div>
        {groups.map((group) => (
          <fieldset className="modifier-group" key={group.modifierGroupId}>
            <legend>
              {group.name}
              {group.minSelections > 0 ? " (zorunlu)" : " (isteğe bağlı)"}
              {group.maxSelections > 1 ? ` · en fazla ${group.maxSelections}` : ""}
            </legend>
            {group.modifiers.map((modifier) => (
              <label key={modifier.modifierId}>
                <input
                  type={group.maxSelections === 1 && group.minSelections > 0 ? "radio" : "checkbox"}
                  name={group.modifierGroupId}
                  checked={chosen.has(modifier.modifierId)}
                  onChange={() => toggle(group, modifier.modifierId)}
                />
                {modifier.name}
                {modifier.priceDelta > 0 ? ` +${formatMoney(modifier.priceDelta)}` : ""}
              </label>
            ))}
          </fieldset>
        ))}
        <div className="dialog-actions">
          <button data-telemetry-id="b.routes.cashiermodifierpicker.2" className="secondary" onClick={onCancel}>Vazgeç</button>
          <button data-telemetry-id="b.routes.cashiermodifierpicker.3"
            className="primary"
            disabled={!groups.every((group) => isSatisfied(group, chosen))}
            onClick={() => onConfirm([...chosen])}
          >
            Siparişe ekle
          </button>
        </div>
      </div>
    </div>
  );
}
