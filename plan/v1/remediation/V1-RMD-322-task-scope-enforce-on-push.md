# V1-RMD-322 - `task-scope.yml`'nin Owned-surface kontrolü yalnız PR'da çalışıyordu

- Task ID: V1-RMD-322
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

Bağımsız 13 ajanlı derin denetimin (2026-09-26) K13 bulgusu: `.github/workflows/task-scope.yml`'nin `enforce` job'ı `if: github.event_name != 'push'` koşuluyla yalnız `pull_request`/`workflow_dispatch` olaylarında çalışıyordu. Bu depodaki gerçek iş akışı (bu oturumun kendi geçmişi dahil) hiçbir zaman pull request kullanmıyor, doğrudan `master`'a push ediyor — yani Owned-surface denetimi bu repodaki TEK BİR gerçek değişiklikten bile hiç geçmemişti.

## Owned surface

- `plan/v1/remediation/V1-RMD-322-task-scope-enforce-on-push.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): .github/workflows/task-scope.yml

## In scope

1. `enforce` job'ının `if: github.event_name != 'push'` koşulu kaldırılır — artık push'ta da çalışır.
2. Task ID çözümlemesi: bir push için `head_commit.message`'dan `V\d+-[A-Z]+-\d+` deseniyle eşleşen HER görev kimliği toplanır (yalnız ilki değil) — bu oturumun kendi pratiği bir commit'te birden fazla göreve ait değişikliği birleştirebiliyor (örn. "K3/K4/K9" gibi 3 ayrı plan görevini tek commit'te kapatmak).
3. Doğrulama adımı: her toplanan görev kimliği için `task_scope_tool.py` ayrı ayrı çalıştırılır (araç kendi tasarımı gereği tek-görev modeli kullanıyor, bu değiştirilmedi); bir yol yalnız HİÇBİR görevin Owned surface'ı onu kapsamıyorsa gerçek bir ihlal sayılır (tüm çalıştırmaların bulgu kümelerinin KESİŞİMİ).

## Out of scope

- `task_scope_tool.py`'nin kendisinin çok-görevli bir modele geçirilmesi — bu görev yalnız CI iş akışı katmanında (PowerShell) birleştirme yapıyor, aracın kendi tek-görev-bazlı iç mantığına dokunmadı.
- K11 (`plan_audit_tool.py`'nin kanıt metninin doğruluğunu değil biçimini kontrol etmesi) ve K12 (ayrı görev, V1-RMD-321) — ayrı bulgular.

## Dependencies

- None

## Acceptance evidence

GitHub Actions şu an faturalandırma sorunu nedeniyle çalışmıyor (peer oturumun 2026-09-26 tarihli bildirimi) — bu yüzden canlı CI'da doğrulanamadı, dürüstçe not ediliyor. Bunun yerine:

- YAML sözdizimi `python -c "import yaml; yaml.safe_load(...)"` ile doğrulandı — geçerli.
- Kesişim mantığının kendisi GERÇEK PowerShell'de (Windows PowerShell 5.1, `pwsh`'in ayrı bir sürümü ama aynı `ConvertFrom-Json`/dizi indeksleme davranışı), gerçek `task_scope_tool.py` çıktısına karşı doğrulandı: `V1-RMD-317` (yalnız `KitchenTicket.cs`'i sahiplenen) ve `V1-RMD-318` (yalnız `SentItemVoidStore.cs`'i sahiplenen) için, her iki dosyada da bilerek değişiklik yapılıp çalıştırıldı — her görevin kendi bulgu kümesi diğerinin dosyasını "ihlal" olarak işaretledi ama KESİŞİM doğru şekilde ikisini de dışladı (yalnız hiçbir görevin sahiplenmediği dosyalar kesişimde kaldı). Tek-görev durumu da ayrıca doğrulandı (kendisiyle kesişim, no-op).
- İlk taslak `1..($n-1)` PowerShell aralık dilimlemesi kullanıyordu — tek görevli durumda bu `1..0` (AZALAN bir aralık, `[1,0]`) üretiyor, dizi sınırları dışına taşıyor ve PowerShell sürümüne göre farklı davranabilirdi; basit bir artan `for` döngüsüne değiştirildi, aynı testlerle tekrar doğrulandı.

## Handoff

- None
