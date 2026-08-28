# V1-GOV-003 - Full project production audit and remediation planning

- Task ID: V1-GOV-003
- Status: Done
- Assignee: /root/full_production_audit
- Work type: validation
- Surface state: Existing

## Goal

`a03d02146961c29a8b847a7b0c472c6c8dd42c9f` commit'ini sabit aday kabul ederek repository'deki 1.727 tracked dosyanın
tamamını satır satır ve varlık bazında bağımsız denetime tabi tutmak; ledger, findings ve master audit raporunu
üreterek tespit edilen tüm bulguları V1-RMD-007..011 ve V1-GOV-004 remediasyon görevlerine yönlendirmek.

## Owned surface

- `docs/audit/FULL_PROJECT_PRODUCTION_AUDIT_2026-08-24.md`
- `docs/audit/FULL_PROJECT_PRODUCTION_AUDIT_LEDGER.jsonl`
- `docs/audit/FULL_PROJECT_PRODUCTION_AUDIT_FINDINGS.json`
- `evidence/V1-GOV-003/**`

## In scope

- Repository içindeki 1.727 tracked dosyanın tamamını kapsayan satır bazlı denetim defteri üretmek.
- Doğrulanan bulgular için yapılandırılmış katalog ve etki analizleri oluşturmak.
- Master audit raporu hazırlayarak bulguları P0 ile P3 arasında derecelendirmek.
- `evidence/V1-GOV-003/**` altında yeniden üretilebilir kanıt paketi bırakmak.

## Out of scope

- Üretim kodunu bu denetim görevi içinde doğrudan değiştirmek.
- Onaysız risk feragati düzenlemek veya sahte hazır olma kararı vermek.

## Dependencies

- V1-FND-026
- V1-RMD-001
- V1-RMD-002
- V1-RMD-005

## Deliverables

- Tam kapsamlı `FULL_PROJECT_PRODUCTION_AUDIT_2026-08-24.md` denetim raporu belgesi.
- 1.727 dosyayı içeren `FULL_PROJECT_PRODUCTION_AUDIT_LEDGER.jsonl` denetim defteri.
- Yapılandırılmış `FULL_PROJECT_PRODUCTION_AUDIT_FINDINGS.json` bulgu kataloğu.
- `evidence/V1-GOV-003/**` altında kanıt paketi.

## Acceptance evidence

- Candidate commit/tree disposable checkout'ta test öncesi ve sonrası aynıdır; tracked manifest 1.727 unique path ile
  birebir eşleşir ve binary/text sınıfları Git blob, SHA-256 ve gerçek satır sayısıyla doğrulanır.
- Ledger her metin satırını kapsayan aralık, reviewer, verdict, finding ID ve raw evidence bağlantısı taşır; yapısal,
  hash, binary veya line-range hatası sıfırdır ve `PASS` yalnız yeniden üretilebilir inceleme kanıtıyla verilir.
- Bulgular authN/authZ, tenant/device sınırı, trusted proxy/TLS, rate-limit partitioning, exception/secret redaction,
  PostgreSQL 18 forward/down/concurrency/query ve designer seviyesinde UI state/viewport/a11y turlarını kapsar.
- Fresh PostgreSQL 18 full solution testi, `dotnet format`, locked build/test, Node/Python testleri ve dokuz viewport browser
  kayıtları gerçek exit code/raw transcript üretir; eksik tool, psql, cihaz, secret veya sandbox sonucu `PASS` sayılmaz.
- `evidence/V1-GOV-003/**` altında ham komut, ekran görüntüsü, DOM/a11y, network/console ve manifest doğrulama çıktıları
  kaydedilir; nihai hüküm dış go-live kanıtları gelene kadar `NOT PRODUCTION READY` kalır.

## Handoff

- V1-RMD-007
- V1-RMD-008
- V1-RMD-009
- V1-RMD-010
- V1-RMD-011
- V1-GOV-004
