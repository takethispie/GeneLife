#!/usr/bin/env bash
# Restore the seeded V5 preview world without manual steps.
#
# Usage:
#   tools/preview/reset.sh                 # API is already running
#   PREVIEW_API=http://localhost:5288 tools/preview/reset.sh
#
# The script stops the current simulation (discarding the in-memory world),
# starts a fresh map, generates the configured population from a fixed seed and
# saves a snapshot, so repeated runs start from the same world.
set -euo pipefail

API="${PREVIEW_API:-http://localhost:5288}"
SEED="${PREVIEW_SEED:-21}"
SIMS="${PREVIEW_SIMS:-500}"
ADULTS="${PREVIEW_ADULTS:-380}"
CHILDREN="${PREVIEW_CHILDREN:-120}"

token_json="$(curl -fsS -X POST "$API/api/auth/local-admin")"
token="$(python3 -c 'import json,sys; print(json.load(sys.stdin)["accessToken"])' <<<"$token_json")"
auth=(-H "Authorization: Bearer $token" -H "Content-Type: application/json")

echo "preview: stopping simulation at $API"
curl -fsS -X POST "$API/api/simulation/stop" "${auth[@]}" >/dev/null || true

echo "preview: starting fresh map (seed $SEED)"
curl -fsS -X POST "$API/api/simulation/start" "${auth[@]}" >/dev/null

echo "preview: generating $SIMS sims (seed $SEED)"
curl -fsS -X POST "$API/api/population/generate" "${auth[@]}" \
  -d "{\"totalSims\":$SIMS,\"seed\":$SEED,\"adults\":$ADULTS,\"children\":$CHILDREN}" >/dev/null

ready=0
for _ in $(seq 1 120); do
  status="$(curl -fsS "$API/api/data" "${auth[@]}")"
  count="$(python3 -c 'import json,sys; print(len(json.load(sys.stdin).get("sims", [])))' <<<"$status")"
  if [ "$count" -ge "$SIMS" ]; then
    echo "preview: $count sims ready"
    ready=1
    break
  fi
  sleep 0.5
done

if [ "$ready" -ne 1 ]; then
  echo "preview: population did not reach $SIMS sims; check the API logs" >&2
  exit 1
fi

curl -fsS -X POST "$API/api/simulation/save" "${auth[@]}" >/dev/null
echo "preview: seeded snapshot saved"
