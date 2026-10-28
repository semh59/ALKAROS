# V1-RMD-499 - Arayüzde ham hata metni sızıntısının kapatılması

- Task ID: V1-RMD-499
- Status: Done
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-01

## Goal

Bağımsız taramanın bulduğu arayüz sızıntılarını kapatmak: sunucu hatası olmayan (ağ, programlama, tarayıcı) bir `Error`ın ham `message` metni ekrana basılıyor ve yedekleme hatasında sunucunun serbest metni gösteriliyor. Kullanıcı yalnız önceden yazılmış Türkçe metin görür.

## Owned surface

- `plan/v1/remediation/V1-RMD-499-ui-error-text-leaks.md`
- `evidence/V1-RMD-499/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/userMessage.ts ve src/Clients/PosTerminal/src/userMessage.test.ts — yeni ortak yardımcı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/pending-checks/PendingChecksWorkspace.tsx ve src/Clients/PosTerminal/src/features/pending-checks/PendingChecksWorkspace.test.tsx — yalnız hata metni
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/invoice-settings/SellerProfileCard.tsx ve src/Clients/PosTerminal/src/features/invoice-settings/SellerProfileCard.test.tsx — yalnız hata metni
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/kitchen-operations/KitchenOperationsWorkspace.tsx ve src/Clients/PosTerminal/src/features/kitchen-operations/KitchenOperationsWorkspace.test.tsx — yalnız rota hatası ve yedekleme hata satırı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/strings.ts — yalnız yeni Türkçe metinler
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.
- V1-RMD-739 doğrulama notu (2026-10-28): src/Clients/PosTerminal/src/userMessage.ts, src/Clients/PosTerminal/src/userMessage.test.ts yolları HEAD'de bulunmaz; bu görev tarihsel olarak kapalı kalır.

## In scope

- Ortak `userMessage(reason, fallback)`: yalnız HTTP durumu taşıyan (sunucu cevabından üretilmiş) hatanın metnini döndürür, başka her hatada önceden yazılmış Türkçe metni verir.
- Bekleyen hesaplar, fatura işletme bilgileri ve mutfak rota formunda ham `Error.message` yerine bu yardımcı kullanılır.
- Başarısız yedeklemede sunucunun serbest hata metni yerine sabit Türkçe metin gösterilir.

## Out of scope

- Sunucu hata metinlerinin (Türkçe sözleşmeli `error.message`) değiştirilmesi; programlama korumalarındaki (`terminalId is required`) İngilizce istisna metinleri, çünkü bu yardımcıyla ekrana ulaşamazlar.

## Dependencies

- V1-RMD-114

## Acceptance evidence

- Testler ve mutasyon kanıtı; çıktılar `evidence/V1-RMD-499/` altındadır.

## Handoff

- None
