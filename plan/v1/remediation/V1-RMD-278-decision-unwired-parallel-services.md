# V1-RMD-278 - Karar: paralel tasarımlı kayıtlı servisler bilinçli olarak bağlanmaz

- Task ID: V1-RMD-278
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: decision
- Surface state: Existing

## Source basis

- PO:2026-09-24

## Goal

`V1-RMD-272` erişilebilirlik kapısının bulduğu borcun geri kalanı için kod okunarak verilen karar.
Semih 2026-09-24'te öneriyi onayladı: çalışan bir mekanizma varken, ona paralel ikinci bir
mekanizmayı bağlamak yerine bilinçli olarak bağlanmaz ve nedeni kayda geçer.

**Karar:** Sipariş kabulünde stok düşümü `OrderStockConsumptionService` (`V1-RMD-143`) ile TEK yoldan,
aynı işlemde, doğrudan ve korumalı biçimde yapılır. Aşağıdaki türler bu yolun paralel/eski bir tasarımıdır;
bağlanırlarsa aynı tüketim iki kez düşülebilir. Kod SİLİNMEDİ (testli, gelecekte bilinçli bir görevle
benimsenebilir), yalnız `unreachable_services_allowlist.json` içinde bu karara bağlandı.

| Aile | Türler | Gerekçe |
| --- | --- | --- |
| Porsiyon rezervasyonu | `IPortionReservationLifecycleService`, `IPortionReservationArbitrator` (+ depolar), `IPortionCancellationDecisionService`, `IKitchenItemStateProvider` | Tüketim `OrderStockConsumptionService` ile düşülüyor; rezervasyon yaşam döngüsü ikinci bir düşüm yolu açar. Benimsemek sipariş akışında (bekleyen → kabul → iptal) mimari bir değişikliktir. |
| Rezervasyon bakiyesi | `IReservationBalanceProjector` (+ depo) | Yalnız rezervasyon ailesinin izdüşümü; onsuz anlamsız. |
| Stok hareket servisi | `IStockMovementService` | Fire ve sayım gibi çalışan yollar hareket deposunu doğrudan kullanıyor; genel servis kimseye gerekmedi. |
| Mutfak yönlendirme | `IKitchenRoutingService` | Mutfak yönlendirmesi Host'ta depolar ve yönlendirici üzerinden zaten çalışıyor; sarmalayıcı gereksiz. |
| Sır çözümleyici | `IRotatingSecretResolver` | Tüketicisi yok; yedek şifreleyici sürüm kaydını doğrudan okuyor. |
| Yeniden şifreleme | `AuthorizedReEncryptionService` | Yalnız saklama kayıtları üretildiğinde anlamlı; kayıt üreten veri görevleri (V14) bağlayacak. |
| İade niyeti | `IRefundIntentRepository` | Gerçek terminal ister (`V13-HUG-003`, Planned, dış bağımlılık). Ayrı referansla listede. |

Bu kararla `V1-RMD-273` (Planned) kapatıldı: her ailenin sınıflandırması yapıldı; ölü olup gerekli olanlar
`V1-RMD-274/275/276/277` ile bağlandı, kalanlar burada gerekçelendirildi.

## Owned surface

- `plan/v1/remediation/V1-RMD-278-decision-unwired-parallel-services.md`

## In scope

1. Karar kaydı ve izin listesi girişlerinin bu karara bağlanması (`V1-RMD-277` görevinde yapıldı).

## Out of scope

- Bu türleri silmek ya da benimsemek: gelecekte bilinçli bir görevin işi.

## Dependencies

- V1-RMD-272
- V1-RMD-274
- V1-RMD-275
- V1-RMD-276
- V1-RMD-277

## Acceptance evidence

- `tools/consistency-audit/unreachable_services_allowlist.json`: 25 giriş; her biri bu göreve ya da `V13-HUG-003`'e
  referanslı; `consistency_audit.py` temiz. Yeni bir ulaşılamayan kayıt ya da artık ulaşılabilir olan bir giriş
  denetimi kırar.
- `python tools/plan-audit/plan_audit_tool.py validate` temiz.

## Handoff

- None
