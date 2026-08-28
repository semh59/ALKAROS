# V1-RMD-007 - Audit manifest, gate counts, build provenance and CI validation

- Task ID: V1-RMD-007
- Status: Done
- Assignee: /root
- Work type: integration
- Surface state: Existing

## Goal

Stale audit manifestini, gate task ve dalga metriklerini, build provenance assembly commit kontrollerini ve kök
doküman lint kurallarını sıfır hata ile güncellemek ve doğrulamak.

## Owned surface

- `Directory.Build.props`
- `plan/AUDIT_MANIFEST.json`
- `plan/AUDIT_BASELINE_MANIFEST.json`
- `plan/AUDIT_REPORT.md`
- `plan/GATES.md`
- `plan/v1/README.md`
- `.github/workflows/task-scope.yml`
- `tools/build-provenance/verify_build_provenance.py`
- `tools/project-manifest/project_manifest_tool.py`
- `evidence/V1-RMD-007/**`

## In scope

- `plan/AUDIT_MANIFEST.json` dosyasını gerçek markdown dosya sayısı (554), satır ve byte toplamlarıyla yeniden üretmek.
- Temiz Release derlemesinde candidate SHA override edilmeden 34/34 assembly eşleşmesini ve yanlış/stale SHA negatif
  yolunu doğrulamak; PO:2026-08-24 onayıyla yalnız `Directory.Build.props` içindeki stale commit provenance değerini
  düzeltmek.
- `Directory.Build.props` yetkisi V1-FND-001 reserved surface kuralına dar, tek seferlik correction exception'dır;
  başka root build/project/config dosyasına veya wildcard yüzeye yetki vermez.
- `plan/GATES.md` ve `plan/v1/README.md` içindeki görev sayılarını ve dalga tanımlarını V1-RMD-007..011 serisiyle
  senkronize etmek.
- Plan dosyalarındaki satır sonu ve boşluk biçimlendirme artıklarını gidermek.

## Out of scope

- Production C# veya TypeScript kod mantığını değiştirmek.
- Dış donanım veya sandbox gerektiren V0/V20 görevlerini değiştirmek.

## Dependencies

- V1-GOV-003
- V1-GOV-006

## Deliverables

- Güncellenmiş ve doğrulanmış `plan/AUDIT_MANIFEST.json` manifest dosyası.
- Eşleşen 34/34 Release derleme provenance kanıtı.
- Sıfır hata veren `plan_audit_tool.py validate` ve `verify-manifest` çıktısı.

## Acceptance evidence

- `python -B tools/plan-audit/plan_audit_tool.py verify-manifest` 0 hata ile exit code 0 verir.
- Clean checkout'ta locked restore/build sonrası `verify_build_provenance.py` 34/34 eşleşme verir; stale binary ve yanlış
  commit metadata testleri fail-closed olur, CI aynı doğrulamayı çalıştırır; doğrulama candidate SHA override'ıyla
  hard-coded stale değeri maskelemez.
- Root lint, code/branch coverage, dependency vulnerability, SBOM/license ve history secret scan komutları gerçek exit
  code/raw output üretir; tanımlı coverage/SLO eşiği yoksa eşik uydurulmaz ve release blocker kaydedilir.
- `python -B tools/plan-audit/plan_audit_tool.py validate` exit code 0 verir.
- `evidence/V1-RMD-007/**` altında komut çıktıları belgelenir.

## Handoff

- V1-RMD-008
