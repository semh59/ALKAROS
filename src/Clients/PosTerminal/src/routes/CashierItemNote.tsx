import { focusOnMount } from "../focusOnMount";
import { itemNoteText } from "../strings";

/** `null` is the closed state; a string (even empty) is the open field. The note goes with the next product the cashier taps. */
export function CashierItemNote({ note, onChange }: { note: string | null; onChange: (note: string | null) => void }) {
  if (note === null) {
    return (
      <div className="item-note">
        <button data-telemetry-id="b.routes.cashieritemnote.open" className="secondary" aria-expanded={false} onClick={() => onChange("")}>{itemNoteText.open}</button>
      </div>
    );
  }
  return (
    <div className="item-note" role="group" aria-label={itemNoteText.group}>
      <div className="item-note__row">
        <input
          ref={focusOnMount}
          value={note}
          maxLength={200}
          placeholder={itemNoteText.placeholder}
          aria-label={itemNoteText.input}
          onChange={(event) => onChange(event.target.value)}
        />
        <button data-telemetry-id="b.routes.cashieritemnote.close" className="secondary" onClick={() => onChange(null)}>{itemNoteText.close}</button>
      </div>
      <small>{itemNoteText.hint}</small>
    </div>
  );
}
