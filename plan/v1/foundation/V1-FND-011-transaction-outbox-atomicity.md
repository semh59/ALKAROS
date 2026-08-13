# V1-FND-011 - Correct transaction-outbox atomicity

- Task ID: V1-FND-011
- Status: Done
- Assignee: opencode-v1-fnd-011
- Work type: implementation
- Surface state: Existing

## Source basis

- CORR:C28
- CORR:C32

## Goal

Domain write ve Outbox enqueue islemlerini ayni PostgreSQL transaction'ina
baglamak; tek commit veya tam rollback disinda kalici sonuc birakmamak.

## Owned surface

- `src/BuildingBlocks/Transactions/DefaultRetryClassifier.cs`
- `src/BuildingBlocks/Transactions/IRetryClassifier.cs`
- `src/BuildingBlocks/Transactions/ITransactionContext.cs`
- `src/BuildingBlocks/Transactions/ITransactionResource.cs`
- `src/BuildingBlocks/Transactions/ITransientFailure.cs`
- `src/BuildingBlocks/Transactions/NestedTransactionException.cs`
- `src/BuildingBlocks/Transactions/RetryClassification.cs`
- `src/BuildingBlocks/Transactions/TransactionContext.cs`
- `src/BuildingBlocks/Transactions/TransactionExecutionException.cs`
- `src/BuildingBlocks/Transactions/TransactionOptions.cs`
- `src/BuildingBlocks/Transactions/TransactionRetryPolicy.cs`
- `src/BuildingBlocks/TransactionOutboxIntegration/TransactionOutbox.cs`
- `src/BuildingBlocks/TransactionOutboxIntegration/TransactionOutboxResource.cs`
- `tests/BuildingBlocks/TransactionOutboxIntegration/Fixtures/Sinks.cs`
- `tests/BuildingBlocks/TransactionOutboxIntegration/Fixtures/TransactionOutboxTestDatabase.cs`
- `tests/BuildingBlocks/TransactionOutboxIntegration/TransactionOutboxIntegrationTests.cs`
- `tests/BuildingBlocks/TransactionOutboxIntegration/ALKAROS.TransactionOutboxIntegration.Tests.csproj`
- `tests/BuildingBlocks/Transactions/Concurrency/TransactionConcurrencyTests.cs`
- `tests/BuildingBlocks/Transactions/Propagation/TransactionPropagationTests.cs`
- `tests/BuildingBlocks/Transactions/Retry/TransactionRetryTests.cs`
- `tests/BuildingBlocks/Transactions/ALKAROS.Transactions.Tests.csproj`
- `evidence/V1-FND-011/**`
- C52 rollback-exhaustion remediation surface is transferred to V1-FND-020; this historical task remains closed.

## In scope

- Ortak connection ve transaction nesnesini transaction scope tarafindan
  yasam dongusu boyunca yonetmek.
- Outbox insertlerini resource'un kendi transaction'i yerine bu ortak
  transaction ile calistirmak.
- Kalici domain write ile Outbox hatasi arasindaki failure window'u gercek
  PostgreSQL integration testinde kanitlamak.

## Out of scope

- Module repository'lerini transaction mekanizmasina tasimak, Outbox schema,
  dispatcher, provider transport veya domain event mapping.

## Dependencies

- V0-GOV-003
- V1-FND-002
- V1-FND-005
- V1-FND-006

## Deliverables

- Ortak PostgreSQL transaction mekanizmasi ve domain/Outbox atomicity
  integration testleri.

## Acceptance evidence

- Domain write ile Outbox row ayni transaction'da commit edilir.
- Outbox write veya commit hatasinda domain write ve Outbox row kalici olmaz.
- Tekrar denemesi yeni transaction kullanir ve tum ilgili testler exit code 0
  verir.

## Handoff

- V1-IAM-004
- V1-ORD-002
- V12-PAY-004
