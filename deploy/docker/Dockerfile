# syntax=docker/dockerfile:1.7
#
# ALKAROS container build. Four stages, two publishable targets:
#
#   backend-build  ->  api   the .NET 8 host, JSON API + SignalR hubs only
#   frontend-build ->  web   Caddy: static bundles + reverse proxy to the api
#
# Build context is the repo root (compose sets `context: .`).

# ---------------------------------------------------------------------------
# backend-build: restore the whole solution (locked) and publish the host.
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0.302@sha256:72dd743782f2ae7e5476fd64f6a460045e3998dc862218b80e6944cba79a01b0 AS backend-build
WORKDIR /src
COPY . .
RUN dotnet restore ALKAROS.slnx --locked-mode
RUN dotnet publish src/Host/ALKAROS.Host.csproj --configuration Release --no-restore --output /out/api

# ---------------------------------------------------------------------------
# test: runs the .NET test suite in the same Debian/Linux environment
# production uses, with a real `psql` client on PATH. PsqlScriptRunner
# (src/Host/Composition/Migrations) shells out to `psql` for every migration,
# so the MigrationComposition test suite depends on a working native psql
# binary; a host machine's own psql (e.g. a broken Windows install) is not
# part of this contract and must never cause a false failure. Never
# published — run on demand via compose.test.yaml.
# ---------------------------------------------------------------------------
FROM backend-build AS test
RUN apt-get update \
    && apt-get install --no-install-recommends -y postgresql-client \
    && rm -rf /var/lib/apt/lists/*
# Test projects target net8.0 (Directory.Build.props keeps that as the
# published runtime target, B-6) but this stage's SDK is 10.0, which only
# bundles its own 10.x shared runtime — `dotnet test` cannot launch a net8.0
# test host without the actual 8.0 shared frameworks alongside it.
COPY --from=mcr.microsoft.com/dotnet/aspnet:8.0@sha256:787c228ea85457bec43c8b084e6ac360b26ea43b5c2fcbe861f721f2e8670dd3 \
    /usr/share/dotnet/shared/Microsoft.NETCore.App /usr/share/dotnet/shared/Microsoft.NETCore.App
COPY --from=mcr.microsoft.com/dotnet/aspnet:8.0@sha256:787c228ea85457bec43c8b084e6ac360b26ea43b5c2fcbe861f721f2e8670dd3 \
    /usr/share/dotnet/shared/Microsoft.AspNetCore.App /usr/share/dotnet/shared/Microsoft.AspNetCore.App

# ---------------------------------------------------------------------------
# frontend-build: build PosTerminal and assemble one static tree with every
# client shell - PosTerminal SPA at the root, the vanilla WaiterPwa and
# Cashier shells under their own path prefixes.
# ---------------------------------------------------------------------------
FROM node:24.6.0-bookworm-slim@sha256:9b741b28148b0195d62fa456ed84dd6c953c1f17a3761f3e6e6797a754d9edff AS frontend-build
WORKDIR /src
COPY src/Clients/PosTerminal/package.json src/Clients/PosTerminal/pnpm-lock.yaml ./
RUN corepack enable && corepack prepare pnpm@11.19.0 --activate && pnpm install --frozen-lockfile
COPY src/Clients/PosTerminal ./
RUN pnpm build
# V1-RMD-141: a second, standalone build of just the NFC customer page
# (nfc.html/vite.nfc.config.ts) - its own isolated dist-nfc/ output, copied
# into the `api` stage below (not here), since the Cloudflare Tunnel
# connector reaches that process over loopback and never goes through this
# `web` image at all (same reasoning V12-CWB-001 already established for
# QR's own --qr-web-root).
RUN pnpm build:nfc-standalone
RUN mkdir -p /out/app && cp -r /src/dist/. /out/app/
COPY src/Clients/WaiterPwa/wwwroot /out/app/waiter
COPY src/Clients/Cashier/wwwroot /out/app/cashier
# V12-CWB-001: the QR customer bundle is deliberately NOT copied here. Its
# own API calls (/api/v1/qr/*) are only ever admitted on the NFC/QR-gated
# origin (DualScreenApplication.cs's origin gate — same pre-existing rule
# NFC's own /api/v1/nfc/* already lives under), which this `web` image's
# Caddy-proxied path never satisfies (no --nfc-origin-header is configured
# for it). Serving the static page here would silently half-work: the page
# loads, every API call it makes 404s. The `api` stage below (--qr-web-root)
# is QR's one real, functional path - the same public relay hostname a
# customer reaches over WiFi or mobile data alike.

# ---------------------------------------------------------------------------
# api: aspnet:8.0-alpine matches <TargetFramework>net8.0</TargetFramework>
# (Directory.Build.props documents the SDK-vs-target-vs-runtime split, B-6).
# Plain HTTP only - `web` terminates TLS and forwards X-Forwarded-Proto: https
# from inside the trusted network. No static assets: `web` serves those.
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine@sha256:b288317d8ed45bb763fa95dbc807cf9d36e3bf9373ec2fac6b6548675f1f4b23 AS api
RUN apk update \
    && apk add --no-cache libcrypto3=3.5.8-r0 libssl3=3.5.8-r0 openssl=3.5.8-r0 postgresql-client ca-certificates curl \
    && rm -rf /var/cache/apk/*
# V12-QRT-001 LocalConnector: cloudflared is a statically-linked Go binary
# (CGO_ENABLED=0 release build) — verified to run unmodified on Alpine/musl,
# no glibc compat layer needed. Pinned by exact release + checksum, same
# convention as the base image digests above.
RUN curl -fsSL -o /usr/local/bin/cloudflared \
        https://github.com/cloudflare/cloudflared/releases/download/2026.8.3/cloudflared-linux-amd64 \
    && echo "f29324fe934d1e100617484c78deef803c4dc2cd351d645bbde42e96b4fccc5e  /usr/local/bin/cloudflared" | sha256sum -c - \
    && chmod +x /usr/local/bin/cloudflared
WORKDIR /app
COPY --from=backend-build /out/api ./
COPY database ./database
COPY build/project-manifest.json ./build/project-manifest.json
# V12-CWB-001/V12-CWB-002: the QR customer app's static bundle, read
# directly by this process (not `web`) - the public relay's tunnel reaches
# this container over loopback and never goes through `web` at all. See
# DualScreenOptions.QrWebRoot's own doc comment for why. Menu and OrderEntry
# are separate owned-surface source directories that merge into one served
# tree - OrderEntry's own order-entry.html/js/css sit alongside Menu's
# index.html/menu-app.js, linked from there (Menu's own cart bar).
COPY src/Apps/CustomerWeb/Menu/wwwroot ./qr-web
COPY src/Apps/CustomerWeb/OrderEntry/wwwroot ./qr-web
# V1-RMD-141: NFC's own customer page, symmetric with QR's above - built by
# frontend-build's separate `pnpm build:nfc-standalone` step (nfc.html,
# base "/nfc/"), served directly by this process so a relay-connected
# customer's phone can actually load it (see DualScreenOptions.NfcWebRoot's
# own doc comment for the gap this closes).
COPY --from=frontend-build /src/dist-nfc ./nfc-web
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 5080
# Compose overrides this entrypoint to inject ALKAROS_DB_PASSWORD from a mounted
# secret; this form keeps a bare `docker run -e ALKAROS_DB_PASSWORD=... ` usable.
ENTRYPOINT ["dotnet", "ALKAROS.Host.dll", "serve", \
    "--db-url", "postgresql://alkaros@postgres:5432/alkaros", \
    "--urls", "http://0.0.0.0:5080", \
    "--api-only", \
    "--customer-display-origin-header", "X-Alkaros-Origin", \
    "--nfc-loopback-origin", \
    "--qr-web-root", "/app/qr-web", \
    "--nfc-web-root", "/app/nfc-web", \
    "--trusted-network", "172.16.0.0/12"]

# ---------------------------------------------------------------------------
# web: Caddy serves the assembled static tree and reverse-proxies the dynamic
# paths to the api. It owns TLS termination and the customer-display origin
# split (finding B-4) - see deploy/docker/Caddyfile.
# ---------------------------------------------------------------------------
FROM caddy:2.9-alpine@sha256:b4e3952384eb9524a887633ce65c752dd7c71314d2c2acf98cd5c715aaa534f0 AS web
COPY deploy/docker/Caddyfile /etc/caddy/Caddyfile
COPY --from=frontend-build /out/app /srv/app
EXPOSE 80 443
