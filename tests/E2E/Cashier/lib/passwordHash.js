// Mirrors src/Modules/Identity/Authentication/PasswordHasher.cs exactly
// (PBKDF2-HMAC-SHA256, self-describing encoded format) so a seeded password
// verifies against the real login endpoint - the actual login screen is
// part of what this suite exercises, not a bypassed cookie only.
import { pbkdf2Sync, randomBytes } from 'node:crypto';

const ITERATIONS = 600_000;
const SALT_SIZE = 16;
const HASH_SIZE = 32;
const ALGORITHM_TAG = 'pbkdf2-sha256';

export function hashPassword(password) {
  const salt = randomBytes(SALT_SIZE);
  const hash = pbkdf2Sync(password, salt, ITERATIONS, HASH_SIZE, 'sha256');
  return `${ALGORITHM_TAG}$${ITERATIONS}$${salt.toString('base64')}$${hash.toString('base64')}`;
}
