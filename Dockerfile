# syntax=docker/dockerfile:1.7
FROM mcr.microsoft.com/dotnet/sdk:10.0.302@sha256:72dd743782f2ae7e5476fd64f6a460045e3998dc862218b80e6944cba79a01b0 AS host-build
WORKDIR /src
COPY . .
RUN dotnet restore ALKAROS.slnx --locked-mode
RUN dotnet publish src/Host/ALKAROS.Host.csproj --configuration Release --no-restore --output /out/host

FROM node:24.6.0-bookworm-slim@sha256:9b741b28148b0195d62fa456ed84dd6c953c1f17a3761f3e6e6797a754d9edff AS ui-build
WORKDIR /src
COPY src/Clients/PosTerminal/package.json src/Clients/PosTerminal/pnpm-lock.yaml ./
RUN corepack enable && corepack prepare pnpm@11.19.0 --activate && pnpm install --frozen-lockfile
COPY src/Clients/PosTerminal ./
RUN pnpm build

# Runtime base is aspnet:8.0-alpine to match <TargetFramework>net8.0</TargetFramework>
# (see Directory.Build.props for the SDK-vs-target-vs-runtime rationale, finding B-6).
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine@sha256:b288317d8ed45bb763fa95dbc807cf9d36e3bf9373ec2fac6b6548675f1f4b23 AS runtime
RUN apk update \
    && apk add --no-cache libcrypto3=3.5.8-r0 libssl3=3.5.8-r0 openssl=3.5.8-r0 postgresql-client ca-certificates curl \
    && rm -rf /var/cache/apk/*
WORKDIR /app
COPY --from=host-build /out/host ./
COPY --from=ui-build /src/dist ./wwwroot
COPY src/Clients/WaiterPwa/wwwroot ./wwwroot/waiter
COPY src/Clients/Cashier/wwwroot ./wwwroot/cashier
COPY database ./database
COPY build/project-manifest.json ./build/project-manifest.json
ENV ASPNETCORE_ENVIRONMENT=Production
# 5080/http is for a trusted TLS-terminating reverse proxy (Caddy) that sets
# X-Forwarded-Proto: https. 5443/https is terminated by the host itself with a
# self-signed certificate (regenerated + cached under /app/tls) so waiter phones
# get a secure context - and therefore a working service worker / offline queue -
# even when the reverse proxy is absent or misconfigured. Override
# --self-signed-host with the LAN hostname/IP the devices use, or mount an
# approved certificate with --tls-cert / --tls-key.
EXPOSE 5080 5443
ENTRYPOINT ["dotnet", "ALKAROS.Host.dll", "serve", "--db-url", "postgresql://alkaros@postgres:5432/alkaros", "--web-root", "/app/wwwroot", "--urls", "http://0.0.0.0:5080;https://0.0.0.0:5443", "--self-signed-host", "localhost", "--trusted-network", "172.16.0.0/12"]
