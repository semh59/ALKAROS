# V1-RMD-424 - Mutfağa gitmiş kalemin void'inde stok iadesinin sipariş kaydıyla tek işlemde yapılması

- Task ID: V1-RMD-424
- Status: Done
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-28

## Goal

V1-RMD-398 stok/reçete denetimi G-12 (Düşük): mutfağa gitmiş ama hazırlanmaya başlanmamış (`Sent`/`Held`) bir kalemin
void'inde stok iadesi sipariş yazımından ayrı ve "best-effort" idi (`SentItemVoidStore`). İade başarısız olursa kalem
iptal edilmiş olarak kalıyor, stok geri gelmiyor, denetim kaydında `StockRestored=false` kalıyordu. Stok hareketi
tersine çevirme servisinin kendisi de defter kaydı ile bakiye güncellemesini ayrı yazıyordu.

Bu görev: tersine çevirme servisine çağıranın bağlantısı ve işlemiyle çalışan bir yol eklenir (defter kaydı ve bakiye
aynı işlemde); void, sipariş kaydını ve stok iadesini aynı veritabanı işleminde yapar. İade yapılamazsa (ör. stok
kalemi pasif) void Türkçe 409 ile reddedilir ve hiçbir şey değişmez. Mutfak fişi iptali ve hesaptaki fire satırı
bu görevin kapsamı dışında, eskisi gibi ayrı adımlardır.

## Owned surface

- `plan/v1/remediation/V1-RMD-424-void-stock-restore-atomic.md`
- `evidence/V1-RMD-424/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Inventory/MovementReversal/IStockMovementReversalService.cs ve
  src/Modules/Inventory/MovementReversal/StockMovementReversalService.cs (V11-INV-003 sahipliğinde) — yalnız işlem içi
  tersine çevirme yolu
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Orders/SentItemVoid/SentItemVoidStore.cs
  (V1-RMD-101 sahipliğinde) — yalnız sipariş kaydı ile stok iadesinin aynı işleme alınması ve yeni istisna
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Orders/OrderManagementEndpoints.cs (V1-RMD-101
  sahipliğinde) — yalnız yeni istisnanın Türkçe 409 eşlemesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Orders/VoidSent/OrderManagementVoidSentHttpTests.cs
  (V1-RMD-083 sahipliğinde) — yeni test

## In scope

- `Sent`/`Held` void: sipariş kaydı + stok iadesi tek işlem; iade başarısızsa 409 `STOCK_RESTORE_FAILED`, sipariş
  değişmez.

## Out of scope

- Mutfak fişi iptali ve hesaptaki fire dönüşümünün aynı işleme alınması.

## Dependencies

- V1-RMD-423

## Acceptance evidence

- `ALKAROS.Host.Experience.Orders.VoidSent.Tests` 17/17, `ALKAROS.Inventory.MovementReversal.Tests` 16/16 ve
  `ALKAROS.Host` derlemesi (gerçek PostgreSQL 18, Release, 0 uyarı / 0 hata; `evidence/V1-RMD-424/tests.log`). Sent,
  Held ve ek malzemeli kalemlerin stok iadesini sınayan mevcut testler değişmeden geçer.
- Yeni test `AVoidWhoseStockCannotBeGivenBackIsRefusedAndChangesNothing`: stok kalemi pasife alınmış Sent kalemin
  void'i 409 `STOCK_RESTORE_FAILED` döner; kalem `Active`/`Sent` kalır, stok 9'da kalır. Üretim değişikliği geri
  alınınca kırmızı (409 beklenirken 500; `evidence/V1-RMD-424/red-without-fix.log`).
- V1-RMD-398 probe'u S11 (çok satırlı void iadesi) düzeltilmiş kopyada geçer; S02'nin stok iadesi kontrol adımı geçer,
  son adımı V1-RMD-418'de açıklanan değiştirilemez defter nedeniyle başarısızdır (`evidence/V1-RMD-424/stock-flow-probes.log`).
- Semih'in elle deneyebileceği senaryo: mutfağa gitmiş ama hazırlanmamış bir kalemi void edin; stok geri gelir. Stok
  geri verilemeyecek bir durumda (ör. stok kalemi pasif) "Ürünün stoğu geri verilemedi; iptal yapılmadı." uyarısı
  çıkar ve kalem siparişte kalır.

## Handoff

- None
