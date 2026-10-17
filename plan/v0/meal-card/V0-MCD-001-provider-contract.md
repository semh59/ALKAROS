# V0-MCD-001 - Validate meal-card provider contract

- Task ID: V0-MCD-001
- Status: Blocked
- Assignee: Unassigned
- Work type: validation
- Surface state: Planned

## Source basis

- PDF:I.6
- PDF:I.6.2
- PDF:II.2.14
- PDF:II.3.10
- PDF:II.5.10
- PDF:III.17
- EXT:GIB-TK2-4.0

## Goal

Desteklenecek meal-card provider'larını belirlemek ve payment, cancellation/refund, commission, statement ve settlement
contract'larını doğrulamak.

## Owned surface

- `evidence/v0/integrations/V0-MCD-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Provider erişimi, kimlik bilgisi modeli, işlem kimliği, bilinmeyen durum, ekstre formatı, ödeme süresi ve ücretler.

## Out of scope

- Production adapter yazmak veya private provider contract kanıtı olmadan provider seçmek.
- Araştırma notu (2026-10-17, V1-RMD-584), kayıt durumunu ve kararı değiştirmez:
- Devlet tarafından verilmiş bir "legal provider code" bulunamadı (2026-10-16/17). GİB'in "yemek kartı POS cihazlarının haricen kullanılmasına izin verilen firmalar" listesinde (https://ynokc.gib.gov.tr/UploadedFiles/Files/yemekkartiposfirmalari_2.pdf) 8 firmanın yalnız unvanı vardır: Set, Multinet, Metropal, Edenred, Sodexo, CIO, Token, iWallet. GİB bildirimlerinde sağlayıcı VKN ve unvanla tanımlanır (GMP-3 v5.0 Ek-2). Token ödeme tipi 7 + `operatorId` kullanır: Edenred 1001, Multinet 1002, Setcard 1003, Sodexo 1004, TokenFlex 1005, Metropol 1006, test 1000 (`developer.tokeninc.com/x-platform/baslangic/banka-yemek-uygulamalari`); bunlar Token'ın kodlarıdır, yasal kod değildir. Hiçbir sağlayıcının herkese açık POS yazılımı API'si bulunamadı. Mevzuat: komisyon %6'yı geçemez, ödeme fatura tarihinden itibaren 30 günü aşamaz (Perakende Ticarette Uygulanacak İlke ve Kurallar Hakkında Yönetmelik m.12/A). Tamamı yemek kartıyla ödemede mali değeri olmayan bilgi fişi, kısmi ödemede mali fiş kesilir ve yemek kartı kısmına KDV hesaplanmaz; dönem sonunda her sağlayıcıya ayrı fatura kesilir. Bu dönemsel fatura için planda sahip görev yoktur.
- Karar bekliyor: onaylı sağlayıcı listesi ve "provider code" tanımı (öneri: GİB listesindeki unvan + sağlayıcı VKN'si; `operatorId` ayrı cihaz eşlemesi).

## Dependencies

- V0-DOM-003
- V0-ARC-003

## Blocker

- İncelenecek provider adayları, imzalı provider contract, credential ve sandbox veya cihaz erişimi çalışma alanında
  yoktur; approved provider listesi bu görevin çıktısıdır.
- Görev ancak provider adayları iş sahibi tarafından adlandırıldığında ve her aday için contract ile kullanılabilir
  sandbox/cihaz erişimi sağlandığında `Planned` durumuna alınabilir. Gerçek transcript'ler `Done` acceptance kanıtıdır.

## Deliverables

- V0-MCD-001 için tarihli resmi/business evidence paketi.
- Desteklenen ve desteklenmeyen kapsam listesi.
- Doğrulanamayan madde için blocker; varsayımsal sonuç yok.

## Acceptance evidence

- Onaylanmış en az bir provider'ın resmi contract/sandbox kanıtı vardır; bilinmeyen/kullanılamayan provider'lar açıkça
  desteklenmemektedir.

## Handoff

- V13-MCD-001
- V13-MCD-002
- V13-MCD-003
