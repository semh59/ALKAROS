# V1-RMD-421 - Gün sonu cirosu ve sipariş sayısının sunucuda hesaplanması

- Task ID: V1-RMD-421
- Status: InProgress
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-28

## Goal

V1-RMD-393 para akışı denetimi F-10 (Düşük–Orta): gün sonu kapanışı ciroyu ve sipariş sayısını istemcinin gönderdiği
gövdeden olduğu gibi saklıyordu (V1-RMD-249 sözleşmesi); kayıtlı ödemelerden türetmiyordu. Semih'in kararı
(2026-09-28, "F-10 önerini uygula"): bu iki değer sunucuda hesaplanır.

Tanım:

- İş günü penceresi: iş tarihinin 06:00'ı (Europe/Istanbul) ile ertesi günün 06:00'ı arası, sonu hariç — ödeme
  mutabakat raporunun (V13-RPT-001) kullandığı pencere.
- Ciro: pencerede onaylanmış (`Approved`) ödemelerin onaylı tutarlarının toplamı — mutabakat raporunun ödeme
  karışımıyla aynı kural.
- Sipariş sayısı: pencerede gönderilmiş (`submitted_at`) siparişler; taslak, reddedilen ve iptal edilenler hariç.

Kapanış isteğinin gövdesinden `TotalRevenue` ve `TotalOrders` alanları kaldırılır; iptal edilen kalem, yazdırma
hatası ve garson/yazıcı özetleri bu görevin kapsamı dışında, istemciden gelmeye devam eder.

## Owned surface

- `plan/v1/remediation/V1-RMD-421-end-of-day-totals-derived-server-side.md`
- `evidence/V1-RMD-421/**`
- `src/Modules/Reporting/BusinessDayTotals/BusinessDayTotalsReader.cs`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Reporting/V1Operations/OperationalReportService.cs
  (V1-RMD-085 sahipliğinde) — yalnız kapanışta toplamların okuyucudan alınması
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Reporting/ReportingModule.cs (V1-RMD-002 sahipliğinde) —
  yalnız okuyucunun kaydı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Reporting/EndOfDayEndpoints.cs (V1-RMD-249
  sahipliğinde) — yalnız kapanış gövdesi ve okuyucunun kaydı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Reporting/V1Operations/PostgresOperationalReportRepositoryTests.cs
  ve tests/Modules/Reporting/V1Operations/ReportingDomainTests.cs (V1-RMD-085 sahipliğinde) — sabit toplam dönen okuyucu
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Reporting/EndOfDayHttpTests.cs ve
  tests/Host/Experience/Reporting/EndOfDayTestDatabase.cs (V1-RMD-249 sahipliğinde) — yeni gövde ve yeni test

## In scope

- `IBusinessDayTotalsReader` ve PostgreSQL uygulaması; servis kapanışta toplamları ondan alır.
- Kapanış gövdesinden iki alanın kaldırılması.

## Out of scope

- İptal edilen kalem, yazdırma hatası ve garson/yazıcı özetlerinin türetilmesi.
- İade (net ciro): iadeler henüz tamamlanmış tutar üretmiyor (V13-ALC-004).

## Dependencies

- V1-RMD-420

## Acceptance evidence

- Kapanışta doldurulacak.

## Handoff

- None
