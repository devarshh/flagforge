#!/usr/bin/env bash
# Creates .env from .env.example with a random JWT signing key and a random SQL Server SA password.
#
# Usage: scripts/gen-dev-secrets.sh [--force]
#
# The SA password takes effect only when SQL Server creates its volume, so remove an existing one first
# (docker compose down -v, which deletes the local database).
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
example="$repo_root/.env.example"
target="$repo_root/.env"

if [[ -e "$target" && "${1:-}" != "--force" ]]; then
  echo ".env already exists. Run with --force to replace it." >&2
  exit 1
fi

# Letters and digits only, so the values need no quoting in .env files or connection strings.
random_alnum() {
  local length="$1" value=""
  while [[ ${#value} -lt $length ]]; do
    if command -v openssl > /dev/null 2>&1; then
      value+="$(openssl rand -base64 48 | LC_ALL=C tr -dc 'A-Za-z0-9')"
    else
      value+="$(LC_ALL=C tr -dc 'A-Za-z0-9' < /dev/urandom | head -c 64 || true)"
    fi
  done
  printf '%s' "${value:0:$length}"
}

jwt_signing_key="$(random_alnum 64)"
# SQL Server wants three of: uppercase, lowercase, digits, symbols. The fixed parts guarantee all four.
sa_password="Ff-$(random_alnum 24)-9"

awk -v sa="$sa_password" -v jwt="$jwt_signing_key" '
  /^MSSQL_SA_PASSWORD=/ { print "MSSQL_SA_PASSWORD=" sa; next }
  /^JWT_SIGNING_KEY=/ { print "JWT_SIGNING_KEY=" jwt; next }
  { print }
' "$example" > "$target.tmp"
mv "$target.tmp" "$target"
chmod 600 "$target"

echo "Wrote .env with a random JWT signing key and SQL Server SA password."
echo "If a SQL Server volume already exists, run 'docker compose down -v' so the new password takes effect."
