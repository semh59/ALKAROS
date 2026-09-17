# V13-GOV-001 - Admit the contract-independent Payments/Cash chain ahead of GATE-V13-ENTRY

- Task ID: V13-GOV-001
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: decision
- Surface state: Existing

## Source basis

- PO:2026-09-17

## Goal

`plan/v1.3/README.md`'nin giriş koşulu iki ayrı şeyi birden istiyor:
`GATE-V13-ENTRY` (`GATE-V12-EXIT`'e bağlı — V1.2'nin `online-ordering`/
`channel-mapping`/`reconciliation`/`reporting`/`shared-stock` modülleri
hâlâ `V0-YSP-001`'e — Yemeksepeti partner API, hâlâ `Blocked` — bağımlı
olduğu için resmen KAPANMAMIŞ) VE "uygulanacak Hugin/meal-card private
sözleşmeleri" (ticari/hukuki bir ön koşul, koddan bağımsız).

Bu görev, V1.3 içindeki görevlerin bir alt kümesini — Hugin T300 cihazı,
meal card sağlayıcı sözleşmesi veya fiscal (GİB/e-arşiv) entegrasyonuna
HİÇBİR şekilde bağımlı olmayan saf domain/backend zincirini — bu iki dış
koşulu beklemeden başlatmayı, Semih'in açık kararıyla kaydeder. Emsal:
`V12-GOV-001`/`V12-GOV-002` (NFC ve QR kanallarının, kendileriyle teknik
bağı olmayan dış sözleşme bağımlılıklarını beklemeden kabul edilmesi).

Karar mekanizması: bu oturumda Semih'e "V13 giriş kapısı kapalı görünüyor,
nasıl ilerleyelim?" diye üç seçenekli bir soru soruldu (kod bağımsız
kısımları başlat / önce resmi gate kararı al / başka modüle geç); Semih
"Kod bağımsız kısımları başlat (Recommended)" seçeneğini seçti.

## Owned surface

- `plan/v1.3/governance/V13-GOV-001-admit-contract-independent-payments-cash-chain.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan) — hiçbiri sahiplik iddiası
  değil, yalnız `Dependencies`/`Giriş koşulu` metni güncelleniyor:
  - plan/v1.3/README.md — "Giriş koşulu" bölümüne bu istisna eklenir.
  - plan/v1.3/payments/V13-PAY-001-payment-aggregate.md — `Dependencies`
    listesinden yalnız `GATE-V13-ENTRY` satırı çıkarılır; `V0-CMP-002`
    (zaten `Done`) korunur.

## In scope

Aşağıdaki 10 görev, gerçek bağımlılık zincirleri `git`/plan dosyaları
üzerinden tek tek doğrulanarak (her birinin geçişli bağımlılıkları ya
zaten `Done` bir V0/V1 görevi, ya da bu listedeki başka bir görev)
`GATE-V13-ENTRY`'den ve Hugin/meal-card sözleşmelerinden bağımsız olduğu
için kabul edilir:

- `V13-PAY-001` (Payment aggregate) — yalnız `V0-CMP-002` (Done).
- `V13-PAY-002` (Tender command routing) — yalnız `V13-PAY-001`,
  `V0-DAT-002` (Done), `V0-ARC-004` (Done).
- `V13-PAY-005` (EFT tender handler) — kendi metninde açıkça
  "Provider entegrasyonu YOK" diyor; yalnız `V13-PAY-001/002/003`'e
  bağımlı (PAY-003 kendisi bloklu olsa da PAY-005'in EFT akışı PAY-003'ün
  registry'sine kayıt olmayı bekler — bu görev PAY-003 açılana kadar
  yalnız domain/handler kodu olarak ilerler, registry'ye kayıt PAY-003
  açıldığında tamamlanır).
- `V13-CSH-001` (Cash session lifecycle) — yalnız `V13-PAY-002`,
  `V1-IAM-002` (Done), `V0-DOM-001` (Done), `V1-CSH-001`'e (Done, ADR)
  bağımlı.
- `V13-CSH-002` (Cash transaction ledger) — `V13-CSH-001`, `V13-PAY-001`,
  `V0-DAT-004` (Done).
- `V13-CSH-003` (Cash tender handler) — `V13-PAY-002`, `V13-CSH-001/002`,
  `V13-ALC-001`, `V1-FND-005` (Done).
- `V13-ALC-001` (Allocation persistence) — `V13-PAY-001`, `V1-BIL-002`
  (Done), `V0-DOM-004` (Done), `V0-DAT-003` (Done).
- `V13-ALC-002` (Bill closure projection) — yalnız `V13-ALC-001`,
  `V1-BIL-001` (Done), `V0-DAT-004` (Done), `V1-FND-005`'e (Done)
  bağımlı.
- `V13-ALC-003` (Partial refund allocation — yalnız `RefundIntent`
  kalıcılaştırma, provider çağrısı yok) — `V13-ALC-001/002`,
  `V0-DOM-003` (Done), `V11-RSV-003` (Done).
- `V13-PUI-002` (Cash session UI) — `V13-CSH-001/002`, `V1-CSH-001`
  (Done), `V0-CMP-005` (Done).

## Out of scope

Aşağıdakiler hâlâ bloklu kalır, bu görev bunları değiştirmez:

- `V13-PAY-003`/`V13-PAY-004` — gerçek BankCard/MealCard provider
  onaylanmış sonucuna bağımlı.
- `V13-HUG-001..004` — gerçek Hugin T300 cihazı/sandbox'ı gerektirir.
- `V13-MCD-001..004` — approved meal card sağlayıcı sözleşmesi gerektirir.
- `V13-FSC-001..005` — gerçek GİB/e-arşiv/T300 e-adisyon/QNB e-adisyon
  entegrasyonu gerektirir.
- `V13-ALC-004` — yalnız "provider Approved refund sonucundan sonra"
  çalışır, `V13-HUG-003`/`V13-MCD-003`'e bağımlı.
- `V13-REC-001`, `V13-RPT-001` — fiscal/meal-card verisine bağımlı.
- `V13-TBL-001` — `V13-PAY-004`'e bağımlı.
- `V13-PUI-001`/`V13-PUI-003`/`V13-PUI-004` — zincirleri `V13-PAY-003`
  veya Hugin/`V13-PUI-001` üzerinden bloklu kalıyor.
- `GATE-V13-ENTRY`'nin kendisini (yani `GATE-V12-EXIT`'i) kapatmak veya
  V1.2'nin `V0-YSP-001` bağımlılığını çözmek — bu görevin kapsamı dışında.

## Dependencies

- V0-CMP-002
- V1-CSH-001

## Onay

Approved by Semih — Founder/Product Owner — 2026-09-17. Bu oturumda
sorulan "V13 giriş kapısı kapalı görünüyor, nasıl ilerleyelim?" sorusuna
"Kod bağımsız kısımları başlat (Recommended)" yanıtı verildi. Gerekçe:
`GATE-V13-ENTRY`'nin gerçek blok nedeni (Yemeksepeti partner API) ve
Hugin/meal-card sözleşmeleri, yukarıdaki 10 görevin hiçbiriyle teknik
olarak bağlı değil — tıpkı `V12-GOV-001`/`V12-GOV-002`'nin NFC/QR için
kaydettiği gibi.

## Deliverables

- `plan/v1.3/README.md`'de kayıtlı istisna.
- `V13-PAY-001`'in `Dependencies`'inden `GATE-V13-ENTRY`'nin çıkarılması.

## Acceptance evidence

- `plan/v1.3/README.md`, bu istisnayı adıyla ve gerekçesiyle (V12-GOV-001/
  002 emsaliyle) yazar.
- `V13-PAY-001` artık yalnız `V0-CMP-002`'ye (Done) bağımlı.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- Yukarıdaki "Out of scope" listesindeki hiçbir görev dosyasında değişiklik
  yok.

## Handoff

- V13-PAY-001
