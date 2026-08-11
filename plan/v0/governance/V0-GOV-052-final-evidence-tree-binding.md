# V0-GOV-052 - Bind final evidence validation to the checkpoint tree

- Task ID: V0-GOV-052
- Status: InProgress
- Assignee: /root/implement_v0_gov_052
- Work type: implementation
- Surface state: Existing

## Source basis

- CORR:C52
- CORR:C53
- CORR:C54
- CORR:C55

## Goal

`--final-commit` doğrulamasının E checkpoint envelope ve raw evidence bytes'ını
çağrıldığı worktree yerine yalnız E Git tree'sinden okumasını sağlamak; böylece
uncommitted substitution ile yanlış closure sonucu üretmesini engellemek.

## Owned surface

- `tools/evidence-envelope/evidence_envelope_tool.py`
- `tests/Architecture/EvidenceEnvelope/test_evidence_envelope.py`
- `docs/engineering/closure-evidence-envelope.md`
- `plan/VALIDATION_CONTRACT.md`
- `evidence/V0-GOV-052/**`

## In scope

- B→E→F closure doğrulamasında envelope ve bütün raw-output artifact bytes'ını
  E commit tree'sinden `git show` ile okuyup hashlemek.
- E'den sonra worktree'de oluşturulan, değiştirilen veya silinen envelope/raw
  dosyalarının final doğrulama sonucunu etkileyemediğini negatif fixture ile
  kanıtlamak.
- B'de değişen owned non-evidence artifactlardan biri envelope'dan çıkarılırsa
  `SUBJECT_ARTIFACT_SET_MISMATCH` üreten kalıcı negatif regression testi eklemek.
- E'nin yalnız aktif task evidence yolu eklediğini ve F'nin yalnız aktif task
  metadata `Status` geçişini taşıdığını fail-closed doğrulamak.
- Yeni B→E→F zinciriyle kendi tool/test/doc/contract artifactlarını ve raw
  acceptance outputunu commit-tree bağlı olarak kapatmak.

## Out of scope

- `V0-GOV-049` historical `Done` body, evidence veya commitini değiştirmek ya
  da yeniden açmak.
- Ürün behavior, test discovery, admission seti veya başka bir task yüzeyi.

## Dependencies

- V0-GOV-049

## Deliverables

- Commit-tree-only final evidence verifier, worktree substitution negative
  fixture'ı, güncel protocol/contract ve hashli raw checkpoint kanıtı.

## Acceptance evidence

- E tree'sindeki bytes ile worktree substitution bytes farklıyken
  `--final-commit F` non-zero ve deterministic error verir.
- Missing owned artifact fixture'ı `SUBJECT_ARTIFACT_SET_MISMATCH` ile
  fail-closed reddedilir.
- Clean temporary worktree'de `--final-commit F` geçer; B/E/F parent, trailer,
  artifact, raw hash ve metadata-only kuralları birlikte doğrulanır.
- İlgili pytest, plan validation, pre-Done task-scope ve whitespace kontrolü
  exit code `0` verir; raw transcripts `evidence/V0-GOV-052/**` altındadır.

## Handoff

- V0-GOV-046
- V0-GOV-050
- V0-GOV-051
- V0-GOV-045
- V0-GOV-048
