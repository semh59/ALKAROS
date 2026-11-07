# V1-RMD-881 - Token ve QNB karar belgelerini 2026-10-17 araştırmasına uydur

- Task ID: V1-RMD-881
- Status: InProgress
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: documentation
- Surface state: Existing

## Goal

Belge görevi (kod değişmez, ürün kararı verilmez): V13-GOV-005 karar kaydı, docs/domain/token-integration-path-decision.md ve V13-GOV-007 kaydı, 2026-10-17 dış bağımlılık araştırmasıyla çelişen ifadeleri kaynak ve tarihle işaretler. Bulgular F0089 ve F0090'dır. Çelişkiyi sessizce silmez; Done kararın metnini korur, üstüne araştırma notu ekler; karar gerektirenler 'Karar bekliyor' diye işaretlenir.

## Owned surface

- `evidence/V1-RMD-881/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - plan/v1.3/governance/V13-GOV-005-token-cloud-integration-path-decision.md — yalnız sonuna araştırma notu
  - docs/domain/token-integration-path-decision.md — yalnız sonuna araştırma notu
  - plan/v1.4/qnb-esolutions/V13-GOV-007-qnb-efatura-unverified-draft-authorization.md — yalnız sonuna araştırma notu
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. V13-GOV-005 notu: polling varsayılanı ve '300 TR / X30 TR' ifadesi Token HoReCa sayfasıyla (webhook zorunlu, Cloud yalnız X30TR) çelişir; kayıt V0-HUG-001 notuna bağlanır
2. docs/domain notu aynı çelişkiyi ve Cloud sözleşme/test kuyruğu bilgisini ekler
3. V13-GOV-007 notu: V13-FSC-005 NotApplicable gerekçesinin (Token e-Adisyon cihazı, kamuya açık doküman yok) GİB özel entegratör listesiyle çelişmesi ve taslak göndermede 'return' alanı varsayımı
4. Her nota kaynak adresi ve tarih; doğrulanamayanlar 'doğrulanamadı'

## Out of scope

- Durum, karar, bağımlılık, kabul koşulu ve kod değişikliği; V13-FSC-005, V0-CMP-001 ve V0-HUG-001 kayıtları (V1-RMD-584 notlarını taşıyor); F0099 ve F0457; ürün kararı verilmesi

## Dependencies

- None

## Acceptance evidence

1. git diff yalnız üç dosyada ekleme gösterir (silinen satır 0)
2. Her not kaynak adresi ve 2026-10-17 tarihi taşır; karar gerektirenler 'Karar bekliyor' ile işaretli
3. evidence/V1-RMD-881/RESULT.md ve REVIEWS.md
4. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
