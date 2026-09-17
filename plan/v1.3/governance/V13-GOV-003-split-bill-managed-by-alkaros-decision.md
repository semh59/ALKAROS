# V13-GOV-003 - Decide that split-bill is managed by ALKAROS, not the terminal

- Task ID: V13-GOV-003
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: decision
- Surface state: Existing

## Source basis

- PO:2026-09-17

## Goal

Bir hesabın bölünmesinin (kim ne kadar borçlu) NEREDE hesaplanacağını ve
NASIL tahsil edileceğini resmi bir karara bağlamak: **ALKAROS'un kendi
`SplitEngine`'i** hesaplar; ödeme terminali (Token/Beko) hiçbir zaman
"toplamı N'e böl" komutu almaz. Bunun yerine ALKAROS, her split
owner/segment için **ayrı bir sepet + ayrı bir ödeme isteği** gönderir.
Terminalin kendi yerleşik bölme özelliği (varsa) hiç kullanılmaz.

## Owned surface

- `docs/domain/split-bill-terminal-architecture.md`
- `evidence/V13-GOV-003/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Split hesaplamasının sahibi (ALKAROS `SplitEngine`, zaten mevcut ve
  Done) ile terminale gönderim biriminin (tek bir "böl" komutu değil,
  N ayrı sepet+ödeme isteği) kararı.
- Gelecekteki Token adaptörü (`V13-PAY-003`/`V13-HUG-001`/`V13-PUI-001`)
  için bağlayıcı bir mimari kısıt.

## Out of scope

- Split-bill adaptör kodunun kendisi (sepet dönüştürücü) — ayrı bir
  implementation görevi, henüz açılmadı.
- `SplitEngine`'in kendi hesaplama mantığı — zaten Done (V1-BIL-004 ailesi),
  bu karar onu değiştirmez.

## Dependencies

- None

## Onay

Approved by Semih — Founder/Product Owner — 2026-09-17 ("Split-bill
adaptör katmanı: SplitEngine'in çıktısını ... cihaza ayrı ayrı sepet +
ödeme olarak gönderecek bir dönüştürücü yaz — cihazın kendi bölme
özelliğini kullanma kararın netti"). Gerekçe: PostgreSQL tek doğruluk
kaynağıdır (V0-ARC-004); terminalin kendi bölme UI'ı kullanılırsa
ALKAROS'un `PaymentAllocation`/`Bill` modeli terminalin iç mantığına bağımlı
hâle gelir, denetlenemez ve terminal firmware'i değiştiğinde sessizce
bozulabilir. `SplitEngine` zaten owner/segment bazında tutarları
hesaplıyor (Done) — terminale yalnız zaten hesaplanmış N ayrı tutarı
sırayla göndermek, ek bir güven sınırı açmaz.

## Deliverables

- `docs/domain/split-bill-terminal-architecture.md`: seçilen model,
  reddedilen alternatif (terminal-native split), örnekler, tüketici
  görevler için invariant listesi.

## Acceptance evidence

- Karar kaydı `SplitEngine`'in mevcut, Done olan çıktı şeklini (owner/segment
  başına `AllocatedAmount`) doğru yansıtır.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.

## Handoff

- V13-PAY-003
- V13-PUI-001
