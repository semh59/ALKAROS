# V1-RMD-436 - Kredi limiti tanımlanmadan cari hesaba borç yazılmaması

- Task ID: V1-RMD-436
- Status: InProgress
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-28

## Goal

V1-RMD-393 para akışı denetimi F-13 (ikinci yarı): cari hesaba borç yazan `AccountChargeHandler`, kredi kontrolü
için üretimde `AlwaysApproveCreditPolicy` kullanıyordu; her tutarı her müşteriye onaylıyordu (AGENTS.md
yer tutucu/sahte başarı yasağı). Sistemde müşteri başına kredi limiti kavramı yok; bugün "hesaba yaz" uç noktası
da yok, ama ilk açıldığında her müşteriye sınırsız veresiye verilirdi.

Bu görev: yer tutucu kaldırılır; yerine kapalı-varsayılan bir politika gelir. Kredi limiti tanımlanana kadar her
cari borç yazma, Türkçe gerekçeyle ("müşteri için kredi limiti tanımlanmadı") reddedilir; hesap ve adisyon
değişmez. Limitli gerçek politika (müşteri başına limit, vade) ayrı bir karar ve görevdir; o geldiğinde bu kayıt
değiştirilir.

## Owned surface

- `plan/v1/remediation/V1-RMD-436-account-charge-refused-without-credit-limit.md`
- `evidence/V1-RMD-436/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/CustomerAccounts/BillCharges/ICustomerCreditPolicy.cs ve
  src/Modules/CustomerAccounts/BillCharges/CustomerAccountsBillChargesModule.cs (V14-ACC-003 sahipliğinde) — yalnız
  politika sınıfı ve kaydı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/CustomerAccounts/BillCharges/AccountChargeHandlerTests.cs
  (V14-ACC-003 sahipliğinde) — onaylayan test politikası ve yeni testler
- Sınırlı ek (paylaşılan, geri-tik olmadan): tools/consistency-audit/unreachable_services_allowlist.json (V1-RMD-272
  sahipliğinde) — yalnız kaldırılan yer tutucunun satırı

## In scope

- `NoCreditLimitDefinedPolicy` ve üretim modülündeki kaydı.

## Out of scope

- Müşteri başına kredi limiti, vade ve limit yönetim ekranı (ayrı karar).
- "Hesaba yaz" HTTP uç noktası.

## Dependencies

- V1-RMD-435

## Acceptance evidence

- Yeni testler (gerçek PostgreSQL 18): üretim politikasıyla cari borç yazma Türkçe gerekçeyle reddedilir, ne
  tahsilat/dağıtım ne cari hareket oluşur; üretim modülü `ICustomerCreditPolicy`'yi bu politikaya çözer. Mevcut
  işleyici testleri açıkça onaylayan bir test politikasıyla aynı davranışı doğrular. Yer tutucu geri konunca kırmızı
  (`evidence/V1-RMD-436/red-without-fix.log`); yeşil koşu `evidence/V1-RMD-436/tests.log`.
- Semih'in elle deneyebileceği senaryo: yok; cari hesaba yazan bir ekran veya uç nokta henüz yok. İlk açıldığında,
  kredi limiti kararı verilene kadar "hesaba yaz" reddedilir.

## Handoff

- None
