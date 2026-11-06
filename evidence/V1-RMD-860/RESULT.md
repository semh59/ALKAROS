# V1-RMD-860 sonuç

Yalnız belge değişti; kod ve migration değişmedi.

| Belge | Düzeltme | Kod kanıtı |
| --- | --- | --- |
| docs/domain/bill-order-cardinality.md | bileşik benzersizlik (bill_id, bill_item_id); order_item_id benzersizliği yalnız superseded_at boş satırlarda | database/migrations/V1/V1-BIL-004/032-*.up.sql:8-11, V1/V1-RMD-506/180-bill-cancellation.up.sql:12-14 |
| docs/engineering/kasa-faz0-redesign-requirements.md | V1-CUI-008 ve V1-CUI-009 Planned -> Done | plan/v1/cashier-ui/V1-CUI-008-*.md:4, V1-CUI-009-*.md:4 |
| docs/domain/authorization-model.md | "sabit indirim merdiveni yok" cümlesi, aynı belgenin başlangıç limiti (100 TL, 8 saatte 10) kuralıyla hizalandı | belgenin kendi satırları 81 ve 154 (2026-10-21 ürün sahibi kararı, migration 196) |
| tests/E2E/Cashier/README.md | dört senaryo ile başladı, dizinde 32 spec | tests/E2E/Cashier/specs (32 dosya) |
| plan/GATES.md:273 | "bu 8 görev" -> tablodaki 4 tüketici görev | plan/GATES.md:253-266 tablo (4 tüketici, 12 çift); plan-audit sabitine dokunulmadı |

Eski Done görev md ve kanıt kayıtlarının güncel karşılıkları HISTORICAL.md içindedir; o dosyalar değiştirilmedi.

Açık kalan: F0082 (Billing csproj'da paket referansı yok, repository Npgsql kullanıyor) bu turda doğrulanmadı; F0915 ok tuşu gezinmesi V1-RMD-862.
