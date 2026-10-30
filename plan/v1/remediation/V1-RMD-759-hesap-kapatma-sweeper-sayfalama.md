# V1-RMD-759 - Hesap kapatma sweeper'ı tüm adayları sayfalayarak tarar

- Task ID: V1-RMD-759
- Status: InProgress
- Assignee: claude-code-session_01FMN5WVXi1HJCTEk5argkgZ
- Work type: remediation
- Surface state: Existing

## Goal

V1-RMD-750 sonrası denetim bulgusu: `BillClosureSweeper.SweepOnceAsync` adayları `updated_at DESC` sıralayıp tek `LIMIT 200` ile alıyor. 750 yalnız yönü değiştirdi: 200'den fazla yeni ve kapanamayan (kısmi ödemeli) hesap varsa eski kapanabilir hesaplar her taramada aday listesinin dışında kalır ve açık kalır.

Sıralama tabanlı çözüm her yönde aç bırakma üretir. Çözüm: tek taramada tüm adayları `bill_id` anahtar sayfalamasıyla (`bill_id > @after`, `ORDER BY bill_id`, sayfa başına `BatchSize`) gezmek; her aday her taramada bir kez denenir.

## Owned surface

- `plan/v1/remediation/V1-RMD-759-hesap-kapatma-sweeper-sayfalama.md`
- `evidence/V1-RMD-759/**`
- Sınırlı ek: src/Host/Experience/Billing/BillClosureSweeper.cs
- Sınırlı ek: tests/Host/Experience/PaymentTender/PaymentTenderHttpTests.cs

## In scope

- Aday sorgusu `bill_id` anahtar sayfalaması; sayfalar boşalana kadar döngü. Kapatma ve hata günlüğü davranışı aynı kalır.
- Gerçek PostgreSQL Host testi: kapanabilir hesap en eski, ondan sonra 200'den fazla kapanamayan hesap; tek tarama kapanabilir hesabı kapatır. Mevcut 750 testi (ters sıra) geçmeye devam eder.

## Out of scope

- Tarama aralığı, ek süresi (`gracePeriod`) ve kapatma servisi.
- Kapanamayan hesapların tarama dışı bırakılması (sayaç, geri çekilme).

## Dependencies

- V1-RMD-750

## Acceptance evidence

1. Düzeltmeden önce yeni test kırmızı olur; çıktı kaydedilir.
2. Düzeltmeden sonra `PaymentTender` Host testleri (gerçek PostgreSQL, 750 ve 736 sweeper testleri dahil) exit 0 geçer; Host derlemesi 0 uyarı 0 hata.
3. Mutasyon: sayfalama döngüsü tek sayfaya indirildiğinde yeni test kırmızı olur; dosya hash'i geri yüklenir.
4. Kapsam aracı InProgress baseline'a karşı exit 0 verir; iki bağımsız incelemede artık bulgu kalmaz.

## Handoff

- None
