# V0-QNB-001 - Validate QNB eSolutions contract

- Task ID: V0-QNB-001
- Status: Blocked
- Assignee: Unassigned
- Work type: validation
- Surface state: Planned

## Source basis

- PDF:I.6
- PDF:I.6.3
- CORR:C21
- EXT:QNB-API-PUBLIC

## Goal

Outgoing/incoming e-belge, registered-user query, idempotency, status query ve timeout sözleşmesini doğrulamak.

## Owned surface

- `evidence/v0/integrations/V0-QNB-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- API version, authentication, test tenant, document profiles, rate limits, retry ve archive responsibilities.

## Out of scope

- Production invoice generation ve adapter kodu.
- Araştırma notu (2026-10-17, V1-RMD-584), kayıt durumunu ve kararı değiştirmez:
- QNB'nin test servis tanımları herkese açıktır (girişsiz indirildi, 2026-10-16/17): e-Fatura https://erpefaturatest1.qnbesolutions.com.tr/efatura/ws/connectorService?wsdl ve userService; e-Arşiv https://earsivtest.qnbesolutions.com.tr/earsiv/ws/EarsivWebService?wsdl (giriş: .../earsiv/ws/userService). e-Arşiv'de `faturaOlusturExt`, `faturaSorgula`, `faturaIptalEt`, `faturaItirazEt`, `raporGonder` vardır; e-Fatura'da genel iptal metodu yoktur (yalnız itiraz/iade işaretleme). Erişim: test formu https://www.qnbesolutions.com.tr/destek/api-teknik , `erpKodu` başvuru anında verilir, test ortamına yalnız başvuran VKN kaydedilir; hız sınırı IP başına dakikada 300. Gerçek hesap, `erpKodu` ve yaşam döngüsü kaydı hâlâ sahibin başvurusuna bağlıdır. e-Arşiv gönderim istemcisi V1-RMD-582 ile şemadan yazıldı (gerçek hesapla denenmedi).

## Dependencies

- V0-CMP-001

## Blocker

- Kamuya açık API temel e-belge işlemlerini gösteriyor; test tenant, özel contract ve gerçek document lifecycle
  transcript kanıtı çalışma alanında yoktur.
- Görev ancak test tenant credential/endpoint erişimi ve yürürlükteki private/partner contract sağlandığında `Planned`
  durumuna alınabilir. Gerçek lifecycle transcript'i `Done` acceptance kanıtıdır.

## Deliverables

- V0-QNB-001 için tarihli ve kaynakları belirtilmiş evidence package.
- Başarı ve en az bir gerçek hata/edge-case çıktısı.
- Doğrulanamayan maddeler için açık blocker kaydı; varsayımla kapatma yok.

## Acceptance evidence

- Test tenant üzerinde kayıtlı kullanıcı sorgusu ve en az bir doğrulanmış document lifecycle kanıtı var.

## Handoff

- V14-QNB-001
- V14-QNB-002
- V14-QNB-003
- V14-QNB-004
