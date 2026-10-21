# Third-party package license policy

Every package comes from `Directory.Packages.props` (central versions) and is pinned by the `packages.lock.json` of each project; restore runs in locked mode, so a new or changed package cannot enter silently.

Rules:

- Allowed without review: MIT, Apache-2.0, BSD-2/3-Clause, ISC, MIT-0, Unlicense, the PostgreSQL License (Npgsql) and the Microsoft .NET libraries that ship under MIT.
- MPL-2.0 is allowed while the package is used unmodified (it is file-level copyleft: only changes to the package's own files must be shared). Today this covers the build and test tools `axe-core` and `lightningcss`. Patching or vendoring such a package needs a new decision.
- The rules apply to npm packages too (`pnpm-lock.yaml` of each client); GPL, AGPL, SSPL and "non-commercial" licenses are never allowed without a Product Owner decision.
- A package under any other license (commercial, source-available, copyleft such as GPL/AGPL, or "free for non-commercial use") needs a Product Owner decision before it is added.
- `FluentAssertions` stays on 6.x on purpose: from 8.0 the package moved to a commercial license for commercial use. Raising it past 6.x is a licensing decision, not a routine bump.
- A version bump re-checks the license of the new version; a license can change between versions.
- The package list and versions live only in `Directory.Packages.props`; this file does not copy them, so it cannot drift.

Not verified here: a machine-readable license inventory of the transitive packages. Producing one (for example with a license-listing tool in the release pipeline) is a separate piece of work.

Decisions (Product Owner, 2026-10-21): `Npgsql` (PostgreSQL License) is allowed; npm packages follow the same list, with MPL-2.0 allowed unmodified.

Still open (found by the BuildingBlocks audit, V1-RMD-596):

- Four transitive packages publish no machine-readable license expression in their package metadata (for example `System.Configuration.ConfigurationManager` 4.4.0); their license was not independently verified.
