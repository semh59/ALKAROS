# Kapanış Kanıt Zarfı Sözleşmesi

Bir görev `Done` yapılmadan önce kanıt, yalnız anlatı veya elle yazılmış bir
özet olamaz. `tools/evidence-envelope/evidence_envelope_tool.py`, JSON
biçimindeki kapanış zarfını doğrular ve herhangi bir eksiklikte non-zero exit
ile fail-closed sonuç üretir.

## Şema

Kök nesne aşağıdaki alanların tamamını ve yalnız bunları taşır:

```json
{
  "schema": "alkaros.closure-evidence-envelope/v1",
  "task_id": "V0-GOV-039",
  "candidate_commit": "40-or-64-lowercase-git-hex",
  "environment": {
    "platform": "Windows 10",
    "toolchain": { "python": "3.12.12" },
    "variables": { "CI": "false" },
    "secrets": [
      {
        "location": "env:ALKAROS_TEST_PG_PASSWORD",
        "fingerprint": "sha256:<64-lowercase-hex>"
      }
    ]
  },
  "commands": [
    {
      "command": "py -m pytest tests/Architecture/EvidenceEnvelope -q",
      "exit_code": 0,
      "raw_output": {
        "path": "evidence/V0-GOV-039/raw/pytest.txt",
        "sha256": "<64-lowercase-hex>"
      }
    }
  ],
  "artifacts": [
    {
      "path": "tools/evidence-envelope/evidence_envelope_tool.py",
      "sha256": "<64-lowercase-hex>"
    }
  ],
  "integrity": { "payload_sha256": "<64-lowercase-hex>" }
}
```

`integrity.payload_sha256`, `integrity` alanı hariç kök nesnenin UTF-8,
anahtarları sıralı ve boşluksuz JSON gösteriminin SHA-256 değeridir. Böylece
zarf alanında yapılmış bir değişiklik hash güncellenmeden görünür olur. Git
commit nesnesi ise zarfın saklandığı kapanış kaydını immutable history içinde
ayrı olarak bağlar; zarfın kendi hash'i Git tarihini yeniden yazmaya yetki
vermez.

## Candidate ve artifact doğrulaması

`candidate_commit`, acceptance komutlarının çalıştırıldığı kaynak bloblarını
içeren gerçek Git commit'idir. Her `artifacts` kaydı bu commit'ten byte olarak
okunur; SHA-256 değeri kaydedilen değerle eşleşmelidir. Aynı artifact candidate
ile mevcut `HEAD` arasında değişmişse candidate stale sayılır ve zarf reddedilir.

Bu kural V0-GOV-035'in tarihsel kaydındaki hatayı yakalar: verification
dosyasındaki `1d41e97b39ac975ab55c2bdf4198b0d6b92681ed` SHA'sı, görevin altı
source/test/contract blob'unu değiştiren `78b317a5c3d04009d94394da58c5913d59c22b91`
kapanış commit'inin parent'ıdır. Kaydedilen final SHA-256 değerleri bu candidate
tree'de değildir. Bu tarihsel kayıt değiştirilmez; validator bu tür zarfı
`STALE_CANDIDATE_COMMIT` ve `FINAL_BLOB_HASH_MISMATCH` ile reddeder.

## Raw çıktı ve secret koruması

Her komut `command`, integer `exit_code: 0` ve task'ın kendi
`evidence/<Task-ID>/` dizininde hash'i doğrulanabilir raw çıktıyı taşır.
Anlatı tek başına şema değildir. Secret değerleri `environment.variables`,
komut veya raw çıktıda bulunamaz. Sensitive girişler yalnız `env:<NAME>`
konumu ve SHA-256 fingerprint'iyle kaydedilir; değer asla yazılmaz.

## Tarihsel acceptance replay

Mevcut `Done` task doğrudan task-scope acceptance için executable değildir.
Replay, geçici bir Git worktree'de, code ve acceptance testinin bulunduğu
tarihsel candidate commit'te yapılır:

```text
git worktree add --detach <outside-repository-path> <candidate-commit>
Set required non-secret environment values and supply secrets only through the process environment.
Run the task acceptance command and capture raw stdout/stderr under the active task's evidence directory.
git worktree remove --force <outside-repository-path>
```

V1-IAM-005 için 2026-08-04 candidate commit
`9528f783e26a1248d490c28b1989556fec5fcbf7` code blobs'unu taşır ve login
source yolları bu commit ile mevcut `HEAD` arasında değişmemiştir. Bu commit
aynı zamanda task metadata'sını `Done` yaptığı için pre-Done task-scope replay
kanıtı üretmez; bu ayrı tarihsel kusur kabul edilmez. Acceptance replay yalnız
task'ın gerçek `dotnet test ALKAROS.slnx --no-restore -v q` koşulunu bu geçici
worktree'de çalıştırır. Candidate bulunamaz veya ortam kurulamazsa görev
`Blocked` kalır; başarılı sonuç uydurulmaz.

## Kullanım

```text
py -B tools/evidence-envelope/evidence_envelope_tool.py --envelope evidence/<Task-ID>/closure-evidence-envelope.json --repository . --format text
```
