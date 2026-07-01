#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
COMPOSE_FILE="$REPO_ROOT/MailBatchSample/docker-compose.yml"
NETWORK_NAME="mail-batch-sample-network"
DEVCONTAINER_ID="${DEVCONTAINER_ID:-$(cat /etc/hostname)}"

if docker network inspect "$NETWORK_NAME" >/dev/null 2>&1 \
  && docker container inspect "$DEVCONTAINER_ID" >/dev/null 2>&1 \
  && docker container inspect "$DEVCONTAINER_ID" \
    --format '{{range $name, $_ := .NetworkSettings.Networks}}{{println $name}}{{end}}' \
    | grep -qx "$NETWORK_NAME"; then
  printf 'Disconnecting Dev Container %s from %s...\n' "$DEVCONTAINER_ID" "$NETWORK_NAME"
  docker network disconnect "$NETWORK_NAME" "$DEVCONTAINER_ID"
fi

printf 'Stopping MailBatchSample services...\n'
docker compose -f "$COMPOSE_FILE" down "$@"
