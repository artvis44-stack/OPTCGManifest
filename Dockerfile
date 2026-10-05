# syntax=docker/dockerfile:1
#
# One image for the site and the worker: `docker run <image>` serves, and
# `docker run <image> worker` runs background jobs. Everything it keeps lives
# under /data (MANIFEST_ROOT); with PostgreSQL and object storage configured,
# that is only the catalogue file and the odd debug scan.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Manifest/Manifest.csproj Manifest/
RUN dotnet restore Manifest/Manifest.csproj
COPY Manifest/ Manifest/
RUN dotnet publish Manifest/Manifest.csproj -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
# tesseract for free local scanning; curl for the compose health checks.
RUN apt-get update \
 && apt-get install -y --no-install-recommends tesseract-ocr curl \
 && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /out/ ./
COPY catalog.json ./catalog.json
COPY docker/entrypoint.sh /usr/local/bin/manifest-entrypoint
RUN chmod +x /usr/local/bin/manifest-entrypoint \
 && mkdir -p /data && chown "$APP_UID" /data

ENV MANIFEST_ROOT=/data \
    MANIFEST_ENV=Production \
    DOTNET_gcServer=0
USER $APP_UID
EXPOSE 8420

ENTRYPOINT ["manifest-entrypoint"]
# Behind Caddy: listen on every interface, trust its X-Forwarded-* headers.
CMD ["--host", "0.0.0.0", "--behind-proxy"]
