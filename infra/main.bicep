// FlagForge on Azure: a container registry, an AKS cluster, Azure SQL (serverless), and the managed identity that
// GitHub Actions uses to deploy. Deploy with scripts/azure-deploy-infra.sh (docs/azure-setup.md).
targetScope = 'resourceGroup'

@description('Prefix for resource names: lowercase letters, digits, and hyphens.')
@minLength(3)
@maxLength(16)
param namePrefix string = 'flagforge'

@description('Azure region. Defaults to the resource group\'s region.')
param location string = resourceGroup().location

@description('The GitHub repository allowed to deploy, as owner/name.')
param githubRepository string

@description('SQL Server administrator login.')
param sqlAdminLogin string

@description('SQL Server administrator password.')
@secure()
param sqlAdminPassword string

@description('Use the Azure SQL Database free offer. A subscription has one free database; set false if it is taken.')
param sqlUseFreeLimit bool = true

@description('VM size of the AKS node pool. The node VM is the main cost of this deployment.')
param aksNodeVmSize string = 'Standard_B2ms'

@description('Minimum node count for the cluster autoscaler.')
@minValue(1)
param aksMinNodes int = 1

@description('Maximum node count for the cluster autoscaler.')
@minValue(1)
param aksMaxNodes int = 3

module acr 'modules/acr.bicep' = {
  name: 'acr'
  params: {
    namePrefix: namePrefix
    location: location
  }
}

module aks 'modules/aks.bicep' = {
  name: 'aks'
  params: {
    namePrefix: namePrefix
    location: location
    nodeVmSize: aksNodeVmSize
    minNodes: aksMinNodes
    maxNodes: aksMaxNodes
    acrName: acr.outputs.name
  }
}

module sql 'modules/sql.bicep' = {
  name: 'sql'
  params: {
    namePrefix: namePrefix
    location: location
    adminLogin: sqlAdminLogin
    adminPassword: sqlAdminPassword
    useFreeLimit: sqlUseFreeLimit
  }
}

module githubIdentity 'modules/github-identity.bicep' = {
  name: 'github-identity'
  params: {
    namePrefix: namePrefix
    location: location
    githubRepository: githubRepository
    acrName: acr.outputs.name
    aksName: aks.outputs.name
  }
}

output acrName string = acr.outputs.name
output acrLoginServer string = acr.outputs.loginServer
output aksName string = aks.outputs.name
output sqlServerFqdn string = sql.outputs.serverFqdn
output sqlDatabaseName string = sql.outputs.databaseName
output githubClientId string = githubIdentity.outputs.clientId
output tenantId string = tenant().tenantId
output subscriptionId string = subscription().subscriptionId
