(function (root, factory) {
  const api = factory();
  if (typeof module === 'object' && module.exports) module.exports = api;
  if (root) root.AlkarosMockRuntime = api;
})(typeof globalThis !== 'undefined' ? globalThis : this, function () {
  'use strict';

  const clone = value => JSON.parse(JSON.stringify(value));
  const toCents = value => Math.round(Number(value || 0) * 100);
  const fromCents = value => value / 100;

  function hashSeed(seed, salt) {
    let hash = (2166136261 ^ salt) >>> 0;
    for (const character of String(seed)) {
      hash ^= character.charCodeAt(0);
      hash = Math.imul(hash, 16777619) >>> 0;
    }
    return hash.toString(16).padStart(8, '0');
  }

  function toDeterministicGuid(seed) {
    const hex = [0, 1, 2, 3].map(salt => hashSeed(seed, salt)).join('');
    return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-4${hex.slice(13, 16)}-a${hex.slice(17, 20)}-${hex.slice(20, 32)}`;
  }

  function normalizeGuid(value) {
    const text = String(value || '');
    return /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(text)
      ? text.toLowerCase()
      : toDeterministicGuid(text);
  }

  function createId() {
    if (globalThis.crypto && typeof globalThis.crypto.randomUUID === 'function') {
      return globalThis.crypto.randomUUID();
    }
    return toDeterministicGuid(`${Date.now()}-${Math.random()}`);
  }

  function createIdempotencyKey() {
    return createId().replace(/-/g, '');
  }

  function createTable(source) {
    const table = clone(source);
    table.id = normalizeGuid(source.id);
    table.rowVersion = Number.isInteger(source.rowVersion) ? source.rowVersion : 1;
    const sourceLines = source.openOrderLines || (Number(source.billAmount || 0) > 0 ? [{
      name: 'Existing mock open bill',
      quantity: 1,
      unitPrice: Number(source.billAmount),
      seat: 'shared'
    }] : []);
    table.openOrderLines = sourceLines.map((line, index) => ({
      lineId: line.lineId ? normalizeGuid(line.lineId) : toDeterministicGuid(`${source.id}-line-${index}`),
      productId: line.productId ? normalizeGuid(line.productId) : null,
      name: line.name,
      quantity: Number(line.quantity || 1),
      unitPrice: Number(line.unitPrice || 0),
      seat: String(line.seat || 'shared'),
      paymentStatus: line.paymentStatus || 'Unpaid'
    }));
    return table;
  }

  function createProduct(source) {
    const product = clone(source);
    product.id = normalizeGuid(source.id);
    product.rowVersion = Number.isInteger(source.rowVersion) ? source.rowVersion : 1;
    product.modifierGroups = (product.modifierGroups || []).map((group, groupIndex) => ({
      ...group,
      id: toDeterministicGuid(`${source.id}-${group.id || groupIndex}`)
    }));
    return product;
  }

  function error(code, message, details) {
    return { ok: false, error: { code, message, details: details ? clone(details) : null } };
  }

  function stableSignature(value) {
    if (Array.isArray(value)) return `[${value.map(stableSignature).join(',')}]`;
    if (value && typeof value === 'object') {
      return `{${Object.keys(value).sort().map(key => `${JSON.stringify(key)}:${stableSignature(value[key])}`).join(',')}}`;
    }
    return JSON.stringify(value);
  }

  class MockBackendService {
    constructor(options) {
      const settings = options || {};
      this.createId = settings.createId || createId;
      this.now = settings.now || (() => new Date().toISOString());
      this.latencyMs = Number(settings.latencyMs || 0);
      this.tables = new Map((settings.tables || []).map(table => [table.id, clone(table)]));
      this.orders = new Map();
      this.idempotency = new Map();
      this.failures = { order: [], payment: [], print: [], replay: [] };
    }

    failNext(operation, code, message) {
      if (!this.failures[operation]) throw new Error(`Unsupported mock operation: ${operation}`);
      this.failures[operation].push({ code: code || 'MOCK_FAILURE', message: message || 'Mock service rejected the operation.' });
    }

    bumpTableVersion(tableId, mutate) {
      const table = this.tables.get(tableId);
      if (!table) throw new Error(`Unknown mock table: ${tableId}`);
      if (typeof mutate === 'function') mutate(table);
      table.rowVersion += 1;
      return clone(table);
    }

    getOrderCount() {
      return this.orders.size;
    }

    getTable(tableId) {
      const table = this.tables.get(tableId);
      return table ? clone(table) : null;
    }

    registerTable(table) {
      if (this.tables.has(table.id)) return error('TABLE_ALREADY_EXISTS', 'The mock table already exists.');
      this.tables.set(table.id, clone(table));
      return { ok: true, value: { table: clone(table) } };
    }

    async wait() {
      if (this.latencyMs <= 0) return;
      await new Promise(resolve => setTimeout(resolve, this.latencyMs));
    }

    consumeFailure(operation) {
      const failure = this.failures[operation].shift();
      return failure ? error(failure.code, failure.message) : null;
    }

    async submitOrder(request) {
      await this.wait();
      if (!request || !request.idempotencyKey) return error('VALIDATION_ERROR', 'Idempotency key is required.');

      const signature = stableSignature({
        tableId: request.tableId,
        expectedTableVersion: request.expectedTableVersion,
        lines: request.lines,
        channel: request.channel
      });
      const prior = this.idempotency.get(request.idempotencyKey);
      if (prior) {
        if (prior.signature !== signature) {
          return error('IDEMPOTENCY_CONFLICT', 'The idempotency key was already used with a different request.');
        }
        return clone(prior.result);
      }

      const injected = this.consumeFailure('order');
      if (injected) return injected;
      const table = this.tables.get(request.tableId);
      if (!table) return error('TABLE_NOT_FOUND', 'The mock table does not exist.');
      if (table.rowVersion !== request.expectedTableVersion) {
        return error('TABLE_VERSION_CONFLICT', 'The table changed on another mock terminal.', { serverTable: table });
      }
      if (!Array.isArray(request.lines) || request.lines.length === 0) {
        return error('VALIDATION_ERROR', 'At least one order line is required.');
      }

      const lines = request.lines.map((line, index) => ({
        lineId: line.lineId || this.createId(),
        productId: line.productId || null,
        name: String(line.name || ''),
        quantity: Number(line.quantity || 0),
        unitPrice: Number(line.unitPrice || 0),
        seat: String(line.seat || 'shared'),
        course: String(line.course || 'Main'),
        paymentStatus: 'Unpaid',
        sortOrder: index
      }));
      if (lines.some(line => !line.name || line.quantity <= 0 || line.unitPrice < 0)) {
        return error('VALIDATION_ERROR', 'Order lines contain invalid values.');
      }

      const totalCents = lines.reduce((sum, line) => sum + toCents(line.unitPrice) * line.quantity, 0);
      const order = {
        orderId: this.createId(),
        tableId: table.id,
        rowVersion: 1,
        status: 'Submitted',
        channel: request.channel || 'Cashier',
        idempotencyKey: request.idempotencyKey,
        submittedAt: this.now(),
        lines,
        totalAmount: fromCents(totalCents)
      };
      table.occupancy = 'occupied';
      table.opBadge = 'cooking';
      table.waiter = request.actorName || table.waiter;
      table.minutes = 1;
      table.billAmount = fromCents(toCents(table.billAmount) + totalCents);
      table.openOrderLines = [...(table.openOrderLines || []), ...clone(lines)];
      table.rowVersion += 1;

      this.orders.set(order.orderId, clone(order));
      const result = { ok: true, value: { order: clone(order), table: clone(table) } };
      this.idempotency.set(request.idempotencyKey, { signature, result: clone(result) });
      return result;
    }

    async takePayment(request) {
      await this.wait();
      const injected = this.consumeFailure('payment');
      if (injected) return injected;
      const table = this.tables.get(request.tableId);
      if (!table) return error('TABLE_NOT_FOUND', 'The mock table does not exist.');
      if (table.rowVersion !== request.expectedTableVersion) {
        return error('TABLE_VERSION_CONFLICT', 'The table changed before payment.', { serverTable: table });
      }
      const amountCents = toCents(request.amount);
      if (amountCents <= 0 || amountCents > toCents(table.billAmount)) {
        return error('PAYMENT_AMOUNT_INVALID', 'Payment amount is outside the open balance.');
      }
      const selected = new Set(request.lineIds || []);
      const selectedCents = (table.openOrderLines || [])
        .filter(line => selected.has(line.lineId) && line.paymentStatus !== 'Paid')
        .reduce((sum, line) => sum + toCents(line.unitPrice) * Number(line.quantity), 0);
      if (selected.size === 0 || selectedCents !== amountCents) {
        return error('PAYMENT_ALLOCATION_INVALID', 'Selected mock order lines do not match the payment amount.');
      }
      table.openOrderLines = (table.openOrderLines || []).map(line => (
        selected.has(line.lineId) ? { ...line, paymentStatus: 'Paid' } : line
      ));
      table.billAmount = fromCents(toCents(table.billAmount) - amountCents);
      table.rowVersion += 1;
      const payment = {
        paymentId: this.createId(),
        tableId: table.id,
        amount: fromCents(amountCents),
        method: request.method,
        status: 'Approved',
        processedAt: this.now()
      };
      return { ok: true, value: { payment, table: clone(table) } };
    }

    async printPrebill(request) {
      await this.wait();
      const injected = this.consumeFailure('print');
      if (injected) return injected;
      const table = this.tables.get(request.tableId);
      if (!table) return error('TABLE_NOT_FOUND', 'The mock table does not exist.');
      table.opBadge = 'bill-requested';
      table.rowVersion += 1;
      return {
        ok: true,
        value: {
          printJobId: this.createId(),
          tableId: table.id,
          status: 'Acknowledged',
          acknowledgedAt: this.now(),
          table: clone(table)
        }
      };
    }

    async acknowledgeOperation(envelope) {
      const injected = this.consumeFailure('replay');
      if (injected) return injected;
      if (!envelope || envelope.schemaVersion !== 1 || envelope.operationType !== 'SubmitOrder') {
        return error('QUEUE_ENVELOPE_INVALID', 'The queued operation envelope is not supported.');
      }
      let payload;
      try {
        payload = JSON.parse(envelope.payloadJson);
      } catch (_error) {
        return error('QUEUE_PAYLOAD_INVALID', 'The queued operation payload is not valid JSON.');
      }
      const result = await this.submitOrder({ ...payload, idempotencyKey: envelope.idempotencyKey });
      if (!result.ok) return result;
      return {
        ok: true,
        value: {
          operationId: envelope.operationId,
          acknowledgedAt: this.now(),
          order: result.value.order,
          table: result.value.table
        }
      };
    }
  }

  function createOperationEnvelope(payload, options) {
    const settings = options || {};
    const nextId = settings.createId || createId;
    const nextIdempotencyKey = settings.createIdempotencyKey || createIdempotencyKey;
    const now = settings.now || (() => new Date().toISOString());
    return {
      schemaVersion: 1,
      operationId: nextId(),
      idempotencyKey: payload.idempotencyKey || nextIdempotencyKey(),
      operationType: 'SubmitOrder',
      payloadJson: JSON.stringify({ ...payload, idempotencyKey: undefined }),
      queuedAt: now(),
      retryAttempts: 0
    };
  }

  function isSessionValid(session, now) {
    return Boolean(
      session &&
      session.isActive &&
      !session.isRevoked &&
      Date.parse(session.expiresAt) > Date.parse(now || new Date().toISOString())
    );
  }

  async function replayQueue(operations, service, persist, options) {
    const settings = options || {};
    if (settings.session && !isSessionValid(settings.session, settings.now?.())) {
      return {
        ok: false,
        error: { code: 'SESSION_INVALID', message: 'The mock waiter session is expired or revoked.', details: null },
        acknowledgements: [],
        remaining: clone(operations || [])
      };
    }
    let remaining = clone(operations || []);
    const acknowledgements = [];
    while (remaining.length > 0) {
      const current = remaining[0];
      const result = await service.acknowledgeOperation(current);
      if (!result.ok) {
        remaining[0] = { ...current, retryAttempts: Number(current.retryAttempts || 0) + 1 };
        await persist(clone(remaining));
        return { ok: false, error: result.error, acknowledgements, remaining };
      }
      acknowledgements.push(result.value);
      remaining = remaining.slice(1);
      await persist(clone(remaining));
    }
    return { ok: true, acknowledgements, remaining };
  }

  function deriveSplitPayment(lines, seat, openBalance) {
    const selectedLines = (lines || []).filter(line => (
      String(line.seat || 'shared') === String(seat) && line.paymentStatus !== 'Paid'
    ));
    const amountCents = selectedLines.reduce((sum, line) => (
      sum + toCents(line.unitPrice) * Number(line.quantity || 0)
    ), 0);
    return {
      seat: String(seat),
      lineIds: selectedLines.map(line => line.lineId),
      items: clone(selectedLines),
      amount: fromCents(amountCents),
      remaining: fromCents(Math.max(0, toCents(openBalance) - amountCents))
    };
  }

  class MockSessionLock {
    constructor(options) {
      const settings = options || {};
      this.pin = String(settings.pin || '1234');
      this.maxAttempts = Number(settings.maxAttempts || 3);
      this.cooldownMs = Number(settings.cooldownMs || 30000);
      this.now = settings.now || Date.now;
      this.failedAttempts = 0;
      this.cooldownUntil = 0;
    }

    remainingCooldownMs() {
      return Math.max(0, this.cooldownUntil - this.now());
    }

    submit(pin) {
      const remaining = this.remainingCooldownMs();
      if (remaining > 0) {
        return error('PIN_COOLDOWN', 'The mock session is temporarily locked.', { remainingMs: remaining });
      }
      if (this.cooldownUntil > 0) {
        this.cooldownUntil = 0;
        this.failedAttempts = 0;
      }
      if (String(pin) === this.pin) {
        this.failedAttempts = 0;
        return { ok: true, value: { unlocked: true } };
      }
      this.failedAttempts += 1;
      const attemptsRemaining = Math.max(0, this.maxAttempts - this.failedAttempts);
      if (attemptsRemaining === 0) {
        this.cooldownUntil = this.now() + this.cooldownMs;
        return error('PIN_COOLDOWN', 'Too many incorrect mock PIN attempts.', {
          remainingMs: this.cooldownMs,
          attemptsRemaining: 0
        });
      }
      return error('PIN_INVALID', 'The mock PIN is incorrect.', { attemptsRemaining });
    }
  }

  return {
    MockBackendService,
    MockSessionLock,
    createId,
    createIdempotencyKey,
    createOperationEnvelope,
    createProduct,
    createTable,
    deriveSplitPayment,
    isSessionValid,
    replayQueue,
    toDeterministicGuid
  };
});
