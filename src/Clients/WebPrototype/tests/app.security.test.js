'use strict';

const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');

const appPath = path.resolve(__dirname, '..', 'app.js');
const source = fs.readFileSync(appPath, 'utf8');
const html = fs.readFileSync(path.resolve(__dirname, '..', 'index.html'), 'utf8');

function loadEscapeHtml() {
  const match = source.match(/function escapeHtml\(str\) \{[\s\S]*?\n  \}/);
  assert.ok(match, 'escapeHtml must remain available to every HTML template renderer.');
  return vm.runInNewContext(`(${match[0]})`);
}

test('HTML encoder neutralizes element and attribute injection characters', () => {
  const escapeHtml = loadEscapeHtml();
  assert.equal(
    escapeHtml('<img src=x onerror="attack()">\'&'),
    '&lt;img src=x onerror=&quot;attack()&quot;&gt;&#39;&amp;'
  );
});

test('modifier templates do not interpolate unencoded catalog strings', () => {
  for (const unsafeInterpolation of ['${g.title}', '${g.id}', '${opt.name}']) {
    assert.equal(
      source.includes(unsafeInterpolation),
      false,
      `${unsafeInterpolation} must be passed through escapeHtml before reaching innerHTML.`
    );
  }
});

test('offline reconnect preserves queued operations until server acknowledgement', () => {
  const start = source.indexOf('const replayOfflineQueue = async () =>');
  const end = source.indexOf('const updateNetworkUI = async () =>', start);
  assert.ok(start >= 0 && end > start, 'Offline replay handler must be present.');
  const replayHandler = source.slice(start, end);

  assert.equal(replayHandler.includes('state.wtrOfflineQueue = []'), false);
  assert.equal(replayHandler.includes('offlineQueueStore.save([])'), false);
  assert.match(replayHandler, /MockRuntime\.replayQueue/);
  assert.match(replayHandler, /state\.wtrOfflineQueue = remaining/);
});

test('versioned mock runtime loads before the UI and queue uses the v2 store', () => {
  assert.ok(html.indexOf('<script src="mock-runtime.js') < html.indexOf('<script src="app.js'));
  assert.match(source, /indexedDB\.open\('alkaros-waiter-pwa', 2\)/);
  assert.match(source, /objectStore\('operations-v2'\)/);
});

test('offline enqueue persists before clearing the visible cart', () => {
  const saveIndex = source.indexOf('await offlineQueueStore.save(persistedQueue)');
  const assignIndex = source.indexOf('state.wtrOfflineQueue = persistedQueue', saveIndex);
  const clearIndex = source.indexOf('state.wtrCart = []', assignIndex);

  assert.ok(saveIndex >= 0, 'Queue persistence call must exist.');
  assert.ok(assignIndex > saveIndex, 'In-memory queue must only update after durable persistence.');
  assert.ok(clearIndex > assignIndex, 'Cart must only clear after durable persistence succeeds.');
});
