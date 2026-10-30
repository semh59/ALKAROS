# V1-RMD-758 - Kasa hareketi anahtarı yalnız sunucunun karşılaştırdığı alanlara bağlanır

- Task ID: V1-RMD-758
- Status: Planned
- Assignee: claude-code-session_01FMN5WVXi1HJCTEk5argkgZ
- Work type: remediation
- Surface state: Existing

## Goal

V1-RMD-756 sonrası denetim bulgusu: `submitCashMovement` anahtar imzasına `Notes` alanını da katıyor (`cash-session.js`). Sunucu tekrar isteğinde yalnız tutar ve yönü karşılaştırır (`DualScreenApplication.CashSession.cs` `MovementReplay`). İlk istek ağ hatasıyla sunucuya ulaşmış olsa bile kasiyer yalnız açıklamayı düzeltip yeniden gönderirse yeni anahtar üretilir ve aynı nakit hareketi ikinci kez yazılır.

Anahtar imzası yalnız sunucunun ayırt ettiği alanlardan (`Direction`, `Amount`) oluşur. Açıklama değişimi aynı anahtarı korur; sunucu zaten kaydedilmiş hareketi döndürür.

## Owned surface

- `plan/v1/remediation/V1-RMD-758-kasa-hareketi-anahtari-yalniz-tutar-yon.md`
- `evidence/V1-RMD-758/**`
- Sınırlı ek: src/Clients/Cashier/wwwroot/payments/cash-session/cash-session.js
- Sınırlı ek: tests/E2E/Cashier/specs/05-cash-session-lifecycle.spec.js

## In scope

- `submitCashMovement`: imza yalnız `Direction` ve `Amount` ile kurulur; `Notes` imzaya girmez, gövdede gönderilmeye devam eder.
- E2E testi: ağ hatasından sonra yalnız açıklaması değişen hareket aynı anahtarı gönderir; tutarı değişen yeni anahtarı gönderir.

## Out of scope

- Sunucu tarafı idempotency davranışı ve `MovementReplay` karşılaştırması.
- İndirim ve bahşiş anahtarları (sunucu karşılaştırması gövdenin tamamına bağlı; ayrı doğrulama gerekir).
- A→B→A tek yuvalı imza durumu.
- V1-RMD-750 ve plan düzeltmeleri.

## Dependencies

- V1-RMD-756

## Acceptance evidence

1. Düzeltmeden önce yeni E2E adımı (yalnız not değişimi aynı anahtar) kırmızı olur; çıktı kaydedilir.
2. Düzeltmeden sonra `05-cash-session-lifecycle.spec.js` gerçek Chromium + Host + PostgreSQL ortamında exit 0 geçer.
3. Mutasyon: imzaya `Notes` geri eklendiğinde test kırmızı olur; dosya hash'i geri yüklenir.
4. Kapsam aracı InProgress baseline'a karşı exit 0 verir; iki bağımsız incelemede artık bulgu kalmaz.

## Handoff

- None
