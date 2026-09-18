// Boots a REAL environment for the Kasa (Cashier/PosTerminal/CustomerDisplay)
// suite: a fresh Postgres database with every V1/V1.1/V1.2 migration
// applied (same order the verified manifest enforces - see lib/migrate.js),
// seeded with one cashier account + the fixed KASA-1 table + two catalog
// products (lib/seed.js), then the actual production Host binary
// (ALKAROS.Host.dll serve ...) serving a web root assembled the same way
// deploy/docker/Dockerfile assembles /srv/app: the built PosTerminal SPA
// (Cashier.tsx at "/", CustomerDisplay at "/display", the screensaver
// settings route at "/settings/screensaver") with the vanilla
// src/Clients/Cashier shell copied under "/cashier" alongside it - both
// real Kasa surfaces share one origin here exactly as they do in
// production, just without the Caddy reverse proxy in front.
//
// Returns a teardown function (Playwright's documented pattern for
// in-process cleanup) that stops the Host and drops the database, so
// nothing outlives the run and no separate global-teardown file is needed.
import { spawn } from 'node:child_process';
import { cpSync, existsSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join, resolve } from 'node:path';
import pg from 'pg';
import { applyAllMigrations } from './lib/migrate.js';
import { seedDatabase } from './lib/seed.js';
import { BASE_URL } from './playwright.config.js';

const HERE = dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = resolve(HERE, '../../..');
const STATE_FILE = join(HERE, '.e2e-state.json');

const PG_HOST = process.env.ALKAROS_TEST_PG_HOST || 'localhost';
const PG_PORT = Number(process.env.ALKAROS_TEST_PG_PORT || 5432);
const PG_USER = process.env.ALKAROS_TEST_PG_USER || 'postgres';
const PG_PASSWORD = process.env.ALKAROS_TEST_PG_PASSWORD || 'postgres';
const DB_NAME = 'alkaros_e2e_cashier';
const KITCHEN_STATION_ID = 'e2e-kasa-station';
const DOTNET_EXE = process.env.DOTNET_EXE || 'C:\\Users\\semih\\.dotnet\\dotnet.exe';
const HOST_DLL = join(REPO_ROOT, 'src', 'Host', 'bin', 'Debug', 'net8.0', 'ALKAROS.Host.dll');
const POS_TERMINAL_DIST = join(REPO_ROOT, 'src', 'Clients', 'PosTerminal', 'dist');
const VANILLA_CASHIER_WWWROOT = join(REPO_ROOT, 'src', 'Clients', 'Cashier', 'wwwroot');

async function waitForReady(url, timeoutMs) {
  const deadline = Date.now() + timeoutMs;
  let lastError;
  while (Date.now() < deadline) {
    try {
      const response = await fetch(url);
      if (response.ok) return;
      lastError = new Error(`Health check returned ${response.status}`);
    } catch (error) {
      lastError = error;
    }
    await new Promise((r) => setTimeout(r, 300));
  }
  throw new Error(`Host never became ready at ${url}: ${lastError}`);
}

export default async function globalSetup() {
  if (!existsSync(POS_TERMINAL_DIST)) {
    throw new Error(
      `PosTerminal is not built - ${POS_TERMINAL_DIST} does not exist. `
      + 'Run `corepack pnpm build` in src/Clients/PosTerminal first (see this suite\'s README).',
    );
  }
  // Same assembly step deploy/docker/Dockerfile's frontend-build stage does
  // (COPY src/Clients/Cashier/wwwroot /out/app/cashier) - idempotent, safe
  // to repeat across runs.
  cpSync(VANILLA_CASHIER_WWWROOT, join(POS_TERMINAL_DIST, 'cashier'), { recursive: true });

  const maintenance = new pg.Client({
    host: PG_HOST, port: PG_PORT, user: PG_USER, password: PG_PASSWORD, database: 'postgres',
  });
  await maintenance.connect();
  await maintenance.query(`DROP DATABASE IF EXISTS ${DB_NAME} WITH (FORCE);`);
  await maintenance.query(`CREATE DATABASE ${DB_NAME};`);
  await maintenance.end();

  const client = new pg.Client({
    host: PG_HOST, port: PG_PORT, user: PG_USER, password: PG_PASSWORD, database: DB_NAME,
  });
  await client.connect();
  const migrationCount = await applyAllMigrations(client, join(REPO_ROOT, 'database', 'migrations'));
  const seed = await seedDatabase(client);
  await client.end();
  console.log(`[global-setup] applied ${migrationCount} migrations, seeded database ${DB_NAME}`);

  writeFileSync(STATE_FILE, JSON.stringify({ baseUrl: BASE_URL, seed }, null, 2));

  const dbUrl = `postgresql://${PG_USER}@${PG_HOST}:${PG_PORT}/${DB_NAME}`;
  const hostProcess = spawn(
    DOTNET_EXE,
    [
      HOST_DLL, 'serve',
      '--db-url', dbUrl,
      '--web-root', POS_TERMINAL_DIST,
      '--urls', BASE_URL,
      '--allow-insecure-loopback-development',
    ],
    {
      env: {
        ...process.env,
        ALKAROS_DB_PASSWORD: PG_PASSWORD,
        ALKAROS_KITCHEN_STATION_ID: KITCHEN_STATION_ID,
        // --allow-insecure-loopback-development's own HTTPS exemption only
        // applies when the ASP.NET Core environment is Development
        // (DualScreenApplication.cs's own check) - unset, it defaults to
        // Production and every plain-HTTP request is refused with
        // HTTPS_REQUIRED (confirmed the same way tests/E2E/WaiterPwa's own
        // global-setup.js found it).
        ASPNETCORE_ENVIRONMENT: 'Development',
        // Same reasoning as tests/E2E/WaiterPwa/global-setup.js: every spec
        // in one run shares the login rate limiter's single per-IP bucket
        // (all from 127.0.0.1). A generous test-only override; production's
        // own default is untouched when this variable is unset.
        ALKAROS_LOGIN_RATE_LIMIT_PERMITS: '1000',
      },
      stdio: ['ignore', 'pipe', 'pipe'],
    },
  );
  let hostOutput = '';
  hostProcess.stdout.on('data', (chunk) => { hostOutput += chunk; if (process.env.E2E_HOST_LOG) process.stdout.write(chunk); });
  hostProcess.stderr.on('data', (chunk) => { hostOutput += chunk; if (process.env.E2E_HOST_LOG) process.stderr.write(chunk); });
  hostProcess.on('exit', (code) => {
    if (code !== null && code !== 0) {
      console.error(`[global-setup] Host process exited early with code ${code}:\n${hostOutput}`);
    }
  });

  try {
    await waitForReady(`${BASE_URL}/health/ready`, 30_000);
  } catch (error) {
    console.error(`[global-setup] Host output so far:\n${hostOutput}`);
    throw error;
  }
  console.log(`[global-setup] Host ready at ${BASE_URL}`);

  return async function globalTeardown() {
    hostProcess.kill();
    const cleanup = new pg.Client({
      host: PG_HOST, port: PG_PORT, user: PG_USER, password: PG_PASSWORD, database: 'postgres',
    });
    await cleanup.connect();
    await cleanup.query(`DROP DATABASE IF EXISTS ${DB_NAME} WITH (FORCE);`);
    await cleanup.end();
    console.log('[global-teardown] Host stopped, database dropped');
  };
}
