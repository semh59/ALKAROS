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
