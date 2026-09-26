# V1-RMD-293 - Mutfak yazıcı hataları ve belirsiz teslimler personele görünür

- Task ID: V1-RMD-293
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-25

## Goal

`KitchenPrintDispatchHostedService` yazıcı hatalarında yeniden dener ve belirsiz iletimi (`PrinterTransmissionUncertain`) kayda alır; ama `print-jobs` ve denetim uç noktalarının hiçbir istemcisi yok. Yazıcı erişilemezse ya da fiş belirsiz kalırsa mutfak personeli ve kasiyer bunu görmez. Mutfak ekranına başarısız/belirsiz yazdırma işleri şeridi, yeniden yazdırma ve 'fiş çıktı' onayı eklenir.

## Owned surface

- `plan/v1/remediation/V1-RMD-293-kitchen-print-failure-visibility.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/kitchen-operations/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/Cashier/specs/17-kitchen-print-failures.spec.js
  (görevde 16 yazıyordu; V1-RMD-294 o numarayı zaten almıştı, 17 kullanıldı)

## In scope

1. Yazdırma işleri listesi, yeniden deneme ve belirsiz teslim onayı, Türkçe durum etiketleri, testler.

## Out of scope

- Fiziksel yazıcı sürücüsü ve gerçek cihaz doğrulaması (dış bağımlılık).

## Dependencies

- V1-RMD-220

## Acceptance evidence

### Yerel doğrulama ve kapsam bulguları (görevin metniyle çelişiyor)

- **'Fiş çıktı onayı' (belirsiz teslim onay/red) zaten vardı.** `KitchenUnknownDelivery`, `approveReprint`, `rejectReprint` ve `UnknownPanel` bu görevden ÖNCE tam olarak yazılmıştı (`/deliveries/unknown` çağrılıyor, onay/red uç noktaları kullanılıyor). Görevin 'hiçbir istemcisi yok' iddiası yalnızca düz `/print-jobs` uç noktaları için doğruydu; `/deliveries/*` için yanlıştı. Bu kısma dokunmadım.
- **'Yeniden yazdırma' uygulanamaz — sunucuda böyle bir eylem yok.** `PrintJob` bir kez `DeadLetter` olunca onu yeniden kuyruğa alan hiçbir uç nokta yok (outbox'taki `V1-RMD-288`'in aksine). Ayrıca `PhysicalPrintRecoveryService.ExecuteApprovedReprintAsync` hiçbir yerden çağrılmıyor — onaylanan bir yeniden yazdırma kararı asla fiilen yazdırmıyor. İkisi de Host/Modules dosyalarında, bu görevin Owned surface'ı yalnızca istemci. Sahte bir 'yeniden dene' düğmesi eklemedim; bu iki bulguyu ayrı bir takip görevine bırakıyorum.
- **Bu ortamda gerçek bir başarısız/ölü mektup yazdırma işi üretilemiyor.** `BridgeUnprintedTicketsAsync` yalnız ticket'ın istasyonunda AKTİF kayıtlı bir yazıcı varsa iş oluşturuyor; Cashier E2E ortamı hiç yazıcı seed'lemiyor (`tests/E2E/Cashier/lib/seed.js` Owned surface dışında). Bu, görevin kendi 'gerçek cihaz doğrulaması dış bağımlılık, kapsam dışı' notuyla örtüşüyor.

### Uygulama

- `kitchenApi.ts`: yüklenen her ticket için `GET /print-jobs?ticketId=` çağrılıyor (istasyon genelinde tek bir liste uç noktası yok), `Failed`/`DeadLetter` işleri `printJobFailures` olarak birleştiriliyor.
- `KitchenOperationsWorkspace.tsx`: yeni 'Yazdırma sorunu' istatistik kartı ve salt-okur 'Yazdırma sorunları' paneli (ticket numarası, yazıcı adı, Türkçe durum etiketi, deneme sayısı). Eylem düğmesi yok — çalışan bir sunucu eylemi olmadığı için.
- PosTerminal vitest: 54/54 (3 yeni). `pnpm typecheck` temiz.
- Cashier E2E (gerçek Host + Chromium): 38/38 (37 mevcut + 1 yeni `17-kitchen-print-failures`). Bu ortamda yazıcı olmadığından senaryo, gerçek bir ticket için `/print-jobs` çağrısının fiilen yapıldığını ve panelin 'Bekleyen yazdırma sorunu yok.' dediğini kanıtlıyor; gerçek bir başarısız işi kanıtlamıyor.
- Mutasyon kontrolü: istemci kodu (üç dosya) eski hâline döndürülünce hem yeni vitest testleri hem yeni E2E senaryosu kırıldı.
- `plan_audit_tool.py validate` ve `consistency_audit.py` temiz.

## Handoff

- None
