# V1-RMD-412 - "Kart çekilmedi" çözümünden sonra ödenmiş hesabı kapatmak

- Task ID: V1-RMD-412
- Status: Done
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-28

## Goal

V1-RMD-393 para akışı denetimi F-06 (Orta): kapanışı engelleyen çözülmemiş bir kart denemesi "kart çekilmedi" diye
çözüldüğünde, hesabın geri kalanı zaten tahsil edilmiş olsa bile hesap kapatılmıyor (kalan 0, durum `Open`); arayüzde
kapatma yolu yok. Kart onayı (`confirmations/{id}/approve`) ve tahsilat uçları başarılı işlemden sonra hesabı kapatmayı
zaten deniyor; "kart çekilmedi" ucu denemiyordu. V1-RMD-409 ile çözülmemiş ödeme varken yeni tahsilat artık
reddedildiği için bu duruma yeni işlemle düşülmez; ancak düzeltmeden önce oluşmuş kayıtlar ve eşzamanlı çözümler için
kapatma denemesi gereklidir. Bu görev "kart çekilmedi" çözümünden sonra hesabı kapatmayı dener (tam ödenmiş değilse
hesap açık kalır) ve yanıta kapanıp kapanmadığını ekler.

## Owned surface

- `plan/v1/remediation/V1-RMD-412-not-charged-resolution-closes-bill.md`
- `evidence/V1-RMD-412/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.Payments.cs (V13-PUI-001
  sahipliğinde) — yalnız `not-charged` ucunun kapatma denemesi ve yanıt alanı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/PaymentTender/PaymentTenderHttpTests.cs
  (V13-PUI-001 sahipliğinde) — yeni test ve yardımcılar

## In scope

- `POST .../unsettled/{paymentId}/not-charged`: çözümden sonra `TryCloseBillAsync`; yanıtta `billClosed`.

## Out of scope

- Kart onayı ve tahsilat uçlarının mevcut kapatma davranışı.

## Dependencies

- V1-RMD-411

## Acceptance evidence

- `ALKAROS.Host.Experience.PaymentTender.Tests` (gerçek PostgreSQL 18, Release, 0 uyarı / 0 hata): 34/34
  (`evidence/V1-RMD-412/tests.log`).
- Yeni test `MarkingTheLastUnresolvedCardAttemptNotChargedClosesAnOtherwiseSettledBill`: gerçek kart ucuyla çözülmemiş
  deneme; hesabın tamamı düzeltme öncesi bir kayıt olarak dağıtılmış; yönetici "kart çekilmedi" der → yanıtta
  `billClosed = true`, hesap `Paid`. Üretim değişikliği geri alınınca kırmızı (aynı dosya).
- V1-RMD-393 probe'u P09 artık ön koşuluna ulaşamıyor: duruma F-04 açığıyla (çözülmemiş kart varken nakit) giriyordu ve
  V1-RMD-409 bunu kapattı (aynı dosyada açıklandı). Senaryo yukarıdaki HTTP testiyle sınanıyor.
- Semih'in elle deneyebileceği senaryo: kartı sonuçsuz kalan bir hesabın geri kalanı ödenmişse, yönetici "kart
  çekilmedi" dediği anda hesap kapanır ve masa boşalır.

## Handoff

- None
