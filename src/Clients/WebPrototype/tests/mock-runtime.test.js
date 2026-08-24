'use strict';

const assert = require('node:assert/strict');
const test = require('node:test');
const {
  MockBackendService,
  MockSessionLock,
  createIdempotencyKey,
  createOperationEnvelope,
  createProduct,
  createTable,
  deriveSplitPayment,
  replayQueue,
  toDeterministicGuid
} = require('../mock-runtime.js');

test('mock identifiers and row versions match client contract primitives', () => {
  const table = createTable({ id: 'legacy-table', number: 'S-01' });
  const product = createProduct({ id: 'legacy-product', name: 'Meal', modifierGroups: [] });
  const guidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-a[0-9a-f]{3}-[0-9a-f]{12}$/;

  assert.match(table.id, guidPattern);
  assert.match(product.id, guidPattern);
  assert.equal(table.rowVersion, 1);
  assert.equal(product.rowVersion, 1);
  assert.match(createIdempotencyKey(), /^[0-9a-f]{32}$/);
});

function idFactory() {
  let next = 1;
  return () => toDeterministicGuid(`test-${next++}`);
}

function tableFixture() {
  return createTable({
    id: 'table-1',
    number: 'S-01',
    occupancy: 'available',
    billAmount: 0,
    openOrderLines: []
  });
}

function orderRequest(table, idempotencyKey, name = 'Burger') {
  return {
    tableId: table.id,
    expectedTableVersion: table.rowVersion,
    idempotencyKey,
    channel: 'Cashier',
    actorName: 'Mock Cashier',
    lines: [{
      lineId: toDeterministicGuid(`${idempotencyKey}-line`),
      productId: toDeterministicGuid('burger'),
      name,
      quantity: 1,
      unitPrice: 240,
      seat: '1',
      course: 'Main'
    }]
  };
}

test('duplicate order submission returns the original result and creates one order', async () => {
  const table = tableFixture();
  const service = new MockBackendService({ tables: [table], createId: idFactory() });
  const request = orderRequest(table, toDeterministicGuid('same-request'));

  const first = await service.submitOrder(request);
  const second = await service.submitOrder(request);

  assert.equal(first.ok, true);
  assert.deepEqual(second, first);
  assert.equal(service.getOrderCount(), 1);
  assert.equal(service.getTable(table.id).billAmount, 240);
});

test('reusing an idempotency key with another body is rejected without mutation', async () => {
  const table = tableFixture();
  const service = new MockBackendService({ tables: [table], createId: idFactory() });
  const key = toDeterministicGuid('body-conflict');
  assert.equal((await service.submitOrder(orderRequest(table, key))).ok, true);

  const conflict = await service.submitOrder(orderRequest(table, key, 'Different item'));

  assert.equal(conflict.ok, false);
  assert.equal(conflict.error.code, 'IDEMPOTENCY_CONFLICT');
  assert.equal(service.getOrderCount(), 1);
});

test('table version conflict and injected failure preserve server state', async () => {
  const table = tableFixture();
  const service = new MockBackendService({ tables: [table], createId: idFactory() });
  service.bumpTableVersion(table.id);

  const conflict = await service.submitOrder(orderRequest(table, toDeterministicGuid('stale')));
  assert.equal(conflict.ok, false);
  assert.equal(conflict.error.code, 'TABLE_VERSION_CONFLICT');
  assert.equal(service.getOrderCount(), 0);

  const fresh = service.getTable(table.id);
  service.failNext('order', 'MOCK_ORDER_FAILURE', 'Mock order failure.');
  const failed = await service.submitOrder(orderRequest(fresh, toDeterministicGuid('failure')));
  assert.equal(failed.ok, false);
  assert.equal(failed.error.code, 'MOCK_ORDER_FAILURE');
  assert.equal(service.getOrderCount(), 0);
  assert.equal(service.getTable(table.id).billAmount, 0);
});

test('offline replay is FIFO, removes acknowledged entries, and stops at first rejection', async () => {
  const firstTable = tableFixture();
  const secondTable = createTable({ id: 'table-2', number: 'S-02', billAmount: 0 });
  const service = new MockBackendService({ tables: [firstTable, secondTable], createId: idFactory() });
  const envelopeId = idFactory();
  const queue = [
    createOperationEnvelope(orderRequest(firstTable, toDeterministicGuid('queue-1')), { createId: envelopeId }),
    createOperationEnvelope(orderRequest(secondTable, toDeterministicGuid('queue-2')), { createId: envelopeId })
  ];
  const persisted = [];
  const calls = [];
  const acknowledge = service.acknowledgeOperation.bind(service);
  service.acknowledgeOperation = async envelope => {
    calls.push(envelope.operationId);
    if (calls.length === 2) service.failNext('replay', 'MOCK_REPLAY_REJECTED', 'Replay rejected.');
    return acknowledge(envelope);
  };

  const result = await replayQueue(queue, service, operations => {
    persisted.push(operations);
  });

  assert.equal(result.ok, false);
  assert.deepEqual(calls, queue.map(item => item.operationId));
  assert.equal(result.acknowledgements.length, 1);
  assert.equal(result.remaining.length, 1);
  assert.equal(result.remaining[0].operationId, queue[1].operationId);
  assert.equal(result.remaining[0].retryAttempts, 1);
  assert.equal(persisted[0].length, 1);
  assert.equal(persisted.at(-1).length, 1);
});

test('successful replay persists an empty queue only after both acknowledgements', async () => {
  const firstTable = tableFixture();
  const secondTable = createTable({ id: 'table-2', number: 'S-02', billAmount: 0 });
  const service = new MockBackendService({ tables: [firstTable, secondTable], createId: idFactory() });
  const envelopeId = idFactory();
  const queue = [
    createOperationEnvelope(orderRequest(firstTable, toDeterministicGuid('queue-ok-1')), { createId: envelopeId }),
    createOperationEnvelope(orderRequest(secondTable, toDeterministicGuid('queue-ok-2')), { createId: envelopeId })
  ];
  const sizes = [];

  const result = await replayQueue(queue, service, operations => sizes.push(operations.length));

  assert.equal(result.ok, true);
  assert.equal(result.acknowledgements.length, 2);
  assert.deepEqual(sizes, [1, 0]);
  assert.equal(service.getOrderCount(), 2);
});

test('expired waiter session cannot replay or remove queued operations', async () => {
  const table = tableFixture();
  const service = new MockBackendService({ tables: [table], createId: idFactory() });
  const queue = [createOperationEnvelope(orderRequest(table, toDeterministicGuid('expired-session')))];
  let persistCalled = false;

  const result = await replayQueue(queue, service, () => {
    persistCalled = true;
  }, {
    session: { isActive: true, isRevoked: false, expiresAt: '2026-01-01T00:00:00.000Z' },
    now: () => '2026-08-24T00:00:00.000Z'
  });

  assert.equal(result.ok, false);
  assert.equal(result.error.code, 'SESSION_INVALID');
  assert.equal(result.remaining.length, 1);
  assert.equal(persistCalled, false);
  assert.equal(service.getOrderCount(), 0);
});

test('split payment amount is derived from unpaid order lines', () => {
  const lines = [
    { lineId: '1', name: 'Burger', seat: '1', quantity: 1, unitPrice: 240, paymentStatus: 'Unpaid' },
    { lineId: '2', name: 'Drink', seat: '1', quantity: 2, unitPrice: 30, paymentStatus: 'Unpaid' },
    { lineId: '3', name: 'Paid item', seat: '1', quantity: 1, unitPrice: 50, paymentStatus: 'Paid' },
    { lineId: '4', name: 'Shared item', seat: 'shared', quantity: 1, unitPrice: 85, paymentStatus: 'Unpaid' }
  ];

  assert.deepEqual(deriveSplitPayment(lines, '1', 385), {
    seat: '1',
    lineIds: ['1', '2'],
    items: lines.slice(0, 2),
    amount: 300,
    remaining: 85
  });
});

test('payment and print failures are explicit and do not mutate the table', async () => {
  const table = createTable({
    id: 'table-payment',
    number: 'S-03',
    billAmount: 100,
    openOrderLines: [{ lineId: 'line-1', name: 'Meal', seat: '1', quantity: 1, unitPrice: 100 }]
  });
  const service = new MockBackendService({ tables: [table], createId: idFactory() });
  service.failNext('payment', 'MOCK_PAYMENT_DECLINED', 'Payment declined.');
  service.failNext('print', 'MOCK_PRINTER_OFFLINE', 'Printer offline.');

  const payment = await service.takePayment({
    tableId: table.id,
    expectedTableVersion: table.rowVersion,
    amount: 100,
    method: 'Card',
    lineIds: [table.openOrderLines[0].lineId]
  });
  const print = await service.printPrebill({ tableId: table.id });

  assert.equal(payment.ok, false);
  assert.equal(payment.error.code, 'MOCK_PAYMENT_DECLINED');
  assert.equal(print.ok, false);
  assert.equal(print.error.code, 'MOCK_PRINTER_OFFLINE');
  assert.equal(service.getTable(table.id).billAmount, 100);

  const acknowledgedPrint = await service.printPrebill({ tableId: table.id });
  assert.equal(acknowledgedPrint.ok, true);
  assert.equal(acknowledgedPrint.value.status, 'Acknowledged');
  assert.equal(acknowledgedPrint.value.table.opBadge, 'bill-requested');
  assert.equal(acknowledgedPrint.value.table.rowVersion, table.rowVersion + 1);
});

test('payment rejects a line allocation that does not match the amount', async () => {
  const table = createTable({
    id: 'table-allocation',
    number: 'S-04',
    billAmount: 100,
    openOrderLines: [{ name: 'Meal', seat: '1', quantity: 1, unitPrice: 100 }]
  });
  const service = new MockBackendService({ tables: [table], createId: idFactory() });

  const result = await service.takePayment({
    tableId: table.id,
    expectedTableVersion: table.rowVersion,
    amount: 50,
    method: 'Card',
    lineIds: [table.openOrderLines[0].lineId]
  });

  assert.equal(result.ok, false);
  assert.equal(result.error.code, 'PAYMENT_ALLOCATION_INVALID');
  assert.equal(service.getTable(table.id).billAmount, 100);
});

test('three wrong PIN attempts start cooldown and correct PIN cannot bypass it', () => {
  let now = 1000;
  const lock = new MockSessionLock({ pin: '1234', maxAttempts: 3, cooldownMs: 30000, now: () => now });

  assert.equal(lock.submit('0000').error.code, 'PIN_INVALID');
  assert.equal(lock.submit('0000').error.code, 'PIN_INVALID');
  assert.equal(lock.submit('0000').error.code, 'PIN_COOLDOWN');
  assert.equal(lock.submit('1234').error.code, 'PIN_COOLDOWN');
  now += 30000;
  assert.equal(lock.submit('1234').ok, true);
});
