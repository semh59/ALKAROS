# Third-party package license policy

Every package comes from `Directory.Packages.props` (central versions) and is pinned by the `packages.lock.json` of each project; restore runs in locked mode, so a new or changed package cannot enter silently.

Rules:

- Allowed without review: MIT, Apache-2.0, BSD-2/3-Clause and the Microsoft .NET libraries that ship under MIT.
- A package under any other license (commercial, source-available, copyleft such as GPL/AGPL, or "free for non-commercial use") needs a Product Owner decision before it is added.
- `FluentAssertions` stays on 6.x on purpose: from 8.0 the package moved to a commercial license for commercial use. Raising it past 6.x is a licensing decision, not a routine bump.
- A version bump re-checks the license of the new version; a license can change between versions.
- The package list and versions live only in `Directory.Packages.props`; this file does not copy them, so it cannot drift.

Not verified here: a machine-readable license inventory of the transitive packages. Producing one (for example with a license-listing tool in the release pipeline) is a separate piece of work.

Known exceptions awaiting a Product Owner decision (found by the BuildingBlocks audit, V1-RMD-596):

- `Npgsql` ships under the PostgreSQL License, a permissive OSI-approved license comparable to MIT, but it is not in the allowed list above; the decision to add it to the list is open.
- Four transitive packages publish no machine-readable license expression in their package metadata (for example `System.Configuration.ConfigurationManager` 4.4.0); their license was not independently verified.
