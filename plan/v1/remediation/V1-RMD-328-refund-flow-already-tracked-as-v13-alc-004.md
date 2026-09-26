# V1-RMD-328 - İade (refund) akışının çalışmaması: yeni bir bulgu değil, zaten V13-ALC-004'te izlenen bilinen bir açık

- Task ID: V1-RMD-328
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

Bağımsız 13 ajanlı derin denetimin (2026-09-26) K2 bulgusu: `docs/domain/refund-ledger.md`'nin (V0-DOM-003, Semih'in onayladığı bir karar belgesi) tanımladığı `payment_reversals` tablosu kodda hiç yok; `Payment.CanTransitionTo` `Refunded`/`PartiallyRefunded`'ı desteklemiyor; onaylanmış bir iade talebi asla fiilen para geri döndürmüyor, sonsuza kadar `Pending` kalıyor.

Bu görev, bunun YENİ, ele alınmamış bir bulgu OLMADIĞINI doğruluyor: kod tabanının kendisi bunu zaten açıkça, isimlendirerek belgeliyor ve izliyor.

## Owned surface

- `plan/v1/remediation/V1-RMD-328-refund-flow-already-tracked-as-v13-alc-004.md`
- Kod değişikliği yok.

## In scope

1. Doğrulama: `Payment.CanTransitionTo`'nun kendi belge yorumu ("Refunded/PartiallyRefunded are never reachable through this aggregate — out of scope, see V0-DOM-003/V13-ALC-003/V13-ALC-004") gerçekten kodda böyle mi, yoksa bir yazım hatası mı — gerçekten böyle.
2. Doğrulama: `RefundIntentFactory.cs`'in kendi yorumu ("no payment_reversals ledger exists yet — V13-ALC-004's own table") gerçek mi — gerçek, `payment_reversals` tablosu için sıfır migration/kod sonucu (grep ile doğrulandı).
3. Doğrulama: `plan/v1.3/payment-allocation/V13-ALC-004-refund-finalization.md` — Status: `Planned`, `plan/GATES.md`'in kendi bekleyen görevler tablosunda (`V13-ALC-004 | 2026-09-22 | V13-HUG-003 | Gerçek Token/Beko provider contract/erişim kanıtı`) zaten kayıtlı — tam bu bulgunun kapsadığı işi, aynı gerçek Token/Beko terminal bağımlılık zincirine bağlı olarak.

## Out of scope

- `V13-ALC-004`'ün kendisinin uygulanması — `V13-HUG-003`'e (gerçek Token/Beko provider transport) bağımlı, bu oturumun `v1.3-and-earlier external dependencies` zincirinin bir parçası; gerçek bir terminal erişimi/sözleşme kanıtı olmadan sahte bir "finalization" uygulamak, görevin kendi Acceptance evidence'ının gerektirdiği gerçek crash-resume/idempotency/fiscal-handoff davranışını asla doğru şekilde test edemez — bu, bu oturumun tekrar tekrar karşılaştığı ve sahte bir çözümle kapatmayı reddettiği tam olarak aynı sınıf bağımlılık.

## Dependencies

- None

## Acceptance evidence

Kod tabanının kendisi bu açığı zaten isimlendiriyor ve izliyor:

- `grep -rn "payment_reversals"` kod tabanında sıfır tablo/migration sonucu döndürüyor — `RefundIntentFactory.cs`'in kendi yorumu bunu doğruluyor.
- `Payment.cs:136-140`'ın kendi belge yorumu: "Refunded/PartiallyRefunded are never reachable through this aggregate — out of scope, see V0-DOM-003/V13-ALC-003/V13-ALC-004."
- `plan/v1.3/payment-allocation/V13-ALC-004-refund-finalization.md`: `Status: Planned`, bağımlılıkları `V13-HUG-003` (gerçek Token/Beko transport), `V13-FSC-001` (mali), `V13-MCD-003` (yemek kartı adaptörü) — hiçbiri henüz gerçek bir dış erişime sahip değil.
- `plan/GATES.md:207`: `V13-ALC-004` zaten `V0_DEFERRED_TASKS`-benzeri bekleyen görevler tablosunda, "Gerçek Token/Beko provider contract/erişim kanıtı" gerekçesiyle kayıtlı.

Sonuç: K2, bu oturumun `v1.3-and-earlier external dependencies` zincirinin (Token/Beko) ZATEN kapsadığı, isimlendirilmiş, gerekçelendirilmiş bir açığın bağımsız denetim tarafından yeniden bulunmasıdır — yeni kod, sahte bir uygulama veya "tümünü düzelt" direktifi altında bu gerçek dış bağımlılığı atlayan bir kısayol gerektirmez. Bu görev, denetim raporunun bu bulgusunun gözden kaçırılmadığını, araştırıldığını ve doğru şekilde zaten var olan izleme mekanizmasına bağlandığını kayda geçirir.

## Handoff

- V13-ALC-004
