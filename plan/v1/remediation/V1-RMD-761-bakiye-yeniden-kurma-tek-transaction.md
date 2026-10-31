# V1-RMD-761 - Bakiye yeniden kurma silme ve yazmayı tek transaction'da yapar

- Task ID: V1-RMD-761
- Status: Done
- Assignee: claude-code-session_01FMN5WVXi1HJCTEk5argkgZ
- Work type: remediation
- Surface state: Existing

## Goal

V1-RMD-742 sonrası denetim bulgusu: `StockBalanceProjector.RebuildAllBalancesAsync` önce tüm `inventory.stock_balances` satırlarını siliyor (`ResetAllBalancesAsync`), sonra her toplamı ayrı bir otomatik-commit ifadesiyle yazıyor, sonra rezervasyonları geri yazıyor. Yazma sırasında hata olursa (bağlantı kopması, kısıt ihlali) silme kalıcı olur ve bakiyeler boş ya da yarım kalır.

Silme, yazma ve rezervasyon geri yükleme tek transaction'da yapılır; hata olursa mevcut bakiyeler olduğu gibi kalır. Rezervasyonlar silmeden önce aynı transaction içinde okunur.

Not: metodun bugün üretimde çağıranı yoktur (yalnız testler); düzeltme bir bakım aracı bağlandığında veri kaybını önler. Eşzamanlı hareket yazımına karşı koruma bu görevin konusu değildir.

## Owned surface

- `plan/v1/remediation/V1-RMD-761-bakiye-yeniden-kurma-tek-transaction.md`
- `evidence/V1-RMD-761/**`
- Sınırlı ek: src/Modules/Inventory/BalanceProjection/IStockBalanceRepository.cs
- Sınırlı ek: src/Modules/Inventory/BalanceProjection/PostgresStockBalanceRepository.cs
- Sınırlı ek: src/Modules/Inventory/BalanceProjection/StockBalanceProjector.cs
- Sınırlı ek: tests/Modules/Inventory/BalanceProjection/StockBalanceDatabaseTests.cs

## In scope

- `IStockBalanceRepository.ReplaceAllBalancesAsync(sums)`: varsayılan arayüz gövdesi eski sıralı çağrıları kullanır (sahte depolar değişmez); Postgres tek transaction'da okur, siler, yazar ve rezervasyonları geri yükler.
- `RebuildAllBalancesAsync` yeni metodu çağırır; rezervasyonu silmeden önce okuma işi depoya taşınır.
- Gerçek PostgreSQL testi: yazma sırasında kısıt ihlali olursa mevcut bakiye ve rezervasyon korunur.

## Out of scope

- Eşzamanlı hareket yazımı ile yeniden kurma yarışı.
- `ResetAllBalancesAsync`, `SetExactBalanceAsync`, `RestoreReservedQuantityAsync` imzaları.
- Yeniden kurmayı bir uç noktaya veya zamanlanmış göreve bağlamak.

## Dependencies

- V1-RMD-742
- V1-RMD-320

## Acceptance evidence

1. Düzeltmeden önce yeni test (varsayılan sıralı gövdeyle) kırmızı olur; çıktı kaydedilir.
2. Düzeltmeden sonra `BalanceProjection` testleri (gerçek PostgreSQL) exit 0 geçer; rezervasyon koruma testi dahil.
3. Mutasyon: transaction commit'i yerine her ifade ayrı commit edilirse (ya da silme transaction dışına alınırsa) yeni test kırmızı olur; dosya hash'i geri yüklenir.
4. Kapsam aracı InProgress baseline'a karşı exit 0 verir; iki bağımsız incelemede artık bulgu kalmaz.

## Handoff

- None
