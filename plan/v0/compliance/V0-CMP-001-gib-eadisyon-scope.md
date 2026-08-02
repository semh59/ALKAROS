# V0-CMP-001 - Determine GIB and e-Adisyon scope

- Task ID: V0-CMP-001
- Status: Done
- Assignee: codex-v0-cmp-001
- Work type: validation
- Surface state: Existing

## Source basis

- PDF:II.2.16
- PDF:II.3.12
- PDF:II.5.4
- PDF:III.19
- EXT:GIB-YNOKC-GUIDE
- EXT:GIB-TK2-4.0

## Goal

Hedef restoran profilinin YN ÖKC, adisyon/e-Adisyon ve 2026 GİB kuralları kapsamındaki yükümlülüklerini yazılı olarak
doğrulamak.

## Owned surface

- `evidence/v0/compliance/V0-CMP-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- İşletme tipi, belge başlangıç/kapanış ilişkisi, saklama, raporlama ve entegratör/device sorumluluğu.
- Kapsam dışı: e-İrsaliye belge akışı (restoran perakende satışında irsaliye düzenlenmez); belge süreçleri e-Fatura/e-Arşiv ve YN ÖKC/adisyon ile sınırlıdır. Gelecekte irsaliye gereksinimi çıkarsa yeni görev açılır.

## Out of scope

- Vergi hukuku yorumu üretmek veya QNB adapter kodlamak.

## Dependencies

- None

## Deliverables

- V0-CMP-001 için tarihli ve kaynakları belirtilmiş evidence package.
- Başarı ve en az bir gerçek hata/edge-case çıktısı.
- Doğrulanamayan maddeler için açık blocker kaydı; varsayımla kapatma yok.

## Acceptance evidence

- Güncel resmi kaynak sürümleri ve mali müşavir/uyum sorumlusu onayıyla applicability matrix mevcut.

## Handoff

- V0-HUG-001
- V0-QNB-001
- V12-FSC-001
