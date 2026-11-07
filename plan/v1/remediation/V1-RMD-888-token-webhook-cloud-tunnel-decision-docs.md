# V1-RMD-888 - Token karar belgeleri: webhook'lu Cloud ve tünel rotası kararı (belge)

- Task ID: V1-RMD-888
- Status: InProgress
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: documentation
- Surface state: Existing

## Goal

Ürün sahibi kararı (Semih, 2026-11-07): Token yolu, webhook'lu TokenX Connect Cloud ve bunun için yeni bir Cloudflare Tunnel rotasıdır. V1-RMD-881 belgelere "araştırma notu" olarak yalnız çelişkiyi ve açık kararı yazmıştı; bu görev kararı kayıtlara işler: "sonuç alma varsayılanı polling" ifadesi geçersizdir, webhook gereklidir, Cloud yalnız X30 TR için doğrulanmıştır (300 TR kablolu), yeni tünel rotası gerekir. Kod yazılmaz; webhook alımı ve tünel rotası ayrı gelecekteki görevlerdir.

## Owned surface

- `evidence/V1-RMD-888/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - docs/domain/token-integration-path-decision.md — karar güncellemesi bölümü eklenir
  - plan/v1.3/governance/V13-GOV-005-token-cloud-integration-path-decision.md — karar güncellemesi notu
  - plan/v1.3/hugin-t300/V13-HUG-001-payment-request-path.md — çelişki çözüldü notu
  - plan/v0/hugin-t300/V0-HUG-001-integration-contract.md — çelişki çözüldü notu
  - docs/architecture/qr-relay-topology.md — Token webhook rotası için tünel notu
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Karar güncellemesinin dört belgede yalnız ekleme olarak yazılması (silinen satır yok)
2. Bağımsız belge incelemesi

## Out of scope

- Her türlü kod, webhook alıcısı, tünel yapılandırması, Token sözleşmesi ve cihaz süreci, V13-FSC-005 kararı

## Dependencies

- None

## Acceptance evidence

1. Eklenen satırların git diff sayımı (silinen satır 0)
2. plan-audit ve consistency-audit temiz
3. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
