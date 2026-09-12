// Boots a REAL environment for the suite: a fresh Postgres database with
// every V1/V1.1/V1.2 migration applied (same order the verified manifest
// enforces - see lib/migrate.js), seeded with one waiter account + table +
// products (lib/seed.js), then the actual production Host binary
// (ALKAROS.Host.dll serve ...) serving WaiterPwa's own wwwroot on the same
// origin as its API - exactly how it runs in production, just without the
// Caddy reverse proxy merging multiple client bundles.
//
// Returns a teardown function (Playwright's documented pattern for
// in-process cleanup) that stops the Host and drops the database, so
// nothing outlives the run and no separate global-teardown file is needed.
import { spawn } from 'node:child_process';
import { writeFileSync } from 'node:fs';
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
const DB_NAME = 'alkaros_e2e_waiterpwa';
const KITCHEN_STATION_ID = 'e2e-station';
const DOTNET_EXE = process.env.DOTNET_EXE || 'C:\\Users\\semih\\.dotnet\\dotnet.exe';
const HOST_DLL = join(REPO_ROOT, 'src', 'Host', 'bin', 'Debug', 'net8.0', 'ALKAROS.Host.dll');
const WEB_ROOT = join(REPO_ROOT, 'src', 'Clients', 'WaiterPwa', 'wwwroot');

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
      '--web-root', WEB_ROOT,
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
        // HTTPS_REQUIRED, found the first time this harness actually ran.
        ASPNETCORE_ENVIRONMENT: 'Development',
        // Root-caused a real full-suite-only flake (2026-09-12): the login
        // endpoint's rate limiter partitions by remote IP alone, 10/minute
        // - every Playwright test in one run shares that single bucket
        // (all from 127.0.0.1), so specs 01-04's own logins had already
        // spent most of the window before 05's load test fired 6 more
        // concurrent ones, tipping 2 into a real 429 (confirmed via
        // E2E_HOST_LOG). A generous test-only override; production's own
        // default (10/minute) is untouched when this variable is unset.
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
