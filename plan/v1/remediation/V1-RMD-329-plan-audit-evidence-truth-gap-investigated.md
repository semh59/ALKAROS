# V1-RMD-329 - `plan_audit_tool.py`'nin kanıt doğruluğu boşluğu araştırıldı; tam kapanış ayrı bir görev

- Task ID: V1-RMD-329
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

Bağımsız 13 ajanlı derin denetimin (2026-09-26) K11 bulgusu: `plan_audit_tool.py`'nin `validate` komutu bir görevin "Acceptance evidence" bölümünün var olup olmadığını, Türkçe olup olmadığını, yasaklı ifade içerip içermediğini kontrol ediyor; ama içeriğin *doğruluğunu* (örn. "23/23 test geçti" iddiasının gerçek olup olmadığını) hiçbir yerde doğrulamıyor. Tek istisna, tek bir görev için özel yazılmış bir git-commit kontrolü (`v3_interrupted_closure_errors`, yalnız `V1-FND-023`'e bağlı).

## Owned surface

- `plan/v1/remediation/V1-RMD-329-plan-audit-evidence-truth-gap-investigated.md`
- Kod değişikliği yok (bu görevin kendisi — bkz. Acceptance evidence).

## In scope

1. Bulgunun somut, ölçülebilir bir versiyonunu inşa edip KURU ÇALIŞTIRMA yapmak: "Acceptance evidence" metninde `ALKAROS.*.Tests` biçiminde adı geçen bir test projesinin GERÇEKTEN var olup olmadığını (dosya sisteminde bir `.csproj` olarak) doğrulayan bir kontrol.
2. Bu kontrolün TÜM `plan/` ağacına karşı ne bulduğunu kaydetmek.

## Out of scope

1. Kontrolün gerçek bir `validate` hatası/uyarısı olarak eklenmesi — kuru çalıştırma 12 farklı, ZATEN `Done` kapanmış, bu görevle tamamen ilgisiz göreve ait "hayalet" test projesi referansı buldu (aşağıya bkz.). Bunları tek tek araştırmadan (her biri için: kanıt mı uydurulmuş, yoksa proje daha sonra mı yeniden adlandırıldı/birleştirildi?) bir sert kapı eklemek ya (a) haksız yere 12 alakasız, geçmiş görevi kırmızıya çevirir ya da (b) düşünülmeden eklenen bir muafiyet listesiyle bulgunun asıl amacını (gerçek uydurma kanıtı yakalamak) baştan yener. Bu, K11'in kendisinden daha büyük, bağımsız bir araştırma görevi gerektiriyor — ayrı, kendi Owned surface'ı olan bir görev olmalı.
2. Genel, herhangi bir serbest metin iddiasını (örn. "X/Y test geçti" sayısının doğruluğu) doğrulayan bir mekanizma — yapılandırılmış, makine-okunur bir kanıt formatı (örn. her kapanışın gerçek bir CI çalışma kimliğine/test sonucu dosyası özetine referans vermesi) gerektirir; bu, tüm `plan/` sistemini etkileyen ayrı, büyük bir tasarım kararı.

## Dependencies

- None

## Acceptance evidence

Bulgunun gerçekliği somut olarak doğrulandı — kuru çalıştırılan kontrol (`ALKAROS\.[A-Za-z0-9]+(?:\.[A-Za-z0-9]+)*\.Tests` deseniyle "Acceptance evidence" metninde geçen bir proje adının `tests/**/*.csproj` altında GERÇEKTEN var olup olmadığını kontrol eden bir Python betiği) `plan/` ağacının tamamına karşı çalıştırıldı:

- 143 gerçek test projesi (`tests/**/*.csproj`) bulundu.
- 12 FARKLI, gerçekten var OLMAYAN proje adı, en az bir görev dosyasının metninde geçiyor: `ALKAROS.WaiterPwa.SessionQueue.Tests`, `ALKAROS.Cashier.Production.Tests`, `ALKAROS.Cashier.InventoryPurchasing.Tests`, `ALKAROS.Cashier.MenuRecipeAdmin.Tests`, `ALKAROS.Host.MigrationComposition.Manifest.Tests`, `ALKAROS.Transactions.Tests`, `ALKAROS.TransactionOutboxIntegration.Tests`, `ALKAROS.Idempotency.Tests`, `ALKAROS.WaiterPwa.ManagerDecisions.Tests`, `ALKAROS.Host.Experience.Orders.Tests`, `ALKAROS.Cashier.OperationsStatus.Tests`, `ALKAROS.Cashier.TableShell.Tests` (dosyalar: `plan/GATES.md`, `plan/TRACEABILITY.md`, ve `V11-UI-001/002/003`, `V1-BIL-004`, `V1-FND-011`, `V1-FND-019`, `V1-IAM-020`, `V1-IAM-024`, `V1-RMD-135`).

Bu, K11'in kendi iddiasının GERÇEK, ölçülebilir bir kanıtı: bu 12 referansın HER BİRİ ya (a) bir görev kapandıktan sonra yeniden adlandırılmış/birleştirilmiş bir proje, ya da (b) bulgunun tam tarif ettiği şey — hiç doğrulanmamış bir iddia. Hangisi olduğunu tek tek belirlemek bu görevin kendi kapsamının dışında bırakıldı (yukarı bkz.), ama somut liste gelecekteki bir görevin başlangıç noktası olarak burada kayıtlı.

## Handoff

- None
