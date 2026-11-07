# V1-RMD-880 - Cari yaşlandırma raporu ve bakiye anlık görüntülerinin gerçek akışa bağlanması (F0097)

- Task ID: V1-RMD-880
- Status: InProgress
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: implementation
- Surface state: Existing

## Goal

F0097: IBalanceSnapshotStore üretimde hiç çağrılmıyordu. Mevcut depo gerçek akışa bağlanır: günlük bakım işi (balance-snapshot, Güvenlik/Sistem ekranından elle de çalışır) son kapanmış işletme gününün kapanış bakiyesini defterden hesaplayıp yazar (geriye dönük 7 gün tamamlar); Yönetim ekranında Cari yaşlandırma raporu: 0-30 / 31-60 / 61-90 / 90+ gün kovaları defterden FIFO ile (ödemeler en eski borcu kapatır, kredi politikasıyla aynı kural), fazla ödeme ayrı sütun, 30/60/90 gün önceki bakiye anlık görüntüden. Kararlar: yaş işlem tarihinden (vade değil), işletme günü 06:00 kuralı, izin reports.view + yönetici oturumu, anlık görüntü değişmez (sonradan geriye tarihli kayıt yansımaz).

## Owned surface

- `evidence/V1-RMD-880/**`, `src/Modules/CustomerAccounts/Aging/**`, `src/Host/Experience/CustomerAging/**`, `src/Clients/PosTerminal/src/features/management-customer-aging/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Modules/CustomerAccounts/BalanceProjection/IBalanceSnapshotStore.cs — kapanış bakiyesi yazma ve toplu okuma
  - src/Modules/CustomerAccounts/BalanceProjection/PostgresBalanceSnapshotStore.cs
  - src/Modules/CustomerAccounts/BalanceProjection/BalanceSnapshot.cs
  - src/Modules/CustomerAccounts/CustomerAccountsModule.cs — yaşlandırma servisi kaydı
  - src/Modules/CustomerAccounts/Aging/ReceivablesAging.cs — FIFO hesaplayıcı ve modeller
  - src/Modules/CustomerAccounts/Aging/PostgresReceivablesAgingService.cs
  - tests/Modules/CustomerAccounts/BalanceProjection/PostgresBalanceSnapshotStoreTests.cs
  - tests/Modules/CustomerAccounts/BalanceProjection/ReceivablesAgingCalculatorTests.cs
  - tests/Modules/CustomerAccounts/BalanceProjection/PostgresReceivablesAgingServiceTests.cs
  - src/Host/Experience/CustomerAging/ReceivablesAgingEndpoints.cs — uç nokta, süzgeç, kayıt
  - src/Host/Experience/CustomerAging/BalanceSnapshotMaintenanceJob.cs
  - src/Host/DualScreen/DualScreenApplication.cs — kayıt ve eşleme
  - src/Host/Composition/Errors/ApiErrorCatalog.cs — kendi hata alanı
  - tools/consistency-audit/unreachable_services_allowlist.json — iki girdi çıkar
  - tests/Host/Experience/CustomerAccounts/CustomerAgingHttpTests.cs
  - tests/Host/Experience/CustomerAccounts/CustomerAccountsHttpTestDatabase.cs
  - tests/Host/Experience/CustomerAccounts/ALKAROS.Host.Experience.CustomerAccounts.Tests.csproj
  - src/Clients/PosTerminal/src/features/management-customer-aging/**
  - src/Clients/PosTerminal/src/features/management/sections.ts
  - src/Clients/PosTerminal/src/strings.ts
  - src/Clients/PosTerminal/src/features/interaction-telemetry/buttonSites.ts
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Yaşlandırma hesaplayıcı (FIFO, kovalar, fazla ödeme, check.isBalanced) ve gerçek Postgres servisi
2. Anlık görüntü deposu: kapanış bakiyesi yazma (idempotent) ve tarihlere göre toplu okuma; kullanılmayan eski üyeler silinir
3. balance-snapshot bakım işi ve kaydı
4. GET /api/v1/management/reports/customer-aging uç noktası (yönetici oturumu + reports.view, kendi hata alanı)
5. Yönetim ekranı Cari yaşlandırma (Türkçe)
6. Testler, mutasyon, iki bağımsız qa incelemesi

## Out of scope

- Cari e-fatura (F0099/F0457), F0089/F0090, vade tarihine göre gecikme sınıfı, geçmiş tarihli rapor (asOf), tahsilat/ihtar iş akışı, RebuildAsync kararı

## Dependencies

- None

## Acceptance evidence

1. Kırmızı/yeşil test çıktıları
2. Mutasyon sonuçları
3. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
