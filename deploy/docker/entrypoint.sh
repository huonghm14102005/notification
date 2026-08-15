#!/bin/sh
set -eu

case "${1:-api}" in
  api) exec dotnet /app/api/Notification.Api.dll ;;
  migrate) shift; exec dotnet /app/api/Notification.Api.dll --migrate "${1:-latest}" ;;
  worker) exec dotnet /app/worker/Notification.Worker.dll ;;
  *) echo "Unknown process: $1" >&2; exit 64 ;;
esac
