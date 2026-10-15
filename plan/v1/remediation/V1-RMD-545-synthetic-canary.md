# V1-RMD-545 - Sentetik sağlık sondası (kanarya)

- Task ID: V1-RMD-545
- Status: InProgress
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-15

## Goal

Sistem her dakika kendi üzerinde salt okuyan veya işlemi geri alan bir akışı dener ve süresini ölçer.

## Owned surface

- `plan/v1/remediation/V1-RMD-545-synthetic-canary.md`
- `evidence/V1-RMD-545/**`
- `src/Host/Observability/SyntheticProbes/**`
- `tests/Host/Observability/SyntheticProbes/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.cs - yalnız sonda kaydı
- Sınırlı ek (paylaşılan, geri-tik olmadan): ALKAROS.slnx - yalnız yeni test projesi girişi
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Giriş yapmayan okuma uçları, katalog okuma, sağlık ucu; taslak sipariş yazılmaz ya da işlem geri alınır; kalıcı yetkili sentetik kullanıcı yoktur.
- Başarısızlık veya yavaşlama hata olayı olur.

## Out of scope

- Gerçek ödeme ve yazıcı çıktısı.

## Dependencies

- V1-RMD-533

## Acceptance evidence

1. Bir günde kalıcı sentetik kayıt sayısı 0.
2. Sondanın kendisinin yükü dakikada en çok 5 sorgu.
3. Çıktılar `evidence/V1-RMD-545/` altındadır.

## Handoff

- None
