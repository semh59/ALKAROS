# V1-FND-009 Full-History Rewrite — Closure Report (2026-08-05)

Kullanıcı onaylı plan değişikliği: `TRACEABILITY.md` C45 (2026-08-05, "Tam geçmişi yeniden yaz

+ force-push", "Net olanlara atıf, belirsizlere istisna").

## Yapılan işlem

+ `git filter-branch --force --msg-filter ... --tag-name-filter cat` ile tüm `master` geçmişi
  (126 commit) yeniden yazıldı; yalnız 19 hedef commit'in mesajına footer eklendi, diğer mesajlar
  ve tüm içerik aynen korundu.
+ Hem `gate/v0-entry` hem `v0.0.0` tag'i yeniden konumlandı (annotated tag objeleri yeniden
  oluşturuldu).
+ Yedek: `fnd009-safety-backup` branch'i (`a268a10`) rewrite öncesi durumu korur.

## Footer atıfları (19 commit)

| Orijinal SHA | Yeni SHA | Footer |
| --- | --- | --- |
| `0cd9a71` | `a4e48ad` | `Task: V1-FND-005` |
| `570c7b3` | `8fa12b3` | `Task: V1-FND-004` |
| `143009d` | `8f8e6b1` | `Task: V1-FND-003` |
| `03f6102` | `3857126` | `Task: V1-FND-010` |
| `4c3094a` | `813e5ee` | `Task: V0-GOV-032` |
| `f56ead9` | `0cdc35d` | `Task: V0-GOV-032` |
| `9c441d1` | `00b78a8` | `Task: V0-GOV-032` |
| `0bc0193` | `983f25d` | `Task: V0-GOV-032` |
| `b6c1c0a` | `439361e` | `Task: V1-FND-001` |
| `3211643` | `1cf96bf` | `Task: V0-GOV-030` |
| `3aab465` | `55c8167` | `Task: V1-FND-015` |
| `baf8b67` | `ca7cde9` | `Task: V1-FND-014` |
| `d4d62d6` | `ded51aa` | `Task: V1-FND-013` |
| `678565f` | `9528f78` | `Task: V1-IAM-005` |
| `78958da` | `00cc666` | `Task: V0-GOV-031` |
| `9e2a6b6` | `371acb3` | `Task: V0-GOV-031` |
| `5c9697d` | `0d6c720` | `Gate: GATE-V0-EXIT` |
| `8d59493` | `ce9d3ae` | `Gate: GATE-V0-EXIT` |
| `1c11603` | `950efca` | `Task: V1-FND-006` |

## Kayıtlı istisnalar (footer yok)

+ 11 belirsiz commit (`1991abb`, `ed3d97d`, `f87c7dc`, `849bcaa`, `d6f7438`, `4374a3b`,
  `f87468b`, `17c9080`, `792fa2c`, `630e324`, `6f5278c`) — sahiplik kanıtlanamadı, kurgusal atıf
  yapılmadı.
+ `fc5ae22` kök baseline → yeni `46c8d7d` (konvansiyon öncesi, FIND-IA-0050).

## İçerik korunması

+ `git diff --stat fnd009-safety-backup master` → boş (yalnız commit SHA/mesaj değişti, içerik
  birebir korundu).
+ `git fsck --no-dangling` → sorunsuz.
