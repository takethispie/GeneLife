#!/usr/bin/env bash
# Restore the seeded V5 preview world without manual steps.
#
# Usage:
#   tools/preview/reset.sh                 # API is already running
#   PREVIEW_API=http://localhost:5288 tools/preview/reset.sh
#
# The script creates a fresh map and population from a fixed seed and saves a
# snapshot, so repeated runs start from the same world. /api/map/create is the
# same destructive admin operation the admin UI uses; it replaces any existing
# map, buildings, NPCs and player characters.
set -euo pipefail

API="${PREVIEW_API:-http://localhost:5288}"
SEED="${PREVIEW_SEED:-21}"
GENOSES="${PREVIEW_GENOSES:-500}"
ADULTS="${PREVIEW_ADULTS:-380}"
CHILDREN="${PREVIEW_CHILDREN:-120}"

token_json="$(curl -fsS -X POST "$API/api/auth/local-admin")"
token="$(python3 -c 'import json,sys; print(json.load(sys.stdin)["accessToken"])' <<<"$token_json")"
auth=(-H "Authorization: Bearer $token" -H "Content-Type: application/json")

echo "preview: creating map with $GENOSES genoses (seed $SEED)"
curl -fsS -X POST "$API/api/map/create" "${auth[@]}" \
  -d "{\"seed\":$SEED,\"width\":256,\"height\":256,\"population\":$GENOSES,\"populationSeed\":$SEED,\"adults\":$ADULTS,\"children\":$CHILDREN,\"confirm\":true}" >/dev/null

ready=0
for _ in $(seq 1 120); do
  status="$(curl -fsS "$API/api/data" "${auth[@]}")"
  count="$(python3 -c 'import json,sys; print(len(json.load(sys.stdin).get("genoses", [])))' <<<"$status")"
  if [ "$count" -ge "$GENOSES" ]; then
    echo "preview: $count genoses ready"
    ready=1
    break
  fi
  sleep 0.5
done

if [ "$ready" -ne 1 ]; then
  echo "preview: population did not reach $GENOSES genoses; check the API logs" >&2
  exit 1
fi

echo "preview: world created and snapshot saved"
