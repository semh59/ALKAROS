# V13-GOV-005 - Decide TokenX Connect Cloud as the V1.3 kasa integration path

- Task ID: V13-GOV-005
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: decision
- Surface state: Existing

## Source basis

- PO:2026-09-17
- CORR:C98

## Goal

`V0-GOV-064`/CORR:C98, Token/Beko'yu hedef donanım olarak seçti ama Token'ın
KENDİSİ üç farklı entegrasyon yüzeyi sunuyor
(`developer.tokeninc.com`'un derin incelemesiyle doğrulandı):

1. **IntegrationHub.dll (kablolu/USB)** — Windows-only (.NET/C++ DLL,
   Visual Studio + VC++ Redistributable gerektiriyor); `ALKAROS.Host`
   Linux (Debian) Docker container'ında çalıştığı için (`deploy/docker/
   Dockerfile`) doğrudan kullanılamaz — ayrı bir native Windows köprü
   süreci gerektirir.
2. **TokenX Connect Cloud (REST/bulut)** — düz HTTPS, `client-id`/
   `client-secret` → Bearer token, `Add Basket`/`Instant Basket`/
   `Get Terminals` endpoint'leri. Linux container'dan sorunsuz çalışır.
3. **In-App Integration (Android, cihaz üzerinde çalışan özel uygulama)**
   — kendi ALKAROS uygulamamızı cihazın "Satış Uygulamaları" menüsüne
   ekleyip Intent tabanlı sepet/ödeme akışı kurmak; garson elinde taşınan
   bağımsız bir sipariş+ödeme cihazı senaryosu için gerekli, ama yeni bir
   native Android istemci geliştirmeyi gerektiriyor.

Bu görev, **V1.3 kapsamında hangisinin birincil yol olacağını** karara
bağlıyor.

## Owned surface

- `docs/domain/token-integration-path-decision.md`
- `evidence/V13-GOV-005/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v0/hugin-t300/V0-HUG-001-integration-contract.md
  (V0-HUG-001 sahipliğinde kalır) — yalnız Goal/In scope'a bu kararın
  referansı eklenir; Status/Blocker değişmez (hâlâ Blocked — gerçek
  sözleşme/cihaz kanıtı yok).

## In scope

- Kasa (sabit, kasiyer tarafından tetiklenen) ödeme akışı için **TokenX
  Connect Cloud (REST)**'in birincil entegrasyon yolu olarak seçilmesi.
- Kablolu yolun (IntegrationHub.dll) V1.3'te İZLENMEYECEĞİNİN kaydı
  (Windows-only, mevcut Linux container mimarisiyle uyumsuz; yeni bir
  native köprü bileşeni gerektirir, bu maliyeti karşılayacak bir gerekçe
  yok).
- In-App Integration (Android, garson-taşınabilir senaryo) yolunun
  **ŞİMDİLİK ertelendiğinin** kaydı ("Belki sonra" — Semih'in kendi ifadesi)
  — bir ihtiyaç değil, gelecekte ayrı bir karar ve ayrı bir görev
  gerektirir.
- Ağ topolojisi notu: `docs/architecture/qr-relay-topology.md`'nin
  kilitli "no public inbound ports" kuralı nedeniyle, Token'ın webhook
  callback'i (`BASKET_COMPLETED`) yerine **polling** (`Get Basket
  Details`/`Get Open Baskets`) varsayılan tercih olarak öneriliyor — bu,
  dual-screen-pos-topology'nin zaten kullandığı "periyodik snapshot
  reconciliation" felsefesiyle örtüşüyor ve sıfır yeni altyapı
  (Cloudflare Tunnel rotası) gerektirmiyor. Webhook istenirse, bu ayrı
  bir kararla (yeni bir tunnel rotası açarak) değiştirilebilir.

## Out of scope

- In-App Integration'ın gerçek tasarımı/kodu — ertelendi, ayrı bir görev.
- Kablolu yolun bir native Windows köprüsüyle mümkün kılınması — reddedildi
  (maliyet/fayda gerekçesiyle), yeniden açılması ayrı bir karar gerektirir.
- Webhook vs. polling kararının kesinleştirilmesi — bu görev yalnız bir
  varsayılan ÖNERİ kaydediyor, gerçek implementation görevi (`V13-HUG-001`
  veya benzeri) kesin kararı verir.
- Araştırma notu (2026-10-17, V1-RMD-881), kayıt durumunu ve kararı değiştirmez; kaynak 2026-10-17 dış bağımlılık araştırması (Token geliştirici portalı), kayıtlı hâli `plan/v0/hugin-t300/V0-HUG-001-integration-contract.md` sonundaki V1-RMD-584 notudur (bulgu F0089):
- Çelişki 1: Karar polling'i varsayılan yapıyor, webhook'u "gerekmez" sayıyor. HoReCa sayfası (`x-platform/baslangic/horeca`) "Polling YAPILMAZ, Webhook ZORUNLUDUR" diyor (olaylar BASKET_COMPLETED/LOCKED/UNLOCKED); sayfanın bu kuralı yalnız Cloud için mi yazdığı doğrulanamadı (kablolu mod için webhook anlamsız). `Get Basket Details`/`Get Open Baskets` polling'inin desteklenip desteklenmediği doğrulanamadı.
- Çelişki 2: Karar "300 TR / X30 TR" için Cloud'u seçiyor; portalın Cloud tanıtımında yalnız X30TR geçiyor, 300TR yalnız kablolu (V0-HUG-001 notu).
- Çelişki 3: "Sıfır yeni altyapı" gerekçesi webhook zorunluysa geçmez; Token bulutundan gelen girişi karşılayacak yeni bir tünel rotası gerekir (`docs/architecture/qr-relay-topology.md` "no public inbound ports").
- Karar bekliyor: webhook'lu Cloud yolu ve tünel rotası, ya da kablolu/uygulama içi yola dönüş. Sahibi: ürün sahibi.
- Karar güncellemesi (2026-11-07, Semih, V1-RMD-888): yukarıdaki açık karar kapandı. Token yolu webhook'lu TokenX Connect Cloud ve yeni bir Cloudflare Tunnel rotasıdır; "polling varsayılan" ve "sıfır yeni altyapı" ifadeleri geçersizdir, kapsam X30 TR ile sınırlıdır. Ayrıntı `docs/domain/token-integration-path-decision.md` sonundaki "Decision update" bölümündedir. Kod yazılmadı; webhook alıcısı ve tünel rotası ayrı gelecek görevlerdir.


## Dependencies

- V0-GOV-064

## Onay

Approved by Semih — Founder/Product Owner — 2026-09-17. Karar akışı:
Semih "Biz pos cihazına bağlanmak için kasa dibinde bile olsa android
uygulaması yazacağım" dedi; bu, gerçekten gerekli olup olmadığı netleşince
("kasa'da sabit duran cihaz için Android app gerekmiyor, yalnız garson
taşınabilir senaryosunda gerekir" açıklaması sonrası) "Anladım şuan bu
cihazla kasada ödeme almayı tercih ederim. Belki sonra" yanıtıyla
netleşti — yani ŞİMDİLİK yalnız kasa senaryosu, REST/bulut yeterli;
Android/taşınabilir senaryo gelecekte ayrı bir karar olarak
değerlendirilecek.

## Deliverables

- `docs/domain/token-integration-path-decision.md`: üç yolun karşılaştırması,
  seçilen sonuç, reddedilen/ertelenen alternatifler, invariant listesi.

## Acceptance evidence

- Karar kaydı, `developer.tokeninc.com`'un gerçek dokümantasyonuyla
  (IntegrationHub Hızlı Başlangıç, TokenX Connect Cloud Geliştirici
  Dokümanı, In-App Integration Hızlı Başlangıç) doğrulanmış bulgulara
  dayanıyor.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.

## Handoff

- V0-HUG-001
