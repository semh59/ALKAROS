# V1-RMD-662 - QR bağlayıcı anahtarı, açılış doğrulaması ve ölü mektup izleyicisi

- Task ID: V1-RMD-662
- Status: InProgress
- Assignee: claude-code-session_01MwPj6Y6uC4Kbceoi6Rzzby
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

V1-RMD-596 bulguları B002, B012 ve B016 kapanır. B002: Docker kurulumunda `connector` servisi `ALKAROS_SECRET_ENVELOPE_MASTER_KEY` almıyordu, bu yüzden QR dış bağlantı (tünel) anahtarı çözülemiyor ve halka açık QR yolu başlamıyordu; servise aynı anahtar verilir ve compose sözleşme testi bunu denetler. B012: anahtar eksikse bağlayıcı sessizce çalışıp ilk kullanımda çökmesin; açılışta net bir hata mesajıyla (çıkış kodu 2) durur. B016: ölü mektup izleyicisi açılışta zaten var olan ölü kayıtları atlıyordu; ilk kontrolde ölü kayıt varsa bildirir.

## Owned surface

- `plan/v1/remediation/V1-RMD-662-connector-key-and-deadletter-watch.md`
- `evidence/V1-RMD-662/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): compose.yaml - yalnız connector servisinin ortam değişkeni
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Deployment/test_container_contract.py - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Integrations/QrRelay/LocalConnector/ConnectorHost/Program.cs - yalnız açılış doğrulaması
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Observability/SecurityDetectors/SecurityDetectors.cs - yalnız ölü mektup izleyicisi ilk kontrolü
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Observability/SecurityDetectors/SecurityDetectorsTests.cs - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-661-outbox-resilience.md - yalnız Handoff satırı
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Connector servisine anahtar, compose sözleşme testi, bağlayıcı açılış doğrulaması, izleyici ilk kontrol davranışı ve testi.

## Out of scope

- API Host'un anahtarsız açılması (compose zaten `:?` ile zorunlu kılar; yerel geliştirme anahtarsız çalışır), kullanılmayan `SecretStartupValidator` sınıfının silinmesi/bağlanması (belge görevinde ele alınır), Docker'da gerçek çalıştırma (doğrulanamadı: bu makinede Docker yok).

## Dependencies

- V1-RMD-661

## Acceptance evidence

1. Önce kırmızı test (compose connector bloğunda anahtar yok; izleyici açılıştaki ölüyü bildirmiyor), sonra yeşil; mutasyon kanıtı.
2. `evidence/V1-RMD-662/` altında çıktılar; SecurityDetectors testleri, Deployment testi yeşil.

## Handoff

- None
