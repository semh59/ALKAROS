# V1-RMD-992 Reviews

## qa-backend
Window, event name, partitioned index use, DI, permission and defaults checked clean. Open: the count is computed live, not stored, so events written after an early close but inside the same window still change it (the audit trail is append-only; the other day totals are stored at close); the event name text is repeated in the counter and in the producing store (a shared constant is not added).

## ui-turkce-metin-uzmani
Applied: label "Mutfağa gitmeden eklenen kalem" (same unit "kalem" as cancelled items, matches the till wording).
Not done: optional hint text for the manager; style guide glossary entry.
