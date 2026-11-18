# V1-RMD-1171 - Eskimiş dağıtım sözleşmesi testi (test_container_contract.py) güncel compose ile uyuşur

- Task ID: V1-RMD-1171
- Status: InProgress
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

`tests/Deployment/test_container_contract.py` içindeki 4 test, V1-RMD-1167'den önce de kırmızıydı (7 geçer, 4 kalır): parola dışa aktarma satırı sayısı (4 bekler, 2 var), `backup:` alt dizgisinin `alkaros-offsite-backup` ile eşleşmesi, dev overlay ve entrypoint sayımı. Bu görev testleri gerçek compose.yaml'a göre günceller (kasıtlı davranış değişmez, yalnız eskimiş beklentiler düzelir).

Beşinci bulgu (gerçek hata): V1-RMD-700 `app_db_password`, `restore_db_password` ve veritabanı TLS anahtar/sertifika dosyalarını ekledi ama `.dockerignore` yalnız `db_password` ile `admin_password` dosyalarını dışlıyor; `COPY . .` yapan derleme aşamaları bu sırları ara katmana alır. Burada eskimiş olan test değil `.dockerignore`'dır; `.dockerignore` düzeltilir ve test bunu kilitler.

## Owned surface

- `evidence/V1-RMD-1171/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - tests/Deployment/test_container_contract.py
  - .dockerignore
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Her kırmızı testin nedeni compose.yaml'dan doğrulanır; beklenti yalnız compose gerçeğiyle uyuşmuyorsa değişir, test gevşetilmez veya silinmez
2. `.dockerignore` gerçek sır dosyalarının tamamını derleme bağlamından dışlar (`deploy/docker/.gitignore` ile aynı küme); test bu eşitliği kilitler
3. İki bağımsız qa incelemesi, mutasyon (`cp` + `cmp`)

## Out of scope

- compose.yaml, compose.ops.yaml veya betik değişikliği; `.dockerignore` dışında dağıtım dosyası değişikliği

## Dependencies

- V1-RMD-1167

## Acceptance evidence

1. `python -m pytest tests/Deployment/test_container_contract.py` exit code 0; kırmızı ve mutant çıktıları
2. Plan audit ve consistency audit 0 hata 0 uyarı
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None
