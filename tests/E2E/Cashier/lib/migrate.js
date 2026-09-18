// Applies every V1/V1.1/V1.2 migration's .up.sql file, in the same strict
// numeric-position order the verified manifest (database/MigrationComposition
// /order.json) enforces. Sorting by the file's own zero-padded 3-digit
// prefix reproduces that exact order without re-implementing the manifest's
// phase-A/phase-B validation — confirmed 1:1 against order.json's own id
// list before relying on it (every id present has exactly one *.up.sql file
// with that numeric prefix, and vice versa).
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join } from 'node:path';

function collectUpFiles(root) {
  const results = [];
  const walk = (dir) => {
    for (const entry of readdirSync(dir)) {
      const full = join(dir, entry);
      if (statSync(full).isDirectory()) {
        walk(full);
      } else if (entry.endsWith('.up.sql')) {
        const match = /^(\d{3})-/.exec(entry);
        if (!match) throw new Error(`Migration file without a 3-digit prefix: ${full}`);
        results.push({ position: match[1], path: full });
      }
    }
  };
  walk(root);
  results.sort((a, b) => a.position.localeCompare(b.position));
  return results;
}

export async function applyAllMigrations(client, migrationsRoot) {
  const files = collectUpFiles(migrationsRoot);
  if (files.length === 0) throw new Error(`No migration files found under ${migrationsRoot}`);
  for (const file of files) {
    // Some migration files carry a leading UTF-8 BOM (Windows editor
    // artifact); Postgres's parser treats it as a stray character before
    // the first statement and rejects the whole batch.
    const sql = readFileSync(file.path, 'utf8').replace(/^﻿/, '');
    try {
      await client.query(sql);
    } catch (error) {
      throw new Error(`Migration ${file.path} failed: ${error.message}`);
    }
  }
  return files.length;
}
