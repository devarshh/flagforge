// Parameters for scripts/azure-deploy-infra.sh, which exports the environment variables read here. Secrets never
// live in this file.
using 'main.bicep'

param namePrefix = 'flagforge'
param githubRepository = readEnvironmentVariable('GITHUB_REPOSITORY', 'OWNER/flagforge')
param sqlAdminLogin = readEnvironmentVariable('SQL_ADMIN_LOGIN', 'flagforgeadmin')
param sqlAdminPassword = readEnvironmentVariable('SQL_ADMIN_PASSWORD')
param sqlUseFreeLimit = bool(readEnvironmentVariable('SQL_USE_FREE_LIMIT', 'true'))
