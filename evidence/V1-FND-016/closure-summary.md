# V1-FND-016 markdownlint kök remediasyonu — kapanış kanıtı

Tarih: 2026-08-12
Yürütme: opencode-v1-fnd-016

## 1. Kapsam ve yöntem

Bağımsız denetim bulgu listesi (AUDIT:20260810-110911-825882aa) kök markdownlint
hatalarını 29 dosyada raporladı (tekil küme 173; markdownlint-cli2@0.23.2,
config globs: `plan/**`, `docs/**`, `evidence/**`, `AGENTS.md`). Bu görev yalnız
biçim düzeltmesi yaptı; cümle, sayı ve kanıt anlamı korundu.

Hata dağılımı (başlangıç): MD060:60, MD040:45, MD013:20, MD032:12, MD047:8,
MD004:7, MD036:6, MD031:5, MD022:2, MD038:2, MD058:1, MD056:1, MD009:1,
MD033:1, MD025:1, MD034:1 → toplam 173.

## 2. Komut kanıtları

Komut: `npx.cmd --yes markdownlint-cli2@0.23.2`

Başlangıç (evidence/V1-FND-016/markdownlint_before.txt):
Summary: 173 issues in 29 files

Kapanış:
Summary: 0 issues in 0 files
exit=0

Komut: `python tools/plan-audit/plan_audit_tool.py validate`
Validation errors: 0, Validation warnings: 0, exit=0

Komut: `python tools/plan-audit/plan_audit_tool.py verify-manifest`
Manifest errors: 0, exit=0

Komut: `python tools/plan-audit/plan_audit_tool.py generate-audit-report`
Audit findings recorded: 1827, exit=0

Komut: `python tools/plan-audit/plan_audit_tool.py generate-manifest`
Manifest SHA-256: CB0632599A66B089A1A551F4FB9C15EAE7D7EAED99323BD4FD70129625B8EBCC, exit=0
(kayıt anındaki üretim değeri; ikili üretim→kayıt döngüsü nedeniyle sonraki üretimlerde değişebilir)

## 3. Düzeltilen dosyalar ve kural bazında özet

- evidence/ENV-001/env001-sdk-repair.md — MD034 (bare URL → angle brackets)
- evidence/ENV-003/env003-test-matrix.md — MD033 (inline HTML → backtick)
- evidence/V0-GOV-030/verification.md — MD040 ×2 (fence dil etiketi)
- evidence/V0-GOV-031/verification.md — MD040 ×2
- evidence/V1-FND-001/defect-1-closure.md — MD040 ×2, MD060 ×4
- evidence/V1-FND-001/verification.md — MD040 ×6 (fence ` ```text `)
- evidence/V1-FND-002/defect-3-closure.md — MD040, MD060 ×4
- evidence/V1-FND-002/defect-4-closure.md — MD040, MD060 ×4
- evidence/V1-FND-002/defect-6-closure.md — MD009 (trailing space), MD040, MD060 ×4
- evidence/V1-FND-003/verification.md — MD013 ×5 (satır sarması), MD032, MD047, MD060 ×4
- evidence/V1-FND-004/verification.md — MD013 ×5, MD032 ×3,
  MD036 ×3 (kalın metin → başlık), MD047, MD060 ×4
- evidence/V1-FND-005/defect-5-closure.md — MD040, MD060 ×4
- evidence/V1-FND-005/verification.md — MD013 ×5, MD032 ×3, MD036 ×3, MD047
- evidence/V1-FND-009/closure-report-2026-08-05.md — MD004 ×7 (devam satırı girintisi + `+` liste işareti), MD032
- evidence/V1-FND-013/verification.md — MD040 ×3
- evidence/V1-FND-014/verification.md — MD040 ×3
- evidence/V1-FND-015/verification.md — MD040 ×3
- evidence/V1-IAM-001/closure-2026-08-05.md — MD032 (liste öncesi boş satır), MD040
- evidence/V1-IAM-001/defect-7-closure.md — MD013 ×4, MD032, MD040 ×2, MD060 ×10
- evidence/V1-IAM-002/closure-2026-08-08.md — MD025 (ikinci H1 → H2), MD040 ×2, MD047, MD060 ×6
- evidence/V1-IAM-003/audit-slnx-regression-2026-08-09.md — MD047
- evidence/V1-IAM-003/closure-2026-08-08.md — MD022 ×2 (başlık altı boş satır),
  MD031 ×5 (fence çevresi boş satır), MD032, MD040 ×5, MD047, MD058, MD060 ×4
- evidence/V1-IAM-003/manifest-recover-2026-08-09.md — MD040 ×2, MD047
- evidence/V1-IAM-004/closure-2026-08-08.md — MD040, MD047
- evidence/V1-IAM-005/verification.md — MD040 ×3
- evidence/V1-SEC-003/closure-2026-08-05.md — MD032, MD040 ×2
- evidence/V1-SEC-003/defect-2-closure.md — MD040 ×2, MD060 ×4
- plan/TRACEABILITY.md — C46 satırı MD038 ×2 / MD056 / MD060 ×8 (süslü
  parantez içindeki pipe'lar `\|` escape; apostrof code span içine alındı);
  C52 kayıt satırı mevcut
- plan/v0/governance/V0-GOV-031-c42-entry-gate-approval.md — MD013 (satır sarması)
- plan/AUDIT_REPORT.md — araçla yeniden üretildi (generate-audit-report)
- plan/AUDIT_MANIFEST.json — araçla yeniden üretildi (generate-manifest)

## 4. Kapanış doğrulaması

- markdownlint: 0 issues / 0 files, exit 0 (kapanış koşusu üretim sonrası tekrar doğrulandı)
- plan-audit validate: 0 hata, 0 uyarı, exit 0
- plan-audit verify-manifest: 0 hata, exit 0
- AUDIT_REPORT/MANIFEST üretim sırası: report → manifest (V0-GOV-030 ve C51 emsali)
- Git kapanış snapshot'ı: evidence/V1-FND-016/git_closing_snapshot.txt
  (görevin own edilmiş yolları dışında görev kaynaklı değişiklik yok;
  kapanış anında zaten mevcut olan diğer değişiklikler başka oturumların
  (V1-TBL-001 dahil) uncommitted işidir; bu görev onlara dokunmadı)

## 5. Bağımsız denetim düzeltmeleri (2026-08-13)

- `plan_audit_verify_manifest.txt`: kapanışta kaydedilen dosya ara-durum
  çıktısıydı ("Manifest errors: 6"); gerçek kapanış sonrası 0-hata çıktısıyla
  yeniden kaydedildi (exit 0).
- `git_closing_snapshot.txt`: UTF-16 LE (BOM) kodlamasından UTF-8'e çevrildi;
  içerik korundu.
- "Diğer değişiklikler kullanıcı V1-TBL-001 işine aittir" ifadesi, kapanış
  anında başka oturumların da uncommitted işi mevcut olduğu için netleştirildi.
- Bölüm 3 kural özetleri `markdownlint_before.txt`'ten (ground truth) yeniden
  üretildi: hayali `MD010 ×6`/`MD022` ibareleri kaldırıldı, MD060 ve diğer
  per-file sayıları gerçek hata listesiyle eşitlendi (29 dosya, 173 hata).

## Sonuç

Tüm acceptance kriterleri karşılandı: lint 0/0, validate 0, verify-manifest 0.
Görev Done.
