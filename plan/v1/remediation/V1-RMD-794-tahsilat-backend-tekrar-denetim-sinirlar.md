# V1-RMD-794 - Tahsilat (backend): nakit tekrarında alınan tutar, reddedilen tahsilatın izi, istemci sınırlarının kilidi

- Task ID: V1-RMD-794
- Status: InProgress
- Assignee: claude-code-session_019vXeYXjE3sM6VZKpRMj1L9
- Work type: implementation
- Surface state: Existing

## Goal

Kasa incelemesinin G4 grubunun tahsilat tarafı (`7-birlesik-dogrulama.md`), PO kararları değişmeden:

1. F0409: nakit tahsilatın tekrarında (`BuildReplayResultAsync`) hesap ve tutar karşılaştırılıyor ama müşterinin verdiği `TenderedAmount` karşılaştırılmıyordu; aynı anahtarla başka bir verilen tutar (ve dolayısıyla başka para üstü) 200 ile eski sonucu döndürüyordu. Kayıtlı verilen tutar (`ApprovedAmount + ChangeAmount`) istekle farklıysa tekrar bugünkü Türkçe 409 `TENDER_IDEMPOTENCY_KEY_REUSED` ile reddedilir; aynı istek eskisi gibi aynı sonucu döner.
2. Reddedilen tahsilatın izi: kalan tutarı aşan kart ve EFT tahsilatı (`TENDER_OVER_ALLOCATION`) yalnız 409 dönüyor, denetim kaydı bırakmıyordu. Reddedilen her kalan-aşımı için `payment.tender-rejected` denetim olayı (hesap, yöntem, istenen tutar, kalan tutar, kasiyer, tahsilat kimliği anahtarı) yazılır; yazım başarısız olursa kasiyer yine aynı Türkçe 409'u görür ve hata günlüğe düşer, tahsilat sonucu değişmez.
3. İstemci sınırlarının tek kaynağı: iade ve kasa açıklaması 500 karakter sınırı, kasa kapanış fark toleransı (0-500), nakit çıkış onay eşiği ve diğer sayısal ayar aralıkları istemcide ayrıca yazılı. Kaynak sunucudaki sabitlerdir (`CashSessionPolicy.MaxExplanationLength`, `CashTransaction.MaxReasonLength`, `*Setting.MaxValue`); istemci değerleri bu sabitlere kilitlenir: bir sabit değişir de istemci değişmezse test kırılır. Arayüzün kendisi V1-RMD-795'in.
4. Türkçe hata metinleri: tahsilat ve iade uçlarının bütün hata yanıtları taranır; İngilizce sızıntı varsa düzeltilir, yoksa tarama testi kalıcı kilit olur.

HEAD'de kapalı olan (kod değişmez, RESULT'ta atıf): F0389 (V1-RMD-788).

Kapsam dışı: iade tarafı (V1-RMD-793, Done); arayüzler ve E2E (V1-RMD-795); BankCard terminal entegrasyonu (dış bağımlılık).

## Owned surface

- `evidence/V1-RMD-794/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Modules/Cash/TenderHandler/CashTenderHandler.cs (V13-CSH-003) — tekrar yolunda verilen tutar karşılaştırması.
  - src/Host/DualScreen/DualScreenApplication.Payments.cs (V1-RMD-258) — reddedilen tahsilatın denetim olayı.
  - tests/Modules/Cash/TenderHandler/CashTenderHandlerTests.cs (V13-CSH-003) — F0409 testleri.
  - tests/Host/Experience/PaymentTender/TenderRejectionAuditHttpTests.cs (V1-RMD-258) — yeni dosya: denetim izi ve Türkçe hata taraması.
  - tests/Host/Experience/CashSession/ClientLimitLockTests.cs (V1-RMD-788) — yeni dosya: istemci sınırlarının sunucu sabitlerine kilidi.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Her kabul koşulu için önce kırmızı test, sonra düzeltme, sonra yeşil; mutasyon turu.
2. Nakit tekrar testi: aynı anahtar ve aynı tutar tekrar eder; aynı anahtar + başka verilen tutar 409 olur ve yeni kayıt yazılmaz.
3. Kalan tutarı aşan kart ve EFT tahsilatı: 409 aynı kalır, tam bir denetim olayı yazılır (alanlar doğrulanır); kalanı aşmayan tahsilat olay yazmaz; olay yazımı başarısız olsa bile yanıt 409 kalır.
4. İstemci sınırları kilidi: PosTerminal `models.ts` sayısal aralıkları ve Cashier `cash-session.js` 500 karakter sınırları sunucu sabitleriyle birebir eşleşir.
5. İngilizce kod ve test adları; kullanıcıya dönen metinler Türkçe.

## Out of scope

- İade backend'i, arayüzler, E2E.

## Dependencies

- V1-RMD-793

## Acceptance evidence

1. Kırmızı sonra yeşil: `CashTenderHandlerTests`, `TenderRejectionAuditHttpTests`, `ClientLimitLockTests`, mevcut `PaymentTenderHttpTests` (gerçek Postgres, `evidence/V1-RMD-794/`).
2. Mutasyon: tekrar karşılaştırması ve denetim olayı kaldırılınca ilgili test kırılır; kaynak `cmp` ile geri alınır.
3. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
4. qa-backend ve qa-test-kalitesi incelemeleri sırayla, bulgular giderilir, düzeltmelerden sonra doğrulama turu.
5. Semih'in elle deneyeceği senaryo: nakit satışı bitirdikten sonra aynı işlemi başka bir verilen tutarla tekrar göndermek mümkün değil (hata verir); kalan tutardan fazla kartla tahsilat denemesi reddedilir ve denetim kayıtlarında görünür.

## Handoff

- None
