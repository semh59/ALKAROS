# V1-RMD-127 - Independent audit: Orders raw English error leaks

- Task ID: V1-RMD-127
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Bağımsız denetimin (2026-09-09) bir başka kritik bulgusu:
`OrderManagementEndpoints.cs`'teki `/void`, `/comp` ve `/void-sent`
uç noktaları kendi inline `try/catch` bloklarında istisnaları yakalayıp
ham `ex.Message` (İngilizce, istisna sınıflarından geldiği haliyle)
döndürüyordu — `docs/UI_STYLE_GUIDE.md`'nin "kullanıcıya görünen her metin
Türkçedir" kuralının doğrudan ihlali. Bunun daha da tuhaf yanı: aynı grup
üzerinde zaten kayıtlı `OrderManagementExceptionFilter`, bu istisnaların
HER BİRİ için doğru Türkçe metni ve AYNI durum/hata koduyla eşlemeyi zaten
içeriyordu (`Map` metodu) — inline `catch` blokları bu filtreyi asla
tetiklenmeyecek şekilde gölgeliyordu (ölü/çift mantık). Ayrıca aynı
dosyada altı yerde İngilizce sabit metin (`"TableId cannot be empty."`,
`"Order items cannot be empty."`, iki kez `"...not found."`, iki
`GRANT_DENIED` mesajı) doğrudan istemciye dönüyordu.

## Owned surface

- `plan/v1/remediation/V1-RMD-127-orders-raw-english-error-leaks.md` (yeni)
- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır (yollar
  geri-tik olmadan yazıldı ki denetleyici bunları sahiplik iddiası olarak
  parse etmesin):
  - src/Host/Experience/Orders/OrderManagementEndpoints.cs (V1-ORD-005/
    V1-BIL-005/V1-IAM-027/V1-RMD-113 sahipliğinde) — `/void`, `/comp`,
    `/void-sent` uç noktalarındaki üç inline `try/catch` bloğu tamamen
    kaldırıldı (istisnalar artık zaten kayıtlı `OrderManagementExceptionFilter`e
    düşüyor — durum kodları ve hata kodları birebir aynı kaldı, yalnız
    mesaj metni İngilizce `ex.Message`'dan doğru Türkçe çeviriye geçti);
    altı sabit İngilizce mesaj Türkçeye çevrildi; filtrenin sınıf-üstü
    açıklaması güncellendi. Mevcut hiçbir endpoint imzası, davranışı veya
    HTTP durum kodu değişmedi.

## In scope

1. `/void`, `/comp`, `/void-sent` uç noktalarındaki inline `catch`
   blokları kaldırıldı; `OrderItemNotFoundException`, `InvalidItemReasonException`,
   `LateVoidRejectedException`, `StaleOrderRowVersionException`,
   `ItemNotYetSentException`, `ItemAlreadyServedException`,
   `BillNotModifiableForWasteException`, `InvalidOperationException`
   artık doğrudan `OrderManagementExceptionFilter.Map`'e düşüyor — bu
   eşleme zaten her biri için aynı durum kodu ve hata kodunu üretiyordu,
   yalnız mesaj artık Türkçe.
   - Tek gözlemlenebilir fark: `OrderItemNotFoundException` artık inline
     `"ITEM_NOT_FOUND"` yerine filtrenin genel `"NOT_FOUND"` kodunu
     üretiyor (durum kodu hâlâ 404). Hiçbir istemci (`src/Clients/**`)
     veya test bu koda bağımlı değildi (grep ile doğrulandı) — davranışsal
     bir regresyon değil, kasıtlı bir sadeleştirme.
2. Altı sabit İngilizce mesaj Türkçeye çevrildi: `"TableId cannot be
   empty."` → `"Masa kimliği boş olamaz."`, `"Order items cannot be
   empty."` → `"Sipariş kalemleri boş olamaz."`, `"No active order for
   table."` → `"Bu masa için aktif sipariş bulunamadı."`, `"Order not
   found."` → `"Sipariş bulunamadı."`, `"Complimentary request was
   denied."` → `"İkram talebi reddedildi."`, `"Void request was denied."`
   → `"İptal talebi reddedildi."`.
3. Yeni bir test eklenmedi — mevcut dört HTTP test dosyası
   (`tests/Host/Experience/Orders/{Void,Comp,VoidSent,TableDraft}`) zaten
   yalnız HTTP durum kodunu doğruluyor (mesaj metnini değil), bu yüzden
   davranışsal eşdeğerlik zaten onlarla kanıtlanıyor.

## Out of scope

- Audit'in "3 admin surfaces entirely in English" notu — grep ile
  doğrulandı, bu üç yüzey bu dosyanın dışında (ayrı bir tarama/görev
  gerektiriyor, bu görev yalnız `OrderManagementEndpoints.cs`'i kapsadı).
- Audit'in aynı ailedeki diğer bulguları (#9 `is_available` kontrolü,
  #11-13 WaiterPwa alan uyuşmazlıkları) — ayrı remediation görevleri.

## Dependencies

- V1-ORD-005
- V1-BIL-005
- V1-IAM-027

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug`: 0 Uyarı, 0 Hata.
- Bu görev sırasında paylaşılan Docker VM belleği başka, ilgisiz
  projelerin konteynerleri tarafından doldurulmuş durumdaydı (V1-RMD-126
  ile aynı ortam koşulu). Etkilenen dört test projesi tek bir konteyner
  çalıştırmasında, aynı Docker imajıyla, gerçek Postgres'e karşı
  çalıştırıldı (boru hattı olmadan, gerçek `$?` yakalanarak):
  `ALKAROS.Host.Experience.Orders.Void.Tests`: 5/5,
  `ALKAROS.Host.Experience.Orders.Comp.Tests`: 9/9,
  `ALKAROS.Host.Experience.Orders.VoidSent.Tests`: 10/10,
  `ALKAROS.Host.Experience.Orders.TableDraft.Tests`: 13/13 — gerçek çıkış
  kodu `0`.
- `python tools/plan-audit/plan_audit_tool.py validate`: 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py`: 13 önceden var
  olan ihlal (değişmedi), yeni ihlal yok.

## Handoff

- None
