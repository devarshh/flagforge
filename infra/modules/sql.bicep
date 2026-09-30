// Azure SQL: a logical server with SQL authentication and a serverless General Purpose database that pauses when
// idle. With useFreeLimit, the database uses the Azure SQL Database free offer and pauses when the monthly free
// amount runs out instead of billing for more.

param namePrefix string
param location string
param adminLogin string

@secure()
param adminPassword string

param useFreeLimit bool

resource server 'Microsoft.Sql/servers@2025-01-01' = {
  name: '${namePrefix}-sql-${uniqueString(resourceGroup().id)}'
  location: location
  properties: {
    administratorLogin: adminLogin
    administratorLoginPassword: adminPassword
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    version: '12.0'
  }
}

// The 0.0.0.0 rule is Azure's switch for "allow Azure services", which lets the AKS nodes connect.
resource allowAzureServices 'Microsoft.Sql/servers/firewallRules@2025-01-01' = {
  parent: server
  name: 'AllowAllWindowsAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource database 'Microsoft.Sql/servers/databases@2025-01-01' = {
  parent: server
  name: 'flagforge'
  location: location
  sku: {
    name: 'GP_S_Gen5'
    tier: 'GeneralPurpose'
    family: 'Gen5'
    capacity: 2
  }
  properties: {
    minCapacity: json('0.5')
    autoPauseDelay: 60
    maxSizeBytes: 34359738368
    zoneRedundant: false
    requestedBackupStorageRedundancy: 'Local'
    useFreeLimit: useFreeLimit
    freeLimitExhaustionBehavior: useFreeLimit ? 'AutoPause' : null
  }
}

output serverFqdn string = server.properties.fullyQualifiedDomainName
output databaseName string = database.name
