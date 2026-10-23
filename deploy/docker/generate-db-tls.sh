#!/bin/sh
# Creates the database's TLS material (V1-RMD-700): a private CA and a server certificate for the compose service
# name `postgres`. Writes db_tls_ca.crt, db_tls.crt and db_tls.key next to this script (all gitignored), or into the
# directory given as the first argument. Clients verify the server against the CA (verify-full), so the
# certificate must name the host they connect to: `postgres` inside the compose network, plus `localhost`.
#
# Re-run it before the certificate expires (825 days) or to rotate it, then restart `postgres` and every client.
# Refuses to overwrite existing material unless ALKAROS_TLS_FORCE=1.
set -eu
# Git Bash on Windows would otherwise rewrite the "/CN=..." subject as a file path.
export MSYS_NO_PATHCONV=1

OUT="${1:-$(dirname "$0")}"
# `pwd -W` is the Windows spelling of the path under Git Bash (a native openssl cannot open /d/...); elsewhere plain `pwd`.
OUT="$(cd "$OUT" && { pwd -W 2>/dev/null || pwd; })"
DAYS="${ALKAROS_TLS_DAYS:-825}"

if [ -e "$OUT/db_tls.key" ] && [ "${ALKAROS_TLS_FORCE:-0}" != "1" ]; then
  echo "refusing to overwrite $OUT/db_tls.key; set ALKAROS_TLS_FORCE=1 to rotate" >&2
  exit 5
fi

# Inside the output directory (not /tmp): a native Windows openssl cannot open MSYS paths such as /tmp.
WORK="$(mktemp -d "$OUT/.tls.XXXXXX")"
trap 'rm -f "$WORK"/*; rmdir "$WORK"' EXIT

cat > "$WORK/server.cnf" <<'CNF'
[req]
distinguished_name = dn
prompt = no
[dn]
CN = postgres
[ext]
subjectAltName = DNS:postgres, DNS:localhost, IP:127.0.0.1
basicConstraints = CA:FALSE
keyUsage = digitalSignature, keyEncipherment
extendedKeyUsage = serverAuth
CNF

openssl req -quiet -x509 -newkey rsa:3072 -nodes -days "$DAYS" -subj "/CN=ALKAROS database CA" \
  -addext "basicConstraints=critical,CA:TRUE" -addext "keyUsage=critical,keyCertSign,cRLSign" \
  -keyout "$WORK/ca.key" -out "$OUT/db_tls_ca.crt" >/dev/null
openssl req -quiet -newkey rsa:3072 -nodes -keyout "$OUT/db_tls.key" -out "$WORK/server.csr" -config "$WORK/server.cnf" >/dev/null
openssl x509 -req -in "$WORK/server.csr" -CA "$OUT/db_tls_ca.crt" -CAkey "$WORK/ca.key" -set_serial "0x$(openssl rand -hex 16)" \
  -days "$DAYS" -extfile "$WORK/server.cnf" -extensions ext -out "$OUT/db_tls.crt"

# Docker mounts a file secret with the file's own mode; the postgres container copies the key to a 0600 file of its
# own, so here it only has to be unreadable to other accounts on the host.
chmod 600 "$OUT/db_tls.key"
chmod 644 "$OUT/db_tls.crt" "$OUT/db_tls_ca.crt"
echo "wrote $OUT/db_tls_ca.crt, $OUT/db_tls.crt, $OUT/db_tls.key"
