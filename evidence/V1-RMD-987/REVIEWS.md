# V1-RMD-987 Reviews

## qa-backend
Server accepts any person id, save/payment are not affected; saved people come back from the owner list, but the server's "N. Kişi" numbering depends on write order (unstable). Applied: the screen now numbers people by position itself so labels are unique.
Open: an added person who got no share is not saved and disappears after a reload (expected, now stated here).

## qa-frontend
Applied: unique labels (finding 1), added person selected at once in the equal split, double tap safe (functional update), telemetry id format.
Not done: remove button for an added person; crypto.randomUUID needs a secure context (same assumption as api.ts elsewhere); the owner list is not re-read after save (labels are consistent anyway); equal-split heading still says "Sandalye veya kişi seçin".
