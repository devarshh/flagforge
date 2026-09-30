// A Basic container registry. Access is through Entra ID only (the kubelet identity pulls, GitHub Actions pushes).

param namePrefix string
param location string

// Registry names are global, alphanumeric, and 5 to 50 characters.
var registryName = take('${replace(namePrefix, '-', '')}${uniqueString(resourceGroup().id)}', 50)

resource registry 'Microsoft.ContainerRegistry/registries@2025-11-01' = {
  name: registryName
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: false
  }
}

output name string = registry.name
output loginServer string = registry.properties.loginServer
