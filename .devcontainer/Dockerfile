FROM mcr.microsoft.com/devcontainers/dotnet:8.0-bookworm

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        sqlite3 \
        netcat-openbsd \
    && rm -rf /var/lib/apt/lists/*
