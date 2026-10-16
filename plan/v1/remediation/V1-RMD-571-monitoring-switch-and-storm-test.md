# V1-RMD-571 - İzleme anahtarı ve olay fırtınası testi

- Task ID: V1-RMD-571
- Status: InProgress
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-16

## Goal

İzleme sistemi yapılandırmayla kapatılabilir (varsayılan açık), böylece izlemenin yükü açık/kapalı karşılaştırmasıyla ölçülür; olay fırtınasında ana bağlantı havuzunun etkilenmediği kodlu testle kanıtlanır.

## Owned surface

- `plan/v1/remediation/V1-RMD-571-monitoring-switch-and-storm-test.md`
- `evidence/V1-RMD-571/**`
- `src/Host/Observability/MonitoringSwitch/**`
- `tests/Host/Observability/MonitoringSwitch/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.cs - yalnız izleme kayıtlarının anahtar arkasına alınması
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.Endpoints.cs - yalnız giriş ucunun güvenlik dedektörünü isteğe bağlı alması
- Sınırlı ek (paylaşılan, geri-tik olmadan): ALKAROS.slnx - yalnız yeni test projesi girişi
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- `Monitoring:Enabled` ayarı (varsayılan açık). Kapalıyken izleme kayıtları (olay yazıcı, dedektörler, izleme bağıntısı, dış çağrı ve sorgu planı dinleyicileri, istek süre ara katmanı) eklenmez; uçlar ve ekranlar çalışmaya devam eder.
- Gerçek Postgres ile fırtına testi: saniyede 1000 olay, ana havuzun bağlantı kullanımı ve düşen olay sayısı ölçülür.

## Out of scope

- Ölçümün kendisini Docker yığınında çalıştırmak (V1-RMD-556).

## Dependencies

- V1-RMD-534

## Acceptance evidence

1. Ayar verilmezse izleme açıktır; kapalıyken dinleyiciler ve yazıcı çalışmaz.
2. Saniyede 1000 olaylık fırtınada ana havuz bağlantı kullanımı fırtınasız ölçümden farklı değildir.
3. Çıktılar `evidence/V1-RMD-571/` altındadır.

## Handoff

- None
