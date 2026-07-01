#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
COMPOSE_FILE="$REPO_ROOT/MailBatchSample/docker-compose.yml"
NETWORK_NAME="mail-batch-sample-network"
DEVCONTAINER_ID="${DEVCONTAINER_ID:-$(cat /etc/hostname)}"

printf 'Starting MailBatchSample services...\n'
docker compose -f "$COMPOSE_FILE" up -d --build "$@"

if ! docker network inspect "$NETWORK_NAME" >/dev/null 2>&1; then
  printf 'Docker network %s was not found after compose up.\n' "$NETWORK_NAME" >&2
  exit 1
fi

if ! docker container inspect "$DEVCONTAINER_ID" >/dev/null 2>&1; then
  cat >&2 <<MSG
Could not inspect container "$DEVCONTAINER_ID".
Run this script inside the Dev Container, or set DEVCONTAINER_ID to the Dev Container container ID/name.
MSG
  exit 1
fi

if docker container inspect "$DEVCONTAINER_ID" \
  --format '{{range $name, $_ := .NetworkSettings.Networks}}{{println $name}}{{end}}' \
  | grep -qx "$NETWORK_NAME"; then
  printf 'Dev Container %s is already connected to %s.\n' "$DEVCONTAINER_ID" "$NETWORK_NAME"
else
  printf 'Connecting Dev Container %s to %s...\n' "$DEVCONTAINER_ID" "$NETWORK_NAME"
  docker network connect "$NETWORK_NAME" "$DEVCONTAINER_ID"
fi

printf 'MailBatchSample services are ready for Dev Container access:\n'
printf '  API:  http://mailreceiver-api:8080\n'
printf '  SMTP: mailserver:3025\n'
printf '  IMAP: mailserver:3143\n'
