# V1-RMD-1041 - Müşteri ekranı iptal testi eski varsayımda kalmıştı: hesap iptali siparişi masada bırakır, siparişle iptal ekranı boşaltır

- Task ID: V1-RMD-1041
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

PaymentTender.Tests "ACancelledBillNoLongerLeavesOrderReceivedOnTheDisplay" hesap iptalinden sonra müşteri ekranı "Idle" olmadığı için düşüyordu. Kök neden üretim hatası değildir: V1-RMD-801 ile hesap iptali varsayılan olarak siparişi masada bırakır (yalnız CancelOrder işaretliyse siparişi de iptal eder) ve müşteri ekranı yaşayan siparişi göstermeye devam eder; test eski varsayımla yalnız hesabı iptal ediyordu. Test, siparişle birlikte iptal (CancelOrder) için ekranın boşaldığını kanıtlar; ayrı bir test yalnız hesap iptalinde siparişin satırlarıyla ekranda kaldığını kilitler. Test silinmez ve gevşetilmez.

## Owned surface

- `evidence/V1-RMD-1041/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - tests/Host/Experience/PaymentTender/PaymentTenderHttpTests.cs
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-1040

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
