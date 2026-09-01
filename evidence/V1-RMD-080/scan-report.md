# V1-RMD-080 - Code identity English-only audit report

- Date: 2026-08-31
- Scope: `database/migrations/**` and `src/**` (schema identifiers, code
  identifiers, comments). User-facing string literals are exempt (`I.4`).

## Method

- Schema identifiers: `grep` over `database/migrations/**` for Turkish
  characters (`ş ğ ı ö ü ç` and capitals) and Turkish word roots in
  `CREATE/ALTER TABLE|INDEX|SEQUENCE`, `CONSTRAINT`, `ADD COLUMN` statements.
- Code identifiers: `grep` over `src/**` (`*.cs`, `*.ts`, `*.tsx`, excluding
  `bin`, `obj`, `node_modules`, `dist`) for Turkish characters after
  `class|interface|record|enum|func|function|const|let|var|public|private|type`
  and for Turkish word roots (`Masa`, `Siparis`, `Mutfak`, `Adisyon`, `Urun`,
  `Kullanici`, `Fiyat`, `Yazici`, `Garson`, `Kasiyer`, `Sube`, `Firma`,
  `Magaza`).
- Comments: `grep` over `src/**` comment lines (`//`, `///`, `*`) for Turkish
  characters.

## Findings

| Area | Result |
| --- | --- |
| Migration table / column / index / constraint names | Clean. No Turkish characters or word roots. |
| C# / TypeScript class, interface, method, variable, file names | Clean. No Turkish characters or word roots. |
| Code comments | Two English summary comments embedded a Turkish quotation of the V1-IAM-002 acceptance text. Fixed. |

### Fixed comment violations

- `src/Modules/Identity/Authorization/IDenialEventSink.cs`: replaced
  `"reddetme denetimi kancası"` with `denial audit hook`.
- `src/Modules/Identity/Authorization/PermissionCodes.cs`: replaced
  `"her korunan komutun adlandırılmış bir izni vardır"` with
  `every protected command has a named permission`.

### Retained term

- `kuruş` appears in rounding comments in `BillMath.cs`, `SplitEngine.cs`,
  `OrderItem.cs` and `OrderMath.cs`. It is the official name of the Turkish
  lira minor unit (1/100), a currency proper noun with no English equivalent,
  and is retained as domain terminology.

## Conclusion

No Turkish identifiers exist in the schema or the code. The only comment-level
violations were the two IAM-002 quotations, now translated. The recurring scan
is delivered as `tools/consistency-audit/consistency_audit.py` under
`V1-RMD-081`.
