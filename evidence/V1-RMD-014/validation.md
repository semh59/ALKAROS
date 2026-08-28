# V1-RMD-014 validation evidence

- Candidate HEAD: `a03d02146961c29a8b847a7b0c472c6c8dd42c9f`
- Executor: `/root/rmd014_catalog_api_retry`
- Date: `2026-08-26`
- PostgreSQL image: `postgres:18-alpine@sha256:d3e1620b530c944afa6e887d22eb899824da68e19c52024bf98f5220c88a65b2`
- Database lifecycle: an ephemeral PostgreSQL 18 container and unique test database were created for the run and
  removed after the test process exited. Credentials were generated in process and were not persisted.

## Commands and results

1. `dotnet build ALKAROS.slnx -c Release --no-restore`
   - Exit code: `0`
   - Result: `0` warnings, `0` errors.
2. `dotnet build tests/Host/Experience/Catalog/ALKAROS.Host.Experience.Catalog.Tests.csproj -c Release --no-restore`
   - Exit code: `0`
   - Result: `0` warnings, `0` errors.
3. `dotnet test tests/Host/Experience/Catalog/ALKAROS.Host.Experience.Catalog.Tests.csproj -c Release --no-build --no-restore`
   - Exit code: `0`
   - Result: `6` passed, `0` failed, `0` skipped against real Kestrel HTTP and PostgreSQL 18.
   - Covered category, tax profile, product, modifier group/modifier, assignment and effective price create/read;
     deterministic resource-bound cursor continuation; page bounds; missing and permission-denied sessions;
     duplicate SKU; negative price; price overlap; missing foreign key; retry after failed mutation; and concurrent
     duplicate-SKU atomicity.
4. `python -B tools/plan-audit/plan_audit_tool.py validate`
   - Exit code: `0`
   - Result: `0` errors, `0` warnings.
5. `git diff --check`
   - Exit code: `0`.

The first focused test attempt used the workstation default PostgreSQL endpoint without a configured password and
failed before database creation. The acceptance run above used the pinned PostgreSQL 18 container and passed; no test
was skipped or replaced by an in-memory implementation.

## Manual scenario

Start a Host that registers `AddCatalogManagement` and `MapCatalogManagement`, create a manager device session whose
role grants `catalog.manage`, and send its raw token in the `alkaros.manager` cookie. Create a category, tax profile,
product, modifier group, modifier, assignment and effective price under `/api/v1/management/catalog`; read them back
through the bounded list and effective-price routes. Repeat the product with the same SKU, send a negative price and
an overlapping interval, then correct each rejected request and resend it. The rejects return stable 400/409 error
codes without partial rows, and the corrected requests succeed.

## SHA-256

```text
619c0e9954f8b46fcd05df45baae1bc94243fb4231480ef4fb1629948c0a5b4a  src/Host/Experience/Catalog/CatalogManagementContracts.cs
9ea33082b9844bf2225b2a57d700a0a0ae275328e021d9ab8de6f43f5463b9a5  src/Host/Experience/Catalog/CatalogManagementEndpoints.cs
a3a85ec9e2700531cb91decbf99951ba151be7bc8f9a9a0ad1ee42b54c5cf188  src/Host/Experience/Catalog/CatalogManagementStore.cs
acd211438773723c8e0da0009290ea6e077c02d9fb6f443900fb9bed94b9ff55  tests/Host/Experience/Catalog/ALKAROS.Host.Experience.Catalog.Tests.csproj
110f46c8016de5228fd1e57ae3c59f1931d521a6dd58176badd5f890aa392b43  tests/Host/Experience/Catalog/CatalogApiTestDatabase.cs
898691c47d1b6d823992c190aa26dd5b24f73aaa33e6f9c8a7505c3d75ee8fec  tests/Host/Experience/Catalog/CatalogManagementHttpTests.cs
264cd14b6240e05975f6d2d8179349a458137c1a6813aadcf8a041f46dfa8b57  tests/Host/Experience/Catalog/packages.lock.json
```
