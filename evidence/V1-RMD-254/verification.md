# V1-RMD-254 — Verification transcript

## Ortam

- Chromium 141.0.7390.37 (`/opt/pw-browsers/chromium-1194`, Playwright 1.56.1)
- WaiterPwa statik dosyaları gerçek olarak sunuldu; `/api/v1/*` uç noktaları
  (session, runtime-configuration, zones, catalog, tables, pending) temsili
  örnek veriyle yanıtlandı — 9 masa, 5 farklı durum (Available, Occupied,
  Reserved, Cleaning, OutOfService) kapsanacak şekilde.

## Ölçüm — düzeltmeden önce (`git stash` ile geçici olarak eski koda dönülüp

alındı, sonra `git stash pop` ile düzeltme geri getirildi)

| Masa | Durum etiketi | Tutar alanı |
| --- | --- | --- |
| M1 | Boş | **Boş** (tekrar) |
| M3 | Boş | **Boş** (tekrar) |
| M5 | Rezerve | **Boş** (çelişki) |
| M6 | Toplanıyor | **Boş** (çelişki) |
| M8 | Servis dışı | **Boş** (çelişki) |
| M9 | Boş | **Boş** (tekrar) |
| M2/M4/M7 (Dolu) | Dolu | ₺440,00 / ₺210,00 / ₺615,00 (doğru) |

`before.png`.

## Ölçüm — düzeltmeden sonra

| Masa | Durum etiketi | Tutar alanı |
| --- | --- | --- |
| M1, M3, M9 (Boş) | Boş | *(boş)* |
| M5 | Rezerve | *(boş)* |
| M6 | Toplanıyor | *(boş)* |
| M8 | Servis dışı | *(boş)* |
| M2/M4/M7 (Dolu) | Dolu | ₺440,00 / ₺210,00 / ₺615,00 (değişmedi) |

`after.png` — kartlar artık kısa ve tutarlı; hiçbir durum etiketiyle
çelişen ikinci bir metin yok. Dolu masalarda tutar gösterimi hiç
değişmedi.

## Düzeltme

`src/Clients/WaiterPwa/wwwroot/js/screens/tables.js`, `renderTables()`:

```diff
-<span class="table-amount${busy ? '' : ' is-empty'}">${busy ? formatMoney(table.amount) : 'Boş'}</span>
+<span class="table-amount${busy ? '' : ' is-empty'}">${busy ? formatMoney(table.amount) : ''}</span>
```

## Otomatik testler

```text
$ cd tests/Clients/StaticApps && npx vitest run waiter-app.test.js
 Test Files  1 passed (1)
      Tests  5 passed (5)
```

Değişiklik öncesi ve sonrası birebir aynı sonuç (5/5) — mevcut testler bu
metne bağlı değildi.

## Proje-geneli kontroller

- `python tools/plan-audit/plan_audit_tool.py validate` → 1 hata
  (`C54_APPLICATION_ADMISSION_V3_FINAL_MISSING`), önceden de vardı, ilgisiz.
- `python tools/consistency-audit/consistency_audit.py` → `consistency-audit: clean`.

## Çalıştırılamayan kontrol

`dotnet build`/`dotnet test`: bu ortamda .NET SDK kurulu değil (aynı
V1-RMD-252/253'teki gibi). Owned surface yalnızca statik bir `.js` dosyası.
