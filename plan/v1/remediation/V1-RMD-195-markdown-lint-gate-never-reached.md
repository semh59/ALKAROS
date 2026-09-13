# V1-RMD-195 - `Root Markdown lint` kapısı da hiç ulaşılmamıştı

- Task ID: V1-RMD-195
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Goal

V1-RMD-194'ün `verify-manifest` kapısını açmasıyla, CI ilk kez
`Root Markdown lint` adımına ulaştı — ve orada da başarısız oldu: ~70
`markdownlint-cli2@0.23.2` ihlali, günlerce/haftalarca birikmiş, hiç
görülmemiş. Bu, `verify-manifest`'in önündeki kapı kapalı olduğu için
CI'ın hiçbir zaman bu adıma ulaşamadığının ikinci doğrudan kanıtı.

## Owned surface

- `plan/v1/remediation/V1-RMD-195-markdown-lint-gate-never-reached.md` (yeni)
- Sınırlı ek — ihlal düzeltmesi, her dosyanın kendi sahipliğinde,
  içerik değişikliği yok, yalnızca Markdown biçimlendirmesi:
  - `npx markdownlint-cli2@0.23.2 --fix` ile 113 ihlal otomatik
    düzeltildi (23 dosya: başlık/liste/kod-bloğu etrafı boşluk, `+` →
    `-` liste stili, fazla boş satır).
  - Kalan 16 ihlal elle düzeltildi (araç güvenle otomatik
    düzeltemediği türler):
    - `docs/engineering/garson-refactor-plan.md`, `docs/operations/
      qr-relay-setup-guide.md`, `evidence/v0/integrations/V0-QRG-001/
      2026-09-07-cloudflare-tunnel-feasibility.md`,
      `plan/v1/remediation/V1-RMD-193-...md`,
      `plan/v1/waiter-pwa/V1-WTR-036-...md` — MD040 (kod bloklarına dil
      etiketi eksikti): düz metin/konsol çıktısı bloklarına `text`,
      gerçek bir shell komutuna `bash`.
    - `plan/v1/remediation/V1-RMD-114-...md`,
      `plan/v1/remediation/V1-RMD-150-...md` — MD033 (inline HTML olarak
      yorumlanan `<span>`/`<eklenti>` yer tutucuları): geri işaretle
      (`` ` ``) kod olarak biçimlendirildi, anlam değişmedi.
    - `plan/v1/remediation/V1-RMD-153-...md`,
      `plan/v1/waiter-pwa/V1-WTR-024-...md`,
      `plan/v1/waiter-pwa/V1-WTR-034-...md` — MD032 (bir cümle içi
      tire kullanımı yanlışlıkla liste olarak ayrıştırılmıştı): öncesine
      boş satır eklendi, metnin kendisi değişmedi.

## Out of scope

- Yok — tümü biçimlendirme; hiçbir dosyanın anlamı/içeriği değişmedi.

## Dependencies

- V1-RMD-194

## Acceptance evidence

- Düzeltmeden önce: `npx --yes markdownlint-cli2@0.23.2` (CI'ın
  çalıştırdığı tam pinlenmiş sürüm) → ~70 hata, CI logundaki listeyle
  birebir eşleşti.
- Düzeltmeden sonra: aynı komut → **`Summary: 0 issues in 0 files`**.
- Markdown içeriği değiştiği için denetim eserleri yeniden üretildi ve
  tüm zincir tekrar sıfır hatayla doğrulandı:
  - `python tools/plan-audit/plan_audit_tool.py generate-audit-report`
    ve `generate-manifest`.
  - `validate` → 0 hata, 0 uyarı.
  - `validate-coverage` → 0 hata.
  - `verify-manifest` → Manifest errors: 0.
  - `tools/project-manifest/project_manifest_tool.py` → VALID, 0 fark.
  - `python tools/consistency-audit/consistency_audit.py` → clean.

## Handoff

- None
