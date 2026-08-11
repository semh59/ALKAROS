# Kapanış Kanıt Zarfı Sözleşmesi

Bir görev `Done` olmadan önce kanıt anlatı veya elle yazılmış özet olamaz.
`tools/evidence-envelope/evidence_envelope_tool.py`, JSON zarfını ve v2 kapanış
zincirini doğrular; her ihlal non-zero exit ile fail-closed olur.

## V2 zarf şeması

Zarf, E evidence checkpoint içinde şu alanları ve yalnız bu alanları taşır:

```json
{
  "schema": "alkaros.closure-evidence-envelope/v2",
  "task_id": "V0-GOV-049",
  "subject_commit": "<B full Git SHA>",
  "environment": { "platform": "Windows", "toolchain": {}, "variables": {}, "secrets": [] },
  "commands": [{ "command": "...", "exit_code": 0, "raw_output": { "path": "...", "sha256": "..." } }],
  "artifacts": [{ "path": "...", "sha256": "..." }],
  "integrity": { "payload_sha256": "..." }
}
```

`integrity.payload_sha256`, `integrity` hariç kök nesnenin sıralı, boşluksuz
UTF-8 JSON gösteriminin SHA-256 değeridir. Zarf F SHA'sını veya F'nin payload
hash'ini taşımaz; bu nedenle F öncesi kanıtta self-reference kurulamaz.

## B → E → F protokolü

`--final-commit F` yalnız aşağıdaki üç bitişik committen oluşan zinciri kabul eder:

1. B, aktif taskı `Planned` durumundan gerçek assignee ile `InProgress` durumuna
   taşır ve aktif taskın bütün non-evidence `Owned surface` artifactlarını değiştirir.
2. E, B'nin doğrudan çocuğudur ve yalnız `evidence/<Task-ID>/` altındaki raw
   çıktıları ve `closure-evidence-envelope.json` zarfını ekler.
3. F, E'nin doğrudan çocuğudur; yalnız aktif task dosyasında `Status: InProgress`
   satırını `Status: Done` yapar.

F'nin son trailer bloğu Git tarafından ayrıştırıldığında aşağıdaki dört satır,
bu sırayla ve bitişik olarak bulunur:

```text
Task: V0-GOV-049
Gate: GATE-V0-EXIT
Closure-Subject: <B full Git SHA>
Closure-Evidence-Checkpoint: <E full Git SHA>
```

V2 validator, B'de değişen owned artifactların zarf listesinin tamamı ve tam
kümesi olduğunu; B blob hashlerinin E ve F'de stale olmadığını da kontrol eder.
Bu kural validator'ın kendi tool, test, doküman veya plan sözleşmesi değişikliğini
atlamayı reddeder.

## V3 interrupted `V1-FND-023` closure

V3 yalnız `V1-FND-023` için, immutable B0
`fd3344f15c5257b53bf5281ee9129f800c62f0a7` ve direct-child interruption
`479881636c8142c7161f2d5980d37ca2f9b48591` ile uygulanır. Bu, başka task,
subject veya interruption için genel bir closure istisnası değildir.

V3'ün immutable kaynak zinciri `B0 → interruption`dır. Reentry ancak bu
interruption'ın descendant'ı olan geçerli `V0-GOV-060` v2 finalinin direct child'ı
`A` ile başlar; ardından `A → E → F` gelir. Validator B0 parent'ını, B0'ın exact
değişen path/bloblarını ve interruption'ın yalnız exact `InProgress`→`Blocked`
metadata + `Blocker` diff'ini byte/diff/topology ile doğrular. A yalnız exact
blocker'ı kaldırıp task'ı `Blocked`den `InProgress`e geçirir. E, A'nın direct
child'ı olarak yalnız `evidence/V1-FND-023/**` ekler; F, E'nin direct child'ı
olarak yalnız task statusunu `Done` yapar.

B0'nın iki source artifact blobu `V0-GOV-060` finalinde ve A/E/F'de değişemez
veya stale olamaz. E zarfı bu iki source artifact'ın SHA-256 değerlerinin tam
kümesini taşır. F'nin son contiguous trailer bloğu `Task`, `Gate`,
`Closure-Subject`, `Closure-Interruption`, `Closure-Reentry` ve
`Closure-Evidence-Checkpoint` alanlarını full SHA ile bu sırada taşımalıdır.
Wrong SHA, task, parent, diff, blob, evidence path veya trailer deterministic
fail-closed sonuç üretir. `V1-FND-023` `Done` admission'ı current `HEAD` için
doğrudan task-specific v3 verifier'ı çağırır; generic geçerli bir v2 final veya
V0 gate'in açık/kapalı olması v3 kontrolünün yerine geçmez.

`--final-commit` zarfı ve her kayıtlı raw output'u yalnız `E:<path>` Git
blobundan okur; çağrıldığı worktree bu bytes'ların kaynağı değildir. Aynı zarf
veya kayıtlı raw yolunda worktree byte'ı E blobundan farklıysa validator
`WORKTREE_EVIDENCE_SUBSTITUTION` ile reddeder. Böylece uncommitted değiştirme,
silme veya yeniden yazma sahte bir başarı sonucu üretemez.

## Raw çıktı ve secret koruması

Her command kaydı komut, integer `exit_code: 0` ve taskın kendi evidence dizinindeki
hash'i doğrulanabilir raw çıktıyı taşır. Raw transcript ve command içinde secret
değeri olamaz. `Authorization: Bearer <value>` ve `api key: <value>` biçimleri de
secret leakage sayılır. Sensitive environment girdileri yalnız redacted `env:<NAME>`
konumu ve SHA-256 fingerprint ile yazılır.

Geçici worktree create/remove işlemleri de redacted komut, exit code, raw output
hash ve cleanup sonucu ile E altına kaydedilir. Raw Git text dosyaları LF ile yazılır.

## Tarihsel kayıtlar

V0-GOV-035, V0-GOV-037, V0-GOV-039 ve V1-IAM-005 kapanışları C53 uyarınca immutable
historical exception'dır. Eski evidence veya history değiştirilmez. Legacy v1 zarfı
doğrulandığında V0-GOV-035 için bilinen stale candidate ve blob-hash mismatch
fail-closed raporlanır; bu sonuç yeni bir başarı iddiası değildir.

## Kullanım

```text
py -B tools/evidence-envelope/evidence_envelope_tool.py --envelope evidence/<Task-ID>/closure-evidence-envelope.json --repository . --format text
py -B tools/evidence-envelope/evidence_envelope_tool.py --final-commit <F full Git SHA> --repository . --format text
py -B tools/evidence-envelope/evidence_envelope_tool.py --historical-v0-gov-035 --repository . --format text
```
