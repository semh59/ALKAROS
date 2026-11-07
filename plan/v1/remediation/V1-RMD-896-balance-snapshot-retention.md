# V1-RMD-896 - Günlük cari bakiye kayıtları için saklama süresi ve silme işi (2 yıl)

- Task ID: V1-RMD-896
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Goal

`customer_account.balance_snapshots` her işletme günü her müşteri için bir satır biriktirir ve hiç silinmez; yaşlandırma raporu yalnız son 90 günü okur. Ürün sahibi kararı (2026-11-01): fatura olmayan müşteri verisi son işlemden 2 yıl sonra saklanmaz. Günlük bakiye kayıtları deftere (değişmez kaynak) bağlı, müşteri başına türetilmiş veridir ve fatura kaydı değildir; bu yüzden 2 yıl (730 gün) saklanır. Mevcut `balance-snapshot` bakım işi (6 saatte bir) yazdığı kayıtların yanında 730 günden eski kayıtları siler: `IBalanceSnapshotStore.DeleteOlderThanAsync(date)` eklenir, iş silinen sayıyı sonuç metnine yazar. Silme idempotenttir; defter ve güncel bakiye (`balances`) etkilenmez.

## Owned surface

- `evidence/V1-RMD-896/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Modules/CustomerAccounts/BalanceProjection/IBalanceSnapshotStore.cs — silme işlemi
  - src/Modules/CustomerAccounts/BalanceProjection/PostgresBalanceSnapshotStore.cs — silme sorgusu
  - src/Host/Experience/CustomerAging/BalanceSnapshotMaintenanceJob.cs — saklama süresi ve silme
  - tests/Modules/CustomerAccounts/BalanceProjection/PostgresBalanceSnapshotStoreTests.cs — silme testi
  - tests/Host/Experience/CustomerAccounts/CustomerAgingHttpTests.cs — bakım işi testi
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test: sınırdan eski kayıtlar silinir, sınırdaki ve yeni kayıtlar kalır; silme tekrarında hata yoktur; bakım işi çalışınca eski satırlar gider ve rapordaki 30/60/90 günlük bakiyeler değişmez
2. Mutasyon ve iki bağımsız qa incelemesi

## Out of scope

- Defter satırları, `balances` önbelleği, fatura kayıtları (10 yıl kararı ayrı görev)

## Dependencies

- None

## Acceptance evidence

1. Kırmızı/yeşil test çıktıları
2. Mutasyon sonuçları
3. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
