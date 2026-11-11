# V1-RMD-983 Reviews

## qa-frontend
Found: mode stayed on after the order changed or closed (High); button shown on unsent drafts, which the server would turn into orphan lines (Medium); unstyled buttons (Medium); thin tests (Medium).
Applied: reset effect on order id/editable change; button gated on non-editable order; secondary class; tests for cancel and draft gate (mutations M4, M5).
Not done: quantity fixed at 1 (design choice); disabling option products while in the mode; tests for 409 and double tap (existing execute/busy pattern is shared with other add paths).

## ui-turkce-metin-uzmani
Applied: reason prompt "Mutfağa neden gönderilmiyor?", banner text, "Normal eklemeye dön", "Vazgeç", clearer options message, aria-labelledby, aria-expanded.
Kept: button label and reason labels as decided by the product owner.
