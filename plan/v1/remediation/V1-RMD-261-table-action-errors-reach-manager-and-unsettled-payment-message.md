# V1-RMD-261 - Masa işlemi hataları müdüre ulaşır; çözülmemiş ödeme kendi mesajıyla bildirilir; iki istemcili devir E2E'si

- Task ID: V1-RMD-261
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-24

## Goal

`docs/engineering/e2e-playwright-master-test-plan.md`'nin Faz 3 "bayrak"
senaryosunu uygulamak: çözülmemiş bir kart tahsilatı olan masanın PosTerminal
"Masa yönetimi" ekranından devredilememesini (`V13-TBL-001`, `V1-RMD-258`) iki
gerçek istemciyle (Cashier PWA + PosTerminal) tek bir gerçek arka uca karşı
kanıtlamak. Spec, ilk dürüst koşuda **gerçek bir kullanıcı deneyimi hatası**
buldu.

**Hata — masa işlemlerindeki her hata genel "Tekrar deneyin"e düşüyordu.**
`tableApi.ts` başarısız yanıtlarda kendi `TableManagementApiError` sınıfını
fırlatır; `TableWorkspace.tsx`'in `errorMessage()`'i ise yalnız paylaşılan
`ApiError` sınıfına `instanceof` bakıyordu (`V1-RMD-114`'ün düzeltmesi, tableApi
kendi sınıfını getirdiğinde güncellenmemişti). Sonuç: çakışma, yetki, durum
kuralı, ödeme kilidi — masa eylemlerindeki HER hata sunucunun özenle eşlenmiş
Türkçe mesajı yerine "İşlem tamamlanamadı. Tekrar deneyin." olarak görünüyordu.
"Masa güncellendi; işlem tekrarlanmadı" çakışma kolu da hiç çalışmıyordu (mesaj
metni üzerinde `409|conflict` regex'i genel metne bakıyordu). Ödeme kilidinde
bunun somut zararı: müdür, ödeme mutabakatı yapılana kadar asla işe yaramayacak
bir işlemi tekrar denemeye yönlendiriliyordu, nedenini hiç görmüyordu.

Ek olarak sunucu, ödeme politikası reddini (`PaymentPolicyRequiredException`)
diğer durum kurallarıyla aynı genel `409 DOMAIN_CONFLICT` ("Masa işlemi mevcut
durumla çakışıyor.") altında topluyordu; istemci düzelse bile neden söylenmezdi.

## Owned surface

- `plan/v1/remediation/V1-RMD-261-table-action-errors-reach-manager-and-unsettled-payment-message.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Tables/TableManagementApplication.cs
  (Tables Host modülünün sahibi görevlerde kalır — yalnız `Map`: iki
  `PaymentPolicyRequiredException` artık `409 PAYMENT_UNSETTLED` ve kendi
  Türkçe mesajıyla eşleniyor)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/tables/TableWorkspace.tsx
  (V1-TBL sahipliğinde kalır — yalnız `errorMessage`, yeni `isConcurrencyConflict`
  ve `submitAction`'ın catch dalı)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/tables/TableWorkspace.test.tsx
  (aynı görev sahipliğinde — bayat çakışma testi gerçek hata biçimine çevrildi,
  ödeme reddi için yeni test eklendi)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/Cashier/specs/11-cross-client-payment-aware-transfer.spec.js
  (V1-CUI-011 sahipliğindeki Cashier E2E paketine eklenen yeni dosya)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/Cashier/lib/seed.js
  (V1-CUI-011 sahipliğinde kalır — yalnız 12 dokunulmamış `TRF-n` masalı ayrı
  bir salon eklendi)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/Cashier/lib/paymentHelpers.js
  (V13-RMD-003 ile eklenen yardımcı — `createBill` artık isteğe bağlı gerçek bir
  masa alıyor)

## In scope

1. Sunucu: `TableTransfer.PaymentPolicyRequiredException` ve
   `TableMerge.PaymentPolicyRequiredException` → `409 PAYMENT_UNSETTLED`,
   "Bu masanın hesabında çözülmemiş bir ödeme var. Ödeme mutabakatı
   tamamlanmadan masa taşınamaz veya birleştirilemez."
2. İstemci: `errorMessage` artık `TableManagementApiError` mesajını da gösterir;
   "masa güncellendi" çakışma kolu yalnız gerçek eşzamanlılık hatasında
   (`CONCURRENT_MODIFICATION`) çalışır, mesaj metnine bakan regex kaldırıldı.
3. Spec 11: (a) çözülmemiş kart tahsilatı olan masa PosTerminal'den
   devredilemez, müdür nedeni görür, iki masa ve hesap değişmez, Cashier PWA
   aynı hesabı "Manuel mutabakat gerekiyor" ile gösterir; (b) tüm ödemeleri
   çözülmüş kısmen ödenmiş masa devredilir, hesap aynı kalır ve 40 TL tahsis
   aynı hesapta durur (para ne yaratıldı ne yok oldu).

## Out of scope

- Birleştirme (merge) ve birleşimi ayırma (unmerge) için ayrı iki istemcili
  spec'ler — sunucu eşlemesi üçünü de kapsıyor, ama bu görevde yalnız devir
  tarayıcıda sürüldü.
- PosTerminal masa ekranında ham Sipariş/Hesap GUID'lerinin gösterilmesi — ayrı
  bir arayüz bulgusu, burada değiştirilmedi.
- Çözülmemiş ödemenin gerçekten çözülebileceği bir arayüz (`V13-HUG-001` gerçek
  terminali gerektiriyor).

## Dependencies

- V1-RMD-114
- V13-TBL-001
- V13-RMD-003
- V1-CUI-011

## Acceptance evidence

- Cashier E2E suite'i (`tests/E2E/Cashier`, gerçek Host, gerçek Chromium,
  UTF8 Postgres 18): **25/25 geçti** (23 önceki + 2 yeni).
- **Mutasyon kontrolü:** sunucu eşlemesi ve istemci düzeltmesi birlikte geri
  alınıp Host ve `dist` yeniden derlendi → spec 11 ilk test beklenen yerde
  kırıldı (beklenen "çözülmemiş bir ödeme", görülen "İşlem tamamlanamadı.
  Tekrar deneyin."); düzeltmeler geri uygulanıp yeniden derlenince geçti.
- `pnpm typecheck` temiz; `src/features/tables` vitest 27/27 (yeni ödeme-reddi
  testi dahil). Bayat çakışma testi, üretimde hiç oluşmayan bir hata biçimini
  (`ApiError` + mesaj metni) taklit ediyordu; gerçek biçime çevrildi.
- Host testleri, UTF8 Postgres'e karşı: Host.Experience.Tables 14/14,
  Tables.TableTransfer 35/35, TableMerge 29/29, PaymentTopology 11/11,
  TableLifecycle 59/59, Reservations 27/27, CurrentPointers 19/19. `dotnet
  build` Host 0 hata, 0 uyarı.
- Dürüstçe belirtilen sınırlar: sunucudaki yeni `PAYMENT_UNSETTLED` kodu için
  ayrı bir Host HTTP testi yok (uçtan uca E2E ve mutasyon kanıtı var); merge ve
  unmerge tarayıcıda sürülmedi; suite CI'da koşmuyor.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.

## Handoff

- None
