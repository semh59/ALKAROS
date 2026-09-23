# V13-RMD-GOV-001 - Remediate governance, documentation, and accessibility findings from the Faz 2 independent audit

- Task ID: V13-RMD-GOV-001
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-23

## Goal

6 bağımsız ajanla yapılan Faz 2 (V13-GOV-008 + 8 ödeme görevi) denetiminin bulduğu, para/mimari
güvenliği dışında kalan governance/dokümantasyon/erişilebilirlik bulgularını kapatmak. Aynı denetimin
bulduğu para güvenliği ve mimari kümesi (CardSettlementOrchestrator bağlama, TOCTOU kilidi, exception
eşleme, BillId kapsamı) **ayrı, paralel bir remediation görevinde** ele alınıyor — bu görev onunla
çakışmamak için yalnız aşağıdaki dosyalara dokunur.

## Owned surface

- `src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js` (yalnız iki event handler'a
  odak-geri-yükleme eklendi — render()/lock/state-loading mantığına dokunulmadı)
- `src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.css`
- `docs/compliance/accessibility-target.md`
- `plan/TRACEABILITY.md`
- `plan/v1.3/governance/V13-RMD-GOV-001-faz2-audit-governance-and-accessibility-remediation.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1.3/governance/V13-GOV-008-payment-orchestration-dependency-waiver.md
  (yalnız kendi C101→C102 referansı düzeltildi), plan/v1.3/payments/V13-PAY-003-tender-handler-composition.md
  (retroaktif Owned surface notu eklendi — bkz. Acceptance evidence), tools/plan-audit/plan_audit_tool.py
  (yalnız bir yorum satırındaki C101→C102 düzeltmesi), tools/task-scope/task_scope_tool.py (aynı düzeltme) —
  hepsi paylaşılan dosyalar, plain text (backtick'siz).

## In scope

1. **[High, erişilebilirlik]** `split-payment.js`'te tender-yöntemi çip'i ve EFT onay kutusu her
   tıklandığında/değiştiğinde `render()` tüm kartı yeniden oluşturuyor, klavye/switch-access odağını
   `<body>`'ye düşürüyordu. WaiterPwa'nın kendi "render sonrası taze düğümü yeniden bul ve odakla"
   deseniyle düzeltildi — iki event handler'a `render()` sonrası ilgili taze düğümü bulup `.focus()`
   çağrısı eklendi.
2. **[Low]** EFT onay kutusunun dokunma hedefi (22×22px) WCAG 2.2 2.5.8'in 24×24px asgari değerinin
   altındaydı — 24×24px'e çıkarıldı.
3. **[Medium, dokümantasyon doğruluğu]** `docs/compliance/accessibility-target.md`'nin `PUI` önekini
   "Customer QR UI (mobile browser)" olarak sınıflandırması **tamamen yanlıştı** — gerçek 4 `V13-PUI-*`
   görevinin hepsi (001-004) `src/Clients/Cashier/wwwroot/**` altında, gerçek müşteri-yüzü QR
   sipariş sayfası değil, Cashier POS ekranı inşa ediyor (doğrulama: her 4 görevin kendi Owned surface
   metni okundu). §2'deki satır ve §6'daki liste düzeltildi; `V13-PUI-004` §6'da hiç yoktu, eklendi.
   **Bilinçli olarak çözülmeden bırakılan soru**: `PUI`'nin de `CUI`'nin `EXC-001` touch-only
   istisnasını alıp almayacağı — bu, bu dokümantasyon-doğruluğu düzeltmesinin tek başına karar
   veremeyeceği ayrı bir erişilebilirlik-politikası kararı, `EXC-001`'in kendisiyle aynı isimli-onaylayan
   incelemesini gerektiriyor.
4. **[High, governance]** `plan/TRACEABILITY.md`'de İKİ ayrı kayıt `C101` etiketini taşıyordu: önceden var
   olan (2026-09-22, `V15-GOV-001` hakkında) ve bu oturumun yeni eklediği (`V13-GOV-008` hakkında).
   Yenisi `C102`'ye yeniden numaralandırıldı (öncekine dokunulmadı). `plan_audit_tool.py`'nin kendi
   traceability kontrolü ID'leri bir Python `set`'e topladığından bu çakışmayı asla yakalayamıyordu —
   `git grep -rn "C101"` ile tüm repo taranıp gerçek 3 referans (`V13-GOV-008`'in kendi task dosyası ×2,
   `plan_audit_tool.py`/`task_scope_tool.py`'deki birer yorum satırı) `C102`'ye düzeltildi;
   `plan/GATES.md`/`V15-GOV-001`'in kendi doğru `C101` referansına dokunulmadı.
5. **[High, governance]** `V13-PAY-003` (commit `1ea043cc`) gerçekte `tools/plan-audit/plan_audit_tool.py`'yi
   değiştirmişti (8 satır — `find_non_final_ancestors`'ın transitive-waiver kontrolündeki gerçek bir
   hatayı düzeltti) ama bu dosya `V13-GOV-008`'in münhasır Owned surface'ı ve `V13-PAY-003`'ün kendi
   task dosyası bu dokunuşu hiçbir yerde (Sınırlı ek dahil) beyan etmiyordu — Acceptance evidence
   metninde düzyazı olarak açıklanmıştı ama Owned surface bölümünde yoktu. Retroaktif olarak doğru
   "Sınırlı ek" satırı eklendi (bkz. o dosyanın kendi Owned surface bölümü).
6. **[Medium, kayıt tutma]** Faz 2'nin 4 commit'i (`1ea043cc`, `6308a8a7`, `e3fc1687`, `763f6ce8`)
   `dotnet restore --force-evaluate`'in mekanik ürünü olarak onlarca `tests/Host/Experience/**/
   packages.lock.json` dosyasına dokundu, bazı görev dosyaları bunu yalnız genel bir cümleyle
   anıyordu. Gerçek sayılar (`git show <hash> --stat | grep -c packages.lock.json`): `1ea043cc`→38,
   `6308a8a7`→49, `e3fc1687`→39, `763f6ce8`→40. Zaten `Done`/push'lanmış görevlerin kendi Owned
   surface'ları tek tek geriye dönük yeniden yazılmadı (bu, kapsamı gizlemek gibi görünebilirdi); bunun
   yerine kayıt burada, tam ve doğru sayılarla tutuluyor.

## Out of scope

- Para güvenliği/mimari kümesi (ayrı, paralel remediation görevi kapsamında).
- `PUI`'nin `EXC-001` istisnasını alıp almayacağı kararı (madde 3'te açıklandığı gibi, ayrı bir karar
  gerektiriyor, burada verilmedi).
- Zaten `Done`/push'lanmış 9 görevin kendi Owned surface metinlerinin `packages.lock.json` churn'ü
  için tek tek geriye dönük yeniden yazılması (madde 6'da açıklandığı gibi, kayıt burada tutuluyor).

## Dependencies

- None

## Deliverables

- `split-payment.js`/`.css`: odak-geri-yükleme + dokunma hedefi düzeltmesi.
- `docs/compliance/accessibility-target.md`: `PUI` sınıflandırma düzeltmesi + remediation notu.
- `plan/TRACEABILITY.md` + 3 kod/plan dosyası: `C101`→`C102` düzeltmesi.
- `V13-PAY-003`'ün task dosyası: retroaktif Owned surface notu.

## Acceptance evidence

- `node --check src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js` → temiz (sözdizimi
  hatası yok). **Gerçek tarayıcıda sürülmedi** — odak-geri-yükleme mantığı kod okuma + sözdizimi
  kontrolüyle doğrulandı, gerçek klavye/switch-access testi yapılmadı; bu açıkça belirtiliyor, iddia
  edilmiyor.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.
- `git grep -rn "C101"` sonrası: yalnız `plan/GATES.md` ve `plan/v1.5/governance/V15-GOV-001-*.md`'nin
  kendi DOĞRU `C101` referansları kaldı; `V13-GOV-008`/`plan_audit_tool.py`/`task_scope_tool.py`'nin
  tamamı `C102`'ye düzeltildi.
- `git show 1ea043cc --stat | grep -c packages.lock.json` = 38, `git show 6308a8a7 --stat | grep -c
  packages.lock.json` = 49, `git show e3fc1687 --stat | grep -c packages.lock.json` = 39,
  `git show 763f6ce8 --stat | grep -c packages.lock.json` = 40 — bizzat çalıştırılıp doğrulandı.

## Handoff

- None
