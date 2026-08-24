const assert = require('node:assert/strict');
const { EventEmitter } = require('node:events');
const test = require('node:test');

const {
  appendBounded,
  chromeCandidates,
  getCDPTarget,
  resolveChromeExecutable,
  stopProcess,
  waitForCDPTarget,
  waitForDevToolsPort
} = require('../run_e2e_browser_test.js');

test('configured Chrome path must exist', () => {
  assert.throws(
    () => resolveChromeExecutable({
      env: { ALKAROS_CHROME_PATH: 'C:\\missing\\chrome.exe' },
      platform: 'win32',
      pathExists: () => false
    }),
    /does not point to a file/
  );
});

test('Chrome discovery uses platform environment paths without a user-specific directory', () => {
  const expected = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';
  const env = {
    PROGRAMFILES: 'C:\\Program Files',
    'PROGRAMFILES(X86)': 'C:\\Program Files (x86)',
    LOCALAPPDATA: 'C:\\Users\\runner\\AppData\\Local'
  };

  assert.equal(resolveChromeExecutable({ env, platform: 'win32', pathExists: path => path === expected }), expected);
  assert.deepEqual(chromeCandidates(env, 'win32')[0], expected);
});

test('stderr capture keeps only the configured tail', () => {
  assert.equal(appendBounded('1234', Buffer.from('56789'), 6), '456789');
});

test('DevTools port discovery retries and validates the published port', async () => {
  let reads = 0;
  const port = await waitForDevToolsPort('C:\\profile', {
    attempts: 3,
    readText: async () => {
      reads++;
      if (reads === 1) throw new Error('not ready');
      return '54321\n/devtools/browser/example';
    },
    sleepFn: async () => {}
  });

  assert.equal(port, 54321);
  assert.equal(reads, 2);
});

test('CDP target lookup rejects invalid HTTP responses', async () => {
  await assert.rejects(
    getCDPTarget(9222, async () => ({ ok: false, status: 503 })),
    /HTTP 503/
  );
});

test('CDP retry reports the last connection error', async () => {
  let attempts = 0;
  await assert.rejects(
    waitForCDPTarget(9222, {
      attempts: 3,
      loadTarget: async () => {
        attempts++;
        throw new Error(`connection-${attempts}`);
      },
      sleepFn: async () => {}
    }),
    /Last error: connection-3/
  );
  assert.equal(attempts, 3);
});

test('Chrome cleanup waits for the child process to exit', async () => {
  const child = new EventEmitter();
  child.exitCode = null;
  child.kill = signal => {
    assert.equal(signal, undefined);
    queueMicrotask(() => {
      child.exitCode = 0;
      child.emit('exit', 0);
    });
    return true;
  };

  await stopProcess(child, 50);
  assert.equal(child.exitCode, 0);
  assert.equal(child.listenerCount('exit'), 0);
});
