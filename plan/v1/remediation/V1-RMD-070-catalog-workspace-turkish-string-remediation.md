# V1-RMD-070 - Catalog workspace Turkish string remediation

- Task ID: V1-RMD-070
- Status: Done
- Assignee: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-08-31

## Goal

PosTerminal katalog çalışma alanında kullanıcıya görünen ve erişilebilirlik etiketlerinde geçen İngilizce `Catalog` kelimesini, uygulamanın diğer ekranlarıyla tutarlı biçimde `Katalog` olarak düzeltmek.

## Owned surface

- `plan/v1/remediation/V1-RMD-070-catalog-workspace-turkish-string-remediation.md`
- PO:2026-08-31 kararıyla src/Clients/PosTerminal/src/features/catalog/** yüzeyi V1-RMD-076'ya devredildi; bu historical task closed kalır.
- `evidence/V1-RMD-070/**`

## In scope

- Arama alanının görünen metni ve `aria-label` değerinin `Katalog ara` olması.
- Yükleniyor, kaydediliyor, alınamadı, güncel değil ve çakışma durum başlıklarındaki `Catalog` kelimesinin `Katalog` yapılması.
- Kaynak türü sekme navigasyonu `aria-label` değeri ve boş liste başlığındaki `Catalog` kelimesinin düzeltilmesi.
- `Catalog` kelimesinin kullanıcıya görünen metinde kalmadığını doğrulayan Vitest testi.

## Out of scope

- Katalog domain davranışını, API sözleşmesini veya ürün kullanılabilirliği özelliğini değiştirmek.
- Kod tanımlayıcılarındaki `catalog` kelimesi; yalnızca kullanıcıya görünen metin hedeflenir.

## Dependencies

- V1-RMD-069

## Deliverables

- Kullanıcıya görünen tüm metinlerde `Katalog` terimini kullanan katalog çalışma alanı.

## Acceptance evidence

- `pnpm --dir src/Clients/PosTerminal typecheck`, `pnpm --dir src/Clients/PosTerminal test` ve `pnpm --dir src/Clients/PosTerminal build` sıfır çıkış kodu verir.
- Semih katalog ekranını açar; arama kutusunda ve tüm durum başlıklarında `Katalog` yazdığını doğrular.

## Handoff

- V1-RMD-071
