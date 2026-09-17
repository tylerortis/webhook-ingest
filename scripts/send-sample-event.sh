#!/usr/bin/env bash
# Sends one signed sample event to a locally running webhook-ingest.
# Usage: WEBHOOK_SECRET=whsec_... ./scripts/send-sample-event.sh [type] [campaign_id] [recipient]
set -euo pipefail

secret="${WEBHOOK_SECRET:?Set WEBHOOK_SECRET to the value of WebhookSigning:SigningSecret}"
url="${WEBHOOK_URL:-http://localhost:5080/webhooks/email}"
type="${1:-email.delivered}"
campaign="${2:-cmp_demo}"
recipient="${3:-reader@example.com}"

id="msg_$(date +%s)_${RANDOM}"
timestamp="$(date +%s)"
created_at="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
body=$(printf '{"type":"%s","created_at":"%s","data":{"campaign_id":"%s","recipient":"%s"}}' \
  "$type" "$created_at" "$campaign" "$recipient")

key_hex=$(printf '%s' "${secret#whsec_}" | base64 -d | od -An -vtx1 | tr -d ' \n')
signature=$(printf '%s' "${id}.${timestamp}.${body}" \
  | openssl dgst -sha256 -mac HMAC -macopt "hexkey:${key_hex}" -binary | base64)

curl -sS -i "$url" \
  -H "Content-Type: application/json" \
  -H "svix-id: ${id}" \
  -H "svix-timestamp: ${timestamp}" \
  -H "svix-signature: v1,${signature}" \
  --data "$body"
echo
