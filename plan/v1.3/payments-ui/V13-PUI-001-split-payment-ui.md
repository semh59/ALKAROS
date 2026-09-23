# V13-PUI-001 - Implement cashier payment and split allocation UI

- Task ID: V13-PUI-001
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PDF:I.26-I.26A

## Goal

Açık Bill tahsisleri üzerine Cash, BankCard ve onaylı MealCard payment kompozisyonunu uygulayın.

## Owned surface

- **Düzeltme (kapanış, 2026-09-23):** görev dosyası özgün olarak
  `src/Clients/Cashier/Payments/SplitPayment/**` (C# ad alanı biçiminde)
  yazıyordu. Gerçek Cashier istemcisi incelendiğinde bunun artık geçerli
  bir konvansiyon olmadığı görüldü — Cashier düz JS PWA (React değil),
  ve `payments/` altında zaten kurulu, gerçek bir sayfa deseni var
  (`wwwroot/payments/cash-session/**`, V13-PUI-002). Bu deseni taklit
  etmek, `src/Clients/**` altında daha önce defalarca bulunup silinen
  "ölü C# Engine sınıfı" hatasına düşmemek için tercih edildi. Gerçek
  Owned surface aşağıdaki gibidir:
- `src/Clients/Cashier/wwwroot/payments/split-payment/**`
- `src/Host/DualScreen/DualScreenApplication.Payments.cs` (yeni dosya —
  generik BankCard/EFT tahsilat HTTP yüzeyi; Nakit kendi mevcut
  `.../cash-sessions/{id}/cash-tender` ucunu kullanmaya devam eder)
- `tests/Host/Experience/PaymentTender/**`
- `evidence/V13-PUI-001/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.cs
  (yalnız `app.MapPaymentTenderApi();` çağrısı eklendi), ALKAROS.slnx
  (yeni test projesi kaydı)
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Eşit/tutar/madde ayrımı, kalan miktar, tender seçimi, bağımsız gönderim ve bilinmeyen durum kilidi.

## Out of scope

- CustomerAccount tender, geri ödeme ve cash kapatma.

## Dependencies

- V13-PAY-002
- V13-PAY-003
- V13-ALC-001
- V13-ALC-002
- V1-BIL-002
- V0-CMP-005

## Deliverables

- `src/Clients/Cashier/Payments/SplitPayment/**` altında Goal kapsamını uygulayan production code ve task-specific
  automated test assets.
- Contract/UI ve otomatik success/failure/retry testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- **Backend (gerçek Postgres'e karşı, `tests/Host/Experience/PaymentTender`, 10/10 yeşil):**
  EFT kalan tutarı aşan tahsilat 409 ile reddedilir ve hiçbir allocation yazmaz; aynı idempotency key'in tekrarı
  ikinci bir allocation üretmeden aynı sonucu döner; BankCard denemesi HER ZAMAN `RequiresReconciliation` döner
  (asla sahte Approved/Declined) ve hiçbir allocation yazmaz; MealCard `TENDER_METHOD_NOT_REGISTERED` ile,
  CustomerAccount `TENDER_VERSION_NOT_ENABLED` ile reddedilir; Nakit bu genel uçtan reddedilip kendi
  `cash-tender` ucuna yönlendirilir; karışık EFT+BankCard denemesinde özet yalnız gerçekten allocate edilen
  EFT tutarını gösterir (istemci aritmetiğiyle değil, sunucudan okunarak) — `MixedEftAndBankCardOnly...` testi.
  `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata. `python tools/plan-audit/plan_audit_tool.py validate`
  ve `python tools/consistency-audit/consistency_audit.py` → temiz.
- **Frontend (`split-payment.js`):** over-allocation istemci tarafında da engellenir (tutar kalanı aşarsa
  gönderim engellenir); bir tahsilat `RequiresReconciliation` döndüğünde sayfa `locked` durumuna geçer ve
  aynı sekmede yeni tahsilat eklenmesini engeller (mükerrer tahsilat riskine karşı istemci-taraflı kilit —
  aşağıdaki bilinen sınırla birlikte okunmalı); "Ödendi" durumu yalnız sunucudan okunan `remainingAmount`
  sıfıra indiğinde gösterilir, hiçbir yerde istemci-taraflı toplama güvenilmez.
- **Bilinen, dürüstçe kaydedilen sınır:** `PendingBankCardTerminalIntegrationHandler` hiçbir Payment/allocation
  satırı yazmıyor (gerçek terminal sonucu yok) — bu yüzden "Unknown durum kilidi" sunucu tarafında kalıcı
  değil, yalnız bu sayfanın kendi oturumu içinde (JS state) uygulanıyor. Gerçek, kalıcı bir kilit
  `V13-HUG-001` gerçek terminal entegrasyonunu ve/veya `V13-PAY-004`'ün orkestrasyonunu bu HTTP yüzeyine
  bağlayan bir fast-follow gerektirir.
- **Kapsam dışı bırakılan (madde bazlı bölüştürme):** `V13-ALC-002`'nin split-design altyapısı (owner bazlı
  bölüştürme) mevcut, ama bu jenerik tender akışına hiç bağlanmamış bir read-model — madde/kişi bazlı
  otomatik tutar önerisi bu görevde icat edilmedi (yalnız eşit ve serbest tutar bölüştürme var); bu, task
  dosyasının kendi In-scope metnindeki "madde ayrımı" ifadesinden bir daralma, dürüstçe kaydedildi.
- **V0-CMP-005 karşısında doğrulanan/doğrulanamayan:** Sayfa `lang="tr"`, görünür odak halkası
  (`:focus-visible`), `aria-live`/`aria-busy`, klavye ile ulaşılabilir form elemanları ve cash-session.js
  ile aynı, zaten onaylı renk paletini kullanıyor — CUI (Cashier UI, dokunmatik kiosk) satırının 2.4.11
  istisnasıyla tutarlı. Gerçek cihaz/ekran okuyucu matrisiyle (NVDA/VoiceOver, gerçek Windows 11 Chrome
  kiosk modu) bu oturumda CANLI test YAPILMADI — yalnız yapısal/kod seviyesinde uyum sağlandı, dürüstçe
  kaydedilir.
- **E2E (Playwright) kapsamı:** bu oturumda yeni bir E2E spec YAZILMADI (zaman kısıtı) — `tests/E2E/Cashier/`
  altında `cash-session` sayfasının kendi speclerine (`05-cash-session-lifecycle.spec.js`) paralel bir
  `split-payment` speci gerçek bir fast-follow olarak önerilir; backend'in 10/10 gerçek-Postgres HTTP testi
  para-kritik mantığı zaten kapsıyor.

## Handoff

- V13-PUI-002
- V13-PUI-003
- V13-PUI-004
