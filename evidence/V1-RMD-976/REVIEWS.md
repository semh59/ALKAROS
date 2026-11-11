# V1-RMD-976 Reviews

- qa-backend: no other List<Guid>.FirstOrDefault() pointer writer in Tables, Billing, Orders; other modules not scanned.
- qa-veritabani: no blocker; no writer found that violates the new constraint; direct UPDATEs with Guid.Empty would now fail loudly instead of corrupting silently.
