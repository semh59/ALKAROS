# V1-RMD-537 - Tarayıcı hata raporlama

- Task ID: V1-RMD-537
- Status: Done
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-15

## Goal

PosTerminal ekranındaki yakalanmamış hatalar ve ağ hataları sunucuya raporlanır (vanilla kasa ve garson V1-RMD-553'te).

## Owned surface

- `plan/v1/remediation/V1-RMD-537-browser-error-reporting.md`
- `evidence/V1-RMD-537/**`
- `src/Clients/PosTerminal/src/features/error-reporting/**`
- `src/Host/Observability/BrowserErrors/**`
- `tests/Host/Observability/BrowserErrors/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.cs - yalnız uç nokta ve servis kaydı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/main.tsx - yalnız raporlayıcıyı başlatan satır
- Sınırlı ek (paylaşılan, geri-tik olmadan): ALKAROS.slnx - yalnız yeni test projesi girişi
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Uç nokta oturum doğrulaması ister; oran sınırı terminal+oturum başına; olay kimliği ile yinelenmez; sunucu saati esastır.
- İstemci aynı hatadan en çok 3 rapor üretir, `sendBeacon`; kişisel veri ve token içermez; metin uzunluğu sınırlıdır; telemetri ayrı istek yolundan gider (oturum kapatma tetiklemez).
- Sunucu istemci metnini hata türü kataloğuna eşler; bilinmeyen metin sabit künyeye düşer.

## Out of scope

- Vanilla uygulamalar.

## Dependencies

- V1-RMD-533
- V1-RMD-549

## Acceptance evidence

1. Oturumsuz çağrı 401; oran sınırı aşımı gerçek hata kaydını engellemez (ayrı kota).
2. Aynı hata 50 kez raporlanınca sunucuya en çok 3 ulaşır.
3. Metin sızıntı testi 0 kalıp.
4. Çıktılar `evidence/V1-RMD-537/` altındadır.

## Handoff

- None
