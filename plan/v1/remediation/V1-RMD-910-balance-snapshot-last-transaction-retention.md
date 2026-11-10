# V1-RMD-910 - Günlük bakiye kayıtları son işlemden 2 yıl sonra saklanmaz: işlemi bırakan müşteriye kayıt yazma ve sil

- Task ID: V1-RMD-910
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-896 açığı (Semih kararı: kapat). Ürün kararı (2026-11-01): fatura olmayan müşteri verisi SON İŞLEMDEN 2 yıl sonra saklanmaz. 896 yalnız kaydın kendi gününe baktı (730 günden eski satırı siler); ama günlük bakiye işi, defterinde hiç işlemi olan HER müşteri için son 100 günün satırını yazmaya devam ediyor: son işlemi 2 yıldan eski bir müşterinin verisi süresiz kalıyor (her gün yeni satır, bakiye değişmese de). Karar: (1) iş, son işlemi o gün için 730 günden eski olan müşteriye kayıt yazmaz; (2) bakım işi, son işlemi 730 günden eski (ya da hiç işlemi olmayan) müşterilerin tüm günlük bakiye kayıtlarını siler; mevcut 730 günden eski satır silme kuralı bakım işinde kalır (aktif müşterinin kaydı da sınırlı kalsın). Sonuç metni silinen toplam kaydı yazar. KVKK politika tablosunda bakiye kayıtlarının yokluğu ve snapshot_date indeksi bu görevin dışındadır.

## Owned surface

- `evidence/V1-RMD-910/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Modules/CustomerAccounts/BalanceProjection/IBalanceSnapshotStore.cs
  - src/Modules/CustomerAccounts/BalanceProjection/PostgresBalanceSnapshotStore.cs
  - src/Host/Experience/CustomerAging/BalanceSnapshotMaintenanceJob.cs
  - tests/Modules/CustomerAccounts/BalanceProjection/PostgresBalanceSnapshotStoreTests.cs
  - tests/Host/Experience/CustomerAccounts/CustomerAgingHttpTests.cs
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Testler: işlemi bırakan müşteriye kayıt yazılmaz, aktif müşteriye yazılır; bakım işi bırakan müşterinin tüm kayıtlarını siler, aktifin kayıtlarını (730 gün içindeki) korur; sonraki çalıştırma yeniden yazmaz
2. Mağaza ve iş değişiklikleri
3. Mutasyon ve iki bağımsız qa incelemesi

## Out of scope

- KVKK politika tablosuna bakiye kaydı ekleme, snapshot_date indeksi, anonimleştirilen müşterinin satırları

## Dependencies

- V1-RMD-909

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
