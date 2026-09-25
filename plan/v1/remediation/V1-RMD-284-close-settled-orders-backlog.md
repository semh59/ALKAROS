# V1-RMD-284 - Ödemesi tamamlanmış eski siparişleri kapatan iki adımlı bakım aracı

- Task ID: V1-RMD-284
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-24

## Goal

`V1-RMD-282` yeni siparişleri ödemeyle kapatıyor; ondan ÖNCEKİ siparişler (hesabı çoktan `Paid` olanlar dahil)
hâlâ `Submitted`/`Accepted`. Garson yükü, garson devri ve not saklaması geçmiş veri yüzünden hâlâ kirli.
Semih'in kararıyla iki adımlı, yalnız-yönetici (`security.manage`, yönetici oturumu) bir araç eklendi:

1. `GET /api/v1/management/security/orders/backlog` SALT-OKUR: canlı sipariş sayısı, kanıtlanabilir biçimde
   ödenmiş olanlar (tüm hesapları `Paid`/`Cancelled` ve en az biri `Paid`), hiç hesabı olmayanlar, açık hesabı
   olanlar, en eski ödenmiş siparişin tarihi.
2. `POST .../orders/close-settled?dryRun=&limit=`: YALNIZ kanıtlanabilir biçimde ödenmiş olanları, taze bir ödemenin
   kullandığı aynı `OrderSettlementService` üzerinden `Completed` yapar. `dryRun` VARSAYILAN olarak `true`: hiçbir
   şey değiştirmez, kaç siparişin kapanacağını ve örnek sipariş numaralarını söyler. Gerçek çalıştırma
   (`dryRun=false`) sınırlı partilerle (varsayılan 200, en fazla 1000) çalışır, sipariş başına durum geçmişine "Ödeme
   tamamlandı" yazar (masaya hâlâ bağlıysa masayı da boşaltır) ve parti başına `orders.backfill.closed-settled`
   denetim olayı yazar. Tekrar çalıştırmak zararsızdır.

Hesabı hiç olmayan ya da hâlâ açık hesabı olan siparişlere ASLA dokunulmaz: onları kapatmak bir iş kararıdır
(gün sonunda müdür karar verir), veri onarımı değildir.

## Owned surface

- `plan/v1/remediation/V1-RMD-284-close-settled-orders-backlog.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/SecurityAdministration/OrderBacklogAdministration.cs
  (V1-RMD-266 sahipliğindeki klasöre eklenen yeni dosya)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/SecurityAdministration/SecurityAdministrationEndpoints.cs
  (yalnız `MapOrderBacklog()` çağrısı ve servis kaydı)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/SecurityAdministration/SecurityAdministrationHttpTests.cs
  (3 yeni test)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/SecurityAdministration/SecurityAdministrationTestDatabase.cs
  (yalnız sipariş/hesap tohumlama yardımcıları)

## In scope

1. Rapor ve kuru-çalıştırmalı toplu kapatma, denetim, testler.

## Out of scope

- Hesabı olmayan ya da açık hesabı olan siparişleri kapatmak (iş kararı).
- Otomatik/zamanlanmış çalıştırma (bilerek elle).
- Yönetim arayüzü ekranı.

## Dependencies

- V1-RMD-282
- V1-RMD-266

## Acceptance evidence

- Host.Experience.SecurityAdministration (UTF8 Postgres 18): 22/22. Yeni: anonim 401, `reports.view`'lu 403, `supervisor:`
  cihazı 401; rapor 4 gerçek siparişi (ödenmiş, hesapsız, açık hesaplı, zaten tamamlanmış) doğru ayırır (canlı 3, ödenmiş 1,
  hesapsız 1, açık hesaplı 1); varsayılan çağrı kuru çalıştırma (uygun 1, kapanan 0, sipariş `Submitted`); `dryRun=false`
  yalnız ödenmiş olanı `Completed` yapar, hesapsız ve açık hesaplı olanlar aynı kalır, denetim olayı bir kez, ikinci çalıştırma
  uygun 0.
- `consistency_audit.py`, modül sınır testleri 9/9, `plan_audit_tool.py validate` temiz.

## Handoff

- None
