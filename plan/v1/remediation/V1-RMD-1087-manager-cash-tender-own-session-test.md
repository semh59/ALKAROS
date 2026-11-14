# V1-RMD-1087 - Yönetici de nakdi yalnız kendi açık kasa oturumuna alır (sunucu testi)

- Task ID: V1-RMD-1087
- Status: InProgress
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Semih kararı 2026-11-14: yönetici nakdi yalnız kendi açık oturumuna alır; kasiyerle aynı kural, yönetici muafiyeti yok. Kod zaten böyle çalışıyor: cash-tender ucu oturum sahibi çağıran değilse yetki ne olursa olsun 409 CASH_SESSION_NOT_OWNED döndürür (V1-RMD-1049). Ama V1-RMD-1049 yalnız sıradan kasiyerle sınanmıştı; cash.session.override yetkili yöneticinin başkasının oturumuna nakit alamadığını hiçbir test korumuyordu. Bu görev o açığı kapatır: yetkili yönetici başkasının açık oturumuna cash-tender gönderince 409 CASH_SESSION_NOT_OWNED alır ve hiçbir ödeme yazılmaz; kendi oturumuna alabilir. Üretim kodu değişmez.

## Owned surface

- `evidence/V1-RMD-1087/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - tests/Host/Experience/CashSession/CashSessionHttpTests.cs
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Yönetici ve kasiyer için sahiplik testi, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Üretim kodu; cash-movements ucunun sahiplik kuralı; kasa sayımı ve kapatma yetkisi

## Dependencies

- V1-RMD-1086

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None
