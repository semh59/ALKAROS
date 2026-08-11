# V0-GOV-049 - Reaccept the closure-evidence protocol

- Task ID: V0-GOV-049
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Existing

## Source basis

- CORR:C52
- CORR:C53
- CORR:C54

## Goal

Kapanış kanıtını self-binding, fail-closed ve final committen yeniden
oynatılabilir hale getirmek; `V0-GOV-039`un tarihsel evidence'ını değiştirmeden
aynı kusurların tekrarını önlemek.

## Owned surface

- `tools/evidence-envelope/evidence_envelope_tool.py`
- `tests/Architecture/EvidenceEnvelope/test_evidence_envelope.py`
- `docs/engineering/closure-evidence-envelope.md`
- `plan/VALIDATION_CONTRACT.md`
- `evidence/V0-GOV-049/**`

## In scope

- `B` subject commit → `E` evidence checkpoint → `F` metadata-only final
  closure zincirini ve `F` trailerlarındaki `Closure-Subject` ile
  `Closure-Evidence-Checkpoint` bağlarını doğrulamak.
- Envelope'un aktif taskın bütün non-evidence artifactlarını, command, exit
  code, environment ve hash alanlarını bağlamasını; kendi tool/test/doc
  değişikliklerini atlayamamasını sağlamak.
- `Authorization: Bearer <value>` ve `api key: <value>` dahil command/raw
  transcript secret sızıntılarını fail-closed reddetmek.
- Geçici worktree create/remove command ve exit kayıtlarını redacted raw
  transcript, hash ve cleanup sonucu olarak envelope'a bağlamak.
- `V0-GOV-035`, `V0-GOV-037`, `V0-GOV-039` ve `V1-IAM-005` historical
  kanıtlarını immutable-invalid olarak sınıflamak; değiştirmemek veya başarı
  iddiasına dönüştürmemek.

## Out of scope

- Mevcut `Done` task status/assignee veya historical evidence/commit geçmişini
  değiştirmek.
- Ürün davranışı, test project discovery veya admission setini değiştirmek.

## Dependencies

- V0-GOV-035
- V0-GOV-039

## Deliverables

- Final-commit verifier, gerçek historical-invalid fixture'lar, closure
  protocol sözleşmesi ve redacted raw evidence checkpoint'i.

## Acceptance evidence

- Missing/stale/hash-mismatch/narrative-only/secret-leakage fixtures;
  Bearer ve API-key biçimleri dahil, fail-closed reddedilir.
- `--final-commit F` yalnız `B → E → F` parent/trailer/artifact chain'i ve
  metadata-only final diff'i doğrular.
- `F` için task body outside metadata veya evidence payload hash'iyle
  self-reference oluşmaz.
- Gerçek `V0-GOV-035` historical kaydı stale/hash mismatch olarak bulunur;
  eski dosya değiştirilmez.
- İlgili pytest, plan validation, pre-Done task-scope ve whitespace kontrolü
  exit code `0` verir; raw transcripts `evidence/V0-GOV-049/**` altındadır.

## Handoff

- V0-GOV-046
- V0-GOV-050
- V0-GOV-051
