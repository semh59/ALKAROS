# V1-RMD-870 - Aynı ödemeye eşzamanlı birden fazla iade talebi yarış testi

- Task ID: V1-RMD-870
- Status: Done
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: implementation
- Surface state: Existing

## Goal

PaymentRefundStore.RequestAsync hesap kilidi altında çalışır ve ödeme başına tek açık iade isteği kuralı kısmi tekil indeksle (TryInsertAsync) korunur. Ardışık ikinci istek 409 REFUND_ALREADY_REQUESTED alır (test var), ancak aynı ödeme için eşzamanlı gelen istekler sınanmıyor. Eşzamanlı N istekte tam bir isteğin oluştuğu, diğerlerinin 409 aldığı ve tek satır kaldığı kanıtlanır.

## Owned surface

- `evidence/V1-RMD-870/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - tests/Host/Experience/Billing/PaymentRefundHttpTests.cs — eşzamanlı iade talebi testi.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Test: aynı ödeme için farklı işlem anahtarlarıyla 6 eşzamanlı talep; tam bir 200, kalanı 409 REFUND_ALREADY_REQUESTED, payments.refund_requests'te tek satır, ödeme iadesi yok (açık oturum yok).
2. Mutasyon: TryInsertAsync sonucunun yok sayılması ve hesap kilidinin kaldırılması (cp+cmp geri alınır).

## Out of scope

- Üretim kodu değişikliği (davranış doğruysa).

## Dependencies

- None

## Acceptance evidence

1. Host Billing iade testleri gerçek Postgres ile exit 0; çıktılar evidence/V1-RMD-870/.
2. Mutasyon kanıtı cp+cmp ile geri alınmış.
3. Plan audit ve consistency audit 0 hata/0 uyarı.
4. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
