#!/usr/bin/env bash
# Creates everything Resell Tracker needs in Azure, once. Run it in Azure Cloud
# Shell (shell.azure.com, Bash), where you are already signed in:
#
#   curl -fsSL https://raw.githubusercontent.com/nfb1799/resell-tracker-vue/main/infra/azure-setup.sh -o setup.sh
#   bash setup.sh
#
# It creates, in one resource group:
#   - an Azure SQL logical server and a database on the free offer
#     (100,000 vCore-seconds and 32 GB a month; pauses itself if that runs out,
#     rather than billing)
#   - a Container Apps environment and the app, scaling to zero when idle,
#     running the image CI publishes to GitHub's registry
#   - an Entra app registration that GitHub Actions signs in as through OIDC,
#     allowed to manage this resource group only
#
# Before running: the image must exist and be public. Push to main once (CI
# publishes ghcr.io/nfb1799/resell-tracker-vue), then on GitHub open the
# package's settings and set its visibility to Public.
#
# It ends by printing the repository variables the deploy job reads.

set -euo pipefail

REPO="nfb1799/resell-tracker-vue"
IMAGE="ghcr.io/nfb1799/resell-tracker-vue:latest"
LOCATION="${LOCATION:-eastus}"
RG="${RG:-resell-tracker}"
SUFFIX="$(openssl rand -hex 3)"
SQL_SERVER="resell-tracker-sql-${SUFFIX}"
DB="resell-tracker"
ENV_NAME="resell-tracker-env"
APP="resell-tracker"
SQL_ADMIN="rtadmin"
# Random, used once below and kept only as a Container Apps secret. If you ever
# need to query the database yourself, reset it from the SQL server in the portal.
SQL_PASSWORD="$(openssl rand -base64 48 | tr -dc 'A-Za-z0-9' | head -c 32)Aa1!"

az config set extension.use_dynamic_install=yes_without_prompt --only-show-errors >/dev/null

read -rsp "Resend API key (input hidden; press Enter to skip and add it later): " RESEND_API_KEY
echo

SUBSCRIPTION_ID="$(az account show --query id -o tsv)"
TENANT_ID="$(az account show --query tenantId -o tsv)"
echo "Using subscription ${SUBSCRIPTION_ID} in ${LOCATION}."

echo "==> Registering the resource providers this needs"
for ns in Microsoft.App Microsoft.OperationalInsights Microsoft.Sql; do
  az provider register --namespace "$ns" --wait
done

echo "==> Resource group ${RG}"
az group create --name "$RG" --location "$LOCATION" --output none

echo "==> SQL server ${SQL_SERVER} and the free database"
az sql server create --resource-group "$RG" --name "$SQL_SERVER" --location "$LOCATION" \
  --admin-user "$SQL_ADMIN" --admin-password "$SQL_PASSWORD" --minimal-tls-version 1.2 --output none
# The special 0.0.0.0 rule admits Azure services (the container app), not the internet.
az sql server firewall-rule create --resource-group "$RG" --server "$SQL_SERVER" --name AllowAzureServices \
  --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0 --output none
az sql db create --resource-group "$RG" --server "$SQL_SERVER" --name "$DB" \
  --edition GeneralPurpose --family Gen5 --capacity 2 --compute-model Serverless \
  --use-free-limit --free-limit-exhaustion-behavior AutoPause --output none

CONNECTION="Server=tcp:${SQL_SERVER}.database.windows.net,1433;Database=${DB};User ID=${SQL_ADMIN};Password=${SQL_PASSWORD};Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;"

echo "==> Container Apps environment ${ENV_NAME}"
az containerapp env create --resource-group "$RG" --name "$ENV_NAME" --location "$LOCATION" --output none

echo "==> Container app ${APP}"
SECRETS=("sql-connection=${CONNECTION}")
ENV_VARS=(
  "ConnectionStrings__Default=secretref:sql-connection"
  "Database__MigrateOnStartup=true"
  "Proxy__TrustForwardedHeaders=true"
)
if [[ -n "$RESEND_API_KEY" ]]; then
  SECRETS+=("resend-api-key=${RESEND_API_KEY}")
  ENV_VARS+=("Email__ResendApiKey=secretref:resend-api-key")
fi
az containerapp create --resource-group "$RG" --name "$APP" --environment "$ENV_NAME" \
  --image "$IMAGE" --target-port 8080 --ingress external \
  --min-replicas 0 --max-replicas 1 --cpu 0.25 --memory 0.5Gi \
  --secrets "${SECRETS[@]}" --env-vars "${ENV_VARS[@]}" --output none

FQDN="$(az containerapp show --resource-group "$RG" --name "$APP" --query properties.configuration.ingress.fqdn -o tsv)"
APP_URL="https://${FQDN}"
# Password reset links point here, never at whatever Host header a request carries.
az containerapp update --resource-group "$RG" --name "$APP" --set-env-vars "App__BaseUrl=${APP_URL}" --output none

echo "==> Letting GitHub Actions deploy (OIDC: no password stored in GitHub)"
CLIENT_ID="$(az ad app create --display-name "resell-tracker-github-deploy" --query appId -o tsv)"
az ad sp create --id "$CLIENT_ID" --output none
# The deploy job runs in the "production" environment, which is what this trusts.
az ad app federated-credential create --id "$CLIENT_ID" --parameters "{
  \"name\": \"github-production\",
  \"issuer\": \"https://token.actions.githubusercontent.com\",
  \"subject\": \"repo:${REPO}:environment:production\",
  \"audiences\": [\"api://AzureADTokenExchange\"]
}" --output none
# A new service principal can take a moment to be visible to role assignment.
for attempt in 1 2 3 4 5 6; do
  if az role assignment create --assignee "$CLIENT_ID" --role Contributor \
      --scope "/subscriptions/${SUBSCRIPTION_ID}/resourceGroups/${RG}" --output none 2>/dev/null; then
    break
  fi
  [[ $attempt -eq 6 ]] && { echo "Could not grant the deploy identity access; re-run the role assignment by hand." >&2; exit 1; }
  sleep 10
done

cat <<EOF

Done. The app is at ${APP_URL}
(the first request wakes it, and the database, which can take a minute).

Set these as GitHub repository variables (Settings > Secrets and variables >
Actions > Variables). None of them is secret:

  AZURE_CLIENT_ID=${CLIENT_ID}
  AZURE_TENANT_ID=${TENANT_ID}
  AZURE_SUBSCRIPTION_ID=${SUBSCRIPTION_ID}
  AZURE_RESOURCE_GROUP=${RG}
  AZURE_CONTAINER_APP=${APP}
  APP_URL=${APP_URL}
EOF
