#!/usr/bin/env bash
# Deploys infra/main.bicep (registry, AKS, Azure SQL, and the GitHub deployment identity) into a resource group, then
# prints the `gh variable set` and `gh secret set` commands that connect the repository to it.
#
# Usage:
#   export SQL_ADMIN_PASSWORD='...'                  # required; keep it: the SQL connection string needs it
#   scripts/azure-deploy-infra.sh
#
# Optional environment variables:
#   RESOURCE_GROUP      default flagforge-rg
#   LOCATION            default eastus (only used when the resource group is created)
#   GITHUB_REPOSITORY   owner/name; default taken from the git remote "origin"
#   SQL_ADMIN_LOGIN     default flagforgeadmin
#   SQL_USE_FREE_LIMIT  default true (the Azure SQL free offer; one free database per subscription)
#
# See docs/azure-setup.md for the whole setup, including costs.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

if ! command -v az > /dev/null 2>&1; then
  echo "The Azure CLI is required: https://learn.microsoft.com/cli/azure/install-azure-cli" >&2
  exit 1
fi

if ! az account show > /dev/null 2>&1; then
  echo "Sign in first: az login (and az account set --subscription <id> to pick a subscription)." >&2
  exit 1
fi

resource_group="${RESOURCE_GROUP:-flagforge-rg}"
location="${LOCATION:-eastus}"

if [[ -z "${GITHUB_REPOSITORY:-}" ]]; then
  origin="$(git remote get-url origin 2> /dev/null || true)"
  # git@github.com:owner/name.git or https://github.com/owner/name(.git)
  GITHUB_REPOSITORY="$(printf '%s' "$origin" | sed -E -n 's#^(git@github\.com:|https://github\.com/)([^/]+/[^/]+)$#\2#p' | sed -E 's#\.git$##')"
fi

if [[ ! "${GITHUB_REPOSITORY:-}" =~ ^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$ ]]; then
  echo "Set GITHUB_REPOSITORY to your repository (owner/name); it could not be read from the git remote." >&2
  exit 1
fi

if [[ -z "${SQL_ADMIN_PASSWORD:-}" ]]; then
  echo "Set SQL_ADMIN_PASSWORD first, for example:" >&2
  echo "  export SQL_ADMIN_PASSWORD=\"\$(openssl rand -base64 24 | tr -d '/+=')-Aa1\"" >&2
  exit 1
fi

export GITHUB_REPOSITORY
export SQL_ADMIN_LOGIN="${SQL_ADMIN_LOGIN:-flagforgeadmin}"
export SQL_USE_FREE_LIMIT="${SQL_USE_FREE_LIMIT:-true}"

if [[ "$(az group exists --name "$resource_group")" != "true" ]]; then
  echo "Creating resource group $resource_group in $location"
  az group create --name "$resource_group" --location "$location" --output none
fi

echo "Deploying infra/main.bicep to $resource_group (this takes several minutes)"
az deployment group create \
  --resource-group "$resource_group" \
  --name flagforge-infra \
  --parameters infra/main.bicepparam \
  --output none

output() {
  az deployment group show --resource-group "$resource_group" --name flagforge-infra \
    --query "properties.outputs.$1.value" --output tsv
}

acr_name="$(output acrName)"
aks_name="$(output aksName)"
sql_fqdn="$(output sqlServerFqdn)"
sql_database="$(output sqlDatabaseName)"
client_id="$(output githubClientId)"
tenant_id="$(output tenantId)"
subscription_id="$(output subscriptionId)"

cat << COMMANDS

Deployed. Run these commands to connect $GITHUB_REPOSITORY (GitHub CLI, signed in with access to the repository).
Secret values are generated or read from your shell, so they are not printed here.

gh variable set AZURE_CLIENT_ID --repo "$GITHUB_REPOSITORY" --body "$client_id"
gh variable set AZURE_TENANT_ID --repo "$GITHUB_REPOSITORY" --body "$tenant_id"
gh variable set AZURE_SUBSCRIPTION_ID --repo "$GITHUB_REPOSITORY" --body "$subscription_id"
gh variable set AZURE_RESOURCE_GROUP --repo "$GITHUB_REPOSITORY" --body "$resource_group"
gh variable set ACR_NAME --repo "$GITHUB_REPOSITORY" --body "$acr_name"
gh variable set AKS_NAME --repo "$GITHUB_REPOSITORY" --body "$aks_name"

gh secret set SQL_CONNECTION_STRING --repo "$GITHUB_REPOSITORY" --body "Server=tcp:$sql_fqdn,1433;Database=$sql_database;User ID=$SQL_ADMIN_LOGIN;Password=\$SQL_ADMIN_PASSWORD;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60"
gh secret set JWT_SIGNING_KEY --repo "$GITHUB_REPOSITORY" --body "\$(openssl rand -base64 48)"
gh secret set DEMO_SDK_KEY --repo "$GITHUB_REPOSITORY" --body "ffk_\$(openssl rand -hex 24)"
gh secret set SEED_ADMIN_PASSWORD --repo "$GITHUB_REPOSITORY"   # prompts; at least 10 characters
gh secret set DEMO_USER_PASSWORD --repo "$GITHUB_REPOSITORY"    # prompts; at least 10 characters

Run them in this shell, where SQL_ADMIN_PASSWORD is set. Next: create the "production" environment in the repository
settings, run the bootstrap-cluster workflow, and push to main (docs/azure-setup.md).
COMMANDS
