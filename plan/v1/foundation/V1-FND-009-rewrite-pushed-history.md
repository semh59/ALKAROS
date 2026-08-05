# V1-FND-009 - Rewrite pushed history with footers and force-push

- Task ID: V1-FND-009
- Status: Done
- Assignee: opencode-v1-fnd-009
- Work type: implementation
- Surface state: Existing

## Source basis

- CORR:C31

## Goal

V1-FND-008 kapanışında Out of scope bırakılan push edilmiş aralığı (`fc5ae22..8374fc3`, 9 commit) kullanıcı
talimatı "DÜZELT" (2026-08-01) kapsamında düzeltmek: her commit gerçek sahipliğine göre `Task:` veya `Gate:`
footer'ı taşır; `RepositoryCommit` pin'i güncellenir; `origin/master` force-push ile yeni geçmişe alınır.
Yalnız `fc5ae22` (kök baseline, konvansiyon öncesi) kayıtlı istisnadır ve kurgusal atıf almaz.

## Owned surface

- `plan/v1/foundation/V1-FND-009-rewrite-pushed-history.md` (kendi metadata dosyası, otomatik)
- `plan/TRACEABILITY.md`
- `plan/GATES.md`
- `plan/VALIDATION_CONTRACT.md`
- `plan/AUDIT_REPORT.md`
- `plan/AUDIT_MANIFEST.json`
- `Directory.Build.props`
- `docs/versioning-strategy.md`
- `evidence/V1-FND-009/**`
- Bu görev notları yalnız kendi `evidence/V1-FND-009/**` dizinine yazar (başka görev kanıt dizinlerine yazılmaz).
- Bu görev, başka bir task'ın owned surface alanını değiştiremez; aşağıdaki geçmiş yeniden yazımı 2026-08-01
  kullanıcı onaylı plan değişikliğidir.

## In scope

- Push edilmiş 9 commit'in yeniden yazımı (footer atıfları, commit mesajındaki görev listesi ve kayıtlı oturum
  gerçeğine dayanır; 2026-08-01 tam geçmiş denetim sonucuna göre):
  - `655d0b2`: mevcut `Task: V0-DOM-001` footer'ı korundu; SHA değişmedi. Commit-anı yüzeyi
    `docs/versioning-strategy.md`
    içermediği için denetim VIOLATION raporlar — FIND-IA-0040 kaydıyla örtüşür (kayıtlı istisna).
  - `662e466`: mesajı zaten `Gate: GATE-V0-ENTRY` trailer'ı taşıyordu → yeniden yazılmadı; denetim GATE_OK.
  - `72db2fa`: `Task: V0-DOM-007` eklendi (dosya seti V0-DOM-007 yüzeyiyle birebir örtüşür).
  - `70bf442`: mesajı zaten `Gate: GATE-V0-ENTRY` trailer'ı taşıyordu → yeniden yazılmadı; denetim GATE_OK.
  - `df7605c`: `Gate: GATE-V0-EXIT` eklendi (gate closure commit'i; konvansiyon `Gate:` footer'ını tanımlar).
  - `0f4f537`: `Gate: GATE-V0-EXIT` eklendi (gate kapanış ön koşulu olan plan geneli metadata güncellemesi).
  - `1784dc5`: `Task: V1-FND-003` eklendi (kayıtlı oturum gerçeği FIND-IA-0042); yüzey dışı artıkları
    FIND-IA-0051 kaydı.
  - `8374fc3`: `Task: V1-FND-003` eklendi (aynı oturumun infra chore commit'i); `.gitignore` ve `tmp/**`
    artıkları FIND-IA-0051 kaydı.
  - `fc5ae22`: kök baseline commit'i; konvansiyon öncesi ve hiçbir görev yüzeyine atfedilemez — footer
    eklenmedi, FIND-IA-0050 kaydıyla istisna.
- Tag doğrulaması: `gate/v0-entry` ve `v0.0.0` tag'leri `655d0b2` commit'ine işaret ediyordu; commit SHA
  değişmediği için taşıma gerekmedi (annotated tag objeleri `39ccb50`/`e302c4c`).
- `docs/versioning-strategy.md` Current State tablosunun gerçek tag commit'leriyle düzeltilmesi (V0-DOM-001
  sahipliğinde; plan değişikliği kapsamı).
- `Directory.Build.props` `RepositoryCommit` pin'inin yeni HEAD commit'ine güncellenmesi.
- `origin/master`'a force-push (kullanıcı onayı: "DÜZELT", 2026-08-01).
- Sınır denetimi scriptinin `Gate:` footer desteğiyle güncellenmesi ve tam geçmiş (25 commit) denetimi.
- Governance: GATES/VALIDATION_CONTRACT FND-009 notu, TRACEABILITY FIND-IA-0050/0051 kayıtları,
  `evidence/V1-FND-009/**` altındaki tarihli düzeltme notları, AUDIT_REPORT/MANIFEST yeniden üretimi.

## Out of scope

- Batch commit'lerin (`662e466`, `70bf442`) çoklu-görev içeriğinin commit'lere bölüştürülmesi: geçmiş yeniden
  bölünmez; tek footer + FIND-IA-0051 kaydı.
- `fc5ae22` kök baseline'a kurgusal Task ID atfı.
- Remote'da var olmayan tag'lerin push'u: tag'ler yalnız local'dir (doğrulandı), yeniden konumlanır ama
  push edilmez.

## Dependencies

- V1-FND-008
- V0-DOM-001

## Deliverables

- `Task:`/`Gate:` footer'lı tam geçmiş (25 commit, yalnız `fc5ae22` kayıtlı istisna).
- Doğrulanmış `gate/v0-entry` ve `v0.0.0` tag konumları; güncel `docs/versioning-strategy.md` tablosu.
- Güncel `RepositoryCommit` pin'i; force-push edilmiş `origin/master`.
- `evidence/V1-FND-009/**` altında komut çıktıları, exit code'lar ve force-push kanıtı.

## Acceptance evidence

- Sınır denetimi scripti tam geçmişte (25 commit): `fc5ae22` dışında footer'sız commit 0; VIOLATION yalnız
  kayıtlı istisnalar — FIND-IA-0040 (`655d0b2` docs/versioning-strategy.md), FIND-IA-0045..0048 (b25e91f,
  7e6ca26, 92a5372, d88d9fa), FIND-IA-0050 (`fc5ae22` NO_FOOTER), FIND-IA-0051 (0c37dc6, 36c06cf).
- `validate` 0 hata, `verify-manifest` 0 hata, markdownlint 0 sorun.
- Task-scope validator: FND-009 worktree ve diff modu `valid: true`.
- `dotnet build` ve çözüm testleri 0 hata; pytest seti yeşil.
- `origin/master` = yeni HEAD; `git log origin/master..HEAD` boş.
- Kapanış write-set'i allowlist ile birebir eşleşir; commit'ler `Task: V1-FND-009` footer'ı taşır.

## Handoff

- V1-FND-002
- GATE-V1-EXIT
