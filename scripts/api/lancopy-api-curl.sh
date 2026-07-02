#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${LANCOPY_API_URL:-http://127.0.0.1:3489}"
TOKEN="${LANCOPY_API_TOKEN:-}"
TRANSFER_ID="${1:-}"
LOCAL_FILE="${LOCAL_FILE:-/tmp/file.zip}"
LOCAL_DIR="${LOCAL_DIR:-/tmp/data}"
TARGET="${LANCOPY_TARGET:-192.168.1.50:8742}"

if [[ -z "$TOKEN" ]]; then
  echo "Set LANCOPY_API_TOKEN first." >&2
  exit 1
fi

echo "== health =="
curl -fsS "$BASE_URL/api/v1/health" | jq .

echo "== peers =="
curl -fsS -H "X-LanCopy-Token: $TOKEN" "$BASE_URL/api/v1/peers" | jq .

echo "== openapi =="
curl -fsS "$BASE_URL/api/v1/openapi.json" | jq '.info,.paths|keys? // .'

echo "== send =="
curl -fsS -X POST "$BASE_URL/api/v1/transfers/send" \
  -H "Content-Type: application/json" \
  -H "X-LanCopy-Token: $TOKEN" \
  -d "{\"localPath\":\"$LOCAL_FILE\",\"to\":\"$TARGET\"}" | jq .

echo "== sync =="
curl -fsS -X POST "$BASE_URL/api/v1/sync" \
  -H "Content-Type: application/json" \
  -H "X-LanCopy-Token: $TOKEN" \
  -d "{\"localDir\":\"$LOCAL_DIR\",\"to\":\"$TARGET\",\"remoteRoot\":\"backup\"}" | jq .

if [[ -n "$TRANSFER_ID" ]]; then
  echo "== status $TRANSFER_ID =="
  curl -fsS -H "X-LanCopy-Token: $TOKEN" "$BASE_URL/api/v1/transfers/$TRANSFER_ID" | jq .

  echo "== cancel $TRANSFER_ID =="
  curl -fsS -X POST -H "X-LanCopy-Token: $TOKEN" "$BASE_URL/api/v1/transfers/$TRANSFER_ID/cancel" | jq .

  echo "== retry $TRANSFER_ID =="
  curl -fsS -X POST -H "X-LanCopy-Token: $TOKEN" "$BASE_URL/api/v1/transfers/$TRANSFER_ID/retry" | jq .
fi
