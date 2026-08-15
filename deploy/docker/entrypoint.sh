#!/bin/sh
set -eu

if [ -n "${TRUSTED_CA_FILE:-}" ] && [ -f "$TRUSTED_CA_FILE" ]; then
  cp "$TRUSTED_CA_FILE" /usr/local/share/ca-certificates/notification-test-ca.crt
  update-ca-certificates >/dev/null
fi

case "${1:-api}" in
  api) exec dotnet /app/api/Notification.Api.dll ;;
  migrate) shift; exec dotnet /app/api/Notification.Api.dll --migrate "${1:-latest}" ;;
  worker) exec dotnet /app/worker/Notification.Worker.dll ;;
  *) echo "Unknown process: $1" >&2; exit 64 ;;
esac
