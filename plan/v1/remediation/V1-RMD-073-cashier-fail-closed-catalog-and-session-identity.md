# V1-RMD-073 - Cashier fail-closed catalog and session identity

- Task ID: V1-RMD-073
- Status: Done
- Assignee: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-08-31

## Goal

Cashier istemcisinde katalog sunucudan alınamadığında sessizce sabit sahte ürün listesine düşme davranışını kaldırmak ve bu durumu açık bir hata durumu olarak göstererek sipariş göndermeyi engellemek. Ayrıca kasiyer adını sabit metin yerine gerçek oturum bilgisinden almak.

## Owned surface

- `plan/v1/remediation/V1-RMD-073-cashier-fail-closed-catalog-and-session-identity.md`
- PO:2026-08-31 kararıyla src/Clients/Cashier/wwwroot/** yüzeyi V1-RMD-077'ye devredildi; bu historical task closed kalır.

## In scope

- Sabit gömülü `products` ve `categories` yedeğinin kaldırılması; katalog yüklenemediğinde ürün matrisinin hata mesajı göstermesi ve sipariş gönder düğmesinin devre dışı kalması.
- Kasiyer adının `/api/v1/auth/session` yanıtındaki `displayName` değerinden alınması; alınamazsa nötr bir etiket gösterilmesi ve `Kasiyer Zeynep` sabit metninin kaldırılması.
- `index.html` içindeki sabit kasiyer adı etiketinin nötr başlangıç metniyle değiştirilmesi.

## Out of scope

- Terminal kimliğinin cihaz eşleştirme oturumundan türetilmesi; bu ayrı bir sonraki dalga görevine aittir.
- Host tarafındaki oturum veya katalog uç noktalarını değiştirmek.

## Dependencies

- V1-RMD-072

## Deliverables

- Katalog alınamadığında güvenli biçimde kapanan ve gerçek oturum adını gösteren Cashier istemcisi.

## Acceptance evidence

- `python -m pytest tests/Architecture` sıfır hata verir.
- Semih; katalog uç noktası kapalıyken Cashier ekranında sahte ürün görünmediğini, hata mesajı çıktığını ve sipariş gönderilemediğini doğrular.

## Handoff

- V1-GOV-039
