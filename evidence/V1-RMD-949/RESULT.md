# V1-RMD-949 Result

Status: Done.

- Creating a stock item or location with a missing or invalid field answers 400 with the field named in Turkish (Kod, Ad, Takip birimi, Stok türü, Konum türü, Birim tanınmıyor, Yeniden sipariş noktası); the screen already shows the server sentence.
- Evidence: red.log (7 red), green.log and full-server.log (Inventory project, 35 tests green), mutations.txt (5 mutants killed).
