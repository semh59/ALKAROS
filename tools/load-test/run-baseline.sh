#!/bin/sh
# ALKAROS V1 go-live load baseline runner (V1-RMD-087).
#
# Runs tools/load-test/load_test.py against a live ALKAROS stack from a
# throwaway python container on the stack's Docker network, so the generator
# does not compete with the app for CPU.
#
# Usage:
#   ALKAROS_NETWORK=alkaros_default \
#   ALKAROS_HOST_URL=http://alkaros-host-1:5080 \
#   ALKAROS_ADMIN_PASSWORD="$(tr -d '\r\n' < deploy/docker/admin_password)" \
#   tools/load-test/run-baseline.sh
#
# Optional: ALKAROS_ADMIN_USER (default admin), LEVELS (default 1,5,10,20),
#           DURATION (default 20), RATE (default 2), OUT_DIR (default .).
set -eu

NETWORK="${ALKAROS_NETWORK:-alkaros_default}"
HOST_URL="${ALKAROS_HOST_URL:-http://alkaros-host-1:5080}"
ADMIN_USER="${ALKAROS_ADMIN_USER:-admin}"
LEVELS="${LEVELS:-1,5,10,20}"
DURATION="${DURATION:-20}"
RATE="${RATE:-2}"
OUT_DIR="${OUT_DIR:-.}"

if [ -z "${ALKAROS_ADMIN_PASSWORD:-}" ]; then
  echo "run-baseline: set ALKAROS_ADMIN_PASSWORD" >&2
  exit 2
fi

REPO_ROOT="$(cd "$(dirname "$0")/../.." && pwd)"

echo "run-baseline: network=${NETWORK} host=${HOST_URL} levels=${LEVELS} rate=${RATE}/s dur=${DURATION}s"
docker run --rm -v "${REPO_ROOT}:/repo" --network "${NETWORK}" -w /repo python:3.12-slim \
  python tools/load-test/load_test.py \
    --base-url "${HOST_URL}" \
    --login "${ADMIN_USER}:${ALKAROS_ADMIN_PASSWORD}" \
    --terminals "${LEVELS}" \
    --duration "${DURATION}" \
    --rate "${RATE}" \
    --forwarded-proto https \
    --json "${OUT_DIR}/load-baseline.json"
