#!/usr/bin/env bash
# Quick check that a deployment works. Safe for production: it creates no orders.
#
# Usage: smoke-test.sh <url> <expected-version>

set -euo pipefail

url="$1"
expected_version="$2"

attempts="${SMOKE_ATTEMPTS:-20}"
delay_seconds="${SMOKE_DELAY_SECONDS:-10}"

# Retries a check, because a container that was scaled to zero needs time to start.
retry() {
  local description="$1"
  shift

  for ((attempt = 1; attempt <= attempts; attempt++)); do
    if "$@"; then
      echo "PASS: $description"
      return 0
    fi
    echo "Waiting: $description ($attempt/$attempts)"
    sleep "$delay_seconds"
  done

  echo "FAIL: $description"
  return 1
}

# The NEW version is running, rather than the old one still answering.
new_version_is_live() {
  curl --silent --fail --max-time 30 "$url/version" | grep --quiet "\"version\":\"$expected_version\""
}

is_healthy() {
  curl --silent --fail --max-time 30 --output /dev/null "$url/health"
}

# Ordering a product that doesn't exist makes Orders call Catalog without creating
# anything. Only a real answer from Catalog produces "product_not_found".
orders_can_reach_catalog() {
  curl --silent --max-time 60 "$url/orders" \
    --header "Content-Type: application/json" \
    --data '{"productId": 0, "quantity": 1}' | grep --quiet "product_not_found"
}

retry "version $expected_version is live" new_version_is_live
retry "Orders is healthy" is_healthy
retry "Orders can reach Catalog" orders_can_reach_catalog
