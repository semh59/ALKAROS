# V1-RMD-1067 RESULT

Masa paneli (.table-order-bridge) satirlari arasi .8rem -> 1rem (16px). Tahsilat sayfasi: .sp-method-chips 8 -> 16px, .sp-row 10 -> 16px, .sp-quick-cash (iki bildirim) 8 -> 16px, .sp-customer-list 6 -> 16px (qa-ux bulgusu). Testler: TableOrderBridge.test.tsx (.table-order-bridge eklendi), split-payment-cashier-rules.test.js (uc secici).
Kanit: red.log (4 kirmizi), green-file.log, green-all.log (PosTerminal), green-static.log (StaticApps), client-build-gate.log exit 0, mutations.txt (10 killed).
Olculemedi: 16px aralikla 7 sutunlu hizli nakit izgarasinin 640-768px genislikte tek satirda sigmasi (hesap: dugme ~64px) gercek ekranda dogrulanmadi.
