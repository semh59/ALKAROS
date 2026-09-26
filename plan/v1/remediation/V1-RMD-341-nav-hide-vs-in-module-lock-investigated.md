# V1-RMD-341 - Üst navigasyon gizleme ile modül-içi kilitli-görünür deseni araştırıldı: iki kasıtlı, tutarlı seviye

- Task ID: V1-RMD-341
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: investigation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

Bağımsız 13 ajanlı derin denetimin (2026-09-26) orta seviye bulgusu: "PosTerminal'de üst navigasyon tamamen
gizliyor, Mutfak modülü 'kilitli ama görünür' yapıyor — tutarsız yetki UX'i" (`shell/models.ts:45-47` vs
`workspace.tsx:562`). Doğrulandı: iki gerçek, birbirinden farklı ama HER İKİSİ de kasıtlı olan desen var:

1. `allowedNavigation()` (`shell/models.ts:45-47`): üst navigasyondaki her modül (Satış, Masalar, Fatura, Mutfak,
   Katalog, ...) kullanıcının o modülün TEK bir "taban" yetkisine (örn. Mutfak için `kitchen.advance`) sahip
   olmadığı sürece TAMAMEN gizleniyor.
2. `workspace.tsx:562` ve çevresi (`onApproveReprint`/`onManageRouting`/vb.): kullanıcı Mutfak modülünün
   İÇİNDEYSE (yani taban yetkiye zaten sahipse), modül içindeki DAHA İNCE TANE tekil eylemler (yeniden basım
   onayı, yönlendirme yönetimi, gibi yönetici-katmanı yetkiler) eksikse gizlenmiyor — kilitli ama görünür kalıyor.
   Bu, `KitchenOperationsWorkspace`'in kendi V1-KDS-002 görev yorumunun AÇIKÇA belgelediği, kasıtlı bir karar:
   "the workspace itself decides whether the button is enabled or shown-but-locked... a kitchen-staff session
   must see the same button, locked, not a hidden one."

Bu iki desen ÇATIŞMIYOR — biri "bu alana hiç girebilir miyim" (taban yetki, tamamen gizle: erişilemez bir alanı
reklam etmenin anlamı yok), diğeri "alandayım, bu SPESİFİK eylemi yapabilir miyim" (ince tane yetki, kilitli
göster: personelin bu yeteneğin var olduğunu ve bir yöneticiden isteyebileceğini bilmesi gerekiyor — V1-KDS-002'nin
kendi gerekçesi). Aynı iki-katmanlı model, Katalog/Sistem Sağlığı gibi diğer üst-seviye modüllerde de (taban
yetki = `catalog.manage`, o modülün İÇİNDEKİ ince taneli eylemler ayrıca kendi izin kontrollerine sahip) zaten
var — Mutfak'a özel bir tutarsızlık değil, bütün uygulamanın genel iki-seviyeli deseni.

## Owned surface

- Kod değişikliği yok (bu görevin kendisi — bkz. Acceptance evidence).
- `plan/v1/remediation/V1-RMD-341-nav-hide-vs-in-module-lock-investigated.md`

## In scope

1. `shell/models.ts`'in `allowedNavigation()` fonksiyonunun ve `workspace.tsx`'in Mutfak eylem-seviyesi
   yetkilendirme kodunun gerçekten farklı davrandığının doğrulanması (doğrulandı — yukarı bkz.).
2. Bu farkın bir kod kusuru mu yoksa kasıtlı bir tasarım kararı mı olduğunun araştırılması: `workspace.tsx:565-568`
   satırlarındaki kendi yorumu, bunun V1-KDS-002 kapsamında kasıtlı olarak seçildiğini ve bu görevin kendi
   "Acceptance evidence"ının GEREKTİRDİĞİNİ ("a kitchen-staff session must see the same button, locked, not a
   hidden one") doğrudan belirtiyor.
3. Aynı iki-katmanlı desenin diğer üst-seviye modüllerde (Katalog, Sistem Sağlığı) de zaten var olduğunun
   doğrulanması — Mutfak'a özgü bir tutarsızlık olmadığı, uygulamanın genel deseni olduğu sonucuna varıldı.

## Out of scope

1. Tüm uygulama genelinde TEK bir "gizle mi, kilitli göster mi" felsefesine zorlamak — bu, ürünün genel
   yetkilendirme UX kararını değiştirmek anlamına gelir (hangi modüllerin taban yetkisi hâlâ tamamen gizlenmeli,
   hangilerinin kilitli-görünür olmalı), Semih'in ürün kararı gerektirir. V1-KDS-002 zaten "kilitli-görünür"ü
   özellikle Mutfak için BİLİNÇLİ olarak seçmişken, bunu geriye almak veya diğer modüllere zorla yaymak, test
   edilmiş ve kasıtlı bir davranışı, bu bulgunun kendi kapsamının çok ötesinde bir ürün kararıyla değiştirmek olur.

## Dependencies

- None

## Acceptance evidence

- `shell/models.ts:45-47` (`allowedNavigation`) okunarak taban-yetki-yoksa-tamamen-gizle davranışı doğrulandı.
- `workspace.tsx:559-569` okunarak Mutfak modülü içindeki ince-taneli eylemlerin (yeniden basım onayı/reddi,
  yönlendirme yönetimi) kilitli-ama-görünür kaldığı, ve bunun `onSuspendProductAvailability`'nin kendi V1-KDS-002
  yorumunda AÇIKÇA belgelendiği doğrulandı: "the workspace itself decides whether the button is enabled or
  shown-but-locked... a kitchen-staff session must see the same button, locked, not a hidden one."
- `workspace.tsx:114-130`'daki navigasyon tanımları okunarak Katalog (`catalog.manage`) ve Sistem Sağlığı
  (`catalog.manage`) gibi diğer üst-seviye modüllerin de AYNI iki-katmanlı (taban yetki = nav görünürlüğü,
  modül-içi ince tane = ayrı kontroller) deseni izlediği doğrulandı — Mutfak'a özgü bir sapma değil.
- Sonuç: bu, birbirini çelişen değil TAMAMLAYAN, her ikisi de kasıtlı ve belgelenmiş iki farklı granularite
  seviyesi. Kod değişikliği gerektirmiyor; ilerideki bir ürün kararıyla tüm uygulamanın tek bir felsefeye
  zorlanması istenirse bu, ayrı bir görev olarak ele alınmalı.

## Handoff

- None
