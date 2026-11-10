# V1-RMD-919 result

Verified first: no production code writes `security.retention_subjects` (the only InsertAsync is the store itself) and nothing references `DataCategory.CustomerPii` outside the matrix and enum; customer retention lives in the Privacy classes (2y / 10y). The category value stays (persisted CHECK in migration 137, 9-category inventory); its sweep period is now null with the reason in a comment, and the runbook sentence is corrected.
Red: new test failed (10 years found), green 44/44 Security.DataProtectionRetention tests, mutant killed.
Open: the whole generic retention_subjects sweep has no record producer in production (not removed here, larger decision).
Review follow-up: CustomerProfile still carried a 10-year `RetentionYears` constant and an `IsRetentionExpired` method that only two tests called (no production caller): both and their tests were deleted (CustomerData.Profiles tests 60/60), stale comments fixed.
