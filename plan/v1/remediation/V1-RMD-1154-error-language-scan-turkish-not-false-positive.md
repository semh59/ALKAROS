# V1-RMD-1154 - Hata metni dil taraması Türkçe "not" kelimesini İngilizce saymasın

- Task ID: V1-RMD-1154
- Status: InProgress
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

`ErrorMessageLanguageTests` "Bir gerekçe seçin; \"Diğer\" için not yazın" (ApiErrorCatalog.cs, V1-RMD-1134) iletisini İngilizce sayıyor: tarama listesindeki `not` Türkçe "not" (kayıt, açıklama) kelimesiyle çakışıyor. Metin doğru Türkçe, sızıntı yok; metin değil tarama düzeltilir ve "not yaz..." kalıbı dışında `not` yine yakalanır.

1. `not` yalnız Türkçe "not yaz" kalıbında istisna olur.
2. İngilizce "is not", "must not" gibi iletiler yine yakalanır (testle kanıtlanır).

## Owned surface

- `evidence/V1-RMD-1154/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - tests/Host/Experience/PaymentTender/ErrorMessageLanguageTests.cs
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı kanıt, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Ürün metinleri; taranan kaynak listesi

## Dependencies

- V1-RMD-1134

## Acceptance evidence

1. Kırmızı ve yeşil test çıktısı, mutant listesi
2. İki ardışık bağımsız qa-* incelemesi
3. ErrorMessageLanguageTests exit code 0

## Handoff

- None
